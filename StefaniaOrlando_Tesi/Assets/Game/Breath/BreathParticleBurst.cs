using UnityEngine;

namespace Holobiont
{
    /*
     * Listens to the BreathSimulator and emits a particle burst on each phase
     * change (default: exhale). Burst size scales with current lung displacement.
     *
     * Wire BreathSimulator.OnExhaleStart -> this.EmitBurst in the Inspector,
     * or enable Auto Subscribe.
     */
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

        public void EmitBurst()
        {
            if (particles == null) return;

            float fill = breath != null ? breath.Displacement : 1f;
            int count = Mathf.RoundToInt(Mathf.Lerp(minBurstCount, maxBurstCount, fill));
            particles.Emit(count);
        }
    }
}
