using HuntHappens.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HuntHappens.Editor
{
    public static class PrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/ForestPrototype.unity";

        [MenuItem("Tools/Hunt Happens/Create Prototype Scene")]
        public static void CreatePrototypeScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Exit Play Mode before generating the prototype.");
                return;
            }
#if !ENABLE_INPUT_SYSTEM
            Debug.LogError("Enable Input System in Edit > Project Settings > Player > Active Input Handling, then restart Unity.");
            return;
#else
            // Keep existing scenes and assets intact, including a previously generated prototype.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(ScenePath);
                    InteractionSceneBuilder.AddInteractionObjects();
                    EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                }
                Debug.Log("Opened existing ForestPrototype. To generate a fresh scene, first move or rename the existing scene asset.");
                return;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("URP Lit shader is missing. Check that Universal RP is installed in Package Manager.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets", "Materials");
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Materials", "ForestPrototype");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material grass = Material("Grass", new Color(0.36f, 0.64f, 0.24f), shader);
            Material bark = Material("Bark", new Color(0.38f, 0.22f, 0.12f), shader);
            Material leaves = Material("Leaves", new Color(0.19f, 0.48f, 0.22f), shader);
            Material rock = Material("Rock", new Color(0.48f, 0.53f, 0.56f), shader);
            Material wood = Material("Crate", new Color(0.68f, 0.43f, 0.21f), shader);
            var environment = new GameObject("Environment").transform;
            Primitive("Ground", PrimitiveType.Cube, new Vector3(0, -0.5f, 0), new Vector3(100, 1, 100), grass, environment);
            // A private deterministic generator does not modify UnityEngine.Random's global state.
            var random = new System.Random(42);
            for (int i = 0; i < 48; i++)
            {
                float x = (float)random.NextDouble() * 84f - 42f;
                float z = (float)random.NextDouble() * 84f - 42f;
                if (Mathf.Abs(x) < 7f && Mathf.Abs(z) < 12f) continue;
                float height = 3f + (float)random.NextDouble() * 2f;
                var tree = new GameObject($"Tree {i:00}").transform;
                tree.SetParent(environment, false);
                tree.position = new Vector3(x, 0, z);
                Primitive("Trunk", PrimitiveType.Cylinder, new Vector3(x, height / 2, z), new Vector3(0.65f, height / 2, 0.65f), bark, tree);
                Primitive("Crown", PrimitiveType.Sphere, new Vector3(x, height + 0.4f, z), new Vector3(3.6f, 3.5f, 3.6f), leaves, tree);
            }
            for (int i = 0; i < 10; i++)
            {
                float x = i % 2 == 0 ? -5f - i * 1.8f : 5f + i * 1.5f;
                float z = -2f + i * 3f;
                Primitive($"Rock {i:00}", PrimitiveType.Sphere, new Vector3(x, 0.5f, z), new Vector3(2.1f, 1.4f, 1.7f), rock, environment);
            }
            for (int i = 0; i < 5; i++)
            {
                GameObject crate = Primitive($"Crate {i:00}", PrimitiveType.Cube, new Vector3(2.5f + i * 1.8f, 0.6f, 5f + i % 2 * 2f), Vector3.one * 1.2f, wood, environment);
                crate.transform.rotation = Quaternion.Euler(0, i * 17f, 0);
            }
            // Low perimeter hedges make the edge visible and prevent walking off the test area.
            Primitive("North hedge", PrimitiveType.Cube, new Vector3(0, 1, 49), new Vector3(100, 2, 2), leaves, environment);
            Primitive("South hedge", PrimitiveType.Cube, new Vector3(0, 1, -49), new Vector3(100, 2, 2), leaves, environment);
            Primitive("East hedge", PrimitiveType.Cube, new Vector3(49, 1, 0), new Vector3(2, 2, 100), leaves, environment);
            Primitive("West hedge", PrimitiveType.Cube, new Vector3(-49, 1, 0), new Vector3(2, 2, 100), leaves, environment);

            var sun = new GameObject("Daylight").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.86f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.65f;
            sun.transform.rotation = Quaternion.Euler(48, -30, 0);
            sun.gameObject.AddComponent<UniversalAdditionalLightData>();
            RenderSettings.sun = sun;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.65f, 0.73f, 0.8f);
            RenderSettings.fog = false;

            var player = new GameObject("Player");
            player.transform.position = new Vector3(0, 0.1f, -8);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0, 0.9f, 0);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45;
            controller.minMoveDistance = 0;
            player.AddComponent<PrototypeInput>();
            var look = player.AddComponent<FirstPersonLook>();
            player.AddComponent<FirstPersonMotor>();
            var cameraObject = new GameObject("First Person Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 1.62f, 0);
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 75;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 180f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.48f, 0.76f, 0.94f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderShadows = true;
            look.SetCamera(cameraObject.transform);
            string prefabPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Prefabs/PrototypePlayer.prefab");
            PrefabUtility.SaveAsPrefabAssetAndConnect(player, prefabPath, InteractionMode.AutomatedAction);
            InteractionSceneBuilder.AddInteractionObjects();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new System.IO.IOException("Could not save " + ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            Debug.Log("ForestPrototype saved. Press Play. WASD: move, mouse: look, Space: jump, Escape: release cursor, left click: capture cursor.");
#endif
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        private static Material Material(string name, Color color, Shader shader)
        {
            string path = "Assets/Materials/ForestPrototype/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0f);
            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(path));
            return material;
        }

        private static GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            GameObject result = GameObject.CreatePrimitive(type);
            result.name = name;
            result.transform.SetParent(parent, false);
            result.transform.position = position;
            result.transform.localScale = scale;
            result.GetComponent<Renderer>().sharedMaterial = material;
            return result;
        }
    }
}
