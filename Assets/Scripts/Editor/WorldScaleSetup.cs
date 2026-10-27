using System;
using System.Linq;
using HuntHappens.Animals;
using HuntHappens.Characters;
using HuntHappens.Player;
using HuntHappens.SmallGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    // Metres, measured on actual geometry; padded renderer bounds are not body dimensions.
    public static class WorldScaleSetup
    {
        public const float PlayerHeight = 1.8f, EyeHeight = 1.68f, HeroHeight = 1.85f, SemenychHeight = 1.75f;
        public const float DeerHeight = 1.9f, LeaderHeight = 2.2f, RatHeight = .12f, LargeRatHeight = .168f;
        public const float CrowLength = .45f, NavigationRadius = .42f, NavigationHeight = 2.2f;

        [MenuItem("Tools/Hunt Happens/Apply Realistic Character Scale")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/ForestPrototype.unity") throw new InvalidOperationException("Open ForestPrototype first");
            if (!EditorSceneManager.SaveOpenScenes()) throw new InvalidOperationException("Cannot save open scenes");
            EditPrefab("Assets/Characters/Camper/CamperPlayerVisual.prefab", go => FitVisual(go.transform, PlayerHeight, 0));
            EditPrefab("Assets/Prefabs/PrototypePlayer.prefab", ConfigurePlayer);
            EditPrefab("Assets/Art/Hero/Hero.prefab", ConfigureHero);
            EditPrefab("Assets/Prefabs/Animals/Deer.prefab", go => ConfigureDeer(go, DeerHeight));
            EditPrefab("Assets/Prefabs/Animals/BossDeer.prefab", go => ConfigureDeer(go, LeaderHeight));
            foreach (var path in new[] { "Rat", "Rat-Grey", "Rat-Large" })
                EditPrefab("Assets/Art/SmallGame/" + path + ".prefab", ConfigureRat);
            EditPrefab("Assets/Art/SmallGame/Crow.prefab", ConfigureCrow);
            foreach (var player in Object.FindObjectsByType<FirstPersonMotor>(FindObjectsSortMode.None)) ConfigurePlayer(player.gameObject);
            foreach (var hero in Object.FindObjectsByType<HeroWander>(FindObjectsSortMode.None)) ConfigureHero(hero.gameObject);
            foreach (var npc in Object.FindObjectsByType<SemenychDialogue>(FindObjectsSortMode.None)) ConfigureSemenych(npc);
            foreach (var deer in Object.FindObjectsByType<DeerAI>(FindObjectsSortMode.None))
                ConfigureDeer(deer.gameObject, deer.GetComponent<DeerLeader>() ? LeaderHeight : DeerHeight);
            foreach (var rat in Object.FindObjectsByType<RatAI>(FindObjectsSortMode.None)) ConfigureRat(rat.gameObject);
            foreach (var crow in Object.FindObjectsByType<CrowAI>(FindObjectsSortMode.None)) ConfigureCrow(crow.gameObject);
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveOpenScenes()) throw new InvalidOperationException("Cannot save resized scene");
            AssetDatabase.SaveAssets();
            FarmMapSetup.Bake();
            Debug.Log(FarmMapSetup.ValidateRoutes());
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveOpenScenes()) throw new InvalidOperationException("Cannot save navigation");
            AssetDatabase.SaveAssets();
            Debug.Log("WORLD_SCALE_APPLIED: player 1.80m, Hero 1.85m, Semenych 1.75m; deer 1.90/2.20m including antlers; rats .120/.168m; crow length .45m");
        }

        static void EditPrefab(string path, Action<GameObject> configure)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { configure(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void ConfigurePlayer(GameObject root)
        {
            var controller = root.GetComponent<CharacterController>();
            controller.height = PlayerHeight;
            controller.center = Vector3.up * (PlayerHeight * .5f);
            controller.radius = .3f;
            controller.stepOffset = .3f;
            var camera = root.GetComponentInChildren<Camera>();
            var position = camera.transform.localPosition;
            position.y = EyeHeight;
            camera.transform.localPosition = position;
            foreach (var body in root.GetComponentsInChildren<PlayerCharacterVisual>()) FitVisual(body.transform, PlayerHeight, root.transform.position.y);
            // Keep the legacy first-person body/shadow at the same height too.
            foreach (var body in root.GetComponentsInChildren<PlayerBodyAnimation>()) FitVisual(body.transform, PlayerHeight, root.transform.position.y);
            Record(root);
        }

        public static void ConfigureHero(GameObject root)
        {
            var agent = root.GetComponent<NavMeshAgent>();
            var capsule = root.GetComponent<CapsuleCollider>();
            capsule.height = HeroHeight; capsule.center = Vector3.up * (HeroHeight * .5f); capsule.radius = .34f;
            agent.height = HeroHeight; agent.radius = .36f;
            FitVisual(root.GetComponentInChildren<Animator>().transform, HeroHeight, root.transform.position.y + agent.baseOffset);
            Record(root);
        }

        public static void ConfigureSemenych(SemenychDialogue npc)
        {
            var capsule = npc.GetComponent<CapsuleCollider>();
            capsule.height = SemenychHeight; capsule.center = Vector3.up * (SemenychHeight * .5f); capsule.radius = .3f;
            FitVisual(npc.GetComponentInChildren<Animator>().transform, SemenychHeight, npc.transform.position.y);
            Record(npc.gameObject);
        }

        public static void ConfigureDeer(GameObject root, float height)
        {
            // Root scale also scales the skeleton, sleep pose and both physics colliders.
            var bounds = Measure(root.transform);
            root.transform.localScale *= height / bounds.size.y;
            var agent = root.GetComponent<NavMeshAgent>();
            agent.height = height;
            agent.radius = root.GetComponent<CapsuleCollider>().radius * root.transform.lossyScale.x;
            Record(root);
        }

        public static void ConfigureRat(GameObject root)
        {
            var rat = root.GetComponent<RatAI>();
            float height = rat.isAggressive ? LargeRatHeight : RatHeight;
            FitVisual(rat.visual, height, root.transform.position.y);
            var agent = root.GetComponent<NavMeshAgent>();
            agent.height = height; agent.radius = rat.isAggressive ? .1f : .075f;
            // Author the hit box now; RatAI's fallback otherwise uses padded animation bounds.
            var box = rat.visual.GetComponent<BoxCollider>();
            if (!box) box = rat.visual.gameObject.AddComponent<BoxCollider>();
            var bounds = Measure(rat.visual, rat.visual);
            box.center = bounds.center;
            box.size = bounds.size;
            Record(root);
        }

        public static void ConfigureCrow(GameObject root)
        {
            // Measure in root axes so scene yaw does not change the chosen body length.
            var bounds = Measure(root.transform, root.transform);
            root.transform.localScale *= CrowLength / (bounds.size.z * root.transform.lossyScale.z);
            Record(root);
        }

        static void FitVisual(Transform visual, float height, float floor)
        {
            var animator = visual.GetComponentInChildren<Animator>();
            Bounds bounds;
            if (animator && animator.runtimeAnimatorController)
            {
                // Measure a repeatable pose; never save sampled bone transforms into the scene.
                var transforms = visual.GetComponentsInChildren<Transform>(true);
                var positions = transforms.Select(t => t.localPosition).ToArray();
                var rotations = transforms.Select(t => t.localRotation).ToArray();
                var scales = transforms.Select(t => t.localScale).ToArray();
                var hints = transforms.Select(t => new SerializedObject(t).FindProperty("m_LocalEulerAnglesHint").vector3Value).ToArray();
                AnimationMode.StartAnimationMode();
                try
                {
                    var clips = animator.runtimeAnimatorController.animationClips;
                    var clip = clips.FirstOrDefault(c => c.name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0) ?? clips.First();
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(animator.gameObject, clip, 0);
                    AnimationMode.EndSampling();
                    bounds = Measure(visual);
                }
                finally
                {
                    AnimationMode.StopAnimationMode();
                    for (int i = 0; i < transforms.Length; i++)
                    {
                        transforms[i].localPosition = positions[i]; transforms[i].localRotation = rotations[i]; transforms[i].localScale = scales[i];
                        var serialized = new SerializedObject(transforms[i]);
                        serialized.FindProperty("m_LocalEulerAnglesHint").vector3Value = hints[i];
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }
            else bounds = Measure(visual);
            float factor = height / bounds.size.y;
            float bottom = visual.position.y + (bounds.min.y - visual.position.y) * factor;
            visual.localScale *= factor;
            visual.position += Vector3.up * (floor - bottom);
            EditorUtility.SetDirty(visual);
            if (PrefabUtility.IsPartOfPrefabInstance(visual)) PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
        }

        public static Bounds Measure(Transform root, Transform relativeTo = null)
        {
            var bounds = new Bounds();
            bool found = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                Mesh mesh = null; bool temporary = false;
                if (renderer is SkinnedMeshRenderer skin) { mesh = new Mesh(); skin.BakeMesh(mesh, true); temporary = true; }
                else mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (!mesh) continue;
                try
                {
                    // Built-in primitive meshes and baked skins are readable in the editor.
                    foreach (var v in mesh.vertices)
                    {
                        var p = renderer.transform.TransformPoint(v);
                        if (relativeTo) p = relativeTo.InverseTransformPoint(p);
                        if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                        else bounds.Encapsulate(p);
                    }
                }
                finally { if (temporary) Object.DestroyImmediate(mesh); }
            }
            if (!found || bounds.size.y <= 0) throw new InvalidOperationException("Cannot measure " + root.name);
            return bounds;
        }

        static void Record(GameObject root)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (!(component == root.transform || component is Collider || component is NavMeshAgent)) continue;
                EditorUtility.SetDirty(component);
                if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }
    }
}
