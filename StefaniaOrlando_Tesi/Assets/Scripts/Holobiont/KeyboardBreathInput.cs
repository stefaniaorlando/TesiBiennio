using UnityEngine;

namespace Holobiont
{
    /// <summary>Logical breath input slot — used by rebind UI to address one of the three keys without leaking field names.</summary>
    public enum BreathKey { Exhale, Inhale, Hold }

    /*
     * Two-key keyboard mock for IBreathInput. Strictly a test driver — when the
     * real input model (sensor, gamepad, mouse, whatever) lands, replace this
     * component with a different IBreathInput implementation; nothing else changes.
     *
     * Mapping:
     *   Hold Exhale Key (default E) — depth ramps up   (+1/rampSeconds per second)
     *   Hold Inhale Key (default I) — depth ramps down (-1/rampSeconds per second)
     *   Hold Hold Key   (default Space) — depth held; IsHolding becomes true
     *
     * Phase is derived from the sign of d(depth)/dt with a small epsilon. While
     * holding, phase reports 0 (no movement) and the IsExhaleHold / IsInhaleHold
     * flags resolve by depth threshold (>= holdDepthThreshold ⇒ exhale-hold).
     *
     * Frequency, Stamina, and InRecovery are placeholders — Phase 2 doesn't need
     * them, and a real breath sensor will provide meaningful values later.
     */
    [DisallowMultipleComponent]
    public class KeyboardBreathInput : MonoBehaviour, IBreathInput
    {
        // ----- Config -----
        [Header("Keys")]
        [Tooltip("Hold to ramp depth up (exhale).")]
        [SerializeField] private KeyCode exhaleKey = KeyCode.E;

        [Tooltip("Hold to ramp depth down (inhale).")]
        [SerializeField] private KeyCode inhaleKey = KeyCode.I;

        [Tooltip("Hold to freeze depth and report IsHolding. Combined with current depth to resolve IsExhaleHold / IsInhaleHold.")]
        [SerializeField] private KeyCode holdKey = KeyCode.Space;

        [Header("Tuning")]
        [Tooltip("Seconds for depth to traverse from 0 to 1 with the relevant key fully held.")]
        [Min(0.05f)] public float rampSeconds = 1.2f;

        [Tooltip("Depth >= this value while holding ⇒ IsExhaleHold. Below ⇒ IsInhaleHold.")]
        [Range(0f, 1f)] public float holdDepthThreshold = 0.5f;

        [Tooltip("Constant returned for Frequency. Real implementations will measure cycle period; the holobiont treats this as a placeholder.")]
        [Range(0f, 1f)] public float frequencyPlaceholder = 0.5f;

        // ----- Debug -----
        [Header("Debug")]
        [SerializeField, ReadOnly] private float depth;
        [SerializeField, ReadOnly] private float phase;
        [SerializeField, ReadOnly] private bool isHolding;
        [SerializeField, ReadOnly] private bool isExhaleHold;
        [SerializeField, ReadOnly] private bool isInhaleHold;

        // ----- Rebind API -----
        /// <summary>Read the current binding for one of the three breath slots. Used by rebind UI.</summary>
        public KeyCode GetKey(BreathKey slot)
        {
            switch (slot)
            {
                case BreathKey.Exhale: return exhaleKey;
                case BreathKey.Inhale: return inhaleKey;
                case BreathKey.Hold:   return holdKey;
                default:               return KeyCode.None;
            }
        }

        /// <summary>Assign a new binding for one of the three breath slots. Used by rebind UI.</summary>
        public void SetKey(BreathKey slot, KeyCode code)
        {
            switch (slot)
            {
                case BreathKey.Exhale: exhaleKey = code; break;
                case BreathKey.Inhale: inhaleKey = code; break;
                case BreathKey.Hold:   holdKey   = code; break;
            }
        }

        // ----- IBreathInput -----
        public float Depth        => depth;
        public float Phase        => phase;
        public float Frequency    => frequencyPlaceholder;
        public bool  IsHolding    => isHolding;
        public bool  IsExhaleHold => isExhaleHold;
        public bool  IsInhaleHold => isInhaleHold;
        public float Stamina      => 1f;
        public bool  InRecovery   => false;

        // ----- Lifecycle -----
        private void Update()
        {
            bool ex   = Input.GetKey(exhaleKey);
            bool inh  = Input.GetKey(inhaleKey);
            isHolding = Input.GetKey(holdKey);

            float prev = depth;

            if (!isHolding)
            {
                float step = Time.deltaTime / Mathf.Max(0.05f, rampSeconds);
                if (ex && !inh)      depth = Mathf.Min(1f, depth + step);
                else if (inh && !ex) depth = Mathf.Max(0f, depth - step);
                // both or neither held while not in hold mode → depth steady
            }

            // Phase = sign of d(depth)/dt. Hold or steady → 0.
            float delta = depth - prev;
            const float eps = 1e-4f;
            if      (delta >  eps) phase = +1f;
            else if (delta < -eps) phase = -1f;
            else                   phase =  0f;

            isExhaleHold = isHolding && depth >= holdDepthThreshold;
            isInhaleHold = isHolding && depth <  holdDepthThreshold;
        }
    }
}
