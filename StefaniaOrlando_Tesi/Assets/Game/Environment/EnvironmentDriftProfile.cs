using UnityEngine;

/*
 * A complete drift configuration — one "world rhythm" — packaged as an asset.
 * Author multiple profiles (Calm, Volatile, Hostile, etc.) and drop the one
 * you want into EnvironmentDrift's Profile slot. Profiles are also runtime-swappable.
 *
 * Create via: Right-click in Project → Create → Game → Environment Drift Profile
 */

[System.Serializable]
public class VariableDrift
{
    [Tooltip("Disable to freeze this variable at its base value.")]
    public bool enabled = true;

    [Tooltip("Shape of one full drift cycle, sampled cyclically. Author in roughly -1..+1; output multiplied by amplitude.")]
    public AnimationCurve curve = MakeSineLikeCurve();

    [Tooltip("Seconds for one full pass through the curve.")]
    [Min(0.01f)] public float period = 60f;

    [Tooltip("How far the curve output swings the variable around its base value.")]
    public float amplitude = 10f;

    [Tooltip("Starting position along the curve (0..1). Use to desync variables.")]
    [Range(0f, 1f)] public float phaseOffset = 0f;

    [Header("Jitter (Perlin, deterministic)")]
    [Tooltip("Per-variable seed. Same seed → same jitter trajectory.")]
    public int jitterSeed = 0;

    [Tooltip("Cycles per second of the jitter sample.")]
    [Min(0f)] public float jitterFrequency = 0.3f;

    [Tooltip("Magnitude of the jitter (same units as amplitude). 0 disables.")]
    [Min(0f)] public float jitterAmplitude = 0f;

    public static AnimationCurve MakeSineLikeCurve()
    {
        var c = new AnimationCurve(
            new Keyframe(0.00f,  0f),
            new Keyframe(0.25f,  1f),
            new Keyframe(0.50f,  0f),
            new Keyframe(0.75f, -1f),
            new Keyframe(1.00f,  0f)
        );
        for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
        return c;
    }
}

[CreateAssetMenu(fileName = "DriftProfile", menuName = "Game/Environment Drift Profile")]
public class EnvironmentDriftProfile : ScriptableObject
{
    [Header("Temperature")]
    public VariableDrift temperature = new VariableDrift
        { period = 60f, amplitude = 25f, phaseOffset = 0.00f, jitterSeed = 1 };

    [Header("Humidity")]
    public VariableDrift humidity = new VariableDrift
        { period = 80f, amplitude = 30f, phaseOffset = 0.25f, jitterSeed = 2 };

    [Header("Toxicity")]
    public VariableDrift toxicity = new VariableDrift
        { period = 90f, amplitude = 25f, phaseOffset = 0.50f, jitterSeed = 3 };

    [Header("Light")]
    public VariableDrift light = new VariableDrift
        { period = 70f, amplitude = 30f, phaseOffset = 0.75f, jitterSeed = 4 };
}
