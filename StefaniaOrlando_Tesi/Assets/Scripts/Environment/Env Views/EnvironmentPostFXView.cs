using UnityEngine;

namespace Holobiont
{
    /*
     * Drives EnvironmentPostFXController from EnvironmentManager's normalised values.
     *
     * Reads:  Temperature, Light, Humidity, Toxicity.
     * Writes: a disjoint subset of post-fx parameters per variable (see ENV_PRESENTATION_MAPPINGS).
     *
     *   Temperature → WB temperature, saturation, midtones tint, chromatic aberration
     *   Light       → bloom intensity, vignette intensity
     *   Humidity    → DoF max radius
     *   Toxicity    → shadows tint, vignette color, vignette smoothness, film grain
     *
     * Every controller parameter has at most one driver here. Parameters not listed
     * above (exposure, contrast, hue shift, bloom threshold, highlights, WB tint) are
     * authored once on the controller asset and left static.
     *
     * Inspector fields are grouped by INPUT variable, not by output parameter, so the
     * env→post-fx contract is readable at a glance. Disabling this component leaves the
     * controller's serialized fields untouched for manual testing.
     */
    [DisallowMultipleComponent]
    public class EnvironmentPostFXView : MonoBehaviour
    {
        // ----- Config -----
        [Header("Source")]
        [Tooltip("Environment whose normalised channels drive the post-fx mappings.")]
        [SerializeField] private EnvironmentManager environment;

        [Tooltip("Controller this view writes through. Auto-resolved from this GameObject if left empty.")]
        [SerializeField] private EnvironmentPostFXController controller;

        // ----- ← Temperature -----
        [Header("← Temperature")]
        [Tooltip("White balance temperature offset at the cold end of the temperature range.")]
        [SerializeField] private float wbTempCold = -40f;

        [Tooltip("White balance temperature offset at the hot end of the temperature range.")]
        [SerializeField] private float wbTempHot  =  40f;

        [Tooltip("Saturation offset at the cold end. Kept mild — cold should desaturate but not drain colour.")]
        [SerializeField] private float saturationCold = -8f;

        [Tooltip("Saturation offset at the hot end.")]
        [SerializeField] private float saturationHot  = 12f;

        [Tooltip("Midtones tint+offset at the cold end. (xyz = RGB tint, w = offset)")]
        [SerializeField] private Vector4 midtonesCold = new Vector4(0.85f, 0.95f, 1.10f, 0.00f);

        [Tooltip("Midtones tint+offset at the hot end.")]
        [SerializeField] private Vector4 midtonesHot  = new Vector4(1.10f, 0.95f, 0.80f, 0.00f);

        [Tooltip("Temperature-normalised → chromatic aberration intensity. V-shaped: peaks at both extremes (instability), quiet in the middle.")]
        [SerializeField] private AnimationCurve chromaticAberrationFromTemperature = TemperatureExtremesRamp();

        // ----- ← Light -----
        [Header("← Light")]
        [Tooltip("Bloom intensity in dark conditions.")]
        [Min(0f)] [SerializeField] private float bloomIntensityDark   = 0.1f;

        [Tooltip("Bloom intensity in bright conditions.")]
        [Min(0f)] [SerializeField] private float bloomIntensityBright = 1.4f;

        [Tooltip("Vignette intensity in dark conditions. Higher = closes in when the world goes dark.")]
        [Range(0f, 1f)] [SerializeField] private float vignetteIntensityDark   = 0.45f;

        [Tooltip("Vignette intensity in bright conditions. Lower = opens up.")]
        [Range(0f, 1f)] [SerializeField] private float vignetteIntensityBright = 0.15f;

        // ----- ← Humidity -----
        [Header("← Humidity")]
        [Tooltip("Gaussian DoF max radius when dry. Typically zero.")]
        [SerializeField] private float dofRadiusDry = 0f;

        [Tooltip("Gaussian DoF max radius when fully humid. Reads as damp glass / fogged optics.")]
        [SerializeField] private float dofRadiusWet = 1.4f;

