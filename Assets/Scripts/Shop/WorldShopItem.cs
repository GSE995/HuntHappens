using HuntHappens.Interaction;
using HuntHappens.Player;
using UnityEngine;

namespace HuntHappens.Shop
{
    // Display stock stays on the shelf; purchases go into the player's saved inventory.
    public sealed class WorldShopItem : MonoBehaviour
    {
        [SerializeField] private ShopManager shop;
        [SerializeField, Range(0, 8)] private int kind;
        private string feedback;
        private float feedbackUntil;
        public int Kind => kind;
        public ShopManager Shop => shop;
        public string Title => kind == 7 ? "Бутерброд с сыром" : kind == 8 ? "Латте" : kind == 6 ? "Шоколад для вожака" : kind == 0 ? "Клетка для крыс" : kind == 1 ? "Хлеб для приманки" : kind == 2 ? "Дротик с транквилизатором" : kind == 4 ? "Транквилизатор" : kind == 5 ? "Ловушка" : "Приём крыс";
        public static int PriceFor(int kind) => kind == 7 ? 3 : kind == 8 ? 5 : kind == 6 ? ShopManager.DefaultChocolatePrice : kind == 0 ? 15 : kind == 1 ? 2 : kind == 2 ? 12 : kind == 4 ? 50 : kind == 5 ? 25 : 10;
        public int Price => kind == 6 && shop != null ? shop.ChocolatePrice : PriceFor(kind);
        public string Prompt => (kind == 3 ? "Крыс покупает только Семёныч" : Title + " — $" + Price + "\nE — купить 1 шт.") +
            (kind == 1 || kind == 7 || kind == 8 ? "\n" + HuntHappens.SmallGame.RatBait.Description(kind == 1 ? InventoryKind.Bread : kind == 7 ? InventoryKind.CheeseSandwich : InventoryKind.Latte) : "") +
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
