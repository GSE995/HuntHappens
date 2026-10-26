using UnityEngine;

namespace HuntHappens.Interaction
{
    public sealed class WorldInventoryItem : MonoBehaviour
    {
        private Collider[] playerColliders;
        private void FixedUpdate()
        {
            if (playerColliders == null) return;
            var own = GetComponent<Collider>();
            bool separated = true;
            foreach (var other in playerColliders)
                if (other != null && own.bounds.Intersects(other.bounds)) { separated = false; break; }
            if (!separated) return;
            foreach (var other in playerColliders) if (other != null) Physics.IgnoreCollision(own, other, false);
            playerColliders = null;
        }
        public InventoryEntry Entry { get; private set; }
        public PlayerInventory Owner { get; private set; }
        public string Prompt => Entry != null ? "E — поднять: " + Entry.Title : "";
        public void ConsumeOne()
        {
            if (Entry == null) return;
            if (--Entry.quantity <= 0) { Entry = null; gameObject.SetActive(false); Destroy(gameObject); }
        }
        public bool Take(PlayerInventory inventory)
        {
            if (Entry == null || inventory == null || !inventory.Receive(Entry)) return false;
            Entry = null; gameObject.SetActive(false); Destroy(gameObject); return true;
        }
        public static WorldInventoryItem Spawn(PlayerInventory owner, InventoryEntry entry, Vector3 position, Quaternion rotation)
        {
            var root = new GameObject(entry.Title);
            root.transform.SetPositionAndRotation(position, rotation);
            GameObject source = null;
            if (entry.kind == InventoryKind.Gun) source = owner.GetComponent<HuntHappens.Weapons.TranquilizerGun>()?.WeaponVisual?.gameObject;
            if (entry.kind == InventoryKind.Cage) source = owner.GetComponent<HuntHappens.SmallGame.RatHunting>()?.cagePrefab?.gameObject;
            if (entry.kind == InventoryKind.Trap) source = owner.GetComponent<HuntHappens.Traps.TrapPlacement>()?.Manager?.Prefab?.gameObject;
            Transform cageDoor = null;
            var cageTemplate = entry.kind == InventoryKind.Cage ? source?.GetComponent<HuntHappens.SmallGame.RatCage>() : null;
            if (cageTemplate != null)
            {
                cageDoor = new GameObject(cageTemplate.door.name).transform;
                cageDoor.SetParent(root.transform, false);
                cageDoor.SetLocalPositionAndRotation(cageTemplate.door.localPosition, cageTemplate.door.localRotation);
                cageDoor.localScale = cageTemplate.door.localScale;
            }
            // Copy only the render meshes: a dropped tool must not fire, catch animals or run prefab scripts.
            if (source != null)
                foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
                {
                    bool visible = true;
                    for (var node = filter.transform; node != source.transform; node = node.parent)
                        if (!node.gameObject.activeSelf) { visible = false; break; }
                    if (!visible) continue;
                    var renderer = filter.GetComponent<MeshRenderer>(); if (renderer == null) continue;
                    var mesh = new GameObject(filter.name, typeof(MeshFilter), typeof(MeshRenderer));
                    mesh.transform.SetParent(root.transform, false);
                    mesh.transform.localPosition = source.transform.InverseTransformPoint(filter.transform.position);
                    mesh.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * filter.transform.rotation;
                    mesh.transform.localScale = filter.transform.lossyScale;
                    if (cageDoor != null && filter.transform.IsChildOf(cageTemplate.door))
                        mesh.transform.SetParent(cageDoor, true);
                    mesh.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    mesh.GetComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                }
            if (root.transform.childCount == 0)
            {
                var visual = GameObject.CreatePrimitive(entry.kind == InventoryKind.Bread || entry.kind == InventoryKind.Chocolate ? PrimitiveType.Cube : PrimitiveType.Capsule);
                visual.transform.SetParent(root.transform, false); visual.transform.localScale = new Vector3(0.25f, 0.12f, 0.18f);
                if (entry.kind == InventoryKind.Chocolate) visual.GetComponent<Renderer>().material.color = new Color(.23f, .09f, .035f);
                Object.Destroy(visual.GetComponent<Collider>());
            }
            if (entry.kind == InventoryKind.Cage)
            {
                var cargo = root.AddComponent<HuntHappens.SmallGame.CageCargoVisual>();
                cargo.Configure(owner.GetComponent<HuntHappens.SmallGame.RatHunting>().cagePrefab);
                cargo.Bind(entry);
            }
            var bounds = new Bounds(Vector3.zero, Vector3.one * 0.1f);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var b = renderer.localBounds;
                for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                    bounds.Encapsulate(root.transform.InverseTransformPoint(renderer.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x,y,z)))));
            }
            var collider = root.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
            var body = root.AddComponent<Rigidbody>(); body.mass = 2; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            foreach (var playerCollider in owner.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(collider, playerCollider);
            var item = root.AddComponent<WorldInventoryItem>(); item.Entry = entry;
            item.Owner = owner;
            item.playerColliders = owner.GetComponentsInChildren<Collider>();
            return item;
        }
    }
}
