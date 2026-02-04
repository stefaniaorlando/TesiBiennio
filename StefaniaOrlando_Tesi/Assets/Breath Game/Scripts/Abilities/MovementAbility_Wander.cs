using UnityEngine;

/*
 * ============================================================================
 * MOVEMENT ABILITY - WANDER (Autonomous)
 * ============================================================================
 *
 * WHAT THIS DOES:
 * Applies smooth, organic wandering movement using Perlin noise.
 * No player input - purely autonomous drifting.
 *
 * CATEGORY: Movement Ability (mutually exclusive with other movement abilities)
 *
 * ============================================================================
 */

public class MovementAbility_Wander : MonoBehaviour
{
    // =========================================================================
    // REFERENCES
    // =========================================================================

    [Header("References")]
    [Tooltip("The character controller to apply forces to.")]
    [SerializeField] private CharacterController2D controller;

    // =========================================================================
    // WANDER SETTINGS
    // =========================================================================

    [Header("Wander Settings")]
    [Tooltip("Base force applied for wandering movement.")]
    [SerializeField] private float wanderForce = 5f;

    [Tooltip("How quickly the wander direction changes. Lower = smoother paths.")]
    [SerializeField] private float noiseSpeed = 0.3f;

    [Tooltip("Scale of the noise sampling. Affects path curvature.")]
    [SerializeField] private float noiseScale = 1f;

    // =========================================================================
    // RUNTIME STATE
    // =========================================================================

    private Vector2 noiseSeed;
    private float noiseTime;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<CharacterController2D>();
        }

        // Random seed for each axis
        noiseSeed = new Vector2(
            Random.Range(0f, 1000f),
            Random.Range(0f, 1000f)
        );
    }

    private void FixedUpdate()
    {
        if (controller == null) return;

        ApplyWanderForce();
    }

    // =========================================================================
    // WANDER LOGIC
    // =========================================================================

    private void ApplyWanderForce()
    {
        // Advance noise time
        noiseTime += noiseSpeed * Time.fixedDeltaTime;

        // Sample noise for each axis independently (-1 to +1)
        float noiseX = Mathf.PerlinNoise(noiseSeed.x + noiseTime * noiseScale, 0f) * 2f - 1f;
        float noiseY = Mathf.PerlinNoise(noiseSeed.y + noiseTime * noiseScale, 0f) * 2f - 1f;

        Vector2 wanderDirection = new Vector2(noiseX, noiseY).normalized;

        controller.AddForce(wanderDirection * wanderForce);
    }
}
