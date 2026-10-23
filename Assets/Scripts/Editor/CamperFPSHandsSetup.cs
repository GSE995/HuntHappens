using System;
using System.Linq;
using HuntHappens.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HuntHappens.Editor
{
    public static class CamperFPSHandsSetup
    {
        const string Folder="Assets/Characters/Camper/FPSHands";
        [MenuItem("Tools/Hunt Happens/Use Camper FPS Hands")]
        public static void SetupCurrentScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first");
            var arms=UnityEngine.Object.FindAnyObjectByType<FirstPersonArms>();
            if(!arms)throw new InvalidOperationException("Open ForestPrototype with existing FPS hands");
            var motion=arms.GetComponent<FirstPersonArmsAnimator>();
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Characters/Camper/Camper_URP.mat");
            foreach(bool left in new[]{true,false})
            {
                string side=left?"Left":"Right",path=Folder+"/CamperFPS_"+side+".fbx";
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=false;
                importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.optimizeGameObjects=false;importer.SaveAndReimport();
                Transform anchor=left?arms.LeftHand:arms.RightHand;
                var legacy=anchor.Find("Legacy Geometry");
                if(!legacy){legacy=new GameObject("Legacy Geometry").transform;legacy.SetParent(anchor,false);}
                foreach(Transform child in anchor.Cast<Transform>().ToArray())
                    if(child!=legacy && child.name!="Miniature Trap" && child.name!="Camper FPS "+side)child.SetParent(legacy,true);
                legacy.gameObject.SetActive(false);
                Transform old=anchor.Find("Camper FPS "+side);
                GameObject instance=old?old.gameObject:(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),anchor);
                instance.name="Camper FPS "+side;instance.transform.localPosition=Vector3.zero;instance.transform.localRotation=Quaternion.identity;instance.transform.localScale=Vector3.one;
                var bones=instance.GetComponentsInChildren<Transform>(true);
                Transform wrist=Array.Find(bones,t=>t.name=="Wrist"),forward=Array.Find(bones,t=>t.name=="Guide_Forward"),palm=Array.Find(bones,t=>t.name=="Guide_Palm");
                Vector3 localForward=instance.transform.InverseTransformDirection(forward.position-wrist.position);
                Vector3 localPalm=instance.transform.InverseTransformDirection(palm.position-wrist.position);
                instance.transform.localRotation=Quaternion.AngleAxis(-45,Vector3.right)*Quaternion.AngleAxis(180,Vector3.forward)*Quaternion.Inverse(Quaternion.LookRotation(localForward,localPalm));
                instance.transform.localScale=Vector3.one*.62f;
                Transform thumb=Array.Find(bones,t=>t.name=="Thumb_01");float thumbX=anchor.InverseTransformPoint(thumb.position).x;
                if((left && thumbX<0)||(!left && thumbX>0))throw new Exception("Unexpected dorsal hand orientation: check the source handedness and 180-degree roll");
                var animator=instance.GetComponent<Animator>();if(animator){animator.runtimeAnimatorController=null;animator.enabled=false;}
                foreach(var r in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {r.sharedMaterial=material;r.updateWhenOffscreen=true;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;}
                foreach(var t in instance.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
                var pose=instance.GetComponent<CamperFingerPose>();if(!pose)pose=instance.AddComponent<CamperFingerPose>();pose.Configure(arms,motion,left,instance.transform);EditorUtility.SetDirty(pose);
            }
            // Keep the short wrist cuffs at the bottom edge of the idle FPS view.
            var motionSettings=new SerializedObject(motion);
            foreach(string mode in new[]{"empty","carry","trap"})
            foreach(string field in new[]{"leftPosition","rightPosition"})
            {
                var property=motionSettings.FindProperty(mode).FindPropertyRelative(field);
                var position=property.vector3Value;position.y=-.40f;position.z=.57f;property.vector3Value=position;
            }
            motionSettings.FindProperty("keepWristsAtBottom").boolValue=true;
            motionSettings.FindProperty("wristViewportHeight").floatValue=.015f;
            motionSettings.ApplyModifiedPropertiesWithoutUndo();
            foreach(string name in new[]{"Left Sleeve","Right Sleeve"})
            {var sleeve=arms.transform.Find(name);if(sleeve)sleeve.gameObject.SetActive(false);}
            EditorSceneManager.MarkSceneDirty(arms.gameObject.scene);AssetDatabase.SaveAssets();
        }
        public static void IntegrateBatch()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            var arms=UnityEngine.Object.FindAnyObjectByType<FirstPersonArms>();
            var gun=arms.Gun.WeaponVisual;var muzzle=gun.Find("Muzzle");var position=muzzle.position;
            SetupCurrentScene();SetupCurrentScene();
            if(arms.GetComponentsInChildren<CamperFingerPose>().Length!=2 || arms.GetComponentsInChildren<CamperFingerPose>().Any(p=>p.JointCount!=15))throw new Exception("Expected two hands and 30 finger joints");
            if(position!=muzzle.position || arms.Gun.WeaponVisual!=gun)throw new Exception("Weapon was moved");
            if(arms.GetComponentsInChildren<Collider>(true).Length!=0 || arms.GetComponentsInChildren<Rigidbody>(true).Length!=0)throw new Exception("Unexpected physics in FPS hands");
            EditorSceneManager.SaveScene(scene);Debug.Log("CAMPER_FPS_HANDS_INTEGRATED: two character hands, 30 finger joints; existing weapon and interactions preserved");
        }
    }
}
