using System;
using System.Linq;
using HuntHappens.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;
namespace HuntHappens.Editor
{
    public static class DartDisplaySetup
    {
        public static string Apply()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit Mode required");
            var stock=Object.FindObjectsByType<WorldShopItem>().Single(x=>x.Kind==2);
            const string folder="Assets/Art/Weapons/Dart/";
            const string name="Meshy_AI_lowpoly_dart_1k_20261027_texture_remesh_tri";
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(folder+name+".fbx");
            if(!source)throw new Exception("Imported dart model missing");
            Undo.RegisterFullObjectHierarchyUndo(stock.gameObject,"Dart shop display");
            var root=stock.transform;root.position=new Vector3(root.position.x,0,root.position.z);root.localScale=Vector3.one;
            if(stock.GetComponent<MeshRenderer>())Undo.DestroyObjectImmediate(stock.GetComponent<MeshRenderer>());
            if(stock.GetComponent<MeshFilter>())Undo.DestroyObjectImmediate(stock.GetComponent<MeshFilter>());
            var old=root.Find("Dart Display");if(old)Undo.DestroyObjectImmediate(old.gameObject);
            var display=new GameObject("Dart Display");display.transform.SetParent(root,false);
            var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/SmallGame/BreadDisplay/Wood.mat");
            var light=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/SmallGame/BreadDisplay/Wood Light.mat");
            void Board(string name,Vector3 p,Vector3 size,Material mat)
            {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(display.transform,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(go.GetComponent<Collider>());}
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Board("Leg",new Vector3(x*.32f,.275f,z*.3f),new Vector3(.065f,.55f,.065f),wood);
            Board("Dart Case Base",new Vector3(0,.58f,0),new Vector3(.85f,.06f,.8f),light);
            Board("Price Sign",new Vector3(0,.45f,-.42f),new Vector3(.44f,.2f,.025f),wood);
            Board("Case Left",new Vector3(-.4f,.65f,0),new Vector3(.035f,.08f,.8f),wood);
            Board("Case Right",new Vector3(.4f,.65f,0),new Vector3(.035f,.08f,.8f),wood);
            Board("Case Back",new Vector3(0,.65f,.38f),new Vector3(.8f,.08f,.035f),wood);
            Board("Case Front",new Vector3(0,.65f,-.38f),new Vector3(.8f,.08f,.035f),wood);
            var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"Dart.mat");
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,folder+"Dart.mat");}
            var importer=(TextureImporter)AssetImporter.GetAtPath(folder+name+".png");
            if(importer.maxTextureSize!=1024){importer.maxTextureSize=1024;importer.SaveAndReimport();}
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+name+".png"));
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.15f);EditorUtility.SetDirty(material);
            var dart=new GameObject("Display Dart");
            var model=(GameObject)PrefabUtility.InstantiatePrefab(source);model.transform.SetParent(dart.transform,false);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=material;
            var bounds=WorldScaleSetup.Measure(dart.transform,dart.transform);
            Vector3 axis=bounds.size.x>bounds.size.y && bounds.size.x>bounds.size.z?Vector3.right:bounds.size.y>bounds.size.z?Vector3.up:Vector3.forward;
            model.transform.localRotation=Quaternion.FromToRotation(axis,Vector3.forward)*model.transform.localRotation;
            bounds=WorldScaleSetup.Measure(dart.transform,dart.transform);model.transform.localScale*=.26f/bounds.size.z;
            bounds=WorldScaleSetup.Measure(dart.transform,dart.transform);
            model.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            var prefab=PrefabUtility.SaveAsPrefabAsset(dart,folder+"DartVisual.prefab");Object.DestroyImmediate(dart);
            for(int i=0;i<4;i++)
            {
                var copy=(GameObject)PrefabUtility.InstantiatePrefab(prefab);copy.name="Display Dart "+(i+1);copy.transform.SetParent(display.transform,false);
                copy.transform.localPosition=new Vector3(-.27f+i*.18f,.615f,0);
            }
            var label=stock.GetComponentInChildren<TMP_Text>();label.transform.localPosition=new Vector3(0,.45f,-.435f);label.fontSize=.42f;label.rectTransform.sizeDelta=new Vector2(.42f,.18f);label.text="ДРОТИКИ\n$12";
            var box=stock.GetComponent<BoxCollider>();box.center=new Vector3(0,.34f,0);box.size=new Vector3(.87f,.68f,.82f);
            var obstacle=stock.GetComponent<NavMeshObstacle>();obstacle.center=box.center;obstacle.size=box.size;
            PrefabUtility.SaveAsPrefabAsset(display,"Assets/Prefabs/DartShopDisplay.prefab");
            Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(stock.gameObject.scene);if(!EditorSceneManager.SaveOpenScenes())throw new Exception("Save failed");AssetDatabase.SaveAssets();return "Imported dart display installed; product 2, $12 preserved";
        }
    }
}
