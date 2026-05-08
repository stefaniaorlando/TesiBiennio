using UnityEngine;

namespace Holobiont
{
    /*
     * Listens to the BreathSimulator and emits a particle burst on each phase
     * change (default: exhale). Burst size scales with current lung displacement.
     *
     * Wire BreathSimulator.exhaleStartEvent -> this.EmitBurst in the Inspector,
     * or enable Auto Subscribe (uses the C# Action event under the hood).
     */
    public class BreathParticleBurst : MonoBehaviour
    {
        // ----- References -----
        [Header("References")]
        [Tooltip("The BreathSimulator that drives the burst timing and size.")]
        [SerializeField] private BreathSimulator breath;

        [Tooltip("The ParticleSystem to emit bursts on. Set its Emission Rate Over Time to 0.")]
        [SerializeField] private ParticleSystem particles;

        // ----- Burst Settings -----
        [Header("Burst Settings")]
        [Tooltip("Maximum number of particles emitted in one burst (at full lung displacement).")]
        [Min(0)]
        [SerializeField] private int maxBurstCount = 30;

        [Tooltip("Minimum number of particles emitted even at zero displacement.")]
        [Min(0)]
        [SerializeField] private int minBurstCount = 5;

        [Tooltip("If true, also emit a burst at the START of each inhale (in addition to exhale).")]
        [SerializeField] private bool burstOnInhaleStart = false;

        // ----- Auto-Subscribe -----
        [Header("Auto Subscribe")]
        [Tooltip("If true, this script auto-subscribes to BreathSimulator.OnExhaleStart at Start. " +
                 "If false, wire the event manually in the Inspector via exhaleStartEvent.")]
        [SerializeField] private bool autoSubscribeExhale = true;

        [Tooltip("If true and burstOnInhaleStart is enabled, auto-subscribes to OnInhaleStart too.")]
        [SerializeField] private bool autoSubscribeInhale = false;

        // ----- Lifecycle -----
        private void Start()
        {
            if (!breath)
            {
                Debug.LogWarning($"{nameof(BreathParticleBurst)}: no BreathSimulator assigned.", this);
                return;
            }

            if (autoSubscribeExhale)
                breath.OnExhaleStart += EmitBurst;

            if (autoSubscribeInhale && burstOnInhaleStart)
                breath.OnInhaleStart += EmitBurst;
        }

        private void OnDestroy()
        {
            if (!breath) return;

            if (autoSubscribeExhale)
                breath.OnExhaleStart -= EmitBurst;

            if (autoSubscribeInhale && burstOnInhaleStart)
                breath.OnInhaleStart -= EmitBurst;
        }

        // ----- Inputs -----
        [ContextMenu("Emit Burst")]
        public void EmitBurst()
        {
            if (!particles) return;

            float fill = breath ? breath.Displacement : 1f;
            int count = Mathf.RoundToInt(Mathf.Lerp(minBurstCount, maxBurstCount, fill));
            particles.Emit(count);
        }
    }
}
