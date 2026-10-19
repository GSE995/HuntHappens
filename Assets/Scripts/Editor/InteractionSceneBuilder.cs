using HuntHappens.Interaction;
using HuntHappens.Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HuntHappens.Editor
{
    public static class InteractionSceneBuilder
    {
        private const string RootName = "Prototype Interaction Objects";

        [MenuItem("Tools/Hunt Happens/Add Interaction Objects")]
        public static void AddInteractionObjects()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Exit Play Mode before adding interaction objects.");
                return;
            }
            Scene scene = SceneManager.GetActiveScene();
            FirstPersonMotor player = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                player = root.GetComponentInChildren<FirstPersonMotor>();
                if (player != null) break;
            }
            Camera camera = player != null ? player.GetComponentInChildren<Camera>() : null;
            if (camera == null)
            {
                Debug.LogError("The active scene needs an FPS Player with FirstPersonMotor and a child Camera.");
                return;
            }
            TMP_FontAsset font = EnsureFont();
            if (font == null) return;
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Hunt Happens interaction");
            ItemHolder holder = player.GetComponent<ItemHolder>();
            if (holder == null) holder = Undo.AddComponent<ItemHolder>(player.gameObject);
            PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
            if (interaction == null) interaction = Undo.AddComponent<PlayerInteraction>(player.gameObject);
            Undo.RecordObject(holder, "Set holder camera");
            Undo.RecordObject(interaction, "Set interaction camera");
            interaction.SetView(camera);
            PrefabUtility.RecordPrefabInstancePropertyModifications(holder);
            PrefabUtility.RecordPrefabInstancePropertyModifications(interaction);

            GameObject container = null;
            InteractionHUD hud = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == RootName) container = root;
                if (hud == null) hud = root.GetComponentInChildren<InteractionHUD>(true);
            }
            if (container == null)
            {
                container = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(container, "Create interaction objects");
            }
            if (hud == null)
            {
                var hudObject = new GameObject("Interaction HUD");
                Undo.RegisterCreatedObjectUndo(hudObject, "Create interaction HUD");
                hud = Undo.AddComponent<InteractionHUD>(hudObject);
            }
            Undo.RecordObject(hud, "Connect interaction HUD");
            hud.SetInteraction(interaction);
            hud.SetFont(font);

            Material wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ForestPrototype/Crate.mat");
            Material stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ForestPrototype/Rock.mat");
            Material bark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ForestPrototype/Bark.mat");
            // Positions are near the original spawn, away from the existing static crates.
            AddItem(container.transform, "Pickup Crate Small", PrimitiveType.Cube, new Vector3(-1.5f, 0.3f, -5), Vector3.one * 0.6f, 2, wood);
            AddItem(container.transform, "Pickup Crate Medium", PrimitiveType.Cube, new Vector3(0, 0.45f, -3.8f), Vector3.one * 0.9f, 5, wood);
            AddItem(container.transform, "Pickup Crate Large", PrimitiveType.Cube, new Vector3(1.8f, 0.6f, -3), Vector3.one * 1.2f, 10, wood);
            AddItem(container.transform, "Pickup Rock A", PrimitiveType.Sphere, new Vector3(-2.8f, 0.23f, -3), new Vector3(0.65f, 0.46f, 0.55f), 3, stone);
            AddItem(container.transform, "Pickup Rock B", PrimitiveType.Sphere, new Vector3(-1.6f, 0.3f, -1.5f), new Vector3(0.55f, 0.6f, 0.5f), 4, stone);
            AddItem(container.transform, "Pickup Barrel", PrimitiveType.Cylinder, new Vector3(3, 0.65f, -5), new Vector3(0.8f, 0.65f, 0.8f), 7, bark);
            EditorUtility.SetDirty(holder);
            EditorUtility.SetDirty(interaction);
            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("Interaction added without rebuilding the forest. Save the scene (Ctrl+S), then Play. E: pick up/drop, left mouse: throw.");
        }

        private static TMP_FontAsset EnsureFont()
        {
            // TMP ships its shaders/fonts as optional resources inside the installed uGUI package.
            const string sourcePath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
            if (AssetDatabase.LoadAssetAtPath<Font>(sourcePath) == null)
            {
                string package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly).resolvedPath;
                var sceneHandle = SceneManager.GetActiveScene().handle;
                void Imported(string packageName)
                {
                    AssetDatabase.importPackageCompleted -= Imported;
                    EditorApplication.delayCall += () =>
                    {
                        if (SceneManager.GetActiveScene().handle == sceneHandle) AddInteractionObjects();
                        else Debug.LogWarning("TMP resources imported. Run Add Interaction Objects in the intended scene.");
                    };
                }
                AssetDatabase.importPackageCompleted += Imported;
                UnityEditor.AssetPackage.Package.Import(package + "/Package Resources/TMP Essential Resources.unitypackage", false);
                Debug.Log("Importing built-in TMP resources; interaction setup will continue after import.");
                return null;
            }
            const string path = "Assets/Materials/ForestPrototype/InteractionFont.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/ForestPrototype")) AssetDatabase.CreateFolder("Assets/Materials", "ForestPrototype");
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (sourceFont == null) throw new System.InvalidOperationException("TMP Resources are importing. Wait for import, then run Add Interaction Objects again.");
            var font = TMP_FontAsset.CreateFontAsset(sourceFont);
            if (font == null || !font.TryAddCharacters("E — ПоднятьОтпустить | ЛКМБросить", out _))
                throw new System.InvalidOperationException("Could not create a Cyrillic TMP font.");
            font.name = "InteractionFont";
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, path);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (Texture2D atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }

        private static void AddItem(Transform parent, string name, PrimitiveType primitive, Vector3 position, Vector3 scale, float mass, Material material)
        {
            if (parent.Find(name) != null) return;
            GameObject item = GameObject.CreatePrimitive(primitive);
            Undo.RegisterCreatedObjectUndo(item, "Create " + name);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.position = position;
            item.transform.localScale = scale;
            if (material != null) item.GetComponent<Renderer>().sharedMaterial = material;
            // Unity's cylinder primitive has a capsule collider. A convex mesh follows the barrel shape.
            if (primitive == PrimitiveType.Cylinder)
            {
                Object.DestroyImmediate(item.GetComponent<Collider>());
                var meshCollider = item.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = item.GetComponent<MeshFilter>().sharedMesh;
                meshCollider.convex = true;
            }
            var body = item.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            item.AddComponent<PickupItem>();
        }
    }
}
