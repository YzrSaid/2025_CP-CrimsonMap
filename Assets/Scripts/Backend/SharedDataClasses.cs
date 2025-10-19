using System;
using UnityEngine;
using System.Collections.Generic;
using Firebase.Firestore;

// CategoryData
[System.Serializable]
public class Category
{
    public string category_id;
    public string name;
    public string color;
}

[System.Serializable]
public class CategoryList
{
    public List<Category> categories;
}

// EdgeData
[System.Serializable]
public class Edge
{
    public string edge_id;
    public string from_node;
    public string to_node;
    public float distance;
    public string path_type;
    public string elevations;
    public bool is_active;
}

// RouteData Data
[System.Serializable]
public class RouteData
{
    public List<PathNode> path;
    public float totalDistance;
    public string formattedDistance;
    public string walkingTime;
    public Node startNode;
    public string viaMode;
    public bool isRecommended;
    public Node endNode;
}

[System.Serializable]
public class GraphEdge
{
    public string toNodeId;
    public float cost;
    public Edge edgeData;
}

[System.Serializable]
public class PathNode
{
    public Node node;
    public Vector3 worldPosition;
    public bool isStart;
    public bool isEnd;
    public float distanceToNext;
}


[System.Serializable]
public class EdgeList
{
    public List<Edge> edges;
}

// InfrastructureData
[System.Serializable]
public class Infrastructure
{
    public string infra_id;
    public string name;
    public string category_id;
    public string image_url;
    public string email;
    public bool is_deleted;
    public string phone;
}

[System.Serializable]
public class InfrastructureList
{
    public Infrastructure[] infrastructures;
}

// MapData
[Serializable]
public class MapData
{
    public string map_id;
    public string map_name;
    public List<string> campus_included;
}

[Serializable]
public class MapList
{
    public List<MapData> maps;
}

// NodeData
[System.Serializable]
public class Node
{
    public string node_id;
    public string name;
    public float latitude;
    public float longitude;
    public string type;
    public bool is_active;

    public string related_infra_id;
    public string related_room_id;

    public IndoorInfo indoor;
    public string campus_id;
    public float x_coordinate;
    public float y_coordinate;

    public bool HasRelatedInfraId => !string.IsNullOrEmpty( related_infra_id );
    public bool HasRelatedRoomId => !string.IsNullOrEmpty( related_room_id );

    public string GetRelatedInfraId() => HasRelatedInfraId ? related_infra_id : null;
    public string GetRelatedRoomId() => HasRelatedRoomId ? related_room_id : null;
}

[System.Serializable]
public class NodeList
{
    public List<Node> nodes;
}

// IndoorData

[System.Serializable]
public class IndoorInfo
{
    public float x;
    public float y;
    public int floor;
}

// Campus data class
[System.Serializable]
public class CampusData
{
    public string campus_id;
    public string campus_name;
}

[System.Serializable]
public class CampusList
{
    public List<CampusData> campuses;
}

[System.Serializable]
public class MapInfo
{
    public string map_id;
    public string map_name;
    public List<string> campus_included;
}

[System.Serializable]
public class MapVersionInfo
{
    public string map_id;
    public string current_version;
    public string map_name;
    public long last_updated; // Unix timestamp
}

[System.Serializable]
public class LocalVersionCache
{
    public string map_id;
    public string cached_version;
    public string map_name;
    public long cache_timestamp;
}

[System.Serializable]
public class StaticDataVersionInfo
{
    public bool infrastructure_updated;
    public bool categories_updated;
    public bool campus_updated;
}

[System.Serializable]
public class LocalStaticDataCache
{
    public bool infrastructure_synced;
    public bool categories_synced;
    public bool campus_synced;
}

[System.Serializable]
public class RecentDestinationsData
{
    public Dictionary<string, object>[] recent_destinations;
}

[System.Serializable]
public class SavedDestinationsData
{
    public Dictionary<string, object>[] saved_destinations;
}

[System.Serializable]
public class SaveData
{
    public bool onboardingComplete = false;
}

// Helper class for JSON array parsing (Unity's JsonUtility doesn't handle arrays directly)
public static class JsonHelper
{
    public static T[] FromJson<T>( string json )
    {
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>( "{\"Items\":" + json + "}" );
        return wrapper.Items;
    }

    public static string ToJson<T>( T[] array, bool prettyPrint = false )
    {
        Wrapper<T> wrapper = new Wrapper<T>();
        wrapper.Items = array;
        return JsonUtility.ToJson( wrapper, prettyPrint );
    }

    [System.Serializable]
    private class Wrapper<T>
    {
        public T[] Items;
    }
}