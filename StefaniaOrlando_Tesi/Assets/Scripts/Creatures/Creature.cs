using System;
using UnityEngine;
using UnityEngine.Events;

namespace Holobiont
{
    /*
     * One creature's runtime state: type, affinity, bond status, stress, and
     * per-regime physics. Reports state outward; never decides holobiont-level
     * concerns (energy, composition).
     *
     * Lifecycle: instantiate → Initialize(config, EnvironmentManager). Type is read from the config.
     * Per tick (FixedUpdate, gated on GameClock pause):
     *   - distance = Vector4.Distance(affinity, env.AsNormalizedVector)
     *   - efficiency = falloffCurve.Evaluate(distance / sqrt(4))
     *   - stress = 1 - efficiency
     * Unbound: external forces via ApplyForce + lifetime timer + despawn.
     * Bound:   spring toward springTarget; OnStressDeath when stress maxes out.
     */

    [RequireComponent(typeof(Rigidbody2D))]
    public class Creature : MonoBehaviour
    {
        // ----- Constants -----
        // Max possible distance in the unit 4D affinity hypercube: sqrt(4) = 2.
        private const float MAX_AFFINITY_DISTANCE = 2f;

        // ----- Dependencies (injected via Initialize) -----
        private CreatureConfig config;
        private EnvironmentManager environment;

        // ----- Debug (visible runtime state, not editable) -----
        [Header("Debug")]
        [Tooltip("Functional category set at spawn (Nutrici / Scudo / Hub).")]
        [SerializeField, ReadOnly] private CreatureType creatureType;

        [Tooltip("Ideal environmental conditions, generated at spawn from the live environment + scatter.")]
        [SerializeField, ReadOnly] private CreatureAffinityData affinity;

        [Tooltip("Whether the creature is drifting unbound or attached to the holobiont.")]
        [SerializeField, ReadOnly] private BondStatus bondStatus = BondStatus.Unbound;

        [Tooltip("Current stress in [0,1]. 1 - efficiency. Bound creatures die at config.stressDeathThreshold.")]
        [SerializeField, ReadOnly] private float currentStress;

        [Tooltip("Current affinity efficiency in [0,1]. 1 = perfect environment match.")]
        [SerializeField, ReadOnly] private float currentEfficiency = 1f;

        [Tooltip("Seconds remaining before an unbound creature despawns. 0 = no expiry.")]
        [SerializeField, ReadOnly] private float unboundTimer;

        // ----- Internal state -----
        // Spring (bound regime)
        private Vector2 springTarget;
        private float springStrength;
        private bool hasSpringTarget;

        private Rigidbody2D rb;
        private bool initialized;

        // ----- Public read-only API -----
        /// <summary>Config asset this creature was initialized with. Null until Initialize runs.</summary>
        public CreatureConfig Config => config;

        /// <summary>Functional category set at spawn — Nutrici / Scudo / Hub.</summary>
        public CreatureType Type => creatureType;

        /// <summary>Ideal environmental conditions, generated at spawn.</summary>
        public CreatureAffinityData Affinity => affinity;

        /// <summary>Whether the creature is drifting unbound or attached to the holobiont.</summary>
        public BondStatus Status => bondStatus;

        /// <summary>Current stress (1 - efficiency). 1 = maximum mismatch.</summary>
        public float Stress => currentStress;

        /// <summary>Current affinity efficiency in [0,1]. 1 = perfect environment match.</summary>
        public float AffinityEfficiency => currentEfficiency;

        /// <summary>True after Initialize has been called.</summary>
        public bool IsInitialized => initialized;

        /// <summary>Seconds remaining before an unbound creature expires and is destroyed. 0 while bonded or when the config disables expiry. Visual layer reads this for the despawn fade.</summary>
        public float UnboundTimeRemaining => unboundTimer;

        // ----- Outputs -----
        /// <summary>Fired while bound when stress reaches stressDeathThreshold. The HolobiontManager listens and handles release.</summary>
        public event Action<Creature> OnStressDeath;

        /// <summary>Fired when an unbound creature's lifetime expires, immediately before destruction. Spawn/pool listeners can hook here.</summary>
        public event Action<Creature> OnUnboundExpired;

        [Header("Events")]
        [Tooltip("Inspector-wired counterpart of OnStressDeath. Fires while bound when stress reaches the death threshold.")]
        [SerializeField] private UnityEvent<Creature> stressDeathEvent;

        [Tooltip("Inspector-wired counterpart of OnUnboundExpired. Fires immediately before an unbound creature is destroyed.")]
        [SerializeField] private UnityEvent<Creature> unboundExpiredEvent;

