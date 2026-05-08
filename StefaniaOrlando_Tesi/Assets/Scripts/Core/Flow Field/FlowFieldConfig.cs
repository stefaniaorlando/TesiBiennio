using UnityEngine;

namespace Holobiont
{
    /*
     * Tuning for the FlowField — Perlin sampling, base speed, inward bias, gizmos.
     * Env-driven modulation (humidity/temperature → speed/turbulence) lives on
     * EnvironmentFlowFieldView, not here.
     *
     * Create via: Right-click in Project → Create → Game → Flow Field Config
     */

    [CreateAssetMenu(fileName = "FlowFieldConfig", menuName = "Game/Flow Field Config")]
    public class FlowFieldConfig : ScriptableObject
    {
        // ----- Sampling -----
        [Header("Sampling")]
        [Tooltip("Spatial frequency of the base noise. Smaller = bigger eddies.")]
        [Min(0.0001f)] public float noiseScale = 0.15f;

        [Tooltip("Spatial frequency of the turbulence layer. Larger than noiseScale = finer chaos.")]
        [Min(0.0001f)] public float turbulenceNoiseScale = 0.6f;

        [Tooltip("How fast the field evolves over time (multiplier on Time.time).")]
        [Min(0f)] public float temporalScale = 0.1f;

        // ----- Magnitude -----
        [Header("Magnitude")]
        [Tooltip("World-units-per-second magnitude before external modulation.")]
        [Min(0f)] public float baseFlowSpeed = 2f;

        // ----- Inward Bias -----
        [Header("Inward Bias")]
        [Tooltip("If enabled, an extra inward velocity is added based on distance from the FlowField origin. Optional containment without walls.")]
        public bool enableInwardBias = true;

        [Tooltip("Distance from origin (world units, x) → inward speed (world units / sec, y). Curve y is the magnitude pulling toward origin at each distance.")]
        public AnimationCurve inwardBiasByDistance = DefaultInwardBias();

        // ----- Gizmos -----
        [Header("Gizmos")]
        [Tooltip("Number of arrows per axis in the scene-view overlay.")]
        [Min(2)] public int gizmoGridResolution = 16;

        [Tooltip("Half-extent of the gizmo overlay around the FlowField transform (square).")]
        [Min(0.1f)] public float gizmoExtent = 10f;

        [Tooltip("Multiplier on each gizmo arrow's drawn length.")]
        [Min(0f)] public float gizmoArrowScale = 0.3f;

        [Tooltip("Color at zero magnitude.")]
        public Color gizmoColorLow  = new Color(0.2f, 0.6f, 1.0f, 0.3f);

        [Tooltip("Color at max magnitude (in the current sample).")]
        public Color gizmoColorHigh = new Color(1.0f, 0.4f, 0.2f, 1.0f);

        // ----- Defaults -----
        // Slight bowl: nothing in the middle, gentle tug at the rim. Rim pull
        // ~half of baseFlowSpeed so creatures don't hug the edges without the
        // bias feeling like an invisible wall.
        private static AnimationCurve DefaultInwardBias()
            => Smooth(new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(8f, 0.1f),
                new Keyframe(15f, 0.5f)));

        private static AnimationCurve Smooth(AnimationCurve c)
        {
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            return c;
        }
    }
}
