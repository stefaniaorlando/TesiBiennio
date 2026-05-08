using UnityEngine;

namespace Holobiont
{
    /*
     * Holds the live frequency and depth values, drifts them toward extremes
     * while input is held, and back toward baseline (or, during recovery, toward
     * the OPPOSITE extreme as a compensation overshoot) when input is released.
     *
     * Tuning data lives on a BreathConfig SO. Compensation is toggled by the
     * RecoveryController via BeginRecoveryCompensation / EndRecoveryCompensation.
     *
     * Driven by BreathSimulator — do not call Tick() directly.
     */
    public class BreathParameters : MonoBehaviour
    {
        // ----- Config -----
        [Header("Config")]
        [Tooltip("Tuning data for breath frequency and depth.")]
        [SerializeField] private BreathConfig config;

        // ----- Debug -----
        [Header("Debug")]
        [Tooltip("Current frequency value. Drifts based on input and recovery state.")]
        [SerializeField, ReadOnly] private float frequencyCurrent;

        [Tooltip("Current depth value. Drifts based on input and recovery state.")]
        [SerializeField, ReadOnly] private float depthCurrent;

        [Tooltip("True while drifting toward the opposite extreme as a recovery compensation overshoot.")]
        [SerializeField, ReadOnly] private bool inCompensation;

        // ----- Internal state -----
        // Captured at recovery start: which extreme to drift toward, per axis.
        private float frequencyCompensationTarget;
        private float depthCompensationTarget;

        // Input flags, set by BreathInputHandler.
        private bool pushingFrequencyUp;
        private bool pushingFrequencyDown;
        private bool pushingDepthUp;
        private bool pushingDepthDown;

        // ----- Public API -----
        public float FrequencyCurrent  => frequencyCurrent;
        public float DepthCurrent      => depthCurrent;
        public float FrequencyBaseline => config ? config.frequencyBaseline : 0f;
        public float DepthBaseline     => config ? config.depthBaseline     : 0f;

        /// <summary>Frequency as a 0-1 value within the config's min-max range.</summary>
        public float NormalizedFrequency =>
            config ? Mathf.InverseLerp(config.frequencyMin, config.frequencyMax, frequencyCurrent) : 0.5f;

        /// <summary>Depth as a 0-1 value within the config's min-max range.</summary>
        public float NormalizedDepth =>
            config ? Mathf.InverseLerp(config.depthMin, config.depthMax, depthCurrent) : 0.5f;

        /// <summary>How far frequency is from baseline (0 = at baseline, 1 = at extreme).</summary>
        public float FrequencyDeviation
        {
            get
            {
                if (!config) return 0f;
                float range = config.frequencyMax - config.frequencyMin;
                return range > 0f ? Mathf.Abs(frequencyCurrent - config.frequencyBaseline) / range : 0f;
            }
        }

        /// <summary>How far depth is from baseline (0 = at baseline, 1 = at extreme).</summary>
        public float DepthDeviation
        {
            get
            {
                if (!config) return 0f;
                float range = config.depthMax - config.depthMin;
                return range > 0f ? Mathf.Abs(depthCurrent - config.depthBaseline) / range : 0f;
            }
        }

        /// <summary>True while compensation drift is active (during recovery).</summary>
        public bool InCompensation => inCompensation;

        // ----- Lifecycle -----
        private void OnEnable()
        {
            if (!config)
            {
                Debug.LogError($"{nameof(BreathParameters)} on {name} has no BreathConfig assigned.", this);
                enabled = false;
                return;
            }
            frequencyCurrent = config.frequencyBaseline;
            depthCurrent     = config.depthBaseline;
        }

        // ----- Tick -----
        public void Tick(float deltaTime)
        {
            if (!config) return;
            UpdateFrequency(deltaTime);
            UpdateDepth(deltaTime);
        }

        // ----- Inputs -----
        public void PushFrequencyUp(bool active)   => pushingFrequencyUp   = active;
        public void PushFrequencyDown(bool active) => pushingFrequencyDown = active;
        public void PushDepthUp(bool active)       => pushingDepthUp       = active;
        public void PushDepthDown(bool active)     => pushingDepthDown     = active;

        /// <summary>
        /// Snapshot the opposite extreme of each axis (relative to baseline) and start
        /// drifting toward it. Called by RecoveryController when stamina depletes.
        /// </summary>
        [ContextMenu("Begin Recovery Compensation")]
        public void BeginRecoveryCompensation()
        {
            inCompensation = true;
            if (!config) return;

            frequencyCompensationTarget = frequencyCurrent > config.frequencyBaseline
                ? config.frequencyMin
                : (frequencyCurrent < config.frequencyBaseline ? config.frequencyMax : config.frequencyBaseline);

            depthCompensationTarget = depthCurrent > config.depthBaseline
                ? config.depthMin
                : (depthCurrent < config.depthBaseline ? config.depthMax : config.depthBaseline);
        }

        /// <summary>
        /// Stop compensating; subsequent drift returns toward baseline at the decay rate.
        /// Called by RecoveryController when recovery ends.
        /// </summary>
        [ContextMenu("End Recovery Compensation")]
        public void EndRecoveryCompensation()
        {
            inCompensation = false;
        }

        // ----- Private -----
        private void UpdateFrequency(float deltaTime)
        {
            if (pushingFrequencyUp)
            {
                frequencyCurrent += config.frequencyApproachRate * deltaTime;
            }
            else if (pushingFrequencyDown)
            {
                frequencyCurrent -= config.frequencyApproachRate * deltaTime;
            }
            else
            {
                // No input: normally drift to baseline; during recovery compensation,
                // drift to the opposite extreme at the (faster) approach rate.
                float target = inCompensation ? frequencyCompensationTarget : config.frequencyBaseline;
                float rate   = inCompensation ? config.frequencyApproachRate : config.frequencyDecayRate;
                frequencyCurrent = Mathf.MoveTowards(frequencyCurrent, target, rate * deltaTime);
            }

            frequencyCurrent = Mathf.Clamp(frequencyCurrent, config.frequencyMin, config.frequencyMax);
        }

        private void UpdateDepth(float deltaTime)
        {
            if (pushingDepthUp)
            {
                depthCurrent += config.depthApproachRate * deltaTime;
            }
            else if (pushingDepthDown)
            {
                depthCurrent -= config.depthApproachRate * deltaTime;
            }
            else
            {
                float target = inCompensation ? depthCompensationTarget : config.depthBaseline;
                float rate   = inCompensation ? config.depthApproachRate : config.depthDecayRate;
                depthCurrent = Mathf.MoveTowards(depthCurrent, target, rate * deltaTime);
            }

            depthCurrent = Mathf.Clamp(depthCurrent, config.depthMin, config.depthMax);
        }
    }
}
