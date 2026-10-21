using System;
using System.Linq;
using HuntHappens.Animals;
using HuntHappens.Interaction;
using HuntHappens.Traps;
using HuntHappens.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HuntHappens.Editor
{
    public static class AnimalTrapSetup
    {
        private const string PrefabPath = "Assets/Prefabs/AnimalTrap.prefab";
        [MenuItem("Tools/Hunt Happens/Setup Animal Traps")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Exit Play Mode before trap setup."); return; }
            Scene scene = SceneManager.GetActiveScene();
            TranquilizerGun player = InScene<TranquilizerGun>(scene).FirstOrDefault();
            Collider ground = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(true))
                .FirstOrDefault(c => c.name == "Ground" && c.attachedRigidbody == null);
            if (player == null || ground == null || player.GetComponent<PlayerInteraction>() == null)
                throw new InvalidOperationException("Open ForestPrototype with tranquilizer and ground first.");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Traps")) AssetDatabase.CreateFolder("Assets/Materials", "Traps");
            var prefab = AssetDatabase.LoadAssetAtPath<AnimalTrap>(PrefabPath);
            if (prefab == null) prefab = CreatePrefab();
            Material valid = Material("Preview Green", new Color(0.15f, 1, 0.35f, 0.4f), true);
            Material invalid = Material("Preview Red", new Color(1, 0.15f, 0.12f, 0.4f), true);
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup animal traps");
            TrapManager manager = InScene<TrapManager>(scene).FirstOrDefault();
            if (manager == null)
            {
                var root = new GameObject("Animal Traps");
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Create trap manager");
                manager = Undo.AddComponent<TrapManager>(root);
            }
            Undo.RecordObject(manager, "Configure trap prefab");
            manager.Configure(prefab);
            TrapPlacement placement = player.GetComponent<TrapPlacement>();
            if (placement == null) placement = Undo.AddComponent<TrapPlacement>(player.gameObject);
            Undo.RecordObject(placement, "Configure trap placement");
            placement.Configure(player.GetComponentInChildren<Camera>(), ground, manager, valid, invalid);
            PrefabUtility.RecordPrefabInstancePropertyModifications(placement);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(placement);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Animal traps ready. Save scene, Play, 3 to select, LMB on clear ground, E to retrieve deer or empty trap.");
        }
        private static T[] InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        private static AnimalTrap CreatePrefab()
        {
            Material orange = Material("Safety Orange", new Color(1, 0.5f, 0.06f));
            Material rope = Material("Net Rope", new Color(0.92f, 0.85f, 0.45f));
            Material baseMaterial = Material("Platform", new Color(0.22f, 0.48f, 0.28f));
            var root = new GameObject("AnimalTrap");
            try
            {
                Part("Platform", root.transform, PrimitiveType.Cube, new Vector3(0, 0.035f, 0), new Vector3(2.2f, 0.07f, 2.2f), baseMaterial);
                var baseCollider = root.AddComponent<BoxCollider>();
                baseCollider.center = new Vector3(0, 0.035f, 0);
                baseCollider.size = new Vector3(2.2f, 0.07f, 2.2f);
                var trigger = root.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.center = new Vector3(0, 1, 0);
                trigger.size = new Vector3(2.1f, 2, 2.1f);
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                        Part("Bright Corner", root.transform, PrimitiveType.Cylinder, new Vector3(x, 0.12f, z), new Vector3(0.2f, 0.12f, 0.2f), orange);
                for (int i = -4; i <= 4; i++)
                {
                    float p = i * 0.25f;
                    Part("Resting Rope X", root.transform, PrimitiveType.Cube, new Vector3(p, 0.085f, 0), new Vector3(0.025f, 0.025f, 2), rope);
                    Part("Resting Rope Z", root.transform, PrimitiveType.Cube, new Vector3(0, 0.085f, p), new Vector3(2, 0.025f, 0.025f), rope);
                }
                var net = new GameObject("Raised Net").transform;
                net.SetParent(root.transform, false);
                // A rounded cage of ropes encloses the sleeping pose; no decorative colliders.
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * Mathf.PI * 2 / 12;
                    Part("Vertical Rope", net, PrimitiveType.Cylinder, new Vector3(Mathf.Sin(angle) * 1.5f, 0.7f, Mathf.Cos(angle) * 1.5f),
                        new Vector3(0.035f, 0.7f, 0.035f), rope);
                    for (int h = 0; h < 4; h++)
                    {
                        float next = angle + Mathf.PI * 2 / 12;
                        Vector3 a = new Vector3(Mathf.Sin(angle) * 1.5f, h * 0.42f + 0.12f, Mathf.Cos(angle) * 1.5f);
                        Vector3 b = new Vector3(Mathf.Sin(next) * 1.5f, a.y, Mathf.Cos(next) * 1.5f);
                        Transform segment = Part("Net Loop", net, PrimitiveType.Cube, (a + b) * 0.5f, new Vector3(0.03f, 0.03f, Vector3.Distance(a, b)), rope);
                        segment.localRotation = Quaternion.LookRotation(b - a);
                    }
                }
                var marker = Part("Caught Marker", root.transform, PrimitiveType.Sphere, Vector3.up * 2.5f, Vector3.one * 0.25f, orange);
                net.gameObject.SetActive(false);
                marker.gameObject.SetActive(false);
                root.AddComponent<AnimalTrap>().Configure(net, marker);
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath).GetComponent<AnimalTrap>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static Transform Part(string name, Transform parent, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }
        private static Material Material(string name, Color color, bool preview = false)
        {
            string path = "Assets/Materials/Traps/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(Shader.Find(preview ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            if (preview)
            {
                material.SetFloat("_Surface", 1);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = 3000;
            }
            else material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
        public static void IntegrateBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch only");
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            int originalObjects = scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Transform>(true).Length);
            int originalDeer = InScene<DeerAI>(scene).Length;
            int expectedAdded = InScene<TrapManager>(scene).Length == 0 ? 1 : 0;
            Setup();
            int count = scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Transform>(true).Length);
            Setup();
            if (InScene<TrapPlacement>(scene).Length != 1 || InScene<TrapManager>(scene).Length != 1 ||
                InScene<DeerAI>(scene).Length != originalDeer || count != originalObjects + expectedAdded ||
                count != scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Transform>(true).Length))
                throw new InvalidOperationException("Trap setup duplicated or replaced scene objects");
            if (!EditorSceneManager.SaveScene(scene)) throw new System.IO.IOException("Scene save failed");
            Debug.Log("Trap setup PASSED: one manager/placement, idempotent, original objects preserved, prefab and scene saved.");
        }
    }
}
