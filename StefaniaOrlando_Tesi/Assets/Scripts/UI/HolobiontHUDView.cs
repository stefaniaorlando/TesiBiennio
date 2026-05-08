using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Holobiont
{
    /*
     * Read-only on-screen HUD for the holobiont.
     *
     * Mirrors EnvironmentHUDView's structure — every widget is a dumb view that
     * receives values pushed each frame. Drop in only the widgets you've
     * authored on the canvas; un-wired slots are skipped silently.
     *
     * Layout (just the inspector grouping; actual on-screen arrangement is
     * decided by your canvas):
     *   A — Survival vitals: energy bar, net energy flow, phase label
     *   B — Composition:    population bar, nutrici / scudo / hub counts
     *   C — Resistance:     four ValueSlider rows, one per env variable
     *
     * Update uses Time.deltaTime per project_conventions (view layer keeps
     * updating while the simulation is paused).
     */
    [DisallowMultipleComponent]
    public class HolobiontHUDView : MonoBehaviour
    {
        // ----- Source -----
        [Header("Source")]
        [Tooltip("Holobiont to mirror. Required.")]
        [SerializeField] private HolobiontManager source;

        // ----- A. Vitals -----
        [Header("A. Vitals")]
        [Tooltip("Energy as a fraction of MaxEnergy [0,1]. Plain slider — danger is communicated via phaseLabel.")]
        [SerializeField] private Slider energyRow;

        [Tooltip("Signed energy/sec — inflow minus drain. Negative means losing.")]
        [SerializeField] private ValueSlider netFlowRow;

        [Tooltip("How wide the netFlowRow's slider should be in either direction. Symmetric, in energy/sec units.")]
        [Min(0.01f), SerializeField] private float netFlowDisplayRange = 5f;

        [Tooltip("Plain text label showing Phase ('Stable' / 'Declining' / 'Cascade' / 'Dead').")]
        [SerializeField] private TMP_Text phaseLabel;

        [Tooltip("Optional. Frequency-shaped metabolic-rate multiplier on inflow + drain. 1.0 baseline. Leave null to omit.")]
        [SerializeField] private ValueSlider metabolicRateRow;

        [Tooltip("Top of the metabolic-rate row's 0..max range. Curve typically returns 0..2.")]
        [Min(0.1f), SerializeField] private float metabolicRateDisplayMax = 2f;

        [Tooltip("Tint applied to phaseLabel when Phase is Stable.")]
        [SerializeField] private Color stablePhaseColor    = new Color(0.65f, 0.95f, 0.70f);

        [Tooltip("Tint applied to phaseLabel when Phase is Declining.")]
        [SerializeField] private Color decliningPhaseColor = new Color(0.95f, 0.85f, 0.45f);

        [Tooltip("Tint applied to phaseLabel during CascadeFailure.")]
        [SerializeField] private Color cascadePhaseColor   = new Color(0.95f, 0.45f, 0.40f);

        [Tooltip("Tint applied to phaseLabel after Dead.")]
        [SerializeField] private Color deadPhaseColor      = new Color(0.50f, 0.25f, 0.25f);

        // ----- B. Composition -----
        [Header("B. Composition")]
        [Tooltip("Creature count vs. carrying capacity. Range auto-tracks capacity (hub bonuses can grow it).")]
        [SerializeField] private ValueSlider populationRow;

        [Tooltip("Single-line creature breakdown. Format: 'Nutrici: 3 · Scudo: 4 · Hub: 2'.")]
        [SerializeField] private TMP_Text countsLabel;

        // ----- C. Resistance shape -----
        [Header("C. Resistance shape")]
        [Tooltip("Per-axis tolerance widths [0,1]. Currently uniform across axes (Phase-1 simplification in HolobiontManager); the four rows still bind correctly when per-axis resistance lands.")]
        [SerializeField] private ValueSlider tempResistance;
        [SerializeField] private ValueSlider lightResistance;
        [SerializeField] private ValueSlider humidityResistance;
        [SerializeField] private ValueSlider toxicityResistance;

        // ----- Lifecycle -----
        private void OnEnable()
        {
            if (!source)
            {
                Debug.LogError($"{nameof(HolobiontHUDView)} has no {nameof(HolobiontManager)} assigned.", this);
                enabled = false;
                return;
            }

            // One-time titles (ValueSlider rows only) + fixed ranges. Population's
            // range is dynamic and gets refreshed every frame in Update.
            if (energyRow)        { energyRow.minValue = 0f; energyRow.maxValue = 1f; energyRow.wholeNumbers = false; }
            if (netFlowRow)       { netFlowRow.SetTitle("Net flow"); netFlowRow.SetRange(-netFlowDisplayRange, netFlowDisplayRange); }
            if (metabolicRateRow) { metabolicRateRow.SetTitle("Metabolic"); metabolicRateRow.SetRange(0f, metabolicRateDisplayMax); }
            if (populationRow)      { populationRow.SetTitle("Population"); }
            if (tempResistance)     { tempResistance.SetTitle("Temp R");     tempResistance.SetRange(0f, 1f); }
            if (lightResistance)    { lightResistance.SetTitle("Light R");   lightResistance.SetRange(0f, 1f); }
            if (humidityResistance) { humidityResistance.SetTitle("Humid R"); humidityResistance.SetRange(0f, 1f); }
            if (toxicityResistance) { toxicityResistance.SetTitle("Tox R");  toxicityResistance.SetRange(0f, 1f); }
        }

        private void Update()
        {
            if (!source) return;
            var s = source.State;

            // ----- Vitals -----
            if (energyRow) energyRow.SetValueWithoutNotify(s.EnergyNormalized);

            if (netFlowRow)
            {
                netFlowRow.SetValue(Mathf.Clamp(s.NetEnergyFlow, -netFlowDisplayRange, netFlowDisplayRange));
                netFlowRow.SetHighlight(s.NetEnergyFlow < 0f);
            }

            if (phaseLabel)
            {
                phaseLabel.text  = FormatPhase(s.Phase);
                phaseLabel.color = ColorFor(s.Phase);
            }

            if (metabolicRateRow)
                metabolicRateRow.SetValue(Mathf.Clamp(s.MetabolicRate, 0f, metabolicRateDisplayMax));

            // ----- Composition -----
            if (populationRow)
            {
                // Range tracks live capacity — hub bonuses can grow it mid-session.
                int capacity = Mathf.Max(1, s.CarryingCapacity);
                populationRow.SetRange(0f, capacity);
                populationRow.SetValue(s.CreatureCount);
                populationRow.SetHighlight(s.AtCapacity);
            }

            if (countsLabel)
                countsLabel.text = $"Nutrici: {s.NutriciCount} · Scudo: {s.ScudoCount} · Hub: {s.HubCount}";

            // ----- Resistance shape -----
            var r = s.Resistance;
            if (tempResistance)     tempResistance.SetValue(r.temperatureTolerance);
            if (lightResistance)    lightResistance.SetValue(r.lightTolerance);
            if (humidityResistance) humidityResistance.SetValue(r.humidityTolerance);
            if (toxicityResistance) toxicityResistance.SetValue(r.toxicityTolerance);
        }

        // ----- Private -----
        private static string FormatPhase(HolobiontPhase phase)
        {
            switch (phase)
            {
                case HolobiontPhase.Stable:         return "Stable";
                case HolobiontPhase.Declining:      return "Declining";
                case HolobiontPhase.CascadeFailure: return "Cascade";
                case HolobiontPhase.Dead:           return "Dead";
                default:                            return phase.ToString();
            }
        }

        private Color ColorFor(HolobiontPhase phase)
        {
            switch (phase)
            {
                case HolobiontPhase.Stable:         return stablePhaseColor;
                case HolobiontPhase.Declining:      return decliningPhaseColor;
                case HolobiontPhase.CascadeFailure: return cascadePhaseColor;
                case HolobiontPhase.Dead:           return deadPhaseColor;
                default:                            return Color.white;
            }
        }
    }
}
