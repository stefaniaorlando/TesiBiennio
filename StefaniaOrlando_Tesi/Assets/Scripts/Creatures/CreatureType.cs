namespace Holobiont
{
    /*
     * Three functional categories. Each individual creature has one type and a
     * parametric affinity, both set at spawn and immutable afterwards.
     */

    public enum CreatureType
    {
        Nutrici, // Energy producers — convert external drive (eventually breath) into energy
        Scudo,   // Environmental shields — contribute to the holobiont's resistance profile
        Hub      // Metabolic infrastructure — adds energy capacity and creature slots
    }
}
