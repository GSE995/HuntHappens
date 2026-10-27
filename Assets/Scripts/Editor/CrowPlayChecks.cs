using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HuntHappens.Interaction;
using HuntHappens.SmallGame;
using HuntHappens.Weapons;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    public static class CrowPlayChecks
    {
        public static string Result { get; internal set; } = "Not run";
        public static string Run()
        {
            if (!Application.isPlaying || Result == "Running") throw new InvalidOperationException("Requires Play Mode and no active check");
            Result = "Running";
            new GameObject("Crow checks").AddComponent<CrowCheckRunner>();
            return Result;
        }
    }
    public sealed class CrowCheckRunner : MonoBehaviour
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private float previousScale;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private Vector3 origin = new Vector3(1000, 0, 1000);
        private RatCage cage;
        private CrowAI crow;
        private void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        private T Track<T>(T component) where T : Component { created.Add(component.gameObject); return component; }
        private IEnumerator Start()
        {
            previousScale = Time.timeScale;
            var routine = Verify();
            try
            {
                while (true)
                {
                    object next;
                    try { if (!routine.MoveNext()) break; next = routine.Current; }
                    catch (Exception e) { CrowPlayChecks.Result = "FAILED: " + e; yield break; }
                    yield return next;
                }
                CrowPlayChecks.Result = "PASSED: bread/sandwich flights and return; all other inventory kinds rejected; two consumers and both rat/theft orderings; rat path cancelled; stale target; physical dart on perch/approach/return; no sleeping theft; 25s sleep, repeat dose, pause, falling and recovery.";
            }
            finally { Cleanup(); Destroy(gameObject); }
        }
        private IEnumerator Verify()
        {
            Time.timeScale = 1;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); created.Add(floor);
            floor.transform.position = origin + Vector3.down*.25f; floor.transform.localScale = new Vector3(200,.5f,200);
            cage = Track(Instantiate(AssetDatabase.LoadAssetAtPath<RatCage>("Assets/Art/SmallGame/RatCage.prefab"), origin + Vector3.forward*2, Quaternion.identity));
            crow = Track(Instantiate(AssetDatabase.LoadAssetAtPath<CrowAI>("Assets/Art/SmallGame/Crow.prefab"), origin + Vector3.up*4, Quaternion.identity));
            crow.attemptInterval = .1f;
            var feedingSpot = Track(new GameObject("Test crow feeding spot").AddComponent<RatGatheringSpot>());
            feedingSpot.transform.position = origin; feedingSpot.crowsAllowed = true;
            crow.feedingSpot = feedingSpot;
            yield return null;
            cage.RestoreCargo(0, cage, true, InventoryKind.Bread);
            cage.transform.position = origin + Vector3.right*8;
            Check(crow.CanTarget(cage), "8m boundary included");
            cage.transform.position = origin + Vector3.right*8.01f;
            Check(!crow.CanTarget(cage), "outside trash radius rejected despite detection range");
            yield return new WaitForSeconds(.3f);
            Check(crow.State == CrowAI.CrowState.Perched && cage.Baited, "outside bait untouched");
            cage.transform.position = origin + Vector3.forward*2;
            feedingSpot.crowsAllowed = false;
            yield return new WaitForSeconds(.3f);
            Check(!crow.CanTarget(cage) && crow.State == CrowAI.CrowState.Perched && cage.Baited, "safe trash rejects theft");
            crow.feedingSpot = null;
            Check(!crow.CanTarget(cage), "unassigned bird cannot steal");
            crow.feedingSpot = feedingSpot; feedingSpot.crowsAllowed = true;
            yield return new WaitForSeconds(.2f);
            Check(crow.Target == cage, "inside territory starts approach");
            cage.transform.position = origin + Vector3.right*8.01f;
            yield return null;
            Check(crow.Target == null && crow.State == CrowAI.CrowState.Returning && cage.Baited, "moving bait outside cancels flight");
            yield return new WaitForSeconds(4);
            Check(crow.State == CrowAI.CrowState.Perched && cage.Baited, "returns without outside theft");
            cage.transform.position = origin + Vector3.forward*2;
            foreach (InventoryKind kind in Enum.GetValues(typeof(InventoryKind)))
            {
                cage.RestoreCargo(0, cage, true, kind);
                // RestoreCargo intentionally maps non-baits to bread for legacy saves.
                if (!RatBait.IsBait(kind)) typeof(RatCage).GetField("<BaitKind>k__BackingField", Private).SetValue(cage, kind);
                bool edible = kind == InventoryKind.Bread || kind == InventoryKind.CheeseSandwich;
                Check(cage.TryStealBait() == edible, "bait filter " + kind);
                if (edible) Check(!cage.TryStealBait(), "double theft");
            }
            foreach (var kind in new[] { InventoryKind.Bread, InventoryKind.CheeseSandwich })
            {
                cage.RestoreCargo(0, cage, true, kind);
                float deadline = Time.time + 12;
                while (cage.Baited && Time.time < deadline) yield return null;
                Check(!cage.Baited && !cage.bait.activeSelf, "actual flight theft " + kind);
                while (crow.State != CrowAI.CrowState.Perched && Time.time < deadline) yield return null;
                Check(crow.State == CrowAI.CrowState.Perched, "returns to perch");
            }
            var rival = Track(Instantiate(AssetDatabase.LoadAssetAtPath<CrowAI>("Assets/Art/SmallGame/Crow.prefab"), origin + new Vector3(.3f,4,0), Quaternion.identity));
            rival.attemptInterval = .1f;
            rival.feedingSpot = feedingSpot;
            yield return null;
            typeof(CrowAI).GetField("nextAttempt", Private).SetValue(crow, Time.time);
            cage.RestoreCargo(0, cage, true, InventoryKind.Bread);
            yield return new WaitForSeconds(.2f);
            Check(crow.Target == cage && rival.Target == cage, "two birds approach the same bait");
            float raceDeadline = Time.time + 12;
            while (cage.Baited && Time.time < raceDeadline) yield return null;
            yield return null;
            Check(!cage.Baited && crow.Target == null && rival.Target == null && !cage.TryStealBait(), "both birds release consumed bait");
            while ((crow.State != CrowAI.CrowState.Perched || rival.State != CrowAI.CrowState.Perched) && Time.time < raceDeadline) yield return null;
            Check(crow.State == CrowAI.CrowState.Perched && rival.State == CrowAI.CrowState.Perched, "both competitors return");
            Object.DestroyImmediate(rival.gameObject);
            cage.RestoreCargo(0, cage, true, InventoryKind.Latte);
            yield return new WaitForSeconds(.3f);
            Check(crow.State == CrowAI.CrowState.Perched && cage.Baited, "latte never targeted");
            cage.RestoreCargo(0, cage, true, InventoryKind.Bread);
            yield return new WaitForSeconds(.2f);
            Check(crow.State == CrowAI.CrowState.Approaching, "approach started");
            Check(cage.TryStealBait(), "another crow consumes first");
            yield return null;
            Check(crow.State == CrowAI.CrowState.Returning && crow.Target == null, "stale target abandoned");
            yield return new WaitForSeconds(4);
            // Real NavMesh rat: theft must synchronously discard its existing route.
            var source = Object.FindAnyObjectByType<RatAI>();
            var rat = Track(Instantiate(source, source.transform.position, source.transform.rotation));
            yield return null;
            var agent = rat.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Check(agent.isOnNavMesh, "test rat has navigation");
            cage.RestoreCargo(0, cage, true, RatBait.ForRat(rat.Kind));
            // The theft path applies to the two edible rat baits.
            cage.RestoreCargo(0, cage, true, InventoryKind.Bread);
            typeof(RatAI).GetField("baitTarget", Private).SetValue(rat, cage);
            agent.SetDestination(source.transform.position + Vector3.right);
            Check(cage.TryStealBait(), "steal from navigating rat");
            Check(!agent.hasPath && typeof(RatAI).GetField("baitTarget", Private).GetValue(rat) == null, "rat route cleared immediately");
            rat.enabled = false;
            rat.isGrey = rat.isAggressive = false;
            cage.transform.position = rat.transform.position;
            cage.RestoreCargo(0, cage, true, InventoryKind.Bread);
            typeof(RatCage).GetField("scaredUntil", Private).SetValue(cage, Time.time - 1);
            Check(cage.TryStealBait() && !cage.TryCapture(rat), "theft wins race");
            cage.RestoreCargo(0, cage, true, InventoryKind.Bread);
            typeof(RatCage).GetField("scaredUntil", Private).SetValue(cage, Time.time - 1);
            Check(cage.TryCapture(rat) && !cage.TryStealBait(), "rat wins race");
            Object.DestroyImmediate(cage.gameObject);
            cage = Track(Instantiate(AssetDatabase.LoadAssetAtPath<RatCage>("Assets/Art/SmallGame/RatCage.prefab"), origin + Vector3.forward*2, Quaternion.identity));
            Check(crow.State == CrowAI.CrowState.Perched, "ready for perch hit");
            var branch = GameObject.CreatePrimitive(PrimitiveType.Cube); created.Add(branch);
            branch.transform.position = crow.transform.position + Vector3.down * .1f;
            branch.transform.localScale = new Vector3(.13f, .13f, 4);
            Vector3 perchPosition = crow.transform.position;
            FireAt(crow);
            yield return new WaitForSeconds(.2f);
            Check(crow.IsSedated && crow.RemainingSleep > 24.5f && !crow.GetComponent<Rigidbody>().isKinematic && crow.GetComponent<Rigidbody>().useGravity, "physical perched dart enables falling");
            cage.RestoreCargo(0, cage, true, InventoryKind.Bread);
            yield return new WaitForSeconds(.5f);
            Check(cage.Baited && crow.Target == null && crow.transform.position.y < perchPosition.y - .5f, "falls off solid branch without stealing bait");
            yield return new WaitForSeconds(1);
            Check(crow.transform.position.y > origin.y - .3f && crow.transform.position.y < origin.y + .5f, "lands on floor");
            float remaining = crow.RemainingSleep;
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.2f);
            Check(Mathf.Abs(crow.RemainingSleep - remaining) < .02f, "pause freezes sleep");
            Time.timeScale = 1;
            FireAt(crow);
            yield return new WaitForSeconds(.2f);
            Check(crow.RemainingSleep > remaining, "repeat dart extends sleep");
            yield return new WaitForSeconds(24);
            Check(crow.IsSedated && cage.Baited, "still asleep before 25s");
            yield return new WaitForSeconds(1.2f);
            Check(!crow.IsSedated, "wakes after 25s");
            // Freeze decision long enough to hit a known flying state with an actual dart.
            Check(crow.State == CrowAI.CrowState.Returning, "returns from ground after wake");
            float recoveryDeadline = Time.time + Vector3.Distance(crow.transform.position, perchPosition) / crow.flightSpeed + 10;
            while (crow.State != CrowAI.CrowState.Approaching && Time.time < recoveryDeadline) yield return null;
            Check(crow.State == CrowAI.CrowState.Approaching, "resumes approach after returning to perch");
            FireAt(crow);
            yield return new WaitForSeconds(.2f);
            Check(crow.IsSedated && !crow.GetComponent<Rigidbody>().isKinematic && crow.Target == null, "airborne dart cancels flight");
            float height = crow.transform.position.y;
            yield return new WaitForSeconds(.7f);
            Check(crow.transform.position.y < height && cage.Baited, "sleeping bird falls without theft");
            typeof(CrowAI).GetField("wakeAt", Private).SetValue(crow, Time.time);
            yield return null;
            Check(crow.State == CrowAI.CrowState.Returning && crow.GetComponent<Rigidbody>().isKinematic, "wakes and returns");
            FireAt(crow);
            yield return new WaitForSeconds(.2f);
            Check(crow.IsSedated && crow.Target == null, "physical return-flight dart");
            typeof(CrowAI).GetField("wakeAt", Private).SetValue(crow, Time.time);
            yield return new WaitForSeconds(5);
            Check(!crow.IsSedated, "second recovery");
            Check(crow.ApplyHit() && crow.IsSedated && !crow.GetComponent<Rigidbody>().isKinematic, "generic item hit enables falling");
        }
        private void FireAt(CrowAI bird)
        {
            var shooter = new GameObject("Test shooter"); created.Add(shooter);
            Vector3 point = bird.GetComponent<SphereCollider>().bounds.center;
            // Fire close enough that a small bird moving at 5 m/s stays on the shot line.
            shooter.transform.position = point + Vector3.right * .5f;
            var dart = Track(Instantiate(AssetDatabase.LoadAssetAtPath<TranquilizerDart>("Assets/Prefabs/TranquilizerDart.prefab"), shooter.transform.position, Quaternion.LookRotation(Vector3.left)));
            dart.Launch(shooter.transform, Vector3.left*60, 1, 3);
        }
        private void Cleanup()
        {
            Time.timeScale = previousScale;
            foreach (var item in created) if (item != null) Object.DestroyImmediate(item);
            created.Clear();
        }
        private void OnDestroy() => Cleanup();
    }
}
