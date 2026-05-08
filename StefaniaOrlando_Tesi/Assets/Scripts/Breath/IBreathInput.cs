namespace Holobiont
{
    /*
     * Data contract between any breath source (keyboard simulator, hardware sensor,
     * mock for testing) and the holobiont/creature/environment systems that consume
     * breath. See HOLOBIONT_SYSTEM_DESIGN.md "BreathInput".
     *
     * All values are normalized so consumers never need to know the underlying tuning.
     */
    public interface IBreathInput
    {
        /// <summary>Breath depth in [0,1]. 0 = shallowest, 1 = deepest.</summary>
        float Depth { get; }

        /// <summary>Breath frequency in [0,1]. 0 = slowest allowed, 1 = fastest allowed.</summary>
        float Frequency { get; }

        /// <summary>Signed phase. -1 = full inhale (lungs full), +1 = full exhale (lungs empty).</summary>
        float Phase { get; }

        /// <summary>True while the player is actively holding their breath.</summary>
        bool IsHolding { get; }

        /// <summary>True while the player is holding after an exhale (lungs empty hold). Drives capture/bond on the holobiont.</summary>
        bool IsExhaleHold { get; }

        /// <summary>True while the player is holding after an inhale (lungs full hold). Drives shed/release on the holobiont.</summary>
        bool IsInhaleHold { get; }

        /// <summary>Stamina in [0,1]. 1 = fresh, 0 = depleted.</summary>
        float Stamina { get; }

        /// <summary>True while in the recovery refractory after stamina depletion. Input is suppressed; parameters drift to compensate.</summary>
        bool InRecovery { get; }
    }
}
