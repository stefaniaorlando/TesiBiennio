using TMPro;
using UnityEngine;

namespace Holobiont
{
    /*
     * Single-instance world-tracking tooltip for creatures. Shown via Show(creature),
     * hidden via Hide() or automatically when the target creature is destroyed.
     *
     * Content slots (TMP labels + ValueSlider rows) are all optional — un-wired
     * slots are skipped silently, so a designer can iterate from minimal to full.
     *
     * Assumes Screen Space - Overlay canvas. The parent canvas RectTransform is
     * resolved automatically in OnEnable so no inspector wiring is needed for it.
     */
    [DisallowMultipleComponent]
    public class CreatureTooltipView : MonoBehaviour
    {
        // ----- Position -----
        [Header("Position")]
        [Tooltip("Root RectTransform of the tooltip panel. Toggled active by Show/Hide.")]
        [SerializeField] private RectTransform tooltipRoot;

        [Tooltip("Pixel offset applied to the tooltip relative to the creature's screen-space anchor.")]
        [SerializeField] private Vector2 screenOffset = new Vector2(40f, 40f);

        private RectTransform canvasRect;

        // ----- Content -----
        [Header("Content")]
        [Tooltip("Type label — 'Nutrici' / 'Scudo' / 'Hub'.")]
        [SerializeField] private TMP_Text typeLabel;

        [Tooltip("Status line — 'Bound' / 'Unbound'.")]
        [SerializeField] private TMP_Text statusLabel;

        [Tooltip("Stress row, range 0..1.")]
        [SerializeField] private ValueSlider stressRow;

        [Tooltip("Affinity efficiency row, range 0..1.")]
        [SerializeField] private ValueSlider efficiencyRow;

        [Header("Affinity rows  (optional)")]
        [SerializeField] private ValueSlider tempAffinityRow;
        [SerializeField] private ValueSlider lightAffinityRow;
        [SerializeField] private ValueSlider humidityAffinityRow;
        [SerializeField] private ValueSlider toxicityAffinityRow;

        // ----- State -----
        private Creature target;
        private GameSessionManager subscribedSession;

        // ----- Lifecycle -----
        private void OnEnable()
        {
            var canvas = tooltipRoot ? tooltipRoot.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
            canvasRect = canvas ? canvas.GetComponent<RectTransform>() : null;

            // One-time row config (titles + ranges).
            if (stressRow)           { stressRow.SetTitle("Stress");         stressRow.SetRange(0f, 1f); }
            if (efficiencyRow)       { efficiencyRow.SetTitle("Efficiency"); efficiencyRow.SetRange(0f, 1f); }
            if (tempAffinityRow)     { tempAffinityRow.SetTitle("Temp");     tempAffinityRow.SetRange(0f, 1f); }
            if (lightAffinityRow)    { lightAffinityRow.SetTitle("Light");   lightAffinityRow.SetRange(0f, 1f); }
            if (humidityAffinityRow) { humidityAffinityRow.SetTitle("Humid"); humidityAffinityRow.SetRange(0f, 1f); }
            if (toxicityAffinityRow) { toxicityAffinityRow.SetTitle("Tox");  toxicityAffinityRow.SetRange(0f, 1f); }

            Hide();

            // Auto-close on session end so the tooltip doesn't keep tracking a
            // doomed creature between Ended and the next StartSession.
            subscribedSession = GameSessionManager.Instance;
            if (subscribedSession) subscribedSession.OnSessionEnding += HandleSessionEnding;
        }

        private void OnDisable()
        {
            if (subscribedSession is not null)
            {
                subscribedSession.OnSessionEnding -= HandleSessionEnding;
                subscribedSession = null;
            }
        }

        private void HandleSessionEnding(SessionEndReason _) => Hide();

        // ----- Public API -----
        /// <summary>Open the tooltip on the given creature. Subsequent Update ticks track its position.</summary>
        public void Show(Creature creature)
        {
            if (!creature) { Hide(); return; }
            target = creature;
            if (tooltipRoot) tooltipRoot.gameObject.SetActive(true);

            // Push immediately so the first frame is fresh, not stale text from a previous target.
            PushContent(creature);
            PushPosition(creature);
        }

        /// <summary>Close the tooltip. Safe to call when already hidden.</summary>
        public void Hide()
        {
            target = null;
            if (tooltipRoot) tooltipRoot.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!target)
            {
                // Watch for destruction even when Hide() wasn't explicitly called.
                if (tooltipRoot && tooltipRoot.gameObject.activeSelf) Hide();
                return;
            }

            PushContent(target);
            PushPosition(target);
        }

        // ----- Private -----
        private void PushContent(Creature c)
        {
            if (typeLabel)   typeLabel.text   = c.Type.ToString();
            if (statusLabel) statusLabel.text = c.Status == BondStatus.Bound ? "Bound" : "Unbound";

            if (stressRow)     stressRow.SetValue(c.Stress);
            if (efficiencyRow) efficiencyRow.SetValue(c.AffinityEfficiency);

            var a = c.Affinity;
            if (tempAffinityRow)     tempAffinityRow.SetValue(a.idealTemperature);
            if (lightAffinityRow)    lightAffinityRow.SetValue(a.idealLight);
            if (humidityAffinityRow) humidityAffinityRow.SetValue(a.idealHumidity);
            if (toxicityAffinityRow) toxicityAffinityRow.SetValue(a.idealToxicity);
        }

        private void PushPosition(Creature c)
        {
            if (!tooltipRoot || !canvasRect) return;
            var cam = Camera.main;
            if (!cam) return;

            Vector3 screen = cam.WorldToScreenPoint(c.transform.position);

            // WorldToScreenPoint returns negative z when the target is behind the
            // camera and mirrors x/y to the wrong quadrant — hide the visual until
            // it comes back into view. Target tracking continues either way.
            bool inFront = screen.z >= 0f;
            if (tooltipRoot.gameObject.activeSelf != inFront)
                tooltipRoot.gameObject.SetActive(inFront);
            if (!inFront) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
            {
                tooltipRoot.anchoredPosition = local + screenOffset;
            }
        }
    }
}
