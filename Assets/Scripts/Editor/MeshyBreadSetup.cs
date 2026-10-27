using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    public static class MeshyBreadSetup
    {
        const string Folder="Assets/Art/SmallGame/MeshyBread";
        public static void ApplyTo(Transform display)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit Mode required");
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/BreadLoaf.fbx");
            if(!source)throw new InvalidOperationException("Meshy BreadLoaf.fbx not imported");
            var textureImporter=(TextureImporter)AssetImporter.GetAtPath(Folder+"/BaseColor.png");
            if(textureImporter.maxTextureSize!=1024){textureImporter.maxTextureSize=1024;textureImporter.SaveAndReimport();}
            var normalImporter=(TextureImporter)AssetImporter.GetAtPath(Folder+"/Normal.png");
            if(normalImporter.textureType!=TextureImporterType.NormalMap || normalImporter.maxTextureSize!=1024)
            {normalImporter.textureType=TextureImporterType.NormalMap;normalImporter.maxTextureSize=1024;normalImporter.SaveAndReimport();}
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Bread.mat");
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,Folder+"/Bread.mat");}
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/BaseColor.png"));
            mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",.08f);
            mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Normal.png"));mat.EnableKeyword("_NORMALMAP");EditorUtility.SetDirty(mat);
            var root=new GameObject("Meshy Bread Loaf");
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(source);visual.transform.SetParent(root.transform,false);
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=mat;
            var bounds=WorldScaleSetup.Measure(root.transform,root.transform);
            visual.transform.localScale*=.22f/Mathf.Max(bounds.size.x,bounds.size.z);
            bounds=WorldScaleSetup.Measure(root.transform,root.transform);
            visual.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/BreadLoaf.prefab");Object.DestroyImmediate(root);
            BaitVisualSetup.AuthorGrip(Folder+"/BreadLoaf.prefab");
            foreach(string name in new[]{"Loaf 1","Loaf 2","Loaf 3","Cut Loaf"})
            {
                var old=display.Find(name);if(!old)throw new InvalidOperationException("Missing "+name);
                var position=old.localPosition;var rotation=old.localRotation;
                Object.DestroyImmediate(old.gameObject);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);instance.name=name;
                instance.transform.SetParent(display,false);instance.transform.SetLocalPositionAndRotation(position,rotation);
            }
        }
    }
}
