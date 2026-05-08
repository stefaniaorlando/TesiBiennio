using UnityEngine;

namespace HolobiontLegacy
{
    /// <summary>
    /// Minimal placeholder for the environment system. Holds four normalized
    /// (0–1) environmental values that drive creature affinity stress.
    ///
    /// Will be replaced/expanded when the environment system is built per
    /// ENV_SYSTEM_DESIGN.md (drift curves, events, raw + normalized values,
    /// change events, EnvironmentManager owner). Keep the surface used by
    /// CreatureSimulation small so the swap is mechanical: AsNormalizedVector
    /// + the four 0–1 properties.
    /// </summary>
    public class EnvironmentState
    {
        [Range(0f, 1f)] public float Temperature;
        [Range(0f, 1f)] public float Humidity;
        [Range(0f, 1f)] public float Toxicity;
        [Range(0f, 1f)] public float Light;

        public Vector4 AsNormalizedVector =>
            new Vector4(Temperature, Humidity, Toxicity, Light);
    }
}
