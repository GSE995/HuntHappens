using System.Collections.Generic;
using UnityEngine;

namespace HuntHappens.Traps
{
    [DisallowMultipleComponent]
    public sealed class TrapManager : MonoBehaviour
    {
        [SerializeField] private AnimalTrap trapPrefab;
        [SerializeField, Min(1)] private int maximumTraps = 3;
        private readonly List<AnimalTrap> placed = new List<AnimalTrap>();
        public AnimalTrap Prefab => trapPrefab;
        private int additionalTraps;
        public int MaximumTraps => maximumTraps + additionalTraps;
        public void SetAdditionalTraps(int value) => additionalTraps = Mathf.Clamp(value, 0, 3);
        public int PlacedCount { get { placed.RemoveAll(t => t == null || !t.gameObject.activeInHierarchy); return placed.Count; } }
        public string Notification { get; private set; }
        public float NotificationUntil { get; private set; }
        public void Configure(AnimalTrap prefab) => trapPrefab = prefab;
        public AnimalTrap TryPlace(Vector3 position, Quaternion rotation)
        {
            if (trapPrefab == null || PlacedCount >= MaximumTraps) return null;
            AnimalTrap trap = Instantiate(trapPrefab, position, rotation, transform);
            trap.name = "Animal Trap";
            trap.Initialize(this);
            placed.Add(trap);
            return trap;
        }
        public void Notify(string message) { Notification = message; NotificationUntil = Time.time + 4f; }
        public void Retrieve(AnimalTrap trap)
        {
            if (!placed.Contains(trap) || trap.CaughtDeer != null) return;
            placed.Remove(trap);
            trap.gameObject.SetActive(false);
            Destroy(trap.gameObject);
            Notify("Ловушка подобрана. Можно установить снова.");
        }
    }
}
