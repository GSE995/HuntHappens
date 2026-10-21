using System;
using System.Linq;
using HuntHappens.Animals;
using HuntHappens.Delivery;
using HuntHappens.Interaction;
using HuntHappens.Player;
using HuntHappens.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.Editor
{
    [InitializeOnLoad]
    public static class DeliveryPlayChecks
    {
        private const string Pending = "HuntHappens.DeliveryPlayChecks";
        private static int stage;
        private static float began;
        private static double deadline;
        private static PickupTruckDelivery truck;
        private static DeliveryManager manager;
        private static DeliveryZone zone;
        private static DeerSpawner spawner;
        private static DeerSedation[] deer;
        private static PlayerInteraction interaction;
        private static ItemHolder holder;
        private static Camera camera;
        private static Vector3 oldCratePosition;
        private static PickupItem crate;

        static DeliveryPlayChecks() => EditorApplication.playModeStateChanged += OnPlay;
        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch only");
            EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }
        private static void OnPlay(PlayModeStateChange value)
        {
            if (value != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            stage = 0;
            began = Time.time;
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline, "Timeout at stage " + stage);
                float elapsed = Time.time - began;
                if (stage == 0 && elapsed > 0.3f)
                {
                    truck = UnityEngine.Object.FindAnyObjectByType<PickupTruckDelivery>();
                    manager = UnityEngine.Object.FindAnyObjectByType<DeliveryManager>();
                    zone = truck.Zone;
                    spawner = UnityEngine.Object.FindAnyObjectByType<DeerSpawner>();
                    interaction = UnityEngine.Object.FindAnyObjectByType<PlayerInteraction>();
                    holder = interaction.GetComponent<ItemHolder>();
                    camera = interaction.GetComponentInChildren<Camera>();
                    interaction.GetComponent<FirstPersonMotor>().enabled = false;
                    MovePlayerToRear();
                    Check(manager.Balance == 0 && manager.DeliveredCount == 0 && !manager.QuestComplete, "Fresh runtime starts at zero");
                    Check(UnityEngine.Object.FindAnyObjectByType<GameHUD>() != null, "Goal HUD present");
                    deer = UnityEngine.Object.FindObjectsByType<DeerSedation>().OrderBy(d => d.name).ToArray();
                    Check(deer.Length == 3, "Three original animals");
                    foreach (DeerSedation animal in deer)
                    {
                        animal.GetComponent<DeerAI>().enabled = false;
                        animal.GetComponent<NavMeshAgent>().enabled = false;
                        animal.SetDurations(0.5f, 1f, 0.7f);
                    }
                    PositionBody(deer[0], new Vector3(0, 1.3f, -2.65f));
                    crate = UnityEngine.Object.FindObjectsByType<PickupItem>().First(p => p.name == "Pickup Crate Small");
                    oldCratePosition = crate.Body.position;
                    crate.Body.position = truck.transform.TransformPoint(new Vector3(0, 1.3f, 0));
                    Next();
                }
                else if (stage == 1 && elapsed > 1.6f)
                {
                    Check(zone.ReadyCount == 0 && !interaction.TrySubmitDelivery() && manager.Balance == 0, "Awake animal and crate rejected");
                    crate.Body.position = oldCratePosition;
                    crate.Body.linearVelocity = Vector3.zero;
                    foreach (DeerSedation animal in deer) animal.ApplyDose(1);
                    Next();
                }
                else if (stage == 2 && elapsed > 0.8f)
                {
                    Check(deer.All(d => d.State == DeerSedation.SedationState.Unconscious), "Animals became unconscious");
                    PositionBody(deer[0], new Vector3(0, 1.3f, -2.65f));
                    Check(holder.TryPickUp(deer[0].GetComponent<PickupItem>()), "Use existing carry system");
                    Check(!interaction.TrySubmitDelivery() && !manager.TryDeliver(deer[0], zone), "Held sleeping animal rejected");
                    holder.Drop();
                    PositionBody(deer[0], new Vector3(0, 1.3f, -2.65f));
                    deer[0].GetComponent<Rigidbody>().linearVelocity = truck.transform.forward * 15;
                    Next();
                }
                else if (stage == 3 && elapsed > 0.25f)
                {
                    Check(!zone.IsReady(deer[0]) && !manager.TryDeliver(deer[0], zone), "Fast pass through zone never pays");
                    foreach (DeerSedation animal in deer) animal.SetDurations(0.5f, 120f, 0.7f);
                    // A real pickup/drop records the release; test positions stand in for manual transport.
                    ReleaseIntoBed(deer[0], new Vector3(-1.1f, 1.3f, -2.65f));
                    Check(!zone.IsReady(deer[0]), "Newly dropped animal requires settling");
                    Next();
                }
                else if (stage == 4)
                {
                    if (zone.IsReady(deer[0]))
                    {
                        Check(elapsed >= zone.SettleDuration, "Continuous settle duration enforced");
                        Check(deer[0].DeliveryReserved, "Animal in bed is protected from waking");
                        Check(interaction.TrySubmitDelivery(), "E interaction path submits at rear");
                        Check(manager.Balance == 100 && manager.DeliveredCount == 1, "First deer awards exactly 100");
                        Check(!manager.TryDeliver(deer[0], zone) && !interaction.TrySubmitDelivery(), "Immediate duplicate delivery rejected");
                        Check(manager.Balance == 100, "Duplicate cannot pay twice");
                        Next();
                    }
                    else Check(elapsed < 8, "First animal settles inside bed");
                }
                else if (stage == 5 && elapsed > 3.8f)
                {
                    Check(spawner.ActivePopulation == 3, "One delivered animal replaced, population capped");
                    foreach (DeerSedation animal in UnityEngine.Object.FindObjectsByType<DeerSedation>())
                        if (animal.name == "Deer Replacement")
                        {
                            Check(Vector3.Distance(animal.transform.position, interaction.transform.position) >= spawner.MinimumPlayerDistance - 2,
                                "Replacement starts far from player");
                            Check(animal.GetComponent<NavMeshAgent>().isOnNavMesh, "Replacement on NavMesh");
                        }
                    ReleaseIntoBed(deer[1], new Vector3(-1.1f, 1.3f, -2.65f));
                    ReleaseIntoBed(deer[2], new Vector3(1.1f, 1.3f, -2.65f));
                    deer[1].SetDurations(0.5f, 1, 0.7f);
                    deer[2].SetDurations(0.5f, 1, 0.7f);
                    Next();
                }
                else if (stage == 6)
                {
                    if (zone.ReadyCount == 2)
                    {
                        Check(deer[1].State == DeerSedation.SedationState.Unconscious && deer[2].State == DeerSedation.SedationState.Unconscious,
                            "Bed reservation pauses expired wake timers");
                        // Being at the front of the truck cannot collect even when cargo is ready.
                        var controller = interaction.GetComponent<CharacterController>();
                        controller.enabled = false;
                        interaction.transform.position = truck.transform.TransformPoint(new Vector3(0, 0.1f, 6));
                        controller.enabled = true;
                        Check(!interaction.TrySubmitDelivery(), "Submission only from rear");
                        MovePlayerToRear();
                        Check(interaction.TrySubmitDelivery(), "Multiple deer submitted in one E action");
                        Check(manager.Balance == 300 && manager.DeliveredCount == 3 && manager.QuestComplete, "Three deliveries complete quest and pay 300");
                        Check(manager.Notification.Split('\n').Length == 2, "Batch delivery shows a reward message for each animal");
                        Check(!interaction.TrySubmitDelivery() && manager.Balance == 300, "Empty bed cannot pay again");
                        Next();
                    }
                    else Check(elapsed < 8, "Two animals settle together");
                }
                else if (stage == 7 && elapsed > 7.5f)
                {
                    Check(spawner.ActivePopulation == 3 && spawner.PendingReplacements == 0, "Population restored without accumulation");
                    Check(manager.QuestComplete && manager.Balance == 300 && Time.timeScale == 1, "Quest complete without stopping game");
                    Debug.Log("Delivery Play checks PASSED: awake/held/crate/fly-through rejection, settled release, rear interaction, exactly $100 per deer, duplicate protection, multiple cargo, 3/3 goal, safe replacement and capped population.");
                    Finish(0);
                }
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }

        private static void MovePlayerToRear()
        {
            var controller = interaction.GetComponent<CharacterController>();
            controller.enabled = false;
            interaction.transform.position = truck.transform.TransformPoint(new Vector3(0, 0.1f, -5.8f));
            interaction.transform.rotation = truck.transform.rotation;
            camera.transform.localRotation = Quaternion.identity;
            controller.enabled = true;
        }
        private static void PositionBody(DeerSedation animal, Vector3 localCenter)
        {
            Rigidbody body = animal.GetComponent<Rigidbody>();
            Quaternion rotation = truck.transform.rotation * Quaternion.Euler(0, 90, 0);
            body.rotation = rotation;
            BoxCollider box = animal.GetComponent<BoxCollider>();
            body.position = truck.transform.TransformPoint(localCenter) - rotation * box.center;
            if (!body.isKinematic) body.linearVelocity = body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }
        private static void ReleaseIntoBed(DeerSedation animal, Vector3 localCenter)
        {
            Check(holder.TryPickUp(animal.GetComponent<PickupItem>()), "Sleeping animal can be carried before loading");
            holder.Drop();
            PositionBody(animal, localCenter);
        }
        private static void Next() { stage++; began = Time.time; }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("Delivery Play check failed: " + message); }
        private static void Finish(int code) { EditorApplication.update -= Tick; EditorApplication.Exit(code); }
    }
}
