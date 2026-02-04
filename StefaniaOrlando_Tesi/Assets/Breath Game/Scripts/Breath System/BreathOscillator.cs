using UnityEngine;

/*
 * ============================================================================
 * BREATH OSCILLATOR - The Breathing Wave Generator
 * ============================================================================
 *
 * WHAT THIS DOES:
 * This component generates a continuous breathing wave - like a sine wave that
 * bounces between 0 (lungs empty) and 1 (lungs full). It's the "heartbeat" of
 * the breath simulation.
 *
 * HOW IT WORKS:
 * - The "phase" value goes from 0 to 1 (inhale) then back to 0 (exhale)
 * - "Frequency" controls how fast it cycles (higher = faster breathing)
 * - "Depth" scales the output (lower depth = shallower breaths)
 * - The final "Displacement" = phase × depth
 *
 * IMPORTANT:
 * This component is controlled by BreathSimulator. Don't call Tick() yourself!
 * The BreathSimulator orchestrates all the components in the correct order.
 *
 * CUSTOMIZATION:
 * Most students won't need to modify this script. If you need breath data,
 * read it from BreathSimulator instead (breath.Displacement, breath.IsInhaling, etc.)
 *
 * ============================================================================
 */

/// <summary>
/// Core breath oscillator that produces a continuous breathing wave.
/// Phase bounces between 0 (empty) and 1 (full) at a configurable frequency.
/// Displacement is scaled by depth from BreathParameters.
/// </summary>
public class BreathOscillator : MonoBehaviour
{
    // =========================================================================
    // REFERENCES
    // =========================================================================

    [Header("References")]
    [Tooltip("The BreathParameters component that provides frequency and depth values. If not assigned, fallback values are used.")]
    [SerializeField] private BreathParameters parameters;

    // =========================================================================
    // FALLBACK SETTINGS - Used only if BreathParameters is not assigned
    // =========================================================================

    [Header("Fallback Settings")]
    [Tooltip("Breath cycles per second when BreathParameters is not assigned. 0.25 = one full breath every 4 seconds.")]
    [SerializeField] private float fallbackFrequency = 0.25f;

    [Tooltip("Breath depth (amplitude) when BreathParameters is not assigned. 1.0 = full range.")]
    [SerializeField] private float fallbackDepth = 1.0f;

    // =========================================================================
    // RUNTIME STATE - View these in the Inspector during Play mode
    // =========================================================================

    [Header("Runtime State (Read Only)")]
    [Tooltip("Current position in the breath cycle. 0 = empty, 1 = full.")]
    [SerializeField] private float phase = 0f;

    [Tooltip("Current direction: +1 = inhaling (filling up), -1 = exhaling (emptying).")]
    [SerializeField] private int direction = 1;

    [Tooltip("Is the breath currently being held? (Space key)")]
    [SerializeField] private bool isPaused = false;

    // =========================================================================
    // COMPUTED VALUES - Calculated each frame
    // =========================================================================

    private float displacement;      // phase × depth = actual lung fill
    private float velocity;          // how fast displacement is changing
    private float previousDisplacement;

    // =========================================================================
    // PUBLIC PROPERTIES - Read these from other scripts
    // =========================================================================

    /// <summary>Current position in breath cycle (0-1).</summary>
    public float Phase => phase;

    /// <summary>Current direction: +1 = inhaling, -1 = exhaling.</summary>
    public int Direction => direction;

    /// <summary>Is breath being held?</summary>
    public bool IsPaused => isPaused;

    /// <summary>Current lung fill level (phase × depth).</summary>
    public float Displacement => displacement;

    /// <summary>Rate of change of displacement per second.</summary>
    public float Velocity => velocity;

    /// <summary>Current frequency from BreathParameters (or fallback).</summary>
    public float Frequency => parameters != null ? parameters.FrequencyCurrent : fallbackFrequency;

    /// <summary>Current depth from BreathParameters (or fallback).</summary>
    public float Depth => parameters != null ? parameters.DepthCurrent : fallbackDepth;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    private void Start()
    {
        // Initialize to beginning of breath cycle
        phase = 0f;
        direction = 1;
        displacement = 0f;
        previousDisplacement = 0f;
    }

    // =========================================================================
    // TICK - Called by BreathSimulator each frame
    // =========================================================================

    /// <summary>
    /// Advances the oscillator by deltaTime.
    /// Called by BreathSimulator - do not call this manually!
    /// </summary>
    public void Tick(float deltaTime)
    {
        float currentFrequency = Frequency;
        float currentDepth = Depth;

        // If paused (holding breath), freeze everything
        if (isPaused)
        {
            velocity = 0f;
            return;
        }

        // Remember previous value for velocity calculation
        previousDisplacement = displacement;

        // Advance phase based on frequency and direction
        // We multiply by 2 because a full cycle goes 0→1→0 (two half-cycles)
        float phaseDelta = currentFrequency * 2f * deltaTime;
        phase += direction * phaseDelta;

        // Bounce at boundaries (reverse direction at top and bottom)
        if (phase >= 1f)
        {
            phase = 1f;
            direction = -1;  // Start exhaling
        }
        else if (phase <= 0f)
        {
            phase = 0f;
            direction = 1;   // Start inhaling
        }

        // Calculate final displacement (phase scaled by depth)
        displacement = phase * currentDepth;

        // Calculate velocity (how fast displacement is changing)
        if (deltaTime > 0f)
        {
            velocity = (displacement - previousDisplacement) / deltaTime;
        }
    }

    // =========================================================================
    // PUBLIC METHODS - Called by other breath components
    // =========================================================================

    /// <summary>
    /// Sets the pause (breath hold) state.
    /// Called by BreathInputHandler when Space is pressed/released.
    /// </summary>
    public void SetPaused(bool paused)
    {
        isPaused = paused;
    }

    /// <summary>
    /// Toggles the pause state on/off.
    /// </summary>
    public void TogglePause()
    {
        isPaused = !isPaused;
    }
}
