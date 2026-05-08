namespace Holobiont
{
    /*
     * Lifecycle phases of the holobiont's energy economy.
     *   Stable         — energy > 0, net energy flow >= 0.
     *   Declining      — energy > 0, net energy flow  < 0.
     *   CascadeFailure — energy == 0, the cascade coroutine is shedding creatures.
     *   Dead           — last bonded creature has been shed; terminal.
     */
    public enum HolobiontPhase
    {
        Stable,
        Declining,
        CascadeFailure,
        Dead
    }
}
