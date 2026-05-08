using System;
using UnityEngine;

namespace Holobiont
{
    /*
     * Central difficulty knob.
     * One in the scene, like GameClock. Other systems read multipliers from
     * DifficultyManager.Instance and treat its absence as 1× / no gating.
     *
     * Master value (Difficulty) advances from config.initialDifficulty toward 1
     * over config.timeToMax seconds of GameClock time, shaped by progressEasing.
     * Plateaus at 1 once reached; OnReachedMax fires exactly once on first arrival.
     *
     * Difficulty is snapped to exactly 1 once GameClock.Time crosses timeToMax,
     * so per-event difficulty bands like [1.0, 1.0] (max-difficulty-only events)
     * are robust against float drift in the lerp/easing path.
     *
     * Execution order is between GameClock (-100) and EnvironmentManager (-90),
     * so any system reading Difficulty in its Update sees this frame's value.
     */

    [DefaultExecutionOrder(-95)]
    public class DifficultyManager : MonoBehaviour
    {
        public static DifficultyManager Instance { get; private set; }

        // ----- Config -----
        [Header("Config")]
        [Tooltip("Difficulty profile asset. Defines progression and response curves.")]
        [SerializeField] private DifficultyConfig config;

        // ----- Debug (cached this frame, exposed for inspector visibility) -----
        [Header("Debug")]
        [Tooltip("Current 0..1 difficulty value.")]
        [SerializeField, ReadOnly] private float difficulty;

        [Tooltip("Event intensity response curve evaluated at the current difficulty.")]
        [SerializeField, ReadOnly] private float eventIntensityMul = 1f;

        [Tooltip("Event frequency response curve evaluated at the current difficulty.")]
        [SerializeField, ReadOnly] private float eventFrequencyMul = 1f;

        [Tooltip("Drift amplitude response curve evaluated at the current difficulty.")]
        [SerializeField, ReadOnly] private float driftAmplitudeMul = 1f;

        // ----- Public API -----
        /// <summary>The active config asset. Assignable at runtime.</summary>
        public DifficultyConfig Config { get => config; set => config = value; }

        /// <summary>Current difficulty in [0,1]. 0 at session start (or initialDifficulty floor), exactly 1 at and after timeToMax.</summary>
        public float Difficulty => difficulty;

        /// <summary>Multiplier on a newly-spawned event's intensity. 1 if no config assigned.</summary>
        public float EventIntensityMul => eventIntensityMul;

        /// <summary>Multiplier on event spawn frequency. 1 if no config assigned. Higher = shorter wait.</summary>
        public float EventFrequencyMul => eventFrequencyMul;

        /// <summary>Multiplier on the per-frame drift curve output. 1 if no config assigned.</summary>
        public float DriftAmplitudeMul => driftAmplitudeMul;

        /// <summary>Fires once when Difficulty first reaches 1. Useful for "max difficulty" UX cues.</summary>
        public event Action OnReachedMax;

        // ----- State -----
        private bool reachedMax;

        // ----- Lifecycle -----
        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Debug.LogWarning($"Multiple {nameof(DifficultyManager)} instances. Destroying duplicate on {name}.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            if (!config)
            {
                Debug.LogError($"{nameof(DifficultyManager)} has no {nameof(DifficultyConfig)} assigned.", this);
                enabled = false;
                return;
            }
            ResetSessionState();
        }

        /// <summary>Re-arm difficulty progression: snap to initial value, reset OnReachedMax latch, recompute multipliers.</summary>
        [ContextMenu("Reset Session State")]
        public void ResetSessionState()
        {
            if (!config) return;
            difficulty = config.initialDifficulty;
            EvaluateMultipliers();
            reachedMax = difficulty >= 1f;
        }

        private void Update()
        {
            if (!config) return;
            var clock = GameClock.Instance;
            if (!clock) return;

            // Defensive: [Min(1)] guards the inspector but a script-set 0 would NaN/Infinity through Lerp.
            float denom  = Mathf.Max(0.0001f, config.timeToMax);
            float linear = Mathf.Clamp01(clock.Time / denom);

            // Snap to exact 1 once the linear ramp reaches the end so per-event bands like [1,1] match.
            if (linear >= 1f)
            {
                difficulty = 1f;
            }
            else
            {
                float eased = Mathf.Clamp01(config.progressEasing.Evaluate(linear));
                difficulty  = Mathf.Lerp(config.initialDifficulty, 1f, eased);
            }

            EvaluateMultipliers();

            if (!reachedMax && difficulty >= 1f)
            {
                reachedMax = true;
                GameLog.Post("Max difficulty reached");
                OnReachedMax?.Invoke();
            }
        }

        // ----- Private -----
        // Single source of truth: properties read these cached values, written once per frame here.
        private void EvaluateMultipliers()
        {
            eventIntensityMul = config.eventIntensityMul.Evaluate(difficulty);
            eventFrequencyMul = config.eventFrequencyMul.Evaluate(difficulty);
            driftAmplitudeMul = config.driftAmplitudeMul.Evaluate(difficulty);
        }
    }
}
