using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PolygonImage : MaskableGraphic
{
    public List<Vector2> points = new List<Vector2>();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (points.Count < 3) return; // need at least 3 points

        // Calculate polygon bounds
        Rect bounds = GetBounds(points);

        // Fan triangulation with normalized UVs
        for (int i = 1; i < points.Count - 1; i++)
        {
            vh.AddVert(points[0], color, Normalize(points[0], bounds));
            vh.AddVert(points[i], color, Normalize(points[i], bounds));
            vh.AddVert(points[i + 1], color, Normalize(points[i + 1], bounds));

            int idx = (i - 1) * 3;
            vh.AddTriangle(idx, idx + 1, idx + 2);
        }
    }

    private Rect GetBounds(List<Vector2> pts)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var p in pts)
        {
            if (p.x < minX) minX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.x > maxX) maxX = p.x;
            if (p.y > maxY) maxY = p.y;
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private Vector2 Normalize(Vector2 p, Rect r)
    {
        return new Vector2(
            (p.x - r.xMin) / r.width,
            (p.y - r.yMin) / r.height
        );
    }
}