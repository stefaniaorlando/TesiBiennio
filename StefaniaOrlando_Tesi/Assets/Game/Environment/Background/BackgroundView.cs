using UnityEngine;

namespace Holobiont
{
    /*
     * Single writer for the petri-dish background material's per-frame uniforms.
     * Replaces the earlier FlowFieldShaderSync + EnvironmentBackgroundView split:
     * keeping both writers on one component eliminates the MPB race window and
     * makes each sync axis cleanly toggleable.
     *
     * Two independent sync axes, both Get→modify→Set on a MaterialPropertyBlock
     * so the source .mat asset is never mutated:
     *
     *   FlowField axis    → mirrors FlowField.TryGetWarpParams() into:
     *                       _WarpSpeed, _WarpStrength, _FlowTurbScale,
     *                       _FlowTurbAmount, _TimeScale
     *
     *   Environment axis  → temperature lerps into:
     *                       _WarpScale, _PulseStrength
     *
     * _WarpScale lives on the Environment axis, not the FlowField axis — this is
     * a deliberate decoupling: the sim's noiseScale stays authored on
     * FlowFieldConfig and drives GetFlowAtPosition; the visual warp scale takes
     * its own temperature drive. With either axis disabled, those uniforms fall
     * back to whatever is authored on the .mat.
     *
     * OnDisable does NOT clear the block — last-written values stick (matches
     * the project-wide "values stick when the View is off" contract).
     *
     * Known limitation: shader uses _Time.y for ambient animation, while FlowField
     * uses GameClock for pausable game-time. Under pause the background keeps
     * wobbling while creatures freeze. Acceptable for now; fix would push a
     * custom _FlowTime float driven from GameClock.
     */
    [DefaultExecutionOrder(-30)]
    [DisallowMultipleComponent]
    public class BackgroundView : MonoBehaviour
    {
        // ----- Sources -----
        [Header("Sources")]
        [Tooltip("FlowField in the scene. Auto-resolved from FlowField.Instance if unassigned. Required only when FlowField sync is enabled.")]
        [SerializeField] private FlowField flowField;

        [Tooltip("Environment whose normalised channels drive the background ambient. Required only when Environment sync is enabled.")]
        [SerializeField] private EnvironmentManager environment;

        // ----- Targets -----
        [Header("Targets")]
        [Tooltip("Renderer(s) using a PetriDishBackground material (Lit and/or Unlit). Auto-resolved from this GameObject's children on Reset.")]
        [SerializeField] private Renderer[] backgroundRenderers;

        // ----- FlowField Sync -----
        [Header("FlowField Sync")]
        [Tooltip("When enabled, mirrors FlowField warp + turbulence params into the bg shader.")]
        [SerializeField] private bool syncFlowField = true;

        [Tooltip("Maps flow speed to shader warp displacement. Higher = more visible cell distortion when the flow is fast.")]
        [Range(0f, 1f)] [SerializeField] private float warpStrengthGain = 0.25f;

        // ----- Environment Sync — ← Temperature -----
        [Header("Environment Sync — ← Temperature")]
        [Tooltip("When enabled, drives temperature-coupled ambient parameters in the bg shader.")]
        [SerializeField] private bool syncEnvironment = true;

        [Tooltip("Warp spatial frequency, lerped over normalized temperature. Cold = bigger eddies, hot = tighter eddies.")]
        [SerializeField] private Vector2 warpScaleRange = new Vector2(0.1f, 0.4f);

        [Tooltip("Channel pulse amplitude, lerped over normalized temperature. Cold = subtle, hot = vivid.")]
        [SerializeField] private Vector2 pulseStrengthRange = new Vector2(0.1f, 0.6f);

        // ----- Cached property IDs -----
        private static readonly int IDWarpScale      = Shader.PropertyToID("_WarpScale");
        private static readonly int IDWarpSpeed      = Shader.PropertyToID("_WarpSpeed");
        private static readonly int IDWarpStrength   = Shader.PropertyToID("_WarpStrength");
        private static readonly int IDFlowTurbScale  = Shader.PropertyToID("_FlowTurbScale");
        private static readonly int IDFlowTurbAmount = Shader.PropertyToID("_FlowTurbAmount");
        private static readonly int IDTimeScale      = Shader.PropertyToID("_TimeScale");
        private static readonly int IDPulseStrength  = Shader.PropertyToID("_PulseStrength");

        private MaterialPropertyBlock mpb;

        // ----- Lifecycle -----
        private void Reset()
        {
            backgroundRenderers = GetComponentsInChildren<Renderer>();
        }

        private void OnEnable()
        {
            if (!flowField) flowField = FlowField.Instance;
            if (!flowField) flowField = FindAnyObjectByType<FlowField>();

            if (backgroundRenderers == null || backgroundRenderers.Length == 0)
            {
                Debug.LogError($"{nameof(BackgroundView)} requires at least one renderer.", this);
                enabled = false;
                return;
            }

            if (mpb == null) mpb = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            // Resolve each axis up front. Each axis is independently gated on its toggle AND
            // the presence of its dependency — a missing FlowField only silences that axis.
            FlowField.WarpParams w = default;
            bool runFlow = syncFlowField && flowField && flowField.TryGetWarpParams(out w);
            bool runEnv  = syncEnvironment && environment;
            if (!runFlow && !runEnv) return;

            float warpStrength = 0f, turbStrength = 0f;
            if (runFlow)
            {
                warpStrength = w.EffectiveSpeed * warpStrengthGain;
                turbStrength = w.EffectiveSpeed * w.TurbulenceAmount * warpStrengthGain;
            }

            float envWarpScale = 0f, envPulseStrength = 0f;
            if (runEnv)
            {
                float t = environment.TemperatureNormalized;
                envWarpScale     = Mathf.Lerp(warpScaleRange.x,     warpScaleRange.y,     t);
                envPulseStrength = Mathf.Lerp(pulseStrengthRange.x, pulseStrengthRange.y, t);
            }

            for (int i = 0; i < backgroundRenderers.Length; i++)
            {
                var r = backgroundRenderers[i];
                if (!r) continue;

                r.GetPropertyBlock(mpb); // preserve any other per-instance overrides on this renderer

                if (runFlow)
                {
                    mpb.SetFloat(IDWarpSpeed,      w.TemporalScale);
                    mpb.SetFloat(IDWarpStrength,   warpStrength);
                    mpb.SetFloat(IDFlowTurbScale,  w.TurbulenceNoiseScale);
                    mpb.SetFloat(IDFlowTurbAmount, turbStrength);
                    mpb.SetFloat(IDTimeScale,      1f); // _WarpSpeed already drives evolution; collapse master multiplier
                }

                if (runEnv)
                {
                    mpb.SetFloat(IDWarpScale,     envWarpScale);
                    mpb.SetFloat(IDPulseStrength, envPulseStrength);
                }

                r.SetPropertyBlock(mpb);
            }
        }
    }
}
