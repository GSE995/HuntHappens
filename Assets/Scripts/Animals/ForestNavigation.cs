using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.Animals
{
    // Small replacement for a surface component: the Editor bakes data, this registers it.
    [ExecuteAlways, DefaultExecutionOrder(-200), DisallowMultipleComponent]
    public sealed class ForestNavigation : MonoBehaviour
    {
        [SerializeField] private NavMeshData data;
        private NavMeshDataInstance instance;
        public NavMeshData Data => data;

        public void SetData(NavMeshData value)
        {
            Remove();
            data = value;
            if (isActiveAndEnabled) Register();
        }

        private void OnEnable() => Register();
        private void OnDisable() => Remove();
        private void Register()
        {
            if (data != null && !instance.valid)
            {
                instance = NavMesh.AddNavMeshData(data);
                instance.owner = this;
            }
        }
        private void Remove()
        {
            if (instance.valid) instance.Remove();
        }
    }
}
