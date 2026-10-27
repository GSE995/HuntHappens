using System;
using System.Linq;
using HuntHappens.Characters;
using HuntHappens.Interaction;
using HuntHappens.Player;
using HuntHappens.Shop;
using HuntHappens.SmallGame;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    public static class SemenychChecks
    {
        public static bool SellAtNpc(RatHunting hunt)
        {
            var npc = Object.FindAnyObjectByType<SemenychDialogue>();
            var input = hunt.GetComponent<PrototypeInput>();
            int before = hunt.Rats;
            if (!npc.TryOpen(input, null)) return false;
            npc.ShowMenu(); npc.OfferSale(); npc.ConfirmSale(); npc.Close();
            return before > 0 && hunt.Rats == 0;
        }
        private static void Check(bool ok, string what) { if (!ok) throw new Exception("Semenych: " + what); }
        private static void Click(SemenychDialogue npc, string label)
        {
            var button = npc.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.GetComponentInChildren<TMPro.TMP_Text>().text == label);
            button.onClick.Invoke();
        }
        public static string Run()
        {
            if (!Application.isPlaying) throw new Exception("Use Play Mode");
            var npc = Object.FindAnyObjectByType<SemenychDialogue>();
            var hunt = Object.FindAnyObjectByType<RatHunting>();
            var input = hunt.GetComponent<PrototypeInput>();
            var inventory = hunt.GetComponent<PlayerInventory>();
            var menu = Object.FindAnyObjectByType<HuntHappens.UI.GameMenu>();
            string playerSave = PlayerPrefs.GetString(ShopProgress.PlayerKey);
            bool oldFlag = ShopProgress.InventoryChecksRunning;
            var oldPosition = hunt.transform.position;
            bool oldBlocked = input.GameplayBlocked;
            try
            {
                ShopProgress.InventoryChecksRunning = true;
                ShopProgress.Reset(); inventory.Reload(); hunt.Reload(); hunt.shop.ReloadProgress();
                Check(!Object.FindObjectsByType<WorldShopItem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(p => p.Kind == 3), "old reception absent");
                input.SetGameplayBlocked(false);
                Check(npc.TryOpen(input, null) && input.GameplayBlocked && npc.LineIndex == 0, "first opening");
                Click(npc, "Далее  →"); Check(npc.LineIndex == 1, "one line per click");
                npc.Close(); Check(!npc.HasMet && !input.GameplayBlocked, "interrupted introduction not completed");
                Check(npc.TryOpen(input, null) && npc.LineIndex == 0, "unfinished introduction restarts");
                for (int i = 0; i < 6; i++) { Check(npc.LineIndex == i, "intro line " + i); npc.Advance(); }
                Check(npc.HasMet && npc.CurrentPage == SemenychDialogue.Page.Menu, "intro completion persisted");
                npc.Close(); Check(npc.TryOpen(input, null) && npc.CurrentPage == SemenychDialogue.Page.Menu, "reopening skips introduction");
                for (int round = 0; round < 2; round++)
                {
                    Click(npc, "Повторить подсказки");
                    for (int i = 0; i < 5; i++) { Check(npc.LineIndex == i, "hint line " + i); npc.Advance(); }
                    Check(npc.CurrentPage == SemenychDialogue.Page.Menu, "hints return to menu");
                }
                Click(npc, "Продать крыс"); Check(npc.CurrentLine == "Карманами не шурши, я на хлеб не ловлюсь. Крыс неси", "empty catch");
                npc.Advance();
                for (int kind = 0; kind < 4; kind++)
                {
                    npc.Close();
                    var data = ShopProgress.Load();
                    data.rats = kind == 3 ? 3 : 1; data.greyRats = kind == 1 || kind == 3 ? 1 : 0;
                    data.largeRats = kind == 2 || kind == 3 ? 1 : 0; data.balance = 100;
                    data.inventoryVersion = 2;
                    data.inventory = new[] { new InventoryEntry { kind = InventoryKind.Cage, cageRat = 1 } };
                    data.hand = new InventoryEntry { kind = InventoryKind.Cage, cageRat = kind == 3 ? 3 : kind + 1 };
                    PlayerPrefs.SetString(ShopProgress.Key, JsonUtility.ToJson(data));
                    inventory.Reload(); hunt.Reload(); hunt.shop.ReloadProgress();
                    Check(npc.TryOpen(input, null), "sale open");
                    Click(npc, "Продать крыс");
                    int price = kind == 0 ? 10 : kind == 1 ? 15 : kind == 2 ? 25 : 50;
                    Check(npc.CurrentLine.Contains("Итого: $" + price) && npc.CurrentLine.Contains(data.rats + " шт."), "quote quantity/total " + kind);
                    Click(npc, "Отмена"); npc.ConfirmSale();
                    Check(hunt.Rats == data.rats && hunt.shop.Wallet.Balance == 100, "cancel cannot pay");
                    Click(npc, "Продать крыс"); npc.Close(); npc.ConfirmSale();
                    Check(hunt.Rats == data.rats && hunt.shop.Wallet.Balance == 100, "close cancels");
                    npc.TryOpen(input, null); Click(npc, "Продать крыс"); Click(npc, "Подтвердить продажу");
                    npc.ConfirmSale();
                    Check(hunt.Rats == 0 && hunt.shop.Wallet.Balance == 100 + price, "exactly once " + kind);
                    Check(npc.CurrentLine == "Так… лапы, хвост, наглый взгляд. Всё на месте. Держи деньги", "success response");
                    var saved = ShopProgress.Load();
                    Check(saved.rats == 0 && saved.greyRats == 0 && saved.largeRats == 0 && saved.balance == 100 + price && saved.metSemenych, "single saved snapshot");
                    Check(saved.hand.cageRat == 0 && saved.inventory[0].cageRat == 0, "cargo cleared, cages kept");
                    inventory.Reload(); hunt.Reload(); hunt.shop.ReloadProgress();
                    Check(hunt.Rats == 0 && hunt.shop.Wallet.Balance == 100 + price && inventory.Hand.cageRat == 0, "reload sale");
                }
                npc.Close(); hunt.Recover(true); npc.TryOpen(input, null); npc.OfferSale();
                hunt.Recover(true, true); int balance = hunt.shop.Wallet.Balance;
                npc.ConfirmSale(); Check(hunt.Rats == 2 && hunt.shop.Wallet.Balance == balance, "changed quote rejected");
                npc.Advance(); npc.OfferSale(); npc.ConfirmSale(); Check(hunt.Rats == 0, "requote succeeds");
                npc.Close();
                // A fresh component loads the introduction flag; no scene-local cache is required.
                var clone = Object.Instantiate(npc.gameObject);
                try { Check(clone.GetComponent<SemenychDialogue>().HasMet, "new NPC reads saved meeting"); }
                finally { Object.DestroyImmediate(clone); }
                QuickSale(npc, hunt, input);
                Check(PlayerPrefs.GetString(ShopProgress.PlayerKey) == playerSave, "real save untouched");
                return "PASSED: introduction 6 lines, interruption/reopen, hints 5 lines twice, empty catch, three species and mixed sale, cancel/close, double confirmation, changed quote, cages, wallet/inventory/meeting reload.";
            }
            finally
            {
                npc.Close(); ShopProgress.Reset(); ShopProgress.InventoryChecksRunning = oldFlag;
                inventory.Reload(); hunt.Reload(); hunt.shop.ReloadProgress();
                hunt.transform.position = oldPosition; input.SetGameplayBlocked(oldBlocked);
            }
        }
        private static void QuickSale(SemenychDialogue npc, RatHunting hunt, PrototypeInput input)
        {
            npc.Close(); npc.ResetHold();
            int balance = hunt.shop.Wallet.Balance;
            hunt.Recover(true); hunt.Recover(true, true); hunt.Recover(true, false, true);
            Check(!hunt.shop.BuySupply(3) && hunt.Rats == 3 && hunt.shop.Wallet.Balance == balance, "shop cannot buy rats");
            Check(npc.InteractionPrompt(hunt).StartsWith("E — начать диалог\nУдерживайте E, чтобы продать крыс"), "short interaction prompt");
            npc.HandleInteraction(input, null, true, true, false, 0);
            npc.HandleInteraction(input, null, false, true, false, npc.QuickSaleHoldSeconds * .4f);
            Check(!npc.IsOpen && hunt.Rats == 3 && npc.HoldProgress > 0, "hold waits");
            npc.HandleInteraction(input, null, false, false, true, 0);
            Check(npc.IsOpen && hunt.Rats == 3, "short release opens dialogue without sale");
            npc.Close();
            npc.HandleInteraction(input, null, true, true, false, 0);
            npc.HandleInteraction(input, null, false, true, false, npc.QuickSaleHoldSeconds * .4f);
            npc.ResetHold(); // The ray lost its target.
            npc.HandleInteraction(input, null, false, true, false, 2f);
            Check(hunt.Rats == 3 && npc.HoldProgress == 0, "leaving target cancels, fresh press required");
            npc.HandleInteraction(input, null, true, true, false, 0);
            npc.HandleInteraction(input, null, false, true, false, 0);
            Check(hunt.Rats == 3, "zero game time does not advance");
            input.SetGameplayBlocked(true);
            npc.HandleInteraction(input, null, false, true, false, 2f);
            input.SetGameplayBlocked(false);
            npc.HandleInteraction(input, null, false, true, false, 2f);
            Check(hunt.Rats == 3, "blocked input cancels");
            npc.HandleInteraction(input, null, true, true, false, 0);
            npc.HandleInteraction(input, null, false, true, false, npc.QuickSaleHoldSeconds);
            Check(hunt.Rats == 0 && hunt.shop.Wallet.Balance == balance + 50 && !npc.IsOpen, "quick mixed sale without UI");
            hunt.Recover(true);
            npc.HandleInteraction(input, null, false, true, false, 5f);
            Check(hunt.Rats == 1 && hunt.shop.Wallet.Balance == balance + 50, "continued hold cannot sell new catch");
            npc.HandleInteraction(input, null, false, false, true, 0);
            Check(!npc.IsOpen, "release after sale does not open dialogue");
            npc.HandleInteraction(input, null, true, true, false, 0);
            hunt.Recover(true, true);
            npc.HandleInteraction(input, null, false, true, false, npc.QuickSaleHoldSeconds);
            Check(hunt.Rats == 2 && hunt.shop.Wallet.Balance == balance + 50, "changed quick quote rejected");
            npc.HandleInteraction(input, null, false, false, true, 0);
            npc.HandleInteraction(input, null, true, true, false, 0);
            npc.HandleInteraction(input, null, false, true, false, npc.QuickSaleHoldSeconds);
            Check(hunt.Rats == 0 && ShopProgress.Load().balance == balance + 75, "fresh hold and save");
            npc.HandleInteraction(input, null, false, false, true, 0);
            npc.HandleInteraction(input, null, true, true, false, 0);
            npc.HandleInteraction(input, null, false, true, false, npc.QuickSaleHoldSeconds);
            Check(npc.InteractionPrompt(hunt).Contains("Карманами не шурши") && hunt.shop.Wallet.Balance == balance + 75, "empty quick sale");
            npc.ResetHold();
        }
    }
}
