using UnityEngine;

namespace Holobiont
{
    /*
     * Authoring data for holobiont behavior.
     *
     * Sections:
     *   Energy            — capacity, drain rates, cost multipliers (per second).
     *   Composition       — carrying capacity, hub bonuses.
     *   Cascade Failure   — pacing of forced sheds when energy reaches zero.
     *   Force Field       — radii and forces applied to unbound creatures.
     *   Breath Mapping    — curves that translate breath signals into game effects.
     *   Bound Positioning — ring layout for bonded creatures.
     *
     * Energy values are per-second rates, integrated by GameClock.DeltaTime
     * inside HolobiontManager.Update. Drains and inflow compose additively
     * before being applied as a single net energy flow.
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

        // ----- Force Field -----
        [Header("Force Field")]
        [Tooltip("World-units field reach at minimum breath depth.")]
        [Min(0f)] public float baseAttractionRadius = 3f;

        [Tooltip("World-units field reach at maximum breath depth.")]
        [Min(0f)] public float maxAttractionRadius = 8f;

        [Tooltip("Force magnitude applied to unbound creatures while exhaling. Positive = pulled toward center.")]
        [Min(0f)] public float attractionStrength = 6f;

        [Tooltip("Force magnitude applied to unbound creatures while inhaling. Always pushes away from center.")]
        [Min(0f)] public float repulsionStrength = 6f;

        [Tooltip("Distance from center, normalized to current radius (0 = center, 1 = edge), mapped to a force multiplier. Default linear: max pull at the edge, no pull at the center, so creatures don't oscillate through origin.")]
        public AnimationCurve attractionFalloff = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Capture radius around the holobiont. Unbound creatures inside this radius bond on hold-exhale.")]
        [Min(0f)] public float bondingRange = 1.5f;

        // ----- Breath Mapping -----
        [Header("Breath Mapping")]
        [Tooltip("Breath depth (0..1) mapped to an energy-inflow multiplier per second, before nutrici efficiency. Default linear: depth 0 = no inflow, depth 1 = full inflow.")]
        public AnimationCurve depthToEnergyMultiplier = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Breath phase (-1 inhale → +1 exhale, sampled at -1, 0, +1) mapped to a field-radius multiplier. Default contracts the field on inhale, expands on exhale.")]
        public AnimationCurve breathPhaseToFieldRadius = new AnimationCurve(
            new Keyframe(-1f, 0.5f),
            new Keyframe( 0f, 1.0f),
            new Keyframe(+1f, 1.4f));

        [Tooltip("Normalized breath frequency (0..1) mapped to a global metabolic-rate multiplier. Per design decision #6, this scales BOTH inflow and drain — fast breathing is a tempo change, not a power boost. Default: 0 → 0.5x, 0.5 → 1.0x, 1 → 1.5x.")]
        public AnimationCurve frequencyToMetabolicRate = new AnimationCurve(
            new Keyframe(0f,   0.5f),
            new Keyframe(0.5f, 1.0f),
            new Keyframe(1f,   1.5f));

        // ----- Bound Positioning -----
        [Header("Bound Positioning")]
        [Tooltip("Base orbit radius for bonded creatures around the holobiont center.")]
        [Min(0f)] public float baseOrbitRadius = 1.2f;

        [Tooltip("Breath phase (-1..+1) mapped to a multiplier on the orbit radius. Lets the bonded ring breathe in and out with the player.")]
        public AnimationCurve breathPhaseToOrbitRadius = new AnimationCurve(
            new Keyframe(-1f, 0.85f),
            new Keyframe( 0f, 1.0f),
            new Keyframe(+1f, 1.15f));

        [Tooltip("Spring force constant pulling each bonded creature toward its orbit target.")]
        [Min(0f)] public float boundSpringStrength = 40f;
    }
}
