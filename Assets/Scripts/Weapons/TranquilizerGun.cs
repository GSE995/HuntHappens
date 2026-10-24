using HuntHappens.Interaction;
using HuntHappens.Player;
using UnityEngine;

namespace HuntHappens.Weapons
{
    [DefaultExecutionOrder(20), DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypeInput), typeof(ItemHolder), typeof(PlayerInteraction))]
    public sealed class TranquilizerGun : MonoBehaviour
    {
        [SerializeField] private Camera view;
        [SerializeField] private Transform weaponVisual;
        [SerializeField] private Transform muzzle;
        [SerializeField] private GameObject flash;
        [SerializeField] private TranquilizerDart dartPrefab;
        [SerializeField] private Vector3 viewOffset = new Vector3(0.06f, -0.10f, 0.12f);
        [SerializeField, Min(1)] private float dartSpeed = 38f;
        [SerializeField, Min(0.05f)] private float shotInterval = 0.7f;
        [SerializeField, Min(0.1f)] private float dartLifetime = 10f;
        [SerializeField, Min(0.01f)] private float tranquilizerStrength = 1f;
        private PrototypeInput input;
        private ItemHolder holder;
        private PlayerInteraction interaction;
        private FirstPersonLook look;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private float nextShot;
        private float recoil;
        private float flashUntil;
        public bool Selected => GetComponent<PlayerInventory>()?.IsEquipped(InventoryKind.Gun) == true;
        public bool TrapSelected => GetComponent<PlayerInventory>()?.IsEquipped(InventoryKind.Trap) == true;
        public TranquilizerDart LastDart { get; private set; }
        public Transform WeaponVisual => weaponVisual;
        public event System.Action Fired;
        public float SedationDurationMultiplier { get; private set; } = 1;
        public void SetSedationDurationMultiplier(float value) => SedationDurationMultiplier = Mathf.Clamp(value, 0.25f, 1);

        public void Configure(Camera camera, Transform visual, Transform barrelEnd, GameObject muzzleFlash, TranquilizerDart projectile)
        { view = camera; weaponVisual = visual; muzzle = barrelEnd; flash = muzzleFlash; dartPrefab = projectile; }
        public void SelectTool(bool tranquilizer) { SelectOwned(tranquilizer ? InventoryKind.Gun : (InventoryKind?)null); }
        public void SelectTrapTool() { SelectOwned(InventoryKind.Trap); }
        private void SelectOwned(InventoryKind? kind)
        {
            var inventory = GetComponent<PlayerInventory>(); if (inventory == null) return;
            if (kind == null) { if (inventory.HandSlot >= 0) inventory.SelectSlot(inventory.HandSlot); return; }
            if (inventory.IsEquipped(kind.Value)) return;
            for (int i = 0; i < PlayerInventory.Capacity; i++)
                if (inventory.Slot(i)?.kind == kind) { inventory.SelectSlot(i); return; }
        }

        private void Awake()
        {
            input = GetComponent<PrototypeInput>();
            holder = GetComponent<ItemHolder>();
            interaction = GetComponent<PlayerInteraction>();
            look = GetComponent<FirstPersonLook>();
            if (weaponVisual != null)
            {
                restPosition = weaponVisual.localPosition + viewOffset;
                restRotation = weaponVisual.localRotation;
                weaponVisual.localPosition = restPosition;
            }
        }

        private void LateUpdate()
        {
            if (input.ThrowPressed)
            {
                if (TrapSelected) GetComponent<HuntHappens.Traps.TrapPlacement>()?.TryPlace();
                else TryFire();
            }
            bool visible = Selected && !holder.IsHolding && !input.GameplayBlocked;
            if (weaponVisual != null)
            {
                weaponVisual.gameObject.SetActive(visible);
                float walking = input.Move.magnitude;
                Vector3 sway = new Vector3(Mathf.Sin(Time.time * 7f), Mathf.Cos(Time.time * 14f), 0) * (0.008f * walking);
                weaponVisual.localPosition = restPosition + sway - Vector3.forward * (recoil * 0.065f);
                weaponVisual.localRotation = restRotation * Quaternion.Euler(-recoil * 8f, 0, 0);
            }
            if (flash != null) flash.SetActive(visible && Time.time < flashUntil);
            recoil *= Mathf.Exp(-14f * Time.deltaTime);
        }

        public bool TryFire()
        {
            if (!isActiveAndEnabled || input.GameplayBlocked || !Selected || holder.IsHolding || interaction.LastHandledFrame == Time.frameCount ||
                Time.time < nextShot || dartPrefab == null || view == null || muzzle == null) return false;
            Vector3 origin = muzzle.position;
            Vector3 target = view.transform.position + view.transform.forward * 80f;
            RaycastHit[] hits = Physics.RaycastAll(view.transform.position, view.transform.forward, 80f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<TranquilizerDart>() != null) continue;
                target = hit.point;
                break;
            }
            // Do not spawn the dart beyond a wall when the visible weapon intersects it.
            Vector3 toMuzzle = origin - view.transform.position;
            foreach (RaycastHit hit in Physics.RaycastAll(view.transform.position, toMuzzle.normalized, toMuzzle.magnitude + 0.05f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<TranquilizerDart>() == null)
                { origin = view.transform.position; break; }
            if ((target - origin).sqrMagnitude < 0.0001f) return false;
            var supplies = GetComponent<HuntHappens.SmallGame.RatHunting>();
            if (supplies != null && !supplies.UseDart()) return false;
            Vector3 direction = (target - origin).normalized;
            LastDart = Instantiate(dartPrefab, origin, Quaternion.LookRotation(direction));
            LastDart.Launch(transform, direction * dartSpeed, tranquilizerStrength, dartLifetime, SedationDurationMultiplier);
            nextShot = Time.time + shotInterval;
            recoil = 1;
            flashUntil = Time.time + 0.055f;
            if (look != null) look.AddRecoil(1.2f);
            Fired?.Invoke();
            return true;
        }

        private void OnDisable()
        {
            if (weaponVisual != null) weaponVisual.gameObject.SetActive(false);
            if (flash != null) flash.SetActive(false);
        }
    }
}
