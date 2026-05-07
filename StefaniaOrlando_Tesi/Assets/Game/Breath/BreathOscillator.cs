using UnityEngine;

namespace Holobiont
{
    /*
     * Generates a continuous breathing wave: phase oscillates between 0 (lungs
     * empty) and 1 (lungs full); direction flips at the bounds. Frequency and
     * depth come from BreathParameters (which reads them from BreathConfig).
     *
     * Note: this script's `phase` is the raw lung-fill in [0,1]. The signed
     * phase consumed by holobiont/IBreathInput (-1 inhale .. +1 exhale) is
     * computed in BreathSimulator from this phase + direction.
     *
     * Driven by BreathSimulator — do not call Tick() directly.
     */
    public class BreathOscillator : MonoBehaviour
    {
        // =========================================================================
        // REFERENCES
        // =========================================================================

        [Header("References")]
        [Tooltip("BreathParameters component that supplies current frequency and depth.")]
        [SerializeField] private BreathParameters parameters;

        // =========================================================================
        // RUNTIME STATE
        // =========================================================================

        [Header("Runtime State (Read Only)")]
        [Tooltip("Current position in the breath cycle. 0 = empty, 1 = full.")]
        [SerializeField] private float phase = 0f;

        [Tooltip("Current direction: +1 = inhaling, -1 = exhaling.")]
        [SerializeField] private int direction = 1;

        [Tooltip("Is the breath currently being held? (Space key)")]
        [SerializeField] private bool isPaused = false;

        private float displacement;
        private float velocity;
        private float previousDisplacement;

        // =========================================================================
        // PUBLIC PROPERTIES
        // =========================================================================

        /// <summary>Raw lung-fill position in the cycle (0 = empty, 1 = full).</summary>
        public float Phase => phase;

        /// <summary>+1 = inhaling, -1 = exhaling.</summary>
        public int Direction => direction;

        /// <summary>True while breath is being held.</summary>
        public bool IsPaused => isPaused;

        /// <summary>Current lung fill level (phase × depth).</summary>
        public float Displacement => displacement;

        /// <summary>Rate of change of displacement per second.</summary>
        public float Velocity => velocity;

        /// <summary>Current frequency from BreathParameters (cycles/second).</summary>
        public float Frequency => parameters != null ? parameters.FrequencyCurrent : 0f;

        /// <summary>Current depth from BreathParameters (raw amplitude scalar).</summary>
        public float Depth => parameters != null ? parameters.DepthCurrent : 0f;

        // =========================================================================
        // UNITY LIFECYCLE
        // =========================================================================

        private void Start()
        {
            phase = 0f;
            direction = 1;
            displacement = 0f;
            previousDisplacement = 0f;
        }

        // =========================================================================
        // TICK
        // =========================================================================

        public void Tick(float deltaTime)
        {
            float currentFrequency = Frequency;
            float currentDepth = Depth;

            if (isPaused)
            {
                velocity = 0f;
                return;
            }

            previousDisplacement = displacement;

            // A full cycle goes 0→1→0 (two half-cycles), so multiply by 2.
            float phaseDelta = currentFrequency * 2f * deltaTime;
            phase += direction * phaseDelta;

            if (phase >= 1f)
            {
                phase = 1f;
                direction = -1;
            }
            else if (phase <= 0f)
            {
                phase = 0f;
                direction = 1;
            }

            displacement = phase * currentDepth;

            if (deltaTime > 0f)
                velocity = (displacement - previousDisplacement) / deltaTime;
        }

        // =========================================================================
        // INPUT
        // =========================================================================

        public void SetPaused(bool paused) => isPaused = paused;

        public void TogglePause() => isPaused = !isPaused;
    }
}
