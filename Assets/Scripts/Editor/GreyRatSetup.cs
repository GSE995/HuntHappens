using System.Linq;
using HuntHappens.SmallGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace HuntHappens.Editor
{
 public static class GreyRatSetup
 {
  public static void RunBatch()
  {
   AssetDatabase.Refresh();
   const string path="Assets/Art/SmallGame/Rat-Grey.prefab";
   if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null)AssetDatabase.CopyAsset("Assets/Art/SmallGame/Rat.prefab",path);
   var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Animals/Mouse/GreyMouse.mat");
   if(material==null){material=new Material(Shader.Find("HuntHappens/GreyMouse"));AssetDatabase.CreateAsset(material,"Assets/Art/Animals/Mouse/GreyMouse.mat");}
   material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Animals/Mouse/Mouse.mat").GetTexture("_BaseMap"));
   var root=PrefabUtility.LoadPrefabContents(path);
   root.GetComponent<RatAI>().isGrey=true;
   foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())skin.sharedMaterial=material;
   PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
   var scene=EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
   var rats=Object.FindObjectsByType<RatAI>(FindObjectsSortMode.None).OrderBy(r=>r.transform.position.x).ThenBy(r=>r.transform.position.z).ToArray();
   for(int i=0;i<rats.Length;i++)
   {
    var old=rats[i];bool grey=i%4==0;
    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(grey?path:"Assets/Art/SmallGame/Rat.prefab");
    var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);instance.name=grey?"Grey Rat":"Black Rat";
    instance.transform.SetParent(old.transform.parent);instance.transform.SetPositionAndRotation(old.transform.position,old.transform.rotation);Object.DestroyImmediate(old.gameObject);
   }
   EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("Grey rats installed: 2 grey / 6 black; same rig and AI.");
  }
 }
}
