using UnityEngine;
using UnityEngine.UI;

namespace Holobiont
{
    /*
     * Connects breath state to UI sliders. Reads everything from BreathSimulator;
     * writes nothing back. Visualizer ticks on Time.deltaTime (per project
     * convention, view scripts keep updating while the game is paused).
     */
    public class BreathVisualizer : MonoBehaviour
    {
        // =========================================================================
        // REFERENCES
        // =========================================================================

        [Header("Main Reference")]
        [Tooltip("The BreathSimulator providing all breath data.")]
        [SerializeField] private BreathSimulator breath;

        // =========================================================================
        // UI ELEMENTS
        // =========================================================================

        [Header("UI Sliders")]
        [Tooltip("Vertical slider showing current lung fill (0 = empty, 1 = full).")]
        [SerializeField] private Slider breathSlider;

        [Tooltip("Vertical slider showing current breath depth. INVERTED: 0 = deep, 1 = shallow.")]
        [SerializeField] private Slider depthSlider;

        [Tooltip("Horizontal slider showing current stamina (0 = depleted, 1 = full).")]
        [SerializeField] private Slider staminaSlider;

        [Tooltip("Horizontal slider showing recovery progress. Auto-hidden when not recovering.")]
        [SerializeField] private Slider recoverySlider;

        // =========================================================================
        // OPTIONAL VISUAL FEEDBACK
        // =========================================================================

        [Header("Optional Recovery Feedback")]
        [Tooltip("(Optional) The fill Image of the breath slider. If assigned, changes color during recovery.")]
        [SerializeField] private Image breathFill;

        [Tooltip("Color of breath bar during normal breathing.")]
        [SerializeField] private Color normalColor = Color.white;

        [Tooltip("Color of breath bar during recovery (grayed out to show loss of control).")]
        [SerializeField] private Color recoveryColor = Color.gray;

        // =========================================================================
        // UPDATE
        // =========================================================================

        private void Update()
        {
            if (breath == null) return;

            bool isRecovering = breath.InRecovery;

            if (breathSlider != null)
                breathSlider.value = breath.Displacement;

            if (breathFill != null)
                breathFill.color = isRecovering ? recoveryColor : normalColor;

            // Depth bar inverts so deep breathing reads as low and shallow as high.
            if (depthSlider != null)
                depthSlider.value = 1f - breath.Depth;

            if (staminaSlider != null)
                staminaSlider.value = breath.Stamina;

            if (recoverySlider != null)
            {
                if (isRecovering)
                {
                    recoverySlider.gameObject.SetActive(true);
                    recoverySlider.value = breath.RecoveryProgress;
                }
                else
                {
                    recoverySlider.gameObject.SetActive(false);
                }
            }
        }
    }
}
