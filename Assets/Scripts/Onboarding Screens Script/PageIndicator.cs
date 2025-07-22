using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PageIndicator : MonoBehaviour
{
    public GameObject dotPrefab;
    public Transform indicatorParent;  // parent with Horizontal Layout Group
    public Color activeColor = Color.white;
    public Color inactiveColor = new Color(1f, 1f, 1f, 0.3f);

    private List<GameObject> dots = new List<GameObject>();

    public void SetupIndicators(int pageCount)
    {
        // Clear old dots if re-initializing
        foreach (GameObject dot in dots)
        {
            Destroy(dot);
        }
        dots.Clear();

        // Create new dots
        for (int i = 0; i < pageCount; i++)
        {
            GameObject dot = Instantiate(dotPrefab, indicatorParent);
            dot.GetComponent<Image>().color = inactiveColor;
            dots.Add(dot);
        }
    }

    public void SetActivePage(int index)
    {
        for (int i = 0; i < dots.Count; i++)
        {
          dots[i].GetComponent<Image>().CrossFadeColor((i == index) ? activeColor : inactiveColor, 0.2f, false, true);
        }
    }
}
