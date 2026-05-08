using UnityEngine;
using Holobiont;

namespace HolobiontLegacy
{

/*
 * ============================================================================
 * MOVEMENT ABILITY - PULSE (Jellyfish)
 * ============================================================================
 *
 * WHAT THIS DOES:
 * Modulates movement based on breath phase like a jellyfish:
 * - Inhale: Contracts, increases drag (slows down)
 * - Exhale: Bursts forward in facing direction
 *
 * CATEGORY: Movement Ability (mutually exclusive with other movement abilities)
 *
 * REQUIRES: BreathSimulator reference
 *
 * ============================================================================
 */

public class MovementAbility_Pulse : MonoBehaviour
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
    // PULSE SETTINGS
    // =========================================================================

    [Header("Contraction (Inhale)")]
    [Tooltip("Drag multiplier during inhale. Higher = more slowdown.")]
    [SerializeField] private float inhaleDragMultiplier = 2f;

    [Header("Burst (Exhale)")]
    [Tooltip("Force applied during exhale burst.")]
    [SerializeField] private float burstForce = 8f;

    [Tooltip("How much burst aligns with facing direction. 0 = random, 1 = fully aligned.")]
    [Range(0f, 1f)]
    [SerializeField] private float directionAlignment = 0.7f;

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

    private void FixedUpdate()
    {
        if (controller == null || breath == null) return;

        if (breath.IsPaused)
        {
            // Pause = no modification, drift naturally
            return;
        }

        if (breath.IsInhaling)
        {
            ApplyContraction();
        }
        else if (breath.IsExhaling)
        {
            ApplyBurst();
        }
    }

    // =========================================================================
    // PULSE LOGIC
    // =========================================================================

    private void ApplyContraction()
    {
        // Increase drag scaled by breath depth
        float dragMod = Mathf.Lerp(1f, inhaleDragMultiplier, breath.Depth);
        controller.SetDragModifier(dragMod);
    }

    private void ApplyBurst()
    {
        // Calculate burst direction
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        Vector2 burstDir = Vector2.Lerp(randomDir, controller.FacingDirection, directionAlignment);

        // Scale force by breath velocity and depth (boost is already in Velocity)
        float strength = Mathf.Abs(breath.Velocity) * burstForce * breath.Depth;

        controller.AddForce(burstDir.normalized * strength);
    }
}

}
