using System.Collections.Generic;
using UnityEngine;

namespace Holobiont
{
    /*
     * CPU side of the symbiosis-tendrils visual. Sibling to HolobiontManager —
     * reads the bonded-creature list, computes K-nearest neighbors, and drives
     * a pool of SpriteRenderer children (one per active pair). The wavering,
     * breath-modulated, stress-fraying stylization is all in the shader; this
     * component only does adjacency + transform/color drives.
     *
     * Renderer choice: SpriteRenderer per pair (pooled). Same primitive as
     * creatures and the bg, so the whole visual layer is uniformly URP-2D
     * sprite-based and integrates with sorting layers automatically.
     *
     * Per-tendril transform:
     *   position    — midpoint of A and B
     *   rotation    — Z-rotated so local +X aligns with A→B (so local +Y is
     *                 the world-space perpendicular, which the shader reads
     *                 off UNITY_MATRIX_M for noise displacement)
     *   localScale  — (lengthAB, ribbonWidth, 1)
     *   color.r     — pair stress in [0,1]
     *
     * Why per-frame rebuild: bound creatures spring around the holobiont
     * center with breath-modulated radius (HolobiontManager.UpdateBoundCreaturePositions),
     * so adjacency genuinely changes frame-to-frame. For tens of creatures the
     * naive O(N²) K-NN scan is well under a millisecond.
     *
     * Pool grows on demand and never shrinks — extra entries are deactivated
     * rather than destroyed, so a temporary spike doesn't churn GC.
     */
    [DisallowMultipleComponent]
    public class HolobiontTendrilNetwork : MonoBehaviour
    {
        // ----- Sources -----
        [Header("Sources")]
        [Tooltip("Holobiont whose bonded creatures define the network. Auto-resolved from siblings on Reset.")]
        [SerializeField] private HolobiontManager manager;

        // ----- Rendering -----
        [Header("Rendering")]
        [Tooltip("Material using the Holobiont/SymbiosisTendrils shader. Shared across every pooled tendril SpriteRenderer.")]
        [SerializeField] private Material tendrilMaterial;

        [Tooltip("1×1 white square sprite. The shader composes everything from UVs and the SpriteRenderer.color — sprite texels are not sampled.")]
        [SerializeField] private Sprite tendrilSprite;

        [Tooltip("Sorting layer for the pooled SpriteRenderers — typically below the creatures' layer so tendrils render behind them.")]
        [SerializeField] private string sortingLayerName = "Default";

        [Tooltip("Order in layer for the pooled SpriteRenderers. Negative values draw beneath sprites with order 0.")]
        [SerializeField] private int sortingOrder = -1;

        // ----- Tuning -----
        [Header("Network")]
        [Tooltip("How many nearest neighbors each creature connects to. K=2 is a clean network; K=3 starts to look like spaghetti.")]
        [Range(1, 4)]
        [SerializeField] private int kNeighbors = 2;

        [Tooltip("World-space ribbon width (sprite local Y scale).")]
        [Min(0.001f)]
        [SerializeField] private float ribbonWidth = 0.18f;

        // ----- Debug -----
        [Header("Debug")]
        [Tooltip("Mirror of pairs emitted this frame.")]
        [SerializeField, ReadOnly] private int pairCount;

        [Tooltip("Mirror of how many SpriteRenderer slots the pool currently holds.")]
        [SerializeField, ReadOnly] private int poolCapacity;

        // ----- Internal state -----
        private bool initialized;
        private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>(32);

        private readonly List<(int idx, float distSq)> nearestBuf = new List<(int, float)>(16);
        private readonly HashSet<long> emittedPairs = new HashSet<long>();
        private readonly List<(int a, int b, float pairStress)> pairs = new List<(int, int, float)>(64);

        // ----- Lifecycle -----
        private void Reset()
        {
            manager = GetComponent<HolobiontManager>();
        }

        private void OnEnable()
        {
            if (!manager)
            {
                Debug.LogError($"{nameof(HolobiontTendrilNetwork)}: missing {nameof(HolobiontManager)} reference.", this);
                enabled = false;
                return;
            }

            if (!tendrilMaterial)
            {
                Debug.LogError($"{nameof(HolobiontTendrilNetwork)}: missing tendril material (assign Holobiont/SymbiosisTendrils).", this);
                enabled = false;
                return;
            }

            if (!tendrilSprite)
            {
                Debug.LogError($"{nameof(HolobiontTendrilNetwork)}: missing tendril sprite (assign a 1x1 white square).", this);
                enabled = false;
                return;
            }

            initialized = true;
        }

