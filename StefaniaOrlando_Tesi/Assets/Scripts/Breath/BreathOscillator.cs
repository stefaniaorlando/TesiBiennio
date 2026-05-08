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
        // ----- References -----
        [Header("References")]
        [Tooltip("BreathParameters component that supplies current frequency and depth.")]
        [SerializeField] private BreathParameters parameters;

        // ----- Debug -----
        [Header("Debug")]
        [Tooltip("Current position in the breath cycle. 0 = empty, 1 = full.")]
        [SerializeField, ReadOnly] private float phase = 0f;

        [Tooltip("Current direction: +1 = inhaling, -1 = exhaling.")]
        [SerializeField, ReadOnly] private int direction = 1;

        [Tooltip("Is the breath currently being held? (Space key)")]
        [SerializeField, ReadOnly] private bool isPaused = false;

        // ----- Internal state -----
        private float displacement;
        private float velocity;
        private float previousDisplacement;

        // ----- Public API -----
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
        public float Frequency => parameters ? parameters.FrequencyCurrent : 0f;

        /// <summary>Current depth from BreathParameters (raw amplitude scalar).</summary>
        public float Depth => parameters ? parameters.DepthCurrent : 0f;

        // ----- Lifecycle -----
        private void Start()
        {
            phase = 0f;
            direction = 1;
            displacement = 0f;
            previousDisplacement = 0f;
        }

        // ----- Tick -----
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

        // ----- Inputs -----
        public void SetPaused(bool paused) => isPaused = paused;

        [ContextMenu("Toggle Pause")]
        public void TogglePause() => isPaused = !isPaused;
    }
}
