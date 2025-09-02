// Updated Node.cs with better nullable handling
using System.Collections.Generic;

[System.Serializable]
public class Node
{
    public string node_id;
    public string name;
    public float latitude;
    public float longitude;
    public string type;
    public bool is_active;
    
    // Unity JsonUtility has issues with nullable ints
    public int related_infra_id = -1;  
    public int related_room_id = -1;   
    
    public bool indoor;
    public string campus_id;
    
    // Helper properties to check if values are set
    public bool HasRelatedInfraId => related_infra_id > 0;
    public bool HasRelatedRoomId => related_room_id > 0;
    
    // Get the actual values (returns null if not set)
    public int? GetRelatedInfraId() => related_infra_id > 0 ? related_infra_id : (int?)null;
    public int? GetRelatedRoomId() => related_room_id > 0 ? related_room_id : (int?)null;
}

[System.Serializable]
public class NodeList
{
    public List<Node> nodes;
}