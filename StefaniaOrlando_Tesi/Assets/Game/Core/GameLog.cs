using System;

namespace Holobiont
{
    /*
     * Process-wide message bus for short, human-readable notifications.
     * Anyone can post, anyone can subscribe. Intended for the on-screen
     * notification box, debug overlays, ambient narrative cues, etc.
     *
     * Stateless and main-thread only — no buffer of past messages, so
     * subscribers only see posts that happen after they subscribe. Views are
     * expected to exist in the scene before any publisher posts (Awake/OnEnable
     * run before Update, so that's the default case).
     */
    public static class GameLog
    {
        /// <summary>Fires once per Post() call with the raw message string.</summary>
        public static event Action<string> OnMessage;

        /// <summary>Broadcasts <paramref name="message"/> to every subscriber. No-op if null or empty.</summary>
        public static void Post(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            OnMessage?.Invoke(message);
        }
    }
}
