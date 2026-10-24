using System.Collections.Generic;
using HuntHappens.Characters;
using HuntHappens.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HuntHappens.Editor
{
    public static class PlayerBodySetup
    {
        public static void IntegrateBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            Setup();
            var player = Object.FindAnyObjectByType<FirstPersonMotor>();
            var body = player.GetComponentInChildren<PlayerBodyAnimation>();
            body.transform.localPosition = new Vector3(0, 0, -0.2f);
            var camera = player.GetComponentInChildren<Camera>();
            camera.transform.localPosition = new Vector3(0, 2.04f, 0.45f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Hunt Happens/Setup Player Body")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var player = Object.FindAnyObjectByType<FirstPersonMotor>();
            if (player.GetComponentInChildren<PlayerBodyAnimation>() != null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Hero/Hero.prefab");
            var body = new GameObject("Player Body");
            Undo.RegisterCreatedObjectUndo(body, "Add player body");
            body.transform.SetParent(player.transform, false);
            body.transform.localPosition = new Vector3(0, 0, -0.2f);
            var source = prefab.GetComponentInChildren<Animator>();
            var visual = Object.Instantiate(source.gameObject, body.transform);
            visual.name = "Hero Visual";
            foreach (var child in visual.GetComponentsInChildren<Transform>()) child.gameObject.layer = 2;
            var skin = visual.GetComponentInChildren<SkinnedMeshRenderer>();
            var original = skin.sharedMesh;
            var firstPerson = Object.Instantiate(original);
            firstPerson.name = "Hero First Person Body";
            var weights = original.boneWeights;
            var hidden = new bool[weights.Length];
            bool HideBone(int index)
            {
                string name = skin.bones[index].name;
                return name.Contains("Head") || name.Contains("Neck") || name.Contains("Arm") || name.Contains("Hand") || name.Contains("Shoulder");
            }
            for (int i = 0; i < weights.Length; i++)
            {
                var w = weights[i];
                float influence = (HideBone(w.boneIndex0) ? w.weight0 : 0) + (HideBone(w.boneIndex1) ? w.weight1 : 0) +
                    (HideBone(w.boneIndex2) ? w.weight2 : 0) + (HideBone(w.boneIndex3) ? w.weight3 : 0);
                hidden[i] = influence > 0.2f;
            }
            for (int sub = 0; sub < original.subMeshCount; sub++)
            {
                var triangles = original.GetTriangles(sub);
                var visible = new List<int>();
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    if (hidden[triangles[i]] || hidden[triangles[i+1]] || hidden[triangles[i+2]]) continue;
                    visible.Add(triangles[i]); visible.Add(triangles[i+1]); visible.Add(triangles[i+2]);
                }
                firstPerson.SetTriangles(visible, sub);
            }
            AssetDatabase.CreateAsset(firstPerson, "Assets/Art/Hero/FirstPersonBody.asset");
            skin.sharedMesh = firstPerson;
            skin.shadowCastingMode = ShadowCastingMode.Off;
            skin.updateWhenOffscreen = true;
            var shadowObject = new GameObject("Full Body Shadow");
            shadowObject.transform.SetParent(skin.transform, false);
            var shadow = shadowObject.AddComponent<SkinnedMeshRenderer>();
            shadow.sharedMesh = original;
            shadow.sharedMaterials = skin.sharedMaterials;
            shadow.bones = skin.bones;
            shadow.rootBone = skin.rootBone;
            shadow.localBounds = skin.localBounds;
            shadow.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            shadow.updateWhenOffscreen = true;
            body.AddComponent<PlayerBodyAnimation>();
            body.AddComponent<HeroGrounding>();
            var controller = player.GetComponent<CharacterController>();
            Undo.RecordObject(controller, "Fit player capsule");
            controller.height = 2.25f;
            controller.center = Vector3.up * 1.125f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            var camera = player.GetComponentInChildren<Camera>();
            Undo.RecordObject(camera.transform, "Set eye height");
            camera.transform.localPosition = new Vector3(0, 2.04f, 0.45f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Debug.Log("Player body ready: FPS camera, animated hero body, full shadow, existing interaction arms preserved.");
        }
    }
}
