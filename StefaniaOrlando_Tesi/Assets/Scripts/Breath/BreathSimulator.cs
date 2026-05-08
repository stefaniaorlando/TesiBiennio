using System;
using UnityEngine;
using UnityEngine.Events;

namespace Holobiont
{
    /*
     * Central orchestrator for the breath simulation. Ticks each subsystem in
     * the right order, manages pause-boost, fires the breath-cycle events
     * (paired C# Action + UnityEvent per project convention), and exposes
     * IBreathInput — the data contract every consumer (holobiont, abilities,
     * view) reads from.
     *
     * Time source: GameClock.Instance.DeltaTime when present (so breath pauses
     * with the game), falling back to Time.deltaTime in scenes without a clock
     * (e.g. the breath standalone scene).
     */
    public class BreathSimulator : MonoBehaviour, IBreathInput
    {
        // ----- Config -----
        [Header("Config")]
        [Tooltip("Tuning data; supplies pause-boost parameters.")]
        [SerializeField] private BreathConfig config;

        // ----- References -----
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

        // ----- Outputs (Breath Cycle) -----
        public event Action OnInhaleStart;
        public event Action OnExhaleStart;
        public event Action OnPauseStart;
        public event Action OnPauseEnd;

        [Header("Events - Breath Cycle")]
        [Tooltip("Inspector pair to OnInhaleStart.")]
        [SerializeField] private UnityEvent inhaleStartEvent;

        [Tooltip("Inspector pair to OnExhaleStart.")]
        [SerializeField] private UnityEvent exhaleStartEvent;

        [Tooltip("Inspector pair to OnPauseStart.")]
        [SerializeField] private UnityEvent pauseStartEvent;

        [Tooltip("Inspector pair to OnPauseEnd.")]
        [SerializeField] private UnityEvent pauseEndEvent;

        // ----- Outputs (Stamina) -----
        public event Action OnStaminaLow;
        public event Action OnStaminaDepleted;
        public event Action OnStaminaFull;

        [Header("Events - Stamina")]
        [Tooltip("Inspector pair to OnStaminaLow. Fires when stamina drops below 25%.")]
        [SerializeField] private UnityEvent staminaLowEvent;

        [Tooltip("Inspector pair to OnStaminaDepleted. Fires when stamina hits zero.")]
        [SerializeField] private UnityEvent staminaDepletedEvent;

        [Tooltip("Inspector pair to OnStaminaFull. Fires when stamina is fully restored.")]
        [SerializeField] private UnityEvent staminaFullEvent;

        // ----- Outputs (Recovery) -----
        public event Action OnRecoveryStart;
        public event Action OnRecoveryEnd;

        [Header("Events - Recovery")]
        [Tooltip("Inspector pair to OnRecoveryStart. Fires when recovery begins.")]
        [SerializeField] private UnityEvent recoveryStartEvent;

        [Tooltip("Inspector pair to OnRecoveryEnd. Fires when recovery ends.")]
        [SerializeField] private UnityEvent recoveryEndEvent;

        // ----- Internal state -----
        private int  previousDirection;
        private bool previousPauseState;
        private bool previousRecoveryState;
        private bool staminaWasLow;
        private bool staminaWasFull;

        // Pause boost: which phase we paused in (+1 inhale, -1 exhale, 0 not paused)
        // and the live boost multiplier (1 = no boost, decays back to 1 over time).
        private int   pausedInDirection = 0;
        private float currentBoost      = 1f;

        // ----- IBreathInput (holobiont integration contract) -----
        /// <summary>Normalized depth in [0,1].</summary>
        public float Depth => parameters ? parameters.NormalizedDepth : 0.5f;

        /// <summary>Normalized frequency in [0,1].</summary>
        public float Frequency => parameters ? parameters.NormalizedFrequency : 0.5f;

        /// <summary>
        /// Signed phase: -1 = full inhale (lungs full), +1 = full exhale (lungs empty).
        /// Derived from oscillator's raw phase∈[0,1] where 0=empty, 1=full:
        /// signedPhase = 1 - 2 * rawPhase.
        /// </summary>
        public float Phase => oscillator ? 1f - 2f * oscillator.Phase : 1f;

        /// <summary>True while the player is holding their breath.</summary>
        public bool IsHolding => oscillator && oscillator.IsPaused;

        /// <summary>True while holding after an exhale (paused mid-exhale; lungs emptying).</summary>
        public bool IsExhaleHold => IsHolding && pausedInDirection == -1;

        /// <summary>True while holding after an inhale (paused mid-inhale; lungs filling).</summary>
        public bool IsInhaleHold => IsHolding && pausedInDirection == +1;

        /// <summary>Stamina in [0,1].</summary>
        public float Stamina => staminaSystem ? staminaSystem.NormalizedStamina : 1f;

        /// <summary>True while in the recovery refractory.</summary>
        public bool InRecovery => recoveryController && recoveryController.IsRecovering;

