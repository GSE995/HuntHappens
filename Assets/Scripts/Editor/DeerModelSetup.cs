using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HuntHappens.Editor
{
    public static class DeerModelSetup
    {
        private const string Folder = "Assets/Art/Animals/Deer/";
        private const string Name = "Meshy_AI_smooth_cartoon_deer_3_1023215146_image-to-3d-texture";
        public static void IntegrateBatch()
        {
            const string path = "Assets/Prefabs/Animals/Deer.prefab";
            const string backup = "Assets/Prefabs/Animals/Deer-Legacy.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(backup) == null && !AssetDatabase.CopyAsset(path, backup))
                throw new System.InvalidOperationException("Could not preserve original deer prefab");
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual = prefab.transform.Find("Visual");
                var previous = visual.Find("Imported Deer");
                if (previous != null) Object.DestroyImmediate(previous.gameObject);
                foreach (Transform child in visual) child.gameObject.SetActive(false);
                var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Deer.mat");
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + Name + ".png"));
                    material.SetFloat("_Smoothness", 0.1f);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + Name + "_normal.png");
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                    material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + Name + "_normal.png"));
                    material.EnableKeyword("_NORMALMAP");
                    AssetDatabase.CreateAsset(material, Folder + "Deer.mat");
                }
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + Name + ".fbx"), visual);
                model.name = "Imported Deer";
                var renderers = model.GetComponentsInChildren<Renderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                }
                Debug.Log("New deer bounds before fitting: " + bounds + "; skinned renderers=" + model.GetComponentsInChildren<SkinnedMeshRenderer>().Length);
                float scale = 2.8f / bounds.size.y;
                model.transform.localScale *= scale;
                model.transform.localPosition -= new Vector3(bounds.center.x - visual.position.x,
                    bounds.min.y - visual.position.y, bounds.center.z - visual.position.z) * scale;
                foreach (var collider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            EditorSceneManager.SaveScene(scene);
            Debug.Log("New deer model installed. Original preserved in " + backup);
        }
    }
}
