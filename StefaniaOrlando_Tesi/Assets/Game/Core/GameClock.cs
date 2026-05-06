using UnityEngine;

namespace Holobiont
{
    /*
     * Central, pausable, scalable game-time clock.
     * Single source of time for all gameplay simulation systems
     * (environment drift, events, holobiont metabolism, etc.).
     *
     * Drop one in the scene. Other systems read GameClock.Instance.DeltaTime
     * (or .Time for accumulated seconds).
     *
     * DeltaTime is a computed property, so callers in Update get a fresh value
     * regardless of script execution order. Time is accumulated in this script's
     * own Update — fine for curve evaluation and similar low-precision needs.
     */

    [DefaultExecutionOrder(-100)]
    public class GameClock : MonoBehaviour
    {
        public static GameClock Instance { get; private set; }

        // ----- Config -----
        [Header("Runtime")]
        [Tooltip("Multiplier applied to real delta time. 0 freezes the clock without using the paused flag.")]
        [SerializeField, Min(0f)] private float timeScale = 1f;

        [Tooltip("When true, DeltaTime returns 0 and Time stops accumulating.")]
        [SerializeField] private bool paused;

        // ----- Public API -----
        /// <summary>Accumulated game-time seconds. Stops when paused, scales with TimeScale.</summary>
        public float Time { get; private set; }

        /// <summary>Scaled delta for this frame. Zero while paused.</summary>
        public float DeltaTime => paused ? 0f : UnityEngine.Time.deltaTime * timeScale;

        /// <summary>Pause flag. When true, DeltaTime is zero and Time does not advance.</summary>
        public bool  IsPaused  { get => paused;    set => paused = value; }

        /// <summary>Multiplier on real time. Clamped to non-negative on set.</summary>
        public float TimeScale { get => timeScale; set => timeScale = Mathf.Max(0f, value); }

        /// <summary>Stop accumulating game time.</summary>
        public void Pause()  => paused = true;

        /// <summary>Resume accumulating game time.</summary>
        public void Resume() => paused = false;

        // ----- Lifecycle -----
        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Debug.LogWarning($"Multiple {nameof(GameClock)} instances. Destroying duplicate on {name}.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            Time += DeltaTime;
        }
    }
}
