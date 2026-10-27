using System.Globalization;
using HuntHappens.Delivery;
using TMPro;
using UnityEngine;

namespace HuntHappens.UI
{
    // Built with the HUD, so no scene/prefab migration or per-reward allocations are needed.
    public sealed class DeerRewardEffect : MonoBehaviour
    {
        const float Duration = 0.95f;
        DeliveryManager wallet;
        HuntHappens.SmallGame.RatHunting hunting;
        RatRewardGraphic ratArt;
        Canvas canvas;
        int pendingRats, pendingRatCount;
        bool showingRats;
        RectTransform host, target, reward;
        CanvasGroup group;
        TextMeshProUGUI amount, caption;
        AntlerRewardGraphic antlers;
        int pending, pendingCount;
        float elapsed, pulse;
        bool playing;
        static readonly CultureInfo Numbers = CultureInfo.GetCultureInfo("ru-RU");

        public void Build(RectTransform parent, RectTransform destination, TMP_FontAsset font, DeliveryManager source, HuntHappens.SmallGame.RatHunting ratSource = null)
        {
            host = parent; target = destination; wallet = source; hunting = ratSource;
            canvas = parent.GetComponentInParent<Canvas>();
            reward = Rect("Deer Reward", host, new Vector2(660, 300));
            group = reward.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            var art = Rect("Antler Flash", reward, new Vector2(660, 300));
            antlers = art.gameObject.AddComponent<AntlerRewardGraphic>(); antlers.raycastTarget = false;
            ratArt = Rect("Rat Ears and Whiskers", reward, new Vector2(660,300)).gameObject.AddComponent<RatRewardGraphic>();
            ratArt.raycastTarget = false; ratArt.gameObject.SetActive(false);
            amount = Label("Reward Amount", reward, TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset : font, 100, new Vector2(640, 130));
            amount.fontStyle = FontStyles.Bold;
            amount.outlineColor = new Color32(54, 31, 12, 255); amount.outlineWidth = 0.22f;
            caption = Label("Reward Caption", reward, font, 26, new Vector2(520, 45));
            caption.rectTransform.anchoredPosition = new Vector2(0, -76);
            reward.gameObject.SetActive(false);
            wallet.DeerRewarded += Enqueue;
            if (hunting != null) hunting.RatsSold += EnqueueRats;
        }
        void Enqueue(int value) { if (!isActiveAndEnabled || value <= 0) return; pending = (int)System.Math.Min(int.MaxValue, (long)pending + value); pendingCount++; }
        void EnqueueRats(int value, int count)
        {
            if (!isActiveAndEnabled || value <= 0 || count <= 0) return;
            pendingRats = (int)System.Math.Min(int.MaxValue, (long)pendingRats + value);
            pendingRatCount = (int)System.Math.Min(int.MaxValue, (long)pendingRatCount + count);
        }
        void LateUpdate()
        {
            if (target == null) return;
            // Scaled time intentionally freezes both the reward and wallet pulse in the pause menu.
            float dt = Time.timeScale == 0 || (canvas != null && !canvas.enabled) ? 0 : Time.deltaTime;
            if (pulse > 0)
            {
                pulse = Mathf.Max(0, pulse - dt);
                target.localScale = Vector3.one * (1 + 0.13f * Mathf.Sin(pulse / 0.22f * Mathf.PI));
            }
            if (!playing && (pending > 0 || pendingRats > 0) && (canvas == null || canvas.enabled))
            {
                showingRats = pending == 0;
                amount.text = "+$" + (showingRats ? pendingRats : pending).ToString("N0", Numbers);
                caption.text = showingRats ? "УЛОВ ПРОДАН ×" + pendingRatCount : pendingCount > 1 ? "ОЛЕНИ СДАНЫ ×" + pendingCount : "ОЛЕНЬ СДАН";
                antlers.gameObject.SetActive(!showingRats); ratArt.gameObject.SetActive(showingRats);
                if (showingRats) pendingRats = pendingRatCount = 0; else pending = pendingCount = 0; elapsed = 0; playing = true;
                reward.gameObject.SetActive(true); reward.SetAsLastSibling();
            }
            if (!playing) return;
            elapsed += dt;
            Render(elapsed);
            if (elapsed >= Duration)
            {
                playing = false; reward.gameObject.SetActive(false); pulse = 0.22f;
            }
        }
        void Render(float time)
        {
            float flight = Mathf.Clamp01((time - 0.55f) / 0.4f);
            float ease = flight * flight * (3 - 2 * flight);
            Vector2 start = new Vector2(0, -25);
            Vector2 end = host.InverseTransformPoint(target.position);
            reward.anchoredPosition = Vector2.Lerp(start, end, ease) + Vector2.up * (Mathf.Sin(flight * Mathf.PI) * 100);
            float pop = time < 0.15f ? Mathf.Lerp(0.65f, 1.10f, Mathf.Sin(Mathf.Clamp01(time / 0.15f) * Mathf.PI * 0.5f)) : Mathf.Lerp(1.10f, 1, Mathf.Clamp01((time - 0.15f) / 0.13f));
            reward.localScale = Vector3.one * Mathf.Lerp(pop, 0.22f, ease);
            group.alpha = 1 - Mathf.Clamp01((flight - 0.8f) / 0.2f);
            caption.alpha = 1 - Mathf.Clamp01(flight * 4);
            antlers.Intensity = (1 - Mathf.Clamp01(flight * 3));
            antlers.Burst = 1 - Mathf.Clamp01(time / 0.3f);
            antlers.SetVerticesDirty();
            ratArt.Intensity = antlers.Intensity;
            ratArt.Wobble = Mathf.Sin(time * 24) * 8 * Mathf.Exp(-time * 5);
            ratArt.SetVerticesDirty();
        }
        void OnDisable()
        {
            pending = pendingCount = pendingRats = pendingRatCount = 0; playing = false; pulse = 0;
            if (reward != null) reward.gameObject.SetActive(false);
            if (target != null) target.localScale = Vector3.one;
        }
        void OnDestroy() { if (wallet != null) wallet.DeerRewarded -= Enqueue; if (hunting != null) hunting.RatsSold -= EnqueueRats; }
        static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f; rect.sizeDelta = size;
            return rect;
        }
        static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, float size, Vector2 bounds)
        {
            var text = Rect(name, parent, bounds).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSize = size; text.color = new Color(1, 0.95f, 0.77f);
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.enableAutoSizing = true; text.fontSizeMin = 36; text.fontSizeMax = size;
            if (size < 36) { text.enableAutoSizing = false; }
            return text;
        }
    }
}
