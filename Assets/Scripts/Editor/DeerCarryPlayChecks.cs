using System;
using System.Reflection;
using HuntHappens.Animals;
using HuntHappens.Interaction;
using HuntHappens.Player;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    public static class DeerCarryPlayChecks
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode");
            string report = "";
            void Check(bool condition, string message) { if (!condition) throw new Exception(message); report += "PASS " + message + "\n"; }
            var cursor = Cursor.lockState; bool cursorVisible = Cursor.visible;
            GameObject tester = null, cargo = null; DeerSedation deer = null, attacker = null;
            try
            {
                var zone = Array.Find(Object.FindObjectsByType<DeerCaptureZone>(), x => !x.hasLeader);
                tester = new GameObject("Carry Check Player");
                tester.transform.position = zone.transform.position + Vector3.back;
                var holder = tester.AddComponent<ItemHolder>(); holder.EnsureMaximumMass(100);
                var motor = tester.AddComponent<FirstPersonMotor>(); motor.enabled = false;
                var input = tester.GetComponent<PrototypeInput>(); input.enabled = false;
                var controller = tester.GetComponent<CharacterController>(); controller.height = 2; controller.center = Vector3.up;
                var health = tester.AddComponent<PlayerHealth>();
                var view = new GameObject("Carry Check View").transform; view.SetParent(tester.transform, false); view.localPosition = Vector3.up * 1.6f; holder.SetView(view);
                Set(input, "<SprintHeld>k__BackingField", false); float walk = motor.CurrentMoveSpeed;
                Set(input, "<SprintHeld>k__BackingField", true); float sprint = motor.CurrentMoveSpeed;
                Check(sprint > walk, "empty hands retain sprint");
                deer = Object.Instantiate(zone.ordinaryPrefab, zone.transform.position + Vector3.right * 4, Quaternion.identity);
                deer.GetComponent<DeerPhysics>().FallAsleep(); Set(deer, "currentState", DeerSedation.SedationState.Unconscious);
                var pickup = deer.GetComponent<PickupItem>();
                Check(holder.TryPickUp(pickup), "sleeping deer picked up with existing holder");
                float carry = motor.CurrentMoveSpeed;
                Check(Mathf.Abs(carry - walk * .55f) < .001f, "carrying speed is 55 percent of walk even with sprint held");
                Set(input, "<SprintHeld>k__BackingField", false);
                Check(Mathf.Approximately(carry, motor.CurrentMoveSpeed), "sprint key does not change carrying speed");
                int drops = 0; holder.Released += thrown => { if (!thrown) drops++; };
                attacker = Object.Instantiate(zone.leaderPrefab, zone.transform.position, Quaternion.identity);
                var leader = attacker.GetComponent<DeerLeader>(); leader.enabled = false;
                Set(leader, "player", health); Set(leader, "chargeDirection", Vector3.back);
                var impact = typeof(DeerLeader).GetMethod("CheckImpact", Flags);
                void Hit() { Physics.SyncTransforms(); impact.Invoke(leader, new object[] { leader.transform.position, leader.transform.position }); }
                Hit();
                Check(health.Current == 50 && !holder.IsHolding && pickup.Holder == null && pickup.Body.useGravity && drops == 1,
                    "leader hit deals 50 HP and drops deer through normal release event");
                Check(Mathf.Approximately(motor.CurrentMoveSpeed, walk) && motor.ExternalVelocity.y > 0, "walking restored immediately and knockback preserved");
                Hit(); Check(health.Current == 50 && drops == 1, "same attack cannot hit or drop twice");
                Check(holder.TryPickUp(pickup), "dropped deer can be picked up again");
                Set(health, "immuneUntil", Time.time + 10); Set(leader, "hit", false); Hit();
                Check(holder.IsHolding && drops == 1, "rejected damage does not drop cargo");
                Set(health, "immuneUntil", 0f); Set(health, "spawn", tester.transform.position); Set(leader, "hit", false); Hit();
                Check(health.Current == 100 && !holder.IsHolding && drops == 2 && pickup.Body.useGravity, "lethal hit also drops deer and preserves respawn");
                cargo = GameObject.CreatePrimitive(PrimitiveType.Cube); cargo.transform.position = zone.transform.position + Vector3.left * 4;
                cargo.AddComponent<Rigidbody>(); var item = cargo.AddComponent<PickupItem>();
                Check(holder.TryPickUp(item), "ordinary object pickup");
                Set(input, "<SprintHeld>k__BackingField", true);
                Check(Mathf.Approximately(motor.CurrentMoveSpeed, sprint), "non-deer cargo retains sprint");
                Set(health, "immuneUntil", 0f); Set(leader, "hit", false); Hit();
                Check(holder.HeldItem == item && health.Current == 50, "leader hit does not force-drop other objects");
                holder.Drop();
                // A sleeping leader is also deer cargo, not an exception to the movement penalty.
                attacker.GetComponent<DeerPhysics>().FallAsleep(); Set(attacker, "currentState", DeerSedation.SedationState.Unconscious);
                Check(holder.TryPickUp(attacker.GetComponent<PickupItem>()) && Mathf.Approximately(motor.CurrentMoveSpeed, carry), "carried leader has the same movement restriction");
                holder.Drop(); Check(Mathf.Approximately(motor.CurrentMoveSpeed, sprint), "dropping restores sprint immediately");
                System.IO.Directory.CreateDirectory("Logs"); System.IO.File.WriteAllText("Logs/DeerCarryChecks.txt", report + "PASS");
                return report;
            }
            finally
            {
                if (tester != null) { tester.GetComponent<ItemHolder>()?.Drop(); Object.DestroyImmediate(tester); }
                if (deer != null) Object.DestroyImmediate(deer.gameObject);
                if (attacker != null) Object.DestroyImmediate(attacker.gameObject);
                if (cargo != null) Object.DestroyImmediate(cargo);
                Cursor.lockState = cursor; Cursor.visible = cursorVisible;
            }
        }
    }
}