        // ----- Lifecycle -----
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
        }

        /// <summary>Spawn-time init. Generates affinity from the current environment. Type is read from the config.</summary>
        public void Initialize(CreatureConfig creatureConfig, EnvironmentManager env)
        {
            if (!creatureConfig)
            {
                Debug.LogError($"{nameof(Creature)}.{nameof(Initialize)}: config is null", this);
                return;
            }

            config = creatureConfig;
            creatureType = creatureConfig.Type;
            environment = env;
            affinity = GenerateAffinity(env, creatureConfig.affinityScatter);
            bondStatus = BondStatus.Unbound;
            currentStress = 0f;
            currentEfficiency = 1f;
            unboundTimer = creatureConfig.unboundLifetime;
            ApplyDampingForCurrentStatus();
            initialized = true;
        }

        /// <summary>Generates an affinity point from current env (normalized) + per-axis scatter, clamped to [0,1].</summary>
        public static CreatureAffinityData GenerateAffinity(EnvironmentManager env, float scatter)
        {
            if (!env) return default;
            return new CreatureAffinityData
            {
                idealTemperature = Mathf.Clamp01(env.TemperatureNormalized + UnityEngine.Random.Range(-scatter, scatter)),
                idealHumidity    = Mathf.Clamp01(env.HumidityNormalized    + UnityEngine.Random.Range(-scatter, scatter)),
                idealToxicity    = Mathf.Clamp01(env.ToxicityNormalized    + UnityEngine.Random.Range(-scatter, scatter)),
                idealLight       = Mathf.Clamp01(env.LightNormalized       + UnityEngine.Random.Range(-scatter, scatter))
            };
        }

        private void FixedUpdate()
        {
            if (!initialized || !config || !environment) return;
            var clock = GameClock.Instance;
            if (clock && clock.IsPaused) return;

            UpdateAffinityAndStress();

            if (bondStatus == BondStatus.Unbound)
                TickUnbound();
            else
                TickBound();
        }

        // ----- Public mutators (called by HolobiontManager / ForceField) -----
        /// <summary>Mark the creature as bound to the holobiont and apply bound damping.</summary>
        public void SetBound()
        {
            bondStatus = BondStatus.Bound;
            unboundTimer = 0f;
            ApplyDampingForCurrentStatus();
        }

        /// <summary>Mark the creature as unbound, restart the lifetime timer, and apply unbound damping.</summary>
        public void SetUnbound()
        {
            bondStatus = BondStatus.Unbound;
            unboundTimer = config ? config.unboundLifetime : 0f;
            hasSpringTarget = false;
            ApplyDampingForCurrentStatus();
        }

        /// <summary>External force (flow field, holobiont attraction/repulsion).</summary>
        public void ApplyForce(Vector2 force)
        {
            if (rb) rb.AddForce(force);
        }

        /// <summary>Set the spring target (world position) for bound regime.</summary>
        public void SetSpringTarget(Vector2 target, float strength)
        {
            springTarget = target;
            springStrength = strength;
            hasSpringTarget = true;
        }

        // ----- Private -----
        private void UpdateAffinityAndStress()
        {
            float distance = Vector4.Distance(affinity.AsVector, environment.AsNormalizedVector);
            float normalized = Mathf.Clamp01(distance / MAX_AFFINITY_DISTANCE);
            currentEfficiency = Mathf.Clamp01(config.affinityFalloffCurve.Evaluate(normalized));
            currentStress = 1f - currentEfficiency;
        }

        private void TickUnbound()
        {
            if (config.unboundLifetime > 0f)
            {
                unboundTimer -= Time.fixedDeltaTime;
                if (unboundTimer <= 0f)
                {
                    OnUnboundExpired?.Invoke(this);
                    unboundExpiredEvent?.Invoke(this);
                    Destroy(gameObject);
                }
            }
            // Movement: external forces (flow field, holobiont attraction) come via ApplyForce().
            // Linear damping is set by ApplyDampingForCurrentStatus().
        }

        private void TickBound()
        {
            if (currentStress >= config.stressDeathThreshold)
            {
                OnStressDeath?.Invoke(this);
                stressDeathEvent?.Invoke(this);
                return; // Manager handles release; spring math is moot this tick.
            }

            if (hasSpringTarget)
            {
                Vector2 delta = springTarget - (Vector2)transform.position;
                rb.AddForce(delta * springStrength);
            }
        }

        private void ApplyDampingForCurrentStatus()
        {
            if (!rb || !config) return;
            rb.linearDamping = bondStatus == BondStatus.Bound
                ? config.boundLinearDamping
                : config.unboundLinearDamping;
        }
    }
}
