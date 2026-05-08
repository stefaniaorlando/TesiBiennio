using System;
using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Holobiont
{
    /*
     * Title / start screen. Owns the PreStart phase and the post-"Quit to title"
     * return phase. The pause menu (MainMenuView) only opens while a session is
     * Running, so the two never overlap.
     *
     * Visibility rules:
     *   - On enable: shown if session.State == PreStart, hidden otherwise.
     *   - OnSessionEnded(ReturnedToTitle) → show.
     *   - OnSessionStarted → hide.
     *
     * Start sequence:
     *   1. Set Cinemachine ortho size to gameOrthoSize via a coroutine lerp
     *      (driven by unscaledDeltaTime so it ignores the paused clock).
     *   2. Call session.StartSession(); the session manager handles spawning/reset.
     *
     * The menu pauses the GameClock while shown — sim shouldn't tick before the
     * first Start. Resume is handed off to StartSession (which pauses + resumes
     * around its own reset).
     *
     * GameSessionManager.autoStartOnEnable should be FALSE when this view is in
     * the scene; otherwise the session starts before this menu can show. A
     * runtime warning is logged on enable if that misconfiguration is detected.
     */
    [DisallowMultipleComponent]
    public class StartMenuView : MonoBehaviour
    {
        // ----- Source -----
        [Header("Source")]
        [Tooltip("Session manager driven by the Start button. Defaults to GameSessionManager.Instance at OnEnable when left empty.")]
        [SerializeField] private GameSessionManager session;

        [Tooltip("Clock paused while the menu is shown. Defaults to GameClock.Instance at OnEnable when left empty.")]
        [SerializeField] private GameClock clock;

        // ----- View -----
        [Header("View")]
        [Tooltip("Root GameObject toggled active by this view.")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("Optional. Title label — set once in inspector, never overwritten.")]
        [SerializeField] private TMP_Text titleLabel;

        [Tooltip("Start button. Triggers the zoom-in and starts the session.")]
        [SerializeField] private Button startButton;

        [Tooltip("Optional. Quit button. Application.Quit in builds, stops play mode in the editor.")]
        [SerializeField] private Button quitButton;

        // ----- Camera zoom -----
        [Header("Camera zoom (optional)")]
        [Tooltip("Cinemachine camera whose Lens.OrthographicSize is lerped on Start. Leave empty to skip the zoom.")]
        [SerializeField] private CinemachineCamera cinemachineCamera;

        [Tooltip("Ortho size while the start menu is shown — pulled out so nothing of the game is in frame.")]
        [Min(0.01f)] [SerializeField] private float menuOrthoSize = 12f;

        [Tooltip("Ortho size during gameplay. Captured from the camera the first time the menu shows if left at zero.")]
        [Min(0f)] [SerializeField] private float gameOrthoSize = 6f;

        [Tooltip("Seconds the Start zoom lerp takes. 0 = snap.")]
        [Min(0f)] [SerializeField] private float zoomDuration = 1.0f;

        // ----- Events -----
        [Header("Events")]
        [Tooltip("Fired the moment the Start button is pressed, before the zoom starts. Wire VFX/audio cues here.")]
        [SerializeField] private UnityEngine.Events.UnityEvent startPressed;

        // ----- State -----
        private bool subscribedToSession;
        private bool pausedByMenu;
        private bool gameOrthoCaptured;
        private Coroutine startRoutine;

        // ----- Public API -----
        public event Action OnStartRequested;

        // ----- Lifecycle -----
        private void OnEnable()
        {
            if (!session) session = GameSessionManager.Instance;
            if (!clock)   clock   = GameClock.Instance;

            if (startButton) startButton.onClick.AddListener(HandleStartClicked);
            if (quitButton)  quitButton.onClick.AddListener(HandleQuitClicked);

            SubscribeToSession();

            // Detect the autoStartOnEnable mis-config: if the session is already
            // Running by the time this view enables, the start menu is moot.
            if (session && session.State == SessionState.Running)
            {
                Debug.LogWarning(
                    $"{nameof(StartMenuView)}: session is already Running on enable. Set GameSessionManager.autoStartOnEnable=false so the start menu can gate the first session.",
                    this);
                Hide();
                return;
            }

            // Show only if we're at the title moment (no session has started yet,
            // or the player just returned via Quit-to-title).
            bool shouldShow = !session
                              || session.State == SessionState.PreStart
                              || (session.State == SessionState.Ended && session.LastEndReason == SessionEndReason.ReturnedToTitle);

            if (shouldShow) Show();
            else Hide();
        }

        private void OnDisable()
        {
            if (startButton) startButton.onClick.RemoveListener(HandleStartClicked);
            if (quitButton)  quitButton.onClick.RemoveListener(HandleQuitClicked);

            UnsubscribeFromSession();

            if (startRoutine != null) { StopCoroutine(startRoutine); startRoutine = null; }
            if (pausedByMenu && clock) clock.Resume();
            pausedByMenu = false;
        }

        // ----- Public API: show/hide -----
        [ContextMenu("Show")]
        public void Show()
        {
            CaptureGameOrthoIfNeeded();

            if (clock && !clock.IsPaused)
            {
                clock.Pause();
                pausedByMenu = true;
            }

            // Snap the camera out so the world reads as "title screen state".
            ApplyOrtho(menuOrthoSize);

            if (panelRoot) panelRoot.SetActive(true);
        }

        [ContextMenu("Hide")]
        public void Hide()
        {
            if (panelRoot) panelRoot.SetActive(false);

            if (pausedByMenu && clock) clock.Resume();
            pausedByMenu = false;
        }

        // ----- Handlers -----
        private void HandleStartClicked()
        {
            if (startRoutine != null) return; // already starting; ignore double-click

            startPressed?.Invoke();
            OnStartRequested?.Invoke();

            // Hide the panel immediately so the world becomes visible during the
            // zoom; the camera lerp + StartSession run in the coroutine.
            if (panelRoot) panelRoot.SetActive(false);

            startRoutine = StartCoroutine(StartSequence());
        }

        private void HandleQuitClicked()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HandleSessionStarted() => Hide();

        private void HandleSessionEnded(SessionEndReason reason)
        {
            if (reason == SessionEndReason.ReturnedToTitle) Show();
        }

        // ----- Private -----
        private IEnumerator StartSequence()
        {
            // Lerp ortho size in real time so it works while the clock is paused.
            if (cinemachineCamera && zoomDuration > 0f)
            {
                float t = 0f;
                float from = GetOrtho();
                float to   = gameOrthoSize;
                while (t < zoomDuration)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / zoomDuration);
                    ApplyOrtho(Mathf.Lerp(from, to, EaseInOut(k)));
                    yield return null;
                }
            }
            ApplyOrtho(gameOrthoSize);

            // Hand off to the session. RestartSession would also work but we
            // want a clean Start (no Ending event) when leaving the title.
            pausedByMenu = false; // session.StartSession owns the clock now.
            if (session) session.StartSession();
            else Debug.LogWarning($"{nameof(StartMenuView)}: no GameSessionManager wired; cannot start a session.", this);

            startRoutine = null;
        }

        private void SubscribeToSession()
        {
            if (subscribedToSession || !session) return;
            session.OnSessionStarted += HandleSessionStarted;
            session.OnSessionEnded   += HandleSessionEnded;
            subscribedToSession = true;
        }

        private void UnsubscribeFromSession()
        {
            if (!subscribedToSession) return;
            if (session)
            {
                session.OnSessionStarted -= HandleSessionStarted;
                session.OnSessionEnded   -= HandleSessionEnded;
            }
            subscribedToSession = false;
        }

        private void CaptureGameOrthoIfNeeded()
        {
            if (gameOrthoCaptured || gameOrthoSize > 0f) { gameOrthoCaptured = true; return; }
            if (cinemachineCamera) gameOrthoSize = GetOrtho();
            gameOrthoCaptured = true;
        }

        private float GetOrtho()
        {
            if (!cinemachineCamera) return gameOrthoSize;
            return cinemachineCamera.Lens.OrthographicSize;
        }

        private void ApplyOrtho(float size)
        {
            if (!cinemachineCamera) return;
            var lens = cinemachineCamera.Lens;
            lens.OrthographicSize = size;
            cinemachineCamera.Lens = lens;
        }

        private static float EaseInOut(float k)
        {
            // Smoothstep — pleasant default for a title-to-game zoom.
            return k * k * (3f - 2f * k);
        }
    }
}
