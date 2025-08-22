using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MapUtils
{
    public static Vector2 LatLonToUI(
        float lat, float lon,
        RectTransform mapImage,
        float minLat, float maxLat, float minLon, float maxLon,
        float paddingLeft, float paddingRight, float paddingTop, float paddingBottom)
    {
        float midLat = (minLat + maxLat) / 2f;
        float mapWidthMeters = (maxLon - minLon) * 111320f * Mathf.Cos(midLat * Mathf.Deg2Rad);
        float mapHeightMeters = (maxLat - minLat) * 111320f;

        float xMeters = (lon - minLon) * 111320f * Mathf.Cos(midLat * Mathf.Deg2Rad);
        float yMeters = (lat - minLat) * 111320f;

        float usableWidth = mapImage.rect.width - paddingLeft - paddingRight;
        float usableHeight = mapImage.rect.height - paddingTop - paddingBottom;

        float scaleX = usableWidth / mapWidthMeters;
        float scaleY = usableHeight / mapHeightMeters;

        float pixelX = xMeters * scaleX;
        float pixelY = yMeters * scaleY;

        float anchoredX = pixelX - usableWidth / 2f + paddingLeft;
        float anchoredY = pixelY - usableHeight / 2f + paddingBottom;

        return new Vector2(anchoredX, anchoredY);
    }
}

