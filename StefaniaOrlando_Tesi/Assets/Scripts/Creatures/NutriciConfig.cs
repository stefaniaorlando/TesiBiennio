using UnityEngine;

namespace Holobiont
{
    /*
     * Energy producer. Converts external drive (eventually breath) into holobiont energy,
     * scaled by current affinity efficiency. The only type that contributes inflow.
     */

    [CreateAssetMenu(fileName = "NutriciConfig", menuName = "Game/Creature/Nutrici Config")]
    public class NutriciConfig : CreatureConfig
    {
        public override CreatureType Type => CreatureType.Nutrici;

        // ----- Nutrici stats -----
        [Header("Nutrici")]
        [Tooltip("Energy produced per tick at perfect affinity match, per unit external drive.")]
        public float baseConversionRate = 1f;

        public override float GetEnergyConversion() => baseConversionRate;
    }
}
