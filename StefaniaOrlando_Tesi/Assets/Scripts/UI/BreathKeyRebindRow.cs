using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Holobiont
{
    /*
     * One row of the rebind UI: a button + label that rebinds a single
     * BreathKey slot on a KeyboardBreathInput.
     *
     * Flow:
     *   - Click the button → row enters Listening; label shows the prompt.
     *   - The next non-modifier key press → that key is assigned and Listening exits.
     *   - Press Escape while Listening → cancel without changing the binding.
     *
     * While Listening, UIInputGate.Capturing is set so MainMenuView's Esc
     * toggle defers — otherwise pressing Escape to cancel would also close
     * the menu, and the user could never rebind Escape itself if they wanted to.
     */
    [DisallowMultipleComponent]
    public class BreathKeyRebindRow : MonoBehaviour
    {
        // ----- Source -----
        [Header("Source")]
        [Tooltip("Input driver whose binding this row edits.")]
        [SerializeField] private KeyboardBreathInput source;

        [Tooltip("Which of the three breath slots this row controls.")]
        [SerializeField] private BreathKey slot = BreathKey.Exhale;

        // ----- View -----
        [Header("View")]
        [Tooltip("Button that arms listen mode when clicked.")]
        [SerializeField] private Button button;

        [Tooltip("Label inside the button. Shows the current key, or the prompt while listening.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Optional prefix label to the left of the button (e.g. 'Exhale'). Set in editor; never overwritten by this script.")]
        [SerializeField] private TMP_Text slotLabel;

        [Tooltip("Text shown on the button while waiting for a key press.")]
        [SerializeField] private string listeningPrompt = "Press a key…";

        // ----- State -----
        private bool listening;

        // ----- Lifecycle -----
        private void OnEnable()
        {
            if (button) button.onClick.AddListener(BeginListening);

            if (slotLabel && string.IsNullOrEmpty(slotLabel.text))
                slotLabel.text = slot.ToString();

            RefreshLabel();
        }

        private void OnDisable()
        {
            if (button) button.onClick.RemoveListener(BeginListening);
            EndListening(consumedFlag: false);
        }

        private void Update()
        {
            if (!listening) return;
            if (!Input.anyKeyDown) return;

            // Cancel without rebinding.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                EndListening(consumedFlag: true);
                return;
            }

            // Find the first KeyCode that went down this frame and isn't a modifier
            // we want to ignore. Standard keyboard codes only — joystick/mouse skipped.
            for (int i = (int)KeyCode.Backspace; i <= (int)KeyCode.Menu; i++)
            {
                var code = (KeyCode)i;
                if (!Input.GetKeyDown(code)) continue;
                if (IsIgnoredModifier(code)) continue;

                if (source) source.SetKey(slot, code);
                EndListening(consumedFlag: true);
                return;
            }
        }

        // ----- Private -----
        private void BeginListening()
        {
            if (listening) return;
            listening = true;
            UIInputGate.Capturing = true;
            if (label) label.text = listeningPrompt;
        }

        private void EndListening(bool consumedFlag)
        {
            listening = false;
            if (consumedFlag) UIInputGate.Capturing = false;
            RefreshLabel();
        }

        private void RefreshLabel()
        {
            if (!label || !source) return;
            label.text = source.GetKey(slot).ToString();
        }

        private static bool IsIgnoredModifier(KeyCode code)
        {
            // Modifiers held alongside a real key shouldn't capture the binding by
            // themselves. The user can still rebind to one of these explicitly by
            // pressing it on its own (KeyDown fires for them too).
            return false; // Currently no ignore list — accept any key. Hook here later if needed.
        }
    }
}
