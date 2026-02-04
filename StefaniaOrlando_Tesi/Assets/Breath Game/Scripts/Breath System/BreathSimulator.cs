using UnityEngine;
using UnityEngine.Events;

/*
 * ============================================================================
 * BREATH SIMULATOR - Central Orchestrator
 * ============================================================================
 *
 * WHAT THIS DOES:
 * This is the "brain" of the breath simulation system. It coordinates all the
 * other components (oscillator, parameters, stamina, recovery, input) and
 * provides a single, clean interface for other game systems to use.
 *
 * HOW TO USE IT:
 * 1. Add this component to your BreathSystem GameObject
 * 2. Drag all the other breath components into the reference slots
 * 3. Other scripts only need a reference to THIS component to access breath data
 *
 * FOR OTHER SCRIPTS - Reading breath data:
 *     [SerializeField] BreathSimulator breath;
 *
 *     void Update() {
 *         float lungFill = breath.Displacement;  // How full are the lungs (0-1)
 *         bool inhaling = breath.IsInhaling;     // Currently breathing in?
 *         float energy = breath.Stamina;         // Current stamina (0-1)
 *     }
 *
 * FOR OTHER SCRIPTS - Reacting to events (no code needed!):
 *     In the Inspector, expand "Events" and click + to add listeners.
 *     Example: OnInhaleStart -> PlaySound("inhale")
 *
 * ============================================================================
 */

/// <summary>
/// Central orchestrator for the breath simulation system.
/// Provides a clean API and UnityEvents for other game systems to connect to.
/// </summary>
public class BreathSimulator : MonoBehaviour
{
    // =========================================================================
    // REFERENCES - Drag the other breath components here
    // =========================================================================

    [Header("Subsystem References")]
    [Tooltip("The oscillator that generates the breathing wave. Required.")]
    [SerializeField] private BreathOscillator oscillator;

    [Tooltip("Manages frequency and depth parameters. Required.")]
    [SerializeField] private BreathParameters parameters;

    [Tooltip("Handles stamina drain and regeneration. Required.")]
    [SerializeField] private StaminaSystem staminaSystem;

    [Tooltip("Manages the recovery state when stamina is depleted. Required.")]
    [SerializeField] private RecoveryController recoveryController;

    [Tooltip("Reads player keyboard input. Required.")]
    [SerializeField] private BreathInputHandler inputHandler;
    
    // =========================================================================
    // PAUSE BOOST - Holding breath amplifies the opposite phase
    // =========================================================================

    [Header("Pause Boost")]
    [Tooltip("Enable pause boost: holding breath during inhale boosts next exhale, and vice versa.")]
    [SerializeField] private bool pauseBoostEnabled = true;

    [Tooltip("Boost multiplier applied when releasing pause into opposite phase.")]
    [SerializeField] private float pauseBoostMultiplier = 2f;

    [Tooltip("How quickly the boost decays after being applied.")]
    [SerializeField] private float pauseBoostDecayRate = 3f;

    // =========================================================================
    // EVENTS - Wire these up in the Inspector to react to breath changes
    // =========================================================================

    [Header("Events - Breath Cycle")]
    [Tooltip("Fired when inhalation begins. Use this to trigger inhale sounds, animations, or charge-up effects.")]
    public UnityEvent OnInhaleStart;

    [Tooltip("Fired when exhalation begins. Use this to trigger exhale sounds, release attacks, or apply effects.")]
    public UnityEvent OnExhaleStart;

    [Tooltip("Fired when breath is held (Space pressed). Use this for stealth, aiming, or focus mechanics.")]
    public UnityEvent OnPauseStart;

    [Tooltip("Fired when breath hold ends (Space released). Use this to resume normal behavior.")]
    public UnityEvent OnPauseEnd;

    [Header("Events - Stamina")]
    [Tooltip("Fired when stamina drops below 25%. Use this for warning effects (screen flash, sound).")]
    public UnityEvent OnStaminaLow;

    [Tooltip("Fired when stamina hits zero. Use this for exhaustion effects.")]
    public UnityEvent OnStaminaDepleted;

    [Tooltip("Fired when stamina is fully restored. Use this for 'ready' feedback.")]
    public UnityEvent OnStaminaFull;

    [Header("Events - Recovery")]
    [Tooltip("Fired when recovery begins (catching breath). Use this to show recovery UI or disable abilities.")]
    public UnityEvent OnRecoveryStart;

