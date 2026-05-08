using System;
using UnityEngine;
using UnityEngine.Events;

namespace Holobiont
{
    /*
     * Tracks a 0..staminaMax breath stamina pool. Drains while either parameter
     * deviates from baseline (drain stacks across axes) and while pause is held.
     * Regenerates while parameters are near baseline and breath is not held.
     * Fires OnStaminaDepleted when it hits zero.
     *
     * Tuning lives on BreathConfig. Driven by BreathSimulator — do not call
     * Tick() directly.
     */
    public class StaminaSystem : MonoBehaviour
    {
        // ----- Config -----
        [Header("Config")]
        [Tooltip("Tuning data for stamina drain, regen, and recovery regen.")]
        [SerializeField] private BreathConfig config;

        // ----- References -----
        [Header("References")]
        [Tooltip("BreathParameters component - used to check how far breathing deviates from baseline.")]
        [SerializeField] private BreathParameters parameters;

        [Tooltip("BreathOscillator component - used to check if breath is being held.")]
        [SerializeField] private BreathOscillator oscillator;

        // ----- Debug -----
        [Header("Debug")]
        [Tooltip("Current stamina level. Watch this during play to see drain/regen in action.")]
        [SerializeField, ReadOnly] private float staminaCurrent;

        // ----- Internal state -----
        private bool isInRecovery;

        // ----- Outputs -----
        /// <summary>Fired when stamina hits zero. Subscribed by RecoveryController.</summary>
        public event Action OnStaminaDepleted;

        [Header("Events")]
        [Tooltip("Inspector pair to OnStaminaDepleted. Fires when stamina hits zero.")]
        [SerializeField] private UnityEvent staminaDepletedEvent;

        // ----- Public API -----
        public float StaminaCurrent => staminaCurrent;
        public float StaminaMax     => config ? config.staminaMax : 1f;

        /// <summary>Stamina as 0-1 percentage. Useful for UI bars and IBreathInput.</summary>
        public float NormalizedStamina => StaminaMax > 0f ? staminaCurrent / StaminaMax : 0f;

        // ----- Lifecycle -----
        private void OnEnable()
        {
            if (!config)
            {
                Debug.LogError($"{nameof(StaminaSystem)} on {name} has no BreathConfig assigned.", this);
                enabled = false;
                return;
            }
            staminaCurrent = config.staminaMax;
        }

        // ----- Tick -----
        public void Tick(float deltaTime)
        {
            if (!config) return;

            if (isInRecovery)
            {
                staminaCurrent = Mathf.Min(staminaCurrent + config.regenRateDuringRecovery * deltaTime, config.staminaMax);
                return;
            }

            float frequencyDeviation = parameters ? parameters.FrequencyDeviation : 0f;
            float depthDeviation     = parameters ? parameters.DepthDeviation     : 0f;
            bool isPaused            = oscillator && oscillator.IsPaused;

            bool isNearBaseline = frequencyDeviation < config.baselineTolerance
                               && depthDeviation     < config.baselineTolerance;

            if (isPaused)
            {
                staminaCurrent -= config.pauseDrainRate * deltaTime;
            }
            else if (isNearBaseline)
            {
                staminaCurrent += config.regenRate * deltaTime;
            }
            else
            {
                float drainRate = config.baseDrainRate * (frequencyDeviation + depthDeviation);
                staminaCurrent -= drainRate * deltaTime;
            }

            staminaCurrent = Mathf.Clamp(staminaCurrent, 0f, config.staminaMax);

            if (staminaCurrent <= 0f)
            {
                OnStaminaDepleted?.Invoke();
                staminaDepletedEvent?.Invoke();
            }
        }

        // ----- Inputs -----
        /// <summary>Called by RecoveryController when recovery state changes.</summary>
        public void SetRecoveryState(bool recovering) => isInRecovery = recovering;
    }
}
