using UnityEngine;
using UnityEngine.Events;

namespace Holobiont
{
    /*
     * Central orchestrator for the breath simulation. Ticks each subsystem in
     * the right order, manages pause-boost, fires the breath-cycle UnityEvents,
     * and exposes IBreathInput — the data contract every consumer (holobiont,
     * abilities, view) reads from.
     *
     * Time source: GameClock.Instance.DeltaTime when present (so breath pauses
     * with the game), falling back to Time.deltaTime in scenes without a clock
     * (e.g. the breath standalone scene).
     */
    public class BreathSimulator : MonoBehaviour, IBreathInput
    {
        // =========================================================================
        // CONFIG / REFERENCES
        // =========================================================================

        [Header("Config")]
        [Tooltip("Tuning data; supplies pause-boost parameters.")]
        [SerializeField] private BreathConfig config;

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
        // EVENTS
        // =========================================================================

        [Header("Events - Breath Cycle")]
        [Tooltip("Fired when inhalation begins.")]
        public UnityEvent OnInhaleStart;

        [Tooltip("Fired when exhalation begins.")]
        public UnityEvent OnExhaleStart;

        [Tooltip("Fired when breath is held (Space pressed).")]
        public UnityEvent OnPauseStart;

        [Tooltip("Fired when breath hold ends (Space released).")]
        public UnityEvent OnPauseEnd;

        [Header("Events - Stamina")]
        [Tooltip("Fired when stamina drops below 25%.")]
        public UnityEvent OnStaminaLow;

        [Tooltip("Fired when stamina hits zero.")]
        public UnityEvent OnStaminaDepleted;

        [Tooltip("Fired when stamina is fully restored.")]
        public UnityEvent OnStaminaFull;

        [Header("Events - Recovery")]
        [Tooltip("Fired when recovery begins (catching breath).")]
        public UnityEvent OnRecoveryStart;

        [Tooltip("Fired when recovery ends and control returns.")]
        public UnityEvent OnRecoveryEnd;

        // =========================================================================
        // PRIVATE STATE
        // =========================================================================

        private int  previousDirection;
        private bool previousPauseState;
        private bool previousRecoveryState;
        private bool staminaWasLow;
        private bool staminaWasFull;

        // Pause boost: which phase we paused in (+1 inhale, -1 exhale, 0 not paused)
        // and the live boost multiplier (1 = no boost, decays back to 1 over time).
        private int   pausedInDirection = 0;
        private float currentBoost      = 1f;

        // =========================================================================
        // IBreathInput  (the holobiont/integration contract)
        // =========================================================================

        /// <summary>Normalized depth in [0,1].</summary>
        public float Depth => parameters != null ? parameters.NormalizedDepth : 0.5f;

        /// <summary>Normalized frequency in [0,1].</summary>
        public float Frequency => parameters != null ? parameters.NormalizedFrequency : 0.5f;

        /// <summary>
        /// Signed phase: -1 = full inhale (lungs full), +1 = full exhale (lungs empty).
        /// Derived from oscillator's raw phase∈[0,1] where 0=empty, 1=full:
        /// signedPhase = 1 - 2 * rawPhase.
        /// </summary>
        public float Phase => oscillator != null ? 1f - 2f * oscillator.Phase : 1f;

        /// <summary>True while the player is holding their breath.</summary>
        public bool IsHolding => oscillator != null && oscillator.IsPaused;

        /// <summary>True while holding after an exhale (paused mid-exhale; lungs emptying).</summary>
        public bool IsExhaleHold => IsHolding && pausedInDirection == -1;

        /// <summary>True while holding after an inhale (paused mid-inhale; lungs filling).</summary>
        public bool IsInhaleHold => IsHolding && pausedInDirection == +1;

        /// <summary>Stamina in [0,1].</summary>
        public float Stamina => staminaSystem != null ? staminaSystem.NormalizedStamina : 1f;

        /// <summary>True while in the recovery refractory.</summary>
        public bool InRecovery => recoveryController != null && recoveryController.IsRecovering;

        // =========================================================================
        // ADDITIONAL PUBLIC API  (kept for ability/view consumers)
        // =========================================================================

        /// <summary>Raw lung-fill (phase × depth) — used for breath-bar visualization.</summary>
        public float Displacement => oscillator != null ? oscillator.Displacement : 0f;

        /// <summary>Rate of change of displacement, multiplied by the current pause-boost.</summary>
        public float Velocity => oscillator != null ? oscillator.Velocity * currentBoost : 0f;

