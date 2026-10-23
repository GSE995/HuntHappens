using System;
using System.IO;
using System.Linq;
using HuntHappens.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HuntHappens.Editor
{
    [InitializeOnLoad]
    public static class PlayerCharacterChecks
    {
        const string Pending="HuntHappens.CamperChecks";
        static int stage, previousFrame;
        static float started;
        static double deadline;
        static PlayerCharacterVisual visual;
        static FirstPersonMotor motor;
        static CharacterController controller;
        static Camera view;
        static Vector3 bodyOrigin, handOrigin;
        static Quaternion cameraRotation;
        static Transform foot;
        static Quaternion footStart;
        static bool footMoved;
        static PlayerCharacterChecks()=>EditorApplication.playModeStateChanged+=OnPlay;
        public static void RunBatch()
        {
            SessionState.SetBool(Pending,true);EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");EditorApplication.EnterPlaymode();
        }
        static void OnPlay(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending,false))return;
            SessionState.SetBool(Pending,false);stage=0;previousFrame=-1;started=Time.time;deadline=EditorApplication.timeSinceStartup+90;footMoved=false;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup<deadline,"Play test timeout");
                if(previousFrame==Time.frameCount)return;previousFrame=Time.frameCount;
                float elapsed=Time.time-started;
                if(stage==0 && elapsed>.8f)
                {
                    motor=UnityEngine.Object.FindAnyObjectByType<FirstPersonMotor>();controller=motor.GetComponent<CharacterController>();
                    visual=motor.GetComponentInChildren<PlayerCharacterVisual>();view=motor.GetComponentInChildren<Camera>();
                    Check(visual!=null && visual.BodyAnimator.avatar.isValid,"Valid character visual and avatar");
                    Check(motor.GetComponentsInChildren<PlayerCharacterVisual>(true).Length==1,"Exactly one player body");
                    Check(visual.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length==1,"One intact character mesh; no experimental proxies");
                    Check(visual.GetComponentsInChildren<Collider>(true).Length==0 && visual.GetComponentsInChildren<Rigidbody>(true).Length==0,"No extra physics bodies");
                    Check(visual.transform.parent==motor.transform,"Body follows original player root");
                    Check(visual.BodyAnimator.applyRootMotion==false,"No animation-driven controller drift");
                    Check(UnityEngine.Object.FindObjectsByType<Camera>().Length==1,"Original FPS camera only");
                    Check(motor.GetComponentInChildren<FirstPersonArms>()!=null,"Existing gameplay hands preserved");
                    Check(Mathf.Abs(view.transform.localPosition.y-1.62f)<.001f,"Original camera height");
                    motor.enabled=false;bodyOrigin=visual.transform.localPosition;cameraRotation=view.transform.localRotation;
                    var arms=motor.GetComponentInChildren<FirstPersonArms>();handOrigin=arms.transform.localPosition;
                    foot=visual.GetComponentsInChildren<Transform>().First(t=>t.name=="LeftFoot");footStart=foot.localRotation;
                    CaptureExternal("Camper-Idle");
                    view.transform.localRotation=Quaternion.Euler(72,0,0);Capture(view,"Camper-FPS-LookDown");view.transform.localRotation=cameraRotation;
                    Capture(view,"Camper-FPS-Forward");
                    stage=1;started=Time.time;
                }
                else if(stage==1)
                {
                    controller.Move((motor.transform.forward*1.4f+Vector3.down)*Time.deltaTime);
                    footMoved|=Quaternion.Angle(footStart,foot.localRotation)>2;
                    if(elapsed>1.4f)
                    {
                        Check(visual.MovementBlend>.5f,"Movement selects walking animation");Check(footMoved,"Walking actually animates skeleton");
                        Check(Vector3.Distance(bodyOrigin,visual.transform.localPosition)<.001f,"Body root stays attached without root-motion drift");
                        CaptureExternal("Camper-Walking");stage=2;started=Time.time;
                    }
                }
                else if(stage==2)
                {
                    controller.Move(Vector3.down*Time.deltaTime);
                    if(elapsed>1)
                    {
                        Check(visual.MovementBlend<.05f,"Stopping returns to idle");
                        Check(view.transform.localRotation==cameraRotation,"Camera rotation preserved");
                        Check(motor.GetComponentInChildren<FirstPersonArms>().transform.localPosition==handOrigin,"Gameplay hands root preserved");
                        Check(visual.FullBody.shadowCastingMode==ShadowCastingMode.On,"Per-camera visibility restored");
                        File.WriteAllText("Logs/CamperPlayReport.txt","PASS: avatar, original camera/controller/hands, no extra physics, idle-walk-idle, animated foot, no root drift, per-camera visibility.\n");
                        Debug.Log("CAMPER_PLAY_CHECKS_PASSED");Finish(0);
                    }
                }
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        static void CaptureExternal(string name)
        {
            var go=new GameObject("Temporary Camper Check Camera");var cam=go.AddComponent<Camera>();cam.enabled=false;go.AddComponent<UniversalAdditionalCameraData>();
            cam.backgroundColor=new Color(.10f,.14f,.18f);cam.clearFlags=CameraClearFlags.SolidColor;cam.nearClipPlane=.02f;
            cam.transform.position=motor.transform.TransformPoint(new Vector3(2.3f,1.4f,3));cam.transform.LookAt(motor.transform.position+Vector3.up*.95f);cam.fieldOfView=38;
            Capture(cam,name);UnityEngine.Object.DestroyImmediate(go);
        }
        static void Capture(Camera cam,string name)
        {
            var target=new RenderTexture(1280,720,24);var previous=RenderTexture.active;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());}
            finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        static void Finish(int code){EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