        // ----- ← Toxicity -----
        [Header("← Toxicity")]
        [Tooltip("Shadows tint+offset at zero toxicity. Identity by default.")]
        [SerializeField] private Vector4 shadowsClean = new Vector4(1f, 1f, 1f, 0f);

        [Tooltip("Shadows tint+offset at full toxicity. Sickly green pull.")]
        [SerializeField] private Vector4 shadowsToxic = new Vector4(0.55f, 0.95f, 0.45f, -0.05f);

        [Tooltip("Vignette colour at zero toxicity.")]
        [SerializeField] private Color vignetteColorClean = Color.black;

        [Tooltip("Vignette colour at full toxicity.")]
        [SerializeField] private Color vignetteColorToxic = new Color(0.4f, 0.55f, 0.2f, 1f);

        [Tooltip("Vignette smoothness at zero toxicity. Standard hard-edged vignette.")]
        [Range(0.01f, 1f)] [SerializeField] private float vignetteSmoothnessClean = 0.2f;

        [Tooltip("Vignette smoothness at full toxicity. Higher = wider, softer falloff for an expanding-toxic-glow read.")]
        [Range(0.01f, 1f)] [SerializeField] private float vignetteSmoothnessToxic = 0.6f;

        [Tooltip("Toxicity-normalised → film grain intensity. Returns absolute grain (0..1). Default keeps a small biological baseline and ramps at extremes.")]
        [SerializeField] private AnimationCurve filmGrainFromToxicity = ToxicityGrainRamp();

        // ----- Lifecycle -----
        private void Reset()
        {
            controller = GetComponent<EnvironmentPostFXController>();
        }

        private void OnEnable()
        {
            if (!controller) controller = GetComponent<EnvironmentPostFXController>();
            if (!environment || !controller)
            {
                Debug.LogError($"{nameof(EnvironmentPostFXView)} requires {nameof(EnvironmentManager)} and {nameof(EnvironmentPostFXController)}.", this);
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            float t = environment.TemperatureNormalized;
            float l = environment.LightNormalized;
            float h = environment.HumidityNormalized;
            float k = environment.ToxicityNormalized;

            // ← Temperature
            controller.SetWhiteBalanceTemperature(Mathf.Lerp(wbTempCold,    wbTempHot,    t));
            controller.SetSaturation             (Mathf.Lerp(saturationCold, saturationHot, t));
            controller.SetMidtones               (Vector4.Lerp(midtonesCold, midtonesHot, t));
            controller.SetChromaticAberration    (chromaticAberrationFromTemperature.Evaluate(t));

            // ← Light  (low light → high vignette intensity, bright → low)
            controller.SetBloomIntensity         (Mathf.Lerp(bloomIntensityDark,    bloomIntensityBright,    l));
            controller.SetVignetteIntensity      (Mathf.Lerp(vignetteIntensityDark, vignetteIntensityBright, l));

            // ← Humidity
            controller.SetDofMaxRadius           (Mathf.Lerp(dofRadiusDry, dofRadiusWet, h));

            // ← Toxicity
            controller.SetShadows                (Vector4.Lerp(shadowsClean, shadowsToxic, k));
            controller.SetVignetteColor          (Color.Lerp(vignetteColorClean, vignetteColorToxic, k));
            controller.SetVignetteSmoothness     (Mathf.Lerp(vignetteSmoothnessClean, vignetteSmoothnessToxic, k));
            controller.SetFilmGrain              (filmGrainFromToxicity.Evaluate(k));
        }

        // ----- Private -----
        // V-shape: peaks at both ends of temperature, quiet in the middle.
        private static AnimationCurve TemperatureExtremesRamp()
        {
            var c = new AnimationCurve(
                new Keyframe(0.0f, 0.7f),
                new Keyframe(0.5f, 0.0f),
                new Keyframe(1.0f, 0.7f)
            );
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        // Baseline grain always present, ramps further at high toxicity.
        private static AnimationCurve ToxicityGrainRamp()
        {
            var c = new AnimationCurve(
                new Keyframe(0.0f, 0.15f),
                new Keyframe(0.5f, 0.15f),
                new Keyframe(1.0f, 0.50f)
            );
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            return c;
        }
    }
}
