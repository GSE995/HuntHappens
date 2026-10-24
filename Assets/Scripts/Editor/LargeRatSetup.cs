using System.Linq;
using HuntHappens.SmallGame;
using HuntHappens.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
namespace HuntHappens.Editor
{
    public static class LargeRatSetup
    {
        public static void RunBatch()
        {
            const string path = "Assets/Art/SmallGame/Rat-Large.prefab";
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path)) AssetDatabase.CopyAsset("Assets/Art/SmallGame/Rat.prefab", path);
            const string materialPath = "Assets/Art/Animals/Mouse/LargeMouse.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material) { material = new Material(Shader.Find("HuntHappens/GreyMouse")); AssetDatabase.CreateAsset(material, materialPath); }
            material.SetColor("_FurColor", new Color(0.18f, 0.075f, 0.025f));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Animals/Mouse/Mouse.mat").GetTexture("_BaseMap"));
            var root = PrefabUtility.LoadPrefabContents(path);
            var rat = root.GetComponent<RatAI>(); rat.isAggressive = true; rat.isGrey = false;
            var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/SmallGame/Rat.prefab").GetComponent<RatAI>();
            rat.visual.localScale = original.visual.localScale * 1.4f;
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.sharedMaterial = material;
            root.GetComponent<NavMeshAgent>().radius = 0.18f;
            PrefabUtility.SaveAsPrefabAsset(root, path); PrefabUtility.UnloadPrefabContents(root);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            var player = Object.FindAnyObjectByType<RatHunting>(); if (!player.GetComponent<PlayerHealth>()) player.gameObject.AddComponent<PlayerHealth>();
            if (!Object.FindObjectsByType<RatAI>(FindObjectsSortMode.None).Any(r => r.isAggressive))
            {
                var spot = Object.FindObjectsByType<RatGatheringSpot>(FindObjectsSortMode.None).OrderByDescending(s => s.transform.position.z).First();
                bool found = false;
                for (int i = 0; i < 24; i++)
                {
                    var position = spot.transform.position + Quaternion.Euler(0, i * 47, 0) * Vector3.forward * 1.6f;
                    if (!NavMesh.SamplePosition(position, out var nav, 0.3f, NavMesh.AllAreas)) continue;
                    if (!player.ground.Raycast(new Ray(nav.position + Vector3.up, Vector3.down), out var ground, 2)) continue;
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    instance.name = "Large Guard Rat"; instance.transform.SetParent(GameObject.Find("Small Game").transform); instance.transform.position = ground.point; found = true; break;
                }
                if (!found) throw new System.Exception("No large rat spawn");
            }
            foreach (var item in Object.FindObjectsByType<HuntHappens.Shop.WorldShopItem>(FindObjectsSortMode.None))
                if (item.Kind == 3) item.GetComponentInChildren<TMPro.TextMeshPro>().text = "ПРИЁМ КРЫС\n$10 / $15 / $25";
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Large rat installed: one brown guard, 1.4x size, player health, $25 reception.");
        }
    }
}
