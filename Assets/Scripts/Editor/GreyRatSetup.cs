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
   RatPopulationSetup.Apply();
   EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("Rat population installed: 4 grey, 5 black and 1 large per litter spot.");
  }
 }
}
