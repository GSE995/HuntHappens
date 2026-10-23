using System.Linq;
using HuntHappens.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.Editor
{
    public static class HeroSceneSetup
    {
        private const string Folder = "Assets/Art/Hero/";
        private const string Model = Folder + "Meshy_AI_camper_wide_rigged_biped_Animation_Walking_frame_rate_30.fbx";

        public static void IntegrateBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            AddHero();
            EditorSceneManager.SaveScene(scene);
        }

        public static void ResizeBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            var hero = Object.FindAnyObjectByType<HeroWander>();
            FitToGround(hero.gameObject, 2.25f);
            PrefabUtility.SaveAsPrefabAsset(hero.gameObject, Folder + "Hero.prefab");
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void FitToGround(GameObject hero, float height)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
            var visual = hero.GetComponentInChildren<Animator>().gameObject;
            // The imported walking model measures 1.823m at unit scale in Play Mode.
            // Import-time renderer bounds include the rig and are unsuitable for sizing.
            visual.transform.localScale = Vector3.one * (height / 1.823f);
            visual.transform.localPosition = Vector3.zero;
            var agent = hero.GetComponent<NavMeshAgent>();
            var ground = GameObject.Find("Environment/Ground").GetComponent<Collider>();
            if (!ground.Raycast(new Ray(hero.transform.position + Vector3.up * 5, Vector3.down), out RaycastHit hit, 10))
                throw new System.InvalidOperationException("No ground below Hero");
            agent.baseOffset = hit.point.y - hero.transform.position.y;
            agent.height = height;
            agent.radius = 0.5f;
            var capsule = hero.GetComponent<CapsuleCollider>();
            capsule.height = height;
            capsule.center = Vector3.up * height * 0.5f;
            capsule.radius = 0.44f;
            if (hero.GetComponent<HeroGrounding>() == null) hero.AddComponent<HeroGrounding>();
            Debug.Log("Hero resized to " + height + "m with animated sole grounding.");
        }

        [MenuItem("Tools/Hunt Happens/Add Hero")]
        public static void AddHero()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = true;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            var walk = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "Hero.controller");
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "Hero.controller");
            }
            if (controller.layers.Length == 0) controller.AddLayer("Base Layer");
            if (controller.layers[0].stateMachine.states.Length == 0) controller.AddMotion(walk);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            if (Object.FindAnyObjectByType<HeroWander>() != null) return;
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Hero.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Meshy_AI_camper_wide_rigged_biped_texture_0.png"));
                material.SetFloat("_Smoothness", 0.15f);
                AssetDatabase.CreateAsset(material, Folder + "Hero.mat");
            }
            if (!NavMesh.SamplePosition(new Vector3(5, 0, 8), out NavMeshHit hit, 8, NavMesh.AllAreas))
                throw new System.InvalidOperationException("Hero spawn requires forest navigation.");
            var hero = new GameObject("Hero");
            Undo.RegisterCreatedObjectUndo(hero, "Add Hero");
            hero.transform.position = hit.position;
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model), hero.transform);
            var renderers = visual.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
            }
            float scale = 1.8f / bounds.size.y;
            visual.transform.localScale *= scale;
            visual.transform.localPosition -= new Vector3(bounds.center.x - hero.transform.position.x, bounds.min.y - hero.transform.position.y, bounds.center.z - hero.transform.position.z) * scale;
            var animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            var agent = hero.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 1.8f;
            agent.speed = 1.6f;
            agent.acceleration = 10;
            agent.angularSpeed = 150;
            agent.stoppingDistance = 0.3f;
            var capsule = hero.AddComponent<CapsuleCollider>();
            capsule.center = Vector3.up * 0.9f;
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            hero.AddComponent<HeroWander>();
            FitToGround(hero, 2.25f);
            PrefabUtility.SaveAsPrefabAsset(hero, Folder + "Hero.prefab");
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(hero.scene);
            Debug.Log("Hero added with looping walk animation and forest wandering. Clip: " + walk.name + ", height: 2.25m");
        }
    }
}
