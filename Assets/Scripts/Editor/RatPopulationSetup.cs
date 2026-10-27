using System;
using System.Linq;
using HuntHappens.SmallGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    public static class RatPopulationSetup
    {
        public static void Apply()
        {
            if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/ForestPrototype.unity")
                throw new InvalidOperationException("Open ForestPrototype in Edit Mode");
            var spots = Object.FindObjectsByType<RatGatheringSpot>(FindObjectsSortMode.None);
            var rats = Object.FindObjectsByType<RatAI>(FindObjectsSortMode.None);
            var parent = GameObject.Find("Small Game").transform;
            foreach (var spot in spots)
            {
                var local = rats.Where(r => spots.OrderBy(s => (s.transform.position - r.transform.position).sqrMagnitude).First() == spot).ToArray();
                for (int kind = 0; kind < 3; kind++)
                {
                    int target = kind == 0 ? 5 : kind == 1 ? 4 : 1;
                    int count = local.Count(r => r.Kind == kind);
                    if (count > target) throw new InvalidOperationException("Unexpected excess population at " + spot.name);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/SmallGame/" + (kind == 0 ? "Rat" : kind == 1 ? "Rat-Grey" : "Rat-Large") + ".prefab");
                    for (int i = count; i < target; i++)
                    {
                        bool found = false;
                        for (int attempt = 0; attempt < 24; attempt++)
                        {
                            float angle = (i * 137.5f + kind * 53f + attempt * 29f) * Mathf.Deg2Rad;
                            var point = spot.transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (1.5f + (i % 3) * 0.4f);
                            if (!NavMesh.SamplePosition(point, out var hit, 0.5f, NavMesh.AllAreas)) continue;
                            if (spots.OrderBy(s => (s.transform.position - hit.position).sqrMagnitude).First() != spot) continue;
                            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                            Undo.RegisterCreatedObjectUndo(instance, "Populate litter spots");
                            instance.name = kind == 0 ? "Black Rat" : kind == 1 ? "Grey Rat" : "Large Guard Rat";
                            instance.transform.SetParent(parent);
                            instance.transform.position = hit.position;
                            instance.GetComponent<RatAI>().territoryRadius = 7f;
                            found = true;
                            break;
                        }
                        if (!found) throw new InvalidOperationException("No NavMesh spawn at " + spot.name);
                    }
                }
            }
            Verify(false);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveOpenScenes()) throw new InvalidOperationException("Scene save failed");
            AssetDatabase.SaveAssets();
        }

        public static void Verify(bool playing)
        {
            var spots = Object.FindObjectsByType<RatGatheringSpot>(FindObjectsSortMode.None);
            var rats = Object.FindObjectsByType<RatAI>(FindObjectsSortMode.None);
            if (spots.Length != 3 || rats.Length != 30) throw new InvalidOperationException("Expected 3 spots and 30 rats");
            foreach (var spot in spots)
            {
                var local = rats.Where(r => spots.OrderBy(s => (s.transform.position - (playing ? r.Home : r.transform.position)).sqrMagnitude).First() == spot).ToArray();
                if (local.Length != 10 || local.Count(r => r.Kind == 0) != 5 || local.Count(r => r.Kind == 1) != 4 || local.Count(r => r.Kind == 2) != 1)
                    throw new InvalidOperationException("Wrong population at " + spot.name);
                if (playing && local.Any(r => !r.GetComponent<NavMeshAgent>().isOnNavMesh)) throw new InvalidOperationException("Rat off NavMesh");
                Debug.Log("Rat population PASSED at " + spot.transform.position + ": 5 ordinary, 4 grey, 1 large");
            }
        }
    }
}
