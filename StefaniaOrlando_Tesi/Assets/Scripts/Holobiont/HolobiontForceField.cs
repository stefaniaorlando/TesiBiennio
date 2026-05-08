using System.Collections.Generic;
using UnityEngine;

namespace Holobiont
{
    /*
     * Sibling to HolobiontManager. Translates breath into spatial forces on
     * unbound creatures, plus discrete capture / shed events.
     *
     * Tunables live on BreathFieldConfig (referenced via HolobiontConfig.BreathField):
     * radii, hub bonus, force strengths, falloff, breath-phase curve.
     *
     * Tick (FixedUpdate, matching Creature.FixedUpdate so forces compose cleanly):
     *   - currentRadius = (lerp(baseRadius, maxRadius, depth) + HubCount × radiusPerHub)
     *                   × breathPhaseToRadius.Evaluate(phase).
     *   - hold-exhale (rising) → bond the N closest unbound creatures inside
     *     currentRadius in one batch, where N = min(remainingCapacity,
     *     baseBondsPerHold + floor(bondsPerHub × hubCount)). No further bonds
     *     during the same hold even if creatures drift in.
     *   - hold-inhale (rising) → manager.ShedMostStressed().
     *   - during either hold   → invisible charge field (currentRadius +
     *     holdRadiusBonus × charge) applies attract / repel scaled by charge.
     *   - otherwise             → push attraction (phase > 0) or repulsion
     *     (phase < 0) onto every unbound creature inside currentRadius, scaled
     *     by attractionFalloff.
     *
     * Highlights (passive, refreshed every tick regardless of hold state):
     *   - Bond preview: the N closest unbound creatures in currentRadius get
     *     SetBondCandidate(true), so the player sees who would bond next pause.
     *   - Shed preview: the most-stressed bonded creature gets
     *     SetShedCandidate(true), matching the cascade / inhale-hold target.
     *
     * Origin: queries are centered on captureOrigin if set, else this transform.
     * Wire captureOrigin to the field-ring sprite's transform so the visible
     * ring and the actual capture/force surface line up.
     *
     * Forces are applied via Creature.ApplyForce (push model). We never cache
     * creature references across frames — OverlapCircleNonAlloc is the
     * authoritative query each tick, so destroyed or now-bound creatures are
     * naturally excluded.
     */
    [DisallowMultipleComponent]
    public class HolobiontForceField : MonoBehaviour
    {
        // ----- Config -----
        [Header("Config")]
        [Tooltip("The holobiont manager this field belongs to.")]
        [SerializeField] private HolobiontManager manager;

        [Tooltip("Layer mask for OverlapCircle queries. Default Everything is fine when creatures are the only relevant colliders.")]
        [SerializeField] private LayerMask creatureLayers = ~0;

        [Tooltip("Buffer size for OverlapCircleNonAlloc. Raise this if more than ~32 creatures might be in range at once.")]
        [Min(1), SerializeField] private int overlapBufferSize = 32;

        [Tooltip("Optional. Origin for capture and field-force overlap queries. When set, queries are centered here instead of this GameObject — wire to the field-ring sprite's transform so the visible ring matches the actual capture surface.")]
        [SerializeField] private Transform captureOrigin;

        // ----- Debug -----
        [Header("Debug")]
        [Tooltip("Live attraction/repulsion radius (world units).")]
        [SerializeField, ReadOnly] private float currentRadius;

        [Tooltip("True while hold-exhale capture is active.")]
        [SerializeField, ReadOnly] private bool isCapturing;

        [Tooltip("True while hold-inhale shed mode is active.")]
        [SerializeField, ReadOnly] private bool isShedding;

        [Tooltip("Seconds the current breath-hold has been sustained (capture or shed). Resets to 0 the frame the hold ends.")]
        [SerializeField, ReadOnly] private float holdDuration;

