using System.Text;
using TMPro;
using UnityEngine;

namespace Holobiont
{
    /*
     * Auto-updating "Controls: E / I / Space" reference label.
     *
     * Reads the current bindings from a KeyboardBreathInput each frame so
     * rebinding via BreathKeyRebindRow is reflected without manual wiring.
     *
     * Drop on either the start menu or the pause menu (or both — they'll
     * stay in sync because both read the same source).
     */
    [DisallowMultipleComponent]
    public class BreathControlsLabel : MonoBehaviour
    {
        // ----- Source -----
        [Header("Source")]
        [Tooltip("Input driver to mirror. Required.")]
        [SerializeField] private KeyboardBreathInput source;

        // ----- View -----
        [Header("View")]
        [Tooltip("Label that receives the rendered string.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Format string. {0} = exhale key, {1} = inhale key, {2} = hold key.")]
        [SerializeField] private string format = "Exhale: <b>{0}</b>   Inhale: <b>{1}</b>   Hold: <b>{2}</b>";

        // ----- State -----
        private readonly StringBuilder sb = new StringBuilder();
        private KeyCode lastExhale = KeyCode.None;
        private KeyCode lastInhale = KeyCode.None;
        private KeyCode lastHold   = KeyCode.None;

        // ----- Lifecycle -----
        private void OnEnable()
        {
            // Force a render on enable regardless of last-known values.
            lastExhale = lastInhale = lastHold = KeyCode.None;
            Tick();
        }

        private void Update() => Tick();

        // ----- Private -----
        private void Tick()
        {
            if (!source || !label) return;

            var ex = source.GetKey(BreathKey.Exhale);
            var inh = source.GetKey(BreathKey.Inhale);
            var ho = source.GetKey(BreathKey.Hold);

            if (ex == lastExhale && inh == lastInhale && ho == lastHold) return;

            sb.Clear();
            sb.AppendFormat(format, FormatKey(ex), FormatKey(inh), FormatKey(ho));
            label.text = sb.ToString();

            lastExhale = ex;
            lastInhale = inh;
            lastHold   = ho;
        }

        private static string FormatKey(KeyCode code)
        {
            // Space reads better than "Space" with no spelling, but ToString is
            // already the friendly name for letters. Keep it simple.
            return code.ToString();
        }
    }
}
