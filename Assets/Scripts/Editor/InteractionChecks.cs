using System;
using HuntHappens.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HuntHappens.Editor
{
    // Small Editor checks without a test package or changes to runtime assembly structure.
    public static class InteractionChecks
    {
        [MenuItem("Tools/Hunt Happens/Run Interaction Checks")]
        public static void RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run checks outside Play Mode.");
            Scene preview = EditorSceneManager.NewPreviewScene();
            TMP_FontAsset font = null;
            try
            {
                var player = new GameObject("Check Player");
                SceneManager.MoveGameObjectToScene(player, preview);
                var playerCollider = player.AddComponent<CapsuleCollider>();
                var holder = player.AddComponent<ItemHolder>();
                holder.SetView(player.transform);
                var otherPlayer = new GameObject("Other Check Player");
                SceneManager.MoveGameObjectToScene(otherPlayer, preview);
                var otherHolder = otherPlayer.AddComponent<ItemHolder>();
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(cube, preview);
                cube.transform.position = Vector3.forward * 2;
                var body = cube.AddComponent<Rigidbody>();
                var item = cube.AddComponent<PickupItem>();
                var collider = cube.GetComponent<Collider>();
                body.mass = 21;
                Check(!holder.TryPickUp(item), "Overweight item must be rejected");
                body.mass = 2;
                body.isKinematic = true;
                Check(!holder.TryPickUp(item), "Kinematic item must be rejected");
                body.isKinematic = false;
                collider.enabled = false;
                Check(!holder.TryPickUp(item), "Item without enabled solid collider must be rejected");
                collider.enabled = true;
                Physics.SyncTransforms();
                RigidbodyInterpolation originalInterpolation = body.interpolation;
                CollisionDetectionMode originalCollisionMode = body.collisionDetectionMode;
                Check(holder.TryPickUp(item), "Valid item must be accepted");
                Check(!body.isKinematic && body.detectCollisions && !body.useGravity,
                    "Held body remains dynamic with collisions enabled");
                Check(item.Holder == holder && holder.HeldItem == item, "Ownership must agree");
                Check(Physics.GetIgnoreCollision(collider, playerCollider), "Player collision must be ignored during carry");
                Check(!item.TryClaim(otherHolder, 20), "A held item cannot be claimed twice");
                holder.Drop();
                Check(!holder.IsHolding && item.Holder == null && body.useGravity, "Drop restores ownership and gravity");
                Check(body.interpolation == originalInterpolation && body.collisionDetectionMode == originalCollisionMode,
                    "Drop restores Rigidbody settings");
                Check(!Physics.GetIgnoreCollision(collider, playerCollider), "Separated player collision must be restored");
                holder.Throw(); // Empty hands must be safe.
                Check(holder.TryPickUp(item), "Released item can be picked up again");
                item.enabled = false;
                // Edit Mode does not dispatch normal runtime MonoBehaviour lifecycle messages.
                item.SendMessage("OnDisable");
                Check(!holder.IsHolding && body.useGravity, "Disabling a held item must release it");

                font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf"));
                Check(font != null, "HUD font must be available");
                Check(font.TryAddCharacters("E — ПоднятьОтпустить | ЛКМБросить", out string missing),
                    "HUD font must contain Cyrillic and em dash; missing: " + missing);
                Debug.Log("Interaction checks PASSED: mass, kinematic/collider rejection, ownership, collision exclusion/restoration, release, disable cleanup, Cyrillic font.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                if (font != null)
                {
                    foreach (Texture2D texture in font.atlasTextures) if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                    if (font.material != null) UnityEngine.Object.DestroyImmediate(font.material);
                    UnityEngine.Object.DestroyImmediate(font);
                }
            }
        }

        // Command-line entry point; never replaces an interactive editor's unsaved scene.
        public static void IntegrateAndCheckBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only");
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            int before = CountItems(scene);
            InteractionSceneBuilder.AddInteractionObjects();
            int after = CountItems(scene);
            InteractionSceneBuilder.AddInteractionObjects();
            Check(CountItems(scene) == after && after >= 6 && after >= before, "Integration must be idempotent");
            Check(UnityEngine.Object.FindObjectsByType<InteractionHUD>(FindObjectsInactive.Include).Length == 1,
                "Exactly one interaction HUD expected");
            Check(EditorSceneManager.SaveScene(scene), "Scene save must succeed");
            RunChecks();
            Debug.Log("ForestPrototype interaction integration PASSED; scene saved.");
        }

        public static void RunBatchIntegration()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only");
            if (AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf") == null)
            {
                string package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly).resolvedPath;
                AssetDatabase.importPackageCompleted += Imported;
                UnityEditor.AssetPackage.Package.Import(package + "/Package Resources/TMP Essential Resources.unitypackage", false);
                return;
            }
            ExecuteAndExit();
        }

        private static void Imported(string packageName)
        {
            AssetDatabase.importPackageCompleted -= Imported;
            EditorApplication.delayCall += ExecuteAndExit;
        }

        private static void ExecuteAndExit()
        {
            try { IntegrateAndCheckBatch(); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        private static int CountItems(Scene scene)
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects()) count += root.GetComponentsInChildren<PickupItem>(true).Length;
            return count;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Interaction check failed: " + message);
        }
    }
}
