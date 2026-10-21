using HuntHappens.Interaction;
using HuntHappens.Weapons;
using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.Traps
{
    [DisallowMultipleComponent, DefaultExecutionOrder(30)]
    public sealed class TrapPlacement : MonoBehaviour
    {
        [SerializeField] private Camera view;
        [SerializeField] private Collider ground;
        [SerializeField] private TrapManager manager;
        [SerializeField] private Material validPreview;
        [SerializeField] private Material invalidPreview;
        [SerializeField, Min(0.5f)] private float maximumDistance = 4f;
        [SerializeField, Range(0, 30)] private float maximumSlope = 15f;
        [SerializeField] private Vector3 clearanceHalfExtents = new Vector3(1.2f, 0.8f, 1.2f);
        [SerializeField] private LayerMask placementLayers = ~0;
        private TranquilizerGun tools;
        private ItemHolder holder;
        private PlayerInteraction interaction;
        private GameObject preview;
        private Renderer[] previewRenderers;
        public TrapManager Manager => manager;
        public bool PlacementValid { get; private set; }
        public string PlacementHint { get; private set; }
        public void Configure(Camera camera, Collider support, TrapManager owner, Material valid, Material invalid)
        { view = camera; ground = support; manager = owner; validPreview = valid; invalidPreview = invalid; }
        private void Awake()
        {
            tools = GetComponent<TranquilizerGun>();
            holder = GetComponent<ItemHolder>();
            interaction = GetComponent<PlayerInteraction>();
        }
        public bool Evaluate(out Vector3 position, out Quaternion rotation)
        {
            position = view != null ? view.transform.position + view.transform.forward * maximumDistance : transform.position;
            rotation = Quaternion.Euler(0, view != null ? view.transform.eulerAngles.y : transform.eulerAngles.y, 0);
            PlacementHint = "Наведите на свободную землю в пределах " + maximumDistance.ToString("0.#") + " м";
            if (view == null || ground == null || manager == null) return false;
            RaycastHit[] hits = Physics.RaycastAll(view.transform.position, view.transform.forward, maximumDistance,
                placementLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                position = hit.point + Vector3.up * 0.015f;
                if (hit.collider != ground || Vector3.Angle(hit.normal, Vector3.up) > maximumSlope) return false;
                // The full footprint must have support; a centre ray alone permits floating over an edge.
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = hit.point + rotation * new Vector3(x * clearanceHalfExtents.x, 0, z * clearanceHalfExtents.z);
                        if (!Physics.Raycast(corner + Vector3.up * 0.35f, Vector3.down, out RaycastHit support, 0.7f,
                            placementLayers, QueryTriggerInteraction.Ignore) || support.collider != ground || Mathf.Abs(support.point.y - hit.point.y) > 0.15f)
                            return false;
                    }
                if (!NavMesh.SamplePosition(hit.point, out NavMeshHit navigation, 0.35f, NavMesh.AllAreas) ||
                    Mathf.Abs(navigation.position.y - hit.point.y) > 0.2f) return false;
                foreach (Collider obstacle in Physics.OverlapBox(position + Vector3.up * (clearanceHalfExtents.y + 0.06f),
                    clearanceHalfExtents, rotation, placementLayers, QueryTriggerInteraction.Ignore))
                    if (obstacle != ground) { PlacementHint = "Недостаточно свободного места"; return false; }
                if (holder != null && holder.IsHolding) { PlacementHint = "Сначала отпустите предмет"; return false; }
                if (manager.PlacedCount >= manager.MaximumTraps) { PlacementHint = "Лимит ловушек. Заберите установленную клавишей E"; return false; }
                PlacementHint = "ЛКМ — Установить ловушку | E — Забрать пустую";
                return true;
            }
            return false;
        }
        public bool TryPlace()
        {
            if (tools == null || !tools.TrapSelected || holder.IsHolding || interaction.GameplayBlocked || interaction.LastHandledFrame == Time.frameCount) return false;
            if (!Evaluate(out Vector3 position, out Quaternion rotation)) return false;
            return manager.TryPlace(position, rotation) != null;
        }
        private void LateUpdate()
        {
            bool show = tools != null && tools.TrapSelected && !holder.IsHolding && !interaction.GameplayBlocked && Cursor.lockState == CursorLockMode.Locked;
            if (!show) { if (preview != null) preview.SetActive(false); return; }
            PlacementValid = Evaluate(out Vector3 position, out Quaternion rotation);
            if (preview == null && manager != null && manager.Prefab != null)
            {
                preview = Instantiate(manager.Prefab.gameObject);
                preview.name = "Trap Placement Preview";
                preview.GetComponent<AnimalTrap>().enabled = false;
                foreach (Collider collider in preview.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (Transform child in preview.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 2;
                previewRenderers = preview.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in previewRenderers) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (preview == null) return;
            preview.SetActive(true);
            preview.transform.SetPositionAndRotation(position, rotation);
            foreach (Renderer renderer in previewRenderers) renderer.sharedMaterial = PlacementValid ? validPreview : invalidPreview;
        }
        private void OnDisable() { if (preview != null) preview.SetActive(false); }
        private void OnDestroy() { if (preview != null) Destroy(preview); }
    }
}
