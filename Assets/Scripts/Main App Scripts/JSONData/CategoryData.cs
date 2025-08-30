// CategoryData
using System.Collections.Generic;

[System.Serializable]
public class Category
{
    public int category_id;
    public string name;
    public string icon;
    public List<int> building_id;
}

[System.Serializable]
public class CategoryList
{
    public List<Category> categories;
}
