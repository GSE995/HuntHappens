using System.Collections.Generic;
using HuntHappens.Animals;
using HuntHappens.Interaction;
using UnityEngine;

namespace HuntHappens.Transport
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class DeerCart : MonoBehaviour
    {
        public const int Price = 200;
        public bool IsPurchased { get; private set; }
        public void ReloadOwnership() => IsPurchased = HuntHappens.Shop.ShopProgress.Load().cartPurchased;
        [SerializeField, Min(1)] private float capacityKg = 300;
        [SerializeField, Min(.5f)] private float emptySpeed = 5.5f;
        [SerializeField, Min(.5f)] private float loadedSpeed = 4f;
        [SerializeField, Min(1)] private float loadingDistance = 4.2f;
        [SerializeField] private float handleDistance = 2.8f;
        [SerializeField] private float floorHeight = .72f;
        [SerializeField] private Transform[] wheels;
        private BoxCollider footprint;
        private Rigidbody body;
        private Transform previousParent;
        private readonly List<Collider> cargoColliders = new List<Collider>();
        private RigidbodyConstraints cargoConstraints;
        private RigidbodyInterpolation cargoInterpolation;
        private CollisionDetectionMode cargoCollisionMode;
        public DeerSedation Cargo { get; private set; }
        public ItemHolder LoadedBy { get; private set; }
        public CartDriver Driver { get; private set; }
        public float TravelSpeed => Cargo == null ? emptySpeed : loadedSpeed;
        public float Speed { get; private set; }
        public float LastMovedAt { get; private set; } = float.NegativeInfinity;
        public Vector3 HandlePosition => transform.position - transform.forward * handleDistance;
        public string CargoText => Cargo == null ? "Тележка — пусто (0/1)" : "Тележка — олень " + Cargo.GetComponent<Rigidbody>().mass.ToString("0") + " кг (1/1)";
        public string Notice { get; private set; }
        private float noticeUntil;
        public void ShowNotice(string text) { Notice = text; noticeUntil = Time.unscaledTime + 2; }
        public string Prompt => Time.unscaledTime < noticeUntil ? Notice : !IsPurchased
            ? "Тележка — $" + Price + "\nE — Купить за $" + Price
            : CargoText + "\nE — Везти" + (Cargo != null ? "   Q — Выгрузить" : "");
        public void ConfigureWheels(Transform[] value) => wheels = value;
        public void SetDriver(CartDriver value) { Driver = value; Speed = 0; }

        private void Awake()
        {
            ReloadOwnership();
            footprint = GetComponent<BoxCollider>(); body = GetComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
        }

        public bool TryPurchase(ItemHolder player, HuntHappens.Delivery.DeliveryManager wallet)
        {
            ReloadOwnership();
            if (!isActiveAndEnabled || IsPurchased || player == null || wallet == null ||
                player.GetComponent<HuntHappens.Player.PrototypeInput>()?.GameplayBlocked == true ||
                Vector3.Distance(player.transform.position, transform.position) > 5) return false;
            if (!wallet.TrySpend(Price)) { ShowNotice("Недостаточно денег — нужно $" + Price); return false; }
            var data = HuntHappens.Shop.ShopProgress.Load();
            data.cartPurchased = true;
            data.balance = wallet.Balance;
            PlayerPrefs.SetString(HuntHappens.Shop.ShopProgress.Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            foreach (var cart in FindObjectsByType<DeerCart>(FindObjectsSortMode.None)) cart.ReloadOwnership();
            ShowNotice("Тележка куплена за $" + Price);
            return true;
        }

        public bool Owns(Collider collider) => collider.transform.IsChildOf(transform);
        private bool Ignore(Collider collider) => Owns(collider) ||
            (Driver != null && collider.transform.IsChildOf(Driver.transform));

        public bool CanLoad(DeerSedation deer, ItemHolder player)
        {
            if (!isActiveAndEnabled || !IsPurchased || Cargo != null || Driver != null || deer == null || player == null ||
                !deer.isActiveAndEnabled || deer.IsDelivered || deer.Cart != null || deer.DeliveryReserved ||
                player.GetComponent<HuntHappens.Player.PrototypeInput>()?.GameplayBlocked == true ||
                (deer.State != DeerSedation.SedationState.Unconscious && deer.State != DeerSedation.SedationState.Trapped)) return false;
            var pickup = deer.GetComponent<PickupItem>();
            var deerBody = deer.GetComponent<Rigidbody>();
            if (pickup == null || pickup.Holder != null || deerBody.mass > capacityKg ||
                Vector3.Distance(deerBody.worldCenterOfMass, transform.position + Vector3.up) > loadingDistance ||
                Vector3.Distance(player.transform.position, deerBody.worldCenterOfMass) > 4.5f) return false;
            Vector3 from = deerBody.worldCenterOfMass;
            Vector3 delta = transform.position + Vector3.up * 1.3f - from;
            foreach (var hit in Physics.RaycastAll(from, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!Owns(hit.collider) && !hit.collider.transform.IsChildOf(deer.transform) &&
                    !hit.collider.transform.IsChildOf(player.transform)) return false;
            return true;
        }

        public static DeerCart FindFor(DeerSedation deer, ItemHolder player)
        {
            DeerCart best = null; float distance = float.PositiveInfinity;
            foreach (var cart in FindObjectsByType<DeerCart>(FindObjectsSortMode.None))
            {
                if (!cart.CanLoad(deer, player)) continue;
                float d = (cart.transform.position - deer.transform.position).sqrMagnitude;
                if (d < distance) { best = cart; distance = d; }
            }
            return best;
        }

        public bool TryLoad(DeerSedation deer, ItemHolder player)
        {
            if (!CanLoad(deer, player) || !player.GetComponent<PlayerInventory>().TryStowForCart()) return false;
            deer.ReleaseTrapForCarry();
            Cargo = deer; LoadedBy = player; deer.Cart = this; LastMovedAt = Time.time;
            previousParent = deer.transform.parent;
            deer.ReserveForDelivery(this);
            var cargoBody = deer.GetComponent<Rigidbody>();
            cargoConstraints = cargoBody.constraints;
            cargoInterpolation = cargoBody.interpolation;
            cargoCollisionMode = cargoBody.collisionDetectionMode;
            cargoBody.linearVelocity = cargoBody.angularVelocity = Vector3.zero;
            cargoBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            cargoBody.interpolation = RigidbodyInterpolation.None;
            cargoBody.isKinematic = true; cargoBody.useGravity = false;
            cargoColliders.Clear();
            foreach (var collider in deer.GetComponentsInChildren<Collider>())
                if (collider.enabled) { cargoColliders.Add(collider); collider.enabled = false; }
            deer.transform.SetParent(transform, true);
            Quaternion rotation = Quaternion.Euler(0,90,0);
            var box = deer.GetComponent<BoxCollider>();
            Vector3 scaledCenter = Vector3.Scale(box.center, deer.transform.localScale);
            float halfHeight = box.size.y * deer.transform.localScale.y * .5f;
            deer.transform.localRotation = rotation;
            deer.transform.localPosition = new Vector3(0, floorHeight + halfHeight + .03f, 0) - rotation * scaledCenter;
            foreach (var leader in FindObjectsByType<DeerLeader>(FindObjectsSortMode.None)) leader.NoticeAttack(deer);
            Physics.SyncTransforms();
            return true;
        }

        public bool TryUnload(ItemHolder player)
        {
            if (Cargo == null || Driver != null || player == null ||
                player.GetComponent<HuntHappens.Player.PrototypeInput>()?.GameplayBlocked == true ||
                Vector3.Distance(player.transform.position, transform.position) > 5) return false;
            var box = Cargo.GetComponent<BoxCollider>();
            Vector3 half = Vector3.Scale(box.size * .5f, Cargo.transform.lossyScale);
            Quaternion rotation = transform.rotation * Quaternion.Euler(0,90,0);
            foreach (Vector3 offset in new[] {transform.right * 3.3f, -transform.right * 3.3f, transform.forward * 4})
            {
                Vector3 candidate = transform.position + offset;
                if (!Ground(candidate, out var floor)) continue;
                Vector3 center = new Vector3(candidate.x, floor + half.y + .08f, candidate.z);
                bool blocked = false;
                foreach (var obstacle in Physics.OverlapBox(center, half, rotation, ~0, QueryTriggerInteraction.Ignore))
                    if (!obstacle.transform.IsChildOf(Cargo.transform)) { blocked = true; break; }
                if (blocked) continue;
                Vector3 position = center - rotation * Vector3.Scale(box.center, Cargo.transform.lossyScale);
                DetachCargo(position, rotation, false);
                return true;
            }
            ShowNotice("Нет свободного места для выгрузки");
            return false;
        }

        public void ReleaseDelivered(DeerSedation deer)
        {
            if (Cargo != deer) return;
            DetachCargo(deer.transform.position, deer.transform.rotation, true);
        }

        private void DetachCargo(Vector3 position, Quaternion rotation, bool delivered)
        {
            var deer = Cargo;
            if (deer == null) { Cargo = null; cargoColliders.Clear(); return; }
            Cargo = null; LoadedBy = null;
            deer.transform.SetParent(previousParent, true);
            deer.transform.SetPositionAndRotation(position, rotation);
            deer.Cart = null; deer.ReleaseDeliveryReservation(this);
            var cargoBody = deer.GetComponent<Rigidbody>();
            cargoBody.constraints = cargoConstraints;
            cargoBody.interpolation = cargoInterpolation;
            cargoBody.isKinematic = delivered;
            cargoBody.useGravity = !delivered;
            cargoBody.collisionDetectionMode = delivered ? CollisionDetectionMode.Discrete : cargoCollisionMode;
            foreach (var collider in cargoColliders) if (collider != null) collider.enabled = !delivered;
            cargoColliders.Clear();
            if (!delivered)
            {
                cargoBody.linearVelocity = cargoBody.angularVelocity = Vector3.zero;
                deer.RestartSleepTimer();
                cargoBody.WakeUp();
            }
            Physics.SyncTransforms();
        }

        private bool Ground(Vector3 point, out float height)
        {
            height = 0;
            var hits = Physics.RaycastAll(point + Vector3.up * .65f, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (Ignore(hit.collider)) continue;
                if (hit.normal.y < .9f || hit.collider.attachedRigidbody != null) return false;
                height = hit.point.y;
                return true;
            }
            return false;
        }

        // Conservative swept bounds stop the whole cart at fences/trees; cargo never gets teleported through them.
        public bool TryMove(Vector3 delta, Quaternion heading)
        {
            Speed = 0;
            if (Driver == null || delta.magnitude > .5f) return false;
            Vector3 next = transform.position + delta;
            float floor = float.NegativeInfinity;
            for (int x=-1;x<=1;x+=2) for (int z=-1;z<=1;z+=2)
            {
                if (!Ground(next + heading * new Vector3(x*1.15f,0,z*1.25f), out float h) ||
                    Mathf.Abs(h - transform.position.y) > .35f) return false;
                floor = Mathf.Max(floor,h);
            }
            next.y = floor + .03f;
            Vector3 half = footprint.size * .5f;
            // Include the sleeping animal above the rails in movement clearance.
            if (Cargo != null) half.y += .45f;
            Vector3 center = transform.position + transform.rotation * footprint.center;
            if (Cargo != null) center.y += .45f;
            foreach (var hit in Physics.BoxCastAll(center, half, delta.normalized, transform.rotation,
                         delta.magnitude + .03f, ~0, QueryTriggerInteraction.Ignore))
                if (!Ignore(hit.collider) && hit.normal.y < .7f) return false;
            Vector3 nextCenter = next + heading * footprint.center;
            if (Cargo != null) nextCenter.y += .45f;
            foreach (var obstacle in Physics.OverlapBox(nextCenter, half * .98f, heading, ~0, QueryTriggerInteraction.Ignore))
                if (!Ignore(obstacle)) return false;
            Vector3 handle = next - heading * Vector3.forward * handleDistance;
            if (!Driver.CanMove(Vector3.ProjectOnPlane(handle - Driver.transform.position, Vector3.up), this)) return false;
            Vector3 old = transform.position;
            float angle = Quaternion.Angle(transform.rotation, heading);
            transform.SetPositionAndRotation(next, heading);
            Physics.SyncTransforms();
            float travel = Vector3.ProjectOnPlane(next-old,Vector3.up).magnitude;
            Speed = travel / Mathf.Max(Time.deltaTime,.001f);
            if (travel > .0001f || angle > .05f) LastMovedAt = Time.time;
            if (wheels != null)
                foreach (var wheel in wheels) if (wheel != null)
                    wheel.Rotate(Vector3.right, Vector3.Dot(next-old,transform.forward)/.44f*Mathf.Rad2Deg, Space.Self);
            return true;
        }

        private void OnDisable()
        {
            Driver?.Release();
            if (Cargo != null) DetachCargo(Cargo.transform.position, Cargo.transform.rotation, false);
        }
    }
}
