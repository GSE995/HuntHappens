using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace HuntHappens.Editor
{
    public static class FieldHUDSetup
    {
        public static void RunBatch()
        {
            AssetDatabase.Refresh();
            const string folder="Assets/Art/UI/FieldHUD/";
            var importer=(TextureImporter)AssetImporter.GetAtPath(folder+"Icons.png");
            importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(folder+"Caveat SDF.asset");
            if(!font)
            {
                font=TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(folder+"Caveat.ttf"),64,7,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic);
                font.name="Caveat SDF";
                AssetDatabase.CreateAsset(font,folder+"Caveat SDF.asset");
                AssetDatabase.AddObjectToAsset(font.material,font);
                foreach(var texture in font.atlasTextures)AssetDatabase.AddObjectToAsset(texture,font);
                string glyphs=new string(Enumerable.Range(32,95).Concat(Enumerable.Range(0x400,256)).Concat(new[]{0x2014,0x2013,0x00D7,0x00B7,0x2192}).Select(i=>(char)i).ToArray());
                font.TryAddCharacters(glyphs,out string missing);
                Debug.Log("HUD font missing optional glyphs: "+missing);
            }
            EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            var hud=Object.FindAnyObjectByType<HuntHappens.Interaction.InteractionHUD>();
            hud.SetStyle(font,AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"Icons.png"));EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            Debug.Log("Field HUD assets and font installed.");
        }
    }
}
