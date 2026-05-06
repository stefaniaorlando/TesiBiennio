using UnityEngine;

namespace Holobiont
{
    /*
     * Metabolic infrastructure. Adds max-energy capacity and creature-carrying slots
     * to the holobiont. Doesn't produce energy or resist conditions directly — its value
     * is enabling growth without immediate collapse.
     */

    [CreateAssetMenu(fileName = "HubConfig", menuName = "Game/Creature/Hub Config")]
    public class HubConfig : CreatureConfig
    {
        public override CreatureType Type => CreatureType.Hub;

        // ----- Hub stats -----
        [Header("Hub")]
        [Tooltip("Additional max energy per hub.")]
        public float energyCapacityBonus = 50f;

        [Tooltip("Additional creature slots per hub.")]
        public int carryingCapacityBonus = 1;

        public override float GetCapacityBonus() => energyCapacityBonus;
        public override int GetSlotBonus() => carryingCapacityBonus;
    }
}
