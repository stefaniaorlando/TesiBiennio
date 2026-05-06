using UnityEngine;

namespace Holobiont
{
    /*
     * Schedules and ticks crisis events that perturb the environment on top of
     * baseline drift. Picks events from the schedule's pool (filtered by each
     * event's min/maxDifficulty and weighted by its weight), runs one at a time
     * for its configured lifetime, and applies its current envelope delta
     * through EnvironmentManager.ApplyEventDelta every frame.
     *
     * Execution order is set after EnvironmentManager's reset (-90) and
     * EnvironmentDrift (-50), and before default, so events stack on top of the
     * drift base already written this frame.
     *
     * Composition is explicit on EnvironmentManager: drift writes base via Set*(),
     * events accumulate per-frame deltas via ApplyEventDelta(). The manager zeroes
     * the event-delta accumulators at the start of every frame (order -90), so
     * events never compound across frames even if a variable's drift is disabled.
     *
     * Difficulty scaling (intensity, spawn frequency, pool gating) is sourced
     * from DifficultyManager.Instance. If absent, multipliers default to 1× and
     * gating is open, so the system runs unchanged from its pre-difficulty form.
     *
     * HOW TO SET UP:
     * 1. Add this component on the same GameObject as EnvironmentManager.
     * 2. Create one or more EnvironmentEventConfig assets (Create → Game → Environment Event).
     * 3. Create an EnvironmentEventScheduleConfig asset (Create → Game → Environment Event Schedule)
     *    and add the events to its Event Pool.
     * 4. Assign the schedule asset to this component.
     * 5. (Optional) Add a DifficultyManager to drive intensity/frequency/gating.
     */

    [RequireComponent(typeof(EnvironmentManager))]
    [DefaultExecutionOrder(-25)]
    public class EnvironmentEventSystem : MonoBehaviour
    {
        // ----- Config -----
        [Header("Config")]
        [Tooltip("Schedule asset that drives pacing and the event pool.")]
        [SerializeField] private EnvironmentEventScheduleConfig schedule;

        // ----- Debug (visible runtime state, not editable) -----
        [Header("Debug")]
        [Tooltip("True while an event is currently running. Only one event runs at a time.")]
        [SerializeField, ReadOnly] private bool isEventActive;

        [Tooltip("Display name of the currently running event, or empty when idle.")]
        [SerializeField, ReadOnly] private string activeEventName;

        [Tooltip("Seconds until the next scheduled event spawn check.")]
        [SerializeField, ReadOnly] private float secondsUntilNext;

        [Tooltip("Effective minimum spawn interval after applying DifficultyManager.EventFrequencyMul.")]
        [SerializeField, ReadOnly] private float effectiveMinInterval;

        [Tooltip("Effective maximum spawn interval after applying DifficultyManager.EventFrequencyMul.")]
        [SerializeField, ReadOnly] private float effectiveMaxInterval;

        // ----- Public API -----
        /// <summary>The active schedule. Assignable at runtime.</summary>
        public EnvironmentEventScheduleConfig Schedule
        {
            get => schedule;
            // Re-arm timing and prepick against the new schedule's pool/pacing,
            // otherwise the old timer (and stale prepick) would persist across the swap.
            set { schedule = value; RescheduleNextEvent(); }
        }

        /// <summary>True while an event is currently running.</summary>
        public bool IsEventActive => active != null;

        /// <summary>The currently running event, or null if idle.</summary>
        public RuntimeEvent ActiveEvent => active;

        /// <summary>The pre-picked event scheduled to spawn next, or null when the pool has nothing eligible.</summary>
        public EnvironmentEventConfig NextEvent => nextEvent;

        /// <summary>Seconds remaining until the next spawn attempt. Clamped to 0.</summary>
        public float SecondsUntilNext
        {
            get
            {
                var clock = GameClock.Instance;
                float now = clock ? clock.Time : 0f;
                return Mathf.Max(0f, nextEventAtTime - now);
            }
        }

        // ----- State -----
        private RuntimeEvent active;
        private EnvironmentManager env;
        private float nextEventAtTime;
        private EnvironmentEventConfig nextEvent;

        // ----- Lifecycle -----
        private void Awake() => env = GetComponent<EnvironmentManager>();

        private void OnEnable()
        {
            if (!schedule)
            {
                Debug.LogError($"{nameof(EnvironmentEventSystem)} has no {nameof(EnvironmentEventScheduleConfig)} assigned.", this);
                enabled = false;
                return;
            }
            RescheduleNextEvent();
        }

