using UnityEngine;
// This is a helper class to determine the AR Mode for the system
public static class ARModeHelper
{
    public enum ARMode { DirectAR, Navigation }

    public static ARMode GetCurrentARMode()
    {
        string arModeString = PlayerPrefs.GetString("ARMode");
        return arModeString == "Navigation" ? ARMode.Navigation : ARMode.DirectAR;
    }

    public static bool IsNavigationMode()
    {
        return GetCurrentARMode() == ARMode.Navigation;
    }

    public static bool IsDirectARMode()
    {
        return GetCurrentARMode() == ARMode.DirectAR;
    }

    public static void SetARMode(ARMode mode)
    {
        PlayerPrefs.SetString("ARMode", mode.ToString());
        PlayerPrefs.Save();
    }
}