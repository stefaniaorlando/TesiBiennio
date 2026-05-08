namespace Holobiont
{
    /// <summary>Top-level session lifecycle phase. Owned by <see cref="GameSessionManager"/>.</summary>
    public enum SessionState
    {
        /// <summary>Manager hasn't been started yet. autoStartOnEnable is off and nothing has called StartSession().</summary>
        PreStart,

        /// <summary>Simulation is ticking. The normal play state.</summary>
        Running,

        /// <summary>The session ended (death, manual quit, or pre-restart). Clock is paused; views can show game-over UI.</summary>
        Ended
    }

    /// <summary>Why a session ended. Surfaced through OnSessionEnding/Ended for views to render.</summary>
    public enum SessionEndReason
    {
        /// <summary>The holobiont's cascade failure completed and OnDeath fired.</summary>
        HolobiontDeath,

        /// <summary>The player explicitly restarted; the next StartSession is already queued.</summary>
        ManualRestart,

        /// <summary>The player quit to a menu or closed the session without restarting.</summary>
        ManualQuit,

        /// <summary>The player chose "back to title" from the pause menu — the start screen should show, the game-over screen should not.</summary>
        ReturnedToTitle
    }
}
