using System;
using UnityEngine;

namespace Holobiont
{
    /*
     * Listens for stamina depletion and forces a recovery refractory:
     *   - input is suppressed (BreathInputHandler checks IsRecovering)
     *   - any held breath is forcibly released
     *   - StaminaSystem switches to its accelerated recovery regen
     *   - BreathParameters drifts toward the OPPOSITE extreme of where each
     *     axis was at depletion (compensation overshoot)
     * When the timer expires, control returns and parameters resume drifting
     * toward baseline at the normal decay rate.
     *
     * Driven by BreathSimulator — do not call Tick() directly.
     */
    public class RecoveryController : MonoBehaviour
    {
        // =========================================================================
        // CONFIG / REFERENCES
        // =========================================================================

        [Header("Config")]
        [Tooltip("Tuning data; supplies recoveryDuration.")]
        [SerializeField] private BreathConfig config;

        [Header("References")]
        [Tooltip("StaminaSystem component - listens for the OnStaminaDepleted event.")]
        [SerializeField] private StaminaSystem staminaSystem;

        [Tooltip("BreathOscillator component - used to force-release held breath when recovery starts.")]
        [SerializeField] private BreathOscillator oscillator;

        [Tooltip("BreathParameters component - notified to begin/end recovery compensation drift.")]
        [SerializeField] private BreathParameters parameters;

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

        public event Action OnRecoveryStarted;
        public event Action OnRecoveryEnded;

        // =========================================================================
        // PUBLIC PROPERTIES
        // =========================================================================

        public bool  IsRecovering     => isRecovering;
        public float RecoveryTimer    => recoveryTimer;
        public float RecoveryDuration => config != null ? config.recoveryDuration : 0f;

        /// <summary>1 = just started, 0 = about to end. Useful for UI.</summary>
        public float NormalizedRecovery =>
            RecoveryDuration > 0f ? recoveryTimer / RecoveryDuration : 0f;

        // =========================================================================
        // UNITY LIFECYCLE
        // =========================================================================

        private void OnEnable()
        {
            if (config == null)
                Debug.LogError($"{nameof(RecoveryController)} on {name} has no BreathConfig assigned.", this);

            if (staminaSystem != null)
                staminaSystem.OnStaminaDepleted += HandleStaminaDepleted;
        }

        private void OnDisable()
        {
            if (staminaSystem != null)
                staminaSystem.OnStaminaDepleted -= HandleStaminaDepleted;
        }

        // =========================================================================
        // TICK
        // =========================================================================

        public void Tick(float deltaTime)
        {
            if (!isRecovering) return;

            recoveryTimer -= deltaTime;

            if (recoveryTimer <= 0f)
                EndRecovery();
        }

        // =========================================================================
        // PRIVATE
        // =========================================================================

        private void HandleStaminaDepleted()
        {
            if (isRecovering) return;
            StartRecovery();
        }

        private void StartRecovery()
        {
            isRecovering = true;
            recoveryTimer = RecoveryDuration;

            if (oscillator != null)
                oscillator.SetPaused(false);

            if (staminaSystem != null)
                staminaSystem.SetRecoveryState(true);

            // Force compensation overshoot: each axis drifts toward the opposite
            // extreme of where it sat at depletion.
            if (parameters != null)
                parameters.BeginRecoveryCompensation();

            OnRecoveryStarted?.Invoke();
        }

        private void EndRecovery()
        {
            isRecovering = false;
            recoveryTimer = 0f;

            if (staminaSystem != null)
                staminaSystem.SetRecoveryState(false);

            if (parameters != null)
                parameters.EndRecoveryCompensation();

            OnRecoveryEnded?.Invoke();
        }
    }
}
