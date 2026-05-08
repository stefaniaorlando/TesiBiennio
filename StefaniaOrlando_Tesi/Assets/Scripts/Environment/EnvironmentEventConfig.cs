using UnityEngine;

namespace Holobiont
{
    /*
     * Defines a single crisis event type — heat wave, toxic bloom, drought, etc.
     *
     * An event is a list of (variable, intensityDelta) effects, all shaped by
     * one shared envelope curve over the event's total lifetime
     * (rampUp + sustain + rampDown). Different effects can target different
     * variables with different magnitudes and signs — e.g. a Drought event
     * raises temperature while lowering humidity in lockstep.
     *
     * The named ramp/sustain durations only set the total length; the curve
     * is what actually shapes the envelope. The default is a soft arch — for
     * exact ADSR behaviour, place curve keyframes at rampUp/total and
     * (rampUp+sustain)/total.
     *
     * Create via: Right-click in Project → Create → Game → Environment Event
     */

    [System.Serializable]
    public class EnvironmentEventEffect
    {
        [Tooltip("Variable(s) this effect targets. If multiple bits are set, the same delta is applied to each.")]
        public EnvironmentVariable affected = EnvironmentVariable.Temp;

        [Tooltip("Peak delta at envelope value 1. Negative pulls the variable down.")]
        public float intensityDelta = 20f;
    }

    [CreateAssetMenu(fileName = "EnvironmentEvent", menuName = "Game/Environment Event")]
    public class EnvironmentEventConfig : ScriptableObject
    {
        [Tooltip("Display/debug name only.")]
        public string eventName = "Event";

        [Tooltip("Weighted-random selection weight inside the event pool. 0 = never picked.")]
        [Min(0f)] public float weight = 1f;

        [Header("Difficulty gating")]
        [Tooltip("Event is only eligible while DifficultyManager.Difficulty >= this. 0 = always available.")]
        [Range(0f, 1f)] public float minDifficulty = 0f;

        [Tooltip("Event is only eligible while DifficultyManager.Difficulty <= this. 1 = always available.")]
        [Range(0f, 1f)] public float maxDifficulty = 1f;

        [Header("Effects")]
        [Tooltip("One or more variable-delta pairs. All effects share the envelope curve and ramp together.")]
        public EnvironmentEventEffect[] effects = new[] { new EnvironmentEventEffect() };

        [Tooltip("Seconds to reach peak.")]
        [Min(0f)] public float rampUpDuration = 3f;

        [Tooltip("Seconds at peak.")]
        [Min(0f)] public float sustainDuration = 5f;

        [Tooltip("Seconds to return to baseline.")]
        [Min(0f)] public float rampDownDuration = 4f;

        [Tooltip("Envelope shape over the full lifetime. x: 0..1 normalised time, y: 0..1 envelope. Multiplied by each effect's intensityDelta.")]
        public AnimationCurve envelopeCurve = MakeArchCurve();

        public float TotalDuration => rampUpDuration + sustainDuration + rampDownDuration;

        /// <summary>The default smoothed-arch envelope shape (0 → 1 → 0). Public so editor tooling can reuse it.</summary>
        public static AnimationCurve MakeArchCurve()
        {
            var c = new AnimationCurve(
                new Keyframe(0.00f, 0f),
                new Keyframe(0.50f, 1f),
                new Keyframe(1.00f, 0f)
            );
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            return c;
        }
    }
}
