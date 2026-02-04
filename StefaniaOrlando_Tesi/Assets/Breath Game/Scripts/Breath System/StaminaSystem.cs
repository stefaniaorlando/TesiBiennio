using System;
using UnityEngine;

/*
 * ============================================================================
 * STAMINA SYSTEM - Energy Management
 * ============================================================================
 *
 * WHAT THIS DOES:
 * This component manages the player's "breath stamina" - a resource that:
 * - DRAINS when breathing deviates from the baseline (fast/slow/deep/shallow)
 * - DRAINS when holding breath (Space key)
 * - REGENERATES when breathing normally near baseline
 *
 * THE CORE MECHANIC:
 * Players can push their breathing to extremes for gameplay benefits, but
 * doing so costs stamina. This creates tension between power and sustainability.
 *
 * DRAIN CALCULATION:
 * - Drain rate = baseDrainRate × (frequencyDeviation + depthDeviation)
 * - The further you are from baseline on EITHER axis, the faster you drain
 * - Pushing BOTH axes simultaneously drains even faster (they stack!)
 *
 * WHAT HAPPENS WHEN STAMINA HITS ZERO:
 * - The OnStaminaDepleted event fires
 * - RecoveryController catches this and forces a "recovery" period
 * - During recovery, input is disabled and stamina regenerates faster
 *
 * IMPORTANT:
 * This component is controlled by BreathSimulator. Don't call Tick() yourself!
 *
 * ============================================================================
 */

/// <summary>
/// Manages stamina drain and regeneration based on breath parameter deviations.
/// Fires an event when stamina depletes.
/// </summary>
public class StaminaSystem : MonoBehaviour
{
    // =========================================================================
    // REFERENCES
    // =========================================================================

    [Header("References")]
    [Tooltip("BreathParameters component - used to check how far breathing deviates from baseline.")]
    [SerializeField] private BreathParameters parameters;

    [Tooltip("BreathOscillator component - used to check if breath is being held.")]
    [SerializeField] private BreathOscillator oscillator;

    // =========================================================================
    // STAMINA SETTINGS
    // =========================================================================

    [Header("Stamina Pool")]
    [Tooltip("Maximum stamina value. Default is 1 (normalized). You can increase this for a larger pool.")]
    [SerializeField] private float staminaMax = 1f;

    [Header("Drain Rates")]
    [Tooltip("Base stamina drain per second when at full deviation on ONE axis. Actual drain = this × total deviation.")]
    [SerializeField] private float baseDrainRate = 0.2f;

    [Tooltip("Stamina drain per second while holding breath (Space key). Breath-holding is taxing!")]
    [SerializeField] private float pauseDrainRate = 0.25f;

    [Header("Regeneration Rates")]
    [Tooltip("Stamina regeneration per second when breathing normally (near baseline, not paused).")]
    [SerializeField] private float regenRate = 0.15f;

    [Tooltip("Faster regeneration during recovery period. Ensures player recovers within the recovery time.")]
    [SerializeField] private float regenRateDuringRecovery = 0.3f;

    [Header("Baseline Tolerance")]
    [Tooltip("How close to baseline counts as 'resting' for regeneration. 0.05 = within 5% of the range.")]
    [SerializeField] private float baselineTolerance = 0.05f;

    // =========================================================================
    // RUNTIME STATE
    // =========================================================================

    [Header("Runtime State (Read Only)")]
    [Tooltip("Current stamina level. Watch this during play to see drain/regen in action.")]
    [SerializeField] private float staminaCurrent;

    private bool isInRecovery = false;

    // =========================================================================
    // EVENTS
    // =========================================================================

    /// <summary>
    /// Fired when stamina hits zero. RecoveryController listens for this.
    /// </summary>
    public event Action OnStaminaDepleted;

    // =========================================================================
    // PUBLIC PROPERTIES
    // =========================================================================

    /// <summary>Current stamina value (0 to staminaMax).</summary>
    public float StaminaCurrent => staminaCurrent;

    /// <summary>Maximum stamina value.</summary>
    public float StaminaMax => staminaMax;

    /// <summary>Stamina as 0-1 percentage. Useful for UI bars.</summary>
    public float NormalizedStamina => staminaCurrent / staminaMax;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    private void Start()
    {
        // Start with full stamina
        staminaCurrent = staminaMax;
    }

    // =========================================================================
    // TICK - Called by BreathSimulator each frame
    // =========================================================================

    /// <summary>
    /// Updates stamina drain/regeneration.
    /// Called by BreathSimulator - do not call this manually!
    /// </summary>
    public void Tick(float deltaTime)
    {
        // During recovery, just regenerate at accelerated rate
        if (isInRecovery)
        {
            staminaCurrent += regenRateDuringRecovery * deltaTime;
            staminaCurrent = Mathf.Min(staminaCurrent, staminaMax);
            return;
        }

        // Get current deviation from baseline (how "extreme" the breathing is)
        float frequencyDeviation = parameters != null ? parameters.FrequencyDeviation : 0f;
        float depthDeviation = parameters != null ? parameters.DepthDeviation : 0f;
        bool isPaused = oscillator != null && oscillator.IsPaused;

        // Check if player is breathing normally (near baseline on both axes)
        bool isNearBaseline = frequencyDeviation < baselineTolerance && depthDeviation < baselineTolerance;

        if (isPaused)
        {
            // Holding breath drains stamina
            staminaCurrent -= pauseDrainRate * deltaTime;
        }
        else if (isNearBaseline)
        {
            // Breathing normally = regenerate
            staminaCurrent += regenRate * deltaTime;
        }
        else
        {
            // Breathing at extremes = drain based on total deviation
            float drainRate = baseDrainRate * (frequencyDeviation + depthDeviation);
            staminaCurrent -= drainRate * deltaTime;
        }

        // Keep stamina within valid range
        staminaCurrent = Mathf.Clamp(staminaCurrent, 0f, staminaMax);

        // Check for depletion
        if (staminaCurrent <= 0f)
        {
            OnStaminaDepleted?.Invoke();
        }
    }

    // =========================================================================
    // PUBLIC METHODS
    // =========================================================================

    /// <summary>
    /// Called by RecoveryController when recovery state changes.
    /// During recovery, stamina regenerates faster.
    /// </summary>
    public void SetRecoveryState(bool recovering)
    {
        isInRecovery = recovering;
    }
}
