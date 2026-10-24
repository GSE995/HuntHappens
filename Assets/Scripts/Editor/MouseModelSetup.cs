using HuntHappens.SmallGame;
using UnityEditor;
using UnityEngine;
namespace HuntHappens.Editor
{
    public static class MouseModelSetup
    {
        private const string Folder = "Assets/Art/Animals/Mouse/";
        private const string Name = "Meshy_AI_lowpoly_mouse_3d_1024131856_texture";
        public static void RunBatch()
        {
            AssetDatabase.Refresh();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Mouse.mat");
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, Folder + "Mouse.mat"); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + Name + ".png"));
            material.SetFloat("_Smoothness", 0.15f);
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(Folder + Name + "_normal.png");
            normalImporter.textureType = TextureImporterType.NormalMap; normalImporter.SaveAndReimport();
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + Name + "_normal.png")); material.EnableKeyword("_NORMALMAP");
            string path = "Assets/Art/SmallGame/Rat.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/SmallGame/Rat-Legacy.prefab") == null)
                AssetDatabase.CopyAsset(path, "Assets/Art/SmallGame/Rat-Legacy.prefab");
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var ai = root.GetComponent<RatAI>();
                if (ai.visual != null) Object.DestroyImmediate(ai.visual.gameObject);
                var visual = new GameObject("Mouse Visual").transform; visual.SetParent(root.transform, false); ai.visual = visual;
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + Name + ".fbx")); model.transform.SetParent(visual, false);
                var renderers = model.GetComponentsInChildren<Renderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) { bounds.Encapsulate(renderer.bounds); var mats = renderer.sharedMaterials; for (int i=0;i<mats.Length;i++) mats[i]=material; renderer.sharedMaterials=mats; }
                // Fit the full silhouette, including tail and ears, inside the existing cage.
                float scale = Mathf.Min(0.48f / bounds.size.x, 0.36f / bounds.size.y, 0.78f / bounds.size.z);
                model.transform.localScale *= scale;
                model.transform.localPosition -= new Vector3(bounds.center.x - visual.position.x, bounds.min.y - visual.position.y, bounds.center.z - visual.position.z) * scale;
                PrefabUtility.SaveAsPrefabAsset(root,path);
                Debug.Log("Mouse installed; original bounds=" + bounds + "; scale=" + scale);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
    }
}
