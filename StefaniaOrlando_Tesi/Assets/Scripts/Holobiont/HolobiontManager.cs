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
     *   - energy economy:
     *       inflow = breathDepth × Σ_rank (nutrici_eff × conversion × decay^rank)
     *                            × (1 + synergy × diversity)
     *                            × metabolic^inflowExp
     *       drain  = (baseDrain × N + stressSum + mismatch(perAxisTolerance))
     *                × metabolic^drainExp
     *   - composition: TryBond / Release maintain the bonded list, type counts,
     *     and per-hub-asset capacity bonuses (energy + slots).
     *   - resistance: per-axis tolerance summed from each scudo's affinity-
     *     specialized contribution, with stacking decay applied per-axis so
     *     covering different axes avoids the diminishing-returns penalty.
     *     Recomputed every Tick because scudo efficiency drifts with the
     *     environment.
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
            RecalculateComposition();
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
            RecalculateComposition();
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

            // Resistance drifts with each scudo's affinity efficiency, so refresh
            // it every tick. Composition (max energy, capacity) only changes on
            // bond / release and is handled in RecalculateComposition.
            RecalculateResistance();

            // Inflow: breath depth shapes a per-second multiplier, scaled by the
            // sum of nutrici contributions with rank-decay so monoculture stacking
            // hits diminishing returns. A diversity-weighted synergy bonus rewards
            // mixed compositions. During recovery the player can't breathe
            // intentionally — inflow collapses to 0 even if depth is nonzero.
            float breathDepth   = hasBreath ? breath.Depth : 0f;
            float breathEnergy  = inRecovery ? 0f : config.depthToEnergyMultiplier.Evaluate(breathDepth);
            float nutriciInflow = ComputeNutriciInflow();
            float synergy       = 1f + config.synergyStrength * ComputeDiversityScore();
            float totalInflow   = breathEnergy * nutriciInflow * synergy;

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

            // Metabolic rate: frequency scales both sides, but with separate
            // exponents so fast breathing becomes a real tradeoff (default 0.9
            // on inflow, 1.1 on drain — burst output at a steeper cost). Set
            // both exponents to 1 for the old symmetric behaviour.
            state.MetabolicRate = hasBreath
                ? Mathf.Max(0f, config.frequencyToMetabolicRate.Evaluate(breath.Frequency))
                : 1f;
            totalInflow *= Mathf.Pow(state.MetabolicRate, config.metabolicInflowExponent);
            totalDrain  *= Mathf.Pow(state.MetabolicRate, config.metabolicDrainExponent);

            state.NetEnergyFlow = totalInflow - totalDrain;
            state.Energy = Mathf.Clamp(state.Energy + state.NetEnergyFlow * dt, 0f, state.MaxEnergy);

            UpdatePhase();
        }

        // Per-tick scratch buffers for sorting creature contributions. Sized to
        // hold any reasonable carrying capacity; clamped on use to avoid GC.
        private const int MAX_INLINE_CREATURES = 64;
        private readonly float[] sortBuffer = new float[MAX_INLINE_CREATURES];

        /// <summary>
        /// Σ_rank (eff × baseConv × decay^rank). Sorted descending so the strongest
        /// nutrici always claims the rank-0 slot (= full contribution). decay = 1
        /// reproduces the linear-stacking behaviour; decay &lt; 1 makes additional
        /// nutrici diminish in value, capping monoculture spam.
        /// </summary>
        private float ComputeNutriciInflow()
        {
            int count = 0;
            for (int i = 0; i < state.BondedCreatures.Count && count < MAX_INLINE_CREATURES; i++)
            {
                var c = state.BondedCreatures[i];
                if (!c || c.Type != CreatureType.Nutrici || !c.Config) continue;
                float v = c.AffinityEfficiency * c.Config.GetEnergyConversion();
                if (v > 0f) sortBuffer[count++] = v;
            }
            if (count == 0) return 0f;

            SortDescending(sortBuffer, count);

            float total = 0f;
            float weight = 1f;
            float decay = Mathf.Clamp(config.stackingDecay, 0.0001f, 1f);
            for (int i = 0; i < count; i++)
            {
                total += sortBuffer[i] * weight;
                weight *= decay;
            }
            return total;
        }

        /// <summary>
        /// Shannon entropy of the {nutrici, scudo, hub} count distribution,
        /// normalized by ln(3) so the score is in [0,1]. Returns 0 for a pure
        /// monoculture, 1 for a perfectly balanced 1:1:1 mix. p=0 terms are
        /// skipped (ln 0 is undefined; their contribution is 0 in the limit).
        /// </summary>
        private float ComputeDiversityScore()
        {
            int total = state.CreatureCount;
            if (total <= 1) return 0f;
            float invTotal = 1f / total;
            float h = 0f;
            if (state.NutriciCount > 0) { float p = state.NutriciCount * invTotal; h -= p * Mathf.Log(p); }
            if (state.ScudoCount   > 0) { float p = state.ScudoCount   * invTotal; h -= p * Mathf.Log(p); }
            if (state.HubCount     > 0) { float p = state.HubCount     * invTotal; h -= p * Mathf.Log(p); }
            const float LN3 = 1.0986123f; // ln(3) — entropy of a perfectly balanced 3-bucket distribution.
            return Mathf.Clamp01(h / LN3);
        }

        /// <summary>Per-axis scudo tolerance: each scudo distributes (eff × baseRes) across the four axes weighted by its affinity specialization, then the per-axis stack is sorted and decayed independently. Spreading scudos across different specialty axes avoids the stacking penalty entirely.</summary>
        private void RecalculateResistance()
        {
            int scudoCount = state.ScudoCount;
            if (scudoCount == 0)
            {
                state.Resistance = default;
                return;
            }

            // Per-axis contribution arrays: stack-allocated since scudo count is bounded by carrying capacity (~tens).
            int n = Mathf.Min(scudoCount, MAX_INLINE_CREATURES);
            Span<float> tBuf = stackalloc float[MAX_INLINE_CREATURES];
            Span<float> lBuf = stackalloc float[MAX_INLINE_CREATURES];
            Span<float> hBuf = stackalloc float[MAX_INLINE_CREATURES];
            Span<float> xBuf = stackalloc float[MAX_INLINE_CREATURES];
            int filled = 0;

            float specBlend = Mathf.Clamp01(config.scudoSpecializationBlend);
            float baseShare = (1f - specBlend) * 0.25f;

            for (int i = 0; i < state.BondedCreatures.Count && filled < n; i++)
            {
                var c = state.BondedCreatures[i];
                if (!c || c.Type != CreatureType.Scudo || !c.Config) continue;

                float magnitude = c.Config.GetResistanceContribution() * c.AffinityEfficiency;
                if (magnitude <= 0f) continue;

                var aff = c.Affinity;
                float sT = Mathf.Abs(aff.idealTemperature - 0.5f) * 2f;
                float sL = Mathf.Abs(aff.idealLight       - 0.5f) * 2f;
                float sH = Mathf.Abs(aff.idealHumidity    - 0.5f) * 2f;
                float sX = Mathf.Abs(aff.idealToxicity    - 0.5f) * 2f;
                float sSum = sT + sL + sH + sX;

                float wT, wL, wH, wX;
                if (sSum < 1e-4f)
                {
                    // Pure generalist scudo (affinity at the centre on every axis): split evenly.
                    wT = wL = wH = wX = 0.25f;
                }
                else
                {
                    float invS = specBlend / sSum;
                    wT = baseShare + sT * invS;
                    wL = baseShare + sL * invS;
                    wH = baseShare + sH * invS;
                    wX = baseShare + sX * invS;
                }

                tBuf[filled] = magnitude * wT;
                lBuf[filled] = magnitude * wL;
                hBuf[filled] = magnitude * wH;
                xBuf[filled] = magnitude * wX;
                filled++;
            }

            float decay = Mathf.Clamp(config.stackingDecay, 0.0001f, 1f);
            state.Resistance = new ResistanceProfile
            {
                temperatureTolerance = SumWithDecay(tBuf, filled, decay),
                lightTolerance       = SumWithDecay(lBuf, filled, decay),
                humidityTolerance    = SumWithDecay(hBuf, filled, decay),
                toxicityTolerance    = SumWithDecay(xBuf, filled, decay)
            };
        }

        /// <summary>Sort a span of floats descending in place, then sum entries × decay^rank. Insertion sort because the typical N (creatures of one type) is small.</summary>
        private static float SumWithDecay(Span<float> values, int count, float decay)
        {
            if (count == 0) return 0f;
            SortDescending(values, count);
            float total = 0f;
            float weight = 1f;
            for (int i = 0; i < count; i++)
            {
                total += values[i] * weight;
                weight *= decay;
            }
            return total;
        }

        private static void SortDescending(Span<float> values, int count)
        {
            for (int i = 1; i < count; i++)
            {
                float v = values[i];
                int j = i - 1;
                while (j >= 0 && values[j] < v)
                {
                    values[j + 1] = values[j];
                    j--;
                }
                values[j + 1] = v;
            }
        }

        private static void SortDescending(float[] values, int count) => SortDescending(values.AsSpan(), count);

        private void UpdateBoundCreaturePositions()
        {
            int n = state.BondedCreatures.Count;
            if (n == 0) return;

            // Shell radius: breath-modulated preferred distance from center. Not a hard
            // ring — creatures organize around it via shell pull + peer interactions.
            float phaseFactor = breath != null
                ? Mathf.Max(0f, config.breathPhaseToOrbitRadius.Evaluate(breath.Phase))
                : 1f;
            float shellRadius = config.baseOrbitRadius * phaseFactor;
            Vector2 center    = transform.position;

            float peerR      = Mathf.Max(1e-3f, config.peerRepelRadius);
            float peerR2     = peerR * peerR;
            float typeReach  = peerR * Mathf.Max(1f, config.typeAttractReachMultiplier);
            float typeReach2 = typeReach * typeReach;
            float dt         = Time.deltaTime;

            for (int i = 0; i < n; i++)
            {
                var c = state.BondedCreatures[i];
                if (!c) continue;

                Vector2 cp = c.transform.position;

                // Shell pull: outside the shell pulls inward, inside pushes outward.
                // Sign of `overshoot` carries the direction.
                Vector2 toCenter     = center - cp;
                float   distToCenter = toCenter.magnitude;
                Vector2 force;
                if (distToCenter > 1e-4f)
                {
                    float overshoot = distToCenter - shellRadius;
                    force = (toCenter / distToCenter) * overshoot * config.shellPullStrength;
                }
                else
                {
                    // Right at center — kick out in a deterministic direction so it doesn't NaN.
                    int   h    = c.GetHashCode();
                    float ang  = ((h & 0xFFFF) / 65535f) * Mathf.PI * 2f;
                    force = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * shellRadius * config.shellPullStrength;
                }

                // Peer interactions: close-range repulsion (kills overlap, drives spread),
                // optional same-type attraction beyond the repel radius (mosaic clumping).
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    var cj = state.BondedCreatures[j];
                    if (!cj) continue;

                    Vector2 d     = cp - (Vector2)cj.transform.position;
                    float   dSq   = d.sqrMagnitude;

                    if (dSq < 1e-6f)
                    {
                        // Co-located — deterministic kick by index pair, avoids randomness flicker.
                        float ph = ((i * 7919 + j * 6151) & 0xFFFF) / 65535f * Mathf.PI * 2f;
                        force += new Vector2(Mathf.Cos(ph), Mathf.Sin(ph)) * peerR * config.peerRepelStrength * 0.5f;
                        continue;
                    }

                    float   dist = Mathf.Sqrt(dSq);
                    Vector2 dir  = d / dist;

                    if (dSq < peerR2)
                    {
                        float fall = (peerR - dist) / peerR;
                        force += dir * fall * config.peerRepelStrength;
                    }
                    else if (config.typeAttractStrength > 0f && c.Type == cj.Type && dSq < typeReach2)
                    {
                        float pull = (typeReach - dist) / (typeReach - peerR);
                        force -= dir * pull * config.typeAttractStrength;
                    }
                }

                // Subtle organic drift — keeps the cluster alive even when settled.
                if (config.positionNoise > 0f)
                {
                    int   h         = c.GetHashCode();
                    float driftPh   = ((h >> 8) & 0xFFFF) / 65535f * Mathf.PI * 2f;
                    float driftT    = Time.time * config.driftSpeed;
                    force += new Vector2(Mathf.Sin(driftT + driftPh),
                                         Mathf.Cos(driftT * 0.83f + driftPh * 1.7f)) * config.positionNoise;
                }

                Vector2 target = cp + force * dt;
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

        /// <summary>
        /// Recompute MaxEnergy and CarryingCapacity by summing each bonded hub's
        /// per-asset bonuses (HubConfig.energyCapacityBonus / carryingCapacityBonus).
        /// Different hub variants therefore contribute different bonuses — the old
        /// uniform per-hub multiplier on HolobiontConfig is gone. Called only on
        /// bond / release; resistance lives on its own dynamic recompute.
        /// </summary>
        private void RecalculateComposition()
        {
            float capacityBonus = 0f;
            int   slotBonus     = 0;
            for (int i = 0; i < state.BondedCreatures.Count; i++)
            {
                var c = state.BondedCreatures[i];
                if (!c || c.Type != CreatureType.Hub || !c.Config) continue;
                capacityBonus += c.Config.GetCapacityBonus();
                slotBonus     += c.Config.GetSlotBonus();
            }

            state.MaxEnergy        = config.baseEnergyCapacity   + capacityBonus;
            state.CarryingCapacity = config.baseCarryingCapacity + slotBonus;
            if (state.Energy > state.MaxEnergy) state.Energy = state.MaxEnergy;

            RecalculateResistance();
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
