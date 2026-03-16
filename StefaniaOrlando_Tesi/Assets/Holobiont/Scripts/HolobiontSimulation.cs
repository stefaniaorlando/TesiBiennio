using UnityEngine;
using UnityEngine.Events;

/*
 * ============================================================================
 * HOLOBIONT SIMULATION
 * ============================================================================
 *
 * WHAT THIS DOES:
 * Manages the composition and state of the holobiont:
 * - Tracks how many Nurturers, Crios, and Termos are in the holobiont (max 5 total)
 * - Computes total life energy, heat resistance, and cold resistance
 * - Drains energy over time based on temperature stress and total resistance
 * - Fires events for the UI to react to
 *
 * HOW TO SET UP:
 * 1. Add this component to a GameObject in your simulation scene.
 * 2. Create three CreatureType assets (right-click → Create → Holobiont → Creature Type)
 *    and drag them into Nurturer, Crio, and Termo slots.
 * 3. Wire the slider OnValueChanged events to the Set* methods below:
 *      Nurturer slider → SetNurturerCount
 *      Crio slider     → SetCrioCount
 *      Termo slider    → SetTermoCount
 *      Temperature     → SetTemperature
 * 4. Wire OnEnergyChanged / OnResistanceChanged to your display labels.
 *
 * DRAIN MODEL:
 * Each second, energy drains by:
 *   baseDrain  (always active, one flat value for the whole holobiont)
 *   + heatStress * stressDrainScale - totalHeatResistance * resistanceScale   (when hot)
 *   + coldStress * stressDrainScale - totalColdResistance * resistanceScale   (when cold)
 * Negative resistance adds to the drain. Drain is always >= 0.
 *
 * STRESS:
 * Stress is a 0-1 value that ramps from 0 at the comfort threshold to 1 at the
 * edge of the temperature slider range.
 *
 * ============================================================================
 */

public class HolobiontSimulation : MonoBehaviour
{
    // =========================================================================
    // CREATURE TYPES
    // =========================================================================

    [Header("Creature Types")]
    [SerializeField] private CreatureTypeSO nurturer;
    [SerializeField] private CreatureTypeSO crio;
    [SerializeField] private CreatureTypeSO termo;

    // =========================================================================
    // COMPOSITION
    // =========================================================================

    [Header("Composition")]
    [Tooltip("Maximum total creatures allowed in the holobiont.")]
    [SerializeField] private int maxCreatures = 5;

    private int _nurturerCount;
    private int _crioCount;
    private int _termoCount;

    // =========================================================================
    // ENVIRONMENT
    // =========================================================================

    [Header("Environment")]
    [Tooltip("Temperature range of the slider (symmetric: range = -maxTemp to +maxTemp).")]
    [SerializeField] private float maxTemperature = 50f;

    [Tooltip("Temperature above which heat stress begins.")]
    [SerializeField] private float heatStressThreshold = 10f;

    [Tooltip("Temperature below which cold stress begins (use a negative value).")]
    [SerializeField] private float coldStressThreshold = -10f;

    private float _temperature = 0f;

    // =========================================================================
    // ENERGY / DRAIN SETTINGS
    // =========================================================================

    [Header("Energy & Drain")]
    [Tooltip("Flat energy drain per second for the whole holobiont, regardless of temperature.")]
    [SerializeField] private float baseDrainPerSecond = 0.5f;

    [Tooltip("Maximum stress drain per second when stress is at 1 (peak temperature), before resistance is applied.")]
    [SerializeField] private float maxStressDrainPerSecond = 3f;

    [Tooltip("How much each point of resistance reduces the stress drain per second. " +
             "Negative resistance adds to the drain instead.")]
    [SerializeField] private float resistanceDrainScale = 0.1f;

    // =========================================================================
    // EVENTS
    // =========================================================================

    [Header("Events")]
    [Tooltip("Fired every Update with normalized energy (0-1). Wire to a UI bar or label.")]
    public UnityEvent<float> OnEnergyChanged;

    [Tooltip("Fired when composition changes. Argument is the new total creature count.")]
    public UnityEvent<int> OnCompositionChanged;

    [Tooltip("Fired when composition changes. Argument is the new total heat resistance (can be negative).")]
    public UnityEvent<float> OnHeatResistanceChanged;

    [Tooltip("Fired when composition changes. Argument is the new total cold resistance (can be negative).")]
    public UnityEvent<float> OnColdResistanceChanged;

    [Tooltip("Fired when energy reaches zero.")]
    public UnityEvent OnCollapse;

    // =========================================================================
    // RUNTIME STATE
    // =========================================================================

    private float _currentEnergy;
    private bool _isCollapsed;

    // =========================================================================
    // PUBLIC PROPERTIES
    // =========================================================================

    public int MaxCreatures   => maxCreatures;
    public int NurturerCount  => _nurturerCount;
    public int CrioCount      => _crioCount;
    public int TermoCount     => _termoCount;
    public int TotalCreatures => _nurturerCount + _crioCount + _termoCount;

