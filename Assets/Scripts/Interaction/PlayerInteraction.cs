using HuntHappens.Player;
using HuntHappens.Animals;
using HuntHappens.Delivery;
using UnityEngine;

namespace HuntHappens.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypeInput), typeof(ItemHolder), typeof(PlayerInventory))]
    public sealed class PlayerInteraction : MonoBehaviour
    {
        [SerializeField] private Camera view;
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;
        [SerializeField] private LayerMask interactionLayers = ~0;
        private PrototypeInput input;
        private ItemHolder holder;
        [SerializeField] private PickupTruckDelivery deliveryTruck;
        [SerializeField] private HuntHappens.Shop.ShopManager shop;
        public HuntHappens.Shop.ShopInteractable ShopTarget { get; private set; }
        public bool GameplayBlocked => input != null && input.GameplayBlocked;
        public HuntHappens.Characters.FarmerDialogue FarmerTarget { get; private set; }
        public void ConfigureShop(HuntHappens.Shop.ShopManager value) => shop = value;
        public bool TryOpenShop()
        {
            if (shop == null || !shop.TryOpen()) return false;
            LastHandledFrame = Time.frameCount;
            return true;
        }
        public PickupTruckDelivery DeliveryTarget { get; private set; }
        public void ConfigureDelivery(PickupTruckDelivery truck) => deliveryTruck = truck;
        public bool TrySubmitDelivery()
        {
            if (GameplayBlocked || holder == null || holder.IsHolding || view == null || deliveryTruck == null || !deliveryTruck.CanUse(transform, view.transform.position)) return false;
            LastHandledFrame = Time.frameCount;
            return deliveryTruck.Submit(transform, view.transform.position, holder) > 0;
        }
        public HuntHappens.SmallGame.RatCage CageTarget { get; private set; }
        public HuntHappens.SmallGame.RatAI RatTarget { get; private set; }
        public HuntHappens.SmallGame.BreadBasket BreadTarget { get; private set; }
        public HuntHappens.Shop.WorldShopItem ProductTarget { get; private set; }
        public HuntHappens.Transport.DeerCart CartTarget { get; private set; }
        public HuntHappens.Transport.DeerCart LoadingCart { get; private set; }
        public bool DeerInReach { get; private set; }
        public HuntHappens.Transport.CartDriver CartDriver { get; private set; }
        public string TransportHint => CartDriver != null && CartDriver.IsDriving
            ? CartDriver.Cart.CargoText + "\nWASD — Везти   E / Q — Отпустить"
            : CartTarget != null ? CartTarget.Prompt
            : DeerInReach && ObservedDeer != null && (ObservedDeer.State == DeerSedation.SedationState.Unconscious || ObservedDeer.State == DeerSedation.SedationState.Trapped)
                ? LoadingCart != null ? "E — Погрузить оленя в тележку" : "Олень слишком тяжёлый — подкатите пустую тележку"
                : "";
        public PickupItem Target { get; private set; }
        public WorldInventoryItem InventoryTarget { get; private set; }
        public HuntHappens.Traps.AnimalTrap TrapTarget { get; private set; }
        public DeerSedation ObservedDeer { get; private set; }
        public int LastHandledFrame { get; private set; } = -1;
        public bool IsHolding => holder != null && holder.IsHolding;
        public void SetView(Camera cameraComponent)
        {
            view = cameraComponent;
            GetComponent<ItemHolder>().SetView(view.transform);
        }

        private void Awake()
        {
            if (GetComponent<PlayerInventory>() == null) gameObject.AddComponent<PlayerInventory>();
            CartDriver = GetComponent<HuntHappens.Transport.CartDriver>();
            input = GetComponent<PrototypeInput>();
            holder = GetComponent<ItemHolder>();
            if (view == null) view = GetComponentInChildren<Camera>();
            if (view != null) holder.SetView(view.transform);
        }

        // The motor reads PrototypeInput in Update; consume the same frame after camera rotation.
        private void LateUpdate()
        {
            CartTarget = LoadingCart = null; DeerInReach = false;
            ProductTarget = null;
            InventoryTarget = null;
            CageTarget = null; BreadTarget = null;
            RatTarget = null;
            Target = null;
            ObservedDeer = null;
            DeliveryTarget = null;
            TrapTarget = null;
            ShopTarget = null;
            FarmerTarget = null;
            if (shop != null && shop.IsOpen)
            {
                if (input.MenuClosePressed) { LastHandledFrame = Time.frameCount; shop.Close(); }
                return;
            }
            if (GameplayBlocked || view == null || Cursor.lockState != CursorLockMode.Locked) return;
            if (CartDriver != null && CartDriver.IsDriving)
            {
                if (input.InteractPressed || input.DropPressed) { LastHandledFrame = Time.frameCount; CartDriver.Release(); }
                return;
            }
            if (holder.IsHolding)
            {
                if (input.ThrowPressed) { LastHandledFrame = Time.frameCount; holder.Throw(); }
                return;
            }
            ProductTarget = HuntHappens.Shop.WorldShopItem.FindTarget(view, transform);
            if (ProductTarget != null)
            {
                if (input.InteractPressed) { LastHandledFrame = Time.frameCount; ProductTarget.TryUse(input); }
                return;
            }
            if (shop != null && shop.Kiosk != null && shop.Kiosk.CanUse(transform, view.transform.position))
            {
                ShopTarget = shop.Kiosk;
                if (input.InteractPressed) TryOpenShop();
                return;
            }
            if (deliveryTruck != null && deliveryTruck.CanUse(transform, view.transform.position))
            {
                DeliveryTarget = deliveryTruck;
                if (input.InteractPressed) TrySubmitDelivery();
                return;
            }
            // First solid hit blocks selection: items cannot be picked up through trees or walls.
            RaycastHit[] hits = Physics.RaycastAll(view.transform.position, view.transform.forward,
                Mathf.Max(interactionDistance, 60f), interactionLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.distance <= interactionDistance)
                {
                    CartTarget = hit.collider.GetComponentInParent<HuntHappens.Transport.DeerCart>();
                    FarmerTarget = hit.collider.GetComponentInParent<HuntHappens.Characters.FarmerDialogue>();
                    InventoryTarget = hit.collider.GetComponentInParent<WorldInventoryItem>();
                    CageTarget = hit.collider.GetComponentInParent<HuntHappens.SmallGame.RatCage>();
                    BreadTarget = hit.collider.GetComponentInParent<HuntHappens.SmallGame.BreadBasket>();
                    var rat = hit.collider.GetComponentInParent<HuntHappens.SmallGame.RatAI>();
                    if (rat != null && rat.CanPickUp) RatTarget = rat;
                }
                PickupItem item = hit.rigidbody != null ? hit.rigidbody.GetComponent<PickupItem>() : null;
                ObservedDeer = hit.collider.GetComponentInParent<DeerSedation>();
                if (hit.distance <= interactionDistance)
                {
                    TrapTarget = hit.collider.GetComponentInParent<HuntHappens.Traps.AnimalTrap>();
                    if (ObservedDeer == null && TrapTarget != null) ObservedDeer = TrapTarget.CaughtDeer;
                    DeerInReach = ObservedDeer != null;
                    if (DeerInReach) LoadingCart = HuntHappens.Transport.DeerCart.FindFor(ObservedDeer, holder);
                }
                if (hit.distance <= interactionDistance && item != null && item.CanPickUp(holder.MaximumMass)) Target = item;
                break;
            }
            if (CartTarget != null)
            {
                if (input.InteractPressed) { LastHandledFrame = Time.frameCount; CartDriver?.TryTake(CartTarget); }
                else if (input.DropPressed) { LastHandledFrame = Time.frameCount; CartTarget.TryUnload(holder); }
            }
            else if (input.InteractPressed && LoadingCart != null)
            { LastHandledFrame = Time.frameCount; LoadingCart.TryLoad(ObservedDeer, holder); }
            else if (input.InteractPressed && FarmerTarget != null)
            {
                var hud = GetComponentInChildren<InteractionHUD>();
                if (hud == null) hud = Object.FindAnyObjectByType<InteractionHUD>();
                if (FarmerTarget.TryOpen(input, hud != null ? hud.ThemeFont : null)) LastHandledFrame = Time.frameCount;
            }
            else if (input.InteractPressed && InventoryTarget != null)
            { LastHandledFrame = Time.frameCount; InventoryTarget.Take(GetComponent<PlayerInventory>()); }
            else if (input.InteractPressed && RatTarget != null)
            { LastHandledFrame = Time.frameCount; GetComponent<HuntHappens.SmallGame.RatHunting>()?.TryCollectSleepingRat(RatTarget); }
            else if (input.InteractPressed && CageTarget != null)
            { LastHandledFrame = Time.frameCount; CageTarget.Interact(GetComponent<HuntHappens.SmallGame.RatHunting>()); }
            else if (input.InteractPressed && BreadTarget != null)
            { LastHandledFrame = Time.frameCount; BreadTarget.Take(GetComponent<HuntHappens.SmallGame.RatHunting>()); }
            else if (input.InteractPressed && TrapTarget != null)
            {
                LastHandledFrame = Time.frameCount;
                TrapTarget.TryInteract(holder);
            }
            else if (input.InteractPressed && Target != null)
            {
                LastHandledFrame = Time.frameCount;
                holder.TryPickUp(Target);
            }
        }

        private void OnDisable()
        {
            CartDriver?.Release();
            CartTarget = LoadingCart = null; DeerInReach = false;
            FarmerTarget = null;
            ProductTarget = null;
            CageTarget = null; BreadTarget = null;
            RatTarget = null;
            Target = null;
            ObservedDeer = null;
            DeliveryTarget = null;
            if (holder != null) holder.Drop();
            if (shop != null) shop.Close();
            ShopTarget = null;
            TrapTarget = null;
        }
    }
}
