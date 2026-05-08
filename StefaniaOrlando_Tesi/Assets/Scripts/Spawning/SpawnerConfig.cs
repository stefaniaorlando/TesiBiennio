using UnityEngine;

namespace Holobiont
{
    /*
     * Pacing + pool authoring for CreatureSpawner. One slot per creature category
     * (Nutrici / Scudo / Hub) with a paired weight. Set a weight to 0 to disable
     * a type. All zero → spawning paused.
     *
     * TODO:
     *   - Env-coupled bias (e.g. toxicity → more scudo) — multiplies these base weights
     *   - Bloom / event-coupled spawn bursts
     */

    [CreateAssetMenu(fileName = "SpawnerConfig", menuName = "Game/Spawner Config")]
    public class SpawnerConfig : ScriptableObject
    {
        // ----- Pacing -----
        [Header("Pacing")]
        [Tooltip("Seconds between spawn attempts. Pause-aware via GameClock.")]
        [Min(0.01f)]
        public float spawnInterval = 2f;

        [Tooltip("Max simultaneously alive unbound creatures from this spawner. Bound creatures don't count.")]
        [Min(0)]
        public int maxAlive = 20;

        // ----- Placement -----
        [Header("Placement")]
        [Tooltip("How many perimeter points to sample when picking an upstream-edge spawn location. Higher = smoother bias toward the windward edge, more flow-field samples per spawn.")]
        [Min(1)]
        public int upstreamSampleCount = 16;

        [Tooltip("Inset the spawn AABB inward before picking a perimeter point so creatures don't appear exactly on the rim.")]
        public bool useSpawnInset = true;

        [Tooltip("World units to shrink each side of the spawn AABB by. 0 = on the rim.")]
        [Min(0f)]
        public float spawnInset = 0.5f;

        [Tooltip("Apply an initial inward velocity at spawn so creatures don't loiter on the rim while waiting for flow.")]
        public bool useSpawnInwardKick = true;

        [Tooltip("Initial inward speed (world units / sec) applied along the reversed outward normal of the chosen perimeter edge.")]
        [Min(0f)]
        public float spawnInwardSpeed = 1.5f;

        // ----- Nutrici -----
        [Header("Nutrici")]
        [Tooltip("Nutrici config asset. Leave empty to disable Nutrici spawns.")]
        public NutriciConfig nutriciConfig;

        [Tooltip("Relative weight for Nutrici picks. 0 disables.")]
        [Min(0f)]
        public float nutriciWeight = 1f;

        // ----- Scudo -----
        [Header("Scudo")]
        [Tooltip("Scudo config asset. Leave empty to disable Scudo spawns.")]
        public ScudoConfig scudoConfig;

        [Tooltip("Relative weight for Scudo picks. 0 disables.")]
        [Min(0f)]
        public float scudoWeight = 1f;

        // ----- Hub -----
        [Header("Hub")]
        [Tooltip("Hub config asset. Leave empty to disable Hub spawns.")]
        public HubConfig hubConfig;

        [Tooltip("Relative weight for Hub picks. 0 disables.")]
        [Min(0f)]
        public float hubWeight = 1f;
    }
}
