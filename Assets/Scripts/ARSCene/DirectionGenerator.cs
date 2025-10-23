using System.Collections.Generic;
using UnityEngine;

public class DirectionGenerator : MonoBehaviour
{
    [Header("Settings")]
    public float minSegmentDistance = 5f;
    public float turnThresholdDegrees = 15f;

    public List<NavigationDirection> GenerateDirections(RouteData route)
    {
        if (route == null || route.path == null || route.path.Count < 2)
        {
            return new List<NavigationDirection>();
        }

        var directions = new List<NavigationDirection>();
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
                pathNodes = segment
            };

            directions.Add(direction);
        }

        return directions;
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
    
    public override string ToString()
    {
        return instruction;
    }
}