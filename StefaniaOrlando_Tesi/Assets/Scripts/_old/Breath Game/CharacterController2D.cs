using UnityEngine;

namespace HolobiontLegacy
{

/*
 * ============================================================================
 * CHARACTER CONTROLLER 2D - Base Physics Controller
 * ============================================================================
 *
 * WHAT THIS DOES:
 * Core physics vessel for a 2D character. Owns the Rigidbody2D, tracks state,
 * and provides clean hooks for abilities to modify movement.
 *
 * BY ITSELF: Does nothing. Just drifts with physics.
 * WITH ABILITIES: Abilities call AddForce(), SetDragModifier(), etc.
 *
 * SETUP:
 * 1. Add to GameObject with Rigidbody2D
 * 2. Add movement abilities as separate components
 * 3. Abilities reference this controller
 *
 * ============================================================================
 */

[RequireComponent(typeof(Rigidbody2D))]
public class CharacterController2D : MonoBehaviour
{
    // =========================================================================
    // SETTINGS
    // =========================================================================

    [Header("Base Physics")]
    [Tooltip("Base linear drag when no abilities are modifying it.")]
    [SerializeField] private float baseDrag = 2f;

    [Tooltip("Base mass of the character.")]
    [SerializeField] private float baseMass = 1f;

    // =========================================================================
    // RUNTIME STATE
    // =========================================================================

    private Rigidbody2D rb;
    private float dragModifier = 1f;
    private Vector2 lastNonZeroVelocity = Vector2.right;

    // =========================================================================
    // PUBLIC PROPERTIES - State for abilities to read
    // =========================================================================

    /// <summary>The Rigidbody2D (read-only access for abilities).</summary>
    public Rigidbody2D Rigidbody => rb;

    /// <summary>Current velocity.</summary>
    public Vector2 Velocity => rb.linearVelocity;

    /// <summary>Current speed (magnitude).</summary>
    public float Speed => rb.linearVelocity.magnitude;

    /// <summary>Last known movement direction (never zero).</summary>
    public Vector2 FacingDirection => lastNonZeroVelocity;

    /// <summary>Current position.</summary>
    public Vector2 Position => rb.position;

    /// <summary>Base drag value (before modifiers).</summary>
    public float BaseDrag => baseDrag;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.mass = baseMass;
        rb.linearDamping = baseDrag;
    }

    private void FixedUpdate()
    {
        // Track facing direction
        if (rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            lastNonZeroVelocity = rb.linearVelocity.normalized;
        }

        // Apply drag modifier
        rb.linearDamping = baseDrag * dragModifier;

        // Reset modifier each frame (abilities must re-apply)
        dragModifier = 1f;
    }

    // =========================================================================
    // PUBLIC API - For abilities to call
    // =========================================================================

    /// <summary>
    /// Add a force to the character. Use this instead of accessing Rigidbody directly.
    /// </summary>
    public void AddForce(Vector2 force, ForceMode2D mode = ForceMode2D.Force)
    {
        rb.AddForce(force, mode);
    }

    /// <summary>
    /// Set a drag multiplier for this frame. Values > 1 increase drag, < 1 decrease.
    /// Called each FixedUpdate by abilities. Resets to 1 each frame.
    /// </summary>
    public void SetDragModifier(float modifier)
    {
        dragModifier = modifier;
    }

    /// <summary>
    /// Add to the current drag modifier (stacks with other abilities).
    /// </summary>
    public void AddDragModifier(float additionalDrag)
    {
        dragModifier += additionalDrag / baseDrag;
    }

    /// <summary>
    /// Instantly set velocity (use sparingly - breaks physics feel).
    /// </summary>
    public void SetVelocity(Vector2 velocity)
    {
        rb.linearVelocity = velocity;
    }

    /// <summary>
    /// Stop all movement immediately.
    /// </summary>
    public void Stop()
    {
        rb.linearVelocity = Vector2.zero;
    }
}

}
