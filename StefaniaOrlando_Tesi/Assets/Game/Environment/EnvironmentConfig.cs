using UnityEngine;

/*
 * Baseline configuration for the environment's four variables.
 * One asset in the project. Values here are the world's "personality" —
 * ranges and starting points. Drift, events, etc. will be added later.
 *
 * Create via: Right-click in Project → Create → Game → Environment Config
 */

[System.Serializable]
public class EnvironmentVariableConfig
{
    [Tooltip("Starting value at game start.")]
    public float baseValue;

    [Tooltip("Minimum the value can reach.")]
    public float minValue;

    [Tooltip("Maximum the value can reach.")]
    public float maxValue;
}

[CreateAssetMenu(fileName = "EnvironmentConfig", menuName = "Game/Environment Config")]
public class EnvironmentConfig : ScriptableObject
{
    [Header("Temperature  (cold ↔ hot)")]
    public EnvironmentVariableConfig temperature = new EnvironmentVariableConfig
    {
        baseValue = 0f,
        minValue = -50f,
        maxValue = 50f
    };

    [Header("Humidity  (dry ↔ wet)")]
    public EnvironmentVariableConfig humidity = new EnvironmentVariableConfig
    {
        baseValue = 50f,
        minValue = 0f,
        maxValue = 100f
    };

    [Header("Toxicity  (clean ↔ toxic)")]
    public EnvironmentVariableConfig toxicity = new EnvironmentVariableConfig
    {
        baseValue = 0f,
        minValue = 0f,
        maxValue = 100f
    };

    [Header("Light  (dark ↔ bright)")]
    public EnvironmentVariableConfig light = new EnvironmentVariableConfig
    {
        baseValue = 50f,
        minValue = 0f,
        maxValue = 100f
    };
}
