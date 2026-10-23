using System;
using System.IO;
using System.Linq;
using HuntHappens.Player;
using HuntHappens.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HuntHappens.Editor
{
    [InitializeOnLoad]
    public static class CamperFPSHandsChecks
    {
        const string Pending="HuntHappens.CamperFPSChecks";
        static int stage,frame;static float began;static double deadline;
        static FirstPersonArms arms;static CamperFingerPose left,right;static PickupItem crate;static Quaternion indexRest,middleRest;
        static CamperFPSHandsChecks()=>EditorApplication.playModeStateChanged+=OnPlay;
        public static void RunInteractive(){SessionState.SetBool("HuntHappens.CamperFPSInteractive",true);RunBatch();}
        public static void RunBatch(){SessionState.SetBool(Pending,true);EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");EditorApplication.EnterPlaymode();}
        static void OnPlay(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending,false))return;
            SessionState.SetBool(Pending,false);stage=0;frame=-1;began=Time.time;deadline=EditorApplication.timeSinceStartup+90;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup<deadline,"Timeout");if(frame==Time.frameCount)return;frame=Time.frameCount;float elapsed=Time.time-began;
                if(stage==0 && elapsed>.8f)
                {
                    arms=UnityEngine.Object.FindAnyObjectByType<FirstPersonArms>();left=arms.LeftHand.GetComponentInChildren<CamperFingerPose>();right=arms.RightHand.GetComponentInChildren<CamperFingerPose>();
                    Check(left&&right&&left.JointCount==15&&right.JointCount==15,"Two hands, five articulated fingers each");
                    Check(arms.LeftHand.InverseTransformPoint(Joint(left,"Thumb_01").position).x>0 && arms.RightHand.InverseTransformPoint(Joint(right,"Thumb_01").position).x<0,"Dorsal view: correct anatomical handedness with palms facing away");
                    Check(!arms.LeftHand.Find("Legacy Geometry").gameObject.activeSelf && !arms.RightHand.Find("Legacy Geometry").gameObject.activeSelf,"Old hands disabled");
                    foreach(var r in arms.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        Check(r.sharedMesh.uv.Length==r.sharedMesh.vertexCount,"UV atlas preserved");
                        foreach(string name in new[]{"Thumb","Index","Middle","Ring","Little"})for(int j=1;j<=3;j++)
                        {
                            int bi=Array.FindIndex(r.bones,b=>b.name==name+"_"+j.ToString("00"));Check(bi>=0,"Finger bone exists");
                            Check(r.sharedMesh.boneWeights.Any(w=>(w.boneIndex0==bi&&w.weight0>.001f)||(w.boneIndex1==bi&&w.weight1>.001f)||(w.boneIndex2==bi&&w.weight2>.001f)||(w.boneIndex3==bi&&w.weight3>.001f)),"Every phalanx deforms vertices");
                        }
                    }
                    arms.Gun.SelectTool(false);arms.Motor.GetComponent<FirstPersonMotor>().enabled=false;Next();
                }
                else if(stage==1 && elapsed>.35f)
                {
                    Check(left.Curl<.15f && right.Curl<.15f,"Relaxed open fingers");ValidateFraming();Capture("CamperHands-Idle");
                    indexRest=Joint(right,"Index_02").localRotation;middleRest=Joint(right,"Middle_02").localRotation;
                    arms.Gun.SelectTool(true);Next();
                }
                else if(stage==2 && elapsed>.4f)
                {
                    Check(right.Curl>.65f && left.Curl>.4f,"Gun grip curls fingers");
                    float ia=Quaternion.Angle(indexRest,Joint(right,"Index_02").localRotation),ma=Quaternion.Angle(middleRest,Joint(right,"Middle_02").localRotation);
                    Check(ma>15 && ia<ma*.6f,"Right index remains free for trigger");ValidateSkin();Capture("CamperHands-Weapon");
                    crate=UnityEngine.Object.FindObjectsByType<PickupItem>().First(p=>p.name=="Pickup Crate Small");crate.Body.position=arms.View.transform.position+arms.View.transform.forward*2;crate.Body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
                    Check(arms.Holder.TryPickUp(crate),"Original pickup");Next();
                }
                else if(stage==3 && elapsed>.4f)
                {
                    Check(left.Curl>.6f && right.Curl>.6f,"Carry grip");ValidateFraming();ValidateSkin();Capture("CamperHands-Carry");arms.Holder.Throw();Next();
                }
                else if(stage==4 && elapsed>.10f)
                {
                    Check(!arms.Holder.IsHolding && left.Curl<.60f,"Throw opens fingers and releases item");ValidateFraming();ValidateSkin();Next();
                }
                else if(stage==5 && elapsed>.5f)
                {
                    Check(right.Curl>.65f,"Weapon grip returns");Check(arms.GetComponentsInChildren<Collider>(true).Length==0,"No FPS colliders");
                    File.WriteAllText("Logs/CamperFPSReport.txt","PASS: 30 weighted finger joints, UVs, old hands disabled, idle/weapon/carry/throw, index trigger clearance, finite deformed skin.\n");Debug.Log("CAMPER_FPS_HANDS_CHECKS_PASSED");Finish(0);
                }
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        static Transform Joint(CamperFingerPose hand,string name)=>hand.GetComponentsInChildren<Transform>().Single(t=>t.name==name);
        static void ValidateFraming()
        {
            foreach(var hand in new[]{arms.LeftHand,arms.RightHand})
            {
                var screen=arms.View.WorldToViewportPoint(hand.position);
                Check(screen.z>arms.View.nearClipPlane && screen.y<=.016f,"Short hand wrist remains at bottom edge during idle, carry and throw");
            }
        }
        static void ValidateSkin()
        {
            foreach(var r in arms.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var m=new Mesh();r.BakeMesh(m);Check(m.bounds.size.magnitude<3,"Bounded deformation");
                Check(m.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"Finite skin vertices");UnityEngine.Object.DestroyImmediate(m);
            }
        }
        static void Capture(string name)
        {
            var target=new RenderTexture(1600,900,24);var prev=RenderTexture.active;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try{RenderPipeline.SubmitRenderRequest(arms.View,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());}
            finally{RenderTexture.active=prev;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
        }
        static void Next(){stage++;began=Time.time;}
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        static void Finish(int result){EditorApplication.update-=Tick;if(SessionState.GetBool("HuntHappens.CamperFPSInteractive",false)){SessionState.SetBool("HuntHappens.CamperFPSInteractive",false);File.WriteAllText("Logs/CamperFramingStatus.txt",result==0?"PASS: idle, carry, throw wrist framing; finger, weapon and pickup checks passed":"FAIL: see Unity Console");EditorApplication.isPlaying=false;}else EditorApplication.Exit(result);}
    }
}
