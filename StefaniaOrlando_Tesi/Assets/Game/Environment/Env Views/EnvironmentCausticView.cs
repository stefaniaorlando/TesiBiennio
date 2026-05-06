using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Holobiont
{
    /*
     * Drives an auxiliary sprite Light2D (with a noise-texture cookie) as a
     * subtle moving "caustic" pattern. The light fades in with humidity and
     * drifts in position + slowly rotates so its noise pattern animates across
     * the scene — selling the wet-medium look without a volumetric pass.
     *
     * Reads:  Humidity.
     * Writes: the assigned Light2D's intensity, plus its transform.localPosition
     *         and transform.localRotation (autonomous Perlin drift around the
     *         pose authored on that transform). The view can live on the Light2D's
     *         GameObject (auto-resolved) or on any other GO with the ref dragged in.
     *
     * Motion is intentionally NOT env-driven in v1 — adding Temperature → speed
     * is a future tweak. Drift uses Perlin noise to match the codebase pattern
     * (FlowField, EnvironmentDrift jitter); a Lissajous orbit would read as too
     * mechanical for caustics, which are stochastic in nature.
     *
     * Pause behaviour: motion uses Time.time (not GameClock), so the caustic
     * drift keeps animating while the game is paused — it's a visual layer.
     * Intensity tracks humidity, which freezes naturally with GameClock.
     */
    [DisallowMultipleComponent]
    public class EnvironmentCausticView : MonoBehaviour
    {
        // Same decorrelation constants the FlowField uses, so multiple noise-driven
        // systems sample disjoint regions of Perlin space and don't sync up.
        private const float COORD_BIAS  = 10000f;
        private const float SEED_STRIDE = 1.7320508f; // sqrt(3)

        // ----- Config -----
        [Header("Source")]
        [Tooltip("Environment whose Humidity channel drives this view.")]
        [SerializeField] private EnvironmentManager environment;

        [Tooltip("Sprite Light2D this view drives. Auto-resolved from this GameObject if left empty.")]
        [SerializeField] private Light2D spriteLight;

        // ----- ← Humidity -----
        [Header("← Humidity")]
        [Tooltip("Light intensity when fully dry. Typically 0 — caustics belong to wet medium.")]
        [Min(0f)] [SerializeField] private float intensityDry = 0f;

        [Tooltip("Light intensity when fully humid. Keep low — this is a subtle layer, not a feature.")]
        [Min(0f)] [SerializeField] private float intensityWet = 0.6f;

        // ----- Motion (autonomous) -----
        [Header("Motion (autonomous)")]
        [Tooltip("Half-extent of the position drift in local units. Light orbits its authored position within this radius.")]
        [Min(0f)] [SerializeField] private float driftRadius = 0.5f;

        [Tooltip("Speed of the Perlin drift through noise space. Higher = the caustic pattern moves across the scene faster.")]
        [Min(0f)] [SerializeField] private float driftSpeed = 0.1f;

        [Tooltip("Constant rotation speed in degrees per second. Slow values read as the noise texture turning in place.")]
        [SerializeField] private float rotationSpeed = 3f;

        [Tooltip("Per-instance seed. Different seeds → decorrelated drift, useful if multiple caustic lights coexist.")]
        [SerializeField] private int seed = 0;

        // ----- State -----
        private Transform target;
        private Vector3 origin;
        private Quaternion originRotation;

        // ----- Lifecycle -----
        private void Reset() => spriteLight = GetComponent<Light2D>();

        private void OnEnable()
        {
            if (!spriteLight) spriteLight = GetComponent<Light2D>();
            if (!environment || !spriteLight)
            {
                Debug.LogError($"{nameof(EnvironmentCausticView)} requires {nameof(EnvironmentManager)} and a {nameof(Light2D)} reference.", this);
                enabled = false;
                return;
            }
            target = spriteLight.transform;
            origin = target.localPosition;
            originRotation = target.localRotation;
        }

        private void Update()
        {
            // ← Humidity
            float h = environment.HumidityNormalized;
            spriteLight.intensity = Mathf.Lerp(intensityDry, intensityWet, h);

            // Autonomous motion: Perlin drift in XY around the authored pose.
            // 2D Perlin input is (t + axis-offset, fixed seed-offset), one sample per axis.
            float seedOff = seed * SEED_STRIDE;
            float t = Time.time * driftSpeed + COORD_BIAS;
            float nx = Mathf.PerlinNoise(t,           seedOff         ) * 2f - 1f;
            float ny = Mathf.PerlinNoise(t + 1031.7f, seedOff + 47.2f ) * 2f - 1f;
            target.localPosition = origin + new Vector3(nx, ny, 0f) * driftRadius;

            // Slow rotation so the noise texture turns in place.
            target.localRotation = originRotation * Quaternion.Euler(0f, 0f, Time.time * rotationSpeed);
        }
    }
}
