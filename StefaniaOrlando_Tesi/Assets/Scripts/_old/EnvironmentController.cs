using UnityEngine;

namespace HolobiontLegacy
{

/*
 * ============================================================================
 * ENVIRONMENT CONTROLLER
 * ============================================================================
 *
 * WHAT THIS DOES:
 * Automatically drives the four environmental variables of HolobiontSimulation
 * (temperature, humidity, light, toxicity) with smooth, linear random changes.
 *
 * HOW IT WORKS:
 * Each variable has a current value that moves linearly (MoveTowards) toward a
 * randomly chosen target within its configured range. When the target is reached,
 * the variable optionally holds for a random duration, then picks a new target
 * and a new random speed. This guarantees no sudden jumps — the rate of change
 * is always bounded by the configured min/max speed (units per second).
 *
 * HOW TO SET UP:
 * 1. Add this component to any GameObject in the scene.
 * 2. Drag HolobiontSimulation into the Simulation slot.
 * 3. Adjust each variable's range, speed, and hold-time settings in the Inspector.
 *    Ranges should match the max values configured in HolobiontSimulation.
 *
 * NOTE: While this component is active it will override any manual slider input
 * for the environment variables. Disable the component to restore manual control.
 *
 * ============================================================================
 */

[System.Serializable]
public class EnvVariable
{
    [Tooltip("Minimum value the variable can reach.")]
    public float minValue;

    [Tooltip("Maximum value the variable can reach.")]
    public float maxValue;

    [Tooltip("Starting value when the simulation begins.")]
    public float startValue;

    [Space]
    [Tooltip("Slowest possible rate of change (units per second).")]
    [Min(0.01f)] public float minChangeSpeed = 1f;

    [Tooltip("Fastest possible rate of change (units per second).")]
    [Min(0.01f)] public float maxChangeSpeed = 5f;

    [Space]
    [Tooltip("Minimum time (seconds) to hold at the target before picking a new one.")]
    [Min(0f)] public float minHoldTime = 0f;

    [Tooltip("Maximum time (seconds) to hold at the target before picking a new one.")]
    [Min(0f)] public float maxHoldTime = 3f;

    // Runtime state (not shown in Inspector)
    [HideInInspector] public float current;
    [HideInInspector] public float target;
    [HideInInspector] public float speed;
    [HideInInspector] public float holdTimer;
}

public class EnvironmentController : MonoBehaviour
{
    // =========================================================================
    // REFERENCES
    // =========================================================================

    [Header("Simulation")]
    [SerializeField] private HolobiontSimulation simulation;

    // =========================================================================
    // VARIABLE SETTINGS
    // =========================================================================

    [Header("Temperature  (°C — matches HolobiontSimulation ±maxTemperature)")]
    [SerializeField] private EnvVariable temperature = new EnvVariable
    {
        minValue = -50f, maxValue = 50f, startValue = 0f,
        minChangeSpeed = 1f, maxChangeSpeed = 8f,
        minHoldTime = 1f, maxHoldTime = 4f
    };

    [Header("Humidity  (% — 0 to maxHumidity)")]
    [SerializeField] private EnvVariable humidity = new EnvVariable
    {
        minValue = 0f, maxValue = 100f, startValue = 50f,
        minChangeSpeed = 2f, maxChangeSpeed = 10f,
        minHoldTime = 1f, maxHoldTime = 4f
    };

    [Header("Light  (lux — 0 to maxLight)")]
    [SerializeField] private EnvVariable light = new EnvVariable
    {
        minValue = 0f, maxValue = 100f, startValue = 50f,
        minChangeSpeed = 2f, maxChangeSpeed = 10f,
        minHoldTime = 1f, maxHoldTime = 4f
    };

    [Header("Toxicity  (0 to maxToxicity)")]
    [SerializeField] private EnvVariable toxicity = new EnvVariable
    {
        minValue = 0f, maxValue = 100f, startValue = 0f,
        minChangeSpeed = 1f, maxChangeSpeed = 6f,
        minHoldTime = 2f, maxHoldTime = 6f
    };

    // =========================================================================
    // PUBLIC ACCESSORS (for SimulationUI slider sync)
    // =========================================================================

    public float Temperature => temperature.current;
    public float Humidity    => humidity.current;
    public float Light       => light.current;
    public float Toxicity    => toxicity.current;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    private void OnEnable()
    {
        Init(temperature);
        Init(humidity);
        Init(light);
        Init(toxicity);
    }

    private void Update()
    {
        Step(temperature);
        Step(humidity);
        Step(light);
        Step(toxicity);

        simulation.SetTemperature(temperature.current);
        simulation.SetHumidity(humidity.current);
        simulation.SetLight(light.current);
        simulation.SetToxicity(toxicity.current);
    }

    // =========================================================================
    // PRIVATE HELPERS
    // =========================================================================

    private void Init(EnvVariable v)
    {
        v.current    = Mathf.Clamp(v.startValue, v.minValue, v.maxValue);
        v.target     = PickTarget(v);
        v.speed      = PickSpeed(v);
        v.holdTimer  = 0f;
    }

    private void Step(EnvVariable v)
    {
        if (v.holdTimer > 0f)
        {
            v.holdTimer -= Time.deltaTime;
            return;
        }

        v.current = Mathf.MoveTowards(v.current, v.target, v.speed * Time.deltaTime);

        if (v.current == v.target)
        {
            v.holdTimer = Random.Range(v.minHoldTime, v.maxHoldTime);
            v.target    = PickTarget(v);
            v.speed     = PickSpeed(v);
        }
    }

    private static float PickTarget(EnvVariable v) => Random.Range(v.minValue, v.maxValue);
    private static float PickSpeed(EnvVariable v)  => Random.Range(v.minChangeSpeed, v.maxChangeSpeed);
}

}
