using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Holobiont
{
    /*
     * Reusable read-only value display:
     *   - title label
     *   - main horizontal Slider
     *   - signed numeric value label ("+12.3" / "-4.5" / "0.0")
     *   - highlight GameObject toggled on/off externally
     *
     * Dumb view: it owns no model and never polls. Drivers feed it data via
     * SetTitle / SetRange / SetValue / SetHighlight and the widget renders
     * exactly what was last pushed. Decimals are a per-instance inspector setting.
     *
     * The highlight is a GameObject so the prefab can park multiple visuals
     * (frame, glow, badge, etc.) under one parent and have them all toggle
     * together.
     *
     * Wiring on the prefab: drop the child references into the slots. Any of
     * them may be left null — that feature is skipped. The Slider should have
     * Interactable = false for display-only rows.
     */
    [DisallowMultipleComponent]
    public class ValueSlider : MonoBehaviour
    {
        // ----- View refs -----
        [Header("View")]
        [Tooltip("Optional. Static row title (e.g. 'Temperature').")]
        [SerializeField] private TMP_Text titleLabel;

        [Tooltip("Main slider. minValue/maxValue get overwritten by SetRange.")]
        [SerializeField] private Slider slider;

        [Tooltip("Optional. Numeric label rendered with an explicit + or - sign.")]
        [SerializeField] private TMP_Text valueLabel;

        [Tooltip("Optional. Parent GameObject for highlight visuals (frame, glow, etc.). Toggled by SetHighlight.")]
        [SerializeField] private GameObject highlight;

        // ----- Format -----
        [Header("Format")]
        [Tooltip("Decimal places shown on the value label.")]
        [SerializeField, Range(0, 3)] private int decimals = 1;

        // ----- Public API -----
        /// <summary>Set the row title. No-op if no title label is wired.</summary>
        public void SetTitle(string title)
        {
            if (titleLabel) titleLabel.text = title;
        }

        /// <summary>Set the slider's display range.</summary>
        public void SetRange(float min, float max)
        {
            if (!slider) return;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;
        }

        /// <summary>Push the current value. Updates slider position and signed numeric label.</summary>
        public void SetValue(float value)
        {
            if (slider) slider.SetValueWithoutNotify(value);
            if (valueLabel) valueLabel.text = FormatSigned(value, decimals);
        }

        /// <summary>Push the current value with a custom label string (bypasses the signed numeric format).</summary>
        public void SetValue(float value, string overrideLabel)
        {
            if (slider) slider.SetValueWithoutNotify(value);
            if (valueLabel) valueLabel.text = overrideLabel;
        }

        /// <summary>Show or hide the highlight GameObject.</summary>
        public void SetHighlight(bool active)
        {
            if (highlight && highlight.activeSelf != active) highlight.SetActive(active);
        }

        // ----- Private -----
        // Three-section format strings give "+0.0" / "-0.0" / "0.0" without
        // allocating a new format string on every push.
        private static string FormatSigned(float v, int decimals)
        {
            switch (decimals)
            {
                case 0:  return v.ToString("+0;-0;0");
                case 1:  return v.ToString("+0.0;-0.0;0.0");
                case 2:  return v.ToString("+0.00;-0.00;0.00");
                case 3:  return v.ToString("+0.000;-0.000;0.000");
                default: return v.ToString("+0.0;-0.0;0.0");
            }
        }
    }
}
