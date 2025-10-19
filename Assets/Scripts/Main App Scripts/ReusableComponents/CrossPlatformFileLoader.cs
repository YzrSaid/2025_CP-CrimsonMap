using UnityEngine;
using System.Collections;
using System.IO;

public static class CrossPlatformFileLoader
{
    public static IEnumerator LoadJsonFile(string fileName, System.Action<string> onSuccess, System.Action<string> onError)
    {
        string filePath = GetFilePath(fileName);
        yield return LoadFileFromPersistentData(filePath, onSuccess, onError);
    }

    private static IEnumerator LoadFileFromPersistentData(string filePath, System.Action<string> onSuccess, System.Action<string> onError)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                onError?.Invoke($"File not found: {filePath}");
                yield break;
            }

            string jsonContent = File.ReadAllText(filePath);

            if (string.IsNullOrEmpty(jsonContent))
            {
                onError?.Invoke($"File is empty: {filePath}");
                yield break;
            }

            onSuccess?.Invoke(jsonContent);
        }
        catch (System.Exception e)
        {
            onError?.Invoke($"Error reading file {Path.GetFileName(filePath)}: {e.Message}");
        }

        yield return null; 
    }

    private static string GetFilePath(string fileName)
    {
        string dataPath;

        if (Application.isEditor)
        {
            dataPath = Application.streamingAssetsPath;
        }
        else
        {
            dataPath = Application.persistentDataPath;
        }

        string filePath = Path.Combine(dataPath, fileName);
        Debug.Log($"[CrossPlatformFileLoader] Looking for file at: {filePath}");

        return filePath;
    }
    public static bool FileExists(string fileName)
    {
        string filePath = GetFilePath(fileName);
        return File.Exists(filePath);
    }
}
