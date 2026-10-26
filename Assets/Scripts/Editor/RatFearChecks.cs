using System;
using System.Collections;
using System.Reflection;
using HuntHappens.SmallGame;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    public static class RatFearChecks
    {
        public static string Result { get; private set; } = "Not run";
        // Existing synchronous inventory checks isolate the bait matrix from the real-time fear test.
        internal static bool WhenCalm(RatCage cage, Func<bool> action)
        {
            var owner = cage.Owner;
            var position = owner.transform.position;
            try
            {
                owner.transform.position = cage.transform.position + Vector3.right * 10;
                typeof(RatCage).GetField("scaredUntil", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(cage, Time.time - 1);
                return action();
            }
            finally { owner.transform.position = position; }
        }
        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode");
            if (Result == "Running") throw new InvalidOperationException("Already running");
            Result = "Running";
            new GameObject("Rat fear checks").AddComponent<RatFearCheckRunner>();
            return Result;
        }
        internal static void Finish(string result) => Result = result;
    }

    public sealed class RatFearCheckRunner : MonoBehaviour
    {
        private RatCage cage;
        private RatAI rat;
        private GameObject owner;
        private float previousScale;
        private void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
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
                    catch (Exception e) { RatFearChecks.Finish("FAILED: " + e); yield break; }
                    yield return next;
                }
                RatFearChecks.Finish("PASSED: all 3 species wait; proximity blocks capture; retreat countdown; reapproach resets; pause freezes; ready after 3 seconds; capture consumes bait; restored cage waits again.");
            }
            finally { Cleanup(); Destroy(gameObject); }
        }
        private IEnumerator Verify()
        {
            var template = Object.FindAnyObjectByType<RatHunting>().cagePrefab;
            owner = new GameObject("Fear test owner");
            var hunt = owner.AddComponent<RatHunting>(); hunt.enabled = false;
            for (int species = 0; species < 3; species++)
            {
                Time.timeScale = 1;
                cage = Instantiate(template, new Vector3(1000, 0, 1000), Quaternion.identity); cage.Owner = hunt;
                rat = new GameObject("Fear test rat").AddComponent<RatAI>(); rat.enabled = false;
                rat.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
                rat.visual = new GameObject("Visual").transform; rat.visual.SetParent(rat.transform);
                rat.isGrey = species == 1; rat.isAggressive = species == 2;
                typeof(RatAI).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(rat, null);
                rat.transform.position = cage.transform.position;
                hunt.transform.position = cage.transform.position + Vector3.right;
                cage.RestoreCargo(0, template, true, RatBait.ForRat(species));
                Check(!cage.Accepts(rat) && !cage.TryCapture(rat) && cage.Baited, "immediate capture blocked " + species);
                yield return new WaitForSeconds(.2f);
                Check(cage.FearRemaining > 2.9f, "nearby player holds timer");
                hunt.transform.position = cage.transform.position + Vector3.right * 4;
                yield return new WaitForSeconds(1);
                Check(cage.FearRemaining > 1.7f && cage.FearRemaining < 2.2f && !cage.TryCapture(rat), "retreat countdown");
                hunt.transform.position = cage.transform.position + Vector3.right;
                Check(cage.FearRemaining > 2.9f, "reapproach resets synchronously");
                hunt.transform.position = cage.transform.position + Vector3.right * 4;
                float remaining = cage.FearRemaining;
                Time.timeScale = 0;
                yield return new WaitForSecondsRealtime(.3f);
                Check(Mathf.Abs(cage.FearRemaining - remaining) < .02f && !cage.TryCapture(rat), "pause preserves timer");
                Time.timeScale = 1;
                yield return new WaitForSeconds(3.1f);
                Check(cage.Accepts(rat) && cage.TryCapture(rat) && !cage.Baited && rat.Caught, "calm capture " + species);
                DestroyImmediate(cage.gameObject); cage = null;
                DestroyImmediate(rat.gameObject); rat = null;
                cage = Instantiate(template, new Vector3(1000, 0, 1000), Quaternion.identity); cage.Owner = hunt;
                cage.RestoreCargo(0, template, true, RatBait.ForRat(species));
                Check(cage.FearRemaining > 2.9f, "replaced prebaited cage waits again");
                DestroyImmediate(cage.gameObject); cage = null;
            }
        }
        private void Cleanup()
        {
            Time.timeScale = previousScale;
            if (cage != null) DestroyImmediate(cage.gameObject);
            if (rat != null) DestroyImmediate(rat.gameObject);
            if (owner != null) DestroyImmediate(owner);
        }
        private void OnDestroy() => Cleanup();
    }
}
