using UnityEngine;

namespace Holobiont
{
    /*
     * Destroys any 2D object that exits this collider. Generic culling volume
     * — knows nothing about flow, creatures, or the rest of the simulation.
     *
     * Setup:
     * 1. Empty GameObject, add a Collider2D (Box / Circle / Polygon).
     *    Reset() auto-sets isTrigger = true when this component is added.
     * 2. Size the collider larger than where objects spawn. Anything that
     *    exits the volume gets destroyed.
     * 3. Optionally restrict targets via the layer mask or tag filter.
     *
     * Limitation: only fires OnTriggerExit2D, so objects that never enter the
     * volume aren't culled by this script. Spawn inside the bounds.
     */

    [RequireComponent(typeof(Collider2D))]
    public class DespawnBoundary2D : MonoBehaviour
    {
        // ----- Filter -----
        [Header("Filter")]
        [Tooltip("Only objects on these layers are despawned. Default: Everything.")]
        [SerializeField] private LayerMask layers = ~0;

        [Tooltip("If non-empty, only objects with this tag are despawned.")]
        [SerializeField] private string requiredTag = "";

        // ----- Lifecycle -----
        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col) col.isTrigger = true;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!Matches(other)) return;
            var go = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;
            Destroy(go);
        }

        // ----- Private -----
        private bool Matches(Collider2D other)
        {
            if (((1 << other.gameObject.layer) & layers.value) == 0) return false;
            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return false;
            return true;
        }
    }
}