        [Tooltip("Hold charge (0..1) sampled from BreathFieldConfig.holdChargeCurve. Drives the invisible charge-field radius and force magnitude.")]
        [SerializeField, ReadOnly] private float holdCharge;

        [Tooltip("Invisible charge-field radius (world units). Equals the visible breath ring at charge=0, swells outward by holdRadiusBonus at charge=1. Only the hold-charge attract/repel force uses this; the visible ring and capture surface stay on currentRadius.")]
        [SerializeField, ReadOnly] private float chargeFieldRadius;

        // ----- State -----
        private BreathFieldConfig field;
        private Collider2D[] hits;
        private bool wasInhaleHold;
        private bool wasExhaleHold;

        // Highlight bookkeeping. Lists hold the creatures currently flagged so we can
        // diff next tick — anything in `prev` but not in `curr` gets its flag cleared.
        private readonly List<Creature> bondCandidates     = new List<Creature>();
        private readonly List<Creature> prevBondCandidates = new List<Creature>();
        private Creature shedCandidate;

        // Scratch list reused across closest-N selection to avoid per-tick allocations.
        private readonly List<Creature> nearestScratch = new List<Creature>();

        // ----- Public read-only -----
        public float CurrentRadius     => currentRadius;
        public bool  IsCapturing       => isCapturing;
        public bool  IsShedding        => isShedding;
        public float HoldDuration      => holdDuration;
        public float HoldCharge        => holdCharge;
        public float ChargeFieldRadius => chargeFieldRadius;

        // ----- Lifecycle -----
        private void Awake()
        {
            hits = new Collider2D[Mathf.Max(1, overlapBufferSize)];
        }

        private void OnEnable()
        {
            field = manager && manager.Config ? manager.Config.BreathField : null;
            if (!manager || !manager.Config || !field)
            {
                Debug.LogError($"{nameof(HolobiontForceField)} on {name}: missing {nameof(HolobiontManager)}, its {nameof(HolobiontConfig)}, or {nameof(BreathFieldConfig)}.", this);
                enabled = false;
                return;
            }
        }

        private void OnDisable()
        {
            ClearAllHighlights();
        }

        private void FixedUpdate()
        {
            var breath = manager ? manager.Breath : null;
            if (breath == null)
            {
                currentRadius     = 0f;
                isCapturing       = false;
                isShedding        = false;
                wasInhaleHold     = false;
                wasExhaleHold     = false;
                holdDuration      = 0f;
                holdCharge        = 0f;
                chargeFieldRadius = 0f;
                ClearAllHighlights();
                return;
            }

            float depthFactor = Mathf.Lerp(field.baseRadius, field.maxRadius, Mathf.Clamp01(breath.Depth));
            float hubBonus    = manager.State.HubCount * field.radiusPerHub;
            float phaseFactor = Mathf.Max(0f, field.breathPhaseToRadius.Evaluate(breath.Phase));
            currentRadius = (depthFactor + hubBonus) * phaseFactor;

            isCapturing = breath.IsExhaleHold;
            isShedding  = breath.IsInhaleHold;

            // Charge accumulates during either hold; resets the frame the hold ends.
            // Held in seconds, then mapped through the curve so designers shape the ramp
            // without us having to expose the raw timer.
            if (isCapturing || isShedding)
            {
                holdDuration += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(holdDuration / Mathf.Max(0.0001f, field.holdChargeTime));
                holdCharge = Mathf.Clamp01(field.holdChargeCurve.Evaluate(t));
            }
            else
            {
                holdDuration = 0f;
                holdCharge   = 0f;
            }

            // Invisible charge field: starts at the breath ring, swells outward as charge builds.
            // The visible ring stays on currentRadius — only this radius reaches farther creatures.
            chargeFieldRadius = currentRadius + field.holdRadiusBonus * holdCharge;

            // Rising-edge bond batch: take the N closest unbound creatures inside the
            // breath ring and bond them in one shot. No further bonds during this hold,
            // even if creatures drift in — that's the per-hold cap doing its job.
            if (isCapturing && !wasExhaleHold)
                BondClosestInRange();

            if (isCapturing)
            {
                if (holdCharge > 0f)
                    ApplyRadialForce(field.holdAttractStrength * holdCharge, chargeFieldRadius);
            }
            else if (isShedding)
            {
                // Fire on the rising edge so a sustained hold doesn't drain the
                // network in one frame. Each fresh hold-inhale costs one creature.
                if (!wasInhaleHold) manager.ShedMostStressed();
                if (holdCharge > 0f)
                    ApplyRadialForce(-field.holdRepulsionStrength * holdCharge, chargeFieldRadius);
            }
            else
            {
                ApplyFieldForces(breath.Phase);
            }

            // Passive highlights: refreshed every tick regardless of hold state, so the
            // player always sees who would bond / shed next. Run AFTER capture/shed so the
            // view reflects the post-bond state on the same frame.
            RefreshBondCandidateHighlights();
            RefreshShedCandidateHighlight();

            wasInhaleHold = isShedding;
            wasExhaleHold = isCapturing;
        }

