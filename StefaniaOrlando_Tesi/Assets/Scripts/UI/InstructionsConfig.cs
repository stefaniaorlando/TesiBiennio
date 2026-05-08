using UnityEngine;

namespace Holobiont
{
    /*
     * Source of the Controls and How to Play body strings shown by the start
     * menu and the pause menu. Single asset, two consumers — edit the asset and
     * both menus update on next enable. TMP rich-text tags (<b>, <color>, …)
     * pass through unchanged.
     */
    [CreateAssetMenu(fileName = "InstructionsConfig", menuName = "Game/Instructions Config")]
    public class InstructionsConfig : ScriptableObject
    {
        [Header("Controls")]
        [TextArea(6, 24)]
        [SerializeField] private string controlsText =
@"<b>W / S</b> — Faster / slower breathing rhythm.
<b>D / A</b> — Deeper / shallower breath (grows or shrinks the field).
<b>Space (hold)</b> — Hold the breath at the current point in the cycle.
  • hold mid-exhale → bonds any unbound creatures inside the field
  • hold mid-inhale → releases your most-stressed bonded creature
  • release into the opposite phase to boost the next breath
<b>Esc</b> — Pause menu.

Pushing parameters off baseline or holding too long drains stamina — bottom out and you're locked into a forced recovery with no input.";

        [Header("How to play")]
        [TextArea(6, 24)]
        [SerializeField] private string howToPlayText =
@"You are a holobiont — a colony bound by breath. Survive as long as you can.

Your field breathes on its own. On every exhale it pulls free creatures toward you; on every inhale it pushes them away. Shape the rhythm with WASD — deeper breaths reach further, faster ones cycle quicker — and hold Space at the right moment to bond a creature (mid-exhale) or release one (mid-inhale). Three types matter:

  • <b>Nutrici</b> — convert your breath into energy.
  • <b>Scudo</b> — shield you from environmental stress (temperature, light, humidity, toxicity).
  • <b>Hub</b> — raise your energy capacity and creature slots.

Energy drains every second from upkeep, creature stress, and mismatch between your resistances and the drifting environment. Bond Nutrici to stay in the black, Scudo to survive shifts, and shed stressed creatures before they die. If energy hits zero, the colony cascades and dies.";

        public string ControlsText  => controlsText;
        public string HowToPlayText => howToPlayText;
    }
}
