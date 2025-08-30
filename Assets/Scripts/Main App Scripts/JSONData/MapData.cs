using System;
using System.Collections.Generic;

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
