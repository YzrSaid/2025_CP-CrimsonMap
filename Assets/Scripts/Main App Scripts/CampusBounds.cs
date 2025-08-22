using System.Collections.Generic;
using UnityEngine;

public static class CampusBounds
{
    private static List<Vector2> barrierPolygon = new List<Vector2>();

    // Initialize polygon with XY coordinates already projected
    public static void InitializePolygon(List<Vector2> xyPoints)
    {
        barrierPolygon.Clear();
        barrierPolygon.AddRange(xyPoints);
    }

    // Check if a point is inside the polygon
    public static bool IsPointInPolygon(Vector2 point)
    {
        bool inside = false;
        int j = barrierPolygon.Count - 1;
        for (int i = 0; i < barrierPolygon.Count; j = i++)
        {
            if (((barrierPolygon[i].y > point.y) != (barrierPolygon[j].y > point.y)) &&
                (point.x < (barrierPolygon[j].x - barrierPolygon[i].x) * (point.y - barrierPolygon[i].y) / (barrierPolygon[j].y - barrierPolygon[i].y) + barrierPolygon[i].x))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    // Clamp point to nearest edge if outside
    public static Vector2 ClampPointToPolygon(Vector2 point)
    {
        Vector2 closestPoint = barrierPolygon[0];
        float minDistSqr = float.MaxValue;

        for (int i = 0; i < barrierPolygon.Count; i++)
        {
            Vector2 a = barrierPolygon[i];
            Vector2 b = barrierPolygon[(i + 1) % barrierPolygon.Count];

            Vector2 closestOnSegment = ClosestPointOnLineSegment(a, b, point);
            float distSqr = (point - closestOnSegment).sqrMagnitude;

            if (distSqr < minDistSqr)
            {
                minDistSqr = distSqr;
                closestPoint = closestOnSegment;
            }
        }

        return closestPoint;
    }

    private static Vector2 ClosestPointOnLineSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        Vector2 ab = b - a;
        float t = Vector2.Dot(p - a, ab) / ab.sqrMagnitude;
        t = Mathf.Clamp01(t);
        return a + t * ab;
    }
}