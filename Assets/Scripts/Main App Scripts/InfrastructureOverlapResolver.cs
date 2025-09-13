// using UnityEngine;
// using System.Collections.Generic;
// using System.Linq;
// using Mapbox.Utils;
// using Mapbox.Unity.Map;

// public class InfrastructureOverlapResolver : MonoBehaviour
// {
//     [Header("Overlap Detection")]
//     public float minDistanceBetweenInfrastructure = 5f;
//     public int maxIterations = 50;
//     public bool enableDebugLogs = true;

//     [Header("Positioning")]
//     public float separationForce = 2f;
//     public bool useCircularPattern = true;
//     public float circleRadius = 8f;

//     private AbstractMap mapboxMap;

//     void Awake()
//     {
//         mapboxMap = FindObjectOfType<AbstractMap>();
//     }

//     public void ResolveOverlaps(List<InfrastructureNode> infrastructureNodes)
//     {
//         if (infrastructureNodes == null || infrastructureNodes.Count <= 1)
//         {
//             DebugLog("No overlaps to resolve - too few infrastructure items");
//             return;
//         }

//         DebugLog($"Starting overlap resolution for {infrastructureNodes.Count} infrastructure items");

//         if (useCircularPattern)
//         {
//             ResolveOverlapsWithCircularArrangement(infrastructureNodes);
//         }
//         else
//         {
//             ResolveOverlapsWithForces(infrastructureNodes);
//         }

//         DebugLog("Overlap resolution completed");
//     }

//     private void ResolveOverlapsWithForces(List<InfrastructureNode> nodes)
//     {
//         bool hasOverlaps = true;
//         int iteration = 0;

//         while (hasOverlaps && iteration < maxIterations)
//         {
//             hasOverlaps = false;
//             iteration++;

//             for (int i = 0; i < nodes.Count; i++)
//             {
//                 for (int j = i + 1; j < nodes.Count; j++)
//                 {
//                     if (nodes[i] == null || nodes[j] == null) continue;

//                     float distance = Vector3.Distance(nodes[i].transform.position, nodes[j].transform.position);

//                     if (distance < minDistanceBetweenInfrastructure)
//                     {
//                         hasOverlaps = true;

//                         Vector3 direction = (nodes[j].transform.position - nodes[i].transform.position).normalized;

//                         if (direction.magnitude < 0.1f)
//                         {
//                             direction = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
//                         }

//                         float overlap = minDistanceBetweenInfrastructure - distance;
//                         Vector3 separation = direction * (overlap * 0.5f * separationForce);

//                         Vector3 newPos1 = nodes[i].transform.position - separation;
//                         Vector3 newPos2 = nodes[j].transform.position + separation;

//                         UpdateInfrastructurePosition(nodes[i], newPos1);
//                         UpdateInfrastructurePosition(nodes[j], newPos2);

//                         DebugLog($"Separated {nodes[i].GetInfrastructureData().Infrastructure.name} and {nodes[j].GetInfrastructureData().Infrastructure.name}");
//                     }
//                 }
//             }

//             if (iteration % 10 == 0)
//             {
//                 DebugLog($"Iteration {iteration}: {(hasOverlaps ? "Still resolving overlaps" : "No overlaps found")}");
//             }
//         }

//         if (iteration >= maxIterations)
//         {
//             Debug.LogWarning($"Overlap resolution stopped at max iterations ({maxIterations})");
//         }
//     }

//     private void ResolveOverlapsWithCircularArrangement(List<InfrastructureNode> nodes)
//     {
//         List<List<InfrastructureNode>> overlapGroups = FindOverlapGroups(nodes);

//         foreach (var group in overlapGroups)
//         {
//             if (group.Count > 1)
//             {
//                 ArrangeGroupInCircle(group);
//             }
//         }
//     }

//     private List<List<InfrastructureNode>> FindOverlapGroups(List<InfrastructureNode> nodes)
//     {
//         var groups = new List<List<InfrastructureNode>>();
//         var processed = new HashSet<InfrastructureNode>();

//         foreach (var node in nodes)
//         {
//             if (processed.Contains(node)) continue;

//             var group = new List<InfrastructureNode> { node };
//             processed.Add(node);

//             bool foundNew = true;
//             while (foundNew)
//             {
//                 foundNew = false;
//                 foreach (var groupNode in group.ToList())
//                 {
//                     foreach (var otherNode in nodes)
//                     {
//                         if (processed.Contains(otherNode)) continue;

