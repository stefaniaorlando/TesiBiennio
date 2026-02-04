using UnityEngine;

/*
 * ============================================================================
 * BREATH INPUT HANDLER - Keyboard Controls
 * ============================================================================
 *
 * WHAT THIS DOES:
 * This component reads keyboard input and routes it to the appropriate
 * breath components. It's the bridge between player intention and system state.
 *
 * DEFAULT CONTROLS:
 * - W: Breathe faster (increase frequency)
 * - S: Breathe slower (decrease frequency)
 * - D: Breathe deeper (increase depth)
 * - A: Breathe shallower (decrease depth)
 * - Space: Hold breath (pause oscillator)
 *
 * RECOVERY BEHAVIOR:
 * When the player is in recovery (stamina depleted), ALL input is ignored.
 * This is checked by looking at RecoveryController.IsRecovering.
 *
 * CUSTOMIZATION:
 * You can change the key bindings in the Inspector without modifying code.
 * Just change the KeyCode values for each action.
 *
 * IMPORTANT:
 * This component is controlled by BreathSimulator. Don't call Tick() yourself!
 *
 * ============================================================================
 */

/// <summary>
/// Reads keyboard input and routes to BreathParameters and BreathOscillator.
/// Respects recovery state - all input ignored during recovery.
/// </summary>
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
    // KEY BINDINGS - Change these in the Inspector!
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
    // TICK - Called by BreathSimulator each frame
    // =========================================================================

    /// <summary>
    /// Processes keyboard input and routes to appropriate components.
    /// Called by BreathSimulator - do not call this manually!
    /// </summary>
    public void Tick()
    {
        // Check if we're in recovery - if so, ignore ALL input
        bool isRecovering = recoveryController != null && recoveryController.IsRecovering;

        // Handle frequency and depth input
        if (parameters != null)
        {
            if (isRecovering)
            {
                // During recovery, release all parameter inputs
                // This allows the system to drift back to baseline
                parameters.PushFrequencyUp(false);
                parameters.PushFrequencyDown(false);
                parameters.PushDepthUp(false);
                parameters.PushDepthDown(false);
            }
            else
            {
                // Normal input handling
                parameters.PushFrequencyUp(Input.GetKey(increaseFrequencyKey));
                parameters.PushFrequencyDown(Input.GetKey(decreaseFrequencyKey));
                parameters.PushDepthUp(Input.GetKey(increaseDepthKey));
                parameters.PushDepthDown(Input.GetKey(decreaseDepthKey));
            }
        }

        // Handle pause (breath hold) input
        if (oscillator != null)
        {
            if (isRecovering)
            {
                // Can't hold breath during recovery - you're already out of breath!
                oscillator.SetPaused(false);
            }
            else
            {
                // Normal pause handling
                oscillator.SetPaused(Input.GetKey(pauseKey));
            }
        }
    }
}
