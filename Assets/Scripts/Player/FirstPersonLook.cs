using UnityEngine;

namespace HuntHappens.Player
{
    public sealed class FirstPersonLook : MonoBehaviour
    {
        [SerializeField] private Transform cameraPivot;
        [SerializeField, Min(0.01f)] private float sensitivity = 0.12f;
        [SerializeField, Range(1f, 89f)] private float pitchLimit = 85f;
        [SerializeField, Range(1f, 89f)] private float downwardPitchLimit = 55f;
        private float pitch;
        private float recoil;
        public void AddRecoil(float degrees) => recoil = Mathf.Clamp(recoil + degrees, 0, 4f);

        public void SetCamera(Transform pivot) => cameraPivot = pivot;

        public void ApplyLook(Vector2 delta)
        {
            if (cameraPivot == null) return;
            // Mouse delta already represents displacement this frame: do not multiply by deltaTime.
            transform.Rotate(0f, delta.x * sensitivity, 0f);
            float downLimit = Mathf.Min(pitchLimit, downwardPitchLimit);
            pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -pitchLimit, downLimit);
            cameraPivot.localRotation = Quaternion.Euler(Mathf.Clamp(pitch - recoil, -pitchLimit, downLimit), 0f, 0f);
            recoil *= Mathf.Exp(-12f * Time.deltaTime);
        }
    }
}
