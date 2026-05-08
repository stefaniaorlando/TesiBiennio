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
        // ----- References -----
        [Header("Main Reference")]
        [Tooltip("The BreathSimulator providing all breath data.")]
        [SerializeField] private BreathSimulator breath;

        // ----- UI -----
        [Header("UI Sliders")]
        [Tooltip("Vertical slider showing current lung fill (0 = empty, 1 = full).")]
        [SerializeField] private Slider breathSlider;

        [Tooltip("Vertical slider showing current breath depth. INVERTED: 0 = deep, 1 = shallow.")]
        [SerializeField] private Slider depthSlider;

        [Tooltip("Horizontal slider showing current stamina (0 = depleted, 1 = full).")]
        [SerializeField] private Slider staminaSlider;

        [Tooltip("Horizontal slider showing recovery progress. Auto-hidden when not recovering.")]
        [SerializeField] private Slider recoverySlider;

        // ----- Optional Visual Feedback -----
        [Header("Optional Recovery Feedback")]
        [Tooltip("(Optional) The fill Image of the breath slider. If assigned, changes color during recovery.")]
        [SerializeField] private Image breathFill;

        [Tooltip("Color of breath bar during normal breathing.")]
        [SerializeField] private Color normalColor = Color.white;

        [Tooltip("Color of breath bar during recovery (grayed out to show loss of control).")]
        [SerializeField] private Color recoveryColor = Color.gray;

        // ----- Lifecycle -----
        private void Update()
        {
            if (!breath) return;

            bool isRecovering = breath.InRecovery;

            if (breathSlider)
                breathSlider.value = breath.Displacement;

            if (breathFill)
                breathFill.color = isRecovering ? recoveryColor : normalColor;

            // Depth bar inverts so deep breathing reads as low and shallow as high.
            if (depthSlider)
                depthSlider.value = 1f - breath.Depth;

            if (staminaSlider)
                staminaSlider.value = breath.Stamina;

            if (recoverySlider)
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
