using System;
using UnityEngine;
using UnityEngine.Events;

namespace Holobiont
{
    /*
     * Owns the per-session lifecycle: clean Start, clean End, clean Restart.
     *
     * One in the scene. Everyone reads GameSessionManager.Instance to know
     * whether a session is currently running, when it ended, and why.
     *
     * Reset is wired by serialized references — drag each manager into its
     * slot. On StartSession, the clock is paused, ResetSessionState() runs on
     * every wired manager in dependency order, then the clock is unpaused.
     * EndSession leaves the clock paused (the simulation should stop ticking
     * on the terminal frame) and fires events for views to react.
     *
     * Holobiont death is wired automatically: subscribing to
     * HolobiontManager.OnDeath in OnEnable, the manager calls
     * EndSession(HolobiontDeath) when it fires.
     *
     * There is intentionally no save/load — sessions are short and cheap.
     *
     * Execution order is -110, ahead of GameClock (-100), so any enable-time
     * lifecycle work runs before the first clock tick of the new session.
     */
    [DefaultExecutionOrder(-110)]
    public class GameSessionManager : MonoBehaviour
    {
        public static GameSessionManager Instance { get; private set; }

        // ----- Config -----
        [Header("Behaviour")]
        [Tooltip("If true, StartSession() runs automatically the first time this component enables. Disable for scenes that gate session start behind a menu.")]
        [SerializeField] private bool autoStartOnEnable = true;

        // ----- Wired managers -----
        [Header("Wired managers  (called in this order on Start/Restart)")]
        [Tooltip("Time. Paused around resets; Time is zeroed first so other managers see t=0.")]
        [SerializeField] private GameClock clock;

        [Tooltip("Difficulty. Snapped back to its initial value.")]
        [SerializeField] private DifficultyManager difficulty;

        [Tooltip("Environment base values, extreme states, per-frame deltas.")]
        [SerializeField] private EnvironmentManager environment;

        [Tooltip("Active event + scheduling timer.")]
        [SerializeField] private EnvironmentEventSystem eventSystem;

        [Tooltip("Holobiont state. Bonded creatures destroyed on reset.")]
        [SerializeField] private HolobiontManager holobiont;

        [Tooltip("Creature spawner. Tracked unbound creatures destroyed on reset.")]
        [SerializeField] private CreatureSpawner spawner;

        // ----- Debug -----
        [Header("Debug")]
        [SerializeField, ReadOnly] private SessionState sessionState = SessionState.PreStart;
        [SerializeField, ReadOnly] private string lastEndReason;
        [SerializeField, ReadOnly] private float sessionDuration;

        // ----- Public API -----
        /// <summary>Lifecycle phase: PreStart / Running / Ended.</summary>
        public SessionState State => sessionState;

        /// <summary>The reason for the most recent EndSession call, or null if no session has ended.</summary>
        public SessionEndReason? LastEndReason { get; private set; }

        /// <summary>Real-time seconds since the current (or last) session started. 0 in PreStart.</summary>
        public float SessionDuration => sessionDuration;

        // ----- Outputs -----
        /// <summary>Fired after every wired manager has reset and the clock has resumed. Views may show in-game UI now.</summary>
        public event Action OnSessionStarted;

        /// <summary>Fired before the clock pauses on EndSession. Subscribers see the simulation in its terminal state.</summary>
        public event Action<SessionEndReason> OnSessionEnding;

        /// <summary>Fired after the clock has paused. Game-over views show on this signal.</summary>
        public event Action<SessionEndReason> OnSessionEnded;

        [Header("Events")]
        [Tooltip("Inspector counterpart of OnSessionStarted.")]
        [SerializeField] private UnityEvent sessionStartedEvent;

        [Tooltip("Inspector counterpart of OnSessionEnding (fires before clock pause).")]
        [SerializeField] private UnityEvent<SessionEndReason> sessionEndingEvent;

        [Tooltip("Inspector counterpart of OnSessionEnded (fires after clock pause).")]
        [SerializeField] private UnityEvent<SessionEndReason> sessionEndedEvent;

        // ----- State -----
        private float sessionStartedAtRealTime;
        private bool subscribedToHolobiont;

