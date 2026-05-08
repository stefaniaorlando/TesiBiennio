using UnityEngine;

namespace Holobiont
{
    /*
     * Drives FlowField's modulation from EnvironmentManager's normalised values.
     *
     * Reads:  Humidity, Temperature.
     * Writes: FlowField speedMultiplier, turbulenceAmount.
     *
     *   Humidity    → speed multiplier (viscous slowdown when wet)
     *   Temperature → speed multiplier (cold = still, hot = brisk) AND turbulence
     *
     * The two speed multipliers compose multiplicatively into the single
     * SpeedMultiplier field on the FlowField. Disabling this view leaves the
     * controller's serialized values untouched for manual testing.
     *
     * Inspector fields are grouped by INPUT variable, mirroring EnvironmentPostFXView.
     */
    [DefaultExecutionOrder(-35)]
    [DisallowMultipleComponent]
    public class EnvironmentFlowFieldView : MonoBehaviour
    {
        // ----- Config -----
        [Header("Source")]
        [Tooltip("Environment whose normalised channels drive the flow modulation.")]
        [SerializeField] private EnvironmentManager environment;

        [Tooltip("FlowField this view writes through. Auto-resolved from this GameObject if left empty.")]
        [SerializeField] private FlowField flowField;

        // ----- ← Humidity -----
        [Header("← Humidity")]
        [Tooltip("Humidity normalised → speed multiplier. High humidity should slow the flow (viscous medium).")]
        [SerializeField] private AnimationCurve humiditySpeedCurve = DefaultHumiditySpeed();

        // ----- ← Temperature -----
        [Header("← Temperature")]
        [Tooltip("Temperature normalised → speed multiplier. Cold = nearly still, hot = brisk.")]
        [SerializeField] private AnimationCurve temperatureSpeedCurve = DefaultTemperatureSpeed();

        [Tooltip("Temperature normalised → turbulence amount. High temp adds chaotic high-frequency noise on top.")]
        [SerializeField] private AnimationCurve temperatureTurbulenceCurve = DefaultTemperatureTurbulence();

        // ----- Lifecycle -----
        private void Reset()
        {
            flowField = GetComponent<FlowField>();
        }

        private void OnEnable()
        {
            if (!flowField) flowField = GetComponent<FlowField>();
            if (!environment || !flowField)
            {
                Debug.LogError($"{nameof(EnvironmentFlowFieldView)} requires {nameof(EnvironmentManager)} and {nameof(FlowField)}.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!environment || !flowField) return;

            float h = environment.HumidityNormalized;
            float t = environment.TemperatureNormalized;

            float speedMul = humiditySpeedCurve.Evaluate(h) * temperatureSpeedCurve.Evaluate(t);
            flowField.SetSpeedMultiplier(speedMul);
            flowField.SetTurbulenceAmount(temperatureTurbulenceCurve.Evaluate(t));
        }

        // ----- Defaults -----
        private static AnimationCurve DefaultHumiditySpeed()
            => Smooth(new AnimationCurve(
                new Keyframe(0f, 1.0f),    // dry: full speed
                new Keyframe(1f, 0.3f)));  // wet: viscous slowdown

        private static AnimationCurve DefaultTemperatureSpeed()
            => Smooth(new AnimationCurve(
                new Keyframe(0f, 0.1f),    // cold: nearly still
                new Keyframe(0.5f, 1.0f),  // mid: nominal
                new Keyframe(1f, 1.4f)));  // hot: brisk

        private static AnimationCurve DefaultTemperatureTurbulence()
            => Smooth(new AnimationCurve(
                new Keyframe(0f, 0.0f),
                new Keyframe(0.5f, 0.1f),
                new Keyframe(1f, 1.0f)));

        private static AnimationCurve Smooth(AnimationCurve c)
        {
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            return c;
        }
    }
}
