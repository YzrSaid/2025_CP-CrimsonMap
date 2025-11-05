using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public static class ARNavigationDataHelper
{
    private const string SAVED_DESTINATIONS_FILE = "saved_destinations.json";

    public static void SaveAndClearARNavigationData()
    {
        // First save the current navigation to history
        SaveCurrentNavigationToHistory();
        
        // Then clear the navigation data
        ClearARNavigationData();
    }

    private static void SaveCurrentNavigationToHistory()
    {
        // Check if there's actually navigation data to save
        int pathNodeCount = PlayerPrefs.GetInt("ARNavigation_PathNodeCount", 0);
        if (pathNodeCount == 0) return;

        string startNodeId = PlayerPrefs.GetString("ARNavigation_StartNodeId", "");
        string endNodeId = PlayerPrefs.GetString("ARNavigation_EndNodeId", "");
        
        if (string.IsNullOrEmpty(startNodeId) || string.IsNullOrEmpty(endNodeId)) return;

        // Create navigation history entry
        SavedNavigation newNavigation = new SavedNavigation
        {
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            startNodeId = startNodeId,
            endNodeId = endNodeId,
            startNodeName = PlayerPrefs.GetString("ARNavigation_StartNodeName", "Unknown"),
            endNodeName = PlayerPrefs.GetString("ARNavigation_EndNodeName", "Unknown"),
            totalDistance = PlayerPrefs.GetFloat("ARNavigation_TotalDistance", 0f),
            formattedDistance = PlayerPrefs.GetString("ARNavigation_FormattedDistance", ""),
            walkingTime = PlayerPrefs.GetString("ARNavigation_WalkingTime", ""),
            viaMode = PlayerPrefs.GetString("ARNavigation_ViaMode", ""),
            pathNodes = ExtractPathNodes(),
            edges = ExtractEdges(),
            directions = ExtractDirections()
        };

        // Load existing history
        SavedNavigationHistory savedHistory = LoadSavedDestinations();
        
        // Add new navigation to history
        savedHistory.navigations.Add(newNavigation);
        
        // Save back to file
        SaveDestinationsToFile(savedHistory);
    }

    private static List<string> ExtractPathNodes()
    {
        List<string> pathNodes = new List<string>();
        int pathNodeCount = PlayerPrefs.GetInt("ARNavigation_PathNodeCount", 0);
        
        for (int i = 0; i < pathNodeCount; i++)
        {
            string nodeId = PlayerPrefs.GetString($"ARNavigation_PathNode_{i}", "");
            if (!string.IsNullOrEmpty(nodeId))
            {
                pathNodes.Add(nodeId);
            }
        }
        
        return pathNodes;
    }

    private static List<SavedEdge> ExtractEdges()
    {
        List<SavedEdge> edges = new List<SavedEdge>();
        int edgeCount = PlayerPrefs.GetInt("ARNavigation_EdgeCount", 0);
        
        for (int i = 0; i < edgeCount; i++)
        {
            string fromNode = PlayerPrefs.GetString($"ARNavigation_Edge_{i}_From", "");
            string toNode = PlayerPrefs.GetString($"ARNavigation_Edge_{i}_To", "");
            
            if (!string.IsNullOrEmpty(fromNode) && !string.IsNullOrEmpty(toNode))
            {
                edges.Add(new SavedEdge
                {
                    fromNode = fromNode,
                    toNode = toNode
                });
            }
        }
        
        return edges;
    }

    private static List<SavedDirection> ExtractDirections()
    {
        List<SavedDirection> directions = new List<SavedDirection>();
        int directionCount = PlayerPrefs.GetInt("ARNavigation_DirectionCount", 0);
        
        for (int i = 0; i < directionCount; i++)
        {
            SavedDirection dir = new SavedDirection
            {
                instruction = PlayerPrefs.GetString($"ARNavigation_Direction_{i}_Instruction", ""),
                turn = PlayerPrefs.GetString($"ARNavigation_Direction_{i}_Turn", ""),
                distance = PlayerPrefs.GetFloat($"ARNavigation_Direction_{i}_Distance", 0f),
                destNodeId = PlayerPrefs.GetString($"ARNavigation_Direction_{i}_DestNodeId", ""),
                destNode = PlayerPrefs.GetString($"ARNavigation_Direction_{i}_DestNode", ""),
                isIndoorGrouped = PlayerPrefs.GetInt($"ARNavigation_Direction_{i}_IsIndoorGrouped", 0) == 1,
                isIndoorDirection = PlayerPrefs.GetInt($"ARNavigation_Direction_{i}_IsIndoorDirection", 0) == 1
            };
            
            directions.Add(dir);
        }
        
        return directions;
    }

    private static SavedNavigationHistory LoadSavedDestinations()
    {
        string filePath = GetFilePath();
        
        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                SavedNavigationHistory history = JsonUtility.FromJson<SavedNavigationHistory>(json);
                return history ?? new SavedNavigationHistory();
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load saved destinations: {e.Message}");
                return new SavedNavigationHistory();
            }
        }
        
        return new SavedNavigationHistory();
    }

    private static void SaveDestinationsToFile(SavedNavigationHistory history)
    {
        try
        {
            string json = JsonUtility.ToJson(history, true);
            string filePath = GetFilePath();
            
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.WriteAllText(filePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save destinations: {e.Message}");
        }
    }

    private static string GetFilePath()
    {
        #if UNITY_EDITOR
            return Path.Combine(Application.streamingAssetsPath, SAVED_DESTINATIONS_FILE);
        #else
            return Path.Combine(Application.persistentDataPath, SAVED_DESTINATIONS_FILE);
        #endif
    }

    public static void ClearARNavigationData()
    {
        // Clear path nodes
        int pathNodeCount = PlayerPrefs.GetInt("ARNavigation_PathNodeCount", 0);
        for (int i = 0; i < pathNodeCount; i++)
        {
            PlayerPrefs.DeleteKey($"ARNavigation_PathNode_{i}");
        }
        PlayerPrefs.DeleteKey("ARNavigation_PathNodeCount");

        // Clear edges
        int edgeCount = PlayerPrefs.GetInt("ARNavigation_EdgeCount", 0);
        for (int i = 0; i < edgeCount; i++)
        {
            PlayerPrefs.DeleteKey($"ARNavigation_Edge_{i}_From");
            PlayerPrefs.DeleteKey($"ARNavigation_Edge_{i}_To");
        }
        PlayerPrefs.DeleteKey("ARNavigation_EdgeCount");

        // Clear directions
        int directionCount = PlayerPrefs.GetInt("ARNavigation_DirectionCount", 0);
        for (int i = 0; i < directionCount; i++)
        {
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_Instruction");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_Turn");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_Distance");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_DestNodeId");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_DestNode");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_IsIndoorGrouped");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_IsIndoorDirection");
        }
        PlayerPrefs.DeleteKey("ARNavigation_DirectionCount");

        // Clear route metadata
        PlayerPrefs.DeleteKey("ARNavigation_StartNodeId");
        PlayerPrefs.DeleteKey("ARNavigation_EndNodeId");
        PlayerPrefs.DeleteKey("ARNavigation_StartNodeName");
        PlayerPrefs.DeleteKey("ARNavigation_EndNodeName");
        PlayerPrefs.DeleteKey("ARNavigation_TotalDistance");
        PlayerPrefs.DeleteKey("ARNavigation_FormattedDistance");
        PlayerPrefs.DeleteKey("ARNavigation_WalkingTime");
        PlayerPrefs.DeleteKey("ARNavigation_ViaMode");

        PlayerPrefs.Save();
    }

    public static List<SavedNavigation> GetNavigationHistory()
    {
        SavedNavigationHistory history = LoadSavedDestinations();
        return history.navigations;
    }

    public static void ClearNavigationHistory()
    {
        string filePath = GetFilePath();
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Debug.Log("Navigation history cleared");
        }
    }
}
