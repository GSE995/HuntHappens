using UnityEngine;

namespace HuntHappens.SmallGame
{
    // Add to food/litter props that rats should gather around.
    public sealed class RatGatheringSpot : MonoBehaviour
    {
        [Min(0.5f)] public float radius = 2f;
    }
}
