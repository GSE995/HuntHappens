using System;
using System.Reflection;
using HuntHappens.Animals;
using HuntHappens.Player;
using UnityEditor;
using UnityEngine;

namespace HuntHappens.Editor
{
    public static class DeerStompChecks
    {
        public static string Run()
        {
            if (!Application.isPlaying || Time.deltaTime <= 0) throw new Exception("Requires advancing Play Mode");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            GameObject animal = null, target = null;
            void Check(bool result, string label) { if (!result) throw new Exception(label); }
            try
            {
                animal = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Animals/BossDeer.prefab"), new Vector3(0, 1000, 0), Quaternion.identity);
                var leader = animal.GetComponent<DeerLeader>(); leader.enabled = false;
                target = new GameObject("Isolated wave target");
                var cc = target.AddComponent<CharacterController>(); cc.center = Vector3.up; cc.height = 2;
                target.AddComponent<PrototypeInput>().enabled = false;
                var health = target.AddComponent<PlayerHealth>();
                target.transform.position = animal.transform.position + Vector3.forward * 3;
                leader.SetHome(animal.transform);
                typeof(DeerLeader).GetField("player", flags).SetValue(leader, health);
                void Call(string method) => typeof(DeerLeader).GetMethod(method, flags).Invoke(leader, null);
                void Enter(DeerLeader.LeaderState state) => typeof(DeerLeader).GetMethod("Enter", flags).Invoke(leader, new object[] { state });
                Enter(DeerLeader.LeaderState.Stomp);
                typeof(DeerLeader).GetField("elapsed", flags).SetValue(leader, leader.stompWindup * .65f);
                Check(leader.StompLift > .99f, "Forelegs must lift before impact");
                typeof(DeerLeader).GetField("elapsed", flags).SetValue(leader, leader.stompWindup);
                Check(leader.StompLift < .01f, "Hooves must land at release time");
                Physics.SyncTransforms();
                leader.waveSpeed = 3 / Time.deltaTime;
                Call("ReleaseWave"); Call("UpdateWave");
                Check(health.Current == 75, "Wave deals 25 damage");
                Call("UpdateWave");
                Check(health.Current == 75, "No repeat damage");
                Call("ClearWave");
                target.transform.position += Vector3.up * 2; Physics.SyncTransforms();
                Call("ReleaseWave"); Call("UpdateWave");
                Check(health.Current == 75, "Airborne player avoids ground wave");
                Call("ClearWave");
                target.transform.position -= Vector3.up * 2; Physics.SyncTransforms();
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    wall.transform.position = animal.transform.position + new Vector3(0, 1, 1.5f);
                    wall.transform.localScale = new Vector3(4, 4, .2f); Physics.SyncTransforms();
                    Call("ReleaseWave"); Call("UpdateWave");
                    Check(health.Current == 75, "Wall blocks wave damage");
                }
                finally { UnityEngine.Object.DestroyImmediate(wall); }
                Call("ClearWave");
                Call("ReleaseWave");
                target.transform.position += Vector3.forward * 100;
                Call("UpdateWave");
                Check(typeof(DeerLeader).GetField("wave", flags).GetValue(leader) == null, "Leaving territory clears wave");
                return "PASS: raised/landed hooves, 25 HP, one hit, airborne avoidance, wall occlusion, territory cancellation. Isolated objects; no save writes.";
            }
            finally
            {
                if (animal != null) UnityEngine.Object.DestroyImmediate(animal);
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
