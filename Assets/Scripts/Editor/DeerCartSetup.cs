using System;
using System.Collections.Generic;
using HuntHappens.Animals;
using HuntHappens.Interaction;
using HuntHappens.Transport;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    public static class DeerCartSetup
    {
        public const string PrefabPath = "Assets/Prefabs/Transport/DeerCart.prefab";
        [MenuItem("Tools/Hunt Happens/Configure Deer Cart")]
        public static void Apply()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().path != "Assets/Scenes/ForestPrototype.unity")
                throw new InvalidOperationException("Open ForestPrototype in Edit Mode first.");
            foreach (string name in new[] {"Deer", "BossDeer", "Deer-Legacy"})
            {
                string path = "Assets/Prefabs/Animals/" + name + ".prefab";
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    SetFloat(prefab.GetComponent<DeerPhysics>(), "sleepingMass", name == "BossDeer" ? 240 : 120);
                    prefab.GetComponent<Rigidbody>().mass = name == "BossDeer" ? 240 : 120;
                    PrefabUtility.SaveAsPrefabAsset(prefab,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            var holder = Object.FindAnyObjectByType<ItemHolder>();
            SetFloat(holder,"maximumMass",20);
            if (holder.GetComponent<CartDriver>() == null) Undo.AddComponent<CartDriver>(holder.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(holder);
            System.IO.Directory.CreateDirectory("Assets/Prefabs/Transport");
            System.IO.Directory.CreateDirectory("Assets/Materials/Transport");
            AssetDatabase.Refresh();
            var cartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (cartPrefab == null)
            {
                var root = Build();
                try { cartPrefab = PrefabUtility.SaveAsPrefabAsset(root,PrefabPath); }
                finally { Object.DestroyImmediate(root); }
            }
            if (Object.FindAnyObjectByType<DeerCart>() == null)
            {
                var cart = (GameObject)PrefabUtility.InstantiatePrefab(cartPrefab);
                Undo.RegisterCreatedObjectUndo(cart,"Add deer cart");
                Vector3 chosen = Vector3.zero; bool found = false;
                foreach (var preferred in new[] {new Vector3(4,0,-12),new Vector3(5,0,-5),new Vector3(-5,0,1),new Vector3(6,0,-20)})
                {
                    if (!Physics.Raycast(preferred+Vector3.up*3,Vector3.down,out var floor,5,~0,QueryTriggerInteraction.Ignore)) continue;
                    var blockers = Physics.OverlapBox(floor.point+Vector3.up*1.4f,new Vector3(1.65f,1.1f,3.2f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
                    if (Array.Exists(blockers,c=>!c.transform.IsChildOf(cart.transform))) continue;
                    chosen = floor.point + Vector3.up*.03f; found = true; break;
                }
                if (!found) { Object.DestroyImmediate(cart); throw new InvalidOperationException("No clear parking place found."); }
                cart.transform.position = chosen;
                PrefabUtility.RecordPrefabInstancePropertyModifications(cart.transform);
            }
            var dialogue = AssetDatabase.LoadAssetAtPath<HuntHappens.Characters.DialogueData>("Assets/Dialogues/FarmerIntroduction.asset");
            var serialized = new SerializedObject(dialogue);
            serialized.FindProperty("lines").GetArrayElementAtIndex(8).stringValue = "Поможешь вернуть всех домой? Обычных ищи на двух полянах, на дальней — ещё и вожак. Я выделил $100 на транквилизатор и дротики. Олень тяжёлый — на руках не унести! Бери тележку у фермы. Подкати её к спящему или пойманному оленю, отпусти ручку и нажми E на животном. Потом вези к входу в загон и остановись на площадке приёма.";
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(dialogue);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene())) throw new InvalidOperationException("Scene save failed.");
            AssetDatabase.SaveAssets();
        }

        private static void SetFloat(Object obj,string name,float value)
        { var so=new SerializedObject(obj); so.FindProperty(name).floatValue=value; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(obj); }
        private static Material Mat(string name,Color color)
        {
            string path="Assets/Materials/Transport/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};
            mat.SetColor("_BaseColor",color); mat.SetFloat("_Smoothness",.18f);
            AssetDatabase.CreateAsset(mat,path); return mat;
        }
        private static Transform Part(Transform root,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var part=GameObject.CreatePrimitive(type); part.name=name; part.transform.SetParent(root,false);
            part.transform.localPosition=position;part.transform.localScale=scale;
            part.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(part.GetComponent<Collider>());
            return part.transform;
        }
        private static GameObject Build()
        {
            var wood=Mat("Cart Oak",new Color(.57f,.34f,.14f));
            var rim=Mat("Cart Green",new Color(.16f,.30f,.24f));
            var steel=Mat("Cart Steel",new Color(.29f,.33f,.32f));
            var rubber=Mat("Cart Tyres",new Color(.055f,.065f,.06f));
            var root=new GameObject("Deer Cart");
            var body=root.AddComponent<Rigidbody>();body.mass=45;body.isKinematic=true;body.useGravity=false;
            var box=root.AddComponent<BoxCollider>();box.center=new Vector3(0,.85f,0);box.size=new Vector3(2.85f,1.15f,3.5f);
            var cart=root.AddComponent<DeerCart>();
            Part(root.transform,"Steel chassis",PrimitiveType.Cube,new Vector3(0,.5f,0),new Vector3(2.45f,.17f,3.4f),rim);
            for(int i=0;i<8;i++)
                Part(root.transform,"Oak bed plank "+i,PrimitiveType.Cube,new Vector3(0,.65f,-1.48f+i*.42f),new Vector3(2.43f,.14f,.39f),wood);
            foreach(int side in new[]{-1,1})
            {
                Part(root.transform,"Side rail",PrimitiveType.Cube,new Vector3(side*1.25f,1.03f,0),new Vector3(.10f,.36f,3.4f),wood);
                for(int i=0;i<3;i++) Part(root.transform,"Rail bracket",PrimitiveType.Cube,new Vector3(side*1.31f,.91f,-1.4f+i*1.4f),new Vector3(.08f,.7f,.1f),rim);
                Part(root.transform,"Handle arm",PrimitiveType.Cube,new Vector3(side*.7f,1.12f,-2.05f),new Vector3(.09f,.09f,1.1f),steel);
                Part(root.transform,"Rubber grip",PrimitiveType.Cylinder,new Vector3(side*.48f,1.12f,-2.46f),new Vector3(.13f,.25f,.13f),rubber).localRotation=Quaternion.Euler(0,0,90);
            }
            Part(root.transform,"Handle crossbar",PrimitiveType.Cube,new Vector3(0,1.12f,-2.46f),new Vector3(1.5f,.08f,.08f),steel);
            Part(root.transform,"Front board",PrimitiveType.Cube,new Vector3(0,.96f,1.66f),new Vector3(2.5f,.5f,.1f),rim);
            var wheels=new List<Transform>();
            foreach(int side in new[]{-1,1}) foreach(float z in new[]{-1.05f,1.05f})
            {
                var pivot=new GameObject("Wheel").transform;pivot.SetParent(root.transform,false);pivot.localPosition=new Vector3(side*1.36f,.44f,z);
                Part(pivot,"Tyre",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.88f,.12f,.88f),rubber).localRotation=Quaternion.Euler(0,0,90);
                Part(pivot,"Hub",PrimitiveType.Cylinder,new Vector3(side*.13f,0,0),new Vector3(.48f,.025f,.48f),steel).localRotation=Quaternion.Euler(0,0,90);
                for(int spoke=0;spoke<4;spoke++)
                    Part(pivot,"Spoke",PrimitiveType.Cube,new Vector3(side*.16f,0,0),new Vector3(.035f,.65f,.055f),rim).localRotation=Quaternion.Euler(spoke*45,0,0);
                wheels.Add(pivot);
            }
            cart.ConfigureWheels(wheels.ToArray());
            return root;
        }
    }
}
