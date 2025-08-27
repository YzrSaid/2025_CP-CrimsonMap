// Infrastructure data

[System.Serializable]
public class Infrastructure
{
    public int infra_id;
    public string name;
    public int category_id;
    public string location;
    public float latitude;
    public float longitude;
    public string image_url;
    public string email;
    public string phone;
}

[System.Serializable]
public class InfrastructureList
{
    public Infrastructure[] infrastructures;
}