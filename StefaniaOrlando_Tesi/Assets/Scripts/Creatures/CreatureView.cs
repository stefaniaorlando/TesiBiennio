using System.Collections;
using UnityEngine;

namespace Holobiont
{
    /*
     * Per-creature visual driver. Sits on the creature prefab next to Creature
     * and a SpriteRenderer; pushes per-instance uniforms into the SpriteRenderer's
     * MaterialPropertyBlock so a single shared CreatureSDF.mat renders all three
     * types correctly.
     *
     * Reads:
     *   Creature.Type             — drives _Shape (and _FieldRelation, mirrored)
     *   Creature.Affinity         — affinity-tinted _BaseColor
     *   Creature.AffinityEfficiency / Creature.Stress — drive _Efficiency / _Stress
     *
     * Never writes to simulation. Uses Time.deltaTime (Update) — the visual layer
     * keeps animating while paused, matching the project's view-vs-sim contract
     * in project_conventions.md.
     *
     * Shape ↔ FieldRelation pairing is set up to mirror the canonical mapping
     * (circle/dissolve, triangle/deflect, square/contain) but exposed as
     * separable so a future mechanic could decouple them (e.g. a stressed
     * scudo whose deflection collapses into dissolve).
     */
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Creature))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class CreatureView : MonoBehaviour
    {
        // ----- Sources -----
        [Header("Sources")]
        [Tooltip("Sibling creature whose state drives the view. Auto-resolved on Reset.")]
        [SerializeField] private Creature creature;

        [Tooltip("Sibling sprite renderer using a CreatureSDF material. Auto-resolved on Reset.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        // ----- Tuning -----
        [Header("Affinity Tint")]
        [Tooltip("How strongly per-creature affinity shifts the base color. 0 = type's flat baseColor, 1 = full warm/cool shift.")]
        [Range(0f, 1f)]
        [SerializeField] private float affinityTintStrength = 0.35f;

        [Tooltip("Color the base lerps toward when temperature affinity is high.")]
        [SerializeField] private Color warmTint = new Color(1.10f, 0.90f, 0.70f);

        [Tooltip("Color the base lerps toward when temperature affinity is low.")]
        [SerializeField] private Color coolTint = new Color(0.78f, 0.92f, 1.10f);

        [Header("Field Relation")]
        [Tooltip("If true, _FieldRelation tracks _Shape (canonical mapping). Disable to force a manual override below.")]
        [SerializeField] private bool mirrorFieldRelationFromShape = true;

        [Tooltip("Manual field relation override (0 dissolve / 1 deflect / 2 contain). Only consulted when 'Mirror Field Relation From Shape' is off.")]
        [Range(0, 2)]
        [SerializeField] private int fieldRelationOverride = 0;

        [Header("Bond Pop")]
        [Tooltip("Duration of the scale-punch when the creature transitions Unbound → Bound. Uses sin-arc easing: scale goes baseScale → baseScale*peak → baseScale.")]
        [Min(0f), SerializeField] private float bondPopDuration = 0.3f;

        [Tooltip("Peak scale multiplier at the apex of the bond pop. 1 = no pop.")]
        [Min(1f), SerializeField] private float bondPopPeakScale = 1.3f;

        [Header("Release Flash")]
        [Tooltip("Duration of the brief alpha dip when the creature transitions Bound → Unbound (manual or cascade release). Suppressed if a death flicker is already running.")]
        [Min(0f), SerializeField] private float releaseFlashDuration = 0.4f;

        [Tooltip("Alpha at the dip of the release flash. The creature recovers to full alpha — releases do NOT destroy the creature; it continues drifting unbound until lifetime expiry.")]
        [Range(0f, 1f), SerializeField] private float releaseFlashDipAlpha = 0.4f;

        [Header("Lifetime Fade")]
        [Tooltip("Seconds before unbound despawn during which alpha tapers linearly to 0. 0 disables the fade. Drives the MPB _BaseColor.a so it composes with (rather than overrides) transient flashes via spriteRenderer.color.a.")]
        [Min(0f), SerializeField] private float lifetimeFadeWindow = 1.5f;

        [Header("Death")]
        [Tooltip("Total duration of the alpha flicker triggered when the creature's bonded stress hits the death threshold. Honors the CreatureSDF shader's 'flicker reserved for the death event' contract; the creature itself is not destroyed by this view.")]
        [Min(0f), SerializeField] private float deathFlickerDuration = 0.4f;

        [Tooltip("Number of off/on cycles within the flicker window. Higher = quicker visible flashes.")]
        [Min(1), SerializeField] private int deathFlickerCount = 6;

        [Tooltip("Alpha during the 'off' phase of each flicker cycle. 0 = invisible.")]
        [Range(0f, 1f), SerializeField] private float deathFlickerLowAlpha = 0.15f;

        // ----- Cached property IDs -----
        private static readonly int IDShape         = Shader.PropertyToID("_Shape");
        private static readonly int IDFieldRelation = Shader.PropertyToID("_FieldRelation");
        private static readonly int IDBaseColor     = Shader.PropertyToID("_BaseColor");
        private static readonly int IDStress        = Shader.PropertyToID("_Stress");
        private static readonly int IDEfficiency    = Shader.PropertyToID("_Efficiency");

        private MaterialPropertyBlock mpb;
        private Coroutine deathFlickerRoutine;
        private Coroutine bondPopRoutine;
        private Coroutine releaseFlashRoutine;
        private Creature subscribedCreature;
        private Vector3 baseScale = Vector3.one;
        private BondStatus lastStatus;
        private bool statusInitialized;

        // ----- Lifecycle -----
        private void Reset()
        {
            creature       = GetComponent<Creature>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            if (!creature)       creature       = GetComponent<Creature>();
            if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();

            if (mpb is null) mpb = new MaterialPropertyBlock();

            // Sprite tint becomes a no-op so MPB _BaseColor is the only color driver.
            // Alpha is the one channel we still modulate from C# — used by the death
            // flicker / release flash, which ride on top of the shader via IN.spriteColor.a.
            spriteRenderer.color = Color.white;

            // Cache the prefab/instance scale so transient pops animate around it
            // rather than snapping to one. Re-cached on every enable in case a parent
            // adjusts it between disable cycles.
            baseScale = transform.localScale;

            statusInitialized = false;

            if (creature)
            {
                creature.OnStressDeath += HandleStressDeath;
                subscribedCreature = creature;
            }
        }

        private void OnDisable()
        {
            // C# null (not Unity-bool): if the Creature was Destroyed, the managed
            // object is still alive and the event delegate must be unhooked, otherwise
            // the handler keeps the view alive past the creature's death.
            if (subscribedCreature is not null)
            {
                subscribedCreature.OnStressDeath -= HandleStressDeath;
                subscribedCreature = null;
            }

            // Stop any in-flight transients and reset the channels they touched so
            // the next OnEnable starts from a clean state.
            if (deathFlickerRoutine != null)
            {
                StopCoroutine(deathFlickerRoutine);
                deathFlickerRoutine = null;
                SetSpriteAlpha(1f);
            }
            if (releaseFlashRoutine != null)
            {
                StopCoroutine(releaseFlashRoutine);
                releaseFlashRoutine = null;
                SetSpriteAlpha(1f);
            }
            if (bondPopRoutine != null)
            {
                StopCoroutine(bondPopRoutine);
                bondPopRoutine = null;
                transform.localScale = baseScale;
            }
        }

        private void Update()
        {
            if (!creature || !creature.IsInitialized || !spriteRenderer) return;

            int shape = (int)creature.Type;
            int field = mirrorFieldRelationFromShape ? shape : fieldRelationOverride;

            Color baseColor = creature.Config ? creature.Config.baseColor : Color.white;
            Color tinted   = ComputeAffinityTint(baseColor, creature.Affinity, affinityTintStrength);

            // Lifetime fade rides on _BaseColor.a (via the MPB), not spriteRenderer.color.a.
            // The shader composes alpha as insideMask * _BaseColor.a * IN.spriteColor.a, so
            // the despawn taper multiplies cleanly with any transient flash on color.a
            // instead of fighting it.
            tinted.a *= ComputeLifetimeFadeAlpha();

            spriteRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(IDShape,         shape);
            mpb.SetFloat(IDFieldRelation, field);
            mpb.SetColor(IDBaseColor,     tinted);
            mpb.SetFloat(IDStress,        creature.Stress);
            mpb.SetFloat(IDEfficiency,    creature.AffinityEfficiency);
            spriteRenderer.SetPropertyBlock(mpb);

            DetectBondTransitions();
        }

        // ----- Lifetime fade -----
        // Returns 1 unless the creature is unbound, has a finite lifetime, and is within
        // the fade window of expiry. Linear taper from 1 → 0 over the last
        // `lifetimeFadeWindow` seconds.
        private float ComputeLifetimeFadeAlpha()
        {
            if (lifetimeFadeWindow <= 0f) return 1f;
            if (creature.Status != BondStatus.Unbound) return 1f;
            if (!creature.Config || creature.Config.unboundLifetime <= 0f) return 1f;

            float remaining = creature.UnboundTimeRemaining;
            if (remaining >= lifetimeFadeWindow) return 1f;
            return Mathf.Clamp01(remaining / lifetimeFadeWindow);
        }

        // ----- Bond / Release transitions -----
        // Polls Creature.Status rather than subscribing to a HolobiontManager event,
        // so this view stays decoupled from the manager and reacts to any path that
        // toggles the regime (TryBond, Release, debug context menus, cascade).
        private void DetectBondTransitions()
        {
            BondStatus status = creature.Status;

            if (!statusInitialized)
            {
                lastStatus = status;
                statusInitialized = true;
                return;
            }

            if (status == lastStatus) return;

            if (status == BondStatus.Bound)
            {
                StartBondPop();
            }
            else
            {
                // Bound → Unbound. If a death flicker is already running, the release
                // is a death-driven Release call and the flicker is the canonical
                // visual — don't double up.
                if (deathFlickerRoutine == null) StartReleaseFlash();
            }

            lastStatus = status;
        }

        private void StartBondPop()
        {
            if (bondPopRoutine != null) StopCoroutine(bondPopRoutine);
            bondPopRoutine = StartCoroutine(BondPopCoroutine());
        }

        private void StartReleaseFlash()
        {
            if (releaseFlashRoutine != null) StopCoroutine(releaseFlashRoutine);
            releaseFlashRoutine = StartCoroutine(ReleaseFlashCoroutine());
        }

        private IEnumerator BondPopCoroutine()
        {
            float duration = Mathf.Max(0.001f, bondPopDuration);
            float peak     = Mathf.Max(1f, bondPopPeakScale);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float n = Mathf.Clamp01(t / duration);
                // Sin arc: 1 → peak → 1 over [0..π].
                float scale = 1f + (peak - 1f) * Mathf.Sin(n * Mathf.PI);
                transform.localScale = baseScale * scale;
                yield return null;
            }
            transform.localScale = baseScale;
            bondPopRoutine = null;
        }

        private IEnumerator ReleaseFlashCoroutine()
        {
            float duration = Mathf.Max(0.001f, releaseFlashDuration);
            float dip      = Mathf.Clamp01(releaseFlashDipAlpha);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float n = Mathf.Clamp01(t / duration);
                // Sin arc: 1 → dip → 1 over [0..π].
                float alpha = Mathf.Lerp(1f, dip, Mathf.Sin(n * Mathf.PI));
                SetSpriteAlpha(alpha);
                yield return null;
            }
            SetSpriteAlpha(1f);
            releaseFlashRoutine = null;
        }

        // ----- Death event -----
        private void HandleStressDeath(Creature c)
        {
            if (!isActiveAndEnabled || !spriteRenderer) return;
            if (deathFlickerRoutine != null) StopCoroutine(deathFlickerRoutine);
            deathFlickerRoutine = StartCoroutine(DeathFlickerCoroutine());
        }

        // The CreatureSDF shader composes alpha as insideMask * _BaseColor.a * IN.spriteColor.a.
        // Modulating spriteRenderer.color.a here is the cheapest way to drive the
        // external death flicker without touching the MPB or shader globals.
        private IEnumerator DeathFlickerCoroutine()
        {
            float restoreAlpha = spriteRenderer ? spriteRenderer.color.a : 1f;
            int cycles  = Mathf.Max(1, deathFlickerCount);
            float perPhase = deathFlickerDuration / (cycles * 2);

            for (int i = 0; i < cycles; i++)
            {
                SetSpriteAlpha(deathFlickerLowAlpha);
                yield return new WaitForSeconds(perPhase);
                SetSpriteAlpha(restoreAlpha);
                yield return new WaitForSeconds(perPhase);
            }

            SetSpriteAlpha(restoreAlpha);
            deathFlickerRoutine = null;
        }

        private void SetSpriteAlpha(float a)
        {
            if (!spriteRenderer) return;
            Color c = spriteRenderer.color;
            c.a = a;
            spriteRenderer.color = c;
        }

        // ----- Private -----
        private Color ComputeAffinityTint(Color baseColor, CreatureAffinityData affinity, float strength)
        {
            Color shift = Color.Lerp(coolTint, warmTint, Mathf.Clamp01(affinity.idealTemperature));
            return new Color(
                Mathf.Lerp(baseColor.r, baseColor.r * shift.r, strength),
                Mathf.Lerp(baseColor.g, baseColor.g * shift.g, strength),
                Mathf.Lerp(baseColor.b, baseColor.b * shift.b, strength),
                baseColor.a);
        }
    }
}
