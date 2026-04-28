using UnityEngine;

/*
 * Drives the four environmental variables over time.
 * The actual drift parameters live in the EnvironmentDriftProfile asset
 * referenced below — this component just samples it each frame and writes
 * absolute values (base + drift) through EnvironmentManager's setters.
 *
 * Swap profiles at runtime by assigning a new one to Profile.
 *
 * Runs slightly before default execution order so future drivers (events,
 * debug overrides) can compose on top of drift in the same frame.
 */

[RequireComponent(typeof(EnvironmentManager))]
[DefaultExecutionOrder(-50)]
public class EnvironmentDrift : MonoBehaviour
{
    [SerializeField] private EnvironmentDriftProfile profile;

    public EnvironmentDriftProfile Profile { get => profile; set => profile = value; }

    private EnvironmentManager env;

    private void Awake() => env = GetComponent<EnvironmentManager>();

    private void Update()
    {
        if (profile == null) return;
        var clock = GameClock.Instance;
        if (clock == null) return;

        float t = clock.Time;
        var cfg = env.Config;

        if (profile.temperature.enabled) env.SetTemperature(cfg.temperature.baseValue + Sample(profile.temperature, t));
        if (profile.humidity.enabled)    env.SetHumidity   (cfg.humidity.baseValue    + Sample(profile.humidity,    t));
        if (profile.toxicity.enabled)    env.SetToxicity   (cfg.toxicity.baseValue    + Sample(profile.toxicity,    t));
        if (profile.light.enabled)       env.SetLight      (cfg.light.baseValue       + Sample(profile.light,       t));
    }

    private static float Sample(VariableDrift d, float t)
    {
        float u     = Mathf.Repeat(t / d.period + d.phaseOffset, 1f);
        float shape = d.curve.Evaluate(u) * d.amplitude;

        float jitter = 0f;
        if (d.jitterAmplitude > 0f && d.jitterFrequency > 0f)
        {
            float seedOffset = d.jitterSeed * 1.7320508f; // irrational stride per seed
            float n = Mathf.PerlinNoise(t * d.jitterFrequency + seedOffset, seedOffset);
            jitter = (n * 2f - 1f) * d.jitterAmplitude;
        }

        return shape + jitter;
    }
}
