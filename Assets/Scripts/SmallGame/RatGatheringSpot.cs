using UnityEngine;

namespace HuntHappens.SmallGame
{
    // Add to food/litter props that rats should gather around.
    public sealed class RatGatheringSpot : MonoBehaviour
    {
        [Min(0.5f)] public float radius = 2f;
        public bool crowsAllowed;
        [Min(0.5f)] public float crowBaitRadius = 8f;
        public bool AllowsCrowBait(Vector3 position)
        {
            Vector3 delta = position - transform.position; delta.y = 0;
            return isActiveAndEnabled && crowsAllowed && delta.sqrMagnitude <= crowBaitRadius * crowBaitRadius;
        }
    }
}
