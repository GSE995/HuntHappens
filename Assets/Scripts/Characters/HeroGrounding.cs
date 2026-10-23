using UnityEngine;

namespace HuntHappens.Characters
{
    [DefaultExecutionOrder(100)]
    public sealed class HeroGrounding : MonoBehaviour
    {
        private Transform visual;
        private SkinnedMeshRenderer skin;
        private Vector3[] vertices;
        private BoneWeight[] weights;
        private Matrix4x4[] bindposes;
        private Matrix4x4[] matrices;
        private Transform[] bones;
        private readonly RaycastHit[] hits = new RaycastHit[16];

        private void Awake()
        {
            visual = GetComponentInChildren<Animator>().transform;
            skin = GetComponentInChildren<SkinnedMeshRenderer>();
            vertices = skin.sharedMesh.vertices;
            weights = skin.sharedMesh.boneWeights;
            bindposes = skin.sharedMesh.bindposes;
            bones = skin.bones;
            matrices = new Matrix4x4[bones.Length];
        }

        // Use the same bone/bind-pose transforms as skinning, without applying model scale twice.
        public void Measure(out float bottom, out float top)
        {
            bottom = float.PositiveInfinity;
            top = float.NegativeInfinity;
            for (int i = 0; i < bones.Length; i++) matrices[i] = bones[i].localToWorldMatrix * bindposes[i];
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = weights[i];
                var p = vertices[i];
                Vector3 world = matrices[w.boneIndex0].MultiplyPoint3x4(p) * w.weight0 +
                    matrices[w.boneIndex1].MultiplyPoint3x4(p) * w.weight1 +
                    matrices[w.boneIndex2].MultiplyPoint3x4(p) * w.weight2 +
                    matrices[w.boneIndex3].MultiplyPoint3x4(p) * w.weight3;
                bottom = Mathf.Min(bottom, world.y);
                top = Mathf.Max(top, world.y);
            }
        }

        private void LateUpdate()
        {
            float floor = float.NegativeInfinity;
            int count = Physics.RaycastNonAlloc(transform.position + Vector3.up * 0.5f, Vector3.down, hits, 2f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!hits[i].transform.IsChildOf(transform)) floor = Mathf.Max(floor, hits[i].point.y);
            if (float.IsNegativeInfinity(floor)) return;
            Measure(out float bottom, out _);
            visual.position += Vector3.up * (floor - bottom);
        }
    }
}
