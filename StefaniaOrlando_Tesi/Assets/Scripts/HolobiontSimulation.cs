using UnityEngine;
using System.Collections.Generic;

/*
 * ============================================================================
 * HOLOBIONT SIMULATION
 * ============================================================================
 *
 * WHAT THIS DOES:
 * Manages the composition and state of the holobiont:
 * - Tracks creature counts across any number of creature types (max 5 total)
 * - Computes total life energy, including boosts from Idro/Xero/Foto/Scoto
 * - Drains energy over time based on environmental stress and creature resistances
 *
 * CREATURE ROSTER:
 *   Idro   — thrives in high humidity  (boosts energy as humidity rises)
 *   Xero   — thrives in low humidity   (boosts energy as humidity falls)
 *   Foto   — thrives in high light     (boosts energy as light rises)
 *   Scoto  — thrives in low light      (boosts energy as light falls)
 *   Crio   — resists cold stress
 *   Termo  — resists heat stress
 *   Filtro — resists toxicity stress
 *
 * ENERGY BOOST:
 *   Nurturers (Idro, Xero, Foto, Scoto) multiply their energy contribution
 *   based on how extreme the matching environmental variable is.
 *   The boost factor is the same curve as stress — so Idro thrives exactly
 *   when high humidity is stressful for the holobiont.
 *
 * DRAIN MODEL:
 *   Each second, energy drains by:
 *     baseDrain (always active)
 *     + for each stress direction: stress * maxStressDrain - resistance * resistanceScale
 *   Drain per stress direction is always >= 0.
 *
 * HOW TO SET UP:
 * 1. Add this component to a GameObject in your scene.
 * 2. In the Creature Slots list, add one entry per creature type and assign
 *    the matching CreatureTypeSO asset.
 * 3. Add SimulationUI to the scene and assign sliders for each creature
 *    in the same order as this list.
 *
 * ============================================================================
 */

[System.Serializable]
public class CreatureSlot
{
    public CreatureTypeSO type;
    [HideInInspector] public int count;
}

public class HolobiontSimulation : MonoBehaviour
{
    // =========================================================================
    // CREATURE SLOTS
    // =========================================================================

    [Header("Creature Slots")]
    [Tooltip("Add one entry per creature type. Order must match SimulationUI creature entries.")]
    [SerializeField] private List<CreatureSlot> creatures = new List<CreatureSlot>();

    [Header("Composition")]
    [Tooltip("Maximum total creatures allowed in the holobiont.")]
    [SerializeField] private int maxCreatures = 5;

    // =========================================================================
    // ENVIRONMENT
    // =========================================================================

    [Header("Temperature")]
    [Tooltip("Slider range: -maxTemperature to +maxTemperature.")]
    [SerializeField] private float maxTemperature = 50f;
    [Tooltip("Temperature above which heat stress begins.")]
    [SerializeField] private float heatStressThreshold = 10f;
    [Tooltip("Temperature below which cold stress begins (negative value).")]
    [SerializeField] private float coldStressThreshold = -10f;
    private float _temperature = 0f;

    [Header("Humidity")]
    [Tooltip("Slider range: 0 to maxHumidity.")]
    [SerializeField] private float maxHumidity = 100f;
    [Tooltip("Humidity above which high-humidity stress begins (and Idro boost activates).")]
    [SerializeField] private float highHumidityThreshold = 70f;
    [Tooltip("Humidity below which low-humidity stress begins (and Xero boost activates).")]
    [SerializeField] private float lowHumidityThreshold = 30f;
    private float _humidity = 50f;

    [Header("Light")]
    [Tooltip("Slider range: 0 to maxLight.")]
    [SerializeField] private float maxLight = 100f;
    [Tooltip("Light above which high-light stress begins (and Foto boost activates).")]
    [SerializeField] private float highLightThreshold = 70f;
    [Tooltip("Light below which low-light stress begins (and Scoto boost activates).")]
    [SerializeField] private float lowLightThreshold = 30f;
    private float _light = 50f;

