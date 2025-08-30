// Node.cs
using System.Collections.Generic;

[System.Serializable]
public class Node
{
    public string node_id;
    public string name;
    public float latitude;
    public float longitude;
    public bool linked_building;
    public bool is_barrier;
    public bool is_pathway;
    public bool is_active;
    public string campus_id;
}

[System.Serializable]
public class NodeList
{
    public List<Node> nodes;
}
