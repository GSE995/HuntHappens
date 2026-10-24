using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuntHappens.Player
{
    [DefaultExecutionOrder(80)]
    public sealed class CamperFingerPose : MonoBehaviour
    {
        [SerializeField] private FirstPersonArms arms;
        [SerializeField] private FirstPersonArmsAnimator motion;
        [SerializeField] private bool left;
        [SerializeField] private Transform[] joints;
        [SerializeField] private Quaternion[] rest;
        [SerializeField] private Vector3[] axes;
        [SerializeField] private string[] fingerNames;
        private float curl;
        public int JointCount => joints == null ? 0 : joints.Length;
        public float Curl => curl;
        public void Configure(FirstPersonArms owner,FirstPersonArmsAnimator driver,bool isLeft,Transform model)
        {
            arms=owner;motion=driver;left=isLeft;
            var all=model.GetComponentsInChildren<Transform>(true);
            Transform wrist=Array.Find(all,t=>t.name=="Wrist");
            Transform up=Array.Find(all,t=>t.name=="Guide_Palm");
            Vector3 normal=(up.position-wrist.position).normalized;
            var bones=new List<Transform>();var rotations=new List<Quaternion>();var bendAxes=new List<Vector3>();var names=new List<string>();
            foreach(string name in new[]{"Thumb","Index","Middle","Ring","Little"})
                for(int j=1;j<=3;j++)
                {
                    Transform bone=Array.Find(all,t=>t.name==name+"_"+j.ToString("00"));
                    Transform tip=Array.Find(all,t=>t.name==(j==3?name+"_tip":name+"_"+(j+1).ToString("00")));
                    if(!bone || !tip)throw new InvalidOperationException("Missing finger joint: "+name);
                    Vector3 axis=Vector3.Cross((tip.position-bone.position).normalized,normal).normalized;
                    bones.Add(bone);rotations.Add(bone.localRotation);bendAxes.Add(bone.InverseTransformDirection(axis));names.Add(name);
                }
            joints=bones.ToArray();rest=rotations.ToArray();axes=bendAxes.ToArray();fingerNames=names.ToArray();
        }
        private void LateUpdate()
        {
            if(!arms || joints==null)return;
            float target=arms.Mode==FirstPersonArms.VisualMode.Carry ? .72f : arms.Mode==FirstPersonArms.VisualMode.Tranquilizer ? (left ? .52f : .85f) : arms.Mode==FirstPersonArms.VisualMode.Trap ? .42f : arms.Mode==FirstPersonArms.VisualMode.Cage ? .9f : .08f;
            if(motion){target=Mathf.Lerp(target,.02f,motion.ThrowPulse);if(left && arms.Mode!=FirstPersonArms.VisualMode.Cage)target=Mathf.Lerp(target,.08f,motion.PlacementPulse);}
            curl=Mathf.Lerp(curl,target,1-Mathf.Exp(-16*Time.deltaTime));
            for(int i=0;i<joints.Length;i++)
            {
                float factor=i%3==0 ? .65f : i%3==1 ? .85f : .55f;
                if(fingerNames[i]=="Thumb")factor*=.65f;
                if(!left && arms.Mode==FirstPersonArms.VisualMode.Tranquilizer && fingerNames[i]=="Index")factor*=.25f;
                joints[i].localRotation=rest[i]*Quaternion.AngleAxis(curl*factor*Mathf.Rad2Deg,axes[i]);
            }
        }
    }
}
