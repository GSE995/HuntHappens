using System;
using HuntHappens.SmallGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace HuntHappens.Editor
{
    public static class CageBaitVisualSetup
    {
        static void Configure(RatCage cage)
        {
            var anchor=cage.bait;anchor.name="Bread Bait";
            if(anchor.GetComponent<Renderer>())Object.DestroyImmediate(anchor.GetComponent<Renderer>());
            if(anchor.GetComponent<MeshFilter>())Object.DestroyImmediate(anchor.GetComponent<MeshFilter>());
            if(anchor.GetComponent<Collider>())Object.DestroyImmediate(anchor.GetComponent<Collider>());
            anchor.transform.localScale=Vector3.one;anchor.transform.localPosition=new Vector3(0,.055f,-.2f);
            var visual=cage.GetComponent<CageBaitVisual>();if(!visual)visual=cage.gameObject.AddComponent<CageBaitVisual>();
            var data=new SerializedObject(visual);data.FindProperty("anchor").objectReferenceValue=anchor.transform;
            string[] fields={"breadPrefab","cheesePrefab","lattePrefab"},paths={"MeshyBread/BreadLoaf","MeshyCheese/Cheese","MeshyLatte/Latte"};
            for(int i=0;i<fields.Length;i++){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/SmallGame/"+paths[i]+".prefab");if(!prefab)throw new Exception("Missing "+paths[i]);data.FindProperty(fields[i]).objectReferenceValue=prefab;}
            data.ApplyModifiedProperties();EditorUtility.SetDirty(cage);
        }
        public static string Apply()
        {
            if(Application.isPlaying)throw new Exception("Edit Mode required");
            const string path="Assets/Art/SmallGame/RatCage.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try{Configure(root.GetComponent<RatCage>());PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
            foreach(var cage in Object.FindObjectsByType<RatCage>())Configure(cage);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());if(!EditorSceneManager.SaveOpenScenes())throw new Exception("Save failed");AssetDatabase.SaveAssets();return "Meshy bait assigned to cage prefab and scene";
        }
    }
}
