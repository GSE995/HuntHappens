using System.Linq;
using HuntHappens.SmallGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace HuntHappens.Editor
{
 public static class TrashBagSetup
 {
  const string Folder="Assets/Art/Props/TrashBag/";
  public static void RunBatch()
  {
   AssetDatabase.Refresh();
   var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"TrashBag.mat");
   if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Folder+"TrashBag.mat");}
   material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"texture_0_base_color.png"));
   material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",0.15f);
   var normal=(TextureImporter)AssetImporter.GetAtPath(Folder+"texture_0_normal.png");normal.textureType=TextureImporterType.NormalMap;normal.SaveAndReimport();
   material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"texture_0_normal.png"));material.EnableKeyword("_NORMALMAP");
   var root=new GameObject("Trash Bag");
   root.AddComponent<RatGatheringSpot>();
   var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"TrashBag.fbx"));model.transform.SetParent(root.transform,false);
   var renderers=model.GetComponentsInChildren<Renderer>();if(renderers.Length==0)throw new System.Exception("Bag has no renderer");
   Bounds bounds=renderers[0].bounds;
   foreach(var renderer in renderers){bounds.Encapsulate(renderer.bounds);renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>material).ToArray();}
   float scale=0.7f/bounds.size.y;model.transform.localScale*=scale;
   model.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
   var box=root.AddComponent<BoxCollider>();box.center=Vector3.up*0.35f;box.size=bounds.size*scale;
   var obstacle=root.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=box.center;obstacle.size=box.size;obstacle.carving=true;
   var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"TrashBag.prefab");Object.DestroyImmediate(root);
   var scene=EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
   var ground=GameObject.Find("Environment/Ground").GetComponent<Collider>();
   var previous=GameObject.Find("Forest Litter");if(previous!=null)Object.DestroyImmediate(previous);
   var litter=new GameObject("Forest Litter");
   var rats=Object.FindObjectsByType<RatAI>(FindObjectsSortMode.None).OrderBy(r=>r.transform.position.x).ToArray();
   for(int i=0;i<3;i++)
   {
    bool placed=false;Vector3 origin=rats[i*2].transform.position;
    for(int attempt=0;attempt<24;attempt++)
    {
     Vector3 point=origin+Quaternion.Euler(0,attempt*45,0)*Vector3.forward*(1.2f+attempt/8);
     if(!ground.Raycast(new Ray(point+Vector3.up*4,Vector3.down),out var floor,8))continue;
     if(!NavMesh.SamplePosition(floor.point,out _,0.4f,NavMesh.AllAreas))continue;
     if(Physics.OverlapBox(floor.point+Vector3.up*0.4f,new Vector3(0.45f,0.35f,0.45f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Any(c=>c!=ground))continue;
     var bag=(GameObject)PrefabUtility.InstantiatePrefab(prefab);bag.transform.SetParent(litter.transform);bag.transform.position=floor.point;bag.transform.rotation=Quaternion.Euler(0,i*110,0);
     var actual=bag.GetComponentInChildren<Renderer>().bounds;
     if(Mathf.Abs(actual.min.y-floor.point.y)>0.01f)throw new System.Exception("Bag floating above ground");
     Physics.SyncTransforms();placed=true;break;
    }
    if(!placed)throw new System.Exception("Cannot place trash bag "+i);
   }
   EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("Trash bags installed: 3 instances, 0.7m height, ground contact and collisions verified.");
  }
  public static void PreviewBatch()
  {
   EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
   var bag=GameObject.Find("Forest Litter").transform.GetChild(0);
   var camera=new GameObject("Trash Preview").AddComponent<Camera>();camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
   camera.transform.position=bag.position+new Vector3(1.35f,1.05f,1.35f);camera.transform.LookAt(bag.position+Vector3.up*0.35f);camera.fieldOfView=40;
   int frames=0;var target=new RenderTexture(960,960,24);var image=new Texture2D(960,960,TextureFormat.RGB24,false);target.Create();
   EditorApplication.update+=Render;
   void Render()
   {
    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});if(++frames<25)return;
    var old=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,960,960),0,0);image.Apply();System.IO.File.WriteAllBytes("Logs/TrashBag-preview.png",image.EncodeToPNG());RenderTexture.active=old;
    EditorApplication.update-=Render;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);EditorApplication.Exit(0);
   }
  }
 }
}
