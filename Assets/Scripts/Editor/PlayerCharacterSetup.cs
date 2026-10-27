using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HuntHappens.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HuntHappens.Editor
{
    public static class PlayerCharacterSetup
    {
        const string Folder="Assets/Characters/Camper";
        const string Model=Folder+"/Models/Camper.fbx";
        const string Walking=Folder+"/Models/Walking.fbx";
        public static void IntegrateBatch()
        {
            BuildAssets();
            var contents=PrefabUtility.LoadPrefabContents("Assets/Prefabs/PrototypePlayer.prefab");
            try { Attach(contents);PrefabUtility.SaveAsPrefabAsset(contents,"Assets/Prefabs/PrototypePlayer.prefab"); }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            var player=UnityEngine.Object.FindAnyObjectByType<FirstPersonMotor>();
            if(!player)throw new Exception("FPS player missing");
            var camera=player.GetComponentInChildren<Camera>();var oldRotation=camera.transform.localRotation;
            Attach(player.gameObject);
            if(Mathf.Abs(camera.transform.localPosition.y-WorldScaleSetup.EyeHeight)>.001f || camera.transform.localRotation!=oldRotation)throw new Exception("Camera position unexpectedly changed");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("CAMPER_INTEGRATION_OK: player body added; realistic controller/eye height applied; FPS hands preserved");
        }
        [MenuItem("Tools/Hunt Happens/Configure Camper Player Body")]
        public static void Menu()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            IntegrateBatch();
        }
        public static void OpenGame()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.delayCall+=()=>EditorApplication.isPlaying=true;
        }
        static void BuildAssets()
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(Model);
            importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.optimizeGameObjects=false;importer.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            var avatar=model.GetComponent<Animator>().avatar;
            var walkImporter=(ModelImporter)AssetImporter.GetAtPath(Walking);
            walkImporter.animationType=ModelImporterAnimationType.Generic;walkImporter.avatarSetup=ModelImporterAvatarSetup.CopyFromOther;walkImporter.sourceAvatar=avatar;
            walkImporter.importAnimation=true;walkImporter.materialImportMode=ModelImporterMaterialImportMode.None;walkImporter.optimizeGameObjects=false;
            walkImporter.animationCompression=ModelImporterAnimationCompression.Off;
            var settings=walkImporter.defaultClipAnimations;
            foreach(var clip in settings)
            {
                clip.name="Camper_Walk";clip.loopTime=true;clip.loopPose=true;
                clip.lockRootPositionXZ=true;clip.lockRootHeightY=true;clip.lockRootRotation=true;
            }
            walkImporter.clipAnimations=settings;walkImporter.SaveAndReimport();
            var walk=AssetDatabase.LoadAllAssetsAtPath(Walking).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
            var albedo=Texture(Folder+"/Textures/BaseColor.png",true,false);
            var metallic=Texture(Folder+"/Textures/Metallic.png",false,true);
            var roughness=Texture(Folder+"/Textures/Roughness.png",false,true);
            var colors=metallic.GetPixels();var rough=roughness.GetPixels();
            if(colors.Length!=rough.Length)throw new Exception("Texture dimensions differ");
            for(int i=0;i<colors.Length;i++)colors[i]=new Color(colors[i].r,colors[i].r,colors[i].r,1-rough[i].r);
            var packed=new Texture2D(metallic.width,metallic.height,TextureFormat.RGBA32,false,true);packed.SetPixels(colors);packed.Apply();
            File.WriteAllBytes(Folder+"/Textures/MetallicSmoothness.png",packed.EncodeToPNG());UnityEngine.Object.DestroyImmediate(packed);
            AssetDatabase.ImportAsset(Folder+"/Textures/MetallicSmoothness.png");var map=Texture(Folder+"/Textures/MetallicSmoothness.png",false,false);
            Texture(Folder+"/Textures/Metallic.png",false,false);Texture(Folder+"/Textures/Roughness.png",false,false);
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Camper_URP.mat");
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,Folder+"/Camper_URP.mat");}
            mat.SetTexture("_BaseMap",albedo);mat.SetColor("_BaseColor",Color.white);mat.SetTexture("_MetallicGlossMap",map);
            mat.SetFloat("_Smoothness",1);mat.SetFloat("_SmoothnessTextureChannel",0);mat.EnableKeyword("_METALLICSPECGLOSSMAP");EditorUtility.SetDirty(mat);
            var visual=new GameObject("Camper Player Body");
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(model);instance.transform.SetParent(visual.transform,false);
            var animator=instance.GetComponent<Animator>();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            // Relax the supplied reference pose into an idle stance, keeping the original skeleton.
            foreach(string name in new[]{"LeftArm","RightArm"})
            {
                var arm=instance.GetComponentsInChildren<Transform>().First(t=>t.name==name);
                var fore=arm.GetChild(0);var current=fore.position-arm.position;
                var down=instance.transform.TransformDirection(new Vector3(name.StartsWith("Left")?-.10f:.10f,-1,.05f));
                arm.rotation=Quaternion.FromToRotation(current,down)*arm.rotation;
            }
            var idle=new AnimationClip();idle.name="Camper_Idle";idle.frameRate=30;
            foreach(var t in instance.GetComponentsInChildren<Transform>())
            {
                string path=AnimationUtility.CalculateTransformPath(t,instance.transform);
                Set(idle,path,"m_LocalPosition",t.localPosition);Set(idle,path,"m_LocalScale",t.localScale);
                Quaternion q=t.localRotation;
                foreach(var axis in new[]{"x","y","z","w"})
                {
                    float v=axis=="x"?q.x:axis=="y"?q.y:axis=="z"?q.z:q.w;
                    idle.SetCurve(path,typeof(Transform),"m_LocalRotation."+axis,AnimationCurve.Constant(0,2,v));
                }
            }
            idle.EnsureQuaternionContinuity();var idleSettings=AnimationUtility.GetAnimationClipSettings(idle);idleSettings.loopTime=true;AnimationUtility.SetAnimationClipSettings(idle,idleSettings);
            ReplaceAsset(idle,Folder+"/Camper_Idle.anim");
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"/Camper.controller");
            if(controller)AssetDatabase.DeleteAsset(Folder+"/Camper.controller");
            controller=AnimatorController.CreateAnimatorControllerAtPath(Folder+"/Camper.controller");controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
            var state=controller.CreateBlendTreeInController("Locomotion",out BlendTree tree);tree.blendType=BlendTreeType.Simple1D;tree.blendParameter="Speed";tree.useAutomaticThresholds=false;
            tree.AddChild(idle,0);tree.AddChild(walk,1);controller.layers[0].stateMachine.defaultState=state;animator.runtimeAnimatorController=controller;
            var body=instance.GetComponentInChildren<SkinnedMeshRenderer>();body.sharedMaterial=mat;body.updateWhenOffscreen=true;
            // Align the feet with the existing controller's ground plane; never move the camera.
            instance.transform.localPosition-=new Vector3(0,body.bounds.min.y,0);
            instance.transform.localPosition+=new Vector3(0,0,-.16f);
            visual.AddComponent<PlayerCharacterVisual>().Configure(null,null,animator,body);
            PrefabUtility.SaveAsPrefabAsset(visual,Folder+"/CamperPlayerVisual.prefab");UnityEngine.Object.DestroyImmediate(visual);
            Debug.Log("CAMPER_ASSETS_OK avatar="+avatar.isValid);
        }
        static void Attach(GameObject player)
        {
            var existing=player.GetComponentsInChildren<PlayerCharacterVisual>(true);
            var keep=existing.FirstOrDefault(v=>!PrefabUtility.IsAddedGameObjectOverride(v.gameObject)) ?? existing.FirstOrDefault();
            foreach(var extra in existing)if(extra!=keep)UnityEngine.Object.DestroyImmediate(extra.gameObject);
            Transform visual=keep ? keep.transform : ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/CamperPlayerVisual.prefab"),player.transform)).transform;
            var driver=visual.GetComponent<PlayerCharacterVisual>();var renderers=visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            WorldScaleSetup.ConfigurePlayer(player);
            driver.Configure(player.GetComponent<CharacterController>(),player.GetComponentInChildren<Camera>(),visual.GetComponentInChildren<Animator>(),renderers.Single());
            EditorUtility.SetDirty(driver);PrefabUtility.RecordPrefabInstancePropertyModifications(driver);
        }
        static Texture2D Texture(string path,bool srgb,bool readable)
        {
            var t=(TextureImporter)AssetImporter.GetAtPath(path);t.sRGBTexture=srgb;t.isReadable=readable;t.maxTextureSize=2048;t.alphaSource=TextureImporterAlphaSource.FromInput;t.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static void Set(AnimationClip clip,string path,string property,Vector3 value)
        {
            clip.SetCurve(path,typeof(Transform),property+".x",AnimationCurve.Constant(0,2,value.x));clip.SetCurve(path,typeof(Transform),property+".y",AnimationCurve.Constant(0,2,value.y));clip.SetCurve(path,typeof(Transform),property+".z",AnimationCurve.Constant(0,2,value.z));
        }
        static void ReplaceAsset(UnityEngine.Object value,string path){if(AssetDatabase.LoadMainAssetAtPath(path))AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(value,path);}
    }
}
