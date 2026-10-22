using System;
using System.Linq;
using HuntHappens.Interaction;
using HuntHappens.Player;
using HuntHappens.Traps;
using HuntHappens.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HuntHappens.Editor
{
    public static class FirstPersonArmsSetup
    {
        [MenuItem("Tools/Hunt Happens/Setup First Person Arms")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Exit Play Mode before setting up arms."); return; }
            Scene scene = SceneManager.GetActiveScene();
            var motor = InScene<FirstPersonMotor>(scene).FirstOrDefault();
            var view = motor != null ? motor.GetComponentInChildren<Camera>() : null;
            var gun = motor != null ? motor.GetComponent<TranquilizerGun>() : null;
            if (view == null || gun == null || gun.WeaponVisual == null || motor.GetComponent<ItemHolder>() == null || motor.GetComponent<TrapPlacement>() == null)
                throw new InvalidOperationException("Open ForestPrototype with FPS, tranquilizer, pickup and traps first.");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Arms")) AssetDatabase.CreateFolder("Assets/Materials", "Arms");
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup first person arms");
            Transform root = view.transform.Find("FirstPersonArms");
            if (root == null)
            {
                root = new GameObject("FirstPersonArms").transform;
                root.SetParent(view.transform, false);
                Undo.RegisterCreatedObjectUndo(root.gameObject, "Create first person arms");
            }
            Material skin = Material("Warm Skin", new Color(0.94f, 0.64f, 0.37f));
            Material jacket = Material("Hunter Green", new Color(0.20f, 0.36f, 0.13f));
            Material cuff = Material("Cuffs", new Color(0.36f, 0.22f, 0.09f));
            Material orange = Material("Trap Orange", new Color(1, 0.58f, 0.08f));
            Material rope = Material("Trap Rope", new Color(0.93f, 0.83f, 0.44f));
            Transform left = root.Find("Left Hand");
            Transform right = root.Find("Right Hand");
            if (left == null) left = CreateHand(root, -1, skin, cuff);
            if (right == null) right = CreateHand(root, 1, skin, cuff);
            foreach (Transform hand in new[] { left, right })
            {
                Transform trim = hand.Find("Jacket Cuff");
                if (trim != null && trim.localScale == new Vector3(0.16f, 0.135f, 0.09f))
                {
                    Undo.RecordObject(trim, "Refine default jacket cuffs");
                    trim.localPosition = new Vector3(0, 0, -0.055f);
                    trim.localScale = new Vector3(0.135f, 0.10f, 0.035f);
                }
            }
            Transform leftSleeve = root.Find("Left Sleeve");
            Transform rightSleeve = root.Find("Right Sleeve");
            if (leftSleeve == null) leftSleeve = Part("Left Sleeve", root, PrimitiveType.Capsule, new Vector3(-0.35f, -0.37f, 0.4f), new Vector3(0.135f, 0.2f, 0.135f), jacket);
            if (rightSleeve == null) rightSleeve = Part("Right Sleeve", root, PrimitiveType.Capsule, new Vector3(0.35f, -0.37f, 0.4f), new Vector3(0.135f, 0.2f, 0.135f), jacket);
            Transform miniature = left.Find("Miniature Trap");
            if (miniature == null)
            {
                miniature = new GameObject("Miniature Trap").transform;
                miniature.SetParent(left, false);
                miniature.localPosition = new Vector3(0, 0.075f, 0.07f);
                Part("Base", miniature, PrimitiveType.Cube, Vector3.zero, new Vector3(0.25f, 0.018f, 0.25f), jacket);
                for (int i = -2; i <= 2; i++)
                {
                    Part("Net X", miniature, PrimitiveType.Cube, new Vector3(i * 0.05f, 0.014f, 0), new Vector3(0.009f, 0.009f, 0.24f), rope);
                    Part("Net Z", miniature, PrimitiveType.Cube, new Vector3(0, 0.014f, i * 0.05f), new Vector3(0.24f, 0.009f, 0.009f), rope);
                }
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                        Part("Bright Corner", miniature, PrimitiveType.Sphere, new Vector3(x * 0.11f, 0.025f, z * 0.11f), Vector3.one * 0.035f, orange);
                miniature.gameObject.SetActive(false);
            }
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 2;
            var arms = root.GetComponent<FirstPersonArms>();
            if (arms == null) arms = Undo.AddComponent<FirstPersonArms>(root.gameObject);
            Undo.RecordObject(arms, "Connect arms presentation");
            arms.Configure(view, motor.GetComponent<PrototypeInput>(), motor.GetComponent<CharacterController>(), motor.GetComponent<ItemHolder>(),
                gun, motor.GetComponent<TrapPlacement>(), left, right, leftSleeve, rightSleeve, miniature);
            var animator = root.GetComponent<FirstPersonArmsAnimator>();
            if (animator == null) animator = Undo.AddComponent<FirstPersonArmsAnimator>(root.gameObject);
            // Refine only original defaults, preserving any Inspector custom poses.
            var animationSettings = new SerializedObject(animator);
            if (animationSettings.FindProperty("rightGrip").vector3Value == new Vector3(0.035f, -0.075f, -0.055f))
                animationSettings.FindProperty("rightGrip").vector3Value = new Vector3(0.16f, -0.035f, -0.19f);
            if (animationSettings.FindProperty("leftSupport").vector3Value == new Vector3(-0.065f, -0.065f, 0.25f))
                animationSettings.FindProperty("leftSupport").vector3Value = new Vector3(-0.19f, -0.07f, 0.27f);
            animationSettings.ApplyModifiedProperties();
            EditorUtility.SetDirty(arms);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            Debug.Log("First person arms ready. Save scene, Play. 1: gun grip, 2: empty hands, 3: small trap; carrying takes priority. Existing weapon and muzzle unchanged.");
        }
        private static T[] InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        private static Transform CreateHand(Transform parent, int side, Material skin, Material cuff)
        {
            Transform hand = new GameObject(side < 0 ? "Left Hand" : "Right Hand").transform;
            hand.SetParent(parent, false);
            hand.localPosition = new Vector3(side * 0.28f, -0.27f, 0.57f);
            Part("Palm", hand, PrimitiveType.Sphere, new Vector3(0, 0, 0.055f), new Vector3(0.155f, 0.105f, 0.19f), skin);
            Transform fingers = Part("Fingers", hand, PrimitiveType.Capsule, new Vector3(0, 0, 0.13f), new Vector3(0.14f, 0.055f, 0.075f), skin);
            fingers.localRotation = Quaternion.Euler(0, 0, 90);
            Transform thumb = Part("Thumb", hand, PrimitiveType.Capsule, new Vector3(-side * 0.085f, 0.01f, 0.06f), new Vector3(0.065f, 0.06f, 0.065f), skin);
            thumb.localRotation = Quaternion.Euler(55, 0, side * 25);
            Part("Jacket Cuff", hand, PrimitiveType.Sphere, new Vector3(0, 0, -0.055f), new Vector3(0.135f, 0.10f, 0.035f), cuff);
            return hand;
        }
        private static Transform Part(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return part.transform;
        }
        private static Material Material(string name, Color color)
        {
            string path = "Assets/Materials/Arms/" + name + ".mat";
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
            Transform[] original = InScene<Transform>(scene);
            int cameras = InScene<Camera>(scene).Length;
            var gun = InScene<TranquilizerGun>(scene).Single();
            Transform weapon = gun.WeaponVisual;
            Transform muzzle = weapon.Find("Muzzle");
            Vector3 muzzlePosition = muzzle.position;
            Vector3 weaponPosition = weapon.localPosition;
            Setup();
            int count = InScene<Transform>(scene).Length;
            Setup();
            var arms = InScene<FirstPersonArms>(scene).Single();
            if (InScene<FirstPersonArmsAnimator>(scene).Length != 1 || InScene<Camera>(scene).Length != cameras ||
                count != InScene<Transform>(scene).Length || original.Any(t => t == null) || gun.WeaponVisual != weapon ||
                muzzle.position != muzzlePosition || weapon.localPosition != weaponPosition || arms.GetComponentsInChildren<Collider>(true).Length != 0 ||
                arms.GetComponentsInChildren<Rigidbody>(true).Length != 0) throw new InvalidOperationException("Arms setup duplicated, changed weapon, removed objects or added physics");
            if (!EditorSceneManager.SaveScene(scene)) throw new System.IO.IOException("Scene save failed");
            Debug.Log("Arms setup PASSED: one pair, no extra camera/weapon/physics, original transforms and muzzle preserved, idempotent, scene saved.");
        }
    }
}
