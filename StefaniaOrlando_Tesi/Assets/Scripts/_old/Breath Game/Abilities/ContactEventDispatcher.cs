using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HolobiontLegacy
{

/*
 * ============================================================================
 * CONTACT EVENT DISPATCHER - Tag-Based Collision Events
 * ============================================================================
 *
 * WHAT THIS DOES:
 * Detects collisions and triggers with other objects, checks their tag,
 * and fires a UnityEvent<GameObject> for each matching tag.
 * The contacted GameObject is passed as parameter so wired methods
 * can act on it (destroy, read data, apply effects, etc.).
 *
 * SETUP:
 * 1. Add this component to the player GameObject
 * 2. In the Inspector, add entries to the Contact Events list
 * 3. For each entry:
 *    - Set the Tag (e.g. "Consumable", "Damage")
 *    - Wire the OnContact event to desired methods
 * 4. Ensure both player and target objects have Collider2D
 *    (at least one must be a trigger, or use regular collisions)
 *
 * EXAMPLE:
 *   Entry 0: Tag = "Consumable"  -> OnContact -> DestroyObject(GameObject)
 *   Entry 1: Tag = "Damage"      -> OnContact -> TakeDamage(GameObject)
 *
 * ============================================================================
 */

/// <summary>
/// Maps a tag to a UnityEvent that fires when contact occurs with that tag.
/// </summary>
[System.Serializable]
public class TagContactEvent
{
    [Tooltip("The tag to match on the contacted object.")]
    public string tag;

    [Tooltip("Event fired when contact occurs. Receives the contacted GameObject.")]
    public UnityEvent<GameObject> OnContact;
}

/// <summary>
/// Dispatches UnityEvents based on the tag of contacted objects.
/// Handles both trigger and collision contacts.
/// </summary>
public class ContactEventDispatcher : MonoBehaviour
{
    // =========================================================================
    // SETTINGS
    // =========================================================================

    [Header("Tag -> Event Mappings")]
    [Tooltip("List of tag-to-event mappings. Add one entry per tag you want to react to.")]
    [SerializeField] private List<TagContactEvent> contactEvents = new List<TagContactEvent>();

    // =========================================================================
    // COLLISION DETECTION
    // =========================================================================

    private void OnTriggerEnter2D(Collider2D other)
    {
        DispatchContact(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        DispatchContact(collision.gameObject);
    }

    // =========================================================================
    // DISPATCH LOGIC
    // =========================================================================

    private void DispatchContact(GameObject obj)
    {
        for (int i = 0; i < contactEvents.Count; i++)
        {
            TagContactEvent entry = contactEvents[i];

            if (!string.IsNullOrEmpty(entry.tag) && obj.CompareTag(entry.tag))
            {
                entry.OnContact?.Invoke(obj);
            }
        }
    }
}

}
