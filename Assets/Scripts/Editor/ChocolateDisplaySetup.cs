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
    public static class ChocolateDisplaySetup
    {
        public static string Apply()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit Mode required");
            var stock=Object.FindObjectsByType<WorldShopItem>().Single(x=>x.Kind==6);
            Undo.RegisterFullObjectHierarchyUndo(stock.gameObject,"Chocolate display");
            stock.name="Chocolate Stock";var root=stock.transform;root.position=new Vector3(root.position.x,0,root.position.z);root.localScale=Vector3.one;
            if(stock.GetComponent<MeshRenderer>())Undo.DestroyObjectImmediate(stock.GetComponent<MeshRenderer>());
            if(stock.GetComponent<MeshFilter>())Undo.DestroyObjectImmediate(stock.GetComponent<MeshFilter>());
            var old=root.Find("Chocolate Display");if(old)Undo.DestroyObjectImmediate(old.gameObject);
            var display=new GameObject("Chocolate Display");display.transform.SetParent(root,false);
            var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/SmallGame/BreadDisplay/Wood.mat");
            var light=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/SmallGame/BreadDisplay/Wood Light.mat");
            void Board(string name,Vector3 p,Vector3 size,Material mat)
            {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(display.transform,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(go.GetComponent<Collider>());}
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Board("Leg",new Vector3(x*.23f,.32f,z*.18f),new Vector3(.05f,.64f,.05f),wood);
            Board("Chocolate Board",new Vector3(0,.655f,0),new Vector3(.62f,.05f,.48f),light);
            Board("Price Sign",new Vector3(0,.56f,-.255f),new Vector3(.26f,.16f,.025f),wood);
            Board("Tray Left",new Vector3(-.3f,.72f,0),new Vector3(.03f,.08f,.48f),wood);
            Board("Tray Right",new Vector3(.3f,.72f,0),new Vector3(.03f,.08f,.48f),wood);
            Board("Tray Back",new Vector3(0,.72f,.225f),new Vector3(.6f,.08f,.03f),wood);
            MeshyChocolateSetup.AddChocolate(display.transform);
            var chocolate=display.transform.Find("Chocolate Bar");chocolate.localPosition=new Vector3(-.18f,.68f,0);
            for(int i=1;i<3;i++){var copy=Object.Instantiate(chocolate.gameObject,display.transform);copy.name="Chocolate Bar "+(i+1);copy.transform.localPosition=new Vector3(-.18f+i*.18f,.68f,0);copy.transform.localRotation=Quaternion.Euler(0,(i-1)*12,0);}
            var label=stock.GetComponentInChildren<TMP_Text>();label.transform.localPosition=new Vector3(0,.56f,-.27f);label.fontSize=.27f;label.rectTransform.sizeDelta=new Vector2(.24f,.14f);label.text="ШОКОЛАД\n$"+stock.Price;
            var box=stock.GetComponent<BoxCollider>();box.center=new Vector3(0,.4f,0);box.size=new Vector3(.64f,.8f,.5f);
            var obstacle=stock.GetComponent<NavMeshObstacle>();obstacle.center=box.center;obstacle.size=box.size;
            PrefabUtility.SaveAsPrefabAsset(display,"Assets/Art/SmallGame/MeshyChocolate/ChocolateDisplay.prefab");
            Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(stock.gameObject.scene);if(!EditorSceneManager.SaveOpenScenes())throw new Exception("Save failed");AssetDatabase.SaveAssets();return "Chocolate display installed; product 6, configured price";
        }
    }
}
