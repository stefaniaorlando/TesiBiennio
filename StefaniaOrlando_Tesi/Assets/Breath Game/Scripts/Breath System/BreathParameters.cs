using UnityEngine;

/*
 * ============================================================================
 * BREATH PARAMETERS - Frequency and Depth Control
 * ============================================================================
 *
 * WHAT THIS DOES:
 * This component manages two key breathing parameters:
 * - FREQUENCY: How fast you breathe (cycles per second)
 * - DEPTH: How deep you breathe (amplitude of the wave)
 *
 * HOW IT WORKS:
 * - Each parameter has a BASELINE (resting value), MIN, and MAX
 * - When you hold W/S, frequency drifts toward max/min
 * - When you hold D/A, depth drifts toward max/min
 * - When you release keys, values drift back toward baseline
 *
 * THE "DRIFT" SYSTEM:
 * - "Approach Rate" = how fast values move TOWARD extremes when keys are held
 * - "Decay Rate" = how fast values return TO baseline when keys are released
 * - This creates a smooth, organic feel rather than instant snapping
 *
 * IMPORTANT:
 * This component is controlled by BreathSimulator. Don't call Tick() yourself!
 *
 * TUNING TIPS:
 * - Higher approach rate = more responsive controls
 * - Higher decay rate = faster return to normal breathing
 * - Wider min/max range = more dramatic control
 *
 * ============================================================================
 */

/// <summary>
/// Manages breath frequency and depth parameters with drift toward extremes
/// when input is held, and decay toward baseline when released.
/// </summary>
public class BreathParameters : MonoBehaviour
{
    // =========================================================================
    // FREQUENCY SETTINGS - How fast you breathe
    // =========================================================================

    [Header("Frequency Settings (Breath Speed)")]
    [Tooltip("Resting breath frequency in cycles per second. This is the 'normal' breathing rate the system returns to.")]
    [SerializeField] private float frequencyBaseline = 0.25f;

    [Tooltip("Minimum frequency (slowest breathing). Reached by holding S key.")]
    [SerializeField] private float frequencyMin = 0.1f;

    [Tooltip("Maximum frequency (fastest breathing). Reached by holding W key.")]
    [SerializeField] private float frequencyMax = 0.5f;

    [Tooltip("How fast frequency changes when W or S is held. Higher = more responsive.")]
    [SerializeField] private float frequencyApproachRate = 0.2f;

    [Tooltip("How fast frequency returns to baseline when keys are released. Higher = snappier return.")]
    [SerializeField] private float frequencyDecayRate = 0.1f;

    // =========================================================================
    // DEPTH SETTINGS - How deep you breathe
    // =========================================================================

    [Header("Depth Settings (Breath Amplitude)")]
    [Tooltip("Resting breath depth. This is the 'normal' breathing depth the system returns to.")]
    [SerializeField] private float depthBaseline = 0.5f;

    [Tooltip("Minimum depth (shallowest breathing). Reached by holding A key.")]
    [SerializeField] private float depthMin = 0.2f;

    [Tooltip("Maximum depth (deepest breathing). Reached by holding D key.")]
    [SerializeField] private float depthMax = 1.0f;

    [Tooltip("How fast depth changes when A or D is held. Higher = more responsive.")]
    [SerializeField] private float depthApproachRate = 0.5f;

    [Tooltip("How fast depth returns to baseline when keys are released. Higher = snappier return.")]
    [SerializeField] private float depthDecayRate = 0.3f;

    // =========================================================================
    // RUNTIME STATE - View these in the Inspector during Play mode
    // =========================================================================

    [Header("Runtime State (Read Only)")]
    [Tooltip("Current frequency value. Changes based on input and drift.")]
    [SerializeField] private float frequencyCurrent;

    [Tooltip("Current depth value. Changes based on input and drift.")]
    [SerializeField] private float depthCurrent;

    // =========================================================================
    // INPUT STATE - Set by BreathInputHandler
    // =========================================================================

