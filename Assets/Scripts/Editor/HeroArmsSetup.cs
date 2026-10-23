using System.Linq;
using System.Collections.Generic;
using HuntHappens.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HuntHappens.Editor
{
    public static class HeroArmsSetup
    {
        public static void IntegrateBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            var poses = Object.FindAnyObjectByType<FirstPersonArms>();
            if (poses.GetComponentInChildren<HeroFirstPersonArms>() != null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Hero/Hero.prefab");
            var source = prefab.GetComponentInChildren<Animator>();
            var visual = Object.Instantiate(source.gameObject, poses.transform);
            visual.name = "Hero Arms";
            Object.DestroyImmediate(visual.GetComponent<Animator>());
            var skin = visual.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = Object.Instantiate(skin.sharedMesh);
            var weights = mesh.boneWeights;
            bool Arm(int bone) { string n = skin.bones[bone].name; return n.Contains("Arm") || n.Contains("Hand"); }
            var include = new bool[weights.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                var w = weights[i];
                include[i] = (Arm(w.boneIndex0) ? w.weight0 : 0) + (Arm(w.boneIndex1) ? w.weight1 : 0) +
                    (Arm(w.boneIndex2) ? w.weight2 : 0) + (Arm(w.boneIndex3) ? w.weight3 : 0) > 0.5f;
            }
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var all = mesh.GetTriangles(sub); var selected = new List<int>();
                for (int i = 0; i < all.Length; i += 3)
                    if (include[all[i]] && include[all[i+1]] && include[all[i+2]])
                    { selected.Add(all[i]); selected.Add(all[i+1]); selected.Add(all[i+2]); }
                mesh.SetTriangles(selected, sub);
            }
            mesh.name = "Hero First Person Arms";
            AssetDatabase.CreateAsset(mesh, "Assets/Art/Hero/FirstPersonArms.asset");
            skin.sharedMesh = mesh;
            skin.updateWhenOffscreen = true;
            Transform Bone(string name) => skin.bones.First(t => t.name == name || t.name.EndsWith(":" + name));
            var lu = Bone("LeftArm"); var ll = Bone("LeftForeArm"); var lw = Bone("LeftHand");
            var ru = Bone("RightArm"); var rl = Bone("RightForeArm"); var rw = Bone("RightHand");
            Vector3 midpoint = poses.transform.InverseTransformPoint((lu.position + ru.position) * 0.5f);
            visual.transform.localPosition += new Vector3(0, -0.42f, -0.12f) - midpoint;
            foreach (Transform child in visual.GetComponentsInChildren<Transform>()) child.gameObject.layer = 2;
            foreach (var renderer in poses.LeftHand.GetComponentsInChildren<Renderer>(true))
                if (poses.MiniatureTrap == null || !renderer.transform.IsChildOf(poses.MiniatureTrap)) Object.DestroyImmediate(renderer.gameObject);
            foreach (var renderer in poses.RightHand.GetComponentsInChildren<Renderer>(true)) Object.DestroyImmediate(renderer.gameObject);
            var leftSleeve = poses.transform.Find("Left Sleeve"); var rightSleeve = poses.transform.Find("Right Sleeve");
            if (leftSleeve != null) Object.DestroyImmediate(leftSleeve.gameObject);
            if (rightSleeve != null) Object.DestroyImmediate(rightSleeve.gameObject);
            poses.Configure(poses.View, poses.Input, poses.Motor, poses.Holder, poses.Gun, poses.Placement,
                poses.LeftHand, poses.RightHand, null, null, poses.MiniatureTrap);
            visual.AddComponent<HeroFirstPersonArms>().Configure(poses, lu, ll, lw, ru, rl, rw);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Hero arms installed with original mesh, texture and two-bone pose tracking.");
        }
    }
}
