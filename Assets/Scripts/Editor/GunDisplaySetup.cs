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
    public static class GunDisplaySetup
    {
        public static string Apply()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit Mode required");
            var stock=Object.FindObjectsByType<WorldShopItem>().Single(x=>x.Kind==4);
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Weapons/DartWeapon.prefab");
            if(!source)throw new Exception("DartWeapon prefab missing");
            Undo.RegisterFullObjectHierarchyUndo(stock.gameObject,"Tranquilizer shop display");
            var root=stock.transform;root.position=new Vector3(root.position.x,0,root.position.z);root.localScale=Vector3.one;
            if(stock.GetComponent<MeshRenderer>())Undo.DestroyObjectImmediate(stock.GetComponent<MeshRenderer>());
            if(stock.GetComponent<MeshFilter>())Undo.DestroyObjectImmediate(stock.GetComponent<MeshFilter>());
            var old=root.Find("Gun Display");if(old)Undo.DestroyObjectImmediate(old.gameObject);
            var display=new GameObject("Gun Display");display.transform.SetParent(root,false);
            var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/SmallGame/BreadDisplay/Wood.mat");
            var light=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/SmallGame/BreadDisplay/Wood Light.mat");
            void Board(string name,Vector3 p,Vector3 size,Material mat)
            {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(display.transform,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(go.GetComponent<Collider>());}
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Board("Leg",new Vector3(x*.32f,.275f,z*.3f),new Vector3(.065f,.55f,.065f),wood);
            Board("Gun Stand",new Vector3(0,.58f,0),new Vector3(.85f,.06f,.8f),light);
            Board("Price Sign",new Vector3(0,.45f,-.42f),new Vector3(.44f,.2f,.025f),wood);
            var trap=new GameObject("Display Tranquilizer");trap.transform.SetParent(display.transform,false);
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                bool visible=true;for(var node=filter.transform;node!=source.transform;node=node.parent)if(!node.gameObject.activeSelf){visible=false;break;}
                for(var node=filter.transform;node!=source.transform;node=node.parent)if(node.name=="Muzzle Flash"){visible=false;break;}
                var renderer=filter.GetComponent<MeshRenderer>();if(!visible || !renderer)continue;
                var part=new GameObject(filter.name,typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(trap.transform,false);
                part.transform.localPosition=source.transform.InverseTransformPoint(filter.transform.position);
                part.transform.localRotation=Quaternion.Inverse(source.transform.rotation)*filter.transform.rotation;
                part.transform.localScale=filter.transform.lossyScale;
                part.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;part.GetComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
            }
            trap.transform.localRotation=Quaternion.Euler(0,90,0);
            var bounds=WorldScaleSetup.Measure(trap.transform,display.transform);float scale=.75f/bounds.size.x;
            trap.transform.localScale=Vector3.one*scale;
            bounds=WorldScaleSetup.Measure(trap.transform,display.transform);
            trap.transform.localPosition=new Vector3(-bounds.center.x,.78f-bounds.min.y,-bounds.center.z);
            foreach(float x in new[]{-.22f,.22f})
            {
                float top=float.PositiveInfinity;
                foreach(var filter in trap.GetComponentsInChildren<MeshFilter>())
                    foreach(var vertex in filter.sharedMesh.vertices)
                    {
                        Vector3 point=display.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                        if(Mathf.Abs(point.x-x)<.04f)top=Mathf.Min(top,point.y);
                    }
                if(float.IsInfinity(top))throw new Exception("No gun surface above support");
                float height=Mathf.Max(.02f,top-.61f+.003f);
                Board("Gun Support",new Vector3(x,.61f+height*.5f,0),new Vector3(.055f,height,.25f),wood);
            }
            var label=stock.GetComponentInChildren<TMP_Text>();label.transform.localPosition=new Vector3(0,.45f,-.435f);label.fontSize=.27f;label.rectTransform.sizeDelta=new Vector2(.42f,.18f);label.text="ТРАНКВИЛИЗАТОР\n$"+stock.Price;
            var box=stock.GetComponent<BoxCollider>();box.center=new Vector3(0,.48f,0);box.size=new Vector3(.87f,.96f,.82f);
            var obstacle=stock.GetComponent<NavMeshObstacle>();obstacle.center=box.center;obstacle.size=box.size;
            PrefabUtility.SaveAsPrefabAsset(display,"Assets/Prefabs/GunShopDisplay.prefab");
            Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(stock.gameObject.scene);if(!EditorSceneManager.SaveOpenScenes())throw new Exception("Save failed");AssetDatabase.SaveAssets();return "Existing tranquilizer model displayed; configured price preserved";
        }
    }
}
