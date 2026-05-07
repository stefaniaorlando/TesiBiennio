using UnityEngine;

namespace Holobiont
{
    /*
     * Reads keyboard input each tick and routes it to the appropriate breath
     * subsystems. During recovery, all input is suppressed so parameters drift
     * (and BreathParameters' compensation overshoot can run unimpeded).
     *
     * Driven by BreathSimulator — do not call Tick() directly.
     */
    public class BreathInputHandler : MonoBehaviour
    {
        // =========================================================================
        // REFERENCES
        // =========================================================================

        [Header("References")]
        [Tooltip("BreathParameters component - receives frequency and depth input.")]
        [SerializeField] private BreathParameters parameters;

        [Tooltip("BreathOscillator component - receives pause (breath hold) input.")]
        [SerializeField] private BreathOscillator oscillator;

        [Tooltip("RecoveryController component - checked to see if input should be ignored.")]
        [SerializeField] private RecoveryController recoveryController;

        // =========================================================================
        // KEY BINDINGS
        // =========================================================================

        [Header("Key Bindings")]
        [Tooltip("Key to increase breathing frequency (breathe faster). Default: W")]
        [SerializeField] private KeyCode increaseFrequencyKey = KeyCode.W;

        [Tooltip("Key to decrease breathing frequency (breathe slower). Default: S")]
        [SerializeField] private KeyCode decreaseFrequencyKey = KeyCode.S;

        [Tooltip("Key to increase breathing depth (breathe deeper). Default: D")]
        [SerializeField] private KeyCode increaseDepthKey = KeyCode.D;

        [Tooltip("Key to decrease breathing depth (breathe shallower). Default: A")]
        [SerializeField] private KeyCode decreaseDepthKey = KeyCode.A;

        [Tooltip("Key to hold breath (pause the oscillator). Default: Space")]
        [SerializeField] private KeyCode pauseKey = KeyCode.Space;

        // =========================================================================
        // TICK
        // =========================================================================

        public void Tick()
        {
            bool isRecovering = recoveryController != null && recoveryController.IsRecovering;

            if (parameters != null)
            {
                if (isRecovering)
                {
                    // During recovery, release all input so compensation drift drives the parameters.
                    parameters.PushFrequencyUp(false);
                    parameters.PushFrequencyDown(false);
                    parameters.PushDepthUp(false);
                    parameters.PushDepthDown(false);
                }
                else
                {
                    parameters.PushFrequencyUp(Input.GetKey(increaseFrequencyKey));
                    parameters.PushFrequencyDown(Input.GetKey(decreaseFrequencyKey));
                    parameters.PushDepthUp(Input.GetKey(increaseDepthKey));
                    parameters.PushDepthDown(Input.GetKey(decreaseDepthKey));
                }
            }

            if (oscillator != null)
            {
                if (isRecovering)
                    oscillator.SetPaused(false);
                else
                    oscillator.SetPaused(Input.GetKey(pauseKey));
            }
        }
    }
}
