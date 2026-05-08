using System;
using UnityEngine;

namespace Holobiont
{
    /*
     * Owns and exposes the current state of the environment.
     * Views (post-fx, particles, creature affinity lookups, debug HUD)
     * read from this component.
     *
     * Composition contract (explicit, not implicit):
     *   public value = clamp(base + eventDelta) per variable.
     *   - base is written by drift / debug overrides each frame via Set*().
     *   - eventDelta is accumulated by the event system each frame via ApplyEventDelta(),
     *     and reset to zero at the very start of every frame by this component (order -90).
     * This way an event can target a variable whose drift is disabled without compounding
     * across frames, and drift can write absolute base values without worrying about events.
     *
     * Manual override:
     *   Toggling Manual Override ON freezes drift writes (Set* becomes a no-op) and lets
     *   the four inspector "Environment values" fields drive the base values directly via
     *   OnValidate. Events still apply on top of the typed base — useful for "what does
     *   temperature 35 with a heat wave running look like?" testing.
     *
     * Threshold-cross events fire in LateUpdate after the frame's composition is final.
     *
     * Variable order across this file (and across the rest of the project) is
     * Temperature → Light → Humidity → Toxicity.
     */

    [DefaultExecutionOrder(-90)]
    public class EnvironmentManager : MonoBehaviour
    {
        // ----- Config -----
        [Header("Config")]
        [Tooltip("Per-variable ranges and starting values for the world's environment.")]
        [SerializeField] private EnvironmentConfig config;

        // ----- Environment values (live readouts / manual override targets) -----
        [Header("Environment values")]
        [Tooltip("Composed value this frame (base + event delta, clamped). " +
                 "When Manual Override is OFF this updates every frame from the simulation. " +
                 "When ON, drag to set the base — drift is suppressed and your value sticks (events still apply on top).")]
        [SerializeField] private float temperature;

        [SerializeField] private float light;
        [SerializeField] private float humidity;
        [SerializeField] private float toxicity;

        [Tooltip("When ON, EnvironmentDrift's Set* calls are ignored and the four values above act as the manual base. " +
                 "Inspector edits are pushed to base via OnValidate. Threshold-cross events still fire normally.")]
        [SerializeField] private bool manualOverride;

        // ----- State -----
        // Base values (driver-written, e.g. drift). Reset only at OnEnable; persist across frames.
        private float tempBase, lightBase, humBase, toxBase;
        // Per-frame event delta accumulators. Cleared at start of each frame.
        private float tempEvtDelta, lightEvtDelta, humEvtDelta, toxEvtDelta;

        // Latched extreme state per variable, used to detect transitions for the threshold-cross events.
        private ExtremeKind tempState, lightState, humState, toxState;

        // ----- Public API — raw values -----
        // The single source of truth for environmental state.
        /// <summary>Current temperature in raw config units.</summary>
        public float Temperature => Clamp(config.temperature, tempBase  + tempEvtDelta);
        /// <summary>Current light level in raw config units.</summary>
        public float Light       => Clamp(config.light,       lightBase + lightEvtDelta);
        /// <summary>Current humidity in raw config units.</summary>
        public float Humidity    => Clamp(config.humidity,    humBase   + humEvtDelta);
        /// <summary>Current toxicity in raw config units.</summary>
        public float Toxicity    => Clamp(config.toxicity,    toxBase   + toxEvtDelta);

        // Normalised 0–1 views, useful for post-processing / particle curves.
        /// <summary>Temperature mapped to [0,1] using the config range.</summary>
        public float TemperatureNormalized => Normalize(config.temperature, Temperature);
        /// <summary>Light mapped to [0,1] using the config range.</summary>
        public float LightNormalized       => Normalize(config.light,       Light);
        /// <summary>Humidity mapped to [0,1] using the config range.</summary>
        public float HumidityNormalized    => Normalize(config.humidity,    Humidity);
        /// <summary>Toxicity mapped to [0,1] using the config range.</summary>
        public float ToxicityNormalized    => Normalize(config.toxicity,    Toxicity);

        // Whole-state vectors. AsNormalizedVector is what affinity distance uses (creatures live in 0..1 space).
        // AsVector (raw units) is kept for symmetry / future consumers that need physical magnitudes.
        /// <summary>All four variables packed as a Vector4 in raw units (Temp, Light, Humidity, Toxicity).</summary>
        public Vector4 AsVector
            => new Vector4(Temperature, Light, Humidity, Toxicity);

        /// <summary>All four variables packed as a Vector4 in [0,1] (Temp, Light, Humidity, Toxicity). This is what creature affinity distance is measured against.</summary>
        public Vector4 AsNormalizedVector
            => new Vector4(TemperatureNormalized, LightNormalized, HumidityNormalized, ToxicityNormalized);

        /// <summary>The config asset driving this environment. Null until OnEnable validates.</summary>
        public EnvironmentConfig Config => config;

        /// <summary>True while the inspector's manual override is engaged. Drift writes are suppressed in this mode.</summary>
        public bool ManualOverride => manualOverride;

        // ----- Threshold-cross events -----
        /// <summary>High vs Low vs neither, classified against the per-variable extreme bands in <see cref="EnvironmentVariableConfig"/>.</summary>
        public enum ExtremeKind { None, Low, High }

        /// <summary>Fires when a variable transitions from None into a Low or High extreme. Useful for audio stings, banner triggers, etc.</summary>
        public event Action<EnvironmentVariable, ExtremeKind> OnVariableEnteredExtreme;

        /// <summary>Fires when a variable transitions out of an extreme. Argument is the extreme it just left.</summary>
        public event Action<EnvironmentVariable, ExtremeKind> OnVariableLeftExtreme;

