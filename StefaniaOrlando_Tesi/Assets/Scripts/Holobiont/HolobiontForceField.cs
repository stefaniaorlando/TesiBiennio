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
     *   - hold-exhale          → capture every unbound creature inside currentRadius.
     *   - hold-inhale (rising) → manager.ShedMostStressed().
     *   - otherwise             → push attraction (phase > 0) or repulsion
     *     (phase < 0) onto every unbound creature inside currentRadius, scaled
     *     by attractionFalloff.
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

        // ----- State -----
        private BreathFieldConfig field;
        private Collider2D[] hits;
        private bool wasInhaleHold;

        // ----- Public read-only -----
        public float CurrentRadius => currentRadius;
        public bool  IsCapturing   => isCapturing;
        public bool  IsShedding    => isShedding;

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

        private void FixedUpdate()
        {
            var breath = manager ? manager.Breath : null;
            if (breath == null)
            {
                currentRadius = 0f;
                isCapturing   = false;
                isShedding    = false;
                wasInhaleHold = false;
                return;
            }

            float depthFactor = Mathf.Lerp(field.baseRadius, field.maxRadius, Mathf.Clamp01(breath.Depth));
            float hubBonus    = manager.State.HubCount * field.radiusPerHub;
            float phaseFactor = Mathf.Max(0f, field.breathPhaseToRadius.Evaluate(breath.Phase));
            currentRadius = (depthFactor + hubBonus) * phaseFactor;

            isCapturing = breath.IsExhaleHold;
            isShedding  = breath.IsInhaleHold;

            if (isCapturing)
            {
                CaptureInRange();
            }
            else if (isShedding)
            {
                // Fire on the rising edge so a sustained hold doesn't drain the
                // network in one frame. Each fresh hold-inhale costs one creature.
                if (!wasInhaleHold) manager.ShedMostStressed();
            }
            else
            {
                ApplyFieldForces(breath.Phase);
            }

            wasInhaleHold = isShedding;
        }

        // ----- Private -----
        private Vector2 QueryCenter => captureOrigin ? (Vector2)captureOrigin.position : (Vector2)transform.position;

        private void CaptureInRange()
        {
            if (currentRadius <= 0f) return;

            Vector2 center = QueryCenter;
            int count = Physics2D.OverlapCircleNonAlloc(center, currentRadius, hits, creatureLayers);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (!hit) continue;
                var creature = hit.GetComponentInParent<Creature>();
                if (!creature || creature.Status != BondStatus.Unbound) continue;
                manager.TryBond(creature);
            }
        }

        private void ApplyFieldForces(float phase)
        {
            float strength;
            if      (phase > 0f) strength =  field.attractionStrength;
            else if (phase < 0f) strength = -field.repulsionStrength;
            else return; // dead zone — no force when breath is steady

            if (currentRadius <= 0f) return;

            Vector2 center = QueryCenter;
            int count = Physics2D.OverlapCircleNonAlloc(center, currentRadius, hits, creatureLayers);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (!hit) continue;
                var creature = hit.GetComponentInParent<Creature>();
                if (!creature || creature.Status != BondStatus.Unbound) continue;

                Vector2 toCenter = center - (Vector2)creature.transform.position;
                float dist = toCenter.magnitude;
                if (dist < 0.001f) continue;

                float t       = Mathf.Clamp01(dist / currentRadius);
                float falloff = field.attractionFalloff.Evaluate(t);
                Vector2 dir   = toCenter / dist;
                creature.ApplyForce(dir * strength * falloff);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isCapturing ? new Color(1f,   0.8f, 0f,   0.6f)
                         : isShedding  ? new Color(0.4f, 0.7f, 1f,   0.6f)
                                       : new Color(1f,   1f,   1f,   0.25f);
            Gizmos.DrawWireSphere(QueryCenter, currentRadius);
        }
#endif
    }
}