    [Header("Toxicity")]
    [Tooltip("Slider range: 0 to maxToxicity.")]
    [SerializeField] private float maxToxicity = 100f;
    [Tooltip("Toxicity above which stress begins.")]
    [SerializeField] private float toxicityStressThreshold = 30f;
    private float _toxicity = 0f;

    // =========================================================================
    // ENERGY & DRAIN SETTINGS
    // =========================================================================

    [Header("Energy & Drain")]
    [Tooltip("Flat energy drain per second, regardless of environment.")]
    [SerializeField] private float baseDrainPerSecond = 0.5f;
    [Tooltip("Maximum stress drain per second at full stress, before resistance is applied.")]
    [SerializeField] private float maxStressDrainPerSecond = 3f;
    [Tooltip("How much each point of resistance reduces stress drain per second.")]
    [SerializeField] private float resistanceDrainScale = 0.1f;

    // =========================================================================
    // RUNTIME STATE
    // =========================================================================

    private float _currentEnergy;
    private bool _isCollapsed;

    // =========================================================================
    // PUBLIC API — composition
    // =========================================================================

    public int MaxCreatures  => maxCreatures;
    public int SlotCount     => creatures.Count;
    public int TotalCreatures
    {
        get { int t = 0; foreach (var s in creatures) t += s.count; return t; }
    }

    public CreatureTypeSO GetCreatureType(int index) => creatures[index].type;
    public int            GetCount(int index)        => creatures[index].count;

    // =========================================================================
    // PUBLIC API — environment
    // =========================================================================

    public float Temperature => _temperature;
    public float Humidity    => _humidity;
    public float Light       => _light;
    public float Toxicity    => _toxicity;

    // =========================================================================
    // PUBLIC API — stress (0-1)
    // =========================================================================

    public float HeatStress => _temperature > heatStressThreshold
        ? Mathf.Clamp01((_temperature - heatStressThreshold) / (maxTemperature - heatStressThreshold)) : 0f;

    public float ColdStress => _temperature < coldStressThreshold
        ? Mathf.Clamp01((coldStressThreshold - _temperature) / (maxTemperature - coldStressThreshold)) : 0f;

    /// <summary>Also used as Idro energy boost factor.</summary>
    public float HighHumidityStress => _humidity > highHumidityThreshold
        ? Mathf.Clamp01((_humidity - highHumidityThreshold) / (maxHumidity - highHumidityThreshold)) : 0f;

    /// <summary>Also used as Xero energy boost factor.</summary>
    public float LowHumidityStress => _humidity < lowHumidityThreshold
        ? Mathf.Clamp01((lowHumidityThreshold - _humidity) / lowHumidityThreshold) : 0f;

    /// <summary>Also used as Foto energy boost factor.</summary>
    public float HighLightStress => _light > highLightThreshold
        ? Mathf.Clamp01((_light - highLightThreshold) / (maxLight - highLightThreshold)) : 0f;

    /// <summary>Also used as Scoto energy boost factor.</summary>
    public float LowLightStress => _light < lowLightThreshold
        ? Mathf.Clamp01((lowLightThreshold - _light) / lowLightThreshold) : 0f;

    public float ToxicityStress => _toxicity > toxicityStressThreshold
        ? Mathf.Clamp01((_toxicity - toxicityStressThreshold) / (maxToxicity - toxicityStressThreshold)) : 0f;

    // =========================================================================
    // PUBLIC API — resistance totals
    // =========================================================================

    public float TotalHeatResistance         => SumStat(t => t.heatResistance);
    public float TotalColdResistance         => SumStat(t => t.coldResistance);
    public float TotalHighHumidityResistance => SumStat(t => t.highHumidityResistance);
    public float TotalLowHumidityResistance  => SumStat(t => t.lowHumidityResistance);
    public float TotalHighLightResistance    => SumStat(t => t.highLightResistance);
    public float TotalLowLightResistance     => SumStat(t => t.lowLightResistance);
    public float TotalToxicityResistance     => SumStat(t => t.toxicityResistance);