        // ----- Private -----
        private Vector2 QueryCenter => captureOrigin ? (Vector2)captureOrigin.position : (Vector2)transform.position;

        private void BondClosestInRange()
        {
            int n = ComputeBondsThisHold();
            if (n <= 0) return;

            CollectClosestUnbound(n, nearestScratch);
            for (int i = 0; i < nearestScratch.Count; i++)
            {
                var c = nearestScratch[i];
                if (!c) continue;
                manager.TryBond(c);
            }
            nearestScratch.Clear();
        }

        // Effective per-hold cap: the smaller of "remaining capacity" and
        // "base + floor(perHub × hubCount)". Capacity is the hard ceiling; the
        // hub-scaled term is the soft throttle that keeps holds meaningful.
        private int ComputeBondsThisHold()
        {
            int remainingCapacity = manager.State.CarryingCapacity - manager.State.CreatureCount;
            if (remainingCapacity <= 0) return 0;

            int hubBonus = Mathf.FloorToInt(field.bondsPerHub * manager.State.HubCount);
            int perHold  = Mathf.Max(0, field.baseBondsPerHold + hubBonus);
            return Mathf.Min(remainingCapacity, perHold);
        }

        // Collects up to `count` closest unbound creatures inside `currentRadius`,
        // sorted nearest-first. Reuses `dest` (cleared on entry) to avoid allocations.
        private void CollectClosestUnbound(int count, List<Creature> dest)
        {
            dest.Clear();
            if (count <= 0 || currentRadius <= 0f) return;

            Vector2 center = QueryCenter;
            int hitCount = Physics2D.OverlapCircleNonAlloc(center, currentRadius, hits, creatureLayers);
            if (hitCount == 0) return;

            // Insertion into a small sorted list — N is expected to be tiny (1..a few).
            // For each unbound creature, keep only the `count` smallest distances.
            for (int i = 0; i < hitCount; i++)
            {
                var hit = hits[i];
                if (!hit) continue;
                var creature = hit.GetComponentInParent<Creature>();
                if (!creature || creature.Status != BondStatus.Unbound) continue;

                float dSq = ((Vector2)creature.transform.position - center).sqrMagnitude;

                int insertAt = dest.Count;
                for (int j = 0; j < dest.Count; j++)
                {
                    float existingSq = ((Vector2)dest[j].transform.position - center).sqrMagnitude;
                    if (dSq < existingSq) { insertAt = j; break; }
                }

                if (insertAt < count)
                {
                    dest.Insert(insertAt, creature);
                    if (dest.Count > count) dest.RemoveAt(dest.Count - 1);
                }
            }
        }