        // ----- Public API (kept for ability/view consumers) -----
        /// <summary>Raw lung-fill (phase × depth) — used for breath-bar visualization.</summary>
        public float Displacement => oscillator ? oscillator.Displacement : 0f;

        /// <summary>Rate of change of displacement, multiplied by the current pause-boost.</summary>
        public float Velocity => oscillator ? oscillator.Velocity * currentBoost : 0f;

        /// <summary>+1 = inhaling, -1 = exhaling.</summary>
        public int Direction => oscillator ? oscillator.Direction : 1;

        public bool IsInhaling => Direction > 0;
        public bool IsExhaling => Direction < 0;

        /// <summary>Alias of IsHolding kept for ability scripts.</summary>
        public bool IsPaused => IsHolding;

        /// <summary>Recovery progress: 1 = just started, 0 = about to end.</summary>
        public float RecoveryProgress => recoveryController ? recoveryController.NormalizedRecovery : 0f;

        /// <summary>Current pause-boost multiplier (>= 1, decays toward 1).</summary>
        public float Boost => currentBoost;

        // ----- Lifecycle -----
        private void Start()
        {
            if (oscillator)
            {
                previousDirection  = oscillator.Direction;
                previousPauseState = oscillator.IsPaused;
            }
            if (recoveryController)
                previousRecoveryState = recoveryController.IsRecovering;

            staminaWasLow  = false;
            staminaWasFull = true;
        }

        private void Update()
        {
            // GameClock-aware tick. Falls back to real delta when no clock is present
            // (so the breath standalone scene keeps working). Per project conventions,
            // simulation systems pause with GameClock; the visualizer stays on real time.
            float deltaTime = GameClock.Instance ? GameClock.Instance.DeltaTime : Time.deltaTime;

            // 1. Input
            if (inputHandler) inputHandler.Tick();

            // 2. Parameters
            if (parameters) parameters.Tick(deltaTime);

            // 3. Stamina
            if (staminaSystem) staminaSystem.Tick(deltaTime);

            // 4. Recovery
            if (recoveryController) recoveryController.Tick(deltaTime);

            // 5. Oscillator
            if (oscillator) oscillator.Tick(deltaTime);

            // 6. Pause-boost decay
            UpdatePauseBoost(deltaTime);

            // 7. State-change events
            CheckAndFireEvents();
        }

        // ----- Private -----
        private void UpdatePauseBoost(float deltaTime)
        {
            bool boostEnabled = !config || config.pauseBoostEnabled;
            if (!boostEnabled)
            {
                currentBoost = 1f;
                return;
            }

            if (currentBoost > 1f && config)
            {
                currentBoost -= config.pauseBoostDecayRate * deltaTime;
                currentBoost = Mathf.Max(1f, currentBoost);
            }
        }

        private void CheckAndFireEvents()
        {
            if (oscillator)
            {
                int currentDirection = oscillator.Direction;
                if (currentDirection != previousDirection)
                {
                    if (currentDirection > 0)
                    {
                        OnInhaleStart?.Invoke();
                        inhaleStartEvent?.Invoke();
                    }
                    else
                    {
                        OnExhaleStart?.Invoke();
                        exhaleStartEvent?.Invoke();
                    }
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
                        pauseStartEvent?.Invoke();
                    }
                    else
                    {
                        // Boost applies when releasing into the OPPOSITE phase
                        // (hold during inhale -> boost the next exhale, and vice versa).
                        bool boostEnabled = config && config.pauseBoostEnabled;
                        if (boostEnabled && pausedInDirection != 0 && currentDirection != pausedInDirection)
                            currentBoost = config.pauseBoostMultiplier;

                        pausedInDirection = 0;
                        OnPauseEnd?.Invoke();
                        pauseEndEvent?.Invoke();
                    }
                    previousPauseState = currentPauseState;
                }
            }

            if (staminaSystem)
            {
                float stamina = staminaSystem.NormalizedStamina;

                bool isLow = stamina < 0.25f;
                if (isLow && !staminaWasLow)
                {
                    OnStaminaLow?.Invoke();
                    staminaLowEvent?.Invoke();
                }
                staminaWasLow = isLow;

                bool isFull = stamina >= 1f;
                if (isFull && !staminaWasFull)
                {
                    OnStaminaFull?.Invoke();
                    staminaFullEvent?.Invoke();
                }
                staminaWasFull = isFull;

                if (stamina <= 0f)
                {
                    OnStaminaDepleted?.Invoke();
                    staminaDepletedEvent?.Invoke();
                }
            }

            if (recoveryController)
            {
                bool currentRecoveryState = recoveryController.IsRecovering;
                if (currentRecoveryState != previousRecoveryState)
                {
                    if (currentRecoveryState)
                    {
                        OnRecoveryStart?.Invoke();
                        recoveryStartEvent?.Invoke();
                    }
                    else
                    {
                        OnRecoveryEnd?.Invoke();
                        recoveryEndEvent?.Invoke();
                    }
                    previousRecoveryState = currentRecoveryState;
                }
            }
        }
    }
}