        /// <summary>+1 = inhaling, -1 = exhaling.</summary>
        public int Direction => oscillator != null ? oscillator.Direction : 1;

        public bool IsInhaling => Direction > 0;
        public bool IsExhaling => Direction < 0;

        /// <summary>Alias of IsHolding kept for ability scripts.</summary>
        public bool IsPaused => IsHolding;

        /// <summary>Recovery progress: 1 = just started, 0 = about to end.</summary>
        public float RecoveryProgress => recoveryController != null ? recoveryController.NormalizedRecovery : 0f;

        /// <summary>Current pause-boost multiplier (>= 1, decays toward 1).</summary>
        public float Boost => currentBoost;

        // =========================================================================
        // UNITY LIFECYCLE
        // =========================================================================

        private void Start()
        {
            if (oscillator != null)
            {
                previousDirection  = oscillator.Direction;
                previousPauseState = oscillator.IsPaused;
            }
            if (recoveryController != null)
                previousRecoveryState = recoveryController.IsRecovering;

            staminaWasLow  = false;
            staminaWasFull = true;
        }

        private void Update()
        {
            // GameClock-aware tick. Falls back to real delta when no clock is present
            // (so the breath standalone scene keeps working). Per project conventions,
            // simulation systems pause with GameClock; the visualizer stays on real time.
            float deltaTime = GameClock.Instance != null ? GameClock.Instance.DeltaTime : Time.deltaTime;

            // 1. Input
            if (inputHandler != null) inputHandler.Tick();

            // 2. Parameters
            if (parameters != null) parameters.Tick(deltaTime);

            // 3. Stamina
            if (staminaSystem != null) staminaSystem.Tick(deltaTime);

            // 4. Recovery
            if (recoveryController != null) recoveryController.Tick(deltaTime);

            // 5. Oscillator
            if (oscillator != null) oscillator.Tick(deltaTime);

            // 6. Pause-boost decay
            UpdatePauseBoost(deltaTime);

            // 7. State-change events
            CheckAndFireEvents();
        }

        // =========================================================================
        // PAUSE BOOST
        // =========================================================================

        private void UpdatePauseBoost(float deltaTime)
        {
            bool enabled = config == null || config.pauseBoostEnabled;
            if (!enabled)
            {
                currentBoost = 1f;
                return;
            }

            if (currentBoost > 1f && config != null)
            {
                currentBoost -= config.pauseBoostDecayRate * deltaTime;
                currentBoost = Mathf.Max(1f, currentBoost);
            }
        }

        // =========================================================================
        // EVENT DETECTION
        // =========================================================================

        private void CheckAndFireEvents()
        {
            if (oscillator != null)
            {
                int currentDirection = oscillator.Direction;
                if (currentDirection != previousDirection)
                {
                    if (currentDirection > 0) OnInhaleStart?.Invoke();
                    else                      OnExhaleStart?.Invoke();
                    previousDirection = currentDirection;
                }

                bool currentPauseState = oscillator.IsPaused;
                if (currentPauseState != previousPauseState)
                {
                    if (currentPauseState)
                    {
                        // Snapshot which phase we're holding in.
                        pausedInDirection = currentDirection;
                        OnPauseStart?.Invoke();
                    }
                    else
                    {
                        // Boost applies when releasing into the OPPOSITE phase
                        // (hold during inhale -> boost the next exhale, and vice versa).
                        bool boostEnabled = config != null && config.pauseBoostEnabled;
                        if (boostEnabled && pausedInDirection != 0 && currentDirection != pausedInDirection)
                            currentBoost = config.pauseBoostMultiplier;

                        pausedInDirection = 0;
                        OnPauseEnd?.Invoke();
                    }
                    previousPauseState = currentPauseState;
                }
            }

            if (staminaSystem != null)
            {
                float stamina = staminaSystem.NormalizedStamina;

                bool isLow = stamina < 0.25f;
                if (isLow && !staminaWasLow) OnStaminaLow?.Invoke();
                staminaWasLow = isLow;

                bool isFull = stamina >= 1f;
                if (isFull && !staminaWasFull) OnStaminaFull?.Invoke();
                staminaWasFull = isFull;

                if (stamina <= 0f) OnStaminaDepleted?.Invoke();
            }

            if (recoveryController != null)
            {
                bool currentRecoveryState = recoveryController.IsRecovering;
                if (currentRecoveryState != previousRecoveryState)
                {
                    if (currentRecoveryState) OnRecoveryStart?.Invoke();
                    else                      OnRecoveryEnd?.Invoke();
                    previousRecoveryState = currentRecoveryState;
                }
            }
        }
    }
}
