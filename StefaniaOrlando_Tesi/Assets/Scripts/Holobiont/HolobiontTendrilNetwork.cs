using System.Collections.Generic;
using UnityEngine;

namespace Holobiont
{
    /*
     * CPU side of the symbiosis-tendrils visual. Sibling to HolobiontManager —
     * reads the bonded-creature list, computes K-nearest neighbors, and drives
     * a pool of renderers (one per active pair).
     *
     * Two render paths, selectable via rendererMode:
     *
     *   SpriteQuad — pooled SpriteRenderers on a 1×1 white square sprite.
     *     The wavering, breath-modulated, stress-fraying stylization lives in
     *     the Holobiont/SymbiosisTendrils shader; the CPU just does adjacency
     *     and writes a transform/color per pair:
     *       position    — midpoint of A and B
     *       rotation    — Z so local +X aligns with A→B (local +Y is the
     *                     world-space perpendicular the shader reads off
     *                     UNITY_MATRIX_M for noise displacement)
     *       localScale  — (lengthAB, ribbonWidth, 1)
     *       color.r     — pair stress in [0,1]
     *
     *   LineRenderer — pooled LineRenderers per pair. The wobble and breath
     *     modulation are computed CPU-side and pushed to the line as N
     *     segment positions plus per-line color and width curve. No external
     *     sprite asset, no UV/pivot gotchas, fewer moving parts; tradeoff is
     *     CPU work for the wave (cheap at this scale).
     *
     * Why per-frame rebuild: bound creatures spring around the holobiont
     * center with breath-modulated radius, so adjacency genuinely changes
     * frame-to-frame. For tens of creatures the naive O(N²) K-NN scan is
     * well under a millisecond.
     *
     * Pool grows on demand and never shrinks — extra entries are deactivated
     * rather than destroyed, so a temporary spike doesn't churn GC. Each
     * mode owns its own pool; switching mode at runtime deactivates the
     * other pool's renderers.
     */
    [DisallowMultipleComponent]
    public class HolobiontTendrilNetwork : MonoBehaviour
    {
        public enum RendererMode { SpriteQuad, LineRenderer }

        // ----- Sources -----
        [Header("Sources")]
        [Tooltip("Holobiont whose bonded creatures define the network. Auto-resolved from siblings on Reset.")]
        [SerializeField] private HolobiontManager manager;

        // ----- Mode -----
        [Header("Mode")]
        [Tooltip("Which renderer to use. SpriteQuad uses the SymbiosisTendrils shader on a 1×1 sprite; LineRenderer uses CPU-driven polylines (no sprite needed).")]
        [SerializeField] private RendererMode rendererMode = RendererMode.SpriteQuad;

        // ----- Shared rendering -----
        [Header("Rendering (shared)")]
        [Tooltip("Sorting layer for the pooled renderers — typically below the creatures' layer so tendrils render behind them.")]
        [SerializeField] private string sortingLayerName = "Default";

        [Tooltip("Order in layer for the pooled renderers. Negative values draw beneath sprites with order 0.")]
        [SerializeField] private int sortingOrder = -1;

        [Tooltip("World-space ribbon width. SpriteQuad mode uses this as sprite local Y scale; LineRenderer mode uses it as the line's max width (taper applied via widthAlongLength).")]
        [Min(0.001f)]
        [SerializeField] private float ribbonWidth = 0.18f;

        // ----- Sprite path -----
        [Header("Sprite path")]
        [Tooltip("Material using the Holobiont/SymbiosisTendrils shader. Shared across every pooled tendril SpriteRenderer.")]
        [SerializeField] private Material tendrilMaterial;

        [Tooltip("1×1 white square sprite. Import as: PPU = (texture pixel size) so the sprite quad is exactly 1×1 world units, Pivot = Center, Mesh Type = Full Rect.")]
        [SerializeField] private Sprite tendrilSprite;

        // ----- Line path -----
        [Header("Line path")]
        [Tooltip("Material applied to every pooled LineRenderer. A URP/Unlit (transparent) or URP 2D Sprite-Lit-Default material works — vertex color drives the tint.")]
        [SerializeField] private Material lineMaterial;

        [Tooltip("Number of line segments per tendril. More segments = smoother wobble; 6–8 is plenty.")]
        [Range(2, 24)]
        [SerializeField] private int segmentsPerTendril = 8;

