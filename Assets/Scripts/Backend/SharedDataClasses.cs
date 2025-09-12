using System;
using UnityEngine;
using System.Collections.Generic;

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
    public long last_check;
}

[System.Serializable]
public class LocalStaticDataCache
{
    public bool infrastructure_synced;
    public bool categories_synced;
    public long cache_timestamp;
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
    public bool onboardingComplete;
}

// Helper class for JSON array parsing (Unity's JsonUtility doesn't handle arrays directly)
public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>("{\"Items\":" + json + "}");
        return wrapper.Items;
    }

    public static string ToJson<T>(T[] array, bool prettyPrint = false)
    {
        Wrapper<T> wrapper = new Wrapper<T>();
        wrapper.Items = array;
        return JsonUtility.ToJson(wrapper, prettyPrint);
    }

    [System.Serializable]
    private class Wrapper<T>
    {
        public T[] Items;
    }
}