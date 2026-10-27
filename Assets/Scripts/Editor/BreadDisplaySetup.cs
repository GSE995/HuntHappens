using System;
using System.Collections.Generic;
using System.Linq;
using HuntHappens.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    public static class BreadDisplaySetup
    {
        const string Folder = "Assets/Art/SmallGame/BreadDisplay";
        static Material[] materials;
        static Material Material(string name, Color color)
        {
            string path = Folder + "/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!result) { result = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(result, path); }
            result.color = color; result.SetFloat("_Smoothness", .08f); EditorUtility.SetDirty(result); return result;
        }
        static Mesh MakeBread(string name, bool cut, bool slice)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>[7]; for (int i=0;i<7;i++) triangles[i]=new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c, int mat)
            { int n=vertices.Count; vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles[mat].AddRange(new[]{n,n+1,n+2}); }
            const int sides=14, rings=24;
            float front = slice ? -.011f : -.11f, back = slice ? .011f : .11f;
            Vector3 Point(int r,int j)
            {
                float t=r/(float)rings, angle=j*Mathf.PI*2/sides;
                float z=Mathf.Lerp(front,back,t);
                float taper=slice?1: Mathf.Lerp(.62f,1,Mathf.Sin(t*Mathf.PI)*.8f+.2f);
                if(cut && t<.3f) taper=1;
                float score=0;
                if(!slice && Mathf.Sin(angle)>.25f)
                    foreach(float mark in new[]{-.055f,.005f,.065f})
                        if(Mathf.Abs(z-mark-.025f*Mathf.Cos(angle))<.008f) score=.008f;
                float x=Mathf.Cos(angle)*(.06f*taper-score);
                float y=Mathf.Max(.004f,.043f+Mathf.Sin(angle)*(.054f*taper-score));
                return new Vector3(x,y,z);
            }
            for(int r=0;r<rings;r++) for(int j=0;j<sides;j++)
            {
                var a=Point(r,j);var b=Point(r,j+1);var c=Point(r+1,j+1);var d=Point(r+1,j);
                int mat=(r*7+j*3)%4;
                float z=(a.z+b.z+c.z+d.z)*.25f;
                if(!slice && (a.y+b.y+c.y+d.y)*.25f>.06f)
                    foreach(float mark in new[]{-.055f,.005f,.065f})
                        if(Mathf.Abs(z-mark-.025f*Mathf.Cos((j+.5f)*Mathf.PI*2/sides))<.009f) mat=4;
                Tri(a,b,c,mat);Tri(a,c,d,mat);
            }
            for(int end=0;end<2;end++)
            {
                int r=end==0?0:rings; var center=new Vector3(0,.043f,end==0?front:back);
                for(int j=0;j<sides;j++)
                {
                    var a=Point(r,j);var b=Point(r,j+1);
                    if((cut||slice) && (end==0||slice))
                    {
                        var ia=Vector3.Lerp(center,a,.88f);var ib=Vector3.Lerp(center,b,.88f);
                        if(end==0){Tri(center,ib,ia,5);Tri(ia,ib,b,0);Tri(ia,b,a,0);}
                        else {Tri(center,ia,ib,5);Tri(ia,b,ib,0);Tri(ia,a,b,0);}
                    }
                    else if(end==0) Tri(center,b,a,0); else Tri(center,a,b,0);
                }
            }
            if(cut||slice)
                for(int p=0;p<7;p++)
                {
                    float x=Mathf.Sin(p*12.3f)*.034f,y=.025f+(p%3)*.022f;
                    var center=new Vector3(x,y,front-.0004f);
                    for(int j=0;j<6;j++)
                    {
                        float a=j*Mathf.PI/3,b=(j+1)*Mathf.PI/3;
                        Tri(center,center+new Vector3(Mathf.Cos(b)*.0045f,Mathf.Sin(b)*.006f,0),center+new Vector3(Mathf.Cos(a)*.0045f,Mathf.Sin(a)*.006f,0),6);
                    }
                }
            var mesh=new Mesh {name=name};mesh.SetVertices(vertices);mesh.subMeshCount=7;
            for(int i=0;i<7;i++) mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+name+".asset");
            if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);return existing;}
            AssetDatabase.CreateAsset(mesh,Folder+"/"+name+".asset");return mesh;
        }
        static GameObject Bread(Transform parent,Mesh mesh,string name,Vector3 position,float yaw)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=materials;return go;
        }
        static void Board(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        public static string Apply()
        {
            if(Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Scenes/ForestPrototype.unity") throw new InvalidOperationException("Open ForestPrototype in Edit Mode");
            if(!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/SmallGame","BreadDisplay");
            materials=new[]{Material("Crust Honey",new Color(.69f,.34f,.09f)),Material("Crust Gold",new Color(.74f,.38f,.11f)),Material("Crust Light",new Color(.78f,.42f,.14f)),Material("Crust Toast",new Color(.65f,.30f,.07f)),Material("Scores",new Color(.95f,.7f,.34f)),Material("Crumb",new Color(.94f,.79f,.51f)),Material("Pores",new Color(.65f,.46f,.24f))};
            var wood=Material("Wood",new Color(.34f,.18f,.075f));var woodLight=Material("Wood Light",new Color(.46f,.27f,.12f));
            var whole=MakeBread("Whole Loaf",false,false);var cut=MakeBread("Cut Loaf",true,false);var slice=MakeBread("Bread Slice",true,true);
            var single=Bread(null,cut,"Bread Loaf",Vector3.zero,0);PrefabUtility.SaveAsPrefabAsset(single,Folder+"/BreadLoaf.prefab");Object.DestroyImmediate(single);
            var stock=Object.FindObjectsByType<WorldShopItem>().Single(x=>x.Kind==1);
            Undo.RegisterFullObjectHierarchyUndo(stock.gameObject,"Bread tray display");
            var root=stock.transform;root.localScale=Vector3.one;root.position=new Vector3(9.7f,0,-34);root.rotation=Quaternion.identity;
            if(stock.GetComponent<MeshRenderer>()) Undo.DestroyObjectImmediate(stock.GetComponent<MeshRenderer>());
            if(stock.GetComponent<MeshFilter>()) Undo.DestroyObjectImmediate(stock.GetComponent<MeshFilter>());
            var old=root.Find("Bread Tray Display");if(old) Undo.DestroyObjectImmediate(old.gameObject);
            var display=new GameObject("Bread Tray Display");display.transform.SetParent(root,false);
            // A simple timber stand keeps the actual 22 cm loaves visible at waist height.
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2) Board(display.transform,"Stand Leg",new Vector3(x*.27f,.32f,z*.20f),new Vector3(.055f,.64f,.055f),wood);
            Board(display.transform,"Tray Base",new Vector3(0,.66f,0),new Vector3(.72f,.05f,.58f),woodLight);
            foreach(int side in new[]{-1,1})
            {
                Board(display.transform,"Tray Side",new Vector3(side*.35f,.73f,0),new Vector3(.035f,.12f,.58f),wood);
                Board(display.transform,"Tray End",new Vector3(0,.71f,side*.275f),new Vector3(.72f,.08f,.035f),woodLight);
            }
            Bread(display.transform,whole,"Loaf 1",new Vector3(-.18f,.685f,.12f),-8);
            Bread(display.transform,whole,"Loaf 2",new Vector3(.18f,.685f,.12f),6);
            Bread(display.transform,whole,"Loaf 3",new Vector3(.18f,.685f,-.12f),-5);
            Bread(display.transform,cut,"Cut Loaf",new Vector3(-.18f,.685f,-.12f),-12);
            Bread(display.transform,slice,"Slice",new Vector3(0,.685f,-.18f),-25);
            var box=stock.GetComponent<BoxCollider>();box.center=new Vector3(0,.4f,0);box.size=new Vector3(.74f,.8f,.6f);
            var obstacle=stock.GetComponent<NavMeshObstacle>();obstacle.center=box.center;obstacle.size=box.size;
            Board(display.transform,"Price Sign",new Vector3(0,.58f,-.31f),new Vector3(.26f,.16f,.025f),wood);
            var label=stock.GetComponentInChildren<TMP_Text>();label.transform.localPosition=new Vector3(0,.58f,-.325f);label.fontSize=.32f;label.rectTransform.sizeDelta=new Vector2(.24f,.14f);label.text="ХЛЕБ\n$2";
            MeshyBreadSetup.ApplyTo(display.transform);
            PrefabUtility.SaveAsPrefabAsset(display,Folder+"/BreadTray.prefab");
            Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(stock.gameObject.scene);
            if(!EditorSceneManager.SaveOpenScenes())throw new Exception("Save failed");AssetDatabase.SaveAssets();
            return "Bread tray saved: four loaves and a slice; E purchase $2 retained";
        }
    }
}
