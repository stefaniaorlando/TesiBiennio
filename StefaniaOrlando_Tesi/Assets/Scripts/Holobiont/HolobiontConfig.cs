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
     *   Breath Field      — extracted into BreathFieldConfig (radii, forces, ring visuals).
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

        // Per-hub bonuses now come from each bonded HubConfig asset
        // (HubConfig.energyCapacityBonus / carryingCapacityBonus) so different
        // hub variants can carry different bonuses. The old uniform per-hub
        // fields on this asset have been removed.

        // ----- Stacking & Synergy -----
        [Header("Stacking & Synergy")]
        [Tooltip("Geometric decay applied to per-type stacking. Creatures of the same type are sorted by effective contribution; the rank-k creature's contribution is multiplied by decay^k. 1 = no diminishing returns (linear stacking — old behaviour). 0.85 = each additional creature of the same type contributes ~85% of the previous one. Applied independently to nutrici inflow and to scudo per-axis tolerance, so spreading creatures across axes/types avoids the penalty.")]
        [Range(0.1f, 1f)] public float stackingDecay = 0.85f;

        [Tooltip("How strongly each scudo's affinity directs its tolerance into specific axes. 0 = uniform across all four axes (old behaviour). 1 = pure specialist — a scudo's tolerance is split only across axes where its affinity is extreme (far from 0.5). 0.7 default keeps a baseline contribution everywhere while rewarding specialization.")]
        [Range(0f, 1f)] public float scudoSpecializationBlend = 0.7f;

        [Tooltip("Maximum bonus to inflow from compositional diversity (mix of nutrici/scudo/hub). 0 = pure additive, no synergy. 0.25 means a perfectly balanced 1:1:1 composition produces 25% more inflow than a monoculture.")]
        [Min(0f)] public float synergyStrength = 0.25f;

        [Tooltip("Exponent applied to the metabolic rate when scaling inflow. <1 makes fast breathing produce diminishing inflow. 1 = symmetric (old behaviour); inflow and drain scale identically with breath frequency.")]
        [Min(0f)] public float metabolicInflowExponent = 0.9f;

        [Tooltip("Exponent applied to the metabolic rate when scaling drain. >1 makes fast breathing pay a steeper drain penalty. 1 = symmetric (old behaviour). Combined with metabolicInflowExponent < 1, breath tempo becomes a real strategic axis: fast = burst output at higher cost, slow = efficient steady state.")]
        [Min(0f)] public float metabolicDrainExponent = 1.1f;

        // ----- Cascade Failure -----
        [Header("Cascade Failure")]
        [Tooltip("Seconds between successive sheds while in cascade failure.")]
        [Min(0.05f)] public float cascadeTickInterval = 1f;

        // ----- Breath Field -----
        [Header("Breath Field")]
        [Tooltip("Tuning for the breath-driven attraction/repulsion field and its ring visualization. Single source for HolobiontForceField (geometry, forces, hub bonus) and HolobiontView (ring colors, alphas).")]
        [SerializeField] private BreathFieldConfig breathField;

        /// <summary>Breath-field tuning (radii, forces, hub bonus, ring visuals). May be null if not yet wired.</summary>
        public BreathFieldConfig BreathField => breathField;

        // ----- Breath Mapping -----
        [Header("Breath Mapping")]
        [Tooltip("Breath depth (0..1) mapped to an energy-inflow multiplier per second, before nutrici efficiency. Default linear: depth 0 = no inflow, depth 1 = full inflow.")]
        public AnimationCurve depthToEnergyMultiplier = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Normalized breath frequency (0..1) mapped to a global metabolic-rate multiplier. Per design decision #6, this scales BOTH inflow and drain — fast breathing is a tempo change, not a power boost. Default: 0 → 0.5x, 0.5 → 1.0x, 1 → 1.5x.")]
        public AnimationCurve frequencyToMetabolicRate = new AnimationCurve(
            new Keyframe(0f,   0.5f),
            new Keyframe(0.5f, 1.0f),
            new Keyframe(1f,   1.5f));

        // ----- Bound Positioning -----
        [Header("Bound Positioning")]
        [Tooltip("Preferred distance of bonded creatures from the holobiont center. Used as the soft-shell radius around which the cluster organizes; not a hard ring.")]
        [Min(0f)] public float baseOrbitRadius = 1.2f;

        [Tooltip("Breath phase (-1..+1) mapped to a multiplier on the shell radius. Lets the cluster breathe in and out with the player.")]
        public AnimationCurve breathPhaseToOrbitRadius = new AnimationCurve(
            new Keyframe(-1f, 0.85f),
            new Keyframe( 0f, 1.0f),
            new Keyframe(+1f, 1.15f));

        [Tooltip("Spring force constant pulling each bonded creature toward its target position.")]
        [Min(0f)] public float boundSpringStrength = 40f;

        // ----- Bound Network -----
        [Header("Bound Network")]
        [Tooltip("How strongly creatures are pulled toward the soft-shell radius (units/sec at 1 unit overshoot). Higher = tighter cluster around the center; lower = looser, more rhizomatic spread.")]
        [Min(0f)] public float shellPullStrength = 1.5f;

        [Tooltip("Distance under which bonded creatures start pushing each other away. Sets the average cell size of the mosaic — make it roughly the visible diameter of a creature.")]
        [Min(0.01f)] public float peerRepelRadius = 0.55f;

        [Tooltip("Strength of peer-to-peer repulsion (units/sec at full overlap). Higher = creatures spread more aggressively and fill the available area.")]
        [Min(0f)] public float peerRepelStrength = 2.0f;

        [Tooltip("Strength of same-type attraction beyond the repel range (units/sec). Drives mosaic clumping by creature type. Set to 0 to disable.")]
        [Min(0f)] public float typeAttractStrength = 0.4f;

        [Tooltip("Multiplier on peerRepelRadius defining how far same-type attraction reaches. e.g. 3 means types pull on each other up to 3x the repel radius, then go neutral.")]
        [Min(1f)] public float typeAttractReachMultiplier = 3f;

        [Tooltip("Amplitude of per-creature organic drift (units/sec). Keeps the cluster subtly alive even when settled. Set to 0 for a static layout.")]
        [Min(0f)] public float positionNoise = 0.1f;

        [Tooltip("Speed of the per-creature drift oscillation (cycles/sec, roughly). Slower = lazier sway.")]
        [Min(0f)] public float driftSpeed = 0.6f;
    }
}
