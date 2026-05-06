using UnityEngine;

namespace Holobiont
{
    /*
     * 2D Perlin-noise velocity field. Owns the sampling math; modulation is
     * driven externally via SpeedMultiplier / TurbulenceAmount setters
     * (see EnvironmentFlowFieldView for the env→modulation mapping).
     *
     * Sampling is continuous (no grid cache) — cheap enough for tens of
     * consumers. Revisit if particle counts climb.
     *
     * Singleton: anyone can call FlowField.Instance.GetFlowAtPosition(p).
     * OnDrawGizmosSelected shows an NxN arrow grid in the scene view.
     */

    [DefaultExecutionOrder(-40)]
    public class FlowField : MonoBehaviour
    {
        public static FlowField Instance { get; private set; }

        // ----- Config -----
        [Header("Config")]
        [Tooltip("Tuning asset.")]
        [SerializeField] private FlowFieldConfig config;

        [Header("Modulation")]
        [Tooltip("Multiplier on baseFlowSpeed. Overwritten every frame when EnvironmentFlowFieldView (or any other driver) is active; the inspector value sticks only with no driver.")]
        [Min(0f)] [SerializeField] private float speedMultiplier = 1f;

        [Tooltip("Amount of turbulence added on top of the base flow (0 = none). Overwritten every frame by EnvironmentFlowFieldView when active.")]
        [Min(0f)] [SerializeField] private float turbulenceAmount = 0f;

        public float SpeedMultiplier  => speedMultiplier;
        public float TurbulenceAmount => turbulenceAmount;

        /// <summary>Snapshot of the per-frame parameters a shader-sync layer needs to mirror this field's current behavior.</summary>
        public readonly struct WarpParams
        {
            public readonly float NoiseScale;
            public readonly float TurbulenceNoiseScale;
            public readonly float TemporalScale;
            public readonly float EffectiveSpeed;     // baseFlowSpeed * speedMultiplier
            public readonly float TurbulenceAmount;
            public WarpParams(float noiseScale, float turbulenceNoiseScale, float temporalScale, float effectiveSpeed, float turbulenceAmount)
            {
                NoiseScale           = noiseScale;
                TurbulenceNoiseScale = turbulenceNoiseScale;
                TemporalScale        = temporalScale;
                EffectiveSpeed       = effectiveSpeed;
                TurbulenceAmount     = turbulenceAmount;
            }
        }

        /// <summary>Returns true and fills <paramref name="p"/> when a config is bound; false otherwise (caller skips the frame).</summary>
        public bool TryGetWarpParams(out WarpParams p)
        {
            if (!config) { p = default; return false; }
            p = new WarpParams(
                config.noiseScale,
                config.turbulenceNoiseScale,
                config.temporalScale,
                config.baseFlowSpeed * speedMultiplier,
                turbulenceAmount);
            return true;
        }

        // ----- Constants -----
        // Decorrelation offsets so X/Y axes and base/turbulence layers sample disjoint regions of the noise.
        private const float OFFSET_BASE_X = 0f;
        private const float OFFSET_BASE_Y = 1031.7f;
        private const float OFFSET_TURB_X = 47.2f;
        private const float OFFSET_TURB_Y = 2237.3f;
        private const float TURB_TIME_RATIO = 2f;     // turbulence evolves faster than the base layer
        private const float SAMPLE_Y_SHIFT = 500f;    // offset for the per-axis decorrelation within Sample()
        private const float COORD_BIAS = 10000f;      // shifts world coords into the positive Perlin region — Mathf.PerlinNoise mirrors around 0

