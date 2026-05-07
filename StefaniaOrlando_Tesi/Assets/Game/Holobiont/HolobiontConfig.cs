using UnityEngine;

namespace Holobiont
{
    /*
     * Authoring data for holobiont behavior.
     *
     * Phase 1 covers the energy economy, composition (capacity + hub bonuses),
     * and cascade-failure pacing. Breath mapping curves and force-field tuning
     * land in later phases.
     *
     * Energy values are per-second rates, integrated by GameClock.DeltaTime
     * inside HolobiontManager.Update. Drains compose additively before being
     * applied as a single net energy flow.
     */
    [CreateAssetMenu(fileName = "HolobiontConfig", menuName = "Game/Holobiont Config")]
    public class HolobiontConfig : ScriptableObject
    {
        // ----- Energy -----
        [Header("Energy")]
        [Tooltip("Maximum energy with zero hub creatures.")]
        [Min(0.01f)] public float baseEnergyCapacity = 100f;

        [Tooltip("Energy at scene start. Clamped to MaxEnergy at runtime.")]
        [Min(0f)] public float startingEnergy = 100f;

        [Tooltip("Per-second energy drain per bonded creature, before stress and mismatch terms.")]
        [Min(0f)] public float baseDrainPerCreaturePerSecond = 0.5f;

        [Tooltip("Multiplier applied to summed creature stress to produce an additional per-second drain.")]
        [Min(0f)] public float stressCostMultiplier = 1f;

        [Tooltip("Multiplier applied to environment-resistance mismatch to produce an additional per-second drain.")]
        [Min(0f)] public float environmentMismatchCostMultiplier = 1f;

        // ----- Composition -----
        [Header("Composition")]
        [Tooltip("Maximum bonded creatures with zero hubs.")]
        [Min(1)] public int baseCarryingCapacity = 5;

        [Tooltip("Additional max-energy capacity contributed per hub. Phase 1 default 0 — code path is wired but inert until tuned.")]
        [Min(0f)] public float energyCapacityPerHub = 0f;

        [Tooltip("Additional creature slots contributed per hub. Phase 1 default 0 — code path is wired but inert until tuned.")]
        [Min(0)] public int carryingCapacityPerHub = 0;

        // ----- Cascade Failure -----
        [Header("Cascade Failure")]
        [Tooltip("Seconds between successive sheds while in cascade failure.")]
        [Min(0.05f)] public float cascadeTickInterval = 1f;
    }
}
