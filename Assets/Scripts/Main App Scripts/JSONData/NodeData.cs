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
    public string related_infra_id;  
    public string related_room_id;   
    
    public IndoorInfo indoor; 
    public string campus_id;
    
   // Helpers
    public bool HasRelatedInfraId => !string.IsNullOrEmpty(related_infra_id);
    public bool HasRelatedRoomId => !string.IsNullOrEmpty(related_room_id);

    // If you need them as strings
    public string GetRelatedInfraId() => HasRelatedInfraId ? related_infra_id : null;
    public string GetRelatedRoomId() => HasRelatedRoomId ? related_room_id : null;
}

[System.Serializable]
public class NodeList
{
    public List<Node> nodes;
}

[System.Serializable]
public class IndoorInfo
{
    public float x;
    public float y;
    public int floor;
}