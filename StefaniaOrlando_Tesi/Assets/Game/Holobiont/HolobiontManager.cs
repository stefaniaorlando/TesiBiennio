using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Holobiont
{
    /*
     * Owns the holobiont's runtime state and runs the energy + composition tick.
     *
     * Phase 1 scope:
     *   - drain-only economy (no breath, no inflow path),
     *   - manual Bond / Release via [ContextMenu] for inspector-driven testing,
     *   - cascade-failure coroutine that sheds the most-stressed creature each
     *     interval until the bonded list empties (then transitions to Dead).
     *
     * Composition changes (TryBond / Release / cascade shed) call
     * RecalculateDerivedState which updates MaxEnergy, CarryingCapacity, type
     * counts, and ResistanceProfile. The per-frame Tick computes drain, net
     * energy flow, energy clamp, and phase.
     *
     * Execution order is left at default (0). EnvironmentManager (-90) and
     * GameClock (-100) have already advanced by the time we read them.
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

        [Tooltip("Transform that bonded creatures reparent under. Set up now so Phase 2 force-field capture has a target.")]
        [SerializeField] private Transform bondedCreaturesParent;

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

        /// <summary>Read-only access to the runtime state record.</summary>
        public HolobiontState State => state;

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

            state.BondedCreatures.Clear();
            state.NutriciCount = state.ScudoCount = state.HubCount = 0;
            state.CarryingCapacity = config.baseCarryingCapacity;
            state.MaxEnergy = config.baseEnergyCapacity;
            state.Energy = Mathf.Min(config.startingEnergy, state.MaxEnergy);
            state.NetEnergyFlow = 0f;
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

        // ----- Private -----
        private void Tick(float dt)
        {
            // Drain only — no inflow path until breath / nutrici-driven inflow lands later.
            float baseDrain = state.CreatureCount * config.baseDrainPerCreaturePerSecond;

            float stressSum = 0f;
            for (int i = 0; i < state.BondedCreatures.Count; i++)
            {
                var c = state.BondedCreatures[i];
                if (c) stressSum += c.Stress;
            }
            float stressDrain = stressSum * config.stressCostMultiplier;

            float mismatchDrain = state.Resistance.GetMismatchCost(environment) * config.environmentMismatchCostMultiplier;

            float totalDrain = baseDrain + stressDrain + mismatchDrain;
            state.NetEnergyFlow = -totalDrain;

            state.Energy = Mathf.Clamp(state.Energy + state.NetEnergyFlow * dt, 0f, state.MaxEnergy);

            UpdatePhase();
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
