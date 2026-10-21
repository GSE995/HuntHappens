using HuntHappens.Delivery;
using HuntHappens.Interaction;
using HuntHappens.Traps;
using HuntHappens.Weapons;
using UnityEngine;

namespace HuntHappens.Shop
{
    [DisallowMultipleComponent]
    public sealed class UpgradeManager : MonoBehaviour
    {
        [SerializeField] private UpgradeDefinition[] definitions;
        [SerializeField] private DeliveryManager wallet;
        [SerializeField] private TranquilizerGun gun;
        [SerializeField] private TrapManager traps;
        [SerializeField] private ItemHolder holder;
        [SerializeField] private int[] levels = new int[4];
        public UpgradeDefinition[] Definitions => definitions;
        public bool IsConfigured
        {
            get
            {
                if (wallet == null || gun == null || traps == null || holder == null || definitions == null || definitions.Length != 4) return false;
                for (int i = 0; i < 4; i++) if (definitions[i] == null || !definitions[i].IsValid || (int)definitions[i].Kind != i) return false;
                return true;
            }
        }
        public void Configure(UpgradeDefinition[] data, DeliveryManager money, TranquilizerGun weapon, TrapManager trapManager, ItemHolder carry)
        { definitions = data; wallet = money; gun = weapon; traps = trapManager; holder = carry; }
        public UpgradeDefinition Definition(UpgradeKind kind) => definitions != null && (int)kind >= 0 && (int)kind < definitions.Length ? definitions[(int)kind] : null;
        public int Level(UpgradeKind kind) => (int)kind >= 0 && (int)kind < levels.Length ? levels[(int)kind] : 0;
        public int[] CopyLevels() => (int[])levels.Clone();
        public void RestoreLevels(int[] saved)
        {
            levels = new int[4];
            for (int i = 0; i < 4; i++) levels[i] = saved != null && i < saved.Length ? Mathf.Clamp(saved[i], 0, 3) : 0;
            Apply();
        }
        internal void Increase(UpgradeKind kind) { levels[(int)kind] = Mathf.Min(3, Level(kind) + 1); Apply(); }
        public void Apply()
        {
            if (!IsConfigured) return;
            gun.SetSedationDurationMultiplier(Definition(UpgradeKind.StrongerDarts).Effect(Level(UpgradeKind.StrongerDarts)));
            var trapDefinition = Definition(UpgradeKind.MoreTraps);
            traps.SetAdditionalTraps(Mathf.RoundToInt(trapDefinition.Effect(Level(UpgradeKind.MoreTraps)) - trapDefinition.Effect(0)));
            holder.SetCarryUpgrade(Definition(UpgradeKind.StrongerArms).Effect(Level(UpgradeKind.StrongerArms)));
            wallet.SetReward(Mathf.RoundToInt(Definition(UpgradeKind.BetterContracts).Effect(Level(UpgradeKind.BetterContracts))));
        }
    }
}
