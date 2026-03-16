using UnityEngine;

/*
 * ============================================================================
 * BREATH PARTICLE BURST - Drives particle bursts from breath data
 * ============================================================================
 *
 * WHAT THIS DOES:
 * Listens to the BreathSimulator and emits a burst of particles each time
 * the breath changes phase (e.g. on every exhale). The burst size is
 * proportional to how full the lungs were at the moment of release.
 *
 * HOW TO SET UP:
 * 1. In the scene, select the GameObject that has your ParticleSystem.
 * 2. Add this component to it (or to any GameObject).
 * 3. Drag your BreathSimulator into the "Breath" slot.
 * 4. Drag the ParticleSystem you want to drive into the "Particles" slot.
 * 5. In the ParticleSystem's Emission module, set "Rate over Time" to 0
 *    so that emission is fully controlled by this script.
 * 6. On the BreathSimulator GameObject, expand the Events section and wire:
 *      OnExhaleStart  ->  this GameObject  ->  BreathParticleBurst.EmitBurst
 *    (or OnInhaleStart for inhale-driven bursts)
 * 7. Tune "Max Burst Count" to taste.
 *
 * ALTERNATIVELY - no event wiring needed:
 * Enable "Auto Subscribe" in the Inspector and the script will subscribe to
 * OnExhaleStart automatically at runtime.
 *
 * ============================================================================
 */

/// <summary>
/// Emits a particle burst driven by the BreathSimulator.
/// Burst size scales with breath.Displacement at the moment of the event.
/// </summary>
public class BreathParticleBurst : MonoBehaviour
{
    // =========================================================================
    // REFERENCES
    // =========================================================================

    [Header("References")]
    [Tooltip("The BreathSimulator that drives the burst timing and size.")]
    [SerializeField] private BreathSimulator breath;

    [Tooltip("The ParticleSystem to emit bursts on. Set its Emission Rate Over Time to 0.")]
    [SerializeField] private ParticleSystem particles;

    // =========================================================================
    // BURST SETTINGS
    // =========================================================================

    [Header("Burst Settings")]
    [Tooltip("Maximum number of particles emitted in one burst (at full lung displacement).")]
    [SerializeField] private int maxBurstCount = 30;

    [Tooltip("Minimum number of particles emitted even at zero displacement.")]
    [SerializeField] private int minBurstCount = 5;

    [Tooltip("If true, also emit a burst at the START of each inhale (in addition to exhale).")]
    [SerializeField] private bool burstOnInhaleStart = false;

    // =========================================================================
    // AUTO-SUBSCRIBE
    // =========================================================================

    [Header("Auto Subscribe")]
    [Tooltip("If true, this script auto-subscribes to BreathSimulator.OnExhaleStart at Start. " +
             "If false, wire the event manually in the Inspector.")]
    [SerializeField] private bool autoSubscribeExhale = true;

    [Tooltip("If true and burstOnInhaleStart is enabled, auto-subscribes to OnInhaleStart too.")]
    [SerializeField] private bool autoSubscribeInhale = false;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    private void Start()
    {
        if (breath == null)
        {
            Debug.LogWarning("BreathParticleBurst: no BreathSimulator assigned.", this);
            return;
        }

        if (autoSubscribeExhale)
            breath.OnExhaleStart.AddListener(EmitBurst);

        if (autoSubscribeInhale && burstOnInhaleStart)
            breath.OnInhaleStart.AddListener(EmitBurst);
    }

    private void OnDestroy()
    {
        if (breath == null) return;

        if (autoSubscribeExhale)
            breath.OnExhaleStart.RemoveListener(EmitBurst);

        if (autoSubscribeInhale && burstOnInhaleStart)
            breath.OnInhaleStart.RemoveListener(EmitBurst);
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>
    /// Emit a burst whose size is proportional to the current breath displacement.
    /// Call this from BreathSimulator.OnExhaleStart (or OnInhaleStart) in the Inspector,
    /// or let autoSubscribe handle the wiring automatically.
    /// </summary>
    public void EmitBurst()
    {
        if (particles == null) return;

        float fill = breath != null ? breath.Displacement : 1f;
        int count = Mathf.RoundToInt(Mathf.Lerp(minBurstCount, maxBurstCount, fill));
        particles.Emit(count);
    }
}
