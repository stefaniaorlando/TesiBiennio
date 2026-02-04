using UnityEngine;

/*
 * ============================================================================
 * FIELD ABILITY - ABSORB (Consume and Grow)
 * ============================================================================
 *
 * WHAT THIS DOES:
 * When consumable objects touch the character, they are destroyed
 * and the character grows slightly.
 *
 * SETUP:
 * 1. Add this to the character
 * 2. Mark consumable objects with a tag (default: "Consumable")
 * 3. Ensure both have colliders (at least one must be trigger OR use collision)
 *
 * ============================================================================
 */

public class FieldAbility_Absorb : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Tag for objects that can be consumed.")]
    [SerializeField] private string consumableTag = "Consumable";

    [Tooltip("How much to grow per object consumed (added to scale).")]
    [SerializeField] private float growthAmount = 0.05f;

    [Tooltip("Maximum scale the character can reach.")]
    [SerializeField] private float maxScale = 3f;

    [Header("Runtime (Read Only)")]
    [SerializeField] private int totalConsumed = 0;

    /// <summary>Total objects consumed.</summary>
    public int TotalConsumed => totalConsumed;

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryConsume(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryConsume(collision.gameObject);
    }

    private void TryConsume(GameObject obj)
    {
        if (!obj.CompareTag(consumableTag)) return;

        // Destroy the consumed object
        Destroy(obj);

        // Grow
        float currentScale = transform.localScale.x;
        float newScale = Mathf.Min(currentScale + growthAmount, maxScale);
        transform.localScale = Vector3.one * newScale;

        totalConsumed++;
    }
}