    // =========================================================================
    // PUBLIC API — energy
    // =========================================================================

    public float TotalMaxEnergy
    {
        get { float t = 0f; foreach (var s in creatures) t += EffectiveEnergy(s); return t; }
    }

    public float CurrentEnergy    => _currentEnergy;
    public float NormalizedEnergy => TotalMaxEnergy > 0f ? _currentEnergy / TotalMaxEnergy : 0f;
    public bool  IsCollapsed      => _isCollapsed;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    private void OnEnable()
    {
        ResetSimulation();
    }

    private void Update()
    {
        if (_isCollapsed || TotalCreatures == 0) return;

        _currentEnergy = Mathf.Max(_currentEnergy - ComputeDrainPerSecond() * Time.deltaTime, 0f);

        if (_currentEnergy <= 0f)
            _isCollapsed = true;
    }

    // =========================================================================
    // PUBLIC SETTERS
    // =========================================================================

    /// <summary>Wire each creature slider's OnValueChanged to this via SimulationUI.</summary>
    public void SetCount(int index, float value)
    {
        creatures[index].count = ClampCount(index, (int)value);
        ResetSimulation();
    }

    public void SetTemperature(float value) => _temperature = value;
    public void SetHumidity(float value)    => _humidity    = value;
    public void SetLight(float value)       => _light       = value;
    public void SetToxicity(float value)    => _toxicity    = value;

    public void ResetSimulation()
    {
        _isCollapsed   = false;
        _currentEnergy = TotalMaxEnergy;
    }

    // =========================================================================
    // PRIVATE HELPERS
    // =========================================================================

    private int ClampCount(int slotIndex, int requested)
    {
        int others = 0;
        for (int i = 0; i < creatures.Count; i++)
            if (i != slotIndex) others += creatures[i].count;
        return Mathf.Clamp(requested, 0, Mathf.Max(maxCreatures - others, 0));
    }

    /// <summary>
    /// Energy contributed by a slot, multiplied by nurturer boost factors.
    /// Boost factor = stress value of the matching environment variable,
    /// so Idro/Xero/Foto/Scoto peak exactly at the environmental extremes.
    /// </summary>
    private float EffectiveEnergy(CreatureSlot slot)
    {
        if (slot.type == null || slot.count == 0) return 0f;
        float boost = 1f;
        boost *= 1f + slot.type.highHumidityEnergyBoost * HighHumidityStress;
        boost *= 1f + slot.type.lowHumidityEnergyBoost  * LowHumidityStress;
        boost *= 1f + slot.type.highLightEnergyBoost    * HighLightStress;
        boost *= 1f + slot.type.lowLightEnergyBoost     * LowLightStress;
        return slot.count * slot.type.baseEnergy * boost;
    }

    private float ComputeDrainPerSecond()
    {
        float drain = baseDrainPerSecond;
        drain += StressDrain(HeatStress,         TotalHeatResistance);
        drain += StressDrain(ColdStress,         TotalColdResistance);
        drain += StressDrain(HighHumidityStress, TotalHighHumidityResistance);
        drain += StressDrain(LowHumidityStress,  TotalLowHumidityResistance);
        drain += StressDrain(HighLightStress,    TotalHighLightResistance);
        drain += StressDrain(LowLightStress,     TotalLowLightResistance);
        drain += StressDrain(ToxicityStress,     TotalToxicityResistance);
        return drain;
    }

    private float StressDrain(float stress, float resistance)
    {
        if (stress <= 0f) return 0f;
        return Mathf.Max(stress * maxStressDrainPerSecond - resistance * resistanceDrainScale, 0f);
    }

    private float SumStat(System.Func<CreatureTypeSO, float> stat)
    {
        float total = 0f;
        foreach (var slot in creatures)
            if (slot.type != null) total += slot.count * stat(slot.type);
        return total;
    }
}
