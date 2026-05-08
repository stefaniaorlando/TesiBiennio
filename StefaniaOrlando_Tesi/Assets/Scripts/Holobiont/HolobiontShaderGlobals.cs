using UnityEngine;

namespace Holobiont
{
    /*
     * Single writer for shader globals consumed by CreatureSDF and
     * SymbiosisTendrils. Keeping all global uniforms on one component avoids
     * duplicate writes and races. Currently publishes:
     *
     *   _HoloBreathPhase  — breath phase in [-1,+1] (full inhale .. full exhale)
     *
     * Globals are world-state, not per-material — set once per frame via
     * Shader.SetGlobalFloat. Independent of any specific renderer; doesn't
     * touch MaterialPropertyBlocks (that's CreatureView's job).
     *
     * Additional environmental globals (toxicity, light, etc.) can be added
     * here as the shaders start consuming them — keep this component as the
     * single point of truth.
     */
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-20)]
    public class HolobiontShaderGlobals : MonoBehaviour
    {
        // ----- Sources -----
        [Header("Sources")]
        [Tooltip("Any MonoBehaviour implementing IBreathInput. BreathSimulator (full system) or KeyboardBreathInput (simple two-key mock) both work. " +
                 "Note: BreathInputHandler is just the keyboard reader and does NOT implement IBreathInput — wire its parent BreathSimulator instead. " +
                 "When null, _HoloBreathPhase falls back to 0.")]
        [SerializeField] private MonoBehaviour breathInputBehaviour;

        // ----- Cached IDs -----
        private static readonly int IDBreathPhase = Shader.PropertyToID("_HoloBreathPhase");

        private IBreathInput breath;

        // ----- Lifecycle -----
        private void OnEnable()
        {
            breath = breathInputBehaviour as IBreathInput;
            if (breathInputBehaviour && breath is null)
            {
                Debug.LogError(
                    $"{nameof(HolobiontShaderGlobals)}: {breathInputBehaviour.GetType().Name} " +
                    $"does not implement {nameof(IBreathInput)}.", this);
            }
        }

        private void Update()
        {
            float phase = (breathInputBehaviour && breath is not null) ? breath.Phase : 0f;
            Shader.SetGlobalFloat(IDBreathPhase, phase);
        }
    }
}