    [Tooltip("Fired when recovery ends and control returns. Use this to re-enable abilities.")]
    public UnityEvent OnRecoveryEnd;

    // =========================================================================
    // PRIVATE STATE - Used internally for detecting changes
    // =========================================================================

    private int previousDirection;
    private bool previousPauseState;
    private bool previousRecoveryState;
    private bool staminaWasLow;
    private bool staminaWasFull;

    // Pause boost state
    private int pausedInDirection = 0;  // Which phase we paused in: +1 inhale, -1 exhale
    private float currentBoost = 1f;    // Current active boost multiplier

    // =========================================================================
    // PUBLIC API - Use these properties from other scripts
    // =========================================================================

    /// <summary>
    /// Current lung fill level (0 = empty, up to depth max when full).
    /// This is the primary value for breath-reactive mechanics.
    /// Example: Scale a character's glow based on Displacement.
    /// </summary>
    public float Displacement => oscillator != null ? oscillator.Displacement : 0f;

    /// <summary>
    /// Raw oscillator phase (0-1) before depth scaling.
    /// Useful when you want the pure cycle position regardless of depth setting.
    /// </summary>
    public float Phase => oscillator != null ? oscillator.Phase : 0f;

    /// <summary>
    /// Rate of change of displacement per second.
    /// Positive = inhaling, negative = exhaling, zero = paused.
    /// Automatically includes pause boost when active.
    /// Example: Use velocity to scale particle emission rate.
    /// </summary>
    public float Velocity => oscillator != null ? oscillator.Velocity * currentBoost : 0f;

    /// <summary>
    /// Current breath direction as an integer.
    /// +1 = inhaling, -1 = exhaling.
    /// </summary>
    public int Direction => oscillator != null ? oscillator.Direction : 1;

    /// <summary>
    /// True if currently inhaling (breathing in).
    /// Example: if (breath.IsInhaling) { ChargeAttack(); }
    /// </summary>
    public bool IsInhaling => Direction > 0;

    /// <summary>
    /// True if currently exhaling (breathing out).
    /// Example: if (breath.IsExhaling) { ReleaseAttack(); }
    /// </summary>
    public bool IsExhaling => Direction < 0;

    /// <summary>
    /// True if breath is being held (Space key pressed).
    /// Example: if (breath.IsPaused) { EnableStealthMode(); }
    /// </summary>
    public bool IsPaused => oscillator != null && oscillator.IsPaused;

    /// <summary>
    /// Current breathing frequency in cycles per second.
    /// Higher = faster breathing. Affected by W/S keys.
    /// </summary>
    public float Frequency => oscillator != null ? oscillator.Frequency : 0f;

    /// <summary>
    /// Current breath depth (amplitude).
    /// Higher = deeper breaths. Affected by A/D keys.
    /// </summary>
    public float Depth => oscillator != null ? oscillator.Depth : 0f;

    /// <summary>
    /// Current stamina level from 0 (empty) to 1 (full).
    /// Example: staminaBar.fillAmount = breath.Stamina;
    /// </summary>
    public float Stamina => staminaSystem != null ? staminaSystem.NormalizedStamina : 1f;

    /// <summary>
    /// True if currently in recovery mode (catching breath, input disabled).
    /// Example: if (breath.IsRecovering) { ShowRecoveryOverlay(); }
    /// </summary>
    public bool IsRecovering => recoveryController != null && recoveryController.IsRecovering;

    /// <summary>
    /// Recovery progress from 1 (just started) to 0 (about to end).
    /// Example: recoveryBar.fillAmount = breath.RecoveryProgress;
    /// </summary>
    public float RecoveryProgress => recoveryController != null ? recoveryController.NormalizedRecovery : 0f;

    /// <summary>
    /// Frequency as a normalized 0-1 value within its min-max range.
    /// Useful for UI sliders or visual feedback.
    /// </summary>
    public float NormalizedFrequency => parameters != null ? parameters.NormalizedFrequency : 0.5f;

    /// <summary>
    /// Depth as a normalized 0-1 value within its min-max range.
    /// Useful for UI sliders or visual feedback.
    /// </summary>
    public float NormalizedDepth => parameters != null ? parameters.NormalizedDepth : 0.5f;

    /// <summary>
    /// Current boost multiplier from pause boost mechanic.
    /// Returns > 1 when actively boosted, decays back to 1 over time.
    /// Abilities should multiply their effects by this value.
    /// </summary>
    public float Boost => currentBoost;

