using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HuntHappens.Animals;
using HuntHappens.Delivery;
using HuntHappens.Interaction;
using HuntHappens.Player;
using HuntHappens.Shop;
using HuntHappens.SmallGame;
using HuntHappens.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    [InitializeOnLoad]
    public static class DeerQuestPlayChecks
    {
        const string Flag = "Hunt.DeerQuestChecks";
        static IEnumerator routine;
        static string report;
        static PlayerInventory inventory;
        static PlayerHealth player;
        static Camera camera;
        static ItemHolder holder;
        static DeliveryManager manager;
        static DeliveryZone reception;
        static DeerLeader leader;
        static DeerSedation boss;
        static float deadline;
        static DeerQuestPlayChecks()
        {
            ShopProgress.InventoryChecksRunning = SessionState.GetBool(Flag, false);
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Flag, false))
                { Application.runInBackground = true; routine = Run(); report = ""; EditorApplication.update += Tick; }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    EditorApplication.update -= Tick; routine = null;
                    if (SessionState.GetBool(Flag, false)) Application.runInBackground = SessionState.GetBool(Flag + ".Background", false);
                    SessionState.SetBool(Flag, false); ShopProgress.InventoryChecksRunning = false;
                }
            };
        }
        public static void Begin()
        {
            if (Application.isPlaying) throw new Exception("Stop Play Mode first");
            SessionState.SetBool(Flag + ".Background", Application.runInBackground);
            SessionState.SetBool(Flag, true); ShopProgress.InventoryChecksRunning = true; ShopProgress.Reset();
            EditorApplication.isPlaying = true;
        }
        static void Tick()
        {
            try { if (routine != null && !routine.MoveNext()) Finish("PASS"); }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        static void Finish(string result)
        {
            report += result; System.IO.File.WriteAllText("Logs/DeerQuestPlayChecks.txt", report);
            Debug.Log(report); routine = null; EditorApplication.update -= Tick;
        }
        static void Check(bool condition, string name)
        { if (!condition) throw new Exception(name); report += "PASS " + name + "\n"; }
        static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static void PlayerAt(Vector3 point)
        {
            var controller = player.GetComponent<CharacterController>(); controller.enabled = false;
            player.transform.position = point + Vector3.up * .1f; controller.enabled = true;
            player.GetComponent<FirstPersonMotor>().ApplyImpulse(Vector3.zero); Physics.SyncTransforms();
        }
        static void Aim(Vector3 target) { camera.transform.LookAt(target); Physics.SyncTransforms(); }
        static void Deadline(float seconds) => deadline = Time.time + seconds;
        static void Timeout(string operation) { if (Time.time > deadline) throw new Exception("Timeout: " + operation); }
        static void Deliver(DeerSedation deer)
        {
            if (inventory.HandSlot >= 0) inventory.SelectSlot(inventory.HandSlot);
            PlayerAt(deer.transform.position - Vector3.forward * 2);
            Check(holder.TryPickUp(deer.GetComponent<PickupItem>()), "carry " + deer.QuestId);
            PlayerAt(reception.transform.TransformPoint(reception.Volume.center));
            int before = manager.DeliveredCount;
            Check(manager.TryDeliver(deer, reception), "farm accepts " + deer.QuestId);
            Check(manager.DeliveredCount == before + 1 && !manager.TryDeliver(deer, reception), "delivery counts once");
        }
        static IEnumerator Run()
        {
            int frame = Time.frameCount; Deadline(15);
            while (Time.frameCount < frame + 5) { Timeout("frames advance"); yield return null; }
            Check(Time.frameCount > frame, "Play Mode advances frames");
            inventory = Object.FindAnyObjectByType<PlayerInventory>(); player = inventory.GetComponent<PlayerHealth>();
            camera = player.GetComponentInChildren<Camera>(); holder = player.GetComponent<ItemHolder>();
            manager = Object.FindAnyObjectByType<DeliveryManager>();
            reception = Object.FindObjectsByType<DeliveryZone>().First(z => z.AutomaticReception);
            leader = Object.FindAnyObjectByType<DeerLeader>(); boss = leader.GetComponent<DeerSedation>();
            var all = Object.FindObjectsByType<DeerSedation>().Where(x => !string.IsNullOrEmpty(x.QuestId)).ToArray();
            Check(all.Length == 7 && manager.Goal == 7 && manager.DeliveredCount == 0, "six ordinary deer and one leader");
            Check(all.All(x => x.GetComponent<NavMeshAgent>().isOnNavMesh), "all seven on NavMesh");
            Check(manager.Balance == 100, "one-time 100 advance");
            var hunt = inventory.GetComponent<RatHunting>(); var shop = hunt.shop;
            Set(shop, "worldTrade", true);
            Check(hunt.Buy(4), "buy tranquilizer with starting money"); inventory.SelectSlot(0);
            Check(hunt.Buy(2) && hunt.Buy(2) && hunt.Buy(2) && manager.Balance == 14, "buy three darts before first capture");
            Check(hunt.Buy(6) && manager.Balance == 4, "buy chocolate at Inspector price"); inventory.SelectSlot(1);
            Set(shop, "worldTrade", false);
            var deer = all.First(x => x.QuestId == "training:0");
            deer.GetComponent<DeerAI>().enabled = false;
            deer.GetComponent<NavMeshAgent>().ResetPath();
            PlayerAt(deer.transform.position - Vector3.forward * 5); Aim(deer.transform.position + Vector3.up * 1.2f);
            inventory.SelectSlot(0);
            var gun = inventory.GetComponent<TranquilizerGun>();
            Check(gun.TryFire(), "fire bought tranquilizer");
            Deadline(8); while (deer.State != DeerSedation.SedationState.Unconscious) { Timeout("physical dart capture"); yield return null; }
            Check(inventory.Darts == 2 && gun.LastDart.HasImpacted, "physical dart hits and consumes ammo");
            Deliver(deer);
            PlayerAt(leader.transform.position + Vector3.back * 6);
            Deadline(6); while (leader.State != DeerLeader.LeaderState.Warning) { Timeout("leader warning"); yield return null; }
            Check(leader.LowerHead, "warning lowers head");
            Deadline(5); while (leader.State != DeerLeader.LeaderState.Charge) { Timeout("leader charge"); yield return null; }
            Check(true, "warning precedes charge");
            Deadline(4); while (player.Current == 100) { Timeout("charge collision"); yield return null; }
            Check(player.Current == 50, "single hit removes exactly half maximum HP");
            Check(player.GetComponent<FirstPersonMotor>().ExternalVelocity.y > 0 && Vector3.ProjectOnPlane(player.GetComponent<FirstPersonMotor>().ExternalVelocity, Vector3.up).magnitude > 0, "horizontal and vertical knockback");
            float until = Time.time + 1; while (Time.time < until) yield return null;
            Check(player.Current == 50 && leader.State == DeerLeader.LeaderState.Recovery, "one hit per attack and recovery pause");
            PlayerAt(new Vector3(0, .1f, 0));
            Deadline(20); while (leader.State != DeerLeader.LeaderState.Calm) { Timeout("return to herd"); yield return null; }
            // Throw through the same inventory and ItemHolder impulse used by mouse input.
            PlayerAt(leader.transform.position + Vector3.back * 10); Aim(leader.transform.position + Vector3.back * 4);
            inventory.SelectSlot(1); var bait = inventory.ThrowChocolate();
            float throwAt = Time.fixedTime; while (Time.fixedTime <= throwAt) yield return null;
            Check(bait != null && inventory.Count(InventoryKind.Chocolate) == 0 && bait.GetComponent<Rigidbody>().linearVelocity.magnitude > 0, "throw consumes chocolate and applies impulse");
            PlayerAt(new Vector3(0, .1f, 0));
            Deadline(20); while (leader.State != DeerLeader.LeaderState.Eating) { Timeout("chocolate attraction"); yield return null; }
            Check(true, "leader reaches thrown chocolate and eats");
            float eatingStart = Time.time;
            var guarded = all.First(x => x.QuestId == "guarded:0"); guarded.ApplyDose(1);
            Check(leader.State == DeerLeader.LeaderState.Eating, "ordinary deer sedation does not interrupt meal");
            Deadline(5); while (guarded.State != DeerSedation.SedationState.Unconscious) { Timeout("guarded deer sleeps"); yield return null; }
            Deliver(guarded);
            Check(leader.State == DeerLeader.LeaderState.Eating, "capture possible during distraction");
            Deadline(10); while (bait != null && bait.isActiveAndEnabled) { Timeout("meal finishes"); yield return null; }
            Check(Time.time - eatingStart >= 7.5f, "meal lasts eight seconds and consumes one portion");
            PlayerAt(Vector3.zero);
            Deadline(15); while (leader.State != DeerLeader.LeaderState.Calm) { Timeout("guard resumes"); yield return null; }
            Check(true, "returns to guarding after meal");
            PlayerAt(leader.transform.position + Vector3.back * 8);
            var second = WorldInventoryItem.Spawn(inventory, new InventoryEntry { kind = InventoryKind.Chocolate }, leader.transform.position + leader.transform.right * 2 + Vector3.up * .2f, Quaternion.identity);
            Deadline(15); while (leader.State != DeerLeader.LeaderState.Eating) { Timeout("second meal"); yield return null; }
            boss.ApplyDose(1);
            Check(leader.State == DeerLeader.LeaderState.Warning && boss.State == DeerSedation.SedationState.Awake, "leader hit interrupts meal; one dart insufficient");
            boss.ApplyDose(1); Check(boss.State == DeerSedation.SedationState.Awake, "two darts insufficient");
            boss.ApplyDose(1); Check(boss.State == DeerSedation.SedationState.Sedated, "three darts sedate leader");
            Deadline(6); while (boss.State != DeerSedation.SedationState.Unconscious) { Timeout("leader sleeps"); yield return null; }
            Deliver(boss);
            foreach (var remaining in all.Where(x => x != null && !x.IsDelivered))
            {
                remaining.ApplyDose(1);
                Deadline(6); while (remaining.State != DeerSedation.SedationState.Unconscious) { Timeout("remaining animal"); yield return null; }
                Deliver(remaining);
            }
            Check(manager.QuestComplete && manager.DeliveredCount == 7, "quest complete at seven unique returns");
            Check(ShopProgress.Load().returnedDeerIds.Distinct().Count() == 7, "seven unique identities saved");
            until = Time.time + 4; while (Time.time < until) yield return null;
            Check(!Object.FindObjectsByType<DeerSedation>().Any(x => !x.IsDelivered), "no replacement deer after delivery");
            Check(ShopProgress.Key == ShopProgress.TestKey, "checks use isolated save, not player progress");
        }
    }
}
