using System;
using System.Linq;
using HuntHappens.Animals;
using HuntHappens.Delivery;
using HuntHappens.Interaction;
using HuntHappens.Player;
using HuntHappens.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace HuntHappens.Editor
{
    public static class DeliverySystemSetup
    {
        private const string TruckPath = "Assets/Prefabs/PickupTruck.prefab";

        [MenuItem("Tools/Hunt Happens/Setup Delivery System")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("Exit Play Mode before setting up delivery."); return; }
            Scene scene = SceneManager.GetActiveScene();
            FirstPersonMotor player = InScene<FirstPersonMotor>(scene).FirstOrDefault();
            InteractionHUD hud = InScene<InteractionHUD>(scene).FirstOrDefault();
            DeerAI[] animals = InScene<DeerAI>(scene);
            if (player == null || hud == null || animals.Length == 0 || player.GetComponent<PlayerInteraction>() == null)
                throw new InvalidOperationException("Open ForestPrototype with FPS, interaction and sedatable deer first.");
            var deerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Animals/Deer.prefab");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Materials/Tranquilizer/HUD.asset");
            if (deerPrefab == null || deerPrefab.GetComponent<DeerSedation>() == null || font == null)
                throw new InvalidOperationException("Run Setup Tranquilizer first.");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Delivery")) AssetDatabase.CreateFolder("Assets/Materials", "Delivery");
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup delivery system");
            DeliveryManager manager = InScene<DeliveryManager>(scene).FirstOrDefault();
            if (manager == null)
            {
                var root = new GameObject("Delivery System");
                Undo.RegisterCreatedObjectUndo(root, "Create delivery system");
                manager = Undo.AddComponent<DeliveryManager>(root);
            }
            DeerSpawner spawner = manager.GetComponent<DeerSpawner>();
            if (spawner == null) spawner = Undo.AddComponent<DeerSpawner>(manager.gameObject);
            Undo.RecordObject(spawner, "Configure deer replacements");
            spawner.Configure(deerPrefab.GetComponent<DeerSedation>(), player.transform, animals[0].transform.parent);
            Undo.RecordObject(manager, "Connect rewards and replacements");
            manager.SetSpawner(spawner);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TruckPath);
            if (prefab == null) prefab = CreateTruckPrefab();
            PickupTruckDelivery truck = InScene<PickupTruckDelivery>(scene).FirstOrDefault();
            if (truck == null)
            {
                Vector3 position = FindTruckPosition(player.transform.position);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Place pickup truck");
                instance.transform.position = position;
                truck = instance.GetComponent<PickupTruckDelivery>();
            }
            Undo.RecordObject(truck, "Connect pickup delivery");
            truck.Configure(truck.GetComponentInChildren<DeliveryZone>(), truck.transform.Find("Rear Interaction Point"), manager);
            PrefabUtility.RecordPrefabInstancePropertyModifications(truck);
            PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
            Undo.RecordObject(interaction, "Connect E delivery interaction");
            interaction.ConfigureDelivery(truck);
            PrefabUtility.RecordPrefabInstancePropertyModifications(interaction);
            GameHUD gameHUD = hud.GetComponent<GameHUD>();
            if (gameHUD == null) gameHUD = Undo.AddComponent<GameHUD>(hud.gameObject);
            Undo.RecordObject(gameHUD, "Configure balance HUD");
            gameHUD.Configure(manager, font);
            foreach (UnityEngine.Object value in new UnityEngine.Object[] { manager, spawner, truck, interaction, gameHUD }) EditorUtility.SetDirty(value);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            Debug.Log("Delivery ready. Pickup truck at " + truck.transform.position + ". Save scene, Play. Release a sleeping deer fully in the bed, wait for it to settle, then E at the rear.");
        }

        private static T[] InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static Vector3 FindTruckPosition(Vector3 player)
        {
            Physics.SyncTransforms();
            var filter = new NavMeshQueryFilter { agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID, areaMask = NavMesh.AllAreas };
            for (float radius = 7; radius <= 22; radius += 3)
                for (int i = 0; i < 24; i++)
                {
                    float angle = (i * 15 + 90) * Mathf.Deg2Rad;
                    Vector3 candidate = player + new Vector3(Mathf.Sin(angle) * radius, 0, Mathf.Cos(angle) * radius);
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, filter)) continue;
                    if (Physics.OverlapBox(hit.position + Vector3.up * 1.6f, new Vector3(2.8f, 1.5f, 5.4f),
                            Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Length != 0) continue;
                    return hit.position;
                }
            throw new InvalidOperationException("No clear space for truck near the player. Free a 6 x 11 m area and run setup again.");
        }

        private static GameObject CreateTruckPrefab()
        {
            Material blue = Material("Truck Blue", new Color(0.08f, 0.5f, 0.75f));
            Material orange = Material("Trim", new Color(1, 0.55f, 0.1f));
            Material dark = Material("Tires", new Color(0.055f, 0.07f, 0.085f));
            Material metal = Material("Bed", new Color(0.36f, 0.43f, 0.46f));
            Material glass = Material("Windows", new Color(0.45f, 0.85f, 0.95f));
            var root = new GameObject("PickupTruck");
            try
            {
                Part("Chassis", root.transform, PrimitiveType.Cube, new Vector3(0, 0.46f, 0), new Vector3(4.6f, 0.32f, 9f), metal);
                Part("Bed Floor", root.transform, PrimitiveType.Cube, new Vector3(0, 0.65f, -1.4f), new Vector3(4.8f, 0.16f, 5.8f), metal);
                Part("Left Bed Rail", root.transform, PrimitiveType.Cube, new Vector3(-2.4f, 0.99f, -1.4f), new Vector3(0.18f, 0.52f, 5.8f), blue);
                Part("Right Bed Rail", root.transform, PrimitiveType.Cube, new Vector3(2.4f, 0.99f, -1.4f), new Vector3(0.18f, 0.52f, 5.8f), blue);
                Part("Front Bed Rail", root.transform, PrimitiveType.Cube, new Vector3(0, 1.03f, 1.5f), new Vector3(4.8f, 0.6f, 0.18f), blue);
                Part("Tailgate", root.transform, PrimitiveType.Cube, new Vector3(0, 0.87f, -4.3f), new Vector3(4.8f, 0.28f, 0.18f), orange);
                Part("Rear Step", root.transform, PrimitiveType.Cube, new Vector3(0, 0.3f, -4.65f), new Vector3(3.8f, 0.18f, 0.6f), metal);
                Part("Cabin", root.transform, PrimitiveType.Cube, new Vector3(0, 1.5f, 2.6f), new Vector3(4.1f, 1.9f, 2.05f), blue);
                Part("Roof", root.transform, PrimitiveType.Cube, new Vector3(0, 2.52f, 2.6f), new Vector3(4.4f, 0.2f, 2.2f), orange);
                Part("Hood", root.transform, PrimitiveType.Cube, new Vector3(0, 1.05f, 4f), new Vector3(4.15f, 0.85f, 0.95f), blue);
                Part("Windshield", root.transform, PrimitiveType.Cube, new Vector3(0, 1.91f, 3.64f), new Vector3(3.65f, 0.8f, 0.04f), glass, false);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Side Window", root.transform, PrimitiveType.Cube, new Vector3(side * 2.06f, 1.91f, 2.65f), new Vector3(0.04f, 0.8f, 1.65f), glass, false);
                    for (int axle = 0; axle < 2; axle++)
                    {
                        Transform wheel = Part("Wheel", root.transform, PrimitiveType.Cylinder,
                            new Vector3(side * 2.32f, 0.55f, axle == 0 ? -2.7f : 3.1f), new Vector3(1.1f, 0.27f, 1.1f), dark, false);
                        wheel.localRotation = Quaternion.Euler(0, 0, 90);
                        // Decorative cylinders use matching simple sphere colliders at the wheels.
                        wheel.gameObject.AddComponent<SphereCollider>().radius = 0.5f;
                    }
                }
                var zoneObject = new GameObject("Delivery Zone");
                zoneObject.transform.SetParent(root.transform, false);
                zoneObject.transform.localPosition = new Vector3(0, 2.0f, -1.4f);
                var box = zoneObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(4.6f, 2.5f, 5.5f);
                var zone = zoneObject.AddComponent<DeliveryZone>();
                var rear = new GameObject("Rear Interaction Point").transform;
                rear.SetParent(root.transform, false);
                rear.localPosition = new Vector3(0, 1.3f, -4.95f);
                root.AddComponent<PickupTruckDelivery>().Configure(zone, rear, null);
                var obstacle = root.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = new Vector3(0, 1.5f, 0);
                obstacle.size = new Vector3(5.1f, 3f, 9.8f);
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
                return PrefabUtility.SaveAsPrefabAsset(root, TruckPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Transform Part(string name, Transform parent, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material, bool collider = true)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            if (!collider) UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        private static Material Material(string name, Color color)
        {
            string path = "Assets/Materials/Delivery/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static void IntegrateBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch only");
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            int originalDeer = InScene<DeerSedation>(scene).Length;
            int originalPickups = InScene<PickupItem>(scene).Length;
            Setup();
            Setup();
            if (InScene<PickupTruckDelivery>(scene).Length != 1 || InScene<DeliveryZone>(scene).Length != 1 ||
                InScene<DeliveryManager>(scene).Length != 1 || InScene<DeerSpawner>(scene).Length != 1 || InScene<GameHUD>(scene).Length != 1)
                throw new InvalidOperationException("Delivery setup has missing or duplicate components.");
            if (InScene<DeerSedation>(scene).Length != originalDeer || InScene<PickupItem>(scene).Length != originalPickups)
                throw new InvalidOperationException("Delivery setup altered existing deer/pickups.");
            if (!EditorSceneManager.SaveScene(scene)) throw new System.IO.IOException("Scene save failed");
            Debug.Log("Delivery setup PASSED: one truck/zone/manager/spawner/HUD, no duplicates, existing animals and pickups preserved.");
        }
    }
}
