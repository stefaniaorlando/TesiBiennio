using UnityEngine;

namespace Holobiont
{
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

        [Tooltip("Normalized [0,1] threshold below which the variable is considered in a Low extreme.")]
        [Range(0f, 1f)] public float extremeLowNormalized = 0.1f;

        [Tooltip("Normalized [0,1] threshold above which the variable is considered in a High extreme.")]
        [Range(0f, 1f)] public float extremeHighNormalized = 0.9f;
    }

    [CreateAssetMenu(fileName = "EnvironmentConfig", menuName = "Game/Environment Config")]
    public class EnvironmentConfig : ScriptableObject
    {
#if UNITY_EDITOR
        // Catch authoring mistakes where the low band ends up above the high band — would make the
        // entire range classify as Low and never fire High extremes.
        private void OnValidate()
        {
            ValidateExtremes(temperature, nameof(temperature));
            ValidateExtremes(light,       nameof(light));
            ValidateExtremes(humidity,    nameof(humidity));
            ValidateExtremes(toxicity,    nameof(toxicity));
        }

        private void ValidateExtremes(EnvironmentVariableConfig v, string field)
        {
            if (v != null && v.extremeLowNormalized > v.extremeHighNormalized)
            {
                Debug.LogWarning(
                    $"{name}.{field}: extremeLowNormalized ({v.extremeLowNormalized:F2}) is above extremeHighNormalized ({v.extremeHighNormalized:F2}). " +
                    "Threshold-cross events will misclassify. Swapping.", this);
                (v.extremeLowNormalized, v.extremeHighNormalized) = (v.extremeHighNormalized, v.extremeLowNormalized);
            }
        }
#endif


        [Header("Temperature  (cold ↔ hot)")]
        public EnvironmentVariableConfig temperature = new EnvironmentVariableConfig
        {
            baseValue = 0f,
            minValue = -50f,
            maxValue = 50f
        };

        [Header("Light  (dark ↔ bright)")]
        public EnvironmentVariableConfig light = new EnvironmentVariableConfig
        {
            baseValue = 50f,
            minValue = 0f,
            maxValue = 100f
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
    }
}