    /// <summary>
    /// Whether pause boost is currently enabled.
    /// </summary>
    public bool PauseBoostEnabled => pauseBoostEnabled;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    private void Start()
    {
        // Initialize state tracking for event detection
        if (oscillator != null)
        {
            previousDirection = oscillator.Direction;
            previousPauseState = oscillator.IsPaused;
        }

        if (recoveryController != null)
        {
            previousRecoveryState = recoveryController.IsRecovering;
        }

        staminaWasLow = false;
        staminaWasFull = true;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        // Tick all subsystems in the correct order
        // This order is important for proper data flow!

        // 1. Input (reads what the player wants to do)
        if (inputHandler != null)
        {
            inputHandler.Tick();
        }

        // 2. Parameters (applies input to frequency/depth values)
        if (parameters != null)
        {
            parameters.Tick(deltaTime);
        }

        // 3. Stamina (drains or regenerates based on current state)
        if (staminaSystem != null)
        {
            staminaSystem.Tick(deltaTime);
        }

        // 4. Recovery (checks if stamina depleted, counts down recovery timer)
        if (recoveryController != null)
        {
            recoveryController.Tick(deltaTime);
        }

        // 5. Oscillator (advances the breath cycle using current frequency/depth)
        if (oscillator != null)
        {
            oscillator.Tick(deltaTime);
        }

        // 6. Update pause boost decay
        UpdatePauseBoost(deltaTime);

        // 7. Fire any events that should trigger this frame
        CheckAndFireEvents();
    }

    // =========================================================================
    // PAUSE BOOST LOGIC
    // =========================================================================

    private void UpdatePauseBoost(float deltaTime)
    {
        if (!pauseBoostEnabled)
        {
            currentBoost = 1f;
            return;
        }

        // Decay boost back to 1
        if (currentBoost > 1f)
        {
            currentBoost -= pauseBoostDecayRate * deltaTime;
            currentBoost = Mathf.Max(1f, currentBoost);
        }
    }

    // =========================================================================
    // EVENT DETECTION - Checks for state changes and fires UnityEvents
    // =========================================================================

    private void CheckAndFireEvents()
    {
        // Check breath cycle events
        if (oscillator != null)
        {
            // Did breathing direction change?
            int currentDirection = oscillator.Direction;
            if (currentDirection != previousDirection)
            {
                if (currentDirection > 0)
                    OnInhaleStart?.Invoke();
                else
                    OnExhaleStart?.Invoke();

                previousDirection = currentDirection;
            }

            // Did pause state change?
            bool currentPauseState = oscillator.IsPaused;
            if (currentPauseState != previousPauseState)
            {
                if (currentPauseState)
                {
                    // Starting pause - record which phase we're in
                    pausedInDirection = currentDirection;
                    OnPauseStart?.Invoke();
                }
                else
                {
                    // Ending pause - check if we should apply boost
                    // Boost applies when releasing into the OPPOSITE phase
                    // (pause during inhale -> boost exhale, and vice versa)
                    if (pauseBoostEnabled && pausedInDirection != 0 && currentDirection != pausedInDirection)
                    {
                        currentBoost = pauseBoostMultiplier;
                    }
                    pausedInDirection = 0;
                    OnPauseEnd?.Invoke();
                }

                previousPauseState = currentPauseState;
            }
        }

        // Check stamina events
        if (staminaSystem != null)
        {
            float stamina = staminaSystem.NormalizedStamina;

            // Did stamina drop below 25%?
            bool isLow = stamina < 0.25f;
            if (isLow && !staminaWasLow)
            {
                OnStaminaLow?.Invoke();
            }
            staminaWasLow = isLow;

            // Did stamina reach 100%?
            bool isFull = stamina >= 1f;
            if (isFull && !staminaWasFull)
            {
                OnStaminaFull?.Invoke();
            }
            staminaWasFull = isFull;

            // Did stamina hit zero?
            if (stamina <= 0f)
            {
                OnStaminaDepleted?.Invoke();
            }
        }

        // Check recovery events
        if (recoveryController != null)
        {
            bool currentRecoveryState = recoveryController.IsRecovering;
            if (currentRecoveryState != previousRecoveryState)
            {
                if (currentRecoveryState)
                    OnRecoveryStart?.Invoke();
                else
                    OnRecoveryEnd?.Invoke();

                previousRecoveryState = currentRecoveryState;
            }
        }
    }
}
