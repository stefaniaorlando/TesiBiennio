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
     * Placement: spawn points are picked along the BoxCollider2D's perimeter
     * with bias toward edges where the flow field is blowing inward (so
     * creatures drift naturally across the play area). Falls back to uniform
     * perimeter when no flow field is present or flow is calm/tangential.
     * Note: rotated BoxCollider2Ds are treated as their AABB.
     *
     * TODO:
     *   - Env-coupled bias on top of authored weights (toxicity → scudo, etc.)
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

        // ----- Placement scratch (reused per spawn) -----
        private Vector2[] perimeterPoints;
        private float[] perimeterScores;

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

        /// <summary>Destroy every still-tracked unbound creature and reset the spawn timer. Bonded creatures are the holobiont's responsibility — we skip them here even if they're still in `tracked` (PruneTracked is lazy).</summary>
        [ContextMenu("Reset Session State")]
        public void ResetSessionState()
        {
            foreach (var c in tracked)
            {
                // Gate on Unbound: a creature that bonded between PruneTracked passes
                // is still in `tracked` but already owned by the holobiont, which has
                // its own destroy path in HolobiontManager.ResetSessionState.
                if (c && c.Status == BondStatus.Unbound) Destroy(c.gameObject);
            }
            tracked.Clear();
            activeUnboundCount = 0;
            timeSinceLastSpawn = 0f;
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

            Vector3 pos = PickUpstreamSpawnPoint(spawnArea.bounds);
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

        // Sample N perimeter points; score each by how strongly flow points
        // inward across that edge; weighted-pick. No flow field or all-zero
        // scores → uniform perimeter random.
        private Vector3 PickUpstreamSpawnPoint(Bounds bounds)
        {
            int n = Mathf.Max(1, config.upstreamSampleCount);
            EnsureBuffers(n);

            var ff = FlowField.Instance;
            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                RandomPerimeterPoint(bounds, out Vector2 p, out Vector2 outward);
                perimeterPoints[i] = p;
                float s = 0f;
                if (ff)
                {
                    Vector2 flow = ff.GetFlowAtPosition(p);
                    s = Mathf.Max(0f, Vector2.Dot(flow, -outward));
                }
                perimeterScores[i] = s;
                total += s;
            }

            Vector2 picked;
            if (total > 0f)
            {
                float r = Random.Range(0f, total);
                float acc = 0f;
                int idx = n - 1;
                for (int i = 0; i < n; i++)
                {
                    acc += perimeterScores[i];
                    if (r <= acc) { idx = i; break; }
                }
                picked = perimeterPoints[idx];
            }
            else
            {
                RandomPerimeterPoint(bounds, out picked, out _);
            }

            return new Vector3(picked.x, picked.y, bounds.center.z);
        }

        private void EnsureBuffers(int n)
        {
            if (perimeterPoints == null || perimeterPoints.Length < n)
            {
                perimeterPoints = new Vector2[n];
                perimeterScores = new float[n];
            }
        }

        // Uniformly samples a point on the AABB perimeter and returns the
        // outward unit normal at that point.
        private static void RandomPerimeterPoint(Bounds b, out Vector2 point, out Vector2 outwardNormal)
        {
            float w = b.size.x;
            float h = b.size.y;
            float perimeter = 2f * (w + h);
            float t = Random.Range(0f, perimeter);

            if (t < w)            { point = new Vector2(b.min.x + t,           b.min.y);      outwardNormal = new Vector2(0f, -1f); return; }
            t -= w;
            if (t < h)            { point = new Vector2(b.max.x,               b.min.y + t);  outwardNormal = new Vector2(1f,  0f); return; }
            t -= h;
            if (t < w)            { point = new Vector2(b.max.x - t,           b.max.y);      outwardNormal = new Vector2(0f,  1f); return; }
            t -= h;
            point = new Vector2(b.min.x, b.max.y - t);
            outwardNormal = new Vector2(-1f, 0f);
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
