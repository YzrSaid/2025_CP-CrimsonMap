using System.Collections.Generic;

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

[System.Serializable]
public class EdgeList
{
    public List<Edge> edges;
}
