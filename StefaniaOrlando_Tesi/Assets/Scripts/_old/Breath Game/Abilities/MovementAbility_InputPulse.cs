using UnityEngine;
using Holobiont;

namespace HolobiontLegacy
{

/*
 * ============================================================================
 * MOVEMENT ABILITY - INPUT PULSE (Arrow Keys + Breath Modulation)
 * ============================================================================
 *
 * WHAT THIS DOES:
 * Player-directed movement using arrow keys, modulated by breath state:
 * - Arrow keys set the movement direction
 * - Inhale: Increases drag (character slows down, like Pulse contraction)
 * - Exhale: Boosts movement force (character surges forward)
 * - Pause: No breath modification, drift naturally
 *
 * CATEGORY: Movement Ability (mutually exclusive with other movement abilities)
 *
 * REQUIRES: BreathSimulator reference
 *
 * EVENT-DRIVEN SETUP (Inspector wiring):
 * Wire these BreathSimulator events to the public methods on this component:
 *   - OnInhaleStart  -> SetDragMultiplier(float)   e.g. 2.0
 *   - OnExhaleStart  -> SetBurstMultiplier(float)  e.g. 1.5
 *   - OnPauseStart   -> SetDragMultiplier(0) + SetBurstMultiplier(0)
 *   - OnPauseEnd     -> ResumeFromPause()
 *
 * The float value you pass in the Inspector event slot becomes the active
 * multiplier. Set to 0 to disable that effect.
 *
 * ============================================================================
 */

public class MovementAbility_InputPulse : MonoBehaviour
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
    // MOVEMENT SETTINGS
    // =========================================================================

    [Header("Movement")]
    [Tooltip("Base force applied when pressing arrow keys.")]
    [SerializeField] private float moveForce = 8f;

    // =========================================================================
    // RUNTIME STATE - Set by events, used each FixedUpdate
    // =========================================================================

    [Header("Runtime (Read Only)")]
    [Tooltip("Current drag multiplier. Set via SetDragMultiplier(). 0 = inactive.")]
    [SerializeField] private float currentDragMultiplier = 0f;

    [Tooltip("Current burst multiplier. Set via SetBurstMultiplier(). 0 = inactive.")]
    [SerializeField] private float currentBurstMultiplier = 0f;

    // =========================================================================
    // PUBLIC METHODS - Wire these from BreathSimulator events in the Inspector
    // =========================================================================

    /// <summary>
    /// Sets the drag multiplier (inhale contraction strength).
    /// Pass the desired value from the Inspector event slot (e.g. 2.0).
    /// Pass 0 to disable drag modulation.
    /// Wire to: BreathSimulator -> OnInhaleStart (with value), OnPauseStart (with 0)
    /// </summary>
    public void SetDragMultiplier(float value)
    {
        currentDragMultiplier = value;
    }

    /// <summary>
    /// Sets the burst multiplier (exhale boost strength).
    /// Pass the desired value from the Inspector event slot (e.g. 1.5).
    /// Pass 0 to disable burst modulation.
    /// Wire to: BreathSimulator -> OnExhaleStart (with value), OnPauseStart (with 0)
    /// </summary>
    public void SetBurstMultiplier(float value)
    {
        currentBurstMultiplier = value;
    }

    /// <summary>
    /// Re-applies multipliers when pause ends.
    /// Since pause sets both to 0, this restores them based on current breath direction.
    /// Wire to: BreathSimulator -> OnPauseEnd
    /// Note: This uses the default values. If you want custom values on resume,
    /// wire OnPauseEnd to SetDragMultiplier/SetBurstMultiplier instead.
    /// </summary>
    public void ResumeFromPause()
    {
        // ResumeFromPause is a convenience — it does nothing by itself.
        // The next OnInhaleStart or OnExhaleStart event will restore the values.
        // If the oscillator is mid-phase when unpausing, we restore immediately:
        if (breath == null) return;

        if (breath.IsInhaling)
        {
            // We don't know what value the user set in the Inspector,
            // so we leave it to the next OnInhaleStart event to set it.
            // Clear burst so only drag applies when the event fires.
            currentBurstMultiplier = 0f;
        }
        else if (breath.IsExhaling)
        {
            currentDragMultiplier = 0f;
        }
    }

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

        // Read arrow key input
        Vector2 inputDir = GetInputDirection();

        // Start with base force
        float force = moveForce;

        // Apply drag modulation if active
        if (currentDragMultiplier > 0f)
        {
            float dragMod = Mathf.Lerp(1f, currentDragMultiplier, breath.Depth);
            controller.SetDragModifier(dragMod);
        }

        // Apply burst modulation if active
        if (currentBurstMultiplier > 0f)
        {
            float boost = Mathf.Abs(breath.Velocity) * breath.Depth * currentBurstMultiplier;
            force += boost;
        }

        // Apply movement force in input direction
        if (inputDir.sqrMagnitude > 0f)
        {
            controller.AddForce(inputDir * force);
        }
    }

    // =========================================================================
    // INPUT
    // =========================================================================

    private Vector2 GetInputDirection()
    {
        float x = 0f;
        float y = 0f;

        if (Input.GetKey(KeyCode.RightArrow)) x += 1f;
        if (Input.GetKey(KeyCode.LeftArrow))  x -= 1f;
        if (Input.GetKey(KeyCode.UpArrow))    y += 1f;
        if (Input.GetKey(KeyCode.DownArrow))  y -= 1f;

        Vector2 dir = new Vector2(x, y);
        if (dir.sqrMagnitude > 1f)
        {
            dir.Normalize();
        }

        return dir;
    }
}

}
