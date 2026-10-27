using System.Linq;
using HuntHappens.Animals;
using HuntHappens.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.Editor
{
    public static class BossDeerSetup
    {
        private const string Folder = "Assets/Art/Animals/BossDeer/";
        private const string Name = "Meshy_AI_leader_deer_gold_chai_1023223802_image-to-3d-texture";
        private const string PrefabPath = "Assets/Prefabs/Animals/BossDeer.prefab";
        public static void ResizeBatch()
        {
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                WorldScaleSetup.ConfigureDeer(prefab, WorldScaleSetup.LeaderHeight);
                var agent = prefab.GetComponent<NavMeshAgent>();
                agent.radius = prefab.GetComponent<CapsuleCollider>().radius * prefab.transform.lossyScale.x;
                agent.height = WorldScaleSetup.LeaderHeight;
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            var boss = GameObject.Find("Boss Deer");
            WorldScaleSetup.ConfigureDeer(boss, WorldScaleSetup.LeaderHeight);
            PrefabUtility.RecordPrefabInstancePropertyModifications(boss.transform);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Boss fitted to 2.20m; visual, skeleton and colliders scale together.");
        }
        public static void IntegrateBatch()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                if (!AssetDatabase.CopyAsset("Assets/Prefabs/Animals/Deer-Legacy.prefab", PrefabPath))
                    throw new System.InvalidOperationException("Cannot create boss prefab");
                var boss = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    boss.name = "Boss Deer";
                    var visual = boss.transform.Find("Visual");
                    foreach (Transform child in visual.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                    var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + Name + ".png"));
                    material.SetFloat("_Smoothness", 0.25f);
                    var normal = (TextureImporter)AssetImporter.GetAtPath(Folder + Name + "_normal.png");
                    normal.textureType = TextureImporterType.NormalMap;
                    normal.SaveAndReimport();
                    material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + Name + "_normal.png"));
                    material.EnableKeyword("_NORMALMAP");
                    AssetDatabase.CreateAsset(material, Folder + "BossDeer.mat");
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + Name + ".fbx"), visual);
                    model.name = "Imported Deer";
                    var renderers = model.GetComponentsInChildren<Renderer>();
                    Bounds bounds = renderers[0].bounds;
                    foreach (var r in renderers)
                    {
                        bounds.Encapsulate(r.bounds);
                        r.sharedMaterials = r.sharedMaterials.Select(_ => material).ToArray();
                    }
                    float scale = 2.8f / bounds.size.y;
                    model.transform.localScale *= scale;
                    model.transform.localPosition -= new Vector3(bounds.center.x - visual.position.x,
                        bounds.min.y - visual.position.y, bounds.center.z - visual.position.z) * scale;
                    foreach (var collider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                    PrefabUtility.SaveAsPrefabAsset(boss, PrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(boss); }
                DeerRigSetup.RigPrefab(PrefabPath, Folder);
            }
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            if (GameObject.Find("Boss Deer") == null)
            {
                if (!NavMesh.SamplePosition(new Vector3(10, 0, 16), out NavMeshHit hit, 8, NavMesh.AllAreas))
                    throw new System.InvalidOperationException("No navigation near boss spawn");
                var boss = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                boss.name = "Boss Deer";
                boss.transform.position = hit.position;
                boss.transform.rotation = Quaternion.Euler(0, 210, 0);
                boss.GetComponent<DeerPerception>().SetPlayer(Object.FindAnyObjectByType<FirstPersonMotor>().transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(boss.GetComponent<DeerPerception>());
            }
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Boss deer added with weighted skeleton and deer AI; existing deer retained.");
        }
    }
}
