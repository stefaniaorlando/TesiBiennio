using System;
using UnityEngine;

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
        // =========================================================================
        // CONFIG / REFERENCES
        // =========================================================================

        [Header("Config")]
        [Tooltip("Tuning data for stamina drain, regen, and recovery regen.")]
        [SerializeField] private BreathConfig config;

        [Header("References")]
        [Tooltip("BreathParameters component - used to check how far breathing deviates from baseline.")]
        [SerializeField] private BreathParameters parameters;

        [Tooltip("BreathOscillator component - used to check if breath is being held.")]
        [SerializeField] private BreathOscillator oscillator;

        // =========================================================================
        // RUNTIME STATE
        // =========================================================================

        [Header("Runtime State (Read Only)")]
        [Tooltip("Current stamina level. Watch this during play to see drain/regen in action.")]
        [SerializeField] private float staminaCurrent;

        private bool isInRecovery;

        // =========================================================================
        // EVENTS
        // =========================================================================

        public event Action OnStaminaDepleted;

        // =========================================================================
        // PUBLIC PROPERTIES
        // =========================================================================

        public float StaminaCurrent => staminaCurrent;
        public float StaminaMax     => config != null ? config.staminaMax : 1f;

        /// <summary>Stamina as 0-1 percentage. Useful for UI bars and IBreathInput.</summary>
        public float NormalizedStamina => StaminaMax > 0f ? staminaCurrent / StaminaMax : 0f;

        // =========================================================================
        // UNITY LIFECYCLE
        // =========================================================================

        private void OnEnable()
        {
            if (config == null)
            {
                Debug.LogError($"{nameof(StaminaSystem)} on {name} has no BreathConfig assigned.", this);
                return;
            }
            staminaCurrent = config.staminaMax;
        }

        // =========================================================================
        // TICK
        // =========================================================================

        public void Tick(float deltaTime)
        {
            if (config == null) return;

            if (isInRecovery)
            {
                staminaCurrent = Mathf.Min(staminaCurrent + config.regenRateDuringRecovery * deltaTime, config.staminaMax);
                return;
            }

            float frequencyDeviation = parameters != null ? parameters.FrequencyDeviation : 0f;
            float depthDeviation     = parameters != null ? parameters.DepthDeviation     : 0f;
            bool isPaused            = oscillator != null && oscillator.IsPaused;

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
                OnStaminaDepleted?.Invoke();
        }

        // =========================================================================
        // PUBLIC METHODS
        // =========================================================================

        /// <summary>Called by RecoveryController when recovery state changes.</summary>
        public void SetRecoveryState(bool recovering) => isInRecovery = recovering;
    }
}
