using UnityEngine;
using Holobiont;

namespace HolobiontLegacy
{

/*
 * ============================================================================
 * FIELD ABILITY - POLARITY (Attract / Repel)
 * ============================================================================
 *
 * WHAT THIS DOES:
 * Creates a force field around the character that affects nearby objects:
 * - Inhale: Attracts objects toward character
 * - Exhale: Repels objects away from character
 * - Depth: Controls force strength
 * - Pause: Sustains current polarity (optional)
 *
 * CATEGORY: Field Ability (stackable with other field abilities)
 *
 * REQUIRES: BreathSimulator reference
 *
 * AFFECTED OBJECTS: Must have Rigidbody2D and be on specified layer(s)
 *
 * ============================================================================
 */

public class FieldAbility_Polarity : MonoBehaviour
{
    // =========================================================================
    // REFERENCES
    // =========================================================================

    [Header("References")]
    [Tooltip("The breath simulator to read breath state from.")]
    [SerializeField] private BreathSimulator breath;

    // =========================================================================
    // FIELD SETTINGS
    // =========================================================================

    [Header("Field Settings")]
    [Tooltip("Radius of the force field.")]
    [SerializeField] private float fieldRadius = 5f;

    [Tooltip("Base force applied to objects in range.")]
    [SerializeField] private float baseForce = 10f;

    [Tooltip("Layer mask for objects affected by this field.")]
    [SerializeField] private LayerMask affectedLayers = ~0; // Everything by default

    [Tooltip("Maximum number of objects to affect per frame (performance).")]
    [SerializeField] private int maxAffectedObjects = 20;

    // =========================================================================
    // FORCE MODIFIERS
    // =========================================================================

    [Header("Force Curve")]
    [Tooltip("How force scales with distance. 0 = constant, 1 = linear falloff, 2 = inverse square.")]
    [Range(0f, 2f)]
    [SerializeField] private float distanceFalloff = 1f;

    [Tooltip("Minimum distance to prevent extreme forces when very close.")]
    [SerializeField] private float minDistance = 0.5f;

    // =========================================================================
    // PAUSE BEHAVIOR
    // =========================================================================

    [Header("Pause Behavior")]
    [Tooltip("What happens when breath is paused.")]
    [SerializeField] private PauseBehavior pauseBehavior = PauseBehavior.SustainLast;

    public enum PauseBehavior
    {
        NoForce,        // Field turns off during pause
        SustainLast,    // Keep last polarity (attract or repel)
        AlwaysAttract,  // Always attract during pause
        AlwaysRepel     // Always repel during pause
    }

    // =========================================================================
    // DEBUG
    // =========================================================================

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color attractColor = new Color(0.2f, 0.6f, 1f, 0.3f);
    [SerializeField] private Color repelColor = new Color(1f, 0.4f, 0.2f, 0.3f);

    // =========================================================================
    // RUNTIME STATE
    // =========================================================================

    private Collider2D[] hitBuffer;
    private int lastPolarity = 1; // 1 = attract, -1 = repel
    private int currentPolarity = 0;

    // =========================================================================
    // PUBLIC PROPERTIES
    // =========================================================================

    /// <summary>Current polarity: 1 = attracting, -1 = repelling, 0 = inactive.</summary>
    public int CurrentPolarity => currentPolarity;

    /// <summary>Is field currently attracting?</summary>
    public bool IsAttracting => currentPolarity > 0;

    /// <summary>Is field currently repelling?</summary>
    public bool IsRepelling => currentPolarity < 0;

    /// <summary>Current field radius.</summary>
    public float Radius => fieldRadius;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    private void Awake()
    {
        hitBuffer = new Collider2D[maxAffectedObjects];
    }

    private void FixedUpdate()
    {
        if (breath == null) return;

        // Determine current polarity
        currentPolarity = DeterminePolarity();

        if (currentPolarity == 0) return;

        // Find and affect nearby objects
        ApplyFieldForces();
    }

    // =========================================================================
    // POLARITY LOGIC
    // =========================================================================

    private int DeterminePolarity()
    {
        if (breath.IsPaused)
        {
            switch (pauseBehavior)
            {
                case PauseBehavior.NoForce:
                    return 0;
                case PauseBehavior.SustainLast:
                    return lastPolarity;
                case PauseBehavior.AlwaysAttract:
                    return 1;
                case PauseBehavior.AlwaysRepel:
                    return -1;
            }
        }

        if (breath.IsInhaling)
        {
            lastPolarity = 1;
            return 1; // Attract
        }
        else if (breath.IsExhaling)
        {
            lastPolarity = -1;
            return -1; // Repel
        }

        return 0;
    }

    private void ApplyFieldForces()
    {
        Vector2 center = transform.position;

        // Find all colliders in range
        int hitCount = Physics2D.OverlapCircleNonAlloc(center, fieldRadius, hitBuffer, affectedLayers);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = hitBuffer[i];

            // Skip self
            if (col.gameObject == gameObject) continue;

            // Need a Rigidbody2D to apply forces
            Rigidbody2D targetRb = col.attachedRigidbody;
            if (targetRb == null) continue;

            // Calculate force
            Vector2 toTarget = (Vector2)col.transform.position - center;
            float distance = toTarget.magnitude;

            if (distance < minDistance)
            {
                distance = minDistance;
            }

            // Direction: toward target for repel, toward self for attract
            Vector2 forceDir = toTarget.normalized * currentPolarity * -1; // -1 flips: attract pulls IN

            // Calculate force magnitude with falloff
            float forceMagnitude = CalculateForceMagnitude(distance);

            // Apply force
            targetRb.AddForce(forceDir * forceMagnitude);
        }
    }

    private float CalculateForceMagnitude(float distance)
    {
        // Scale by breath depth
        float depthScale = breath.Depth;

        // Scale by breath velocity (includes pause boost automatically)
        float velocityScale = Mathf.Abs(breath.Velocity) + 0.5f; // +0.5 so there's always some force

        // Distance falloff
        float distanceScale = 1f;
        if (distanceFalloff > 0)
        {
            float normalizedDist = distance / fieldRadius;
            distanceScale = Mathf.Pow(1f - normalizedDist, distanceFalloff);
        }

        return baseForce * depthScale * velocityScale * distanceScale;
    }

    // =========================================================================
    // DEBUG
    // =========================================================================

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        // Draw field radius
        Color color = currentPolarity > 0 ? attractColor :
                      currentPolarity < 0 ? repelColor :
                      Color.gray;
        color.a = 0.3f;

        Gizmos.color = color;
        Gizmos.DrawWireSphere(transform.position, fieldRadius);

        // Filled circle with lower alpha
        color.a = 0.1f;
        Gizmos.color = color;
        Gizmos.DrawSphere(transform.position, fieldRadius);
    }
}

}
