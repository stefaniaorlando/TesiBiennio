using UnityEngine;

/*
 * Owns and exposes the current state of the environment.
 * Views (post-fx, particles, creature affinity lookups, debug HUD)
 * read from this component.
 *
 * For now it only initialises values from the config on enable.
 * Drift, event system, and change notifications will be added later.
 */

public class EnvironmentManager : MonoBehaviour
{
    [SerializeField] private EnvironmentConfig config;

    // Raw values — the single source of truth for environmental state.
    public float Temperature { get; private set; }
    public float Humidity    { get; private set; }
    public float Toxicity    { get; private set; }
    public float Light       { get; private set; }

    // Normalised 0–1 views, useful for post-processing / particle curves.
    public float TemperatureNormalized => Normalize(config.temperature, Temperature);
    public float HumidityNormalized    => Normalize(config.humidity,    Humidity);
    public float ToxicityNormalized    => Normalize(config.toxicity,    Toxicity);
    public float LightNormalized       => Normalize(config.light,       Light);

    public EnvironmentConfig Config => config;

    // Mutators — drivers (drift, events, debug overrides) write through these.
    // Always clamped to the per-variable range from config.
    public void SetTemperature(float v) => Temperature = Clamp(config.temperature, v);
    public void SetHumidity   (float v) => Humidity    = Clamp(config.humidity,    v);
    public void SetToxicity   (float v) => Toxicity    = Clamp(config.toxicity,    v);
    public void SetLight      (float v) => Light       = Clamp(config.light,       v);

    private void OnEnable()
    {
        if (config == null)
        {
            Debug.LogError($"{nameof(EnvironmentManager)} has no {nameof(EnvironmentConfig)} assigned.", this);
            enabled = false;
            return;
        }

        Temperature = config.temperature.baseValue;
        Humidity    = config.humidity.baseValue;
        Toxicity    = config.toxicity.baseValue;
        Light       = config.light.baseValue;
    }

    private static float Normalize(EnvironmentVariableConfig v, float current)
        => Mathf.InverseLerp(v.minValue, v.maxValue, current);

    private static float Clamp(EnvironmentVariableConfig v, float value)
        => Mathf.Clamp(value, v.minValue, v.maxValue);
}
