using System;
using System.Linq;
using HuntHappens.Animals;
using HuntHappens.Interaction;
using HuntHappens.Player;
using HuntHappens.Weapons;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HuntHappens.Editor
{
    public static class TranquilizerSetup
    {
        private const string DeerPath = "Assets/Prefabs/Animals/Deer.prefab";
        private const string DartPath = "Assets/Prefabs/TranquilizerDart.prefab";

        [MenuItem("Tools/Hunt Happens/Setup Tranquilizer")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("Exit Play Mode before setting up the tranquilizer."); return; }
            Scene scene = SceneManager.GetActiveScene();
            FirstPersonMotor player = InScene<FirstPersonMotor>(scene).FirstOrDefault();
            if (player == null || player.GetComponentInChildren<Camera>() == null || !InScene<DeerAI>(scene).Any())
            { Debug.LogError("Open ForestPrototype with its FPS Player and deer first."); return; }
            Folder("Assets/Materials", "Tranquilizer");
            TMP_FontAsset font = CreateFont();
            UpdateDeerPrefab(font);
            foreach (DeerAI deer in InScene<DeerAI>(scene))
            {
                // Also handles unpacked/custom scene deer, preserving model and AI settings.
                if (deer.GetComponent<DeerSedation>() == null) ConfigureDeer(deer.gameObject, font);
            }
            Camera camera = player.GetComponentInChildren<Camera>();
            ItemHolder holder = player.GetComponent<ItemHolder>();
            if (holder == null || player.GetComponent<PlayerInteraction>() == null)
                throw new InvalidOperationException("Set up item interaction before the tranquilizer.");
            Undo.RecordObject(holder, "Increase carry capacity");
            holder.EnsureMaximumMass(80);
            PrefabUtility.RecordPrefabInstancePropertyModifications(holder);
            var gun = player.GetComponent<TranquilizerGun>();
            if (gun == null) gun = Undo.AddComponent<TranquilizerGun>(player.gameObject);
            Transform model = camera.transform.Find("Tranquilizer Model");
            if (model == null) model = CreateGunModel(camera.transform);
            var dart = CreateDart();
            Undo.RecordObject(gun, "Configure tranquilizer");
            gun.Configure(camera, model, model.Find("Muzzle"), model.Find("Muzzle Flash").gameObject, dart);
            PrefabUtility.RecordPrefabInstancePropertyModifications(gun);
            foreach (InteractionHUD hud in InScene<InteractionHUD>(scene))
            {
                Undo.RecordObject(hud, "Set expanded HUD font");
                hud.SetFont(font);
                EditorUtility.SetDirty(hud);
            }
            EditorUtility.SetDirty(holder);
            EditorUtility.SetDirty(gun);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Tranquilizer ready. Save scene, Play. 1: tranquilizer, 2: interaction, left click: shoot or throw held item, E: pick up/drop.");
        }

        private static T[] InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static void UpdateDeerPrefab(TMP_FontAsset font)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(DeerPath);
            try
            {
                ConfigureDeer(root, font);
                PrefabUtility.SaveAsPrefabAsset(root, DeerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigureDeer(GameObject deer, TMP_FontAsset font)
        {
            PickupItem pickup = deer.GetComponent<PickupItem>();
            if (pickup == null)
            {
                pickup = deer.AddComponent<PickupItem>();
                pickup.ConfigureAnimal();
            }
            pickup.enabled = false;
            BoxCollider asleep = deer.GetComponent<BoxCollider>();
            if (asleep == null)
            {
                asleep = deer.AddComponent<BoxCollider>();
                asleep.center = new Vector3(-0.5f, 0.55f, 0.1f);
                asleep.size = new Vector3(2.7f, 1f, 2f);
            }
            asleep.enabled = false;
            var physics = deer.GetComponent<DeerPhysics>();
            if (physics == null) physics = deer.AddComponent<DeerPhysics>();
            physics.Configure(deer.GetComponent<CapsuleCollider>(), asleep);
            var sedation = deer.GetComponent<DeerSedation>();
            if (sedation == null) sedation = deer.AddComponent<DeerSedation>();
            Transform indicator = deer.transform.Find("Sleep Indicator");
            if (indicator == null)
            {
                indicator = new GameObject("Sleep Indicator", typeof(RectTransform)).transform;
                indicator.SetParent(deer.transform, false);
                indicator.localPosition = Vector3.up * 3;
                var text = indicator.gameObject.AddComponent<TextMeshPro>();
                text.font = font;
                text.text = "Zzz";
                text.fontSize = 3;
                text.alignment = TextAlignmentOptions.Center;
                text.color = new Color(0.3f, 0.9f, 1);
                text.rectTransform.sizeDelta = new Vector2(2, 1);
            }
            sedation.SetIndicator(indicator.GetComponent<TMP_Text>());
            indicator.gameObject.SetActive(false);
        }

        private static Transform CreateGunModel(Transform camera)
        {
            Material blue = Material("Blue", new Color(0.05f, 0.65f, 0.9f));
            Material orange = Material("Orange", new Color(1f, 0.42f, 0.06f));
            Material dark = Material("Dark", new Color(0.09f, 0.14f, 0.2f));
            Material glow = Material("Glow", new Color(1f, 0.94f, 0.25f), true);
            Transform model = new GameObject("Tranquilizer Model").transform;
            Undo.RegisterCreatedObjectUndo(model.gameObject, "Create tranquilizer model");
            model.SetParent(camera, false);
            model.localPosition = new Vector3(0.27f, -0.25f, 0.52f);
            Part("Body", model, PrimitiveType.Cube, Vector3.zero, new Vector3(0.18f, 0.16f, 0.36f), blue);
            Part("Grip", model, PrimitiveType.Cube, new Vector3(0, -0.14f, -0.08f), new Vector3(0.1f, 0.22f, 0.12f), dark);
            Part("Barrel", model, PrimitiveType.Cylinder, new Vector3(0, 0.02f, 0.3f), new Vector3(0.075f, 0.22f, 0.075f), dark).localRotation = Quaternion.Euler(90, 0, 0);
            Part("Bright Tip", model, PrimitiveType.Cylinder, new Vector3(0, 0.02f, 0.51f), new Vector3(0.11f, 0.04f, 0.11f), orange).localRotation = Quaternion.Euler(90, 0, 0);
            Part("Dose Tank", model, PrimitiveType.Cylinder, new Vector3(0.11f, 0.08f, -0.02f), new Vector3(0.08f, 0.15f, 0.08f), orange);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(model, false);
            muzzle.localPosition = new Vector3(0, 0.02f, 0.58f);
            var flash = Part("Muzzle Flash", model, PrimitiveType.Sphere, muzzle.localPosition, Vector3.one * 0.12f, glow);
            flash.gameObject.SetActive(false);
            return model;
        }

        private static TranquilizerDart CreateDart()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(DartPath);
            if (existing != null) return existing.GetComponent<TranquilizerDart>();
            Material glow = Material("Glow", new Color(1, 0.94f, 0.25f), true);
            Material blue = Material("Dart", new Color(0.15f, 0.9f, 0.8f), true);
            var root = new GameObject("Tranquilizer Dart");
            try
            {
                root.AddComponent<SphereCollider>().radius = 0.035f;
                var body = root.AddComponent<Rigidbody>();
                body.mass = 0.05f;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                Part("Visible Dart", root.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.045f, 0.16f, 0.045f), blue).localRotation = Quaternion.Euler(90, 0, 0);
                Part("Tail", root.transform, PrimitiveType.Cube, new Vector3(0, 0, -0.14f), new Vector3(0.13f, 0.08f, 0.06f), glow);
                var trail = root.AddComponent<TrailRenderer>();
                trail.time = 0.14f;
                trail.startWidth = 0.045f;
                trail.endWidth = 0;
                trail.sharedMaterial = glow;
                var dart = root.AddComponent<TranquilizerDart>();
                dart.SetImpactMaterial(glow);
                return PrefabUtility.SaveAsPrefabAsset(root, DartPath).GetComponent<TranquilizerDart>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Transform Part(string name, Transform parent, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.layer = 2;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        private static Material Material(string name, Color color, bool unlit = false)
        {
            string path = "Assets/Materials/Tranquilizer/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Required URP shader missing");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static TMP_FontAsset CreateFont()
        {
            const string path = "Assets/Materials/Tranquilizer/HUD.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            if (source == null) throw new InvalidOperationException("Import TMP Essential Resources first.");
            string characters = new string(Enumerable.Range(32, 95).Concat(Enumerable.Range(0x410, 64)).Select(i => (char)i).ToArray()) + "Ёё—";
            var font = TMP_FontAsset.CreateFontAsset(source);
            if (font == null || !font.TryAddCharacters(characters, out _)) throw new InvalidOperationException("HUD glyph bake failed");
            font.name = "Tranquilizer HUD";
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, path);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (Texture2D atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
            EditorUtility.SetDirty(font);
            return font;
        }

        private static void Folder(string parent, string name)
        { if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name); }

        public static void IntegrateBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only");
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            Setup();
            Setup();
            if (InScene<TranquilizerGun>(scene).Length != 1 || InScene<DeerSedation>(scene).Length != 3 ||
                InScene<DeerPhysics>(scene).Length != 3 || InScene<InteractionHUD>(scene).Length != 1)
                throw new InvalidOperationException("Tranquilizer setup duplicated or missed components");
            foreach (DeerSedation deer in InScene<DeerSedation>(scene))
                if (deer.GetComponents<PickupItem>().Length != 1 || deer.GetComponents<BoxCollider>().Length != 1)
                    throw new InvalidOperationException("Deer physics setup is not idempotent");
            if (!EditorSceneManager.SaveScene(scene)) throw new System.IO.IOException("Scene save failed");
            Debug.Log("Tranquilizer setup PASSED: one gun, three sedation/physics components, no duplicates, scene saved.");
        }
    }
}
