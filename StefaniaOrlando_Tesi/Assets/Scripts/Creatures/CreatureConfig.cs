using UnityEngine;

namespace Holobiont
{
    /*
     * Abstract base for the three creature-type configs (NutriciConfig, ScudoConfig, HubConfig).
     * Holds the parameters every type shares — identity, visuals, prefab, sim, physics — plus
     * virtual accessors that subclasses override to expose their type-specific contribution
     * to the holobiont's totals (energy, resistance, capacity, slots).
     *
     * Abstract: no [CreateAssetMenu] here. Only the concrete subclasses are author-able.
     */

    public abstract class CreatureConfig : ScriptableObject
    {
        // ----- Identity -----
        [Header("Identity")]
        [Tooltip("Display name shown in editor / debug overlays.")]
        public string displayName = "Creature";

        /// <summary>Functional category. Hard-coded by the concrete subclass; not an authored field.</summary>
        public abstract CreatureType Type { get; }

        // ----- Visuals -----
        [Header("Visuals")]
        [Tooltip("Prefab the spawner instantiates for this creature type. Must contain a Creature component.")]
        public GameObject prefab;

        [Tooltip("Base color before affinity / stress modulation. Used by the visual layer.")]
        public Color baseColor = Color.white;

        // ----- Simulation (shared) -----
        [Header("Simulation")]
        [Tooltip("Affinity-environment mismatch curve. X = normalized distance (0 = perfect match, 1 = max mismatch). Y = efficiency. Drives nutrici output, scudo effectiveness, and stress = 1 - efficiency.")]
        public AnimationCurve affinityFalloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Tooltip("Stress level at which a bound creature auto-detaches and dies.")]
        [Range(0f, 1f)]
        public float stressDeathThreshold = 0.95f;

        [Tooltip("Random deviation per axis added to environment when generating spawn affinity.")]
        [Range(0f, 1f)]
        public float affinityScatter = 0.15f;

        [Tooltip("Seconds an unbound creature survives before despawning. Set to 0 for no expiry.")]
        public float unboundLifetime = 30f;

        // ----- Physics -----
        [Header("Physics")]
        [Tooltip("Linear damping while drifting unbound (low for free drift).")]
        public float unboundLinearDamping = 0.5f;

        [Tooltip("Linear damping while bound (high for spring stability).")]
        public float boundLinearDamping = 4f;

        // ----- Type-specific accessors -----
        /// <summary>Energy produced per tick at perfect affinity match, per unit external drive. Non-zero only for nutrici.</summary>
        public virtual float GetEnergyConversion() => 0f;

        /// <summary>Resistance contributed at perfect affinity match. Non-zero only for scudo.</summary>
        public virtual float GetResistanceContribution() => 0f;

        /// <summary>Additional max-energy capacity contributed to the holobiont. Non-zero only for hubs.</summary>
        public virtual float GetCapacityBonus() => 0f;

        /// <summary>Additional creature slots contributed to the holobiont. Non-zero only for hubs.</summary>
        public virtual int GetSlotBonus() => 0;
    }
}
