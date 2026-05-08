using UnityEngine;

namespace Holobiont
{
    /*
     * Pacing baseline and event pool for the environment event spawner.
     * Drop one of these into EnvironmentEventSystem.
     *
     * The system picks a random interval in [minInterval, maxInterval] (scaled
     * down by DifficultyManager.EventFrequencyMul at runtime), waits, then
     * weighted-randomly selects an eligible event from eventPool. Each event
     * carries its own weight and difficulty band.
     *
     * Difficulty progression and the response curves live on DifficultyConfig.
     * Events are one-at-a-time; concurrency is not configurable.
     *
     * Create via: Right-click in Project → Create → Game → Environment Event Schedule
     */

    [CreateAssetMenu(fileName = "EnvironmentEventSchedule", menuName = "Game/Environment Event Schedule")]
    public class EnvironmentEventScheduleConfig : ScriptableObject
    {
        [Header("Pacing")]
        [Tooltip("Baseline minimum seconds between events. Divided by DifficultyManager.EventFrequencyMul at runtime.")]
        [Min(0f)] public float minInterval = 15f;

        [Tooltip("Baseline maximum seconds between events. Divided by DifficultyManager.EventFrequencyMul at runtime.")]
        [Min(0f)] public float maxInterval = 30f;

        [Header("Pool")]
        [Tooltip("Available events. Each event owns its own selection weight and difficulty band.")]
        public EnvironmentEventConfig[] eventPool;
    }
}
