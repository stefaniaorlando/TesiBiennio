using UnityEngine;

namespace Holobiont
{
    /*
     * Bare-minimum visual layer for the holobiont. Reads from HolobiontManager
     * and (optionally) HolobiontForceField. Never writes to game state.
     *
     * Drives:
     *   Core sprite — scale pulses with breath phase; color blends from
     *     cascadeColor at low energy to stableColor at full energy.
     *   Field ring — driven by the Holobiont/ForceFieldRing shader on a unit
     *     white square sprite. Local scale = currentRadius × 2; per-instance
     *     MPB pushes _State (0 idle / 1 capture / 2 shed), _CurrentRadius
     *     (so the shader's wave amplitude divides out and stays world-
     *     constant), and the three state colors. Ring colors and the min/max
     *     master alpha are read from BreathFieldConfig (manager.Config.BreathField)
     *     so all force-field tuning lives in one asset. Master alpha on
     *     _BaseColor.a is breath-modulated continuously across all states —
     *     the per-state visuals (captured interior, outflow shimmer) are
     *     layered on top by the shader itself. NO C# alpha pulse during
     *     capture/shed (that was a flicker we banned).
     *
     * Both sprite references are optional — leave one unassigned and that
     * block becomes a no-op. Convenient when bringing visuals up incrementally.
     */
    [DisallowMultipleComponent]
    public class HolobiontView : MonoBehaviour
    {
        // ----- Sources -----
        [Header("Sources")]
        [Tooltip("Manager whose state drives the core visualization.")]
        [SerializeField] private HolobiontManager manager;

        [Tooltip("Force field whose radius drives the field-ring sprite. Optional.")]
        [SerializeField] private HolobiontForceField forceField;

        // ----- Sprites -----
        [Header("Sprites")]
        [Tooltip("Core organism sprite at the holobiont center. Optional.")]
        [SerializeField] private SpriteRenderer coreSprite;

        [Tooltip("Field ring sprite (a soft circle or radial gradient). Author it as a unit-diameter circle so scale 1 = radius 0.5.")]
        [SerializeField] private SpriteRenderer fieldRingSprite;

        // ----- Tuning -----
        [Header("Core Tuning")]
        [Tooltip("Color of the core when energy is full and the network is stable.")]
        [SerializeField] private Color stableColor  = new Color(0.7f,  0.95f, 0.8f);

        [Tooltip("Color of the core during cascade / death (low energy).")]
        [SerializeField] private Color cascadeColor = new Color(0.45f, 0.18f, 0.22f);

        [Tooltip("Scale amplitude as a fraction of base scale, driven by breath phase.")]
        [Range(0f, 0.5f), SerializeField] private float coreBreathAmplitude = 0.12f;

        [Header("Phase Tuning")]
        [Tooltip("Pulse rate (Hz) when Phase is Declining. Subtle warning beat.")]
        [Range(0.1f, 5f), SerializeField] private float decliningPulseRate = 0.8f;

        [Tooltip("How strongly the Declining pulse darkens toward cascadeColor at peak.")]
        [Range(0f, 1f), SerializeField] private float decliningPulseAmount = 0.18f;

        [Tooltip("Pulse rate (Hz) during CascadeFailure. Faster, more alarming.")]
        [Range(0.1f, 8f), SerializeField] private float cascadePulseRate = 2.5f;

        [Tooltip("How strongly the cascade pulse darkens toward cascadeColor at peak.")]
        [Range(0f, 1f), SerializeField] private float cascadePulseAmount = 0.5f;

        [Tooltip("How far the core color is darkened toward black after Death. 1 = full black.")]
        [Range(0f, 1f), SerializeField] private float deadDarkenAmount = 0.7f;

        [Tooltip("Fraction of base scale lost on Death (visual collapse).")]
        [Range(0f, 0.9f), SerializeField] private float deadShrinkAmount = 0.4f;

