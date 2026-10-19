using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        }

        private void LateUpdate()
        {
            bool visible = interaction != null && interaction.isActiveAndEnabled && Cursor.lockState == CursorLockMode.Locked;
            canvas.enabled = visible;
            prompt.text = !visible ? "" : interaction.IsHolding ? "E — Отпустить | ЛКМ — Бросить" :
                interaction.Target != null ? "E — Поднять" : "";
        }

        private void OnDisable()
        {
            if (canvas != null) canvas.enabled = false;
        }

    }
}
