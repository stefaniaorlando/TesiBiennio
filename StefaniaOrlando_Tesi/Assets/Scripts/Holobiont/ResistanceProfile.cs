using UnityEngine;

namespace Holobiont
{
    /*
     * Per-axis tolerance widths, summed from bonded scudo contributions in
     * HolobiontManager.RecalculateResistance. Each scudo's
     * (baseResistanceContribution × efficiency) is split across the four axes
     * weighted by its affinity specialization (extreme axes get more, neutral
     * axes get less), then sorted and decayed per axis so spreading scudos
     * across different specialty axes avoids the stacking-decay penalty.
     *
     * Tolerances are unbounded above: a stack of well-affined scudos can drive
     * an axis past 1, completely cancelling that axis's mismatch cost.
     *
     * Mismatch cost per axis: max(0, |envAxis − 0.5| − toleranceAxis).
     * The center is pinned to 0.5 (a neutral midpoint) until a more
     * sophisticated centering rule is required.
     *
     * Field order matches EnvironmentManager.AsNormalizedVector:
     * Temperature, Light, Humidity, Toxicity.
     */
    [System.Serializable]
    public struct ResistanceProfile
    {
        public float temperatureTolerance;
        public float lightTolerance;
        public float humidityTolerance;
        public float toxicityTolerance;

        /// <summary>Sum of per-axis mismatch costs against the live environment. Returns 0 when env is null.</summary>
        public float GetMismatchCost(EnvironmentManager env)
        {
            if (!env) return 0f;
            float t = Mathf.Max(0f, Mathf.Abs(env.TemperatureNormalized - 0.5f) - temperatureTolerance);
            float l = Mathf.Max(0f, Mathf.Abs(env.LightNormalized       - 0.5f) - lightTolerance);
            float h = Mathf.Max(0f, Mathf.Abs(env.HumidityNormalized    - 0.5f) - humidityTolerance);
            float x = Mathf.Max(0f, Mathf.Abs(env.ToxicityNormalized    - 0.5f) - toxicityTolerance);
            return t + l + h + x;
        }
    }
}
