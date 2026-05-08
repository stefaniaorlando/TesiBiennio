using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Holobiont
{
    /*
     * Pause / main menu. Toggled with Esc (configurable). Pauses the GameClock
     * while open so the simulation freezes; views still tick because the view
     * layer uses Time.deltaTime per project_conventions.
     *
     * Buttons:
     *   Continue       — hide and resume the clock (only if we paused it).
     *   Restart        — GameSessionManager.RestartSession() and hide.
     *   Quit to title  — GameSessionManager.EndSession(ReturnedToTitle); the
     *                    StartMenuView listens for that reason and shows itself.
     *   Quit           — Application.Quit() in builds; stop play mode in editor.
     *
     * Optional widgets:
     *   - Survival timer label  (TMP_Text)        — mirrors session.SessionDuration
     *   - HUD visibility toggle (Toggle)          — flips a list of HUD roots active
     *   - Time-scale slider     (Slider)          — drives GameClock.TimeScale
     *
     * Esc gating: ignores the toggle press while UIInputGate.Capturing is true
     * (a BreathKeyRebindRow is listening for a key). Also only opens when
     * session.State == Running — start screen owns PreStart, game-over owns Ended.
     *
     * The "only if we paused it" rule on Continue matters when the menu is
     * opened on top of the Game Over screen (clock already paused by EndSession):
     * Continue must not silently un-pause an Ended session.
     */
    [DisallowMultipleComponent]
    public class MainMenuView : MonoBehaviour
    {
        // ----- Source -----
        [Header("Source")]
        [Tooltip("Session manager whose Restart/Quit hooks the buttons drive. Defaults to GameSessionManager.Instance at OnEnable when left empty.")]
        [SerializeField] private GameSessionManager session;

        [Tooltip("Clock paused while the menu is open. Defaults to GameClock.Instance at OnEnable when left empty.")]
        [SerializeField] private GameClock clock;

        // ----- Input -----
        [Header("Input")]
        [Tooltip("Key that toggles the menu open/closed.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

        // ----- View -----
        [Header("View")]
        [Tooltip("Root GameObject toggled active by this view.")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("Optional. Survival timer label updated each frame while the menu is open.")]
        [SerializeField] private TMP_Text survivalLabel;

        [Tooltip("Format string for survivalLabel. {0} = MM:SS.")]
        [SerializeField] private string survivalFormat = "Survived {0}";

        // ----- Buttons -----
        [Header("Buttons")]
        [Tooltip("Continue button. Closes the menu and resumes the clock if we paused it.")]
        [SerializeField] private Button continueButton;

        [Tooltip("Restart button. Calls GameSessionManager.RestartSession.")]
        [SerializeField] private Button restartButton;

        [Tooltip("Optional. Quit-to-title button. Ends the session with ReturnedToTitle so the start menu shows.")]
        [SerializeField] private Button quitToTitleButton;

        [Tooltip("Quit button. Application.Quit in builds, stops play mode in the editor.")]
        [SerializeField] private Button quitButton;

        // ----- HUD toggle -----
        [Header("HUD toggle")]
        [Tooltip("Optional. Toggle that shows/hides the GameObjects in hudRoots.")]
        [SerializeField] private Toggle hudToggle;

        [Tooltip("GameObjects flipped active by hudToggle. Typically your two HUD canvases (env + holobiont).")]
        [SerializeField] private GameObject[] hudRoots;

        // ----- Time-scale slider -----
        [Header("Time-scale slider")]
        [Tooltip("Optional. Slider that drives GameClock.TimeScale. Set its min/max in the inspector.")]
        [SerializeField] private Slider timeScaleSlider;

        [Tooltip("Optional. Label that mirrors the slider's current value (e.g. '1.0x').")]
        [SerializeField] private TMP_Text timeScaleLabel;

        // ----- State -----
        private bool isOpen;
        private bool pausedByMenu;

        // ----- Public API -----
        public bool IsOpen => isOpen;

        // ----- Lifecycle -----
        private void OnEnable()
        {
            if (!session) session = GameSessionManager.Instance;
            if (!clock)   clock   = GameClock.Instance;

            if (continueButton)    continueButton.onClick.AddListener(HandleContinueClicked);
            if (restartButton)     restartButton.onClick.AddListener(HandleRestartClicked);
            if (quitToTitleButton) quitToTitleButton.onClick.AddListener(HandleQuitToTitleClicked);
            if (quitButton)        quitButton.onClick.AddListener(HandleQuitClicked);

            if (hudToggle)
            {
                hudToggle.onValueChanged.AddListener(HandleHudToggleChanged);
                ApplyHudVisible(hudToggle.isOn);
            }

            if (timeScaleSlider)
            {
                if (clock) timeScaleSlider.SetValueWithoutNotify(clock.TimeScale);
                timeScaleSlider.onValueChanged.AddListener(HandleTimeScaleChanged);
                UpdateTimeScaleLabel(timeScaleSlider.value);
            }

            ApplyOpenState(false);
        }

        private void OnDisable()
        {
            if (continueButton)    continueButton.onClick.RemoveListener(HandleContinueClicked);
            if (restartButton)     restartButton.onClick.RemoveListener(HandleRestartClicked);
            if (quitToTitleButton) quitToTitleButton.onClick.RemoveListener(HandleQuitToTitleClicked);
            if (quitButton)        quitButton.onClick.RemoveListener(HandleQuitClicked);

            if (hudToggle)        hudToggle.onValueChanged.RemoveListener(HandleHudToggleChanged);
            if (timeScaleSlider)  timeScaleSlider.onValueChanged.RemoveListener(HandleTimeScaleChanged);

            // Don't strand the clock paused if we get disabled while open.
            if (pausedByMenu && clock) clock.Resume();
            pausedByMenu = false;
        }

        private void Update()
        {
            // A rebind row is reading raw input; don't fight it for Esc.
            if (UIInputGate.Capturing) return;

            if (Input.GetKeyDown(toggleKey)) Toggle();

            if (isOpen && survivalLabel && session)
                survivalLabel.text = string.Format(survivalFormat, FormatMMSS(session.SessionDuration));
        }

        // ----- Public API: open/close -----
        [ContextMenu("Toggle")]
        public void Toggle() { if (isOpen) Close(); else Open(); }

        [ContextMenu("Open")]
        public void Open()
        {
            if (isOpen) return;

            // Start screen owns PreStart, game-over owns Ended; pause menu only
            // makes sense mid-run.
            if (session && session.State != SessionState.Running) return;

            // Defensive: if no session is wired we still let the panel open so a
            // dev can use the buttons in isolation, but skip clock pausing.
            if (clock && !clock.IsPaused)
            {
                clock.Pause();
                pausedByMenu = true;
            }

            ApplyOpenState(true);
        }

        [ContextMenu("Close")]
        public void Close()
        {
            if (!isOpen) return;

            if (pausedByMenu && clock) clock.Resume();
            pausedByMenu = false;

            ApplyOpenState(false);
        }

        // ----- Handlers -----
        private void HandleContinueClicked() => Close();

        private void HandleRestartClicked()
        {
            // RestartSession owns the pause/resume around its reset, so drop our
            // pause claim before calling it — otherwise we'd Resume() a clock
            // that's now owned by the new session.
            pausedByMenu = false;
            ApplyOpenState(false);

            if (session) session.RestartSession();
            else Debug.LogWarning($"{nameof(MainMenuView)}: no GameSessionManager wired; Restart ignored.", this);
        }

        private void HandleQuitToTitleClicked()
        {
            // Hand the screen to the start menu by ending the session with the
            // dedicated reason. GameOverView suppresses ReturnedToTitle, so the
            // start menu lands cleanly.
            pausedByMenu = false;
            ApplyOpenState(false);

            if (session) session.EndSession(SessionEndReason.ReturnedToTitle);
            else Debug.LogWarning($"{nameof(MainMenuView)}: no GameSessionManager wired; Quit-to-title ignored.", this);
        }

        private void HandleQuitClicked()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HandleHudToggleChanged(bool on) => ApplyHudVisible(on);

        private void HandleTimeScaleChanged(float v)
        {
            if (clock) clock.TimeScale = v;
            UpdateTimeScaleLabel(v);
        }

        // ----- Private -----
        private void ApplyOpenState(bool open)
        {
            isOpen = open;
            if (panelRoot) panelRoot.SetActive(open);
        }

        private void ApplyHudVisible(bool visible)
        {
            if (hudRoots == null) return;
            for (int i = 0; i < hudRoots.Length; i++)
                if (hudRoots[i]) hudRoots[i].SetActive(visible);
        }

        private void UpdateTimeScaleLabel(float v)
        {
            if (timeScaleLabel) timeScaleLabel.text = $"{v:0.00}x";
        }

        private static string FormatMMSS(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f) seconds = 0f;
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
