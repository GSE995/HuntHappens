using UnityEngine;

namespace HuntHappens.Shop
{
    public enum UpgradeKind { StrongerDarts, MoreTraps, StrongerArms, BetterContracts }

    [CreateAssetMenu(menuName = "Hunt Happens/Upgrade Definition")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        [SerializeField] private UpgradeKind kind;
        [SerializeField] private string title;
        [SerializeField, TextArea] private string description;
        [SerializeField] private int[] prices = new int[3];
        [Tooltip("Values for levels 0, 1, 2, 3. Darts: duration multiplier; traps: capacity; arms: mass multiplier; contracts: dollars.")]
        [SerializeField] private float[] effects = new float[4];
        public UpgradeKind Kind => kind;
        public string Title => title;
        public string Description => description;
        public int MaximumLevel => 3;
        public bool IsValid => prices != null && prices.Length == 3 && System.Array.TrueForAll(prices, p => p > 0) &&
            effects != null && effects.Length == 4 && System.Array.TrueForAll(effects, e => e > 0 && !float.IsNaN(e) && !float.IsInfinity(e));
        public int NextPrice(int level) => IsValid && level >= 0 && level < MaximumLevel ? prices[level] : -1;
        public float Effect(int level) => IsValid ? effects[Mathf.Clamp(level, 0, MaximumLevel)] : 1;
        public string DescribeLevel(int level)
        {
            float value = Effect(level);
            switch (kind)
            {
                case UpgradeKind.StrongerDarts: return "Время усыпления: " + Mathf.RoundToInt(value * 100) + "% от базового";
                case UpgradeKind.MoreTraps: return "Ловушек одновременно: " + Mathf.RoundToInt(value);
                case UpgradeKind.StrongerArms: return "Допустимая масса: " + Mathf.RoundToInt(value * 100) + "%";
                default: return "Награда за оленя: $" + Mathf.RoundToInt(value);
            }
        }
        public void Configure(UpgradeKind type, string name, string details, int[] costs, float[] values)
        { kind = type; title = name; description = details; prices = costs; effects = values; }
    }
}
