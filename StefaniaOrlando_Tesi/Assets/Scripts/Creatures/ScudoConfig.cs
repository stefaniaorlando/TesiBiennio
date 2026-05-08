using UnityEngine;

namespace Holobiont
{
    /*
     * Environmental shield. Contributes to the holobiont's resistance profile,
     * reducing collective drain when the environment exceeds the network's tolerance.
     */

    [CreateAssetMenu(fileName = "ScudoConfig", menuName = "Game/Creature/Scudo Config")]
    public class ScudoConfig : CreatureConfig
    {
        public override CreatureType Type => CreatureType.Scudo;

        // ----- Scudo stats -----
        [Header("Scudo")]
        [Tooltip("Resistance contributed per scudo at perfect affinity match.")]
        public float baseResistanceContribution = 1f;

        public override float GetResistanceContribution() => baseResistanceContribution;
    }
}
