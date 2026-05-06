using Unity.Cinemachine;
using UnityEngine;

namespace Holobiont
{
    /*
     * Drives Cinemachine camera response from environment state and event lifecycle.
     *
     * Reads:  Temperature, Toxicity, EnvironmentEventSystem.ActiveEvent.
     * Writes:
     *   - Multi-Channel Perlin FrequencyGain ← Temperature   (cold = lazy, hot = restless)
     *   - Multi-Channel Perlin AmplitudeGain ← Toxicity      (clean = small idle, toxic = bigger sway)
     *   - Start impulse when a new event begins              (subtle rumble)
     *
     * The event system runs at most one event at a time — tracking is a single
     * RuntimeEvent reference, reset whenever the active event changes or goes null.
     *
     * Force formula:
     *   force = (Σ|effect.intensityDelta| / referenceDelta) * IntensityMultiplier * scalar
     * So a Cool Breeze (Δ=15) gets a small force, an Inferno (Δ=85) a real thump.
     * Difficulty escalation flows through automatically because IntensityMultiplier is
     * already baked into each RuntimeEvent.
     *
     * Pause behaviour: noise gains track env values, which freeze with GameClock —
     * gain just holds. Start impulses naturally suspend because the event system
     * stops scheduling while paused, so ActiveEvent doesn't change.
     *
     * Targets Cinemachine 3.x.
     */
    [DisallowMultipleComponent]
    public class EnvironmentCameraView : MonoBehaviour
    {
        // ----- Source -----
        [Header("Source")]
        [Tooltip("Environment whose Temperature and Toxicity drive the noise gains.")]
        [SerializeField] private EnvironmentManager environment;

        [Tooltip("Multi-Channel Perlin noise component on the CinemachineCamera.")]
        [SerializeField] private CinemachineBasicMultiChannelPerlin noise;

        [Tooltip("Event system whose ActiveEvent is polled to fire an impulse on start.")]
        [SerializeField] private EnvironmentEventSystem events;

        [Tooltip("Impulse source fired when a new event appears. Author its velocity profile as the 'start feel'.")]
        [SerializeField] private CinemachineImpulseSource startImpulseSource;

        // ----- ← Temperature -----
        [Header("← Temperature")]
        [Tooltip("Perlin frequency gain when fully cold. <1 plays the noise profile in slow motion.")]
        [Min(0f)] [SerializeField] private float frequencyGainCold = 0.7f;

        [Tooltip("Perlin frequency gain when fully hot. >1 plays the noise profile faster — restless camera.")]
        [Min(0f)] [SerializeField] private float frequencyGainHot  = 1.6f;

        // ----- ← Toxicity -----
        [Header("← Toxicity")]
        [Tooltip("Perlin amplitude gain when fully clean. 1 = noise plays at authored amplitude.")]
        [Min(0f)] [SerializeField] private float amplitudeGainClean = 1.0f;

        [Tooltip("Perlin amplitude gain when fully toxic. >1 = noise sways more — world feels unstable.")]
        [Min(0f)] [SerializeField] private float amplitudeGainToxic = 1.8f;

        // ----- Events -----
        [Header("Events")]
        [Tooltip("Reference Δ-sum that produces a force scalar of 1 before per-impulse scaling. Tune so 'normal' events feel right at scalar 1.")]
        [Min(0.01f)] [SerializeField] private float referenceDelta = 25f;

        [Tooltip("Force scalar applied to start-impulses. Multiplied by the event's normalised Δ-sum and IntensityMultiplier.")]
        [Min(0f)] [SerializeField] private float startImpulseScalar = 0.4f;

        // ----- State -----
        private EnvironmentEventSystem.RuntimeEvent firedStartFor;

        // ----- Lifecycle -----
        private void OnEnable()
        {
            if (!environment || !noise || !events || !startImpulseSource)
            {
                Debug.LogError(
                    $"{nameof(EnvironmentCameraView)} requires {nameof(EnvironmentManager)}, {nameof(CinemachineBasicMultiChannelPerlin)}, {nameof(EnvironmentEventSystem)}, and a start {nameof(CinemachineImpulseSource)}.",
                    this);
                enabled = false;
                return;
            }

            firedStartFor = null;
        }

        private void Update()
        {
            // ← Temperature / ← Toxicity  — modulate the authored noise profile's gains.
            float t = environment.TemperatureNormalized;
            float k = environment.ToxicityNormalized;
            noise.FrequencyGain = Mathf.Lerp(frequencyGainCold,  frequencyGainHot,  t);
            noise.AmplitudeGain = Mathf.Lerp(amplitudeGainClean, amplitudeGainToxic, k);

            TickEvents();
        }

        // ----- Private -----
        private void TickEvents()
        {
            var active = events.ActiveEvent;

            if (active == null)
            {
                firedStartFor = null;
                return;
            }

            // New active event: fire start.
            if (!ReferenceEquals(firedStartFor, active))
            {
                firedStartFor = active;
                FireImpulse(startImpulseSource, ForceFor(active, startImpulseScalar));
            }
        }

        private float ForceFor(EnvironmentEventSystem.RuntimeEvent e, float scalar)
        {
            var cfg = e.Config;
            if (!cfg) return 0f;

            float total = 0f;
            var effects = cfg.effects;
            if (effects != null)
            {
                for (int i = 0; i < effects.Length; i++)
                {
                    var eff = effects[i];
                    if (eff != null) total += Mathf.Abs(eff.intensityDelta);
                }
            }
            float weight = total / referenceDelta;
            return scalar * weight * e.IntensityMultiplier;
        }

        private static void FireImpulse(CinemachineImpulseSource source, float force)
        {
            if (!source || force <= 0f) return;
            source.GenerateImpulseWithForce(force);
        }
    }
}