        // ----- Cached property IDs -----
        private static readonly int IDState         = Shader.PropertyToID("_State");
        private static readonly int IDCurrentRadius = Shader.PropertyToID("_CurrentRadius");
        private static readonly int IDBaseColor     = Shader.PropertyToID("_BaseColor");
        private static readonly int IDCaptureColor  = Shader.PropertyToID("_CaptureColor");
        private static readonly int IDShedColor     = Shader.PropertyToID("_ShedColor");

        private MaterialPropertyBlock fieldRingMpb;

        // ----- Lifecycle -----
        private void Reset()
        {
            manager    = GetComponent<HolobiontManager>();
            forceField = GetComponent<HolobiontForceField>();
        }

        private void OnEnable()
        {
            if (fieldRingMpb is null) fieldRingMpb = new MaterialPropertyBlock();

            // Sprite tint becomes a no-op so the MPB _BaseColor is the only color
            // driver — same convention as CreatureView. Idempotent: safe even when
            // the renderer isn't wired yet (UpdateFieldRing also early-returns).
            if (fieldRingSprite) fieldRingSprite.color = Color.white;
        }

        private void Update()
        {
            if (!manager) return;
            UpdateCore();
            UpdateFieldRing();
        }

        // ----- Private -----
        private void UpdateCore()
        {
            if (!coreSprite) return;

            float breathPhase = manager.Breath != null ? manager.Breath.Phase : 0f;
            float scale = 1f + breathPhase * coreBreathAmplitude;

            HolobiontPhase phaseEnum = manager.State.Phase;
            if (phaseEnum == HolobiontPhase.Dead)
            {
                // Dead snaps to a smaller silhouette — the holobiont has collapsed.
                scale *= 1f - deadShrinkAmount;
            }
            coreSprite.transform.localScale = Vector3.one * scale;

            float energyT = Mathf.Clamp01(manager.State.EnergyNormalized);
            Color color = Color.Lerp(cascadeColor, stableColor, energyT);

            // Phase modulation rides on top of the energy-driven base color.
            switch (phaseEnum)
            {
                case HolobiontPhase.Declining:
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * decliningPulseRate * Mathf.PI * 2f);
                    color = Color.Lerp(color, cascadeColor, pulse * decliningPulseAmount);
                    break;
                }
                case HolobiontPhase.CascadeFailure:
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * cascadePulseRate * Mathf.PI * 2f);
                    color = Color.Lerp(color, cascadeColor, pulse * cascadePulseAmount);
                    break;
                }
                case HolobiontPhase.Dead:
                {
                    color = Color.Lerp(color, Color.black, deadDarkenAmount);
                    break;
                }
            }

            coreSprite.color = color;
        }

        private void UpdateFieldRing()
        {
            if (!fieldRingSprite || !forceField) return;
            var field = manager.Config ? manager.Config.BreathField : null;
            if (!field) return;

            float radius   = forceField.CurrentRadius;
            float diameter = radius * 2f;
            fieldRingSprite.transform.localScale = new Vector3(diameter, diameter, 1f);

            int state = forceField.IsCapturing ? 1
                      : forceField.IsShedding  ? 2
                                                : 0;

            // Master alpha is a continuous breath-modulated lerp across all states.
            // The shader layers per-state visuals (captured interior, outflow shimmer)
            // on top of this baseline rather than snapping alpha values per state.
            float phase = manager.Breath != null ? manager.Breath.Phase : 0f;
            float lerpT = Mathf.InverseLerp(-1f, 1f, phase);
            float alpha = Mathf.Lerp(field.minRingAlpha, field.maxRingAlpha, lerpT);

            Color baseWithAlpha = field.idleColor;
            baseWithAlpha.a = alpha;

            fieldRingSprite.GetPropertyBlock(fieldRingMpb);
            fieldRingMpb.SetFloat(IDState,         state);
            fieldRingMpb.SetFloat(IDCurrentRadius, Mathf.Max(0.0001f, radius));
            fieldRingMpb.SetColor(IDBaseColor,     baseWithAlpha);
            fieldRingMpb.SetColor(IDCaptureColor,  field.captureColor);
            fieldRingMpb.SetColor(IDShedColor,     field.shedColor);
            fieldRingSprite.SetPropertyBlock(fieldRingMpb);
        }
    }
}
