using UnityEngine;

namespace Holobiont
{
    /*
     * Tuning data for the holobiont's breath-driven attraction/repulsion field
     * and its ring visualization. Single source for everything HolobiontForceField
     * and HolobiontView read about the field — geometry, forces, and ring colors.
     *
     * Named "BreathField" to distinguish it from the environment FlowField.
     * Owned by HolobiontConfig (referenced from there).
     */
    [CreateAssetMenu(fileName = "BreathFieldConfig", menuName = "Game/Breath Field Config")]
    public class BreathFieldConfig : ScriptableObject
    {
        // ----- Geometry -----
        [Header("Geometry")]
        [Tooltip("World-units field reach at minimum breath depth.")]
        [Min(0f)] public float baseRadius = 3f;

        [Tooltip("World-units field reach at maximum breath depth.")]
        [Min(0f)] public float maxRadius = 8f;

        [Tooltip("Additional world-units of reach contributed per bonded hub. Added to the depth-driven base/max range before phase modulation, so hubs grow the holobiont's reach without changing how breath shapes it.")]
        [Min(0f)] public float radiusPerHub = 0f;

        [Tooltip("Breath phase (-1 inhale → +1 exhale, sampled at -1, 0, +1) mapped to a field-radius multiplier. Default contracts the field on inhale, expands on exhale.")]
        public AnimationCurve breathPhaseToRadius = new AnimationCurve(
            new Keyframe(-1f, 0.5f),
            new Keyframe( 0f, 1.0f),
            new Keyframe(+1f, 1.4f));

        // ----- Forces -----
        [Header("Forces")]
        [Tooltip("Force magnitude applied to unbound creatures while exhaling. Positive = pulled toward center.")]
        [Min(0f)] public float attractionStrength = 6f;

        [Tooltip("Force magnitude applied to unbound creatures while inhaling. Always pushes away from center.")]
        [Min(0f)] public float repulsionStrength = 6f;

        [Tooltip("Distance from center, normalized to current radius (0 = center, 1 = edge), mapped to a force multiplier. Default linear: max pull at the edge, no pull at the center, so creatures don't oscillate through origin.")]
        public AnimationCurve attractionFalloff = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        // ----- Hold Charge -----
        [Header("Hold Charge")]
        [Tooltip("Seconds of breath-hold needed to reach full charge (1.0). Charge ramps from 0 at hold start, resets to 0 the frame the hold ends.")]
        [Min(0.01f)] public float holdChargeTime = 1.5f;

        [Tooltip("Hold time normalized to holdChargeTime (0..1) mapped to charge strength (0..1). Default linear; ease-in for slower buildup, ease-out for snappier feel.")]
        public AnimationCurve holdChargeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Max attraction force added on top of the discrete bond capture during exhale-hold, scaled by charge.")]
        [Min(0f)] public float holdAttractStrength = 12f;

        [Tooltip("Max repulsion force added on top of the discrete shed during inhale-hold, scaled by charge.")]
        [Min(0f)] public float holdRepulsionStrength = 12f;

        [Tooltip("Extra radius (world units) added at full charge — field swells outward from the current breath-driven radius so longer holds reach farther creatures.")]
        [Min(0f)] public float holdRadiusBonus = 6f;

        // ----- Bond Throttle -----
        [Header("Bond Throttle")]
        [Tooltip("Base number of creatures bonded per exhale-hold rising edge before hub bonuses. The closest N unbound creatures inside the breath ring are captured in one batch; further creatures drifting in during the same hold are ignored.")]
        [Min(0)] public int baseBondsPerHold = 1;

        [Tooltip("Extra bonds per hold contributed per bonded hub (floored). Ties hub investment to the same scaling axis as carrying capacity and field reach.")]
        [Min(0f)] public float bondsPerHub = 0.5f;

        // ----- Ring Visuals -----
        [Header("Ring Visuals")]
        [Tooltip("Idle ring color (no capture, no shed).")]
        public Color idleColor    = Color.white;

        [Tooltip("Ring color while hold-exhale capture is active.")]
        public Color captureColor = new Color(1f,   0.85f, 0.3f);

        [Tooltip("Ring color while hold-inhale shed is active.")]
        public Color shedColor    = new Color(0.4f, 0.7f,  1f);

        [Tooltip("Ring master alpha at full inhale (phase = -1). Drives the shader's _BaseColor.a continuously across all states.")]
        [Range(0f, 1f)] public float minRingAlpha = 0.05f;

        [Tooltip("Ring master alpha at full exhale (phase = +1). Drives the shader's _BaseColor.a continuously across all states.")]
        [Range(0f, 1f)] public float maxRingAlpha = 0.55f;
    }
}
