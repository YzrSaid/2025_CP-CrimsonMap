using System.Collections.Generic;
using UnityEngine;

public class DirectionGenerator : MonoBehaviour
{
    [Header("Settings")]
    public float minSegmentDistance = 5f; // Minimum distance to group waypoints
    public float turnThresholdDegrees = 15f; // Angle difference to consider it a turn
    
    public enum TurnDirection { Straight, TurnLeft, TurnRight, Continue }

    /// <summary>
    /// Generate human-readable directions from a route
    /// </summary>
    public List<NavigationDirection> GenerateDirections(RouteData route)
    {
        if (route == null || route.path == null || route.path.Count < 2)
        {
            return new List<NavigationDirection>();
        }

        var directions = new List<NavigationDirection>();
        var pathNodes = route.path;

        // Group consecutive nodes into segments
        var segments = GroupPathIntoSegments(pathNodes);

        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            
            if (segment.Count < 2)
                continue;

            // Calculate bearing/heading for this segment
            float segmentHeading = CalculateHeading(segment[0], segment[segment.Count - 1]);

            // Determine turn direction from previous segment
            TurnDirection turn = TurnDirection.Straight;
            if (i > 0)
            {
                var prevSegment = segments[i - 1];
                float prevHeading = CalculateHeading(prevSegment[0], prevSegment[prevSegment.Count - 1]);
                turn = DetermineTurn(prevHeading, segmentHeading);
            }

            // Get destination info
            Node destNode = segment[segment.Count - 1].node;
            string destName = destNode.name;
            
            // Calculate distance for this segment
            float segmentDistance = CalculateSegmentDistance(segment);

            // Create direction
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

    /// <summary>
    /// Group path nodes into logical segments based on distance
    /// </summary>
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

            // Check if we've accumulated enough distance to create a new segment
            if (accumulatedDistance >= minSegmentDistance && i < pathNodes.Count - 1)
            {
                segments.Add(new List<PathNode>(currentSegment));
                currentSegment.Clear();
                currentSegment.Add(pathNodes[i]);
                accumulatedDistance = 0f;
            }
        }

        // Add final segment if not empty
        if (currentSegment.Count > 0)
        {
            segments.Add(currentSegment);
        }

        // Ensure we always have at least the start and end
        if (segments.Count == 0)
        {
            segments.Add(new List<PathNode> { pathNodes[0], pathNodes[pathNodes.Count - 1] });
        }

        return segments;
    }

    /// <summary>
    /// Calculate the heading (bearing) between two PathNodes in degrees
    /// Uses x,y coordinates from the nodes
    /// </summary>
    private float CalculateHeading(PathNode from, PathNode to)
    {
        Vector3 direction = to.worldPosition - from.worldPosition;
        
        // Calculate angle in degrees (0 = North/Up, 90 = East/Right)
        float heading = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        
        // Normalize to 0-360
        if (heading < 0)
            heading += 360f;

        return heading;
    }

    /// <summary>
    /// Determine turn direction based on heading change
    /// </summary>
    private TurnDirection DetermineTurn(float previousHeading, float currentHeading)
    {
        // Calculate the difference in heading
        float headingDiff = currentHeading - previousHeading;

        // Normalize to -180 to 180
        while (headingDiff > 180)
            headingDiff -= 360;
        while (headingDiff < -180)
            headingDiff += 360;

        // Determine turn type
        if (Mathf.Abs(headingDiff) < turnThresholdDegrees)
        {
            return TurnDirection.Straight;
        }
        else if (headingDiff > 0) // Positive difference = right turn
        {
            return TurnDirection.TurnRight;
        }
        else // Negative difference = left turn
        {
            return TurnDirection.TurnLeft;
        }
    }

    /// <summary>
    /// Calculate total distance of a segment
    /// </summary>
    private float CalculateSegmentDistance(List<PathNode> segment)
    {
        float distance = 0f;

        for (int i = 0; i < segment.Count - 1; i++)
        {
            distance += Vector3.Distance(segment[i].worldPosition, segment[i + 1].worldPosition);
        }

        return distance;
    }

    /// <summary>
    /// Build human-readable instruction text
    /// </summary>
    private string BuildInstruction(TurnDirection turn, string destination, float distance)
    {
        string distanceText = FormatDistance(distance);

        switch (turn)
        {
            case TurnDirection.Straight:
                return $"Head straight to {destination} ({distanceText})";

            case TurnDirection.TurnLeft:
                return $"Turn left toward {destination} ({distanceText})";

            case TurnDirection.TurnRight:
                return $"Turn right toward {destination} ({distanceText})";

            case TurnDirection.Continue:
                return $"Continue to {destination} ({distanceText})";

            default:
                return $"Go to {destination} ({distanceText})";
        }
    }

    /// <summary>
    /// Format distance for display
    /// </summary>
    private string FormatDistance(float meters)
    {
        if (meters < 1000)
            return $"{meters:F0}m";
        else
            return $"{meters / 1000:F2}km";
    }

    /// <summary>
    /// Get a simple text representation of turn direction
    /// </summary>
    public string GetTurnSymbol(TurnDirection turn)
    {
        return turn switch
        {
            TurnDirection.Straight => "↑",
            TurnDirection.TurnLeft => "↖",
            TurnDirection.TurnRight => "↗",
            TurnDirection.Continue => "↓",
            _ => "?"
        };
    }

    /// <summary>
    /// Get a simple text representation for voice/text
    /// </summary>
    public string GetTurnText(TurnDirection turn)
    {
        return turn switch
        {
            TurnDirection.Straight => "Straight",
            TurnDirection.TurnLeft => "Left",
            TurnDirection.TurnRight => "Right",
            TurnDirection.Continue => "Continue",
            _ => "Unknown"
        };
    }
}

/// <summary>
/// Represents a single navigation direction step
/// </summary>
public class NavigationDirection
{
    public DirectionGenerator.TurnDirection turn;
    public string instruction; // e.g., "Turn right toward CCS Building (150m)"
    public Node destinationNode; // The target infrastructure/node for this step
    public float distanceInMeters;
    public float heading; // Compass bearing in degrees
    public List<PathNode> pathNodes; // All nodes in this segment
    
    public override string ToString()
    {
        return instruction;
    }
}