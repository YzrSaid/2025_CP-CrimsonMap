using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DirectionGenerator : MonoBehaviour
{
    [Header("Settings")]
    public float minSegmentDistance = 5f;
    public float turnThresholdDegrees = 15f;
    public float floorHeightMeters = 3.048f; // 10 feet per floor

    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>();
    private Dictionary<string, IndoorInfrastructure> indoorData = new Dictionary<string, IndoorInfrastructure>();

    public List<NavigationDirection> GenerateDirections(RouteData route)
    {
        if (route == null || route.path == null || route.path.Count < 2)
        {
            return new List<NavigationDirection>();
        }

        var directions = new List<NavigationDirection>();

        // Load indoor data if needed
        StartCoroutine(LoadIndoorDataIfNeeded());

        // Check if this is an indoor destination
        string originalFromId = PlayerPrefs.GetString("ARNavigation_OriginalFromNodeId", "");
        string originalToId = PlayerPrefs.GetString("ARNavigation_OriginalToNodeId", "");
        bool fromIsIndoor = PlayerPrefs.GetInt("ARNavigation_FromIsIndoor", 0) == 1;
        bool toIsIndoor = PlayerPrefs.GetInt("ARNavigation_ToIsIndoor", 0) == 1;

        // Generate exit directions if starting from indoor
        if (fromIsIndoor && !string.IsNullOrEmpty(originalFromId))
        {
            directions.AddRange(GenerateIndoorExitDirections(originalFromId));
        }

        // Generate outdoor path directions (normal A* route)
        var pathNodes = route.path;
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

        // Generate entry directions if ending at indoor location
        if (toIsIndoor && !string.IsNullOrEmpty(originalToId))
        {
            directions.AddRange(GenerateIndoorEntryDirections(originalToId));
        }

        return directions;
    }

    private System.Collections.IEnumerator LoadIndoorDataIfNeeded()
    {
        if (indoorData.Count > 0)
        {
            yield break; // Already loaded
        }

        bool loadComplete = false;

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
    }

    // Generate directions for exiting indoor location
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

    // Generate directions for entering indoor location
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
            entryInstruction += $". Navigate to {roomName}.";
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
        // Try to find infrastructure name from nodes
        var infraNode = allNodes.Values.FirstOrDefault(n => 
            n.type == "infrastructure" && n.related_infra_id == infraId);
        
        if (infraNode != null)
        {
            return infraNode.name;
        }

        // Fallback to infra_id
        return infraId;
    }

    // Rest of the methods remain the same...
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

        if (segments.Count == 0)
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
    public bool isIndoorDirection; // NEW: Flag for indoor directions
    public bool isIndoorGrouped; // NEW: Flag to group indoor directions together
    
    public override string ToString()
    {
        return instruction;
    }
}