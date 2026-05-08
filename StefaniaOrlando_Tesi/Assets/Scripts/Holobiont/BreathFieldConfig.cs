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
