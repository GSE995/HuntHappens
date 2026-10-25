using HuntHappens.Delivery;
using HuntHappens.Shop;
using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.Animals
{
    [DefaultExecutionOrder(300)]
    public sealed class DeerCaptureZone : MonoBehaviour
    {
        public string zoneId = "training";
        [Min(0)] public int ordinaryCount = 3;
        [Min(2)] public float herdRadius = 8;
        public DeerSedation ordinaryPrefab;
        public DeerSedation leaderPrefab;
        public bool hasLeader;
        public int Count => ordinaryCount + (hasLeader ? 1 : 0);
        public string AnimalId(int index) => zoneId + ":" + (index == ordinaryCount ? "leader" : index.ToString());
        private bool[] spawned;
        private float retryAt;
        private void Start() { spawned = new bool[Count]; SpawnMissing(); }
        private void Update() { if (Time.time >= retryAt) SpawnMissing(); }
        private void SpawnMissing()
        {
            retryAt = Time.time + 2;
            var returned = ShopProgress.Load().returnedDeerIds ?? new string[0];
            var player = FindAnyObjectByType<Player.FirstPersonMotor>();
            for (int i = 0; i < spawned.Length; i++)
            {
                if (spawned[i]) continue;
                var prefab = i == ordinaryCount ? leaderPrefab : ordinaryPrefab;
                if (prefab == null) continue;
                var template = prefab.GetComponent<NavMeshAgent>();
                var filter = new NavMeshQueryFilter { agentTypeID = template.agentTypeID, areaMask = template.areaMask };
                Vector3 offset = i == ordinaryCount ? Vector3.zero : Quaternion.Euler(0, i * 360f / Mathf.Max(1, ordinaryCount), 0) * Vector3.forward * herdRadius * .6f;
                if (!NavMesh.SamplePosition(transform.position + offset, out var hit, 3, filter)) continue;
                var deer = Instantiate(prefab, hit.position, Quaternion.identity, transform);
                deer.QuestId = AnimalId(i);
                deer.name = deer.QuestId;
                deer.GetComponent<DeerPerception>().SetPlayer(player.transform);
                deer.GetComponent<DeerAI>().SetHerdHome(transform, herdRadius, hasLeader && i < ordinaryCount);
                spawned[i] = true;
                int returnedIndex = System.Array.IndexOf(returned, deer.QuestId);
                if (returnedIndex >= 0)
                {
                    var enclosure = FindAnyObjectByType<DeerEnclosure>();
                    if (enclosure != null) enclosure.RestoreAnimal(deer, returnedIndex);
                    else Destroy(deer.gameObject);
                }
                else if (hasLeader && i == ordinaryCount)
                    deer.GetComponent<DeerLeader>().SetHome(transform);
            }
        }
        private void OnDrawGizmosSelected() { Gizmos.color = hasLeader ? Color.yellow : Color.green; Gizmos.DrawWireSphere(transform.position, herdRadius); }
    }
}