        private void Update()
        {
            if (!schedule) return;
            var clock = GameClock.Instance;
            if (!clock) return;

            float now = clock.Time;

            if (now >= nextEventAtTime)
            {
                TrySpawnEvent(now);
                RescheduleNextEvent(now);
            }

            if (active != null)
            {
                float duration = active.Config.TotalDuration;
                if (duration <= 0f)
                {
                    active = null;
                }
                else
                {
                    float u = (now - active.StartTime) / duration;
                    if (u >= 1f)
                    {
                        GameLog.Post($"{active.Config.eventName} ended");
                        active = null;
                    }
                    else
                    {
                        // Clamp >= 0: smoothed AnimationCurves can undershoot below zero at boundary
                        // keys, which would invert effect signs (a +20 temperature event briefly cooling).
                        float envelope = Mathf.Max(0f, active.Config.envelopeCurve.Evaluate(u)) * active.IntensityMultiplier;
                        var effects = active.Config.effects;
                        if (effects != null)
                        {
                            for (int j = 0; j < effects.Length; j++)
                            {
                                var effect = effects[j];
                                if (effect is null) continue;
                                env.ApplyEventDelta(effect.affected, envelope * effect.intensityDelta);
                            }
                        }
                    }
                }
            }

            isEventActive = active != null;
            activeEventName = active != null ? active.Config.eventName : string.Empty;
            secondsUntilNext = Mathf.Max(0f, nextEventAtTime - now);
            UpdateEffectiveInterval();
        }

        // ----- Public mutators -----
        /// <summary>Forces an immediate spawn of the given event at the current intensity multiplier. Useful for debug.</summary>
        public void TriggerNow(EnvironmentEventConfig cfg)
        {
            if (!cfg) return;
            var clock = GameClock.Instance;
            float now = clock ? clock.Time : 0f;
            active = new RuntimeEvent(cfg, now, CurrentIntensityMul());
            GameLog.Post($"{cfg.eventName} started");
            // Refresh the prepick so HUD's "Next" label doesn't keep showing a stale event
            // queued before this manual override.
            RescheduleNextEvent(now);
        }

        // ----- Private -----
        private void TrySpawnEvent(float now)
        {
            if (active != null) return;

            // Use the prepicked event when still eligible at the current difficulty;
            // otherwise re-pick fresh against the current difficulty band.
            var cfg = nextEvent;
            if (cfg != null && !IsEligible(cfg, CurrentDifficulty())) cfg = null;
            if (cfg == null) cfg = PickEvent();
            if (!cfg || cfg.TotalDuration <= 0f) { nextEvent = null; return; }

            active = new RuntimeEvent(cfg, now, CurrentIntensityMul());
            nextEvent = null;
            GameLog.Post($"{cfg.eventName} started");
        }

        private EnvironmentEventConfig PickEvent()
        {
            var pool = schedule.eventPool;
            if (pool is null || pool.Length == 0) return null;

            float d = CurrentDifficulty();

            float total = 0f;
            for (int i = 0; i < pool.Length; i++)
            {
                if (!IsEligible(pool[i], d)) continue;
                total += pool[i].weight;
            }
            if (total <= 0f) return null;

            float roll = Random.value * total;
            for (int i = 0; i < pool.Length; i++)
            {
                if (!IsEligible(pool[i], d)) continue;
                roll -= pool[i].weight;
                if (roll <= 0f) return pool[i];
            }
            return null;
        }

        private static bool IsEligible(EnvironmentEventConfig cfg, float difficulty)
        {
            if (!cfg || cfg.weight <= 0f) return false;
            return difficulty >= cfg.minDifficulty && difficulty <= cfg.maxDifficulty;
        }

        private static float CurrentDifficulty()
            => DifficultyManager.Instance ? DifficultyManager.Instance.Difficulty : 0f;

        private static float CurrentIntensityMul()
            => DifficultyManager.Instance ? DifficultyManager.Instance.EventIntensityMul : 1f;

        private static float CurrentFrequencyMul()
            => DifficultyManager.Instance ? DifficultyManager.Instance.EventFrequencyMul : 1f;

        private void RescheduleNextEvent()
            => RescheduleNextEvent(GameClock.Instance ? GameClock.Instance.Time : 0f);

        private void RescheduleNextEvent(float now)
        {
            if (!schedule) { nextEventAtTime = float.PositiveInfinity; nextEvent = null; return; }
            float min = Mathf.Max(0f, schedule.minInterval);
            float max = Mathf.Max(min, schedule.maxInterval);
            float interval = Random.Range(min, max) / Mathf.Max(0.01f, CurrentFrequencyMul());
            nextEventAtTime = now + interval;
            nextEvent = PickEvent();
        }

        private void UpdateEffectiveInterval()
        {
            float mul = Mathf.Max(0.01f, CurrentFrequencyMul());
            // Show the raw min/max divided by the multiplier so a designer authoring max < min sees their mistake here.
            effectiveMinInterval = schedule.minInterval / mul;
            effectiveMaxInterval = schedule.maxInterval / mul;
        }

        // ----- Nested -----
        /// <summary>Snapshot of the running event: which config, when it started, and its difficulty multiplier.</summary>
        public class RuntimeEvent
        {
            public EnvironmentEventConfig Config { get; }
            public float StartTime { get; }
            public float IntensityMultiplier { get; }

            public RuntimeEvent(EnvironmentEventConfig cfg, float startTime, float intensityMultiplier)
            {
                Config = cfg;
                StartTime = startTime;
                IntensityMultiplier = intensityMultiplier;
            }
        }
    }
}
