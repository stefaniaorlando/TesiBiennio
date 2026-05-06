using System.Collections.Generic;
using UnityEngine;

namespace Holobiont
{
    /*
     * Periodically instantiates Creature prefabs at a random point inside a
     * separate BoxCollider2D's bounds. Picks one of the three creature
     * categories (Nutrici / Scudo / Hub) by authored weight on SpawnerConfig.
     * Enforces a soft cap on simultaneously alive unbound creatures by lazily
     * pruning tracked references.
     *
     * Tick is on Update, gated by GameClock — spawning pauses with the rest of
     * the simulation. No physics here; the Creature owns its own Rigidbody2D
     * and reacts to the flow field through ApplyForce.
     *
     * TODO:
     *   - Env-coupled bias on top of authored weights (toxicity → scudo, etc.)
     *   - Spawn-position bias upstream of the flow field
     *   - Crisis-event coupling / delayed blooms
     */

    [DisallowMultipleComponent]
    public class CreatureSpawner : MonoBehaviour
    {
        // ----- Dependencies (assigned in inspector) -----
        [Header("Dependencies")]
        [Tooltip("Pacing + pool config.")]
        [SerializeField] private SpawnerConfig config;

        [Tooltip("BoxCollider2D whose bounds define the spawn area. Place on a separate GameObject so it's easy to inspect and resize in the scene view.")]
        [SerializeField] private BoxCollider2D spawnArea;

        [Tooltip("Environment used by Creature.Initialize for affinity generation.")]
        [SerializeField] private EnvironmentManager environment;

        [Tooltip("Optional parent transform for spawned creatures. Leave empty for scene root.")]
        [SerializeField] private Transform spawnParent;

        // ----- Debug -----
        [Header("Debug")]
        [Tooltip("Currently tracked unbound creatures spawned by this spawner.")]
        [SerializeField, ReadOnly] private int activeUnboundCount;

        [Tooltip("Seconds accumulated since the last spawn attempt.")]
        [SerializeField, ReadOnly] private float timeSinceLastSpawn;

        // ----- Internal state -----
        private readonly HashSet<Creature> tracked = new HashSet<Creature>();
        private readonly List<Creature> pruneBuffer = new List<Creature>(16);

        // ----- Lifecycle -----
        private void OnEnable()
        {
            if (!config)
            {
                Debug.LogError($"{nameof(CreatureSpawner)} on {name}: missing {nameof(SpawnerConfig)}.", this);
                enabled = false;
                return;
            }
            if (!spawnArea)
            {
                Debug.LogError($"{nameof(CreatureSpawner)} on {name}: missing {nameof(spawnArea)} ({nameof(BoxCollider2D)}).", this);
                enabled = false;
                return;
            }
            if (!environment)
            {
                Debug.LogError($"{nameof(CreatureSpawner)} on {name}: missing {nameof(EnvironmentManager)}.", this);
                enabled = false;
                return;
            }
            if (!config.nutriciConfig && !config.scudoConfig && !config.hubConfig)
            {
                Debug.LogWarning($"{nameof(CreatureSpawner)} on {name}: all creature slots empty. Nothing will spawn.", this);
            }
        }

        private void Update()
        {
            var clock = GameClock.Instance;
            float dt = clock ? clock.DeltaTime : Time.deltaTime;

            timeSinceLastSpawn += dt;
            if (timeSinceLastSpawn < config.spawnInterval) return;
            timeSinceLastSpawn = 0f;

            TrySpawn();
        }

        // ----- Public API -----
        [ContextMenu("Spawn Now")]
        public void SpawnNow()
        {
            if (!isActiveAndEnabled) return;
            TrySpawn();
        }

        // ----- Spawn -----
        private void TrySpawn()
        {
            PruneTracked();
            if (activeUnboundCount >= config.maxAlive) return;

            CreatureConfig pick = PickWeighted();
            if (!pick) return;
            if (!pick.prefab)
            {
                Debug.LogWarning($"{nameof(CreatureSpawner)} on {name}: {pick.name} has no prefab. Skipping.", this);
                return;
            }

            Vector3 pos = RandomPointInBounds(spawnArea.bounds);
            GameObject go = Instantiate(pick.prefab, pos, Quaternion.identity, spawnParent);

            var creature = go.GetComponent<Creature>();
            if (!creature)
            {
                Debug.LogError($"{nameof(CreatureSpawner)}: prefab {pick.prefab.name} has no {nameof(Creature)} component.", this);
                Destroy(go);
                return;
            }

            creature.Initialize(pick, environment);
            tracked.Add(creature);
            activeUnboundCount++;
        }

        // Weighted pick across the three category slots. Null configs and zero
        // weights are skipped naturally. Returns null when nothing is eligible.
        private CreatureConfig PickWeighted()
        {
            float wn = config.nutriciConfig ? Mathf.Max(0f, config.nutriciWeight) : 0f;
            float ws = config.scudoConfig   ? Mathf.Max(0f, config.scudoWeight)   : 0f;
            float wh = config.hubConfig     ? Mathf.Max(0f, config.hubWeight)     : 0f;
            float total = wn + ws + wh;
            if (total <= 0f) return null;

            float r = Random.Range(0f, total);
            if (r < wn)            return config.nutriciConfig;
            if (r < wn + ws)       return config.scudoConfig;
            return config.hubConfig;
        }

        private static Vector3 RandomPointInBounds(Bounds b)
        {
            return new Vector3(
                Random.Range(b.min.x, b.max.x),
                Random.Range(b.min.y, b.max.y),
                b.center.z);
        }

        // ----- Tracking -----
        // Lazy prune: drop destroyed creatures and ones that have transitioned to Bound.
        // Cheap at small caps; revisit if pool sizes grow.
        private void PruneTracked()
        {
            pruneBuffer.Clear();
            int alive = 0;
            foreach (var c in tracked)
            {
                if (!c || c.Status != BondStatus.Unbound) pruneBuffer.Add(c);
                else alive++;
            }
            for (int i = 0; i < pruneBuffer.Count; i++) tracked.Remove(pruneBuffer[i]);
            activeUnboundCount = alive;
        }
    }
}
