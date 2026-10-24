using HuntHappens.Interaction;
using HuntHappens.Player;
using UnityEngine;

namespace HuntHappens.Shop
{
    // Display stock stays on the shelf; purchases go into the player's saved inventory.
    public sealed class WorldShopItem : MonoBehaviour
    {
        [SerializeField] private ShopManager shop;
        [SerializeField, Range(0, 3)] private int kind;
        private string feedback;
        private float feedbackUntil;
        public int Kind => kind;
        public ShopManager Shop => shop;
        public string Title => kind == 0 ? "Клетка для крыс" : kind == 1 ? "Хлеб для приманки" : kind == 2 ? "Дротик с транквилизатором" : "Приём крыс";
        public int Price => kind == 0 ? 15 : kind == 1 ? 2 : kind == 2 ? 12 : 10;
        public string Prompt => (kind == 3 ? "Приём крыс: чёрная — $10, серая — $15, крупная — $25\nE — сдать пойманных крыс" : Title + " — $" + Price + "\nE — купить 1 шт.") +
            (Time.unscaledTime < feedbackUntil ? "\n" + feedback : "");
        public void Configure(ShopManager manager, int product) { shop = manager; kind = product; }
        public bool TryUse(PrototypeInput player)
        {
            if (shop == null || player == null || player.GameplayBlocked || player.GetComponent<ItemHolder>().IsHolding ||
                FindTarget(player.GetComponentInChildren<Camera>(), player.transform) != this) return false;
            bool result = shop.TryTradeWorld(this, player);
            feedback = shop.Message; feedbackUntil = Time.unscaledTime + 2;
            return result;
        }
        public static WorldShopItem FindTarget(Camera view, Transform player)
        {
            if (view == null) return null;
            var hits = Physics.RaycastAll(view.transform.position, view.transform.forward, 3f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(player)) continue;
                return hit.collider.GetComponentInParent<WorldShopItem>();
            }
            return null;
        }
    }
}
