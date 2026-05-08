using System.Collections.Generic;

namespace Holobiont
{
    /*
     * Pure data record for the holobiont's runtime state — no events, no Unity
     * dependencies. Outward-facing events live on HolobiontManager so they can
     * carry the dual C# event + UnityEvent pattern (UnityEvents need a serialized
     * Object owner; a POCO can't host them).
     *
     * HolobiontManager owns one instance and is the only writer. Other systems
     * read through HolobiontManager.State.
     */
    public class HolobiontState
    {
        // ----- Energy -----
        public float Energy;
        public float MaxEnergy;
        public float NetEnergyFlow; // last tick's inflow - drain, per second
        public float EnergyNormalized => MaxEnergy > 0f ? Energy / MaxEnergy : 0f;

        // ----- Composition -----
        public readonly List<Creature> BondedCreatures = new List<Creature>();
        public int CreatureCount => BondedCreatures.Count;
        public int NutriciCount;
        public int ScudoCount;
        public int HubCount;
        public int CarryingCapacity;
        public bool AtCapacity => CreatureCount >= CarryingCapacity;

        // ----- Resistance -----
        public ResistanceProfile Resistance;

        // ----- Phase -----
        public HolobiontPhase Phase = HolobiontPhase.Stable;

        // ----- Drives -----
        // Frequency-shaped multiplier on both inflow and drain (design decision #6:
        // breath rate is tempo, not free power). 1 = baseline; ~0.5..2 typical.
        public float MetabolicRate = 1f;
    }
}
