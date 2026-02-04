using System;
using UnityEngine;

/*
 * ============================================================================
 * RECOVERY CONTROLLER - Exhaustion and Recovery
 * ============================================================================
 *
 * WHAT THIS DOES:
 * When stamina is fully depleted, this component forces a "recovery" period
 * where the player must catch their breath before regaining control.
 *
 * THE RECOVERY MECHANIC:
 * 1. Stamina hits zero → OnStaminaDepleted event fires
 * 2. This controller catches that event and starts recovery
 * 3. During recovery:
 *    - All input is ignored (BreathInputHandler checks IsRecovering)
 *    - Any held breath is forcibly released
 *    - Stamina regenerates at an accelerated rate
 *    - A timer counts down
 * 4. When the timer reaches zero, recovery ends and control returns
 *
 * WHY THIS EXISTS:
 * This creates consequences for over-exertion. Players can push their breathing
 * to extremes, but if they don't manage their stamina, they'll be temporarily
 * vulnerable during recovery.
 *
 * IMPORTANT:
 * This component is controlled by BreathSimulator. Don't call Tick() yourself!
 *
 * TUNING TIPS:
 * - Longer recoveryDuration = harsher penalty for depletion
 * - Make sure StaminaSystem.regenRateDuringRecovery is high enough that
 *   stamina refills before recovery ends
 *
 * ============================================================================
 */

/// <summary>
/// Manages recovery state triggered by stamina depletion.
/// During recovery, input is blocked and a timer counts down until control returns.
/// </summary>
public class RecoveryController : MonoBehaviour
{
    // =========================================================================
    // REFERENCES
    // =========================================================================

    [Header("References")]
    [Tooltip("StaminaSystem component - listens for the OnStaminaDepleted event.")]
    [SerializeField] private StaminaSystem staminaSystem;

    [Tooltip("BreathOscillator component - used to force-release held breath when recovery starts.")]
    [SerializeField] private BreathOscillator oscillator;

    // =========================================================================
    // RECOVERY SETTINGS
    // =========================================================================

    [Header("Recovery Settings")]
    [Tooltip("How long the recovery period lasts in seconds. During this time, input is disabled.")]
    [SerializeField] private float recoveryDuration = 3f;

    // =========================================================================
    // RUNTIME STATE
    // =========================================================================

    [Header("Runtime State (Read Only)")]
    [Tooltip("Is the player currently in recovery? (Input disabled)")]
    [SerializeField] private bool isRecovering = false;

    [Tooltip("Time remaining until recovery ends.")]
    [SerializeField] private float recoveryTimer = 0f;

    // =========================================================================
    // EVENTS
    // =========================================================================

    /// <summary>Fired when recovery period begins.</summary>
    public event Action OnRecoveryStarted;

    /// <summary>Fired when recovery period ends and control returns.</summary>
    public event Action OnRecoveryEnded;

    // =========================================================================
    // PUBLIC PROPERTIES
    // =========================================================================

    /// <summary>Is the player currently in recovery mode?</summary>
    public bool IsRecovering => isRecovering;

    /// <summary>Time remaining in recovery (seconds).</summary>
    public float RecoveryTimer => recoveryTimer;

    /// <summary>Total recovery duration (seconds).</summary>
    public float RecoveryDuration => recoveryDuration;

    /// <summary>Recovery progress: 1 = just started, 0 = about to end. Useful for UI.</summary>
    public float NormalizedRecovery => recoveryDuration > 0 ? recoveryTimer / recoveryDuration : 0f;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    private void OnEnable()
    {
        // Subscribe to stamina depletion event
        if (staminaSystem != null)
        {
            staminaSystem.OnStaminaDepleted += HandleStaminaDepleted;
        }
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        if (staminaSystem != null)
        {
            staminaSystem.OnStaminaDepleted -= HandleStaminaDepleted;
        }
    }

    // =========================================================================
    // TICK - Called by BreathSimulator each frame
    // =========================================================================

    /// <summary>
    /// Updates recovery timer.
    /// Called by BreathSimulator - do not call this manually!
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (!isRecovering)
            return;

        // Count down the recovery timer
        recoveryTimer -= deltaTime;

        // Check if recovery is complete
        if (recoveryTimer <= 0f)
        {
            EndRecovery();
        }
    }

    // =========================================================================
    // PRIVATE METHODS
    // =========================================================================

    private void HandleStaminaDepleted()
    {
        // Don't start recovery if already recovering
        if (isRecovering)
            return;

        StartRecovery();
    }

    private void StartRecovery()
    {
        isRecovering = true;
        recoveryTimer = recoveryDuration;

        // Force release any held breath (can't hold breath while exhausted!)
        if (oscillator != null)
        {
            oscillator.SetPaused(false);
        }

        // Tell stamina system we're in recovery (triggers faster regen)
        if (staminaSystem != null)
        {
            staminaSystem.SetRecoveryState(true);
        }

        // Fire event for other systems to react
        OnRecoveryStarted?.Invoke();
    }

    private void EndRecovery()
    {
        isRecovering = false;
        recoveryTimer = 0f;

        // Tell stamina system recovery is over
        if (staminaSystem != null)
        {
            staminaSystem.SetRecoveryState(false);
        }

        // Fire event for other systems to react
        OnRecoveryEnded?.Invoke();
    }
}
