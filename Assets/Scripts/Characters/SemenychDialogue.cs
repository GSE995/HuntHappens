using HuntHappens.Player;
using HuntHappens.Shop;
using HuntHappens.SmallGame;
using TMPro;
using UnityEngine;

namespace HuntHappens.Characters
{
    [DisallowMultipleComponent]
    public sealed class SemenychDialogue : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset dialogueFont;
        [SerializeField] private DialogueData introduction;
        [SerializeField] private DialogueData hints;
        [SerializeField, Min(1)] private int ordinaryPrice = 10;
        [SerializeField, Min(1)] private int greyPrice = 15;
        [SerializeField, Min(1)] private int largePrice = 25;
        [SerializeField, Min(.2f)] private float quickSaleHoldSeconds = 1f;
        public float QuickSaleHoldSeconds => Mathf.Max(.2f, quickSaleHoldSeconds);
        public float HoldProgress => Mathf.Clamp01(holdElapsed / QuickSaleHoldSeconds);
        private float holdElapsed, quickNoticeUntil;
        private bool holdStarted, holdConsumed;
        private string quickNotice;
        private PrototypeInput holdPlayer;

        public string InteractionPrompt(RatHunting catchInventory)
        {
            string prompt = "E — начать диалог\nУдерживайте E, чтобы продать крыс";
            if (holdStarted && !holdConsumed) prompt += $"\nПродажа: {Mathf.FloorToInt(HoldProgress * 100)}%";
            if (Time.unscaledTime < quickNoticeUntil) prompt += "\n" + quickNotice;
            return prompt;
        }

        public void ResetHold()
        {
            holdStarted = holdConsumed = false; holdElapsed = 0; holdPlayer = null;
        }

        // Called only while the normal interaction ray targets this NPC.
        public void HandleInteraction(PrototypeInput player, TMP_FontAsset font, bool pressed, bool held, bool released, float deltaTime)
        {
            if (!isActiveAndEnabled || IsOpen || player == null || player.GameplayBlocked) { ResetHold(); return; }
            if (pressed)
            {
                ResetHold();
                var cargo = player.GetComponent<RatHunting>();
                if (cargo == null) return;
                cargo.RefreshCarriedRats();
                holdPlayer = player; holdStarted = true;
                quotedOrdinary = cargo.Rats - cargo.GreyRats - cargo.LargeRats;
                quotedGrey = cargo.GreyRats; quotedLarge = cargo.LargeRats;
                quotedOrdinaryPrice = ordinaryPrice; quotedGreyPrice = greyPrice; quotedLargePrice = largePrice;
            }
            if (!holdStarted || holdPlayer != player) return;
            if (held && !holdConsumed)
            {
                // Start timing at the press, without counting the preceding frame.
                if (!pressed) holdElapsed += Mathf.Max(0, deltaTime);
                if (holdElapsed >= QuickSaleHoldSeconds)
                {
                    holdConsumed = true;
                    var cargo = player.GetComponent<RatHunting>();
                    bool sold = cargo.TrySellQuoted(quotedOrdinary, quotedGrey, quotedLarge, quotedOrdinaryPrice, quotedGreyPrice, quotedLargePrice);
                    quickNotice = sold ? "Так… лапы, хвост, наглый взгляд. Всё на месте. Держи деньги" :
                        cargo.Rats == 0 ? "Карманами не шурши, я на хлеб не ловлюсь. Крыс неси" :
                        "Улов изменился или продажа недоступна. Отпустите E и попробуйте снова.";
                    quickNoticeUntil = Time.unscaledTime + 4f;
                }
            }
            if (released)
            {
                bool talk = !holdConsumed;
                ResetHold();
                if (talk) TryOpen(player, font);
            }
            else if (!held) ResetHold();
        }
        public int OrdinaryPrice => ordinaryPrice;
        public int GreyPrice => greyPrice;
        public int LargePrice => largePrice;
        public enum Page { Closed, Introduction, Menu, Hints, Confirmation, Message }
        public Page CurrentPage { get; private set; }
        public int LineIndex { get; private set; }
        public string CurrentLine => panel != null ? panel.body.text : "";
        public bool IsOpen => panel != null && panel.root.activeSelf;
        public bool HasMet => ShopProgress.Load().metSemenych;
        private DialoguePanel panel;
        private PrototypeInput input;
        private RatHunting hunt;
        private int quotedOrdinary, quotedGrey, quotedLarge;
        private int quotedOrdinaryPrice, quotedGreyPrice, quotedLargePrice;

        private void Awake()
        {
            var animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.speed = 0;
        }

