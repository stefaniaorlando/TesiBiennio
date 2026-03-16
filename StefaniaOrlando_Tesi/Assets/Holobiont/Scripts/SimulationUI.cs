using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*
 * ============================================================================
 * SIMULATION UI
 * ============================================================================
 *
 * WHAT THIS DOES:
 * Bridges HolobiontSimulation to the UI:
 * - Updates display labels next to each slider
 * - Caps creature slider maxValues so the total never exceeds maxCreatures
 * - Syncs slider values back to the actual (clamped) counts
 *
 * HOW TO SET UP:
 * 1. Add this component to any GameObject in your simulation scene.
 * 2. Drag your HolobiontSimulation into the Simulation slot.
 * 3. Wire up all the slider and label references in the Inspector.
 * 4. Make sure the three creature sliders still call Set*Count on
 *    HolobiontSimulation via their OnValueChanged events.
 *
 * NOTE ON SLIDER RANGES:
 * - Creature sliders:      min = 0, max = 5 (maxValue is updated dynamically)
 * - Total creatures:       min = 0, max = 5, non-interactable
 * - Temperature:           min/max match HolobiontSimulation.maxTemperature
 * - Energy:                min = 0, max = 1, non-interactable
 * - Heat/Cold resistance:  min = your expected minimum (e.g. -10), max = your expected maximum
 *
 * ============================================================================
 */

public class SimulationUI : MonoBehaviour
{
    // =========================================================================
    // REFERENCES
    // =========================================================================

    [Header("Simulation")]
    [SerializeField] private HolobiontSimulation simulation;

    // =========================================================================
    // CREATURE SLIDERS & LABELS
    // =========================================================================

    [Header("Creature Sliders")]
    [SerializeField] private Slider nurturerSlider;
    [SerializeField] private Slider crioSlider;
    [SerializeField] private Slider termoSlider;
    [SerializeField] private Slider totalSlider;

    [Header("Creature Labels")]
    [SerializeField] private TMP_Text nurturerLabel;
    [SerializeField] private TMP_Text crioLabel;
    [SerializeField] private TMP_Text termoLabel;
    [SerializeField] private TMP_Text totalLabel;

    // =========================================================================
    // TEMPERATURE
    // =========================================================================

    [Header("Temperature")]
    [SerializeField] private Slider temperatureSlider;
    [SerializeField] private TMP_Text temperatureLabel;

    // =========================================================================
    // HOLOBIONT STATS
    // =========================================================================

    [Header("Holobiont Stats")]
    [SerializeField] private Slider energySlider;
    [SerializeField] private TMP_Text energyLabel;

    [SerializeField] private Slider heatResistanceSlider;
    [SerializeField] private TMP_Text heatResistanceLabel;

    [SerializeField] private Slider coldResistanceSlider;
    [SerializeField] private TMP_Text coldResistanceLabel;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    private void OnEnable()
    {
        simulation.OnCompositionChanged.AddListener(HandleCompositionChanged);
        simulation.OnEnergyChanged.AddListener(HandleEnergyChanged);
        simulation.OnHeatResistanceChanged.AddListener(HandleHeatResistanceChanged);
        simulation.OnColdResistanceChanged.AddListener(HandleColdResistanceChanged);

        if (temperatureSlider != null)
            temperatureSlider.onValueChanged.AddListener(HandleTemperatureChanged);
    }

    private void OnDisable()
    {
        simulation.OnCompositionChanged.RemoveListener(HandleCompositionChanged);
        simulation.OnEnergyChanged.RemoveListener(HandleEnergyChanged);
        simulation.OnHeatResistanceChanged.RemoveListener(HandleHeatResistanceChanged);
        simulation.OnColdResistanceChanged.RemoveListener(HandleColdResistanceChanged);

        if (temperatureSlider != null)
            temperatureSlider.onValueChanged.RemoveListener(HandleTemperatureChanged);
    }

    private void Start()
    {
        RefreshCompositionUI();

        if (temperatureLabel != null)
            temperatureLabel.text = FormatTemperature(simulation.Temperature);
    }

    // =========================================================================
    // EVENT HANDLERS
    // =========================================================================

    private void HandleCompositionChanged(int _)
    {
        RefreshCompositionUI();
    }

    private void HandleEnergyChanged(float normalized)
    {
        if (energySlider != null)
            energySlider.value = normalized;

        if (energyLabel != null)
            energyLabel.text = $"{simulation.CurrentEnergy:F1} / {simulation.TotalMaxEnergy:F1}";
    }

    private void HandleHeatResistanceChanged(float value)
    {
        if (heatResistanceSlider != null)
            heatResistanceSlider.value = value;

        if (heatResistanceLabel != null)
            heatResistanceLabel.text = FormatResistance(value);
    }

    private void HandleColdResistanceChanged(float value)
    {
        if (coldResistanceSlider != null)
            coldResistanceSlider.value = value;

        if (coldResistanceLabel != null)
            coldResistanceLabel.text = FormatResistance(value);
    }

    private void HandleTemperatureChanged(float value)
    {
        if (temperatureLabel != null)
            temperatureLabel.text = FormatTemperature(value);
    }

    // =========================================================================
    // COMPOSITION REFRESH
    // =========================================================================

    private void RefreshCompositionUI()
    {
        int n         = simulation.NurturerCount;
        int c         = simulation.CrioCount;
        int t         = simulation.TermoCount;
        int total     = simulation.TotalCreatures;
        int available = simulation.MaxCreatures - total;

        // Cap each slider: current count + remaining free slots
        // SetValueWithoutNotify avoids triggering OnValueChanged (no feedback loop)
        if (nurturerSlider != null)
        {
            nurturerSlider.maxValue = n + available;
            nurturerSlider.SetValueWithoutNotify(n);
        }
        if (crioSlider != null)
        {
            crioSlider.maxValue = c + available;
            crioSlider.SetValueWithoutNotify(c);
        }
        if (termoSlider != null)
        {
            termoSlider.maxValue = t + available;
            termoSlider.SetValueWithoutNotify(t);
        }
        if (totalSlider != null)
            totalSlider.SetValueWithoutNotify(total);

        // Update labels
        if (nurturerLabel != null) nurturerLabel.text = n.ToString();
        if (crioLabel     != null) crioLabel.text     = c.ToString();
        if (termoLabel    != null) termoLabel.text     = t.ToString();
        if (totalLabel    != null) totalLabel.text     = total.ToString();
    }

    // =========================================================================
    // FORMATTING
    // =========================================================================

    private static string FormatResistance(float value) => value.ToString("F1");

    private static string FormatTemperature(float value) => $"{value:F1} °C";
}