        // ----- Public mutators — base values (drift / debug overrides) -----
        // Writes the *base* layer. The frame's composed value is base + eventDelta.
        // No-op when manualOverride is on, so drift can't fight the inspector-typed base.
        /// <summary>Set the temperature base value (drift / debug). Composed with the per-frame event delta.</summary>
        public void SetTemperature(float v) { if (!manualOverride) tempBase  = v; }
        /// <summary>Set the light base value (drift / debug). Composed with the per-frame event delta.</summary>
        public void SetLight      (float v) { if (!manualOverride) lightBase = v; }
        /// <summary>Set the humidity base value (drift / debug). Composed with the per-frame event delta.</summary>
        public void SetHumidity   (float v) { if (!manualOverride) humBase   = v; }
        /// <summary>Set the toxicity base value (drift / debug). Composed with the per-frame event delta.</summary>
        public void SetToxicity   (float v) { if (!manualOverride) toxBase   = v; }

        /// <summary>
        /// Adds <paramref name="delta"/> to the per-frame event delta of every variable in <paramref name="mask"/>.
        /// Accumulators are zeroed at the start of every frame, so events never compound across frames.
        /// </summary>
        public void ApplyEventDelta(EnvironmentVariable mask, float delta)
        {
            if ((mask & EnvironmentVariable.Temp)     != 0) tempEvtDelta  += delta;
            if ((mask & EnvironmentVariable.Light)    != 0) lightEvtDelta += delta;
            if ((mask & EnvironmentVariable.Humidity) != 0) humEvtDelta   += delta;
            if ((mask & EnvironmentVariable.Toxicity) != 0) toxEvtDelta   += delta;
        }

        // ----- Lifecycle -----
        private void OnEnable()
        {
            if (!config)
            {
                Debug.LogError($"{nameof(EnvironmentManager)} has no {nameof(EnvironmentConfig)} assigned.", this);
                enabled = false;
                return;
            }

            ResetSessionState();
        }

        /// <summary>Snap base values to config baselines, clear per-frame event deltas, reclassify extreme states.</summary>
        [ContextMenu("Reset Session State")]
        public void ResetSessionState()
        {
            if (!config) return;

            tempBase  = config.temperature.baseValue;
            lightBase = config.light.baseValue;
            humBase   = config.humidity.baseValue;
            toxBase   = config.toxicity.baseValue;

            tempEvtDelta = lightEvtDelta = humEvtDelta = toxEvtDelta = 0f;

            tempState  = Classify(config.temperature, TemperatureNormalized);
            lightState = Classify(config.light,       LightNormalized);
            humState   = Classify(config.humidity,    HumidityNormalized);
            toxState   = Classify(config.toxicity,    ToxicityNormalized);
        }

        // Runs at -90, after GameClock (-100) and before EnvironmentDrift (-50) / EnvironmentEventSystem (-25).
        // Resets the per-frame event delta so this frame's events start from a clean slate.
        private void Update()
        {
            tempEvtDelta = lightEvtDelta = humEvtDelta = toxEvtDelta = 0f;
        }

        // After all drivers have written this frame, evaluate threshold transitions on the composed values.
        private void LateUpdate()
        {
            if (!config) return;

            // Cache composed values once for both the transition check and the inspector readout.
            float t = Temperature, l = Light, h = Humidity, x = Toxicity;

            // Mirror composed values into the inspector fields when not in manual override.
            // Skipping the writeback in override mode keeps inspector edits visible until the user changes them.
            if (!manualOverride)
            {
                temperature = t;
                light       = l;
                humidity    = h;
                toxicity    = x;
            }

            DetectTransition(EnvironmentVariable.Temp,     config.temperature, Normalize(config.temperature, t), ref tempState);
            DetectTransition(EnvironmentVariable.Light,    config.light,       Normalize(config.light,       l), ref lightState);
            DetectTransition(EnvironmentVariable.Humidity, config.humidity,    Normalize(config.humidity,    h), ref humState);
            DetectTransition(EnvironmentVariable.Toxicity, config.toxicity,    Normalize(config.toxicity,    x), ref toxState);
        }

#if UNITY_EDITOR
        // Inspector-edited values flow into the base layer only while override is engaged.
        // Otherwise the four fields are read-only mirrors and any edit is reverted next frame by LateUpdate.
        private void OnValidate()
        {
            if (!manualOverride) return;
            tempBase  = temperature;
            lightBase = light;
            humBase   = humidity;
            toxBase   = toxicity;
        }
#endif

        // ----- Private -----
        private void DetectTransition(EnvironmentVariable var, EnvironmentVariableConfig cfg, float normalized, ref ExtremeKind state)
        {
            var next = Classify(cfg, normalized);
            if (next == state) return;

            if (state != ExtremeKind.None) OnVariableLeftExtreme?.Invoke(var, state);
            if (next  != ExtremeKind.None) OnVariableEnteredExtreme?.Invoke(var, next);
            state = next;
        }

        private static ExtremeKind Classify(EnvironmentVariableConfig cfg, float normalized)
        {
            if (normalized <= cfg.extremeLowNormalized)  return ExtremeKind.Low;
            if (normalized >= cfg.extremeHighNormalized) return ExtremeKind.High;
            return ExtremeKind.None;
        }

        private static float Normalize(EnvironmentVariableConfig v, float current)
            => Mathf.InverseLerp(v.minValue, v.maxValue, current);

        private static float Clamp(EnvironmentVariableConfig v, float value)
            => Mathf.Clamp(value, v.minValue, v.maxValue);
    }
}
