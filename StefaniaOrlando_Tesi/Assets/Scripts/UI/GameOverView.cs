using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Holobiont
{
    /*
     * Minimal game-over panel. Listens to GameSessionManager.OnSessionEnded,
     * shows itself with a reason + duration, hides on the next OnSessionStarted.
     *
     * Restart button calls GameSessionManager.RestartSession on click. Manual
     * restarts (SessionEndReason.ManualRestart) intentionally don't trigger the
     * panel — the next StartSession is already queued and would hide it again.
     */
    [DisallowMultipleComponent]
    public class GameOverView : MonoBehaviour
    {
        // ----- Source -----
        [Header("Source")]
        [Tooltip("Session manager whose lifecycle drives the panel. Defaults to GameSessionManager.Instance at OnEnable when left empty.")]
        [SerializeField] private GameSessionManager session;

        // ----- View -----
        [Header("View")]
        [Tooltip("Root GameObject toggled active by this view.")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("Optional. Headline label, e.g. 'Holobiont Collapsed'.")]
        [SerializeField] private TMP_Text headlineLabel;

        [Tooltip("Optional. Subtext line — survival duration, etc.")]
        [SerializeField] private TMP_Text subtextLabel;

        [Tooltip("Restart button. onClick is wired automatically in OnEnable.")]
        [SerializeField] private Button restartButton;

        // ----- State -----
        private bool subscribed;

        // ----- Lifecycle -----
        private void OnEnable()
        {
            if (!session) session = GameSessionManager.Instance;
            if (!session)
            {
                Debug.LogError($"{nameof(GameOverView)} has no {nameof(GameSessionManager)} assigned and none is in the scene yet.", this);
                enabled = false;
                return;
            }

            session.OnSessionStarted += HandleSessionStarted;
            session.OnSessionEnded   += HandleSessionEnded;
            subscribed = true;

            if (restartButton) restartButton.onClick.AddListener(HandleRestartClicked);

            // Reflect current state on enable (e.g. wiring this view mid-Ended).
            if (session.State == SessionState.Ended)
                ShowFor(session.LastEndReason ?? SessionEndReason.ManualQuit, session.SessionDuration);
            else
                Hide();
        }

        private void OnDisable()
        {
            if (subscribed && session)
            {
                session.OnSessionStarted -= HandleSessionStarted;
                session.OnSessionEnded   -= HandleSessionEnded;
            }
            subscribed = false;

            if (restartButton) restartButton.onClick.RemoveListener(HandleRestartClicked);
        }

        // ----- Handlers -----
        private void HandleSessionStarted() => Hide();

        private void HandleSessionEnded(SessionEndReason reason)
        {
            // ManualRestart is a transient end-state — the next StartSession is
            // queued and would hide us immediately. ReturnedToTitle hands the
            // screen to the start menu instead. Only show on terminal ends.
            if (reason == SessionEndReason.ManualRestart) return;
            if (reason == SessionEndReason.ReturnedToTitle) return;
            ShowFor(reason, session ? session.SessionDuration : 0f);
        }

        private void HandleRestartClicked()
        {
            if (session) session.RestartSession();
        }

        // ----- Private -----
        private void ShowFor(SessionEndReason reason, float duration)
        {
            if (panelRoot)     panelRoot.SetActive(true);
            if (headlineLabel) headlineLabel.text = HeadlineFor(reason);
            if (subtextLabel)  subtextLabel.text  = $"Survived {FormatMMSS(duration)}";
        }

        private void Hide()
        {
            if (panelRoot) panelRoot.SetActive(false);
        }

        private static string HeadlineFor(SessionEndReason reason)
        {
            switch (reason)
            {
                case SessionEndReason.HolobiontDeath: return "Holobiont Collapsed";
                case SessionEndReason.ManualQuit:     return "Session Ended";
                default:                              return "Game Over";
            }
        }

        private static string FormatMMSS(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f) seconds = 0f;
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
