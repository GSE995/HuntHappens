using HuntHappens.Animals;
using HuntHappens.Interaction;
using UnityEngine;

namespace HuntHappens.Traps
{
    [DisallowMultipleComponent]
    public sealed class AnimalTrap : MonoBehaviour
    {
        public enum TrapState { Armed, Used }
        [SerializeField] private TrapState currentState;
        [SerializeField] private Transform net;
        [SerializeField] private Transform caughtMarker;
        [SerializeField, Min(0.1f)] private float animationDuration = 0.45f;
        private TrapManager manager;
        private float caughtTime;
        public TrapState State => currentState;
        public DeerSedation CaughtDeer { get; private set; }
        public string Prompt => CaughtDeer != null ? "E — Забрать оленя" : "E — Забрать ловушку";
        public void Configure(Transform netVisual, Transform marker) { net = netVisual; caughtMarker = marker; }
        public void Initialize(TrapManager owner) => manager = owner;

        private void OnTriggerEnter(Collider other) => TryCatch(other);
        private void OnTriggerStay(Collider other) => TryCatch(other);
        public bool TryCatch(Collider other)
        {
            if (!isActiveAndEnabled || currentState != TrapState.Armed || other == null) return false;
            DeerSedation deer = other.GetComponentInParent<DeerSedation>();
            if (deer == null || !deer.TryTrap(this)) return false;
            CaughtDeer = deer;
            currentState = TrapState.Used;
            caughtTime = Time.time;
            if (net != null) net.gameObject.SetActive(true);
            if (caughtMarker != null) caughtMarker.gameObject.SetActive(true);
            manager?.Notify("Олень попался в ловушку!");
            return true;
        }
        public bool TryInteract(ItemHolder holder)
        {
            if (holder == null || holder.IsHolding || !isActiveAndEnabled) return false;
            if (CaughtDeer != null) return holder.TryPickUp(CaughtDeer.GetComponent<PickupItem>());
            if (manager == null) return false;
            if (!holder.GetComponent<PlayerInventory>().Receive(new InventoryEntry { kind = InventoryKind.Trap })) return false;
            manager.Retrieve(this);
            return true;
        }
        public void Release(DeerSedation deer)
        {
            if (CaughtDeer != deer) return;
            CaughtDeer = null;
            if (net != null) net.gameObject.SetActive(false);
            if (caughtMarker != null) caughtMarker.gameObject.SetActive(false);
        }
        private void LateUpdate()
        {
            if (CaughtDeer == null || !CaughtDeer.isActiveAndEnabled)
            {
                if (net != null) net.gameObject.SetActive(false);
                if (caughtMarker != null) caughtMarker.gameObject.SetActive(false);
                return;
            }
            float blend = Mathf.SmoothStep(0, 1, (Time.time - caughtTime) / animationDuration);
            // Only the decorative net moves. Physics and NavMesh never animate the same transform.
            if (net != null)
            {
                net.position = CaughtDeer.transform.position;
                net.localScale = new Vector3(1, Mathf.Max(0.02f, blend), 1);
            }
            if (caughtMarker != null)
                caughtMarker.position = CaughtDeer.transform.position + Vector3.up * (2.5f + Mathf.Sin(Time.time * 4) * 0.08f);
        }
        private void OnDisable()
        {
            if (CaughtDeer != null) CaughtDeer.ReleaseTrapForCarry();
        }
    }
}
