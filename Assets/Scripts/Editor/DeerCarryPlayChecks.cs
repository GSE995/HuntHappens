using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HuntHappens.Animals;
using HuntHappens.Interaction;
using HuntHappens.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    public static class DeerCarryPlayChecks
    {
        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var created = new List<GameObject>();
            var report = new List<string>();
            void Check(bool condition, string name)
            {
                if (!condition) throw new Exception("Carry check failed: " + name);
                report.Add("PASS " + name);
            }
            try
            {
                var player = new GameObject("Carry check player"); created.Add(player);
                player.transform.position = new Vector3(0, 1000, 0);
                player.AddComponent<CharacterController>();
                var input = player.AddComponent<PrototypeInput>(); input.enabled = false;
                var holder = player.AddComponent<ItemHolder>(); holder.SetView(player.transform);
                var motor = player.AddComponent<FirstPersonMotor>(); motor.enabled = false;
                player.GetComponent<FirstPersonLook>().enabled = false;
                float normalSpeed = motor.CurrentMoveSpeed;
                var sprint = typeof(PrototypeInput).GetField("<SprintHeld>k__BackingField", flags);
                Vector3 spawn = Object.FindObjectsByType<DeerSedation>(FindObjectsSortMode.None)
                    .First(d => d.GetComponent<NavMeshAgent>().enabled && d.GetComponent<NavMeshAgent>().isOnNavMesh).transform.position;
                foreach (string name in new[] { "Deer", "BossDeer" })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Animals/" + name + ".prefab");
                    var animal = Object.Instantiate(prefab, spawn, Quaternion.identity); created.Add(animal);
                    var deer = animal.GetComponent<DeerSedation>();
                    var item = animal.GetComponent<PickupItem>();
                    animal.GetComponent<DeerAI>().enabled = false;
                    animal.GetComponent<NavMeshAgent>().enabled = false;
                    animal.transform.position = player.transform.position + Vector3.forward * 2;
                    Check(!holder.TryPickUp(item), name + ": awake deer cannot be picked up");
                    animal.GetComponent<DeerPhysics>().FallAsleep();
                    typeof(DeerSedation).GetField("currentState", flags).SetValue(deer, DeerSedation.SedationState.Unconscious);
                    Check(item.Body.mass > holder.MaximumMass && holder.TryPickUp(item), name + ": sleeping deer can be carried without upgrades");
                    Check(holder.IsHoldingDeer && Mathf.Approximately(motor.CurrentMoveSpeed, normalSpeed * .55f), name + ": carrying slows player by 45 percent");
                    sprint.SetValue(input, true);
                    Check(Mathf.Approximately(motor.CurrentMoveSpeed, normalSpeed * .55f), name + ": Shift cannot bypass carry slowdown");
                    Check(!animal.GetComponent<DeerPhysics>().TryBeginRecovery(), name + ": held deer cannot wake");
                    sprint.SetValue(input, false);
                    holder.Drop();
                    Check(!holder.IsHolding && item.Holder == null && item.Body.useGravity && Mathf.Approximately(motor.CurrentMoveSpeed, normalSpeed), name + ": dropping restores movement and physics");
                    typeof(DeerSedation).GetField("currentState", flags).SetValue(deer, DeerSedation.SedationState.Awake);
                    var trapObject = new GameObject("Carry check trap"); created.Add(trapObject);
                    var trap = trapObject.AddComponent<HuntHappens.Traps.AnimalTrap>();
                    Check(deer.TryTrap(trap) && holder.TryPickUp(item) && deer.Trap == null && deer.State == DeerSedation.SedationState.Unconscious, name + ": pickup releases trapped deer");
                    holder.Drop();
                }
                var heavy = new GameObject("Carry check heavy prop"); created.Add(heavy);
                heavy.AddComponent<BoxCollider>(); heavy.AddComponent<Rigidbody>().mass = 100;
                Check(!holder.TryPickUp(heavy.AddComponent<PickupItem>()), "ordinary props still respect weight limit");
                return string.Join("\n", report);
            }
            finally
            {
                for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
            }
        }
    }
}
