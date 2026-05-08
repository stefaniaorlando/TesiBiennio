using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace HolobiontLegacy
{

/*
 * ============================================================================
 * SIMULATION UI
 * ============================================================================
 *
 * WHAT THIS DOES:
 * Reads state from HolobiontSimulation every frame and updates the UI.
 * Also wires slider input back to the simulation on Start.
 *
 * HOW TO SET UP:
 * 1. Add this component to any GameObject in the scene.
 * 2. Drag HolobiontSimulation into the Simulation slot.
 * 3. In Creature Entries, add one entry per creature type in the SAME ORDER
 *    as the Creature Slots list in HolobiontSimulation. Assign each slider
 *    and label. The sliders are wired automatically at runtime.
 * 4. Assign sliders and labels for each environment variable and for energy.
 *
 * NOTE ON SLIDER RANGES (set in the Inspector):
 *   Creature sliders:    min = 0, max = maxCreatures (maxValue updated dynamically)
 *   Total slider:        min = 0, max = maxCreatures, non-interactable
 *   Temperature slider:  min = -maxTemperature, max = +maxTemperature
 *   Humidity slider:     min = 0, max = maxHumidity
 *   Light slider:        min = 0, max = maxLight
 *   Toxicity slider:     min = 0, max = maxToxicity
 *   Energy slider:       min = 0, max = 1, non-interactable
 *
 * ============================================================================
 */

[System.Serializable]
public class CreatureUIEntry
{
    [Tooltip("Slider that controls the count of this creature type.")]
    public Slider slider;
    [Tooltip("Label showing the current count.")]
    public TMP_Text label;
}

public class SimulationUI : MonoBehaviour
{
    // =========================================================================
    // REFERENCES
    // =========================================================================

    [Header("Simulation")]
    [SerializeField] private HolobiontSimulation simulation;

    // =========================================================================
    // CREATURE SLIDERS
    // =========================================================================

    [Header("Creature Entries")]
    [Tooltip("One entry per creature type, in the same order as HolobiontSimulation's Creature Slots.")]
    [SerializeField] private List<CreatureUIEntry> creatureEntries = new List<CreatureUIEntry>();

    [SerializeField] private Slider  totalSlider;
    [SerializeField] private TMP_Text totalLabel;

    // =========================================================================
    // ENVIRONMENT SLIDERS
    // =========================================================================

    [Header("Temperature")]
    [SerializeField] private Slider   temperatureSlider;
    [SerializeField] private TMP_Text temperatureLabel;

    [Header("Humidity")]
    [SerializeField] private Slider   humiditySlider;
    [SerializeField] private TMP_Text humidityLabel;

    [Header("Light")]
    [SerializeField] private Slider   lightSlider;
    [SerializeField] private TMP_Text lightLabel;

    [Header("Toxicity")]
    [SerializeField] private Slider   toxicitySlider;
    [SerializeField] private TMP_Text toxicityLabel;

    // =========================================================================
    // RESISTANCE SLIDERS
    // =========================================================================

    [Header("Resistance Stats")]
    [Tooltip("Set slider max to the maximum expected resistance value.")]
    [SerializeField] private Slider   heatResistanceSlider;
    [SerializeField] private TMP_Text heatResistanceLabel;

    [SerializeField] private Slider   coldResistanceSlider;
    [SerializeField] private TMP_Text coldResistanceLabel;

    [SerializeField] private Slider   highHumidityResistanceSlider;
    [SerializeField] private TMP_Text highHumidityResistanceLabel;

    [SerializeField] private Slider   lowHumidityResistanceSlider;
    [SerializeField] private TMP_Text lowHumidityResistanceLabel;

    [SerializeField] private Slider   highLightResistanceSlider;
    [SerializeField] private TMP_Text highLightResistanceLabel;

    [SerializeField] private Slider   lowLightResistanceSlider;
    [SerializeField] private TMP_Text lowLightResistanceLabel;

    [SerializeField] private Slider   toxicityResistanceSlider;
    [SerializeField] private TMP_Text toxicityResistanceLabel;

    // =========================================================================
    // HOLOBIONT STATS
    // =========================================================================

    [Header("Holobiont Stats")]
    [SerializeField] private Slider   energySlider;
    [SerializeField] private TMP_Text energyLabel;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    private void Start()
    {
        // Wire each creature slider to its simulation slot
        for (int i = 0; i < creatureEntries.Count; i++)
        {
            int index = i; // captured by lambda
            creatureEntries[i].slider?.onValueChanged.AddListener(
                v => simulation.SetCount(index, (int)v));
        }

        // Wire environment sliders
        temperatureSlider?.onValueChanged.AddListener(simulation.SetTemperature);
        humiditySlider?.onValueChanged.AddListener(simulation.SetHumidity);
        lightSlider?.onValueChanged.AddListener(simulation.SetLight);
        toxicitySlider?.onValueChanged.AddListener(simulation.SetToxicity);
    }

    private void OnDisable()
    {
        foreach (var entry in creatureEntries)
            entry.slider?.onValueChanged.RemoveAllListeners();

        temperatureSlider?.onValueChanged.RemoveListener(simulation.SetTemperature);
        humiditySlider?.onValueChanged.RemoveListener(simulation.SetHumidity);
        lightSlider?.onValueChanged.RemoveListener(simulation.SetLight);
        toxicitySlider?.onValueChanged.RemoveListener(simulation.SetToxicity);
    }

    private void Update()
    {
        RefreshCreatureUI();
        RefreshEnvironmentUI();
        RefreshResistanceUI();
        RefreshEnergyUI();
    }

    // =========================================================================
    // PRIVATE REFRESH METHODS
    // =========================================================================

    private void RefreshCreatureUI()
    {
        int available = simulation.MaxCreatures - simulation.TotalCreatures;

        for (int i = 0; i < creatureEntries.Count && i < simulation.SlotCount; i++)
        {
            var entry = creatureEntries[i];
            int count = simulation.GetCount(i);

            if (entry.slider != null)
            {
                entry.slider.maxValue = count + available;
                entry.slider.SetValueWithoutNotify(count);
            }

            if (entry.label != null)
                entry.label.text = count.ToString();
        }

        if (totalSlider != null) totalSlider.SetValueWithoutNotify(simulation.TotalCreatures);
        if (totalLabel  != null) totalLabel.text = simulation.TotalCreatures.ToString();
    }

    private void RefreshEnvironmentUI()
    {
        // Sync slider positions to simulation values (SetValueWithoutNotify avoids
        // re-triggering the OnValueChanged listeners wired in Start).
        temperatureSlider?.SetValueWithoutNotify(simulation.Temperature);
        humiditySlider?.SetValueWithoutNotify(simulation.Humidity);
        lightSlider?.SetValueWithoutNotify(simulation.Light);
        toxicitySlider?.SetValueWithoutNotify(simulation.Toxicity);

        if (temperatureLabel != null)
            temperatureLabel.text = $"{simulation.Temperature:F1} °C";

        if (humidityLabel != null)
            humidityLabel.text = $"{simulation.Humidity:F1} %";

        if (lightLabel != null)
            lightLabel.text = $"{simulation.Light:F1} lux";

        if (toxicityLabel != null)
            toxicityLabel.text = $"{simulation.Toxicity:F1}";
    }

    private void RefreshResistanceUI()
    {
        SetResistance(heatResistanceSlider,         heatResistanceLabel,         simulation.TotalHeatResistance,         "Heat");
        SetResistance(coldResistanceSlider,         coldResistanceLabel,         simulation.TotalColdResistance,         "Cold");
        SetResistance(highHumidityResistanceSlider, highHumidityResistanceLabel, simulation.TotalHighHumidityResistance, "High Hum.");
        SetResistance(lowHumidityResistanceSlider,  lowHumidityResistanceLabel,  simulation.TotalLowHumidityResistance,  "Low Hum.");
        SetResistance(highLightResistanceSlider,    highLightResistanceLabel,    simulation.TotalHighLightResistance,    "High Light");
        SetResistance(lowLightResistanceSlider,     lowLightResistanceLabel,     simulation.TotalLowLightResistance,     "Low Light");
        SetResistance(toxicityResistanceSlider,     toxicityResistanceLabel,     simulation.TotalToxicityResistance,     "Toxicity");
    }

    private void SetResistance(Slider slider, TMP_Text label, float value, string name)
    {
        if (slider != null)
            slider.SetValueWithoutNotify(value);
        if (label != null)
            label.text = $"{name}: {value:F1}";
    }

    private void RefreshEnergyUI()
    {
        if (energySlider != null)
            energySlider.value = simulation.NormalizedEnergy;

        if (energyLabel != null)
            energyLabel.text = $"{simulation.CurrentEnergy:F1} / {simulation.TotalMaxEnergy:F1}";
    }
}

}
