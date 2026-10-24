using HuntHappens.Player;
using HuntHappens.Interaction;
using HuntHappens.Shop;
using HuntHappens.Weapons;
using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.SmallGame
{
    [DefaultExecutionOrder(40)]
    public sealed class RatHunting : MonoBehaviour
    {
        public RatCage cagePrefab;
        public Collider ground;
        public ShopManager shop;
        private PrototypeInput input;
        private ItemHolder holder;
        private Camera view;
        private TranquilizerGun gun;
        public int Cages { get; private set; }
        public int Bread { get; private set; }
        public int Rats { get; private set; }
        public int GreyRats { get; private set; }
        public int SaleTotal => (Rats - GreyRats) * 10 + GreyRats * 15;
        public int Darts { get; private set; }
        public int Deployed { get; private set; }
        public int Available => Cages - Deployed;
        public bool Selected { get; private set; }
        public string Hint { get; private set; } = "";
        public string Stock => "Клетки: " + Available + " | Хлеб: " + Bread + " | Крысы: " + (Rats - GreyRats) + " чёрн., " + GreyRats + " сер." + " | Дротики: " + Darts;
        public void Reload()
        {
            var data = ShopProgress.Load();
            Cages = Mathf.Clamp(data.cages, 1, 20); Bread = Mathf.Clamp(data.bread, 0, 999);
            Rats = Mathf.Clamp(data.rats, 0, 999); Darts = Mathf.Clamp(data.darts, 0, 999);
            GreyRats = Mathf.Clamp(data.greyRats, 0, Rats);
        }
        private void Start()
        {
            input = GetComponent<PrototypeInput>(); holder = GetComponent<ItemHolder>();
            view = GetComponentInChildren<Camera>(); gun = GetComponent<TranquilizerGun>(); Reload();
        }
        private void Save() => ShopProgress.SaveSupplies(Cages, Bread, Rats, Darts, GreyRats);
        public bool Buy(int kind)
        {
            if (shop == null || !shop.CanTradeSupplies) return false;
            int price = kind == 0 ? 15 : kind == 1 ? 2 : 12;
            if (kind < 0 || kind > 2 || (kind == 0 ? Cages >= 20 : kind == 1 ? Bread >= 999 : Darts >= 999)) return false;
            if (!shop.Wallet.TrySpend(price)) return false;
            if (kind == 0) Cages++; else if (kind == 1) Bread++; else Darts++;
            Save(); return true;
        }
        public bool Sell()
        {
            if (shop == null || !shop.CanTradeSupplies || Rats == 0) return false;
            int reward = SaleTotal; Rats = 0; GreyRats = 0; Save();
            shop.Wallet.RestoreBalance((int)System.Math.Min(int.MaxValue, (long)shop.Wallet.Balance + reward));
            return true;
        }
        public bool UseBread() { if (Bread <= 0) return false; Bread--; Save(); return true; }
        public bool UseDart() { if (Darts <= 0) return false; Darts--; Save(); return true; }
        public bool CollectBread() { if (Bread >= 999) return false; Bread++; Save(); return true; }
        public void Recover(bool caught, bool grey = false) { Deployed = Mathf.Max(0, Deployed - 1); if (caught) { Rats++; if (grey) GreyRats++; } Save(); }
        public bool Evaluate(out Vector3 point)
        {
            point = default;
            if (view == null || ground == null || holder.IsHolding || Available <= 0) return false;
            var hits = Physics.RaycastAll(view.transform.position, view.transform.forward, 4f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.collider != ground || hit.normal.y < 0.95f) return false;
                point = hit.point;
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                        if (!ground.Raycast(new Ray(point + new Vector3(x * 0.36f, 0.3f, z * 0.5f), Vector3.down), out var support, 0.5f) || Mathf.Abs(support.point.y - point.y) > 0.05f) return false;
                if (!NavMesh.SamplePosition(point, out var nav, 0.3f, NavMesh.AllAreas)) return false;
                foreach (var obstacle in Physics.OverlapBox(point + Vector3.up * 0.25f, new Vector3(0.36f, 0.23f, 0.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    if (obstacle != ground) return false;
                return true;
            }
            return false;
        }
        public void SelectCageTool() { Selected = true; gun.SelectTool(false); }
        public RatCage TryPlace()
        {
            if (!Selected || input.GameplayBlocked || !Evaluate(out var point)) return null;
            var cage = Instantiate(cagePrefab, point, Quaternion.identity); cage.Owner = this; Deployed++; return cage;
        }
        private void LateUpdate()
        {
            if (input.SelectCagePressed) SelectCageTool();
            if (input.SelectTranquilizerPressed || input.SelectTrapPressed || input.SelectInteractionPressed) Selected = false;
            Hint = Available <= 0 ? "Все клетки установлены — заберите их клавишей E" : Evaluate(out _) ? "ЛКМ — поставить клетку. Затем E — положить хлеб" : "Наведите на свободную землю рядом с собой";
            if (Selected && input.ThrowPressed && GetComponent<PlayerInteraction>().LastHandledFrame != Time.frameCount) TryPlace();
        }
    }
}