        // Passive bond-candidate preview: shows the player which unbound creatures
        // would be captured if they paused right now. Always runs (even outside holds).
        // Diff against last tick's set so we only toggle highlights that actually changed.
        private void RefreshBondCandidateHighlights()
        {
            int n = ComputeBondsThisHold();
            CollectClosestUnbound(n, bondCandidates);

            // Clear highlights that fell out of the new set.
            for (int i = 0; i < prevBondCandidates.Count; i++)
            {
                var c = prevBondCandidates[i];
                if (!c) continue;
                if (!bondCandidates.Contains(c)) c.SetBondCandidate(false);
            }

            // Activate highlights for the new set (idempotent on Creature side).
            for (int i = 0; i < bondCandidates.Count; i++)
            {
                var c = bondCandidates[i];
                if (c) c.SetBondCandidate(true);
            }

            // Swap buffers — make this tick's set the next tick's "previous".
            prevBondCandidates.Clear();
            prevBondCandidates.AddRange(bondCandidates);
        }

        // Passive shed-candidate indicator: glows on the most-stressed bonded creature
        // so the player knows who'll go on the next inhale-hold (and during cascade).
        private void RefreshShedCandidateHighlight()
        {
            Creature pick = null;
            float worst = float.NegativeInfinity;
            var bonded = manager.State.BondedCreatures;
            for (int i = 0; i < bonded.Count; i++)
            {
                var c = bonded[i];
                if (!c) continue;
                if (c.Stress > worst)
                {
                    worst = c.Stress;
                    pick  = c;
                }
            }

            if (pick == shedCandidate) return;
            if (shedCandidate) shedCandidate.SetShedCandidate(false);
            shedCandidate = pick;
            if (shedCandidate) shedCandidate.SetShedCandidate(true);
        }

        private void ClearAllHighlights()
        {
            for (int i = 0; i < prevBondCandidates.Count; i++)
            {
                var c = prevBondCandidates[i];
                if (c) c.SetBondCandidate(false);
            }
            prevBondCandidates.Clear();
            bondCandidates.Clear();

            if (shedCandidate) shedCandidate.SetShedCandidate(false);
            shedCandidate = null;
        }

        private void ApplyFieldForces(float phase)
        {
            float strength;
            if      (phase > 0f) strength =  field.attractionStrength;
            else if (phase < 0f) strength = -field.repulsionStrength;
            else return; // dead zone — no force when breath is steady

            ApplyRadialForce(strength, currentRadius);
        }

        // Sign convention: positive strength pulls toward QueryCenter, negative pushes outward.
        // Falloff curve is sampled on normalized distance against the supplied radius, so the
        // ambient path uses the breath ring while the hold-charge path uses the swelling field.
        private void ApplyRadialForce(float signedStrength, float radius)
        {
            if (radius <= 0f || signedStrength == 0f) return;

            Vector2 center = QueryCenter;
            int count = Physics2D.OverlapCircleNonAlloc(center, radius, hits, creatureLayers);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (!hit) continue;
                var creature = hit.GetComponentInParent<Creature>();
                if (!creature || creature.Status != BondStatus.Unbound) continue;

                Vector2 toCenter = center - (Vector2)creature.transform.position;
                float dist = toCenter.magnitude;
                if (dist < 0.001f) continue;

                float t       = Mathf.Clamp01(dist / radius);
                float falloff = field.attractionFalloff.Evaluate(t);
                Vector2 dir   = toCenter / dist;
                creature.ApplyForce(dir * signedStrength * falloff);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isCapturing ? new Color(1f,   0.8f, 0f,   0.6f)
                         : isShedding  ? new Color(0.4f, 0.7f, 1f,   0.6f)
                                       : new Color(1f,   1f,   1f,   0.25f);
            Gizmos.DrawWireSphere(QueryCenter, currentRadius);

            if ((isCapturing || isShedding) && chargeFieldRadius > currentRadius)
            {
                Gizmos.color = new Color(1f, 0.3f, 0.8f, 0.4f);
                Gizmos.DrawWireSphere(QueryCenter, chargeFieldRadius);
            }
        }
#endif
    }
}
