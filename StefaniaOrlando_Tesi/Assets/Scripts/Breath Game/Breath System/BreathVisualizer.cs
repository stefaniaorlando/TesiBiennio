using UnityEngine;
using UnityEngine.UI;

/*
 * ============================================================================
 * BREATH VISUALIZER - UI Display
 * ============================================================================
 *
 * WHAT THIS DOES:
 * This component connects the breath simulation to Unity UI elements.
 * It reads data from BreathSimulator and updates sliders to visualize:
 * - Breath (lung fill level - the main oscillating bar)
 * - Depth (breathing amplitude - inverted: deep = low, shallow = high)
 * - Stamina (energy remaining)
 * - Recovery progress (when recovering)
 *
 * HOW TO SET UP:
 * 1. Create UI Sliders in your Canvas:
 *    - Breath: Vertical slider (0-1), shows breathing wave
 *    - Depth: Vertical slider (0-1), shows current depth (inverted!)
 *    - Stamina: Horizontal slider (0-1), shows energy level
 *    - Recovery: Horizontal slider (0-1), shows recovery countdown
 *
 * 2. For each slider:
 *    - Set Min Value = 0, Max Value = 1
 *    - Disable "Interactable" (these are display-only)
 *
 * 3. Drag the sliders into the slots on this component
 *
 * 4. (Optional) Assign the breath slider's fill Image to
 *    "breathFill" for color change during recovery
 *
 * NOTE ON DEPTH INVERSION:
 * The depth slider is INVERTED so that:
 * - Deep breathing (diaphragmatic) shows at the BOTTOM
 * - Shallow breathing (thoracic) shows at the TOP
 * This matches the physical metaphor: deep = lower in the body.
 *
 * OPTIONAL FEATURES:
 * - Recovery slider auto-hides when not recovering
 * - Breath bar can change color during recovery
 *
 * ============================================================================
 */

/// <summary>
/// Visualizes breath state using UI sliders.
/// Reads all data from the central BreathSimulator API.
/// </summary>
public class BreathVisualizer : MonoBehaviour
{
    // =========================================================================
    // REFERENCES
    // =========================================================================

    [Header("Main Reference")]
    [Tooltip("The BreathSimulator component. This is the ONLY reference you need - it provides all breath data.")]
    [SerializeField] private BreathSimulator breath;

    // =========================================================================
    // UI ELEMENTS - Drag your sliders here
    // =========================================================================

    [Header("UI Sliders")]
    [Tooltip("Vertical slider showing current lung fill (0 = empty, 1 = full). This oscillates with the breath cycle.")]
    [SerializeField] private Slider breathSlider;

    [Tooltip("Vertical slider showing current breath depth. INVERTED: 0 = deep (diaphragmatic), 1 = shallow (thoracic).")]
    [SerializeField] private Slider depthSlider;

    [Tooltip("Horizontal slider showing current stamina level (0 = depleted, 1 = full).")]
    [SerializeField] private Slider staminaSlider;

    [Tooltip("Horizontal slider showing recovery progress. Only visible during recovery. (1 = just started, 0 = almost done)")]
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
    // UPDATE - Runs every frame to sync UI with breath state
    // =========================================================================

    private void Update()
    {
        // Safety check - do nothing if BreathSimulator isn't assigned
        if (breath == null)
            return;

        bool isRecovering = breath.IsRecovering;

        // Update breath slider (the main breathing visual)
        if (breathSlider != null)
        {
            breathSlider.value = breath.Displacement;
        }

        // Update breath color based on recovery state
        if (breathFill != null)
        {
            breathFill.color = isRecovering ? recoveryColor : normalColor;
        }

        // Update depth slider (INVERTED: deep = 0, shallow = 1)
        if (depthSlider != null)
        {
            // Invert the value: 1 - normalizedDepth
            // So when depth is high (deep breathing), slider shows low
            // And when depth is low (shallow breathing), slider shows high
            depthSlider.value = 1f - breath.NormalizedDepth;
        }

        // Update stamina slider
        if (staminaSlider != null)
        {
            staminaSlider.value = breath.Stamina;
        }

        // Update recovery slider (only visible during recovery)
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