        private void OnDisable()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i]) pool[i].gameObject.SetActive(false);
            }
            initialized = false;
        }

        private void LateUpdate()
        {
            if (!initialized) return;

            var bonded = manager.State.BondedCreatures;
            if (bonded.Count < 2)
            {
                DeactivateAll();
                pairCount = 0;
                return;
            }

            BuildAdjacency(bonded);
            DrivePool(bonded);

            pairCount    = pairs.Count;
            poolCapacity = pool.Count;
        }

        // ----- Private — adjacency -----
        private void BuildAdjacency(List<Creature> bonded)
        {
            pairs.Clear();
            emittedPairs.Clear();

            int n = bonded.Count;
            int k = Mathf.Min(kNeighbors, n - 1);
            if (k <= 0) return;

            for (int i = 0; i < n; i++)
            {
                var ci = bonded[i];
                if (!ci) continue;

                nearestBuf.Clear();
                Vector2 pi = ci.transform.position;

                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    var cj = bonded[j];
                    if (!cj) continue;

                    Vector2 pj = cj.transform.position;
                    nearestBuf.Add((j, (pi - pj).sqrMagnitude));
                }

                SortNearestAscending();

                int take = Mathf.Min(k, nearestBuf.Count);
                for (int x = 0; x < take; x++)
                {
                    int j = nearestBuf[x].idx;

                    int low  = i < j ? i : j;
                    int high = i < j ? j : i;
                    long key = ((long)low << 32) | (uint)high;
                    if (!emittedPairs.Add(key)) continue;

                    float pairStress = (bonded[low].Stress + bonded[high].Stress) * 0.5f;
                    pairs.Add((low, high, pairStress));
                }
            }
        }

        private void SortNearestAscending()
        {
            // Insertion sort — list is short (n-1 entries, often ≤ 16).
            for (int i = 1; i < nearestBuf.Count; i++)
            {
                var key = nearestBuf[i];
                int j = i - 1;
                while (j >= 0 && nearestBuf[j].distSq > key.distSq)
                {
                    nearestBuf[j + 1] = nearestBuf[j];
                    j--;
                }
                nearestBuf[j + 1] = key;
            }
        }

        // ----- Private — pool -----
        private void DrivePool(List<Creature> bonded)
        {
            EnsurePoolSize(pairs.Count);

            int active = 0;
            for (int p = 0; p < pairs.Count; p++)
            {
                var pair = pairs[p];
                var ca = bonded[pair.a];
                var cb = bonded[pair.b];
                if (!ca || !cb) continue;

                Vector2 pa = ca.transform.position;
                Vector2 pb = cb.transform.position;
                Vector2 dir = pb - pa;
                float len = dir.magnitude;
                if (len < 1e-4f) continue;

                Vector2 mid = (pa + pb) * 0.5f;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                var sr = pool[active];
                var tr = sr.transform;
                tr.position   = new Vector3(mid.x, mid.y, 0f);
                tr.rotation   = Quaternion.Euler(0f, 0f, angle);
                tr.localScale = new Vector3(len, ribbonWidth, 1f);

                sr.color = new Color(pair.pairStress, 0f, 0f, 1f);

                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
                active++;
            }

            // Deactivate unused slots — preserved for next frame's reuse.
            for (int i = active; i < pool.Count; i++)
            {
                if (pool[i] && pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
            }
        }

        private void EnsurePoolSize(int requested)
        {
            while (pool.Count < requested)
            {
                pool.Add(CreatePoolItem(pool.Count));
            }
        }

        private SpriteRenderer CreatePoolItem(int index)
        {
            var go = new GameObject($"Tendril_{index}");
            go.transform.SetParent(transform, worldPositionStays: false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite           = tendrilSprite;
            sr.sharedMaterial   = tendrilMaterial;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder     = sortingOrder;
            sr.color            = Color.white;

            go.SetActive(false);
            return sr;
        }

        private void DeactivateAll()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] && pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
            }
        }
    }
}
