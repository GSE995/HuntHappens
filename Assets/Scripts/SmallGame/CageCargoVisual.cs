using System.Collections.Generic;
using System.Linq;
using HuntHappens.Interaction;
using UnityEngine;
using UnityEngine.Rendering;

namespace HuntHappens.SmallGame
{
    // Render-only snapshot: no animal AI, physics, capture logic or additional catch.
    public sealed class CageCargoVisual : MonoBehaviour
    {
        private Transform door;
        private GameObject bait;
        private RatCage placedCage;
        private Vector3 openDoorPosition;
        private InventoryEntry entry;
        private GameObject rat;
        private int shownKind = -1;
        private readonly List<Mesh> bakedMeshes = new List<Mesh>();

        public void Configure(RatCage template)
        {
            door = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == template.door.name);
            if (door != null) openDoorPosition = door.localPosition;
            bait = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == template.bait.name)?.gameObject;
            placedCage = GetComponent<RatCage>();
        }

        public void Bind(InventoryEntry value)
        {
            entry = value;
            Refresh();
        }

        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            int kind = entry?.kind == InventoryKind.Cage ? Mathf.Clamp(entry.cageRat, 0, 3) : 0;
            if (bait != null) bait.SetActive(placedCage != null ? placedCage.Baited : kind == 0 && entry?.kind == InventoryKind.Cage && entry.cageBaited);
            if (kind == shownKind) return;
            shownKind = kind;
            if (door != null) door.localPosition = kind > 0 ? new Vector3(0, .25f, .44f) : openDoorPosition;
            ClearRat();
            if (kind == 0) return;
            var source = FindObjectsByType<RatAI>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(r => r.Kind == kind - 1);
            if (source == null || source.visual == null) { shownKind = -1; return; }
            rat = new GameObject("Caged Rat");
            rat.transform.SetParent(transform, false);
            rat.layer = gameObject.layer;
            foreach (var renderer in source.visual.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                bool visible = true;
                for (var node = renderer.transform; node != source.visual; node = node.parent)
                    if (!node.gameObject.activeSelf) { visible = false; break; }
                if (!visible) continue;
                Mesh mesh;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    mesh = new Mesh { name = "Caged Rat Snapshot" };
                    skin.BakeMesh(mesh, true);
                    bakedMeshes.Add(mesh);
                }
                else
                {
                    mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) continue;
                }
                var part = new GameObject(renderer.name, typeof(MeshFilter), typeof(MeshRenderer));
                part.layer = gameObject.layer;
                part.transform.SetParent(rat.transform, false);
                part.transform.localPosition = source.visual.InverseTransformPoint(renderer.transform.position);
                part.transform.localRotation = Quaternion.Inverse(source.visual.rotation) * renderer.transform.rotation;
                Vector3 scale = source.visual.lossyScale;
                part.transform.localScale = Vector3.Scale(renderer.transform.lossyScale, new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z));
                part.GetComponent<MeshFilter>().sharedMesh = mesh;
                var copy = part.GetComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.shadowCastingMode = ShadowCastingMode.Off;
                copy.receiveShadows = false;
            }
            // Fit all species, including the larger brown rat, inside the existing cage.
            var bounds = new Bounds();
            bool initialized = false;
            foreach (var filter in rat.GetComponentsInChildren<MeshFilter>())
            {
                Bounds b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = rat.transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!initialized) { bounds = new Bounds(p, Vector3.zero); initialized = true; }
                    else bounds.Encapsulate(p);
                }
            }
            if (!initialized) return;
            float naturalScale = source.visual.lossyScale.x / Mathf.Max(.001f, transform.lossyScale.x);
            float fit = Mathf.Min(naturalScale, Mathf.Min(.46f / Mathf.Max(.001f, bounds.size.x), .34f / Mathf.Max(.001f, bounds.size.y), .72f / Mathf.Max(.001f, bounds.size.z)));
            rat.transform.localScale = Vector3.one * fit;
            rat.transform.localPosition = new Vector3(0, .055f, -.03f) - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * fit;
        }

        private void ClearRat()
        {
            if (rat != null) { rat.SetActive(false); Destroy(rat); rat = null; }
            foreach (var mesh in bakedMeshes) if (mesh != null) Destroy(mesh);
            bakedMeshes.Clear();
        }

        private void OnDestroy() => ClearRat();
    }
}
