using UnityEngine;

namespace Holobiont
{
    /*
     * A point in the four-dimensional environmental affinity space (0..1 per axis).
     * Set at spawn from current environment + per-axis scatter, immutable afterwards.
     * Mismatch with the live EnvironmentManager values produces stress.
     */

    [System.Serializable]
    public struct CreatureAffinityData
    {
        [Range(0f, 1f)] public float idealTemperature;
        [Range(0f, 1f)] public float idealHumidity;
        [Range(0f, 1f)] public float idealToxicity;
        [Range(0f, 1f)] public float idealLight;

        // Axis order must match EnvironmentManager.AsNormalizedVector (Temp, Light, Humidity, Toxicity) — Creature.UpdateAffinityAndStress compares them directly.
        public Vector4 AsVector => new Vector4(idealTemperature, idealLight, idealHumidity, idealToxicity);
    }
}