        [Tooltip("Width along the line (x in [0,1] from A to B, y is multiplier 0..1). Default tapers smoothly to zero at both endpoints.")]
        [SerializeField] private AnimationCurve widthAlongLength = new AnimationCurve(
            new Keyframe(0f, 0f, 2f, 2f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 0f, -2f, -2f));

        [Tooltip("Perpendicular wave amplitude in world units, scaled by per-segment endpoint taper.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float waveAmplitude = 0.06f;

        [Tooltip("Spatial frequency of the wave along the tendril (cycles per world unit).")]
        [Range(0f, 4f)]
        [SerializeField] private float waveSpatialFreq = 0.6f;

        [Tooltip("Time scale of the wave animation.")]
        [Range(0f, 4f)]
        [SerializeField] private float waveTimeScale = 0.8f;

        [Tooltip("Tint at pair stress = 0. Alpha here also sets the base opacity (modulated by breath at runtime).")]
        [SerializeField] private Color healthyColor = new Color(0.85f, 0.95f, 0.7f, 0.65f);

        [Tooltip("Tint at pair stress = 1.")]
        [SerializeField] private Color stressedColor = new Color(0.55f, 0.45f, 0.35f, 0.65f);

        [Tooltip("Alpha multiplier at full inhale (breath phase = -1).")]
        [Range(0f, 1f)]
        [SerializeField] private float breathMin = 0.55f;

        [Tooltip("Alpha multiplier at full exhale (breath phase = +1).")]
        [Range(0f, 1f)]
        [SerializeField] private float breathMax = 1.0f;

        // ----- Network -----
        [Header("Network")]
        [Tooltip("How many nearest neighbors each creature connects to. K=2 is a clean network; K=3 starts to look like spaghetti.")]
        [Range(1, 4)]
        [SerializeField] private int kNeighbors = 2;

        [Tooltip("Also draw tendrils from the closest bonded creatures directly to the holobiont core, like primary roots feeding the center.")]
        [SerializeField] private bool connectToCore = true;

        [Tooltip("How many of the closest bonded creatures get a direct tendril to the core. Picked by Euclidean distance to the core each frame.")]
        [Range(0, 6)]
        [SerializeField] private int coreNearestK = 2;

        // ----- Debug -----
        [Header("Debug")]
        [Tooltip("Mirror of pairs emitted this frame.")]
        [SerializeField, ReadOnly] private int pairCount;

        [Tooltip("Mirror of how many renderer slots the active pool currently holds.")]
        [SerializeField, ReadOnly] private int poolCapacity;

        // ----- Internal state -----
        private bool initialized;
        private RendererMode lastMode;

        private readonly List<SpriteRenderer> spritePool = new List<SpriteRenderer>(32);
        private readonly List<LineRenderer>   linePool   = new List<LineRenderer>(32);

        private readonly List<(int idx, float distSq)> nearestBuf = new List<(int, float)>(16);
        private readonly HashSet<long> emittedPairs = new HashSet<long>();
        private readonly List<(int a, int b, float pairStress)> pairs = new List<(int, int, float)>(64);

        private static readonly int IDBreathPhase = Shader.PropertyToID("_HoloBreathPhase");

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

            if (!ValidateModeAssets(rendererMode))
            {
                enabled = false;
                return;
            }

            if (widthAlongLength == null || widthAlongLength.length == 0)
            {
                widthAlongLength = new AnimationCurve(
                    new Keyframe(0f, 0f, 2f, 2f),
                    new Keyframe(0.5f, 1f),
                    new Keyframe(1f, 0f, -2f, -2f));
            }

            lastMode    = rendererMode;
            initialized = true;
        }

        private void OnDisable()
        {
            DeactivateAll(spritePool);
            DeactivateAll(linePool);
            initialized = false;
        }

        private void LateUpdate()
        {
            if (!initialized) return;

            // Mode switch at runtime — hide whatever the previous mode was using.
            if (rendererMode != lastMode)
            {
                if (lastMode == RendererMode.SpriteQuad) DeactivateAll(spritePool);
                else                                     DeactivateAll(linePool);
                lastMode = rendererMode;
            }

            var bonded = manager.State.BondedCreatures;
            // Need at least 1 creature for a core tendril, 2 for any peer tendril.
            // K-NN itself no-ops at n < 2; the core block handles n == 1 cleanly.
            if (bonded.Count < 1)
            {
                DeactivateActivePool();
                pairCount = 0;
                return;
            }

            BuildAdjacency(bonded);

            switch (rendererMode)
            {
                case RendererMode.SpriteQuad:   DriveSpritePool(bonded);  poolCapacity = spritePool.Count; break;
                case RendererMode.LineRenderer: DriveLinePool(bonded);    poolCapacity = linePool.Count;   break;
            }

            pairCount = pairs.Count;
        }

        // ----- Private — adjacency -----
        private void BuildAdjacency(List<Creature> bonded)
        {
            pairs.Clear();
            emittedPairs.Clear();

            int n = bonded.Count;
            int k = Mathf.Min(kNeighbors, n - 1);

            // Note: when k <= 0 the peer loop below is a no-op (take == 0), but the
            // core-connection block still runs so a single bonded creature can still
            // get a tendril to the core.
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

            // Optional core connections: pair the K closest bonded creatures directly
            // with the holobiont core. Encoded with b == -1 (sentinel for "core") so
            // the existing pool-driving code can branch on it without a separate path.
            if (connectToCore && coreNearestK > 0)
            {
                Vector2 corePos = manager.transform.position;

                nearestBuf.Clear();
                for (int i = 0; i < n; i++)
                {
                    var ci = bonded[i];
                    if (!ci) continue;
                    float dSq = ((Vector2)ci.transform.position - corePos).sqrMagnitude;
                    nearestBuf.Add((i, dSq));
                }

                SortNearestAscending();

                int takeCore = Mathf.Min(coreNearestK, nearestBuf.Count);
                for (int x = 0; x < takeCore; x++)
                {
                    int idx = nearestBuf[x].idx;
                    pairs.Add((idx, -1, bonded[idx].Stress));
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

        // ----- Private — sprite pool -----
        private void DriveSpritePool(List<Creature> bonded)
        {
            EnsureSpritePoolSize(pairs.Count);

            Vector2 corePos = manager.transform.position;

            int active = 0;
            for (int p = 0; p < pairs.Count; p++)
            {
                var pair = pairs[p];
                var ca = bonded[pair.a];
                if (!ca) continue;

                Vector2 pa = ca.transform.position;
                Vector2 pb;
                if (pair.b == -1)
                {
                    pb = corePos;
                }
                else
                {
                    var cb = bonded[pair.b];
                    if (!cb) continue;
                    pb = cb.transform.position;
                }

                Vector2 dir = pb - pa;
                float len = dir.magnitude;
                if (len < 1e-4f) continue;

                Vector2 mid = (pa + pb) * 0.5f;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                var sr = spritePool[active];
                var tr = sr.transform;
                tr.position   = new Vector3(mid.x, mid.y, 0f);
                tr.rotation   = Quaternion.Euler(0f, 0f, angle);
                tr.localScale = new Vector3(len, ribbonWidth, 1f);

                sr.color = new Color(pair.pairStress, 0f, 0f, 1f);

                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
                active++;
            }

            for (int i = active; i < spritePool.Count; i++)
            {
                if (spritePool[i] && spritePool[i].gameObject.activeSelf) spritePool[i].gameObject.SetActive(false);
            }
        }

        private void EnsureSpritePoolSize(int requested)
        {
            while (spritePool.Count < requested)
            {
                spritePool.Add(CreateSpritePoolItem(spritePool.Count));
            }
        }

        private SpriteRenderer CreateSpritePoolItem(int index)
        {
            var go = new GameObject($"Tendril_Sprite_{index}");
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

        // ----- Private — line pool -----
        private void DriveLinePool(List<Creature> bonded)
        {
            EnsureLinePoolSize(pairs.Count);

            float breathPhase = Shader.GetGlobalFloat(IDBreathPhase);
            float breathT     = Mathf.Clamp01(breathPhase * 0.5f + 0.5f);
            float breathMult  = Mathf.Lerp(breathMin, breathMax, breathT);
            float t           = Time.time * waveTimeScale;

            int N = Mathf.Max(2, segmentsPerTendril + 1);
            Vector2 corePos = manager.transform.position;

            int active = 0;
            for (int p = 0; p < pairs.Count; p++)
            {
                var pair = pairs[p];
                var ca = bonded[pair.a];
                if (!ca) continue;

                Vector2 pa = ca.transform.position;
                Vector2 pb;
                if (pair.b == -1)
                {
                    pb = corePos;
                }
                else
                {
                    var cb = bonded[pair.b];
                    if (!cb) continue;
                    pb = cb.transform.position;
                }

                Vector2 dir = pb - pa;
                float len = dir.magnitude;
                if (len < 1e-4f) continue;
                Vector2 perp = new Vector2(-dir.y, dir.x) / len;

                var lr = linePool[active];
                if (lr.positionCount != N) lr.positionCount = N;

                // Slight per-pair phase offset so neighboring tendrils don't wobble in lockstep.
                float pairPhase = pair.a * 0.317f + pair.b * 0.911f;

                for (int s = 0; s < N; s++)
                {
                    float u     = s / (float)(N - 1);
                    float taper = Mathf.Sin(u * Mathf.PI);
                    float arg   = u * len * waveSpatialFreq * Mathf.PI * 2f + t + pairPhase;
                    float wave  = (Mathf.Sin(arg) + 0.5f * Mathf.Sin(arg * 2.13f)) * waveAmplitude * taper;

                    Vector2 basePos = Vector2.Lerp(pa, pb, u);
                    Vector2 pos     = basePos + perp * wave;
                    lr.SetPosition(s, new Vector3(pos.x, pos.y, 0f));
                }

                Color c = Color.Lerp(healthyColor, stressedColor, Mathf.Clamp01(pair.pairStress));
                c.a *= breathMult;
                lr.startColor = c;
                lr.endColor   = c;

                lr.startWidth = ribbonWidth;
                lr.endWidth   = ribbonWidth;
                lr.widthCurve = widthAlongLength;

                if (!lr.gameObject.activeSelf) lr.gameObject.SetActive(true);
                active++;
            }

            for (int i = active; i < linePool.Count; i++)
            {
                if (linePool[i] && linePool[i].gameObject.activeSelf) linePool[i].gameObject.SetActive(false);
            }
        }

        private void EnsureLinePoolSize(int requested)
        {
            while (linePool.Count < requested)
            {
                linePool.Add(CreateLinePoolItem(linePool.Count));
            }
        }

        private LineRenderer CreateLinePoolItem(int index)
        {
            var go = new GameObject($"Tendril_Line_{index}");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace      = true;
            lr.sharedMaterial     = lineMaterial;
            lr.sortingLayerName   = sortingLayerName;
            lr.sortingOrder       = sortingOrder;
            lr.alignment          = LineAlignment.View;
            lr.textureMode        = LineTextureMode.Stretch;
            lr.numCapVertices     = 2;
            lr.numCornerVertices  = 2;
            lr.widthCurve         = widthAlongLength;
            lr.startWidth         = ribbonWidth;
            lr.endWidth           = ribbonWidth;
            lr.startColor         = Color.white;
            lr.endColor           = Color.white;

            go.SetActive(false);
            return lr;
        }

        // ----- Private — pool helpers -----
        private bool ValidateModeAssets(RendererMode mode)
        {
            switch (mode)
            {
                case RendererMode.SpriteQuad:
                    if (!tendrilMaterial)
                    {
                        Debug.LogError($"{nameof(HolobiontTendrilNetwork)}: SpriteQuad mode needs a tendril material (assign Holobiont/SymbiosisTendrils).", this);
                        return false;
                    }
                    if (!tendrilSprite)
                    {
                        Debug.LogError($"{nameof(HolobiontTendrilNetwork)}: SpriteQuad mode needs a 1×1 white square sprite.", this);
                        return false;
                    }
                    return true;

                case RendererMode.LineRenderer:
                    if (!lineMaterial)
                    {
                        Debug.LogError($"{nameof(HolobiontTendrilNetwork)}: LineRenderer mode needs a line material (assign a URP/Unlit transparent or URP 2D Sprite-Lit material).", this);
                        return false;
                    }
                    return true;
            }
            return false;
        }

        private void DeactivateActivePool()
        {
            if (rendererMode == RendererMode.SpriteQuad) DeactivateAll(spritePool);
            else                                         DeactivateAll(linePool);
        }

        private static void DeactivateAll<T>(List<T> pool) where T : Component
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] && pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
            }
        }
    }
}
