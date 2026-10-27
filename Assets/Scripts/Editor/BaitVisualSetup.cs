using System;
using HuntHappens.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace HuntHappens.Editor
{
    public static class BaitVisualSetup
    {
        public static void Configure(PlayerInventory inventory)
        {
            var data=new SerializedObject(inventory);
            string[] fields={"breadVisualPrefab","cheeseVisualPrefab","latteVisualPrefab"};
            string[] paths={"MeshyBread/BreadLoaf","MeshyCheese/Cheese","MeshyLatte/Latte"};
            for(int i=0;i<fields.Length;i++)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/SmallGame/"+paths[i]+".prefab");
                if(!prefab)throw new InvalidOperationException("Missing bait prefab: "+paths[i]);
                AuthorGrip(AssetDatabase.GetAssetPath(prefab));
                data.FindProperty(fields[i]).objectReferenceValue=prefab;
            }
            data.ApplyModifiedProperties();EditorUtility.SetDirty(inventory);
        }
        public static void AuthorGrip(string path)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                Bounds bounds=WorldScaleSetup.Measure(root.transform,root.transform);
                Vector3 desired=bounds.center+Vector3.right*bounds.extents.x,contact=desired;
                float best=float.PositiveInfinity;
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
                    foreach(var v in filter.sharedMesh.vertices)
                    {
                        Vector3 point=root.transform.InverseTransformPoint(filter.transform.TransformPoint(v));
                        float distance=(point-desired).sqrMagnitude;
                        if(distance<best){best=distance;contact=point;}
                    }
                var grip=root.transform.Find("Right Hand Grip");
                if(!grip){grip=new GameObject("Right Hand Grip").transform;grip.SetParent(root.transform,false);}
                grip.localPosition=contact-Vector3.right*.008f;
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static string Apply()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit Mode required");
            foreach(var inventory in Object.FindObjectsByType<PlayerInventory>())Configure(inventory);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            if(!EditorSceneManager.SaveOpenScenes())throw new Exception("Scene save failed");AssetDatabase.SaveAssets();
            return "Assigned Meshy bread, cheese and latte to held/dropped inventory visuals";
        }
    }
}
