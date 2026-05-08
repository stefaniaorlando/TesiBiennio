using System;
using UnityEngine;
using UnityEngine.Events;

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
        // ----- Config -----
        [Header("Config")]
        [Tooltip("Tuning data; supplies recoveryDuration.")]
        [SerializeField] private BreathConfig config;

        // ----- References -----
        [Header("References")]
        [Tooltip("StaminaSystem component - listens for the OnStaminaDepleted event.")]
        [SerializeField] private StaminaSystem staminaSystem;

        [Tooltip("BreathOscillator component - used to force-release held breath when recovery starts.")]
        [SerializeField] private BreathOscillator oscillator;

        [Tooltip("BreathParameters component - notified to begin/end recovery compensation drift.")]
        [SerializeField] private BreathParameters parameters;

        // ----- Debug -----
        [Header("Debug")]
        [Tooltip("Is the player currently in recovery? (Input disabled)")]
        [SerializeField, ReadOnly] private bool isRecovering = false;

        [Tooltip("Time remaining until recovery ends.")]
        [SerializeField, ReadOnly] private float recoveryTimer = 0f;

        // ----- Outputs -----
        public event Action OnRecoveryStarted;
        public event Action OnRecoveryEnded;

        [Header("Events")]
        [Tooltip("Inspector pair to OnRecoveryStarted. Fires when recovery begins.")]
        [SerializeField] private UnityEvent recoveryStartedEvent;

        [Tooltip("Inspector pair to OnRecoveryEnded. Fires when recovery ends.")]
        [SerializeField] private UnityEvent recoveryEndedEvent;

        // ----- Public API -----
        public bool  IsRecovering     => isRecovering;
        public float RecoveryTimer    => recoveryTimer;
        public float RecoveryDuration => config ? config.recoveryDuration : 0f;

        /// <summary>1 = just started, 0 = about to end. Useful for UI.</summary>
        public float NormalizedRecovery =>
            RecoveryDuration > 0f ? recoveryTimer / RecoveryDuration : 0f;

        // ----- Lifecycle -----
        private void OnEnable()
        {
            if (!config)
            {
                Debug.LogError($"{nameof(RecoveryController)} on {name} has no BreathConfig assigned.", this);
                enabled = false;
                return;
            }

            if (staminaSystem)
                staminaSystem.OnStaminaDepleted += HandleStaminaDepleted;
        }

        private void OnDisable()
        {
            if (staminaSystem)
                staminaSystem.OnStaminaDepleted -= HandleStaminaDepleted;
        }

        // ----- Tick -----
        public void Tick(float deltaTime)
        {
            if (!isRecovering) return;

            recoveryTimer -= deltaTime;

            if (recoveryTimer <= 0f)
                EndRecovery();
        }

        // ----- Private -----
        private void HandleStaminaDepleted()
        {
            if (isRecovering) return;
            StartRecovery();
        }

        private void StartRecovery()
        {
            isRecovering = true;
            recoveryTimer = RecoveryDuration;

            if (oscillator)
                oscillator.SetPaused(false);

            if (staminaSystem)
                staminaSystem.SetRecoveryState(true);

            // Force compensation overshoot: each axis drifts toward the opposite
            // extreme of where it sat at depletion.
            if (parameters)
                parameters.BeginRecoveryCompensation();

            OnRecoveryStarted?.Invoke();
            recoveryStartedEvent?.Invoke();
        }

        private void EndRecovery()
        {
            isRecovering = false;
            recoveryTimer = 0f;

            if (staminaSystem)
                staminaSystem.SetRecoveryState(false);

            if (parameters)
                parameters.EndRecoveryCompensation();

            OnRecoveryEnded?.Invoke();
            recoveryEndedEvent?.Invoke();
        }
    }
}
