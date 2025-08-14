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

        // Simple fan triangulation (works for convex/concave)
        for (int i = 1; i < points.Count - 1; i++)
        {
            vh.AddVert(points[0], color, Vector2.zero);
            vh.AddVert(points[i], color, Vector2.zero);
            vh.AddVert(points[i + 1], color, Vector2.zero);

            int idx = (i - 1) * 3;
            vh.AddTriangle(idx, idx + 1, idx + 2);
        }
    }
}
