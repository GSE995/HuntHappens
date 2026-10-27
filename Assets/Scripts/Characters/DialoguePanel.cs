using TMPro;
using UnityEngine;

namespace HuntHappens.Characters
{
    // Shared presentation for the farmer and other NPC conversations.
    public sealed class DialoguePanel
    {
        public GameObject root;
        public TMP_Text body, nextLabel, counter;
        private RectTransform panelRect;
        private UnityEngine.UI.Button continueButton;
        private readonly System.Collections.Generic.List<GameObject> choices = new System.Collections.Generic.List<GameObject>();
        public void ClearChoices()
        {
            foreach (var choice in choices) { choice.SetActive(false); Object.Destroy(choice); }
            choices.Clear();
            continueButton.gameObject.SetActive(true);
        }
        public void Choices(string[] labels, UnityEngine.Events.UnityAction[] actions)
        {
            ClearChoices();
            continueButton.gameObject.SetActive(false);
            counter.text = "Esc — закрыть";
            counter.rectTransform.anchorMin = new Vector2(.04f, 0);
            counter.rectTransform.anchorMax = new Vector2(.96f, .1f);
            for (int i = 0; i < labels.Length; i++)
            {
                var go = new GameObject("Choice " + i, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
                go.transform.SetParent(panelRect, false);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(.04f + i * .92f / labels.Length, .11f);
                rect.anchorMax = new Vector2(.04f + (i + 1) * .92f / labels.Length - .015f, .25f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                go.GetComponent<UnityEngine.UI.Image>().color = new Color(.39f,.46f,.20f);
                var button = go.GetComponent<UnityEngine.UI.Button>();
                button.targetGraphic = go.GetComponent<UnityEngine.UI.Image>();
                button.onClick.AddListener(actions[i]);
                var label = Label("Label", rect, body.font, Vector2.zero, Vector2.one, 26);
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 26;
                label.text = labels[i];
                choices.Add(go);
            }
        }
        public void Line(string text, string progress, string next)
        {
            ClearChoices();
            counter.rectTransform.anchorMin = new Vector2(.04f, .04f);
            counter.rectTransform.anchorMax = new Vector2(.64f, .21f);
            body.text = text; counter.text = progress; nextLabel.text = next;
        }
        public DialoguePanel(Transform parent, TMP_FontAsset font, string speaker, UnityEngine.Events.UnityAction advance)
        {
            root = new GameObject("Farmer Dialogue Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            var panel = new GameObject("Dialogue Panel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            panel.transform.SetParent(root.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.12f, .05f); rect.anchorMax = new Vector2(.88f, .37f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.055f, .075f, .045f, .97f);
            Label("Speaker", rect, font, new Vector2(.04f,.77f), new Vector2(.96f,.95f), 34).text = speaker;
            body = Label("Speech", rect, font, new Vector2(.04f,.26f), new Vector2(.96f,.76f), 32);
            body.enableAutoSizing = true; body.fontSizeMin = 24; body.fontSizeMax = 32;
            counter = Label("Progress", rect, font, new Vector2(.04f,.04f), new Vector2(.64f,.21f), 22);
            counter.color = new Color(.75f,.77f,.65f);
            var buttonObject = new GameObject("Continue", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            buttonObject.transform.SetParent(rect, false);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(.68f,.045f); buttonRect.anchorMax = new Vector2(.96f,.23f);
            buttonRect.offsetMin = buttonRect.offsetMax = Vector2.zero;
            buttonObject.GetComponent<UnityEngine.UI.Image>().color = new Color(.39f,.46f,.20f);
            var button = buttonObject.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = buttonObject.GetComponent<UnityEngine.UI.Image>();
            button.onClick.AddListener(advance);
            nextLabel = Label("Label", buttonRect, font, Vector2.zero, Vector2.one, 28);
            nextLabel.alignment = TextAlignmentOptions.Center;
            panelRect = rect; continueButton = button; root.SetActive(false);
        }

        private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, Vector2 min, Vector2 max, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font != null ? font : TMP_Settings.defaultFontAsset;
            text.fontSize = size; text.color = new Color(1,.96f,.83f); text.raycastTarget = false;
            text.rectTransform.anchorMin = min; text.rectTransform.anchorMax = max;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            return text;
        }
    }
}
