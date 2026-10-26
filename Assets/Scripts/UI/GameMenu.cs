using HuntHappens.Interaction;
using HuntHappens.Player;
using HuntHappens.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HuntHappens.UI
{
    // Built beside the existing runtime HUD, using its typeface and field-notebook style.
    [DisallowMultipleComponent]
    public sealed class GameMenu : MonoBehaviour
    {
        private PrototypeInput input;
        private GameObject root;
        private TMP_FontAsset font;
        private float previousTimeScale;
        private bool restarting;
        private static bool skipNextOpening;
        public bool IsOpen => root != null && root.activeSelf;
        public UnityEngine.UI.Button ContinueButton { get; private set; }
        public UnityEngine.UI.Button RestartButton { get; private set; }

        private void Start()
        {
            input = GetComponent<InteractionHUD>().PlayerInput;
            if (input == null) { enabled = false; return; }
            font = GetComponent<InteractionHUD>().ThemeFont;
            Build();
            input.PauseToggleRequested += Toggle;
            if (skipNextOpening) skipNextOpening = false;
            else if (!Application.isBatchMode && !ShopProgress.InventoryChecksRunning) Open();
        }

        public void Toggle()
        {
            if (IsOpen) Continue();
            else if (!input.GameplayBlocked) Open();
        }

        public void Open()
        {
            if (root == null || IsOpen || restarting || input.GameplayBlocked) return;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0;
            input.GetComponent<HuntHappens.SmallGame.RatHunting>()?.SetMenuPaused(true);
            input.SetGameplayBlocked(true);
            root.SetActive(true);
        }

        public void Continue()
        {
            if (!IsOpen || restarting) return;
            root.SetActive(false);
            input.GetComponent<HuntHappens.SmallGame.RatHunting>()?.SetMenuPaused(false);
            Time.timeScale = previousTimeScale;
            input.SetGameplayBlocked(false);
        }

        public void Restart()
        {
            if (!IsOpen || restarting) return;
            var scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path) || !Application.CanStreamedLevelBeLoaded(scene.path))
            {
                Debug.LogError("Cannot restart: the active scene must be included in the build scene list.", this);
                return;
            }
            restarting = true;
            ContinueButton.interactable = RestartButton.interactable = false;
            // No live managers are reloaded here: reloading the scene resets animals, health,
            // dropped objects, traps and quest state together before the next game starts.
            ShopProgress.Reset();
            skipNextOpening = true;
            Time.timeScale = previousTimeScale;
            SceneManager.LoadScene(scene.path);
        }

        private void OnDestroy()
        {
            if (input != null) input.PauseToggleRequested -= Toggle;
            if (IsOpen) Time.timeScale = previousTimeScale;
        }

        private void OnApplicationQuit()
        {
            if (!restarting && input != null) input.GetComponent<HuntHappens.SmallGame.RatHunting>()?.SetMenuPaused(false);
        }

        private void Build()
        {
            root = new GameObject("Game Menu", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;

            var shade = Rect("Shade", root.transform, Vector2.zero, Vector2.zero);
            shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one;
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            shade.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.015f, .025f, .015f, .83f);
            var panel = Rect("Menu Panel", root.transform, Vector2.zero, new Vector2(650, 540));
            var paper = panel.gameObject.AddComponent<SketchPanel>();
            paper.color = new Color(.055f, .065f, .035f, .97f);
            paper.raycastTarget = false;
            Label("Title", panel, 172, 54, "HUNT HAPPENS");
            Label("Subtitle", panel, 105, 30, "МЕНЮ ИГРЫ").color = new Color(.73f, .8f, .55f);
            ContinueButton = Button("Continue", panel, 10, "Продолжить", Continue);
            RestartButton = Button("Restart", panel, -90, "Начать заново", Restart);
            Label("New Game Hint", panel, -168, 25, "Новая игра: $15 · прогресс сбрасывается");
            Label("Controls", panel, -225, 24, "Esc — вернуться в игру");
            root.SetActive(false);
        }

        private UnityEngine.UI.Button Button(string name, Transform parent, float y, string caption, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(name, parent, new Vector2(0, y), new Vector2(500, 76));
            var background = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = Color.white;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.normalColor = new Color(.21f, .26f, .13f);
            colors.highlightedColor = new Color(.38f, .43f, .22f);
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(.7f, .32f, .12f);
            button.colors = colors;
            button.onClick.AddListener(action);
            Label("Caption", rect, 0, 38, caption);
            return button;
        }

        private TextMeshProUGUI Label(string name, Transform parent, float y, float size, string text)
        {
            var label = Rect(name, parent, new Vector2(0, y), new Vector2(610, 65)).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.color = new Color(.96f, .92f, .79f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
