using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Holobiont
{
    /*
     * Owns the holobiont's runtime state and runs the energy + composition tick.
     *
     * Responsibilities:
     *   - energy economy: inflow (breathDepth × Σ nutrici efficiency × conversion)
     *     minus drain (base per-creature + stress + environment mismatch).
     *   - composition: TryBond / Release maintain the bonded list, type counts,
     *     resistance profile, and carrying capacity.
     *   - cascade failure: when energy hits zero, a coroutine sheds the most-
     *     stressed creature each interval until the list empties (then Dead).
     *   - bound positioning: every Update, bonded creatures are assigned spring
     *     targets in a ring around the origin, radius modulated by breath phase.
     *
     * Breath input is consumed via IBreathInput. The component is dragged into
     * the breathInputBehaviour slot; any MonoBehaviour implementing IBreathInput
     * works. When breath is absent, inflow is zero (pure drain) and the orbit
     * radius collapses to its base value — useful for inspector-only testing.
     *
     * Execution order is default (0). EnvironmentManager (-90) and GameClock
     * (-100) have already advanced by the time we read them.
     *
     * Recovery contract: the cascade coroutine never sets Phase back to
     * Stable / Declining — that's the main tick's job. The loop exits via its
     * while-condition once Update flips Phase off CascadeFailure.
     */
    [DisallowMultipleComponent]
    public class HolobiontManager : MonoBehaviour
    {
        // ----- Config -----
        [Header("Config")]
        [Tooltip("Tuning asset for energy, composition, and cascade pacing.")]
        [SerializeField] private HolobiontConfig config;

        [Tooltip("Environment used for the resistance-mismatch drain term. Optional — mismatch cost is 0 when null.")]
        [SerializeField] private EnvironmentManager environment;

        [Tooltip("Transform that bonded creatures reparent under. Force-field capture and ring positioning both use this.")]
        [SerializeField] private Transform bondedCreaturesParent;

        [Tooltip("Any MonoBehaviour implementing IBreathInput. BreathSimulator (full system) or KeyboardBreathInput (simple two-key mock) both work. NOT BreathInputHandler — that's just the keyboard reader feeding BreathSimulator and does not implement IBreathInput. Optional — when null, inflow is 0 and orbit radius doesn't pulse.")]
        [SerializeField] private MonoBehaviour breathInputBehaviour;

        // ----- Debug -----
        [Header("Debug")]
        [Tooltip("Drag a scene Creature here, then use the Bond / Release context-menu entries to drive the network manually.")]
        [SerializeField] private Creature debugBondTarget;

        [Tooltip("Energy added by the 'Add Energy' context-menu entry.")]
        [Min(0f), SerializeField] private float debugEnergyPulse = 20f;

        [Tooltip("Mirror of state.Energy.")]
        [SerializeField, ReadOnly] private float energy;

        [Tooltip("Mirror of state.MaxEnergy.")]
        [SerializeField, ReadOnly] private float maxEnergy;

        [Tooltip("Mirror of state.NetEnergyFlow (per second).")]
        [SerializeField, ReadOnly] private float netEnergyFlow;

        [Tooltip("Mirror of state.Phase.")]
        [SerializeField, ReadOnly] private HolobiontPhase phase;

        [Tooltip("Mirror of state.CreatureCount.")]
        [SerializeField, ReadOnly] private int creatureCount;

        [Tooltip("Mirror of state.CarryingCapacity.")]
        [SerializeField, ReadOnly] private int carryingCapacity;

        [Tooltip("Mirror of state.NutriciCount.")]
        [SerializeField, ReadOnly] private int nutriciCount;

        [Tooltip("Mirror of state.ScudoCount.")]
        [SerializeField, ReadOnly] private int scudoCount;

        [Tooltip("Mirror of state.HubCount.")]
        [SerializeField, ReadOnly] private int hubCount;

        // ----- State -----
        private readonly HolobiontState state = new HolobiontState();
        private Coroutine cascadeRoutine;
        private bool initialized;
        private IBreathInput breath;

        /// <summary>Read-only access to the runtime state record.</summary>
        public HolobiontState State => state;

        /// <summary>Tuning asset. Exposed so sibling components on the same GO can read it without a duplicate serialized field.</summary>
        public HolobiontConfig Config => config;

        /// <summary>Resolved breath input (may be null). Exposed for sibling systems (force field, view).</summary>
        public IBreathInput Breath => breath;

        // ----- Outputs -----
        /// <summary>Fired after a creature has been bonded and derived state has recalculated.</summary>
        public event Action<Creature> OnCreatureBonded;
        /// <summary>Fired after a creature has been released (manual or cascade).</summary>
        public event Action<Creature> OnCreatureReleased;
        /// <summary>Fired the frame energy reaches zero and the cascade coroutine starts.</summary>
        public event Action OnCascadeFailureStarted;
        /// <summary>Fired when the last bonded creature is shed during cascade.</summary>
        public event Action OnDeath;

        [Header("Events")]
        [Tooltip("Inspector counterpart of OnCreatureBonded.")]
        [SerializeField] private UnityEvent<Creature> creatureBondedEvent;

        [Tooltip("Inspector counterpart of OnCreatureReleased.")]
        [SerializeField] private UnityEvent<Creature> creatureReleasedEvent;

        [Tooltip("Inspector counterpart of OnCascadeFailureStarted.")]
        [SerializeField] private UnityEvent cascadeFailureStartedEvent;

        [Tooltip("Inspector counterpart of OnDeath.")]
        [SerializeField] private UnityEvent deathEvent;

        // ----- Lifecycle -----
        private void OnEnable()
        {
            if (!config)
            {
                Debug.LogError($"{nameof(HolobiontManager)} on {name}: missing {nameof(HolobiontConfig)}.", this);
                enabled = false;
                return;
            }

            breath = breathInputBehaviour as IBreathInput;
            if (breathInputBehaviour && breath == null)
            {
                Debug.LogError($"{nameof(HolobiontManager)} on {name}: {breathInputBehaviour.GetType().Name} does not implement {nameof(IBreathInput)}.", this);
            }

            ResetSessionState();
        }

        /// <summary>Tear down any in-flight cascade, destroy bonded creatures, and snap state back to baseline. Safe to call mid-session.</summary>
        [ContextMenu("Reset Session State")]
        public void ResetSessionState()
        {
            if (!config) return;

            if (cascadeRoutine != null)
            {
                StopCoroutine(cascadeRoutine);
                cascadeRoutine = null;
            }

            // Unsubscribe + destroy bonded creatures so handlers don't leak and ghost
            // creatures don't survive into the next session. CreatureSpawner handles
            // the unbound side of the cleanup.
            for (int i = 0; i < state.BondedCreatures.Count; i++)
            {
                var c = state.BondedCreatures[i];
                if (!c) continue;
                c.OnStressDeath -= HandleCreatureStressDeath;
                Destroy(c.gameObject);
            }
            state.BondedCreatures.Clear();

            state.NutriciCount = state.ScudoCount = state.HubCount = 0;
            state.CarryingCapacity = config.baseCarryingCapacity;
            state.MaxEnergy = config.baseEnergyCapacity;
            state.Energy = Mathf.Min(config.startingEnergy, state.MaxEnergy);
            state.NetEnergyFlow = 0f;
            state.MetabolicRate = 1f;
            state.Resistance = default;
            state.Phase = HolobiontPhase.Stable;

            MirrorState();
            initialized = true;
        }

        private void OnDisable()
        {
            if (cascadeRoutine != null)
            {
                StopCoroutine(cascadeRoutine);
                cascadeRoutine = null;
            }
            initialized = false;
        }

        private void Update()
        {
            if (!initialized) return;

            var clock = GameClock.Instance;
            float dt = clock ? clock.DeltaTime : Time.deltaTime;
            if (dt <= 0f) return; // paused or zero-scaled

            Tick(dt);
            UpdateBoundCreaturePositions();
            MirrorState();
        }

        // ----- Inputs -----
        /// <summary>Bond an unbound creature. Returns false if null, already bound, or at capacity.</summary>
        public bool TryBond(Creature creature)
        {
            if (!creature)
            {
                Debug.LogWarning($"{nameof(HolobiontManager)}.{nameof(TryBond)}: null creature.", this);
                return false;
            }
            if (creature.Status == BondStatus.Bound) return false;
            if (state.AtCapacity) return false;

            creature.SetBound();
            state.BondedCreatures.Add(creature);
            if (bondedCreaturesParent) creature.transform.SetParent(bondedCreaturesParent, true);
            creature.OnStressDeath += HandleCreatureStressDeath;

            UpdateTypeCounts();
            RecalculateDerivedState();
            MirrorState();

            OnCreatureBonded?.Invoke(creature);
            creatureBondedEvent?.Invoke(creature);
            return true;
        }

        /// <summary>Release a bonded creature. No-op if null or not currently bonded here.</summary>
        public void Release(Creature creature)
        {
            if (!creature) return;
            if (!state.BondedCreatures.Remove(creature)) return;

            creature.OnStressDeath -= HandleCreatureStressDeath;
            creature.SetUnbound();
            if (bondedCreaturesParent && creature.transform.parent == bondedCreaturesParent)
                creature.transform.SetParent(null, true);

            UpdateTypeCounts();
            RecalculateDerivedState();
            MirrorState();

            OnCreatureReleased?.Invoke(creature);
            creatureReleasedEvent?.Invoke(creature);
        }

        [ContextMenu("Bond")]
        public void BondDebugTarget() => TryBond(debugBondTarget);

        [ContextMenu("Release")]
        public void ReleaseDebugTarget() => Release(debugBondTarget);

        [ContextMenu("Release Last Bonded")]
        public void ReleaseLastBonded()
        {
            int n = state.BondedCreatures.Count;
            if (n == 0) return;
            Release(state.BondedCreatures[n - 1]);
        }

        [ContextMenu("Add Debug Energy")]
        public void AddDebugEnergy()
        {
            state.Energy = Mathf.Clamp(state.Energy + debugEnergyPulse, 0f, state.MaxEnergy);
            MirrorState();
        }

        /// <summary>Shed the currently most-stressed bonded creature, if any. Called by the force field on hold-inhale.</summary>
        public Creature ShedMostStressed()
        {
            if (state.BondedCreatures.Count == 0) return null;
            var pick = SelectMostStressed();
            if (pick) Release(pick);
            return pick;
        }

        // ----- Private -----
        private void Tick(float dt)
        {
            bool hasBreath  = breath != null;
            bool inRecovery = hasBreath && breath.InRecovery;

            // Inflow: breath depth shapes a per-second multiplier, scaled by total
            // nutrici conversion (each nutrici contributes its base rate × current
            // affinity efficiency). During recovery the player can't breathe
            // intentionally — inflow collapses to 0 even if depth is nonzero.
            float breathDepth   = hasBreath ? breath.Depth : 0f;
            float breathEnergy  = inRecovery ? 0f : config.depthToEnergyMultiplier.Evaluate(breathDepth);
            float nutriciInflow = 0f;
            for (int i = 0; i < state.BondedCreatures.Count; i++)
            {
                var c = state.BondedCreatures[i];
                if (!c || c.Type != CreatureType.Nutrici || !c.Config) continue;
                nutriciInflow += c.AffinityEfficiency * c.Config.GetEnergyConversion();
            }
            float totalInflow = breathEnergy * nutriciInflow;

            // Drain: base per creature + summed stress + environment mismatch.
            float baseDrain = state.CreatureCount * config.baseDrainPerCreaturePerSecond;

            float stressSum = 0f;
            for (int i = 0; i < state.BondedCreatures.Count; i++)
            {
                var c = state.BondedCreatures[i];
                if (c) stressSum += c.Stress;
            }
            float stressDrain   = stressSum * config.stressCostMultiplier;
            float mismatchDrain = state.Resistance.GetMismatchCost(environment) * config.environmentMismatchCostMultiplier;
            float totalDrain    = baseDrain + stressDrain + mismatchDrain;

            // Metabolic rate: frequency scales BOTH sides of the equation so fast
            // breathing is a tempo change, not a free power boost (design decision #6).
            // Cached on state so HUDs / VFX can read it without re-evaluating the curve.
            state.MetabolicRate = hasBreath
                ? Mathf.Max(0f, config.frequencyToMetabolicRate.Evaluate(breath.Frequency))
                : 1f;
            totalInflow *= state.MetabolicRate;
            totalDrain  *= state.MetabolicRate;

            state.NetEnergyFlow = totalInflow - totalDrain;
            state.Energy = Mathf.Clamp(state.Energy + state.NetEnergyFlow * dt, 0f, state.MaxEnergy);

            UpdatePhase();
        }

        private void UpdateBoundCreaturePositions()
        {
            int n = state.BondedCreatures.Count;
            if (n == 0) return;

            float phaseFactor = breath != null
                ? Mathf.Max(0f, config.breathPhaseToOrbitRadius.Evaluate(breath.Phase))
                : 1f;
            float radius = config.baseOrbitRadius * phaseFactor;
            Vector2 center = transform.position;

            for (int i = 0; i < n; i++)
            {
                var c = state.BondedCreatures[i];
                if (!c) continue;
                // Even distribution around the ring, plus a small per-creature offset
                // (hash-derived) so identical counts don't collapse into perfect symmetry.
                float baseAngle = (i / (float)n) * Mathf.PI * 2f;
                float jitter    = ((c.GetHashCode() & 0xFFFF) / 65535f) * 0.4f;
                float angle     = baseAngle + jitter;
                Vector2 target  = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                c.SetSpringTarget(target, config.boundSpringStrength);
            }
        }

        private void UpdatePhase()
        {
            if (state.Phase == HolobiontPhase.Dead) return;

            if (state.Energy > 0f)
            {
                state.Phase = state.NetEnergyFlow >= 0f ? HolobiontPhase.Stable : HolobiontPhase.Declining;
                return;
            }

            // Energy is at floor — enter cascade if we're not already there.
            if (state.Phase != HolobiontPhase.CascadeFailure)
            {
                state.Phase = HolobiontPhase.CascadeFailure;
                if (cascadeRoutine != null) StopCoroutine(cascadeRoutine);
                cascadeRoutine = StartCoroutine(CascadeFailureLoop());

                OnCascadeFailureStarted?.Invoke();
                cascadeFailureStartedEvent?.Invoke();
            }
        }

        private IEnumerator CascadeFailureLoop()
        {
            var wait = new WaitForSeconds(config.cascadeTickInterval);
            while (state.Phase == HolobiontPhase.CascadeFailure)
            {
                if (state.BondedCreatures.Count == 0)
                {
                    state.Phase = HolobiontPhase.Dead;
                    cascadeRoutine = null;
                    OnDeath?.Invoke();
                    deathEvent?.Invoke();
                    yield break;
                }

                Release(SelectMostStressed());
                yield return wait;
            }
            cascadeRoutine = null;
        }

        private Creature SelectMostStressed()
        {
            Creature pick = null;
            float worst = float.NegativeInfinity;
            for (int i = 0; i < state.BondedCreatures.Count; i++)
            {
                var c = state.BondedCreatures[i];
                if (!c) continue;
                if (c.Stress > worst)
                {
                    worst = c.Stress;
                    pick = c;
                }
            }
            // Fallback: if every entry was destroyed, return the first non-null we still have.
            if (!pick && state.BondedCreatures.Count > 0)
            {
                for (int i = 0; i < state.BondedCreatures.Count; i++)
                    if (state.BondedCreatures[i]) return state.BondedCreatures[i];
            }
            return pick;
        }

        private void HandleCreatureStressDeath(Creature creature) => Release(creature);

        private void UpdateTypeCounts()
        {
            int n = 0, s = 0, h = 0;
            for (int i = 0; i < state.BondedCreatures.Count; i++)
            {
                var c = state.BondedCreatures[i];
                if (!c) continue;
                switch (c.Type)
                {
                    case CreatureType.Nutrici: n++; break;
                    case CreatureType.Scudo:   s++; break;
                    case CreatureType.Hub:     h++; break;
                }
            }
            state.NutriciCount = n;
            state.ScudoCount   = s;
            state.HubCount     = h;
        }

        private void RecalculateDerivedState()
        {
            // Hub bonuses: defaults are 0 in Phase 1 — code path stays warm for tuning.
            state.MaxEnergy        = config.baseEnergyCapacity   + state.HubCount * config.energyCapacityPerHub;
            state.CarryingCapacity = config.baseCarryingCapacity + state.HubCount * config.carryingCapacityPerHub;
            if (state.Energy > state.MaxEnergy) state.Energy = state.MaxEnergy;

            // Resistance: each scudo contributes baseResistanceContribution * efficiency
            // uniformly to all four axes. Phase 2 may weight by per-scudo affinity.
            float tolerance = 0f;
            for (int i = 0; i < state.BondedCreatures.Count; i++)
            {
                var c = state.BondedCreatures[i];
                if (!c || c.Type != CreatureType.Scudo || !c.Config) continue;
                tolerance += c.Config.GetResistanceContribution() * c.AffinityEfficiency;
            }
            tolerance = Mathf.Clamp(tolerance, 0f, 1f);
            state.Resistance = new ResistanceProfile
            {
                temperatureTolerance = tolerance,
                lightTolerance       = tolerance,
                humidityTolerance    = tolerance,
                toxicityTolerance    = tolerance
            };
        }

        private void MirrorState()
        {
            energy           = state.Energy;
            maxEnergy        = state.MaxEnergy;
            netEnergyFlow    = state.NetEnergyFlow;
            phase            = state.Phase;
            creatureCount    = state.CreatureCount;
            carryingCapacity = state.CarryingCapacity;
            nutriciCount     = state.NutriciCount;
            scudoCount       = state.ScudoCount;
            hubCount         = state.HubCount;
        }
    }
}
