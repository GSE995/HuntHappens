using System;
using System.Collections;
using System.Reflection;
using HuntHappens.Characters;
using HuntHappens.Interaction;
using HuntHappens.Player;
using HuntHappens.Shop;
using HuntHappens.SmallGame;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    public static class CrowCarryChecks
    {
        public static string Result { get; internal set; } = "Not run";
        public static string Run()
        {
            if (!Application.isPlaying || Result == "Running") throw new InvalidOperationException("Requires Play Mode and no active check");
            Result = "Running";
            new GameObject("Crow carry checks").AddComponent<CrowCarryCheckRunner>();
            return Result;
        }
    }

    public sealed class CrowCarryCheckRunner : MonoBehaviour
    {
        private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private IEnumerator Start()
        {
            var hunt = Object.FindAnyObjectByType<RatHunting>();
            var input = hunt.GetComponent<PrototypeInput>();
            var inventory = hunt.GetComponent<PlayerInventory>();
            var holder = hunt.GetComponent<ItemHolder>();
            var npc = Object.FindAnyObjectByType<SemenychDialogue>();
            bool oldFlag = ShopProgress.InventoryChecksRunning, blocked = input.GameplayBlocked;
            float oldScale = Time.timeScale;
            string playerSave = PlayerPrefs.GetString(ShopProgress.PlayerKey);
            CrowAI crow = null;
            try
            {
                ShopProgress.InventoryChecksRunning = true;
                Time.timeScale = 1;
                ShopProgress.Reset(); inventory.Reload(); hunt.Reload(); hunt.shop.ReloadProgress();
                input.SetGameplayBlocked(false);
                crow = Instantiate(AssetDatabase.LoadAssetAtPath<CrowAI>("Assets/Art/SmallGame/Crow.prefab"), holder.HoldTarget, Quaternion.identity);
                yield return null;
                var item = crow.GetComponent<PickupItem>();
                Check(!holder.TryPickUp(item), "awake bird rejected");
                Check(crow.ApplyHit() && holder.TryPickUp(item), "sleeping bird picked up");
                Check(inventory.Hand.physical == item && holder.HeldItem == item, "actual bird held");
                Check(!inventory.SelectSlot(0) && holder.IsHolding && crow.gameObject.activeSelf, "bird cannot be hidden in slot");
                yield return new WaitForSeconds(.4f);
                Check(holder.HeldItem == item && Vector3.Distance(item.Body.worldCenterOfMass, holder.HoldTarget) < .4f, "physical carry follows hands");
                // Exercise the real interaction ray while the held bird is in front of the camera.
                var view = hunt.GetComponentInChildren<Camera>();
                var target = new GameObject("Test crow buyer", typeof(BoxCollider), typeof(SemenychDialogue));
                GameObject wall = null;
                try
                {
                    target.transform.position = view.transform.position + view.transform.forward * 2.5f;
                    Physics.SyncTransforms();
                    var interaction = hunt.GetComponent<PlayerInteraction>();
                    typeof(PlayerInteraction).GetMethod("UpdateInteraction", Private).Invoke(interaction, null);
                    Check(interaction.SemenychTarget == target.GetComponent<SemenychDialogue>(), "buyer selectable while holding crow");
                    wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.position = view.transform.position + view.transform.forward * .7f;
                    wall.transform.localScale = Vector3.one * .3f;
                    Physics.SyncTransforms();
                    typeof(PlayerInteraction).GetMethod("UpdateInteraction", Private).Invoke(interaction, null);
                    Check(interaction.SemenychTarget == null, "wall blocks buyer while carrying");
                }
                finally { if (wall != null) DestroyImmediate(wall); DestroyImmediate(target); }
                holder.Drop();
                Check(!holder.IsHolding && inventory.Hand == null && item.Body.useGravity, "drop releases and restores gravity");
                Check(holder.TryPickUp(item), "pick up again");
                holder.Throw();
                Check(!holder.IsHolding && item.Body.useGravity, "throw releases bird");
                item.Body.position = holder.HoldTarget;
                item.Body.linearVelocity = Vector3.zero;
                Check(holder.TryPickUp(item), "pick up before wake");
                typeof(CrowAI).GetField("wakeAt", Private).SetValue(crow, Time.time - 1);
                typeof(CrowAI).GetMethod("Update", Private).Invoke(crow, null);
                Check(!holder.IsHolding && inventory.Hand == null && crow.State == CrowAI.CrowState.Returning, "waking bird escapes hands");
                Check(crow.ApplyHit() && holder.TryPickUp(item), "re-sedate and carry");
                int balance = hunt.shop.Wallet.Balance;
                Check(npc.TryOpen(input, null), "open dialogue carrying bird");
                npc.ShowMenu(); npc.OfferCrowSale();
                Check(npc.CurrentPage == SemenychDialogue.Page.CrowConfirmation && npc.CurrentLine.Contains("$10"), "crow quote");
                npc.ShowMenu(); npc.ConfirmCrowSale();
                Check(holder.IsHolding && hunt.shop.Wallet.Balance == balance, "cancel does not sell");
                npc.OfferCrowSale();
                typeof(CrowAI).GetField("wakeAt", Private).SetValue(crow, Time.time - 1);
                npc.ConfirmCrowSale();
                Check(hunt.shop.Wallet.Balance == balance, "expired sleep cannot be sold even before Update");
                crow.ApplyHit(); npc.ShowMenu(); npc.OfferCrowSale(); npc.ConfirmCrowSale(); npc.ConfirmCrowSale();
                Check(hunt.shop.Wallet.Balance == balance + 10 && !holder.IsHolding && crow.State == CrowAI.CrowState.Sold, "paid once and removed from hands");
                Check(ShopProgress.Load().balance == balance + 10 && !item.CanPickUp(holder.MaximumMass), "sale persisted and sold bird unavailable");
                npc.Close();
                typeof(CrowAI).GetField("respawnAt", Private).SetValue(crow, Time.time - 1);
                typeof(CrowAI).GetMethod("Update", Private).Invoke(crow, null);
                Check(crow.State == CrowAI.CrowState.Perched && crow.GetComponent<SphereCollider>().enabled, "sold bird respawns on perch");
                Check(crow.ApplyHit() && holder.TryPickUp(item), "pick up respawned bird");
                npc.HandleInteraction(input, null, true, true, false, 0);
                npc.HandleInteraction(input, null, false, true, false, npc.QuickSaleHoldSeconds + .01f);
                npc.HandleInteraction(input, null, false, true, false, 1);
                Check(hunt.shop.Wallet.Balance == balance + 20 && crow.State == CrowAI.CrowState.Sold, "quick sale pays once");
                Check(PlayerPrefs.GetString(ShopProgress.PlayerKey) == playerSave, "real player save unchanged");
                CrowCarryChecks.Result = "PASSED: awake rejection, physical carry, no slot storage, drop/throw, wake escape, quote/cancel, expired quote, sale exactly once, saved money, respawn, quick sale, real save unchanged.";
            }
            finally
            {
                if (CrowCarryChecks.Result == "Running") CrowCarryChecks.Result = "FAILED: see Unity console";
                npc.ResetHold(); npc.Close(); holder.Drop();
                if (crow != null) Destroy(crow.gameObject);
                ShopProgress.Reset(); ShopProgress.InventoryChecksRunning = oldFlag;
                inventory.Reload(); hunt.Reload(); hunt.shop.ReloadProgress(); input.SetGameplayBlocked(blocked);
                Time.timeScale = oldScale;
                Destroy(gameObject);
            }
        }
    }
}
