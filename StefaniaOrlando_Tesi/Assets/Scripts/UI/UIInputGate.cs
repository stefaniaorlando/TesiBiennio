namespace Holobiont
{
    /*
     * Tiny static used to arbitrate keyboard input between UI components.
     * BreathKeyRebindRow sets Capturing=true while it's listening for the
     * next key press; MainMenuView checks the flag before consuming Esc so
     * the user can rebind Esc itself without the menu closing on top of them.
     */
    public static class UIInputGate
    {
        /// <summary>True while a UI component is exclusively reading raw keyboard input. Other input consumers should defer this frame.</summary>
        public static bool Capturing;
    }
}
