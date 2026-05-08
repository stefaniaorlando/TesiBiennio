using UnityEngine;

namespace Holobiont
{
    /*
     * Authoring asset for the global difficulty curve.
     * One asset per "difficulty profile" — drop into DifficultyManager's slot.
     *
     * Difficulty advances from initialDifficulty up to 1 over timeToMax seconds
     * of GameClock time, shaped by progressEasing. The three response curves
     * map the resulting 0..1 difficulty to per-system multipliers (intensity,
     * frequency, drift amplitude). Default curves are flat 1× so installing the
     * manager changes nothing until curves are authored.
     *
     * Create via: Right-click in Project → Create → Game → Difficulty Config
     */

    [CreateAssetMenu(fileName = "DifficultyConfig", menuName = "Game/Difficulty Config")]
    public class DifficultyConfig : ScriptableObject
    {
        [Header("Progression")]
        [Tooltip("Difficulty value at game start. 0 = full ramp from zero. >0 = floor for early game.")]
        [Range(0f, 1f)] public float initialDifficulty = 0f;

        [Tooltip("Seconds of GameClock time to reach max difficulty (1). Difficulty plateaus at 1 afterwards.")]
        [Min(1f)] public float timeToMax = 600f;

        [Tooltip("Shapes how progress moves between initialDifficulty and 1. x: linear time 0..1, y: eased 0..1.")]
        public AnimationCurve progressEasing = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Response curves  (x: difficulty 0..1, y: multiplier)")]
        [Tooltip("Multiplies a newly-spawned event's intensity. Snapshotted at spawn time.")]
        public AnimationCurve eventIntensityMul = AnimationCurve.Constant(0f, 1f, 1f);

        [Tooltip("Multiplies event spawn frequency. Higher = shorter wait between events.")]
        public AnimationCurve eventFrequencyMul = AnimationCurve.Constant(0f, 1f, 1f);

        [Tooltip("Multiplies the per-frame drift curve output (not jitter). Higher = wider swings.")]
        public AnimationCurve driftAmplitudeMul = AnimationCurve.Constant(0f, 1f, 1f);
    }
}
