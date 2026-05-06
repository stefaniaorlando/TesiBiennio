using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Holobiont
{
    /*
     * Drives global 2D light intensity from EnvironmentManager.LightNormalized.
     *
     * Reads:  Light.
     * Writes: Light2D.intensity on the assigned light.
     *
     * v1: intensity only. Color (Temperature) and per-light flicker (Toxicity)
     * are reserved for future passes — see ENV_PRESENTATION_MAPPINGS. When
     * either is added, extract a Controller façade so each output keeps a
     * single setter and the lane discipline holds.
     */
    [DisallowMultipleComponent]
    public class EnvironmentLightingView : MonoBehaviour
    {
        // ----- Config -----
        [Header("Source")]
        [Tooltip("Environment whose Light channel drives this view.")]
        [SerializeField] private EnvironmentManager environment;

        [Tooltip("The 2D light to drive. Auto-resolved to a Light2D on this GameObject if left empty.")]
        [SerializeField] private Light2D globalLight;

        // ----- ← Light -----
        [Header("← Light")]
        [Tooltip("Intensity at the dark end of the Light range.")]
        [Min(0f)] [SerializeField] private float intensityDark   = 0.15f;

        [Tooltip("Intensity at the bright end of the Light range.")]
        [Min(0f)] [SerializeField] private float intensityBright = 1.2f;

        // ----- Lifecycle -----
        private void Reset() => globalLight = GetComponent<Light2D>();

        private void OnEnable()
        {
            if (!globalLight) globalLight = GetComponent<Light2D>();
            if (!environment || !globalLight)
            {
                Debug.LogError($"{nameof(EnvironmentLightingView)} requires {nameof(EnvironmentManager)} and a {nameof(Light2D)} reference.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            float l = environment.LightNormalized;
            globalLight.intensity = Mathf.Lerp(intensityDark, intensityBright, l);
        }
    }
}
