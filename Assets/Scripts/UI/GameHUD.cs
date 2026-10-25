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
        private TextMeshProUGUI notification, quest;
        private HuntHappens.Animals.DeerLeader leader;
        public void Configure(DeliveryManager value, TMP_FontAsset hudFont) { manager = value; font = hudFont; }

        private void Awake()
        {
            Canvas canvas = GetComponentInChildren<Canvas>();
            font = GetComponent<InteractionHUD>().ThemeFont;
            if (canvas == null) { Debug.LogError("Delivery HUD needs the existing Interaction Canvas.", this); enabled = false; return; }
            notification = Label("Delivery Notification", canvas.transform, Vector2.one * 0.5f, new Vector2(0, -150), new Vector2(950, 160));
            quest = Label("Deer Quest", canvas.transform, new Vector2(.5f, 1), new Vector2(0, -80), new Vector2(1100, 120));
            quest.alignment = TextAlignmentOptions.Center;
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
            if (manager == null || notification == null) return;
            if (leader == null) leader = FindAnyObjectByType<HuntHappens.Animals.DeerLeader>();
            quest.text = $"Возвращено оленей: {manager.DeliveredCount} / {manager.Goal}" + (manager.QuestComplete ? " — задание выполнено!" : "");
            if (leader != null && !leader.GetComponent<HuntHappens.Animals.DeerSedation>().IsDelivered && leader.GetComponent<HuntHappens.Animals.DeerSedation>().State == HuntHappens.Animals.DeerSedation.SedationState.Awake && Vector3.Distance(transform.position, leader.transform.position) < 30) quest.text += "\n" + leader.Status;
            if (GetComponent<PlayerInventory>()?.IsEquipped(InventoryKind.Chocolate) == true) quest.text += "\nЛКМ — бросить шоколад";
            notification.text = Time.time - manager.LastRewardTime < 3.5f ? manager.Notification : "";
        }
    }
}
