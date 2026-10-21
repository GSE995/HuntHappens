using System.Collections.Generic;
using HuntHappens.Animals;
using HuntHappens.Interaction;
using HuntHappens.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace HuntHappens.Editor
{
    public static class DeerSceneSetup
    {
        private const string PrefabPath = "Assets/Prefabs/Animals/Deer.prefab";
        private const string RootName = "Prototype Deer";

        [MenuItem("Tools/Hunt Happens/Add Deer")]
        public static void AddDeer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("Exit Play Mode before adding deer or baking navigation."); return; }
            Scene scene = SceneManager.GetActiveScene();
            GameObject environment = null;
            GameObject deerRoot = null;
            ForestNavigation navigation = null;
            FirstPersonMotor player = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Environment") environment = root;
                if (root.name == RootName) deerRoot = root;
                if (navigation == null) navigation = root.GetComponentInChildren<ForestNavigation>(true);
                if (player == null) player = root.GetComponentInChildren<FirstPersonMotor>();
            }
            if (environment == null || player == null)
            { Debug.LogError("Open ForestPrototype: expected Environment and an FPS Player."); return; }
            Collider ground = environment.transform.Find("Ground")?.GetComponent<Collider>();
            if (ground == null) { Debug.LogError("Environment/Ground needs a collider for navigation."); return; }
            Folder("Assets/Prefabs", "Animals");
            Folder("Assets/Materials", "Deer");
            Folder("Assets", "Navigation");
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add deer and bake navigation");
            if (navigation == null)
            {
                var navigationObject = new GameObject("Forest Navigation");
                Undo.RegisterCreatedObjectUndo(navigationObject, "Create navigation");
                navigation = Undo.AddComponent<ForestNavigation>(navigationObject);
            }
            Bake(environment.transform, ground, navigation, scene.name);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = CreatePrefab();
            if (deerRoot == null)
            {
                deerRoot = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(deerRoot, "Create deer group");
            }
            Vector3[] starts = { new Vector3(0, 0, 10), new Vector3(-18, 0, 4), new Vector3(18, 0, 21) };
            for (int i = 0; i < starts.Length; i++)
            {
                string name = "Deer " + (i + 1);
                Transform existing = deerRoot.transform.Find(name);
                GameObject deer;
                if (existing != null) deer = existing.gameObject;
                else
                {
                    var filter = new NavMeshQueryFilter { agentTypeID = prefab.GetComponent<NavMeshAgent>().agentTypeID, areaMask = NavMesh.AllAreas };
                    if (!NavMesh.SamplePosition(starts[i], out NavMeshHit hit, 6f, filter))
                        throw new System.InvalidOperationException("No navigable ground near deer spawn " + starts[i]);
                    deer = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    Undo.RegisterCreatedObjectUndo(deer, "Create " + name);
                    deer.name = name;
                    deer.transform.SetParent(deerRoot.transform, true);
                    deer.transform.position = hit.position;
                    deer.transform.rotation = Quaternion.Euler(0, 35 + i * 95, 0);
                }
                var senses = deer.GetComponent<DeerPerception>();
                Undo.RecordObject(senses, "Connect deer perception");
                senses.SetPlayer(player.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(senses);
                EditorUtility.SetDirty(senses);
            }
            // Movable props are excluded from the bake and carve their current position at runtime.
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (PickupItem item in root.GetComponentsInChildren<PickupItem>())
                    if (item.GetComponent<DeerAI>() == null) AddObstacle(item);
            Undo.CollapseUndoOperations(group);
            EditorUtility.SetDirty(navigation);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Three deer and NavMesh are ready. Save the scene (Ctrl+S), then Play. Add Deer also rebakes navigation after forest edits; existing deer are preserved.");
        }

        private static void Bake(Transform environment, Collider ground, ForestNavigation navigation, string sceneName)
        {
            var markups = new List<NavMeshBuildMarkup>();
            foreach (Collider collider in environment.GetComponentsInChildren<Collider>())
            {
                if (collider == ground || !collider.enabled || collider.isTrigger) continue;
                markups.Add(new NavMeshBuildMarkup { root = collider.transform, overrideArea = true, area = 1 });
            }
            var sources = new List<NavMeshBuildSource>();
            UnityEngine.AI.NavMeshBuilder.CollectSources(environment, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            if (sources.Count == 0) throw new System.InvalidOperationException("No colliders collected for navigation.");
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = 0.55f;
            settings.agentHeight = 2.8f;
            settings.agentClimb = 0.25f;
            settings.agentSlope = 35;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.15f;
            Bounds bounds = ground.bounds;
            bounds.Expand(new Vector3(0, 16, 0));
            NavMeshData baked = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (baked == null) throw new System.InvalidOperationException("Navigation bake failed.");
            baked.name = sceneName + " Deer Navigation";
            string path = "Assets/Navigation/" + sceneName + "-Deer.asset";
            NavMeshData saved = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (saved == null) { AssetDatabase.CreateAsset(baked, path); saved = baked; }
            else { EditorUtility.CopySerialized(baked, saved); Object.DestroyImmediate(baked); EditorUtility.SetDirty(saved); }
            Undo.RecordObject(navigation, "Set baked navigation");
            navigation.SetData(saved);
        }

        private static void AddObstacle(PickupItem item)
        {
            if (item.GetComponent<NavMeshObstacle>() != null) return;
            Collider collider = item.GetComponent<Collider>();
            if (collider == null) return;
            Bounds bounds = collider.bounds;
            Vector3 scale = item.transform.lossyScale;
            var obstacle = Undo.AddComponent<NavMeshObstacle>(item.gameObject);
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = item.transform.InverseTransformPoint(bounds.center);
            obstacle.size = new Vector3(bounds.size.x / Mathf.Max(0.01f, Mathf.Abs(scale.x)),
                bounds.size.y / Mathf.Max(0.01f, Mathf.Abs(scale.y)), bounds.size.z / Mathf.Max(0.01f, Mathf.Abs(scale.z)));
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            obstacle.carvingTimeToStationary = 0.5f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(obstacle);
        }

        private static GameObject CreatePrefab()
        {
            Material fur = Material("Fur", new Color(0.58f, 0.32f, 0.16f));
            Material cream = Material("Cream", new Color(0.9f, 0.77f, 0.52f));
            Material dark = Material("Eyes and Hooves", new Color(0.055f, 0.045f, 0.035f));
            Material antler = Material("Antlers", new Color(0.72f, 0.57f, 0.36f));
            var deer = new GameObject("Deer");
            try
            {
                var agent = deer.AddComponent<NavMeshAgent>();
                agent.agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID;
                agent.radius = 0.55f;
                agent.height = 2.8f;
                agent.speed = 1.6f;
                agent.acceleration = 10;
                agent.angularSpeed = 150;
                agent.stoppingDistance = 0.3f;
                agent.autoBraking = true;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
                var collider = deer.AddComponent<CapsuleCollider>();
                collider.center = new Vector3(0, 1.15f, 0);
                collider.height = 2.3f;
                collider.radius = 0.48f;
                var body = deer.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                deer.AddComponent<DeerPerception>();
                deer.AddComponent<DeerAI>();
                Transform visual = Child("Visual", deer.transform, Vector3.zero);
                Part("Body", PrimitiveType.Cube, visual, new Vector3(0, 1.15f, 0), new Vector3(0.72f, 0.7f, 1.4f), fur);
                Part("Chest", PrimitiveType.Cube, visual, new Vector3(0, 1.1f, 0.56f), new Vector3(0.5f, 0.55f, 0.15f), cream);
                var legs = new Transform[4];
                for (int i = 0; i < legs.Length; i++)
                {
                    legs[i] = Child("Leg Pivot " + i, visual, new Vector3(i % 2 == 0 ? -0.25f : 0.25f, 0.95f, i < 2 ? 0.43f : -0.43f));
                    Part("Leg", PrimitiveType.Cube, legs[i], new Vector3(0, -0.4f, 0), new Vector3(0.13f, 0.8f, 0.15f), fur);
                    Part("Hoof", PrimitiveType.Cube, legs[i], new Vector3(0, -0.87f, 0.025f), new Vector3(0.17f, 0.16f, 0.22f), dark);
                }
                Part("Neck", PrimitiveType.Cube, visual, new Vector3(0, 1.65f, 0.54f), new Vector3(0.34f, 0.85f, 0.38f), fur).localRotation = Quaternion.Euler(-18, 0, 0);
                Transform head = Child("Head Pivot", visual, new Vector3(0, 2.02f, 0.7f));
                Part("Head", PrimitiveType.Cube, head, new Vector3(0, 0, 0.1f), new Vector3(0.44f, 0.43f, 0.58f), fur);
                Part("Muzzle", PrimitiveType.Cube, head, new Vector3(0, -0.1f, 0.42f), new Vector3(0.31f, 0.25f, 0.29f), cream);
                Part("Nose", PrimitiveType.Cube, head, new Vector3(0, -0.06f, 0.57f), new Vector3(0.23f, 0.15f, 0.08f), dark);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Eye", PrimitiveType.Sphere, head, new Vector3(side * 0.225f, 0.08f, 0.26f), Vector3.one * 0.1f, dark);
                    Part("Eye Glint", PrimitiveType.Sphere, head, new Vector3(side * 0.265f, 0.1f, 0.28f), Vector3.one * 0.025f, cream);
                    Part("Ear", PrimitiveType.Cube, head, new Vector3(side * 0.34f, 0.22f, -0.05f), new Vector3(0.32f, 0.13f, 0.21f), fur).localRotation = Quaternion.Euler(0, 0, side * 25);
                    Segment("Antler", head, new Vector3(side * 0.14f, 0.2f, -0.03f), new Vector3(side * 0.27f, 0.73f, -0.12f), 0.065f, antler);
                    Segment("Antler Tip", head, new Vector3(side * 0.27f, 0.73f, -0.12f), new Vector3(side * 0.48f, 0.88f, 0), 0.045f, antler);
                    Segment("Antler Branch", head, new Vector3(side * 0.22f, 0.52f, -0.08f), new Vector3(side * 0.43f, 0.65f, 0.16f), 0.045f, antler);
                }
                Part("Tail", PrimitiveType.Cube, visual, new Vector3(0, 1.34f, -0.83f), new Vector3(0.22f, 0.18f, 0.33f), cream).localRotation = Quaternion.Euler(-25, 0, 0);
                deer.AddComponent<DeerAnimation>().Configure(visual, head, legs);
                return PrefabUtility.SaveAsPrefabAsset(deer, PrefabPath);
            }
            finally { Object.DestroyImmediate(deer); }
        }

        private static Transform Child(string name, Transform parent, Vector3 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = position;
            return child;
        }

        private static Transform Part(string name, PrimitiveType primitive, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        private static void Segment(string name, Transform parent, Vector3 from, Vector3 to, float width, Material material)
        {
            Transform part = Part(name, PrimitiveType.Cylinder, parent, (from + to) * 0.5f,
                new Vector3(width, Vector3.Distance(from, to) * 0.5f, width), material);
            part.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
        }

        private static Material Material(string name, Color color)
        {
            string path = "Assets/Materials/Deer/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new System.InvalidOperationException("URP Lit shader missing.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void Folder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent))
                throw new System.IO.DirectoryNotFoundException("Expected existing folder " + parent);
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        public static void IntegrateBatch()
        {
            if (!Application.isBatchMode) throw new System.InvalidOperationException("Batch only");
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            int originalItems = Object.FindObjectsByType<PickupItem>().Length;
            int originalEnvironment = GameObject.Find("Environment").GetComponentsInChildren<Transform>().Length;
            AddDeer();
            AddDeer();
            if (Object.FindObjectsByType<DeerAI>().Length != 3 || Object.FindObjectsByType<ForestNavigation>().Length != 1)
                throw new System.InvalidOperationException("Deer setup is not idempotent.");
            if (Object.FindObjectsByType<PickupItem>().Length != originalItems ||
                GameObject.Find("Environment").GetComponentsInChildren<Transform>().Length != originalEnvironment)
                throw new System.InvalidOperationException("Existing forest or pickups were changed.");
            foreach (DeerAI deer in Object.FindObjectsByType<DeerAI>())
                if (!NavMesh.SamplePosition(deer.transform.position, out _, 0.3f, NavMesh.AllAreas))
                    throw new System.InvalidOperationException("Deer spawn is off NavMesh.");
            if (!EditorSceneManager.SaveScene(scene)) throw new System.IO.IOException("Could not save scene.");
            Debug.Log("Deer integration PASSED: baked navigation, three agents, no duplicates, existing forest/pickups preserved.");
        }
    }
}
