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
        [SerializeField] private Texture2D iconAtlas;
        private HuntHappens.UI.FieldHUD field;
        private HuntHappens.UI.SketchPanel promptBackdrop;
        private GameObject crosshair;
        public HuntHappens.UI.FieldHUD Field => field;
        public TMP_FontAsset ThemeFont => promptFont;
        public HuntHappens.Player.PrototypeInput PlayerInput => interaction != null ? interaction.GetComponent<HuntHappens.Player.PrototypeInput>() : null;
        public void SetStyle(TMP_FontAsset font, Texture2D icons) { promptFont = font; iconAtlas = icons; }
        private TranquilizerGun gun;
        private HuntHappens.SmallGame.RatHunting rats;
        private HuntHappens.Traps.TrapPlacement traps;
        private TextMeshProUGUI trapNotice;
        private HuntHappens.Player.PlayerHealth health;
        private UnityEngine.UI.Image damageFlash;
        public void SetInteraction(PlayerInteraction value) => interaction = value;
        public void SetFont(TMP_FontAsset value) => promptFont = value;

        private void Awake()
        {
            if (GetComponent<HuntHappens.UI.GameMenu>() == null)
                gameObject.AddComponent<HuntHappens.UI.GameMenu>();
            var canvasObject = new GameObject("Interaction Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var flashObject = new GameObject("Damage Flash", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            flashObject.transform.SetParent(canvas.transform, false);
            damageFlash = flashObject.GetComponent<UnityEngine.UI.Image>();
            damageFlash.raycastTarget = false;
            damageFlash.color = new Color(0.85f, 0.02f, 0.02f, 0);
            damageFlash.rectTransform.anchorMin = Vector2.zero;
            damageFlash.rectTransform.anchorMax = Vector2.one;
            damageFlash.rectTransform.offsetMin = damageFlash.rectTransform.offsetMax = Vector2.zero;

            var dot = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
            crosshair = dot;
            dot.transform.SetParent(canvas.transform, false);
            var rect = dot.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = new Vector2(5, 5);
            dot.GetComponent<Image>().raycastTarget = false;

            // The generator bakes the required Cyrillic glyphs into a serialized font asset.
            if (promptFont == null) promptFont = TMP_Settings.defaultFontAsset;
            var background = new GameObject("Prompt Backdrop", typeof(RectTransform));
            background.transform.SetParent(canvas.transform, false);
            promptBackdrop = background.AddComponent<HuntHappens.UI.SketchPanel>();
            promptBackdrop.color = new Color(0.04f,0.055f,0.025f,0.7f);
            promptBackdrop.raycastTarget = false;
            promptBackdrop.rectTransform.sizeDelta = new Vector2(650,80);
            promptBackdrop.rectTransform.anchoredPosition = new Vector2(0,-85);
            var label = new GameObject("Interaction Prompt", typeof(RectTransform));
            label.transform.SetParent(canvas.transform, false);
            prompt = label.AddComponent<TextMeshProUGUI>();
            prompt.font = promptFont;
            prompt.fontSize = 32;
            prompt.alignment = TextAlignmentOptions.Center;
            prompt.color = Color.white;
            prompt.raycastTarget = false;
            prompt.rectTransform.anchorMin = prompt.rectTransform.anchorMax = Vector2.one * 0.5f;
            prompt.rectTransform.anchoredPosition = new Vector2(0, -85);
            prompt.rectTransform.sizeDelta = new Vector2(1000, 120);
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
            if (interaction != null) health = interaction.GetComponent<HuntHappens.Player.PlayerHealth>();
            if (interaction != null) rats = interaction.GetComponent<HuntHappens.SmallGame.RatHunting>();
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
            tools.gameObject.SetActive(false);
            if(rats != null) { field = gameObject.AddComponent<HuntHappens.UI.FieldHUD>(); field.Build(canvas.transform, rats, promptFont, iconAtlas); }
        }

        private void LateUpdate()
        {
            if (field != null) field.Refresh();
            bool inventoryOpen = field != null && field.IsOpen;
            bool visible = interaction != null && interaction.isActiveAndEnabled && !interaction.GameplayBlocked && Cursor.lockState == CursorLockMode.Locked;
            canvas.enabled = visible || inventoryOpen;
            crosshair.SetActive(visible && !inventoryOpen);
            float flash = health != null ? Mathf.Clamp01(1f - (Time.time - health.LastHitAt) / 0.65f) : 0;
            damageFlash.color = new Color(0.85f, 0.02f, 0.02f, 0.32f * flash * flash);
            prompt.text = !visible || inventoryOpen || interaction.IsHolding ? "" :
                !string.IsNullOrEmpty(interaction.TransportHint) ? interaction.TransportHint :
                interaction.FarmerTarget != null ? "E — Поговорить с фермером" :
                interaction.SemenychTarget != null ? interaction.SemenychTarget.InteractionPrompt(rats) :
                interaction.InventoryTarget != null ? interaction.InventoryTarget.Prompt :
                interaction.ProductTarget != null ? interaction.ProductTarget.Prompt :
                interaction.DeliveryTarget != null ? "E — Сдать животных" :
                interaction.ShopTarget != null ? "E — Купить улучшения" :
                interaction.RatTarget != null || interaction.ObservedDeer != null ? "" :
                interaction.CageTarget != null ? interaction.CageTarget.Prompt :
                interaction.BreadTarget != null ? interaction.BreadTarget.Prompt :
                rats != null && rats.Selected ? rats.Hint :
                interaction.TrapTarget != null ? interaction.TrapTarget.Prompt :
                interaction.Target != null ? interaction.Target.PickupPrompt :
                gun != null && gun.TrapSelected && traps != null ? traps.PlacementHint : "";
            if (visible && !inventoryOpen && string.IsNullOrEmpty(prompt.text)) prompt.text = interaction.GetComponent<PlayerInventory>().Hint;
            if (!string.IsNullOrEmpty(prompt.text))
            {
                // Bait descriptions can wrap to several lines; keep the whole prompt on its backdrop.
                Vector2 textSize = prompt.GetPreferredValues(prompt.text, 1000, Mathf.Infinity);
                float height = Mathf.Max(60, textSize.y);
                prompt.rectTransform.sizeDelta = new Vector2(1000, height);
                prompt.rectTransform.anchoredPosition = new Vector2(0, -45 - height * .5f);
                promptBackdrop.rectTransform.sizeDelta = new Vector2(Mathf.Clamp(textSize.x + 40, 650, 1040), height + 24);
                promptBackdrop.rectTransform.anchoredPosition = prompt.rectTransform.anchoredPosition;
            }
            promptBackdrop.gameObject.SetActive(!string.IsNullOrEmpty(prompt.text));
            if (tools != null) tools.text = interaction.GetComponent<PlayerInventory>().Hint;
            if (health != null) tools.text += "\n" + health.Status;
            trapNotice.text = rats != null && Time.unscaledTime < rats.EscapeNoticeUntil ? rats.EscapeNotice :
                traps != null && traps.Manager != null && Time.time < traps.Manager.NotificationUntil ? traps.Manager.Notification : "";
        }

        private void OnDisable()
        {
            if (field != null) field.Hide();
            if (canvas != null) canvas.enabled = false;
        }

    }
}
