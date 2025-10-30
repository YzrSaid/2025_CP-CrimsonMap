using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;

public class DirectionGenerator : MonoBehaviour
{
    [Header("Settings")]
    public float minSegmentDistance = 5f;
    public float turnThresholdDegrees = 15f;
    public float floorHeightMeters = 3.048f;

    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>();
    private Dictionary<string, IndoorInfrastructure> indoorData = new Dictionary<string, IndoorInfrastructure>();
    private bool dataLoaded = false;
    private bool isLoading = false;

    void Awake()
    {
        // Pre-load data on Awake so it's ready when needed
        StartCoroutine(PreloadData());
    }

    private IEnumerator PreloadData()
    {
        if (isLoading)
        {
            Debug.Log("[DirectionGenerator] Already loading data, waiting...");
            yield return new WaitUntil(() => dataLoaded);
            yield break;
        }

        isLoading = true;
        yield return StartCoroutine(LoadIndoorDataIfNeeded());
        dataLoaded = true;
        isLoading = false;
        Debug.Log("[DirectionGenerator] ✅ Data preloaded successfully");
    }

    public List<NavigationDirection> GenerateDirections(RouteData route)
    {
        if (route == null || route.path == null || route.path.Count < 1)
        {
            Debug.LogWarning("[DirectionGenerator] Invalid route data");
            return new List<NavigationDirection>();
        }

        // ✅ If data not loaded yet, wait for it (don't use synchronous loading!)
        if (!dataLoaded || allNodes.Count == 0)
        {
            Debug.LogError("[DirectionGenerator] ❌ Data not loaded! Cannot generate directions.");
            Debug.LogError("[DirectionGenerator] This should never happen - data should be preloaded in Awake!");
            return new List<NavigationDirection>();
        }

        var directions = new List<NavigationDirection>();

        // Check if this is an indoor destination
        string originalFromId = PlayerPrefs.GetString("ARNavigation_OriginalFromNodeId", "");
        string originalToId = PlayerPrefs.GetString("ARNavigation_OriginalToNodeId", "");
        bool fromIsIndoor = PlayerPrefs.GetInt("ARNavigation_FromIsIndoor", 0) == 1;
        bool toIsIndoor = PlayerPrefs.GetInt("ARNavigation_ToIsIndoor", 0) == 1;
        bool isSameBuilding = PlayerPrefs.GetString("ARNavigation_SameBuilding", "false") == "true";

        Debug.Log($"[DirectionGenerator] Generating directions:");
        Debug.Log($"  FROM: {originalFromId} (Indoor: {fromIsIndoor})");
        Debug.Log($"  TO: {originalToId} (Indoor: {toIsIndoor})");
        Debug.Log($"  Same Building: {isSameBuilding}");

        // ✅ SPECIAL CASE: Same building navigation (outdoor to indoor, same building)
        if (isSameBuilding && toIsIndoor)
        {
            Debug.Log("[DirectionGenerator] Same building - generating indoor-only directions");

            if (!string.IsNullOrEmpty(originalToId))
            {
                if (allNodes.TryGetValue(originalToId, out Node toNode))
                {
                    directions.AddRange(GenerateSameBuildingDirections(toNode));
                    Debug.Log($"[DirectionGenerator] ✅ Generated {directions.Count} indoor directions");
                }
                else
                {
                    Debug.LogError($"[DirectionGenerator] ❌ TO node not found: {originalToId}");
                }
            }

            return directions;
        }

        // Generate exit directions if starting from indoor
        if (fromIsIndoor && !string.IsNullOrEmpty(originalFromId))
        {
            directions.AddRange(GenerateIndoorExitDirections(originalFromId));
        }

        // Generate outdoor path directions (normal A* route)
        var pathNodes = route.path;

        if (pathNodes.Count >= 2)
        {
            var segments = GroupPathIntoSegments(pathNodes);

            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];

                if (segment.Count < 2)
                    continue;

                float segmentHeading = CalculateHeading(segment[0], segment[segment.Count - 1]);

                TurnDirection turn = TurnDirection.Straight;
                if (i > 0)
                {
                    var prevSegment = segments[i - 1];
                    float prevHeading = CalculateHeading(prevSegment[0], prevSegment[prevSegment.Count - 1]);
                    turn = DetermineTurn(prevHeading, segmentHeading);
                }

                Node destNode = segment[segment.Count - 1].node;
                string destName = destNode.name;

                float segmentDistance = CalculateSegmentDistance(segment);

                var direction = new NavigationDirection
                {
                    turn = turn,
                    instruction = BuildInstruction(turn, destName, segmentDistance),
                    destinationNode = destNode,
                    distanceInMeters = segmentDistance,
                    heading = segmentHeading,
                    pathNodes = segment,
                    isIndoorDirection = false
                };

                directions.Add(direction);
            }
        }

        // Generate entry directions if ending at indoor location
        if (toIsIndoor && !string.IsNullOrEmpty(originalToId))
        {
            directions.AddRange(GenerateIndoorEntryDirections(originalToId));
        }

        return directions;
    }

    // ✅ NEW: Generate directions for same building (outdoor to indoor)
    private List<NavigationDirection> GenerateSameBuildingDirections(Node toNode)
    {
        var directions = new List<NavigationDirection>();

        if (toNode.type != "indoorinfra" || toNode.indoor == null)
        {
            Debug.LogWarning($"[DirectionGenerator] Invalid indoor node: {toNode.name}");
            return directions;
        }

        string buildingName = GetBuildingName(toNode.related_infra_id);
        string roomName = toNode.name;
        int targetFloor = int.Parse(toNode.indoor.floor);

        Debug.Log($"[DirectionGenerator] Building: {buildingName}, Room: {roomName}, Floor: {targetFloor}");

        // Entry direction
        string entryInstruction = $"Enter {buildingName}";

        // Add floor navigation if not on ground floor
        if (targetFloor > 1)
        {
            List<string> floorInstructions = new List<string>();

            for (int floor = 1; floor < targetFloor; floor++)
            {
                floorInstructions.Add($"Floor {floor}");
            }

            string floorText = string.Join(" and ", floorInstructions);
            entryInstruction += $". Navigate to {roomName}. Room is on Floor {targetFloor}, look for stairs on {floorText}.";
        }
        else
        {
            entryInstruction += $". Navigate to {roomName}. Room is on Floor {targetFloor}.";
        }

        Debug.Log($"[DirectionGenerator] Generated instruction: {entryInstruction}");

        directions.Add(new NavigationDirection
        {
            turn = TurnDirection.Enter,
            instruction = entryInstruction,
            destinationNode = toNode,
            distanceInMeters = 0f,
            heading = 0f,
            isIndoorDirection = true,
            isIndoorGrouped = true
        });

        return directions;
    }

    private IEnumerator LoadIndoorDataIfNeeded()
    {
        if (dataLoaded && indoorData.Count > 0 && allNodes.Count > 0)
        {
            yield break;
        }

        bool loadComplete = false;

        // Load indoor.json
        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "indoor.json",
            (jsonContent) =>
            {
                try
                {
                    IndoorInfrastructure[] indoorArray = JsonHelper.FromJson<IndoorInfrastructure>(jsonContent);
                    indoorData.Clear();

                    foreach (var indoor in indoorArray)
                    {
                        if (!indoor.is_deleted)
                        {
                            indoorData[indoor.room_id] = indoor;
                        }
                    }

                    loadComplete = true;
                    Debug.Log($"[DirectionGenerator] ✅ Loaded {indoorData.Count} indoor infrastructures");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DirectionGenerator] Error loading indoor data: {e.Message}");
                    loadComplete = true;
                }
            },
            (error) =>
            {
                Debug.LogError($"[DirectionGenerator] Failed to load indoor.json: {error}");
                loadComplete = true;
            }
        ));

        yield return new WaitUntil(() => loadComplete);

        // Load all nodes
        string mapId = PlayerPrefs.GetString("ARScene_MapId", "MAP-01");
        string nodesFile = $"nodes_{mapId}.json";
        loadComplete = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            nodesFile,
            (jsonContent) =>
            {
                try
                {
                    Node[] nodes = JsonHelper.FromJson<Node>(jsonContent);
                    allNodes.Clear();

                    foreach (var node in nodes)
                    {
                        allNodes[node.node_id] = node;
                    }

                    loadComplete = true;
                    Debug.Log($"[DirectionGenerator] ✅ Loaded {allNodes.Count} nodes");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DirectionGenerator] Error loading nodes: {e.Message}");
                    loadComplete = true;
                }
            },
            (error) =>
            {
                Debug.LogError($"[DirectionGenerator] Failed to load nodes: {error}");
                loadComplete = true;
            }
        ));

        yield return new WaitUntil(() => loadComplete);
    }

    private List<NavigationDirection> GenerateIndoorExitDirections(string fromIndoorNodeId)
    {
        var directions = new List<NavigationDirection>();

        if (!allNodes.TryGetValue(fromIndoorNodeId, out Node fromNode))
        {
            return directions;
        }

        if (fromNode.type != "indoorinfra")
        {
            return directions;
        }

        string buildingName = GetBuildingName(fromNode.related_infra_id);

        directions.Add(new NavigationDirection
        {
            turn = TurnDirection.Enter,
            instruction = $"Exit {buildingName} and head outside",
            destinationNode = fromNode,
            distanceInMeters = 0f,
            heading = 0f,
            isIndoorDirection = true,
            isIndoorGrouped = true
        });

        return directions;
    }

    private List<NavigationDirection> GenerateIndoorEntryDirections(string toIndoorNodeId)
    {
        var directions = new List<NavigationDirection>();

        if (!allNodes.TryGetValue(toIndoorNodeId, out Node toNode))
        {
            return directions;
        }

        if (toNode.type != "indoorinfra" || toNode.indoor == null)
        {
            return directions;
        }

        string buildingName = GetBuildingName(toNode.related_infra_id);
        string roomName = toNode.name;
        int targetFloor = int.Parse(toNode.indoor.floor);

        string entryInstruction = $"Enter {buildingName}";

        if (targetFloor > 1)
        {
            List<string> floorInstructions = new List<string>();

            for (int floor = 1; floor < targetFloor; floor++)
            {
                floorInstructions.Add($"Floor {floor}");
            }

            string floorText = string.Join(" and ", floorInstructions);
            entryInstruction += $". Navigate to {roomName}. Room is on Floor {targetFloor}, look for stairs on {floorText}.";
        }
        else
        {
            entryInstruction += $". Navigate to {roomName}. Room is on Floor {targetFloor}.";
        }

        directions.Add(new NavigationDirection
        {
            turn = TurnDirection.Enter,
            instruction = entryInstruction,
            destinationNode = toNode,
            distanceInMeters = 0f,
            heading = 0f,
            isIndoorDirection = true,
            isIndoorGrouped = true
        });

        return directions;
    }

    private string GetBuildingName(string infraId)
    {
        var infraNode = allNodes.Values.FirstOrDefault(n =>
            n.type == "infrastructure" && n.related_infra_id == infraId);

        if (infraNode != null)
        {
            return infraNode.name;
        }

        return infraId;
    }

    private List<List<PathNode>> GroupPathIntoSegments(List<PathNode> pathNodes)
    {
        var segments = new List<List<PathNode>>();
        var currentSegment = new List<PathNode> { pathNodes[0] };
        float accumulatedDistance = 0f;

        for (int i = 1; i < pathNodes.Count; i++)
        {
            float dist = Vector3.Distance(pathNodes[i].worldPosition, pathNodes[i - 1].worldPosition);
            accumulatedDistance += dist;

            currentSegment.Add(pathNodes[i]);

            if (accumulatedDistance >= minSegmentDistance && i < pathNodes.Count - 1)
            {
                segments.Add(new List<PathNode>(currentSegment));
                currentSegment.Clear();
                currentSegment.Add(pathNodes[i]);
                accumulatedDistance = 0f;
            }
        }

        if (currentSegment.Count > 0)
        {
            segments.Add(currentSegment);
        }

        if (segments.Count == 0 && pathNodes.Count >= 2)
        {
            segments.Add(new List<PathNode> { pathNodes[0], pathNodes[pathNodes.Count - 1] });
        }

        return segments;
    }

    private float CalculateHeading(PathNode from, PathNode to)
    {
        Vector3 direction = to.worldPosition - from.worldPosition;
        float heading = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        if (heading < 0)
            heading += 360f;

        return heading;
    }

    private TurnDirection DetermineTurn(float previousHeading, float currentHeading)
    {
        float headingDiff = currentHeading - previousHeading;

        while (headingDiff > 180)
            headingDiff -= 360;
        while (headingDiff < -180)
            headingDiff += 360;

        if (Mathf.Abs(headingDiff) < turnThresholdDegrees)
        {
            return TurnDirection.Straight;
        }
        else if (headingDiff > 0)
        {
            return TurnDirection.Right;
        }
        else
        {
            return TurnDirection.Left;
        }
    }

    private float CalculateSegmentDistance(List<PathNode> segment)
    {
        float distance = 0f;

        for (int i = 0; i < segment.Count - 1; i++)
        {
            distance += Vector3.Distance(segment[i].worldPosition, segment[i + 1].worldPosition);
        }

        return distance;
    }

    private string BuildInstruction(TurnDirection turn, string destination, float distance)
    {
        string distanceText = FormatDistance(distance);

        switch (turn)
        {
            case TurnDirection.Straight:
                return $"Head straight to {destination} ({distanceText})";

            case TurnDirection.Left:
            case TurnDirection.SlightLeft:
                return $"Turn left toward {destination} ({distanceText})";

            case TurnDirection.Right:
            case TurnDirection.SlightRight:
                return $"Turn right toward {destination} ({distanceText})";

            case TurnDirection.Enter:
                return $"Enter {destination} ({distanceText})";

            case TurnDirection.Arrive:
                return $"Arrive at {destination}";

            default:
                return $"Go to {destination} ({distanceText})";
        }
    }

    private string FormatDistance(float meters)
    {
        if (meters < 1000)
            return $"{meters:F0}m";
        else
            return $"{meters / 1000:F2}km";
    }

    public string GetTurnSymbol(TurnDirection turn)
    {
        return turn switch
        {
            TurnDirection.Straight => "↑",
            TurnDirection.Left => "←",
            TurnDirection.Right => "→",
            TurnDirection.SlightLeft => "↖",
            TurnDirection.SlightRight => "↗",
            TurnDirection.Enter => "🚪",
            TurnDirection.Arrive => "🎯",
            _ => "?"
        };
    }

    public bool IsDataLoaded()
    {
        return dataLoaded && allNodes.Count > 0 && indoorData.Count > 0;
    }

    public string GetTurnText(TurnDirection turn)
    {
        return turn switch
        {
            TurnDirection.Straight => "Straight",
            TurnDirection.Left => "Left",
            TurnDirection.Right => "Right",
            TurnDirection.SlightLeft => "Slight Left",
            TurnDirection.SlightRight => "Slight Right",
            TurnDirection.Enter => "Enter",
            TurnDirection.Arrive => "Arrive",
            _ => "Unknown"
        };
    }
}

[System.Serializable]
public class NavigationDirection
{
    public TurnDirection turn;
    public string instruction;
    public Node destinationNode;
    public float distanceInMeters;
    public float heading;
    public List<PathNode> pathNodes;
    public bool isIndoorDirection;
    public bool isIndoorGrouped;

    public override string ToString()
    {
        return instruction;
    }

}