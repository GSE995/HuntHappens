using System;
using System.Linq;
using HuntHappens.Delivery;
using HuntHappens.Interaction;
using HuntHappens.Player;
using HuntHappens.Shop;
using HuntHappens.Traps;
using HuntHappens.Weapons;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace HuntHappens.Editor
{
    public static class ShopSetup
    {
        [MenuItem("Tools/Hunt Happens/Setup Shop")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Exit Play Mode before shop setup."); return; }
            Scene scene = SceneManager.GetActiveScene();
            var wallet = InScene<DeliveryManager>(scene).FirstOrDefault();
            var truck = InScene<PickupTruckDelivery>(scene).FirstOrDefault();
            var gun = InScene<TranquilizerGun>(scene).FirstOrDefault();
            var traps = InScene<TrapManager>(scene).FirstOrDefault();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Materials/Tranquilizer/HUD.asset");
            if (wallet == null || truck == null || gun == null || traps == null || font == null)
                throw new InvalidOperationException("Open ForestPrototype with delivery, traps and tranquilizer first.");
            Folder("Assets", "Shop");
            Folder("Assets/Shop", "Upgrades");
            Folder("Assets/Materials", "Shop");
            UpgradeDefinition[] definitions =
            {
                Definition(UpgradeKind.StrongerDarts, "Stronger Darts", "Олень засыпает быстрее после попадания дротика.", new[] { 200, 400, 700 }, new[] { 1f, 0.8f, 0.65f, 0.5f }),
                Definition(UpgradeKind.MoreTraps, "More Traps", "Больше установленных ловушек одновременно.", new[] { 150, 300, 600 }, new[] { 3f, 4f, 5f, 6f }),
                Definition(UpgradeKind.StrongerArms, "Stronger Arms", "Больше допустимая масса и устойчивее переноска.", new[] { 200, 350, 600 }, new[] { 1f, 1.25f, 1.5f, 1.75f }),
                Definition(UpgradeKind.BetterContracts, "Better Contracts", "Больше денег за каждого доставленного оленя.", new[] { 300, 500, 800 }, new[] { 100f, 125f, 150f, 200f })
            };
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup shop");
            ShopManager shop = InScene<ShopManager>(scene).FirstOrDefault();
            if (shop == null)
            {
                var root = new GameObject("Shop System");
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Create shop system");
                shop = Undo.AddComponent<ShopManager>(root);
            }
            UpgradeManager upgrades = shop.GetComponent<UpgradeManager>();
            if (upgrades == null) upgrades = Undo.AddComponent<UpgradeManager>(shop.gameObject);
            ShopUI ui = shop.GetComponent<ShopUI>();
            if (ui == null) ui = Undo.AddComponent<ShopUI>(shop.gameObject);
            ShopInteractable kiosk = InScene<ShopInteractable>(scene).FirstOrDefault();
            if (kiosk == null)
            {
                Vector3 position = FindPosition(truck.transform);
                GameObject model = CreateKiosk(font);
                SceneManager.MoveGameObjectToScene(model, scene);
                model.transform.SetPositionAndRotation(position, truck.transform.rotation);
                Undo.RegisterCreatedObjectUndo(model, "Place shop kiosk");
                kiosk = model.GetComponent<ShopInteractable>();
            }
            Undo.RecordObjects(new UnityEngine.Object[] { shop, upgrades, ui }, "Connect shop systems");
            upgrades.Configure(definitions, wallet, gun, traps, gun.GetComponent<ItemHolder>());
            ui.Configure(shop, font);
            shop.Configure(wallet, upgrades, ui, kiosk, gun.GetComponent<PrototypeInput>());
            PlayerInteraction interaction = gun.GetComponent<PlayerInteraction>();
            Undo.RecordObject(interaction, "Connect existing E interaction");
            interaction.ConfigureShop(shop);
            PrefabUtility.RecordPrefabInstancePropertyModifications(interaction);
            var events = InScene<EventSystem>(scene).FirstOrDefault();
            if (events == null)
            {
                var root = new GameObject("UI Event System");
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Create UI event system");
                events = Undo.AddComponent<EventSystem>(root);
            }
            if (events.GetComponent<InputSystemUIInputModule>() == null)
            {
                foreach (BaseInputModule legacy in events.GetComponents<BaseInputModule>())
                { Undo.RecordObject(legacy, "Use Input System UI"); legacy.enabled = false; }
                Undo.AddComponent<InputSystemUIInputModule>(events.gameObject);
            }
            foreach (UnityEngine.Object component in new UnityEngine.Object[] { shop, upgrades, ui, interaction, events }) EditorUtility.SetDirty(component);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Shop ready near truck at " + kiosk.transform.position + ". Save scene, Play, approach SHOP with empty hands and press E. Prices: Assets/Shop/Upgrades.");
        }
        private static T[] InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        private static void Folder(string parent, string name) { if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name); }
        private static UpgradeDefinition Definition(UpgradeKind kind, string title, string description, int[] prices, float[] effects)
        {
            string path = "Assets/Shop/Upgrades/" + title + ".asset";
            var definition = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<UpgradeDefinition>();
                definition.Configure(kind, title, description, prices, effects);
                AssetDatabase.CreateAsset(definition, path);
            }
            if (!definition.IsValid || definition.Kind != kind) throw new InvalidOperationException("Invalid upgrade configuration: " + path);
            return definition;
        }
        private static Vector3 FindPosition(Transform truck)
        {
            Physics.SyncTransforms();
            for (float radius = 6; radius <= 16; radius += 2)
                for (int side = -1; side <= 1; side += 2)
                    for (int offset = -4; offset <= 4; offset += 4)
                    {
                        Vector3 point = truck.TransformPoint(new Vector3(side * radius, 0, offset));
                        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 1, NavMesh.AllAreas)) continue;
                        if (Physics.OverlapBox(hit.position + Vector3.up * 1.7f, new Vector3(1.8f, 1.6f, 2.7f), truck.rotation, ~0, QueryTriggerInteraction.Ignore).Length != 0) continue;
                        Vector3 approach = hit.position - truck.forward * 2.3f;
                        if (!NavMesh.SamplePosition(approach, out NavMeshHit entrance, 0.6f, NavMesh.AllAreas)) continue;
                        return hit.position;
                    }
            throw new InvalidOperationException("No clear space near truck for SHOP. Free a 4 x 6 m area and run Setup Shop again.");
        }
        private static GameObject CreateKiosk(TMP_FontAsset font)
        {
            Material wood = Material("Wood", new Color(0.48f, 0.24f, 0.1f));
            Material teal = Material("Teal", new Color(0.08f, 0.62f, 0.57f));
            Material orange = Material("Orange", new Color(1, 0.58f, 0.12f));
            Material dark = Material("Display", new Color(0.08f, 0.18f, 0.23f));
            var root = new GameObject("SHOP Kiosk");
            Part("Base", root.transform, new Vector3(0, 0.12f, 0), new Vector3(2.8f, 0.24f, 2), wood);
            Part("Cabinet", root.transform, new Vector3(0, 1.25f, 0), new Vector3(2.3f, 2.3f, 1.25f), teal);
            Part("Front Display", root.transform, new Vector3(-0.3f, 1.55f, -0.66f), new Vector3(1.25f, 1.25f, 0.08f), dark);
            Part("Counter", root.transform, new Vector3(0, 0.85f, -0.85f), new Vector3(2.6f, 0.2f, 0.7f), wood);
            Part("Roof", root.transform, new Vector3(0, 2.6f, 0), new Vector3(2.8f, 0.25f, 1.9f), orange);
            Part("Sign Board", root.transform, new Vector3(0, 3, 0), new Vector3(2.7f, 0.75f, 0.2f), dark);
            var sign = new GameObject("SHOP Sign").AddComponent<TextMeshPro>();
            sign.transform.SetParent(root.transform, false);
            sign.transform.localPosition = new Vector3(0, 3, -0.13f);
            sign.rectTransform.sizeDelta = new Vector2(2.5f, 0.7f);
            sign.font = font;
            sign.text = "SHOP";
            sign.fontSize = 5;
            sign.alignment = TextAlignmentOptions.Center;
            sign.color = new Color(1, 0.85f, 0.2f);
            for (int i = 0; i < 3; i++) Part("Bright Button", root.transform, new Vector3(0.85f, 1.1f + i * 0.3f, -0.7f), Vector3.one * 0.16f, orange);
            var point = new GameObject("Interaction Point").transform;
            point.SetParent(root.transform, false);
            point.localPosition = new Vector3(0, 1.3f, -1.3f);
            root.AddComponent<ShopInteractable>().Configure(point);
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0, 1.6f, 0);
            obstacle.size = new Vector3(3, 3.4f, 2.2f);
            obstacle.carving = true;
            return root;
        }
        private static void Part(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
        private static Material Material(string name, Color color)
        {
            string path = "Assets/Materials/Shop/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
        [MenuItem("Tools/Hunt Happens/Reset Shop Progress")]
        public static void ResetProgress()
        {
            var shop = Application.isPlaying ? UnityEngine.Object.FindAnyObjectByType<ShopManager>() : null;
            if (shop != null) shop.ResetProgress(); else ShopProgress.Reset();
            Debug.Log("Hunt Happens: balance and upgrade levels reset. Other PlayerPrefs preserved.");
        }
        public static void IntegrateBatch()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            Transform[] original = InScene<Transform>(scene);
            Setup();
            int count = InScene<Transform>(scene).Length;
            Setup();
            if (InScene<ShopManager>(scene).Length != 1 || InScene<UpgradeManager>(scene).Length != 1 || InScene<ShopUI>(scene).Length != 1 ||
                InScene<ShopInteractable>(scene).Length != 1 || InScene<EventSystem>(scene).Length != 1 || InScene<InputSystemUIInputModule>(scene).Length != 1 ||
                count != InScene<Transform>(scene).Length || original.Any(t => t == null)) throw new InvalidOperationException("Shop setup duplicated or removed objects");
            if (!EditorSceneManager.SaveScene(scene)) throw new System.IO.IOException("Scene save failed");
            Debug.Log("Shop setup PASSED: one kiosk/UI/manager/upgrades/event system, original objects preserved, repeat setup idempotent, scene saved.");
        }
    }
}