    private bool pushingFrequencyUp;
    private bool pushingFrequencyDown;
    private bool pushingDepthUp;
    private bool pushingDepthDown;

    // =========================================================================
    // PUBLIC PROPERTIES
    // =========================================================================

    /// <summary>Current frequency value.</summary>
    public float FrequencyCurrent => frequencyCurrent;

    /// <summary>Current depth value.</summary>
    public float DepthCurrent => depthCurrent;

    /// <summary>The baseline frequency (resting value).</summary>
    public float FrequencyBaseline => frequencyBaseline;

    /// <summary>The baseline depth (resting value).</summary>
    public float DepthBaseline => depthBaseline;

    /// <summary>Frequency as 0-1 value within min-max range. Useful for UI.</summary>
    public float NormalizedFrequency => Mathf.InverseLerp(frequencyMin, frequencyMax, frequencyCurrent);

    /// <summary>Depth as 0-1 value within min-max range. Useful for UI.</summary>
    public float NormalizedDepth => Mathf.InverseLerp(depthMin, depthMax, depthCurrent);

    /// <summary>How far frequency is from baseline (0-1). Used for stamina drain.</summary>
    public float FrequencyDeviation => Mathf.Abs(frequencyCurrent - frequencyBaseline) / (frequencyMax - frequencyMin);

    /// <summary>How far depth is from baseline (0-1). Used for stamina drain.</summary>
    public float DepthDeviation => Mathf.Abs(depthCurrent - depthBaseline) / (depthMax - depthMin);

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    private void Start()
    {
        // Start at baseline values
        frequencyCurrent = frequencyBaseline;
        depthCurrent = depthBaseline;
    }

    // =========================================================================
    // TICK - Called by BreathSimulator each frame
    // =========================================================================

    /// <summary>
    /// Updates parameter drift based on input state.
    /// Called by BreathSimulator - do not call this manually!
    /// </summary>
    public void Tick(float deltaTime)
    {
        UpdateFrequency(deltaTime);
        UpdateDepth(deltaTime);
    }

    private void UpdateFrequency(float deltaTime)
    {
        if (pushingFrequencyUp)
        {
            // W held: drift toward max
            frequencyCurrent += frequencyApproachRate * deltaTime;
        }
        else if (pushingFrequencyDown)
        {
            // S held: drift toward min
            frequencyCurrent -= frequencyApproachRate * deltaTime;
        }
        else
        {
            // No input: drift back toward baseline
            frequencyCurrent = Mathf.MoveTowards(frequencyCurrent, frequencyBaseline, frequencyDecayRate * deltaTime);
        }

        // Clamp to valid range
        frequencyCurrent = Mathf.Clamp(frequencyCurrent, frequencyMin, frequencyMax);
    }

    private void UpdateDepth(float deltaTime)
    {
        if (pushingDepthUp)
        {
            // D held: drift toward max (deeper)
            depthCurrent += depthApproachRate * deltaTime;
        }
        else if (pushingDepthDown)
        {
            // A held: drift toward min (shallower)
            depthCurrent -= depthApproachRate * deltaTime;
        }
        else
        {
            // No input: drift back toward baseline
            depthCurrent = Mathf.MoveTowards(depthCurrent, depthBaseline, depthDecayRate * deltaTime);
        }

        // Clamp to valid range
        depthCurrent = Mathf.Clamp(depthCurrent, depthMin, depthMax);
    }

    // =========================================================================
    // INPUT METHODS - Called by BreathInputHandler
    // =========================================================================

    /// <summary>Set whether W key is held (increase frequency).</summary>
    public void PushFrequencyUp(bool active) => pushingFrequencyUp = active;

    /// <summary>Set whether S key is held (decrease frequency).</summary>
    public void PushFrequencyDown(bool active) => pushingFrequencyDown = active;

    /// <summary>Set whether D key is held (increase depth).</summary>
    public void PushDepthUp(bool active) => pushingDepthUp = active;

    /// <summary>Set whether A key is held (decrease depth).</summary>
    public void PushDepthDown(bool active) => pushingDepthDown = active;
}
