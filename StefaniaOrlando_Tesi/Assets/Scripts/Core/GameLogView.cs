using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace Holobiont
{
    /*
     * Renders messages from GameLog as a rolling list in a TMP_Text.
     * Newest message at the bottom. Messages drop when they exceed maxLines
     * or when their age exceeds messageLifetime.
     *
     * Setup: drop on a Canvas, assign the TMP_Text. Anything that calls
     * GameLog.Post(...) shows up here.
     */
    [DisallowMultipleComponent]
    public class GameLogView : MonoBehaviour
    {
        // ----- Config -----
        [Header("Output")]
        [Tooltip("Target TMP text field that receives the rendered log.")]
        [SerializeField] private TMP_Text textField;

        [Header("Buffer")]
        [Tooltip("Maximum number of messages displayed at once.")]
        [SerializeField, Min(1)] private int maxLines = 8;

        [Tooltip("Seconds before a message is dropped. 0 = never expire by age.")]
        [SerializeField, Min(0f)] private float messageLifetime = 8f;

        [Header("Format")]
        [Tooltip("Prefix each line with elapsed seconds in brackets.")]
        [SerializeField] private bool showTimestamp = true;

        [Tooltip("Format string. {0} = elapsed seconds (int), {1} = message text.")]
        [SerializeField] private string lineFormat = "[{0:D3}s] {1}";

        // ----- State -----
        private readonly LinkedList<Entry> entries = new LinkedList<Entry>();
        private readonly StringBuilder sb = new StringBuilder();
        private bool dirty;

        private struct Entry { public string text; public float bornAt; }

        // ----- Lifecycle -----
        private void OnEnable()
        {
            GameLog.OnMessage += HandleMessage;
            Render();
        }

        private void OnDisable()
        {
            GameLog.OnMessage -= HandleMessage;
        }

        private void Update()
        {
            if (messageLifetime > 0f && entries.Count > 0)
            {
                float now = Time.time;
                while (entries.First is not null && now - entries.First.Value.bornAt > messageLifetime)
                {
                    entries.RemoveFirst();
                    dirty = true;
                }
            }
            if (dirty) Render();
        }

        // ----- Private -----
        private void HandleMessage(string message)
        {
            entries.AddLast(new Entry { text = message, bornAt = Time.time });
            while (entries.Count > maxLines) entries.RemoveFirst();
            dirty = true;
        }

        private void Render()
        {
            if (!textField) return;

            sb.Clear();
            foreach (var e in entries)
            {
                if (showTimestamp)
                    sb.AppendFormat(lineFormat, (int)e.bornAt, e.text);
                else
                    sb.Append(e.text);
                sb.Append('\n');
            }
            textField.text = sb.ToString();
            dirty = false;
        }

        /// <summary>Inspector-runnable smoke test to verify wiring.</summary>
        [ContextMenu("Test Post")]
        public void TestPost() => GameLog.Post($"Test message at {Time.time:F1}s");
    }
}
