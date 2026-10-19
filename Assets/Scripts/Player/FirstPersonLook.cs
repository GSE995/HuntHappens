using UnityEngine;

namespace HuntHappens.Player
{
    public sealed class FirstPersonLook : MonoBehaviour
    {
        [SerializeField] private Transform cameraPivot;
        [SerializeField, Min(0.01f)] private float sensitivity = 0.12f;
        [SerializeField, Range(1f, 89f)] private float pitchLimit = 85f;
        private float pitch;

        public void SetCamera(Transform pivot) => cameraPivot = pivot;

        public void ApplyLook(Vector2 delta)
        {
            if (cameraPivot == null) return;
            // Mouse delta already represents displacement this frame: do not multiply by deltaTime.
            transform.Rotate(0f, delta.x * sensitivity, 0f);
            pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -pitchLimit, pitchLimit);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
