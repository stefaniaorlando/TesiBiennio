using UnityEngine;
using UnityEngine.Events;

/*
 * ============================================================================
 * FIELD ABILITY - PARTICLE POLARITY (Attract & Kill Particles)
 * ============================================================================
 *
 * WHAT THIS DOES:
 * Creates a breath-driven field around the player that acts on a ParticleSystem:
 * - Attract radius: pulls particles toward the player during inhale
 * - Kill radius:    destroys any particle that enters this inner zone
 *
 * Breath coupling (same logic as FieldAbility_Polarity):
 * - Inhale  → attraction is active, strength scales with breath.Depth
 * - Exhale  → field off (or repel, see PauseBehavior settings)
 * - Paused  → configurable (sustain last / always attract / no force)
 *
 * HOW TO SET UP:
 * 1. Add this component to the Player GameObject (or any pivot point).
 * 2. Drag your BreathSimulator into "Breath".
 * 3. Drag the ParticleSystem to control into "Particles".
 *    - In that ParticleSystem, enable Simulation Space = "World" so that
 *      particle positions are in world space (required for the math to work).
 * 4. Tune Attract Radius, Kill Radius, and Attract Force.
 * 5. Enable Show Debug Gizmos to see the radii in the Scene view.
 *
 * ============================================================================
 */

/// <summary>
/// Breath-driven field that attracts particles toward the player and kills them
/// on contact. Works by directly manipulating ParticleSystem particle data.
/// </summary>
public class FieldAbility_ParticlePolarity : MonoBehaviour
{
    // =========================================================================
    // REFERENCES
    // =========================================================================

    [Header("References")]
    [Tooltip("The breath simulator driving attraction strength and timing.")]
    [SerializeField] private BreathSimulator breath;

    [Tooltip("The particle system whose particles will be attracted and killed. " +
             "Its Simulation Space must be set to 'World'.")]
    [SerializeField] private ParticleSystem particles;

    // =========================================================================
    // FIELD SETTINGS
    // =========================================================================

    [Header("Field Settings")]
    [Tooltip("Outer radius: particles inside this range are pulled toward the player.")]
    [SerializeField] private float attractRadius = 5f;

    [Tooltip("Inner radius: particles that reach this distance are destroyed immediately.")]
    [SerializeField] private float killRadius = 0.4f;

    [Tooltip("Base attraction acceleration (units/sec²) at full breath depth.")]
    [SerializeField] private float attractForce = 6f;

    [Tooltip("How force scales with distance. 0 = constant, 1 = linear falloff (stronger near edge).")]
    [Range(0f, 2f)]
    [SerializeField] private float distanceFalloff = 0f;

    // =========================================================================
    // PAUSE BEHAVIOR
    // =========================================================================

    [Header("Pause Behavior")]
    [Tooltip("What the field does when the player holds their breath (Space).")]
    [SerializeField] private PauseBehavior pauseBehavior = PauseBehavior.SustainLast;

    public enum PauseBehavior
    {
        NoForce,        // Field turns off while paused
        SustainLast,    // Keep whatever was active last (attract or off)
        AlwaysAttract   // Always attract while paused
    }

    // =========================================================================
    // DEBUG
    // =========================================================================

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color attractColor = new Color(0.2f, 0.6f, 1f, 0.4f);
    [SerializeField] private Color killColor    = new Color(1f, 0.2f, 0.2f, 0.5f);

    // =========================================================================
    // EVENTS
    // =========================================================================

    [Header("Events")]
    [Tooltip("Fired after SetParticles each frame a kill occurred. Argument is the number of particles killed that frame.")]
    public UnityEvent<int> OnParticlesKilled;

    // =========================================================================
    // RUNTIME STATE
    // =========================================================================

    private ParticleSystem.Particle[] particleBuffer;
    private bool lastWasAttracting = false;
    private bool isAttracting = false;

    // =========================================================================
    // PUBLIC PROPERTIES
    // =========================================================================

    /// <summary>True when the field is currently attracting particles.</summary>
    public bool IsAttracting => isAttracting;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    private void Awake()
    {
        if (particles != null)
            particleBuffer = new ParticleSystem.Particle[particles.main.maxParticles];
    }

    private void LateUpdate()
    {
        if (breath == null || particles == null) return;

        isAttracting = DetermineAttracting();
        if (!isAttracting) return;

        int count = particles.GetParticles(particleBuffer);
        if (count == 0) return;

        Vector3 center    = transform.position;
        float depthScale  = Mathf.Max(breath.Depth, 0.1f);
        float force       = attractForce * depthScale * Time.deltaTime;
        float killRadSq   = killRadius   * killRadius;
        float attractRadSq = attractRadius * attractRadius;
        int killCount = 0;

        for (int i = 0; i < count; i++)
        {
            Vector3 toPlayer = center - (Vector3)particleBuffer[i].position;
            float distSq = toPlayer.sqrMagnitude;

            if (distSq > attractRadSq) continue;

            // Kill zone — particle reached the player
            if (distSq <= killRadSq)
            {
                particleBuffer[i].remainingLifetime = 0f;
                killCount++;
                continue;
            }

            // Attraction — scale force by distance falloff
            float dist = Mathf.Sqrt(distSq);
            Vector3 dir = toPlayer / dist;

            float falloffScale = 1f;
            if (distanceFalloff > 0f)
            {
                float normalizedDist = dist / attractRadius;   // 0 = center, 1 = edge
                falloffScale = Mathf.Pow(1f - normalizedDist, distanceFalloff);
            }

            particleBuffer[i].velocity += (Vector3)(dir * force * falloffScale);
        }

        particles.SetParticles(particleBuffer, count);

        if (killCount > 0)
            OnParticlesKilled?.Invoke(killCount);
    }

    // =========================================================================
    // FIELD LOGIC
    // =========================================================================

    private bool DetermineAttracting()
    {
        if (breath.IsPaused)
        {
            return pauseBehavior switch
            {
                PauseBehavior.NoForce      => false,
                PauseBehavior.SustainLast  => lastWasAttracting,
                PauseBehavior.AlwaysAttract => true,
                _ => false
            };
        }

        bool result = breath.IsInhaling;
        lastWasAttracting = result;
        return result;
    }

    // =========================================================================
    // DEBUG GIZMOS
    // =========================================================================

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        // Attract radius
        Gizmos.color = isAttracting ? attractColor : new Color(0.5f, 0.5f, 0.5f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, attractRadius);

        // Kill radius
        Gizmos.color = killColor;
        Gizmos.DrawWireSphere(transform.position, killRadius);
        Gizmos.DrawSphere(transform.position, killRadius);
    }
}
