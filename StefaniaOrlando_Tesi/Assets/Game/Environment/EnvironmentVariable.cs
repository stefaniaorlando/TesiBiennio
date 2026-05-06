namespace Holobiont
{
    /*
     * Identifies one of the four environmental variables.
     * [Flags] so a single value can address one variable, and an event config
     * can address several at once with one mask.
     *
     * Declaration order — Temp, Light, Humidity, Toxicity — is the canonical
     * UI/inspector order. Bit values are kept stable across order changes so
     * existing serialized masks are not corrupted.
     */

    [System.Flags]
    public enum EnvironmentVariable
    {
        None     = 0,
        Temp     = 1 << 0,
        Light    = 1 << 3,
        Humidity = 1 << 1,
        Toxicity = 1 << 2,
    }
}