        // ----- Lifecycle -----
        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Debug.LogWarning($"Multiple {nameof(FlowField)} instances. Destroying duplicate on {name}.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ----- Public API -----
        /// <summary>Returns the current flow velocity (world units / sec) at <paramref name="worldPosition"/>.</summary>
        public Vector2 GetFlowAtPosition(Vector2 worldPosition)
        {
            if (!config) return Vector2.zero;

            var clock = GameClock.Instance;
            float gameTime = clock ? clock.Time : Time.time;
            float t = gameTime * config.temporalScale;

            float speed = config.baseFlowSpeed * speedMultiplier;

            Vector2 flow = Sample(worldPosition, config.noiseScale, t, OFFSET_BASE_X, OFFSET_BASE_Y) * speed;
            if (turbulenceAmount > 0f)
            {
                Vector2 turb = Sample(worldPosition, config.turbulenceNoiseScale, t * TURB_TIME_RATIO, OFFSET_TURB_X, OFFSET_TURB_Y);
                flow += turb * (speed * turbulenceAmount);
            }

            if (config.enableInwardBias)
            {
                Vector2 toCenter = (Vector2)transform.position - worldPosition;
                float dist = toCenter.magnitude;
                if (dist > 0.0001f)
                {
                    float pull = config.inwardBiasByDistance.Evaluate(dist);
                    flow += (toCenter / dist) * pull;
                }
            }

            return flow;
        }

        // ----- Setter façade -----
        // EnvironmentFlowFieldView (or any other driver) writes through these each frame.
        // Mathf.Max(0) mirrors the [Min(0f)] inspector constraint so drivers can't bypass it silently.
        public void SetSpeedMultiplier (float v) => speedMultiplier  = Mathf.Max(0f, v);
        public void SetTurbulenceAmount(float v) => turbulenceAmount = Mathf.Max(0f, v);

        // ----- Private -----
        // Two Perlin samples (one per axis) over the given scale, offset for decorrelation. Output in [-1, 1] x [-1, 1].
        // Coords are shifted by COORD_BIAS so negative world positions don't hit Perlin's mirror seam at the origin.
        private static Vector2 Sample(Vector2 p, float scale, float timeOffset, float offX, float offY)
        {
            float bx = p.x * scale + COORD_BIAS;
            float by = p.y * scale + COORD_BIAS;
            float x = Mathf.PerlinNoise(bx + offX, by + timeOffset) * 2f - 1f;
            float y = Mathf.PerlinNoise(bx + offY, by + timeOffset + SAMPLE_Y_SHIFT) * 2f - 1f;
            return new Vector2(x, y);
        }

        // ----- Gizmos -----
        private void OnDrawGizmosSelected()
        {
            if (!config) return;

            int n = Mathf.Max(2, config.gizmoGridResolution);
            float extent = config.gizmoExtent;
            Vector3 origin = transform.position;
            float step = (extent * 2f) / (n - 1);

            // Two-pass: pass 1 finds max magnitude for color normalization, pass 2 draws.
            var samples = new Vector2[n, n];
            float maxMag = 0f;
            for (int yi = 0; yi < n; yi++)
            for (int xi = 0; xi < n; xi++)
            {
                Vector2 p = new Vector2(origin.x - extent + xi * step, origin.y - extent + yi * step);
                Vector2 v = GetFlowAtPosition(p);
                samples[xi, yi] = v;
                float m = v.magnitude;
                if (m > maxMag) maxMag = m;
            }
            if (maxMag <= 0f) maxMag = 1f;

            for (int yi = 0; yi < n; yi++)
            for (int xi = 0; xi < n; xi++)
            {
                Vector2 p = new Vector2(origin.x - extent + xi * step, origin.y - extent + yi * step);
                Vector2 v = samples[xi, yi];
                float t = Mathf.Clamp01(v.magnitude / maxMag);
                Gizmos.color = Color.Lerp(config.gizmoColorLow, config.gizmoColorHigh, t);
                Vector3 a = new Vector3(p.x, p.y, origin.z);
                Vector3 b = a + (Vector3)(v * config.gizmoArrowScale);
                Gizmos.DrawLine(a, b);
                Gizmos.DrawSphere(b, step * 0.05f);
            }

            // Bounds wireframe so the overlay extent is obvious.
            Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
            Gizmos.DrawWireCube(origin, new Vector3(extent * 2f, extent * 2f, 0f));
        }
    }
}
