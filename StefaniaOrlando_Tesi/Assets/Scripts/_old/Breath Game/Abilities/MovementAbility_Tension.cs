using UnityEngine;
using Holobiont;

namespace HolobiontLegacy
{

/*
 * ============================================================================
 * MOVEMENT ABILITY - TENSION (Bow and Arrow)
 * ============================================================================
 *
 * WHAT THIS DOES:
 * Charges movement energy during inhale, releases on exhale:
 * - Inhale: Builds tension, dampens movement
 * - Exhale: Releases stored energy as burst
 * - Deeper/longer inhale = bigger release
 *
 * CATEGORY: Movement Ability (mutually exclusive with other movement abilities)
 *
 * REQUIRES: BreathSimulator reference
 *
 * ============================================================================
 */

public class MovementAbility_Tension : MonoBehaviour
{
    // =========================================================================
    // REFERENCES
    // =========================================================================

    [Header("References")]
    [Tooltip("The character controller to apply forces to.")]
    [SerializeField] private CharacterController2D controller;

    [Tooltip("The breath simulator to read breath state from.")]
    [SerializeField] private BreathSimulator breath;

    // =========================================================================
    // CHARGE SETTINGS
    // =========================================================================

    [Header("Charge (Inhale)")]
    [Tooltip("How fast tension builds during inhale.")]
    [SerializeField] private float chargeRate = 2f;

    [Tooltip("Maximum tension that can be stored.")]
    [SerializeField] private float maxTension = 1f;

    [Tooltip("Drag multiplier while charging.")]
    [SerializeField] private float chargeDragMultiplier = 2.5f;

    // =========================================================================
    // RELEASE SETTINGS
    // =========================================================================

    [Header("Release (Exhale)")]
    [Tooltip("Force multiplier when releasing tension.")]
    [SerializeField] private float releaseForce = 15f;

    [Tooltip("How quickly tension depletes. Higher = quick burst, Lower = sustained push.")]
    [SerializeField] private float releaseRate = 3f;

    [Tooltip("How much release aligns with facing direction. 0 = random, 1 = fully aligned.")]
    [Range(0f, 1f)]
    [SerializeField] private float directionAlignment = 0.5f;

    // =========================================================================
    // RUNTIME STATE
    // =========================================================================

    [Header("Runtime (Read Only)")]
    [SerializeField] private float currentTension = 0f;

    private Vector2 chargeDirection;

    // =========================================================================
    // PUBLIC PROPERTIES
    // =========================================================================

    /// <summary>Current stored tension (0 to maxTension).</summary>
    public float Tension => currentTension;

    /// <summary>Tension as 0-1 value.</summary>
    public float NormalizedTension => maxTension > 0 ? currentTension / maxTension : 0f;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<CharacterController2D>();
        }
    }

    private void OnDisable()
    {
        currentTension = 0f;
    }

    private void FixedUpdate()
    {
        if (controller == null || breath == null) return;

        if (breath.IsPaused)
        {
            // Pause = hold current tension, no drag modification
            return;
        }

        if (breath.IsInhaling)
        {
            ChargeTension();
        }
        else if (breath.IsExhaling)
        {
            ReleaseTension();
        }
    }

    // =========================================================================
    // TENSION LOGIC
    // =========================================================================

    private void ChargeTension()
    {
        // Increase drag while charging
        controller.SetDragModifier(chargeDragMultiplier);

        // Build tension based on breath depth
        float chargeAmount = chargeRate * breath.Depth * Time.fixedDeltaTime;
        currentTension = Mathf.Min(currentTension + chargeAmount, maxTension);

        // Slowly align charge direction with facing
        chargeDirection = Vector2.Lerp(chargeDirection, controller.FacingDirection, 0.1f);
    }

    private void ReleaseTension()
    {
        if (currentTension <= 0f) return;

        // Calculate how much tension to release this frame
        float releaseAmount = releaseRate * Time.fixedDeltaTime;
        float tensionToRelease = Mathf.Min(currentTension, releaseAmount);

        // Calculate release direction
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        Vector2 releaseDir = Vector2.Lerp(randomDir, chargeDirection, directionAlignment);

        // Apply impulse proportional to tension released
        float force = tensionToRelease * releaseForce * breath.Depth;
        controller.AddForce(releaseDir.normalized * force, ForceMode2D.Impulse);

        // Deplete tension
        currentTension = Mathf.Max(0f, currentTension - tensionToRelease);
    }
}

}
