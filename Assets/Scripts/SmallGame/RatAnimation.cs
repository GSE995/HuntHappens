using UnityEngine;
using UnityEngine.AI;
namespace HuntHappens.SmallGame
{
    [DisallowMultipleComponent]
    public sealed class RatAnimation : MonoBehaviour
    {
        public Transform[] legs;
        public Transform[] tail;
        private Vector3[] legPositions;
        private Quaternion[] legRotations, tailRotations;
        private NavMeshAgent agent;
        private RatAI rat;
        private float movement, phase;
        private void Awake()
        {
            agent=GetComponent<NavMeshAgent>();rat=GetComponent<RatAI>();
            legPositions=new Vector3[legs.Length];legRotations=new Quaternion[legs.Length];tailRotations=new Quaternion[tail.Length];
            for(int i=0;i<legs.Length;i++){legPositions[i]=legs[i].localPosition;legRotations[i]=legs[i].localRotation;}
            for(int i=0;i<tail.Length;i++)tailRotations[i]=tail[i].localRotation;
        }
        private void LateUpdate()
        {
            float speed=agent.enabled && agent.isOnNavMesh && !rat.Caught ? agent.velocity.magnitude : 0;
            movement=Mathf.Lerp(movement,Mathf.Clamp01(speed/0.5f),1-Mathf.Exp(-12*Time.deltaTime));
            phase+=Mathf.Min(speed*16,32)*Time.deltaTime;
            for(int i=0;i<legs.Length;i++)
            {
                float step=Mathf.Sin(phase+(i==0||i==3?0:Mathf.PI));
                legs[i].localRotation=legRotations[i]*Quaternion.Euler(step*18*movement,0,0);
                legs[i].localPosition=legPositions[i]+Vector3.up*(Mathf.Max(0,step)*0.012f*movement);
            }
            for(int i=0;i<tail.Length;i++)
                tail[i].localRotation=tailRotations[i]*Quaternion.Euler(Mathf.Sin(phase*0.45f-i*0.6f)*2*movement,Mathf.Sin(phase*0.5f-i*0.7f)*5*movement,0);
        }
    }
}
