using System;
using HuntHappens.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.Editor
{
    [InitializeOnLoad]
    public static class HeroPlayChecks
    {
        private const string Pending = "HuntHappens.HeroCheck";
        private static HeroWander hero;
        private static Vector3 start;
        private static Quaternion[] pose;
        private static bool animated;
        private static double deadline;
        private static Mesh baked;
        private static int groundedFrames;
        private static float largestGap;
        static HeroPlayChecks() => EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            hero = UnityEngine.Object.FindAnyObjectByType<HeroWander>();
            start = hero.transform.position;
            var bones = hero.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            pose = Array.ConvertAll(bones, b => b.localRotation);
            deadline = EditorApplication.timeSinceStartup + 45;
            baked = new Mesh();
            animated = false;
            groundedFrames = 0;
            largestGap = 0;
            EditorApplication.update += Tick;
        };

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Hero movement/animation timed out");
                if (Time.frameCount < 3) return;
                var agent = hero.GetComponent<NavMeshAgent>();
                if (!agent.isOnNavMesh) throw new Exception("Hero is off NavMesh");
                var bones = hero.GetComponentInChildren<SkinnedMeshRenderer>().bones;
                for (int i = 0; i < bones.Length; i++) animated |= Quaternion.Angle(pose[i], bones[i].localRotation) > 5;
                hero.GetComponent<HeroGrounding>().Measure(out float sole, out float top);
                var ground = GameObject.Find("Environment/Ground").GetComponent<Collider>();
                if (!ground.Raycast(new Ray(hero.transform.position + Vector3.up * 5, Vector3.down), out RaycastHit hit, 10))
                    throw new Exception("No ground below Hero");
                float gap = sole - hit.point.y;
                largestGap = Mathf.Max(largestGap, Mathf.Abs(gap));
                if (Mathf.Abs(gap) > 0.04f) throw new Exception("Hero sole gap: " + gap);
                groundedFrames++;
                if (!animated || Vector3.Distance(start, hero.transform.position) < 2 || groundedFrames < 100) return;
                if (top - sole < 2.1f || top - sole > 2.4f) throw new Exception("Unexpected Hero height: " + (top - sole));
                Debug.Log($"Hero play check PASSED: height={top-sole:F3}m, moved >2m, skeletal walk animated, {groundedFrames} grounded samples, maximum sole gap={largestGap:F4}m.");
                EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.update -= Tick;
                EditorApplication.Exit(1);
            }
        }
    }
}
