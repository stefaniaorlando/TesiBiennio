using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Holobiont
{
    /*
     * Read-only on-screen HUD.
     *
     * Drives four reusable ValueSlider widgets (one per environment variable),
     * a separate plain Slider for difficulty, and a status label that narrates
     * the EnvironmentEventSystem's current event.
     *
     * The four ValueSlider rows are dumb views — this component pushes values
     * and highlight colors into them every frame. The difficulty slider stays a
     * plain UnityEngine.UI.Slider; it has no value label or radial fill needs.
     *
     * Status label cycles:
     *   - "Active: <name>"          while ActiveEvent is non-null
     *   - "Ended: <name>"           for endedLingerSeconds after an event ends
     *   - "Next: <name> in mm:ss"   while idle and NextEvent is non-null
     *   - "—"                       when nothing is queued
     *
     * Difficulty slider mirrors DifficultyManager.Difficulty (0..1). Time label
     * is GameClock.Time formatted as mm:ss; the two share the same growth signal
     * but diverge slightly because Difficulty is curve-shaped.
     */

    [DisallowMultipleComponent]
    public class EnvironmentHUDView : MonoBehaviour
    {
        // ----- Config -----
        [Header("Source")]
        [Tooltip("Environment to mirror.")]
        [SerializeField] private EnvironmentManager environment;

        [Tooltip("Event system to read ActiveEvent / NextEvent / SecondsUntilNext from.")]
        [SerializeField] private EnvironmentEventSystem eventSystem;

        [Header("Environment rows  (ValueSlider widgets)")]
        [SerializeField] private ValueSlider temperature;
        [SerializeField] private ValueSlider light;
        [SerializeField] private ValueSlider humidity;
        [SerializeField] private ValueSlider toxicity;

        [Header("Session")]
        [Tooltip("Plain slider mirroring DifficultyManager.Difficulty (0..1).")]
        [SerializeField] private Slider difficultySlider;

        [Tooltip("Label showing GameClock.Time as mm:ss.")]
        [SerializeField] private TMP_Text timeLabel;

        [Tooltip("Label showing the current event status (Active / Ended / Next / idle).")]
        [SerializeField] private TMP_Text eventStatusLabel;

        [Header("Format")]
        [Tooltip("How long 'Ended: <name>' stays on screen after an event finishes.")]
        [SerializeField, Min(0f)] private float endedLingerSeconds = 2.5f;

        // ----- HUD-local transition state -----
        // The event system clears its own active reference mid-frame, so we track
        // the previous frame's config locally to detect the active→idle edge.
        private EnvironmentEventConfig prevActiveCfg;
        private EnvironmentEventConfig endedCfg;
        private float endedAtTime;

        // ----- Lifecycle -----
        private void OnEnable()
        {
            if (!environment)
            {
                Debug.LogError($"{nameof(EnvironmentHUDView)} has no {nameof(EnvironmentManager)} assigned.", this);
                enabled = false;
                return;
            }

            var c = environment.Config;
            ApplyRow(temperature, EnvironmentVariable.Temp,     c.temperature);
            ApplyRow(light,       EnvironmentVariable.Light,    c.light);
            ApplyRow(humidity,    EnvironmentVariable.Humidity, c.humidity);
            ApplyRow(toxicity,    EnvironmentVariable.Toxicity, c.toxicity);

            if (difficultySlider)
            {
                difficultySlider.minValue = 0f;
                difficultySlider.maxValue = 1f;
                difficultySlider.wholeNumbers = false;
            }

            prevActiveCfg = null;
            endedCfg = null;
            endedAtTime = float.NegativeInfinity;
        }

        private void Update()
        {
            // Push env values into the four ValueSlider widgets.
            if (temperature) temperature.SetValue(environment.Temperature);
            if (light)       light.SetValue(environment.Light);
            if (humidity)    humidity.SetValue(environment.Humidity);
            if (toxicity)    toxicity.SetValue(environment.Toxicity);

            var clock = GameClock.Instance;
            float now = clock ? clock.Time : 0f;

            if (difficultySlider)
            {
                float d = DifficultyManager.Instance ? DifficultyManager.Instance.Difficulty : 0f;
                difficultySlider.SetValueWithoutNotify(Mathf.Clamp01(d));
            }
            if (timeLabel) timeLabel.text = FormatMMSS(now);

            // Resolve the active event's config once.
            EnvironmentEventConfig curCfg = (eventSystem && eventSystem.ActiveEvent != null)
                ? eventSystem.ActiveEvent.Config : null;

            // Active→idle edge starts the "Ended" linger.
            if (prevActiveCfg != null && curCfg == null)
            {
                endedCfg = prevActiveCfg;
                endedAtTime = now;
            }
            prevActiveCfg = curCfg;

            // Union of all variable bits the active event currently affects.
            EnvironmentVariable mask = EnvironmentVariable.None;
            if (curCfg != null && curCfg.effects != null)
            {
                for (int i = 0; i < curCfg.effects.Length; i++)
                {
                    var e = curCfg.effects[i];
                    if (e != null) mask |= e.affected;
                }
            }

            if (eventStatusLabel)
            {
                if (curCfg != null)
                {
                    eventStatusLabel.text = $"Active: {curCfg.eventName}";
                }
                else if (endedCfg != null && (now - endedAtTime) <= endedLingerSeconds)
                {
                    eventStatusLabel.text = $"Ended: {endedCfg.eventName}";
                }
                else if (eventSystem && eventSystem.NextEvent != null)
                {
                    eventStatusLabel.text = $"Next: {eventSystem.NextEvent.eventName} in {FormatMMSS(eventSystem.SecondsUntilNext)}";
                }
                else
                {
                    eventStatusLabel.text = "—";
                }
            }

            ApplyHighlight(temperature, (mask & EnvironmentVariable.Temp)     != 0);
            ApplyHighlight(light,       (mask & EnvironmentVariable.Light)    != 0);
            ApplyHighlight(humidity,    (mask & EnvironmentVariable.Humidity) != 0);
            ApplyHighlight(toxicity,    (mask & EnvironmentVariable.Toxicity) != 0);
        }

        // ----- Private -----
        private static void ApplyRow(ValueSlider widget, EnvironmentVariable id, EnvironmentVariableConfig v)
        {
            if (!widget) return;
            widget.SetTitle(id.ToString());
            widget.SetRange(v.minValue, v.maxValue);
        }

        private static void ApplyHighlight(ValueSlider widget, bool affected)
        {
            if (widget) widget.SetHighlight(affected);
        }

        private static string FormatMMSS(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f) seconds = 0f;
            int total = Mathf.FloorToInt(seconds);
            int m = total / 60;
            int s = total % 60;
            return $"{m:00}:{s:00}";
        }
    }
}