//                         float distance = Vector3.Distance(groupNode.transform.position, otherNode.transform.position);
//                         if (distance < minDistanceBetweenInfrastructure)
//                         {
//                             group.Add(otherNode);
//                             processed.Add(otherNode);
//                             foundNew = true;
//                         }
//                     }
//                 }
//             }

//             groups.Add(group);
//         }

//         return groups;
//     }

//     private void ArrangeGroupInCircle(List<InfrastructureNode> group)
//     {
//         if (group.Count <= 1) return;

//         // ✅ Check if this group actually has overlaps
//         bool hasRealOverlap = false;
//         for (int i = 0; i < group.Count; i++)
//         {
//             for (int j = i + 1; j < group.Count; j++)
//             {
//                 float distance = Vector3.Distance(group[i].transform.position, group[j].transform.position);
//                 if (distance < minDistanceBetweenInfrastructure * 0.9f) // extra margin to confirm real overlap
//                 {
//                     hasRealOverlap = true;
//                     break;
//                 }
//             }
//             if (hasRealOverlap) break;
//         }

//         if (!hasRealOverlap)
//         {
//             DebugLog($"⏩ Skipping circular arrangement for group of {group.Count} (no real overlap)");
//             return; // ✅ don’t apply spacing if they’re not actually overlapping
//         }

//         DebugLog($"Arranging {group.Count} overlapping infrastructure in a circle");

//         Vector3 center = Vector3.zero;
//         foreach (var node in group)
//         {
//             center += node.transform.position;
//         }
//         center /= group.Count;

//         // Scale radius with group size so they don't clump
//         float scaledRadius = circleRadius + (group.Count * 0.5f);

//         float angleStep = 360f / group.Count;
//         for (int i = 0; i < group.Count; i++)
//         {
//             float angle = angleStep * i * Mathf.Deg2Rad;
//             Vector3 offset = new Vector3(
//                 Mathf.Cos(angle) * scaledRadius,
//                 0,
//                 Mathf.Sin(angle) * scaledRadius
//             );

//             Vector3 newPosition = center + offset;
//             UpdateInfrastructurePosition(group[i], newPosition);

//             DebugLog($"Positioned {group[i].GetInfrastructureData().Infrastructure.name} at angle {angleStep * i} degrees");
//         }
//     }

//     private void UpdateInfrastructurePosition(InfrastructureNode infraNode, Vector3 newWorldPosition)
//     {
//         if (mapboxMap == null) return;

//         Vector2d newGeoPosition = mapboxMap.WorldToGeoPosition(newWorldPosition);
//         UpdateInfrastructureGeoLocation(infraNode, newGeoPosition);

//         DebugLog($"Updated {infraNode.GetInfrastructureData().Infrastructure.name} to new position: {newWorldPosition}");
//     }

//     private void UpdateInfrastructureGeoLocation(InfrastructureNode infraNode, Vector2d newGeoLocation)
//     {
//         // ✅ Use the new method so geoLocation is updated correctly
//         infraNode.SetGeoLocation(newGeoLocation);
//     }

//     public bool AreOverlapping(InfrastructureNode node1, InfrastructureNode node2)
//     {
//         if (node1 == null || node2 == null) return false;

//         float distance = Vector3.Distance(node1.transform.position, node2.transform.position);
//         return distance < minDistanceBetweenInfrastructure;
//     }

//     public List<InfrastructureNode> GetNearbyInfrastructure(Vector3 position, float radius, List<InfrastructureNode> allNodes)
//     {
//         return allNodes.Where(node =>
//             node != null &&
//             Vector3.Distance(node.transform.position, position) <= radius
//         ).ToList();
//     }

//     private void DebugLog(string message)
//     {
//         if (enableDebugLogs)
//         {
//             Debug.Log($"[InfraOverlapResolver] {message}");
//         }
//     }

//     void OnDrawGizmosSelected()
//     {
//         var infraNodes = FindObjectsOfType<InfrastructureNode>();

//         Gizmos.color = Color.yellow;
//         foreach (var node in infraNodes)
//         {
//             if (node != null)
//             {
//                 Gizmos.DrawWireSphere(node.transform.position, minDistanceBetweenInfrastructure * 0.5f);
//             }
//         }
//     }
// }