    /// <summary>Maximum possible energy given current composition.</summary>
    public float TotalMaxEnergy =>
        Count(nurturer, _nurturerCount, t => t.baseEnergy) +
        Count(crio,     _crioCount,     t => t.baseEnergy) +
        Count(termo,    _termoCount,    t => t.baseEnergy);

    /// <summary>Sum of heat resistance across all creatures (can be negative).</summary>
    public float TotalHeatResistance =>
        Count(nurturer, _nurturerCount, t => t.heatResistance) +
        Count(crio,     _crioCount,     t => t.heatResistance) +
        Count(termo,    _termoCount,    t => t.heatResistance);

    /// <summary>Sum of cold resistance across all creatures (can be negative).</summary>
    public float TotalColdResistance =>
        Count(nurturer, _nurturerCount, t => t.coldResistance) +
        Count(crio,     _crioCount,     t => t.coldResistance) +
        Count(termo,    _termoCount,    t => t.coldResistance);

    public float CurrentEnergy   => _currentEnergy;
    public float NormalizedEnergy => TotalMaxEnergy > 0f ? _currentEnergy / TotalMaxEnergy : 0f;
    public float Temperature     => _temperature;

    /// <summary>0-1 heat stress. 0 inside comfort zone, 1 at max temperature.</summary>
    public float HeatStress => _temperature > heatStressThreshold
        ? Mathf.Clamp01((_temperature - heatStressThreshold) / (maxTemperature - heatStressThreshold))
        : 0f;

    /// <summary>0-1 cold stress. 0 inside comfort zone, 1 at min temperature.</summary>
    public float ColdStress => _temperature < coldStressThreshold
        ? Mathf.Clamp01((coldStressThreshold - _temperature) / (maxTemperature - coldStressThreshold))
        : 0f;

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

        float drain = ComputeDrainPerSecond() * Time.deltaTime;
        _currentEnergy = Mathf.Max(_currentEnergy - drain, 0f);

        OnEnergyChanged?.Invoke(NormalizedEnergy);

        if (_currentEnergy <= 0f && !_isCollapsed)
        {
            _isCollapsed = true;
            OnCollapse?.Invoke();
        }
    }

    // =========================================================================
    // PUBLIC SETTERS — wire these to Slider.OnValueChanged in the Inspector
    // =========================================================================

    /// <summary>Wire to the Nurturer slider's OnValueChanged.</summary>
    public void SetNurturerCount(float value)
    {
        _nurturerCount = ClampCount((int)value, _crioCount, _termoCount);
        RefreshComposition();
    }

    /// <summary>Wire to the Crio slider's OnValueChanged.</summary>
    public void SetCrioCount(float value)
    {
        _crioCount = ClampCount((int)value, _nurturerCount, _termoCount);
        RefreshComposition();
    }

    /// <summary>Wire to the Termo slider's OnValueChanged.</summary>
    public void SetTermoCount(float value)
    {
        _termoCount = ClampCount((int)value, _nurturerCount, _crioCount);
        RefreshComposition();
    }

    /// <summary>Wire to the Temperature slider's OnValueChanged.</summary>
    public void SetTemperature(float value)
    {
        _temperature = value;
    }

    /// <summary>Resets current energy to max and re-enables the simulation.</summary>
    public void ResetSimulation()
    {
        _isCollapsed = false;
        _currentEnergy = TotalMaxEnergy;
        OnEnergyChanged?.Invoke(NormalizedEnergy);
    }

    // =========================================================================
    // PRIVATE HELPERS
    // =========================================================================

    private int ClampCount(int requested, int otherA, int otherB)
    {
        int available = Mathf.Max(maxCreatures - otherA - otherB, 0);
        return Mathf.Clamp(requested, 0, available);
    }

    private float ComputeDrainPerSecond()
    {
        if (TotalCreatures == 0) return 0f;

        float heatStress = HeatStress;
        float coldStress = ColdStress;

        float stressDrain = 0f;

        if (heatStress > 0f)
        {
            float raw = heatStress * maxStressDrainPerSecond;
            float reduced = TotalHeatResistance * resistanceDrainScale;
            stressDrain += Mathf.Max(raw - reduced, 0f);
        }

        if (coldStress > 0f)
        {
            float raw = coldStress * maxStressDrainPerSecond;
            float reduced = TotalColdResistance * resistanceDrainScale;
            stressDrain += Mathf.Max(raw - reduced, 0f);
        }

        return baseDrainPerSecond + stressDrain;
    }

    private void RefreshComposition()
    {
        // Reset energy to the new capacity so the effect of composition is immediately visible
        ResetSimulation();
        OnCompositionChanged?.Invoke(TotalCreatures);
        OnHeatResistanceChanged?.Invoke(TotalHeatResistance);
        OnColdResistanceChanged?.Invoke(TotalColdResistance);
    }

    private static float Count(CreatureTypeSO type, int count, System.Func<CreatureTypeSO, float> stat)
    {
        return type != null ? count * stat(type) : 0f;
    }
}
