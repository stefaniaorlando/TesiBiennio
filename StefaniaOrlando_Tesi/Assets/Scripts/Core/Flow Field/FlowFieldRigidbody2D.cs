using UnityEngine;

namespace Holobiont
{
    /*
     * Drift a Rigidbody2D on the global FlowField. Generic test driver — the
     * eventual CreatureSimulation will subsume this for unbound creatures.
     *
     * Setup: GameObject with a Rigidbody2D (gravityScale 0; some linear damping
     * — try 0.5..2 — to keep velocities bounded). Add this component. A
     * FlowField must exist in the scene.
     */

    [RequireComponent(typeof(Rigidbody2D))]
    public class FlowFieldRigidbody2D : MonoBehaviour
    {
        // ----- Config -----
        [Header("Force")]
        [Tooltip("Multiplier on the flow vector when applied as a force each FixedUpdate.")]
        [SerializeField, Min(0f)] private float forceMultiplier = 5f;

        // ----- State -----
        private Rigidbody2D rb;

        // ----- Lifecycle -----
        private void Awake() => rb = GetComponent<Rigidbody2D>();

        private void FixedUpdate()
        {
            var clock = GameClock.Instance;
            if (clock && clock.IsPaused) return;

            var flow = FlowField.Instance;
            if (!flow || !rb) return;
            Vector2 v = flow.GetFlowAtPosition(rb.position);
            rb.AddForce(v * forceMultiplier);
        }
    }
}
