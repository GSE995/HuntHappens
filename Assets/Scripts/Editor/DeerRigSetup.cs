using System.Linq;
using HuntHappens.Animals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HuntHappens.Editor
{
    public static class DeerRigSetup
    {
        private const string Folder = "Assets/Art/Animals/Deer/";
        public static void IntegrateBatch()
        {
            RigPrefab("Assets/Prefabs/Animals/Deer.prefab", Folder);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            EditorSceneManager.SaveScene(scene);
        }

        public static void RigPrefab(string path, string outputFolder)
        {
            var deer = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual = deer.transform.Find("Visual");
                var imported = visual.Find("Imported Deer");
                if (visual.Find("Deer Skeleton") != null) throw new System.InvalidOperationException("Deer already rigged");
                var filters = imported.GetComponentsInChildren<MeshFilter>(true);
                var points = filters.SelectMany(f => f.sharedMesh.vertices.Select(v => visual.InverseTransformPoint(f.transform.TransformPoint(v)))).ToArray();
                var skeleton = Bone("Deer Skeleton", visual, Vector3.zero);
                var body = Bone("Body", skeleton, new Vector3(0, 1.15f, 0));
                var head = Bone("Head", body, new Vector3(0, 1.65f, 0.45f));
                var legs = new Transform[4];
                var centers = new Vector3[4];
                // Infer each hoof center from the lowest geometry, in the same diagonal order as the old rig.
                for (int i = 0; i < 4; i++)
                {
                    bool left = i % 2 == 0, front = i < 2;
                    var hoof = points.Where(p => p.y < 0.4f && (p.x < 0) == left && (p.z > 0) == front).ToArray();
                    if (hoof.Length == 0) throw new System.InvalidOperationException("Cannot locate hoof " + i);
                    centers[i] = new Vector3(hoof.Average(p => p.x), 0.94f, hoof.Average(p => p.z));
                    legs[i] = Bone("Leg " + i, body, centers[i]);
                    Debug.Log("Deer leg " + i + " hip=" + centers[i]);
                }
                var bones = new[] { body, head }.Concat(legs).ToArray();
                for (int part = 0; part < filters.Length; part++)
                {
                    var filter = filters[part];
                    var mesh = Object.Instantiate(filter.sharedMesh);
                    mesh.name = "Deer Rigged " + part;
                    Matrix4x4 toVisual = visual.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    var vertices = mesh.vertices;
                    var normals = mesh.normals;
                    var tangents = mesh.tangents;
                    var weights = new BoneWeight[vertices.Length];
                    int[] influenced = new int[6];
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        Vector3 p = vertices[v] = toVisual.MultiplyPoint3x4(vertices[v]);
                        if (normals.Length == vertices.Length) normals[v] = toVisual.inverse.transpose.MultiplyVector(normals[v]).normalized;
                        if (tangents.Length == vertices.Length)
                        {
                            Vector3 t = toVisual.MultiplyVector(new Vector3(tangents[v].x, tangents[v].y, tangents[v].z)).normalized;
                            tangents[v] = new Vector4(t.x, t.y, t.z, tangents[v].w);
                        }
                        int index = (p.z > 0 ? 0 : 2) + (p.x < 0 ? 0 : 1);
                        Vector2 distance = new Vector2(p.x - centers[index].x, p.z - centers[index].z);
                        float legWeight = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.72f, 1.12f, p.y));
                        if (p.y > 0.65f) legWeight *= 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.18f, 0.34f, distance.magnitude));
                        float headWeight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.35f, 1.85f, p.y));
                        int bone = headWeight > legWeight ? 1 : index + 2;
                        float weight = Mathf.Max(headWeight, legWeight);
                        weights[v] = new BoneWeight { boneIndex0 = bone, weight0 = weight, boneIndex1 = 0, weight1 = 1 - weight };
                        if (weight > 0.5f) influenced[bone]++;
                    }
                    mesh.vertices = vertices; mesh.normals = normals; mesh.tangents = tangents;
                    mesh.boneWeights = weights;
                    mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * visual.localToWorldMatrix).ToArray();
                    mesh.RecalculateBounds();
                    AssetDatabase.CreateAsset(mesh, outputFolder + "DeerRigged-" + part + ".asset");
                    var skinObject = new GameObject("Rigged Deer " + part);
                    skinObject.transform.SetParent(visual, false);
                    var skin = skinObject.AddComponent<SkinnedMeshRenderer>();
                    skin.sharedMesh = mesh;
                    skin.bones = bones;
                    skin.rootBone = body;
                    skin.sharedMaterials = filter.GetComponent<Renderer>().sharedMaterials;
                    var bounds = mesh.bounds; bounds.Expand(1.5f); skin.localBounds = bounds;
                    skin.updateWhenOffscreen = true;
                    Debug.Log("Rigged vertices by bone: " + string.Join(",", influenced));
                }
                imported.gameObject.SetActive(false);
                deer.GetComponent<DeerAnimation>().Configure(visual, head, legs);
                PrefabUtility.SaveAsPrefabAsset(deer, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(deer); }
            AssetDatabase.SaveAssets();
            Debug.Log("Deer skeleton installed: body, head and four weighted legs, original DeerAnimation reused.");
        }

        private static Transform Bone(string name, Transform parent, Vector3 visualPosition)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            Transform visual = parent;
            while (visual.name != "Visual") visual = visual.parent;
            bone.position = visual.TransformPoint(visualPosition);
            return bone;
        }
    }
}
