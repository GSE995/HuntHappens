using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HuntHappens.Weapons;

namespace HuntHappens.Interaction
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class InteractionHUD : MonoBehaviour
    {
        [SerializeField] private PlayerInteraction interaction;
        [SerializeField] private TMP_FontAsset promptFont;
        private TextMeshProUGUI prompt;
        private Canvas canvas;
        private TextMeshProUGUI tools;
        private TranquilizerGun gun;
        private HuntHappens.Traps.TrapPlacement traps;
        private TextMeshProUGUI trapNotice;
        public void SetInteraction(PlayerInteraction value) => interaction = value;
        public void SetFont(TMP_FontAsset value) => promptFont = value;

        private void Awake()
        {
            var canvasObject = new GameObject("Interaction Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var dot = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(canvas.transform, false);
            var rect = dot.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = new Vector2(5, 5);
            dot.GetComponent<Image>().raycastTarget = false;

            // The generator bakes the required Cyrillic glyphs into a serialized font asset.
            if (promptFont == null) promptFont = TMP_Settings.defaultFontAsset;
            var label = new GameObject("Interaction Prompt", typeof(RectTransform));
            label.transform.SetParent(canvas.transform, false);
            prompt = label.AddComponent<TextMeshProUGUI>();
            prompt.font = promptFont;
            prompt.fontSize = 27;
            prompt.alignment = TextAlignmentOptions.Center;
            prompt.color = Color.white;
            prompt.raycastTarget = false;
            prompt.rectTransform.anchorMin = prompt.rectTransform.anchorMax = Vector2.one * 0.5f;
            prompt.rectTransform.anchoredPosition = new Vector2(0, -55);
            prompt.rectTransform.sizeDelta = new Vector2(800, 65);
            var toolObject = new GameObject("Selected Tool", typeof(RectTransform));
            toolObject.transform.SetParent(canvas.transform, false);
            tools = toolObject.AddComponent<TextMeshProUGUI>();
            tools.font = promptFont;
            tools.fontSize = 23;
            tools.alignment = TextAlignmentOptions.BottomLeft;
            tools.raycastTarget = false;
            tools.rectTransform.anchorMin = tools.rectTransform.anchorMax = Vector2.zero;
            tools.rectTransform.pivot = Vector2.zero;
            tools.rectTransform.anchoredPosition = new Vector2(30, 25);
            tools.rectTransform.sizeDelta = new Vector2(1800, 145);
            if (interaction != null) gun = interaction.GetComponent<TranquilizerGun>();
            if (interaction != null) traps = interaction.GetComponent<HuntHappens.Traps.TrapPlacement>();
            var notice = new GameObject("Trap Notification", typeof(RectTransform));
            notice.transform.SetParent(canvas.transform, false);
            trapNotice = notice.AddComponent<TextMeshProUGUI>();
            trapNotice.font = promptFont;
            trapNotice.fontSize = 29;
            trapNotice.alignment = TextAlignmentOptions.Center;
            trapNotice.color = new Color(1, 0.85f, 0.25f);
            trapNotice.raycastTarget = false;
            trapNotice.rectTransform.anchorMin = trapNotice.rectTransform.anchorMax = Vector2.one * 0.5f;
            trapNotice.rectTransform.anchoredPosition = new Vector2(0, 110);
            trapNotice.rectTransform.sizeDelta = new Vector2(1000, 80);
        }

        private void LateUpdate()
        {
            bool visible = interaction != null && interaction.isActiveAndEnabled && !interaction.GameplayBlocked && Cursor.lockState == CursorLockMode.Locked;
            canvas.enabled = visible;
            prompt.text = !visible ? "" : interaction.IsHolding ? "E — Отпустить | ЛКМ — Бросить" :
                interaction.DeliveryTarget != null ? "E — Сдать животных" :
                interaction.ShopTarget != null ? "E — Открыть магазин" :
                interaction.TrapTarget != null ? interaction.TrapTarget.Prompt :
                interaction.Target != null ? interaction.Target.PickupPrompt :
                interaction.ObservedDeer != null ? interaction.ObservedDeer.StatusText :
                gun != null && gun.TrapSelected && traps != null ? traps.PlacementHint : "";
            if (tools != null)
                tools.text = gun == null ? "" : (gun.TrapSelected ? "Ловушка" : gun.Selected ? "Транквилизатор" : "Взаимодействие") +
                    (traps != null && traps.Manager != null ? " | Ловушки: " + traps.Manager.PlacedCount + "/" + traps.Manager.MaximumTraps : "") +
                    "\n1 — Транквилизатор | 2 — Взаимодействие | 3 — Ловушка\nE — Поднять / Отпустить | ЛКМ — " +
                    (interaction != null && interaction.IsHolding ? "Бросить" : gun.TrapSelected ? "Установить ловушку" : gun.Selected ? "Выстрел" : "Бросить предмет");
            trapNotice.text = traps != null && traps.Manager != null && Time.time < traps.Manager.NotificationUntil ? traps.Manager.Notification : "";
        }

        private void OnDisable()
        {
            if (canvas != null) canvas.enabled = false;
        }

    }
}
