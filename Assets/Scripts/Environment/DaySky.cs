using UnityEngine;

namespace HuntHappens.World
{
    /// <summary>Synchronizes the visible sun and moves the authored cloud bank.</summary>
    [ExecuteAlways]
    public sealed class DaySky : MonoBehaviour
    {
        public Light sun;
        public Material skyMaterial;
        public Transform clouds;
        [Min(0)] public float windSpeed = 0.65f;
        public Vector2 windDirection = new Vector2(1, 0.22f);
        [Min(100)] public float wrapWidth = 720f;

        private static readonly int SunDirection = Shader.PropertyToID("_SunDirection");

        private void LateUpdate()
        {
            if (sun != null && skyMaterial != null)
                skyMaterial.SetVector(SunDirection, -sun.transform.forward);

            if (!Application.isPlaying || clouds == null) return;
            Vector2 wind = windDirection.normalized * (windSpeed * Time.deltaTime);
            for (int i = 0; i < clouds.childCount; i++)
            {
                Transform cloud = clouds.GetChild(i);
                Vector3 p = cloud.localPosition;
                float speed = 0.85f + (i % 5) * 0.075f;
                p.x = Mathf.Repeat(p.x + wind.x * speed + wrapWidth * 0.5f, wrapWidth) - wrapWidth * 0.5f;
                p.z = Mathf.Repeat(p.z + wind.y * speed + wrapWidth * 0.5f, wrapWidth) - wrapWidth * 0.5f;
                cloud.localPosition = p;
            }
        }
    }
}