        public bool TryOpen(PrototypeInput player, TMP_FontAsset font)
        {
            if (!isActiveAndEnabled || IsOpen || player == null || player.GameplayBlocked || introduction == null || hints == null) return false;
            hunt = player.GetComponent<RatHunting>();
            if (hunt == null) return false;
            input = player;
            if (panel == null) panel = new DialoguePanel(transform, dialogueFont != null ? dialogueFont : font, "Семёныч", Advance);
            input.SetGameplayBlocked(true);
            panel.root.SetActive(true);
            if (HasMet) ShowMenu();
            else { CurrentPage = Page.Introduction; LineIndex = 0; ShowLine(); }
            return true;
        }
        private void ShowLine()
        {
            var data = CurrentPage == Page.Introduction ? introduction : hints;
            panel.Line(data.GetLine(LineIndex), $"{LineIndex + 1} / {data.LineCount}    •    Esc — закрыть",
                LineIndex == data.LineCount - 1 ? "К разговору" : "Далее  →");
        }
        public void Advance()
        {
            if (!IsOpen) return;
            if (CurrentPage == Page.Message) { ShowMenu(); return; }
            if (CurrentPage != Page.Introduction && CurrentPage != Page.Hints) return;
            var data = CurrentPage == Page.Introduction ? introduction : hints;
            if (++LineIndex < data.LineCount) { ShowLine(); return; }
            if (CurrentPage == Page.Introduction)
            {
                var save = ShopProgress.Load(); save.metSemenych = true;
                PlayerPrefs.SetString(ShopProgress.Key, JsonUtility.ToJson(save)); PlayerPrefs.Save();
            }
            ShowMenu();
        }
        public void ShowMenu()
        {
            if (!IsOpen) return;
            CurrentPage = Page.Menu;
            panel.body.text = $"Крыс принёс? Обычная — ${ordinaryPrice}, серая — ${greyPrice}, большая — ${largePrice} за штуку.";
            panel.Choices(new[] { "Продать крыс", "Повторить подсказки", "До встречи" },
                new UnityEngine.Events.UnityAction[] { OfferSale, ShowHints, Close });
        }
        public void ShowHints()
        {
            if (!IsOpen || CurrentPage != Page.Menu) return;
            CurrentPage = Page.Hints; LineIndex = 0; ShowLine();
        }
        public void OfferSale()
        {
            if (!IsOpen || CurrentPage != Page.Menu) return;
            hunt.RefreshCarriedRats();
            if (hunt.Rats == 0) { Message("Карманами не шурши, я на хлеб не ловлюсь. Крыс неси"); return; }
            quotedOrdinary = hunt.Rats - hunt.GreyRats - hunt.LargeRats;
            quotedGrey = hunt.GreyRats; quotedLarge = hunt.LargeRats;
            quotedOrdinaryPrice = ordinaryPrice; quotedGreyPrice = greyPrice; quotedLargePrice = largePrice;
            long total = (long)quotedOrdinary * ordinaryPrice + (long)quotedGrey * greyPrice + (long)quotedLarge * largePrice;
            CurrentPage = Page.Confirmation;
            panel.body.text = $"Продать весь улов: {hunt.Rats} шт.?\nОбычных: {quotedOrdinary}, серых: {quotedGrey}, больших: {quotedLarge}.\nИтого: ${total}. Клетки остаются у тебя.";
            panel.Choices(new[] { "Подтвердить продажу", "Отмена" }, new UnityEngine.Events.UnityAction[] { ConfirmSale, ShowMenu });
        }
        public void ConfirmSale()
        {
            if (!IsOpen || CurrentPage != Page.Confirmation) return;
            // Consume the confirmation before invoking any wallet callbacks.
            CurrentPage = Page.Message;
            bool sold = hunt.TrySellQuoted(quotedOrdinary, quotedGrey, quotedLarge, quotedOrdinaryPrice, quotedGreyPrice, quotedLargePrice);
            Message(sold ? "Так… лапы, хвост, наглый взгляд. Всё на месте. Держи деньги" :
                "Улов изменился или продажа сейчас недоступна. Давай пересчитаем — выбери продажу ещё раз.");
        }
        private void Message(string text)
        {
            CurrentPage = Page.Message;
            panel.Line(text, "Esc — закрыть", "К разговору");
        }
        public void Close()
        {
            CurrentPage = Page.Closed;
            if (!IsOpen) return;
            panel.root.SetActive(false);
            if (input != null) input.SetGameplayBlocked(false);
            input = null; hunt = null;
        }
        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (IsOpen && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
#endif
        }
        private void OnDisable() { ResetHold(); Close(); }
    }
}