        // ----- Lifecycle -----
        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Debug.LogWarning($"Multiple {nameof(GameSessionManager)} instances. Destroying duplicate on {name}.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnsubscribeFromHolobiont();
        }

        private void OnEnable()
        {
            // A duplicate's Awake calls Destroy(this) but Unity still drives OnEnable
            // before the destruction takes effect — bail early so the duplicate doesn't
            // subscribe or kick off a stray session.
            if (Instance != this) return;

            SubscribeToHolobiont();
        }

        private void OnDisable()
        {
            UnsubscribeFromHolobiont();
        }

        private void Start()
        {
            // Deferred to Start so every other manager's OnEnable (config validation,
            // initial state) has already run by the time we drive the first reset.
            if (Instance != this) return;
            if (autoStartOnEnable) StartSession();
        }

        private void Update()
        {
            // View-layer cadence (Time.deltaTime) so the duration readout keeps ticking
            // even when the clock is paused — useful for the game-over screen showing
            // "you survived 2:34".
            if (sessionState == SessionState.Running)
                sessionDuration = Time.realtimeSinceStartup - sessionStartedAtRealTime;
        }

        // ----- Public API: lifecycle -----
        /// <summary>Reset all wired managers, unpause the clock, and enter Running. No-op if already Running.</summary>
        [ContextMenu("Start Session")]
        public void StartSession()
        {
            if (sessionState == SessionState.Running) return;

            // Pause around reset so no manager ticks on stale state mid-reset.
            if (clock) clock.Pause();

            // Clock first (so any reset reading clock.Time sees t=0), then the rest
            // top-down by dependency.
            if (clock)       clock.ResetSessionState();
            if (difficulty)  difficulty.ResetSessionState();
            if (environment) environment.ResetSessionState();
            if (eventSystem) eventSystem.ResetSessionState();
            if (holobiont)   holobiont.ResetSessionState();
            if (spawner)     spawner.ResetSessionState();

            sessionState = SessionState.Running;
            LastEndReason = null;
            lastEndReason = string.Empty;
            sessionStartedAtRealTime = Time.realtimeSinceStartup;
            sessionDuration = 0f;

            if (clock) clock.Resume();

            OnSessionStarted?.Invoke();
            sessionStartedEvent?.Invoke();
            GameLog.Post("Session started");
        }

        /// <summary>End the current session. Fires Ending, pauses the clock, fires Ended. No-op if not Running.</summary>
        public void EndSession(SessionEndReason reason)
        {
            if (sessionState != SessionState.Running) return;

            // Ending fires while sim is still in its terminal state (views can snapshot).
            OnSessionEnding?.Invoke(reason);
            sessionEndingEvent?.Invoke(reason);

            // Freeze simulation. Views still update because they use Time.deltaTime
            // per project_conventions.
            if (clock) clock.Pause();

            sessionState = SessionState.Ended;
            LastEndReason = reason;
            lastEndReason = reason.ToString();

            OnSessionEnded?.Invoke(reason);
            sessionEndedEvent?.Invoke(reason);
            GameLog.Post($"Session ended ({reason})");
        }

        /// <summary>End (if running) then Start. Convenience for retry buttons.</summary>
        [ContextMenu("Restart Session")]
        public void RestartSession()
        {
            if (sessionState == SessionState.Running) EndSession(SessionEndReason.ManualRestart);
            StartSession();
        }

        [ContextMenu("End Session (Manual Quit)")]
        public void EndSessionManualQuit() => EndSession(SessionEndReason.ManualQuit);

        // ----- Private -----
        private void SubscribeToHolobiont()
        {
            if (subscribedToHolobiont || !holobiont) return;
            holobiont.OnDeath += HandleHolobiontDeath;
            subscribedToHolobiont = true;
        }

        private void UnsubscribeFromHolobiont()
        {
            if (!subscribedToHolobiont) return;
            if (holobiont) holobiont.OnDeath -= HandleHolobiontDeath;
            subscribedToHolobiont = false;
        }

        private void HandleHolobiontDeath() => EndSession(SessionEndReason.HolobiontDeath);
    }
}
