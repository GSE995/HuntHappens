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
    public static class TrapDisplaySetup
    {
        public static string Apply()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit Mode required");
            var stock=Object.FindObjectsByType<WorldShopItem>().Single(x=>x.Kind==5);
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/AnimalTrap.prefab");
            if(!source)throw new Exception("AnimalTrap prefab missing");
            Undo.RegisterFullObjectHierarchyUndo(stock.gameObject,"Trap shop display");
            var root=stock.transform;root.position=new Vector3(root.position.x,0,root.position.z);root.localScale=Vector3.one;
            if(stock.GetComponent<MeshRenderer>())Undo.DestroyObjectImmediate(stock.GetComponent<MeshRenderer>());
            if(stock.GetComponent<MeshFilter>())Undo.DestroyObjectImmediate(stock.GetComponent<MeshFilter>());
            var old=root.Find("Trap Display");if(old)Undo.DestroyObjectImmediate(old.gameObject);
            var display=new GameObject("Trap Display");display.transform.SetParent(root,false);
            var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/SmallGame/BreadDisplay/Wood.mat");
            var light=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/SmallGame/BreadDisplay/Wood Light.mat");
            void Board(string name,Vector3 p,Vector3 size,Material mat)
            {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(display.transform,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(go.GetComponent<Collider>());}
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Board("Leg",new Vector3(x*.32f,.275f,z*.3f),new Vector3(.065f,.55f,.065f),wood);
            Board("Trap Stand",new Vector3(0,.58f,0),new Vector3(.85f,.06f,.8f),light);
            Board("Price Sign",new Vector3(0,.45f,-.42f),new Vector3(.44f,.2f,.025f),wood);
            var trap=new GameObject("Display Net Trap");trap.transform.SetParent(display.transform,false);
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                bool visible=true;for(var node=filter.transform;node!=source.transform;node=node.parent)if(!node.gameObject.activeSelf){visible=false;break;}
                var renderer=filter.GetComponent<MeshRenderer>();if(!visible || !renderer)continue;
                var part=new GameObject(filter.name,typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(trap.transform,false);
                part.transform.localPosition=source.transform.InverseTransformPoint(filter.transform.position);
                part.transform.localRotation=Quaternion.Inverse(source.transform.rotation)*filter.transform.rotation;
                part.transform.localScale=filter.transform.lossyScale;
                part.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;part.GetComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
            }
            var bounds=WorldScaleSetup.Measure(trap.transform,trap.transform);float scale=.72f/Mathf.Max(bounds.size.x,bounds.size.z);
            trap.transform.localScale=Vector3.one*scale;
            trap.transform.localPosition=new Vector3(0,.61f,0)-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
            var label=stock.GetComponentInChildren<TMP_Text>();label.transform.localPosition=new Vector3(0,.45f,-.435f);label.fontSize=.42f;label.rectTransform.sizeDelta=new Vector2(.42f,.18f);label.text="ЛОВУШКИ\n$25";
            var box=stock.GetComponent<BoxCollider>();box.center=new Vector3(0,.34f,0);box.size=new Vector3(.87f,.68f,.82f);
            var obstacle=stock.GetComponent<NavMeshObstacle>();obstacle.center=box.center;obstacle.size=box.size;
            PrefabUtility.SaveAsPrefabAsset(display,"Assets/Prefabs/TrapShopDisplay.prefab");
            Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(stock.gameObject.scene);if(!EditorSceneManager.SaveOpenScenes())throw new Exception("Save failed");AssetDatabase.SaveAssets();return "Existing net trap displayed; shop kind 5, $25 preserved";
        }
    }
}
