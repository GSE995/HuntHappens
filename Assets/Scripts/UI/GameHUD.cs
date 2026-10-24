using HuntHappens.Delivery;
using HuntHappens.Interaction;
using TMPro;
using UnityEngine;

namespace HuntHappens.UI
{
    [DefaultExecutionOrder(110), DisallowMultipleComponent, RequireComponent(typeof(InteractionHUD))]
    public sealed class GameHUD : MonoBehaviour
    {
        [SerializeField] private DeliveryManager manager;
        [SerializeField] private TMP_FontAsset font;
        private TextMeshProUGUI score;
        private TextMeshProUGUI notification;
        public void Configure(DeliveryManager value, TMP_FontAsset hudFont) { manager = value; font = hudFont; }

        private void Awake()
        {
            Canvas canvas = GetComponentInChildren<Canvas>();
            font = GetComponent<InteractionHUD>().ThemeFont;
            if (canvas == null) { Debug.LogError("Delivery HUD needs the existing Interaction Canvas.", this); enabled = false; return; }
            score = Label("Balance and Goal", canvas.transform, new Vector2(0, 1), new Vector2(25, -25), new Vector2(600, 70));
            score.alignment = TextAlignmentOptions.TopLeft;
            score.fontSize = 30;
            score.color = new Color(0.96f,0.92f,0.79f);
            notification = Label("Delivery Notification", canvas.transform, Vector2.one * 0.5f, new Vector2(0, -150), new Vector2(950, 160));
            notification.alignment = TextAlignmentOptions.Center;
            notification.color = new Color(1, 0.9f, 0.3f);
        }

        private TextMeshProUGUI Label(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var label = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);
            label.font = font;
            label.fontSize = 30;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = anchor;
            label.rectTransform.anchoredPosition = position;
            label.rectTransform.sizeDelta = size;
            return label;
        }

        private void LateUpdate()
        {
            if (manager == null || score == null) return;
            bool inventoryOpen = GetComponent<InteractionHUD>().Field != null && GetComponent<InteractionHUD>().Field.IsOpen;
            score.text = inventoryOpen ? "" : manager.QuestComplete ? "Олени доставлены!" : "Доставить оленей: " + Mathf.Min(manager.DeliveredCount, manager.Goal) + "/" + manager.Goal;
            notification.text = Time.time - manager.LastRewardTime < 3.5f ? manager.Notification : "";
        }
    }
}
