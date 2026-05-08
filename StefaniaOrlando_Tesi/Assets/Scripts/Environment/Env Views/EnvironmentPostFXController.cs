using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace Holobiont
{
    /*
     * Façade over a URP Volume profile. Caches the override components on enable
     * and exposes typed setters so writers (EnvironmentPostFXView, debug panels,
     * cinematics, audio reactive systems) can drive post-fx without touching the
     * Volume API directly.
     *
     * Only the parameters that env presentation actually drives live as fields
     * on this component:
     *   White Balance temperature · Saturation · Shadows · Midtones · Bloom intensity
     *   DoF max radius · Vignette intensity/color/smoothness · Chromatic aberration
     *   Film grain
     *
     * Other URP overrides on the bound profile (WB tint, exposure, contrast, hue
     * shift, highlights, bloom threshold, etc.) pass through unchanged — author
     * those directly on the Volume profile asset.
     *
     * Storage model: setters mutate the serialized fields; LateUpdate copies them
     * onto the volume's overrides each frame. Tweaking a slider in the inspector
     * at runtime takes effect next frame; with the View disabled, manual values
     * stick.
     */
    [DisallowMultipleComponent]
    public class EnvironmentPostFXController : MonoBehaviour
    {
        // ----- Config -----
        [Header("Volume")]
        [Tooltip("URP Volume whose profile overrides this controller drives.")]
        [SerializeField] private Volume volume;

        [Header("White Balance")]
        [Tooltip("White balance temperature offset.")]
        [Range(-100f, 100f)] [SerializeField] private float wbTemperature = 0f;

        [Header("Color Adjustments")]
        [Tooltip("Saturation adjustment.")]
        [Range(-100f, 100f)]
        [FormerlySerializedAs("caSaturation")]
        [SerializeField] private float saturation = 0f;

        [Header("Shadows / Midtones  (xyz = RGB tint, w = offset)")]
        [Tooltip("Shadows tint and offset.")]
        [FormerlySerializedAs("smhShadows")]
        [SerializeField] private Vector4 shadows  = new Vector4(1f, 1f, 1f, 0f);

        [Tooltip("Midtones tint and offset.")]
        [FormerlySerializedAs("smhMidtones")]
        [SerializeField] private Vector4 midtones = new Vector4(1f, 1f, 1f, 0f);

        [Header("Bloom")]
        [Tooltip("Bloom intensity.")]
        [Min(0f)] [SerializeField] private float bloomIntensity = 0.4f;

        [Header("Depth Of Field (Gaussian)")]
        [Tooltip("Gaussian DOF max radius.")]
        [Range(0f, 2f)] [SerializeField] private float dofMaxRadius = 1f;

        [Header("Vignette")]
        [Tooltip("Vignette intensity.")]
        [Range(0f, 1f)] [SerializeField] private float vignetteIntensity = 0.2f;

        [Tooltip("Vignette color.")]
        [SerializeField] private Color vignetteColor = Color.black;

        [Tooltip("Vignette smoothness. Higher = softer falloff that reads as a wider darkening.")]
        [Range(0.01f, 1f)] [SerializeField] private float vignetteSmoothness = 0.2f;

        [Header("Chromatic Aberration")]
        [Tooltip("Chromatic aberration intensity.")]
        [Range(0f, 1f)] [SerializeField] private float chromaticAberration = 0f;

        [Header("Film Grain")]
        [Tooltip("Film grain intensity.")]
        [Range(0f, 1f)] [SerializeField] private float filmGrainIntensity = 0.15f;

        // ----- Cached overrides -----
        private WhiteBalance              whiteBalance;
        private ColorAdjustments          colorAdjustments;
        private ShadowsMidtonesHighlights smh;
        private Bloom                     bloom;
        private DepthOfField              depthOfField;
        private Vignette                  vignette;
        private ChromaticAberration       chromatic;
        private FilmGrain                 filmGrain;

        // ----- Public API -----
        /// <summary>The bound URP volume.</summary>
        public Volume Volume => volume;

        // ----- Lifecycle -----
        private void OnEnable()
        {
            if (!volume || volume.profile is null)
            {
                Debug.LogError($"{nameof(EnvironmentPostFXController)} has no {nameof(Volume)} or profile assigned.", this);
                enabled = false;
                return;
            }

            volume.profile.TryGet(out whiteBalance);
            volume.profile.TryGet(out colorAdjustments);
            volume.profile.TryGet(out smh);
            volume.profile.TryGet(out bloom);
            volume.profile.TryGet(out depthOfField);
            volume.profile.TryGet(out vignette);
            volume.profile.TryGet(out chromatic);
            volume.profile.TryGet(out filmGrain);
        }

        private void LateUpdate()
        {
            if (whiteBalance)
            {
                whiteBalance.temperature.overrideState = true; whiteBalance.temperature.value = wbTemperature;
            }

            if (colorAdjustments)
            {
                colorAdjustments.saturation.overrideState = true; colorAdjustments.saturation.value = saturation;
            }

            if (smh)
            {
                smh.shadows.overrideState  = true; smh.shadows.value  = shadows;
                smh.midtones.overrideState = true; smh.midtones.value = midtones;
            }

            if (bloom)
            {
                bloom.intensity.overrideState = true; bloom.intensity.value = bloomIntensity;
            }

            if (depthOfField)
            {
                depthOfField.gaussianMaxRadius.overrideState = true;
                depthOfField.gaussianMaxRadius.value         = dofMaxRadius;
            }

            if (vignette)
            {
                vignette.intensity.overrideState  = true; vignette.intensity.value  = vignetteIntensity;
                vignette.color.overrideState      = true; vignette.color.value      = vignetteColor;
                vignette.smoothness.overrideState = true; vignette.smoothness.value = vignetteSmoothness;
            }

            if (chromatic)
            {
                chromatic.intensity.overrideState = true; chromatic.intensity.value = chromaticAberration;
            }

            if (filmGrain)
            {
                filmGrain.intensity.overrideState = true; filmGrain.intensity.value = filmGrainIntensity;
            }
        }

        // ----- Setter façade -----
        // One setter per driven parameter. Each presentation view writes only the parameters it
        // owns (per ENV_PRESENTATION_MAPPINGS).
        public void SetWhiteBalanceTemperature(float v)   => wbTemperature       = v;
        public void SetSaturation             (float v)   => saturation          = v;
        public void SetShadows                (Vector4 v) => shadows             = v;
        public void SetMidtones               (Vector4 v) => midtones            = v;
        public void SetBloomIntensity         (float v)   => bloomIntensity      = v;
        public void SetDofMaxRadius           (float v)   => dofMaxRadius        = v;
        public void SetVignetteIntensity      (float v)   => vignetteIntensity   = v;
        public void SetVignetteColor          (Color v)   => vignetteColor       = v;
        public void SetVignetteSmoothness     (float v)   => vignetteSmoothness  = v;
        public void SetChromaticAberration    (float v)   => chromaticAberration = v;
        public void SetFilmGrain              (float v)   => filmGrainIntensity  = v;
    }
}
