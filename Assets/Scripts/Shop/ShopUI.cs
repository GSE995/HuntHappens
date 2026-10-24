using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuntHappens.Shop
{
    [DisallowMultipleComponent]
    public sealed class ShopUI : MonoBehaviour
    {
        [SerializeField] private ShopManager manager;
        [SerializeField] private TMP_FontAsset font;
        private GameObject root;
        private TMP_Text balance;
        private TMP_Text message;
        private readonly TMP_Text[] details = new TMP_Text[4];
        private readonly TMP_Text[] prices = new TMP_Text[4];
        private readonly Button[] buttons = new Button[4];
        public bool Visible => root != null && root.activeSelf;
        public void Configure(ShopManager shop, TMP_FontAsset hudFont) { manager = shop; font = hudFont; }
        private void Awake()
        {
            root = new GameObject("Shop Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var shade = Box("Backdrop", root.transform, Vector2.zero, Vector2.zero, new Color(0.015f, 0.04f, 0.06f, 0.8f));
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = Vector2.one;
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            var panel = Box("Shop Panel", root.transform, Vector2.zero, new Vector2(1520, 920), new Color(0.08f, 0.17f, 0.23f));
            var title = Label("Title", panel, new Vector2(0, 390), new Vector2(1000, 65), 46);
            title.text = "УЛУЧШЕНИЯ СНАРЯЖЕНИЯ";
            title.color = new Color(1, 0.8f, 0.3f);
            balance = Label("Balance", panel, new Vector2(0, 325), new Vector2(1100, 45), 29);
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                UpgradeDefinition definition = manager.Upgrades.Definition((UpgradeKind)i);
                var card = Box(definition.Title, panel, new Vector2(i % 2 == 0 ? -360 : 360, i < 2 ? 135 : -120),
                    new Vector2(680, 245), new Color(0.14f, 0.28f, 0.31f));
                var name = Label("Name", card, new Vector2(0, 88), new Vector2(620, 38), 29);
                name.alignment = TextAlignmentOptions.Left;
                name.text = definition.Title;
                var description = Label("Effect", card, new Vector2(0, 49), new Vector2(620, 48), 21);
                description.alignment = TextAlignmentOptions.Left;
                description.text = definition.Description;
                details[i] = Label("Levels", card, new Vector2(0, -21), new Vector2(620, 82), 21);
                details[i].alignment = TextAlignmentOptions.Left;
                buttons[i] = MakeButton("Buy", card, new Vector2(175, -95), new Vector2(270, 43), out prices[i]);
                buttons[i].onClick.AddListener(() => manager.TryBuy((UpgradeKind)index));
            }
            message = Label("Message", panel, new Vector2(0, -365), new Vector2(1390, 50), 25);
            message.color = new Color(1, 0.85f, 0.35f);
            Label("Controls", panel, new Vector2(0, -420), new Vector2(1200, 35), 22).text = "E / Escape — закрыть магазин и продолжить охоту";
            Button close = MakeButton("Close", panel, new Vector2(650, 395), new Vector2(150, 45), out TMP_Text closeText);
            closeText.text = "Закрыть";
            close.onClick.AddListener(manager.Close);
            Show(false);
        }
        public void Show(bool visible) { if (root != null) root.SetActive(visible); }
        public Button BuyButton(UpgradeKind kind) => buttons[(int)kind];
        private void LateUpdate()
        {
            if (!Visible || manager == null) return;
            var supplies = FindAnyObjectByType<HuntHappens.SmallGame.RatHunting>();
            balance.text = "Ваш баланс: $" + manager.Wallet.Balance + (supplies != null ? "\n" + supplies.Stock : "");
            balance.fontSize = 23; balance.rectTransform.sizeDelta = new Vector2(1400, 75);
            message.text = manager.Message;
            for (int i = 0; i < 4; i++)
            {
                var definition = manager.Upgrades.Definition((UpgradeKind)i);
                int level = manager.Upgrades.Level((UpgradeKind)i);
                bool maximum = level >= definition.MaximumLevel;
                details[i].text = "Уровень " + level + (maximum ? " — максимум" : " -> " + (level + 1)) +
                    "\nСейчас: " + definition.DescribeLevel(level) +
                    (maximum ? "" : "\nДалее: " + definition.DescribeLevel(level + 1));
                prices[i].text = maximum ? "Максимум" : "Купить — $" + definition.NextPrice(level);
                // Insufficient funds remain clickable so the player gets an explicit explanation.
                buttons[i].interactable = !maximum && manager.CanPurchaseNow;
            }
        }
        private RectTransform Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            image.color = color;
            return rect;
        }
        private TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size, float fontSize)
        {
            var label = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);
            label.font = font;
            label.fontSize = fontSize;
            label.color = new Color(0.96f, 0.97f, 0.9f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = Vector2.one * 0.5f;
            label.rectTransform.anchoredPosition = position;
            label.rectTransform.sizeDelta = size;
            return label;
        }
        private Button MakeButton(string name, Transform parent, Vector2 position, Vector2 size, out TMP_Text text)
        {
            RectTransform rect = Box(name, parent, position, size, new Color(1, 0.63f, 0.18f));
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            text = Label("Label", rect, Vector2.zero, size - new Vector2(12, 2), 23);
            text.color = new Color(0.07f, 0.14f, 0.16f);
            return button;
        }
    }
}
