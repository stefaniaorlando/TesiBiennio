using UnityEngine;

namespace Holobiont
{
    /*
     * Tuning data for the breath simulator. Designers edit instances of this asset
     * to tune frequency/depth ranges, drift rates, stamina economy, recovery, and
     * pause-boost behavior without opening any script.
     *
     * One BreathConfig drives BreathParameters, StaminaSystem, RecoveryController,
     * and BreathSimulator (pause-boost). Place an instance under Game/Configs/.
     */
    [CreateAssetMenu(fileName = "BreathConfig", menuName = "Game/Breath Config")]
    public class BreathConfig : ScriptableObject
    {
        [Header("Frequency (cycles per second)")]
        [Min(0f)] public float frequencyBaseline = 0.25f;
        [Min(0f)] public float frequencyMin      = 0.10f;
        [Min(0f)] public float frequencyMax      = 0.50f;
        [Min(0f)] public float frequencyApproachRate = 0.20f;
        [Min(0f)] public float frequencyDecayRate    = 0.10f;

        [Header("Depth (amplitude scalar)")]
        [Range(0f, 1f)] public float depthBaseline = 0.50f;
        [Range(0f, 1f)] public float depthMin      = 0.20f;
        [Range(0f, 1f)] public float depthMax      = 1.00f;
        [Min(0f)] public float depthApproachRate = 0.50f;
        [Min(0f)] public float depthDecayRate    = 0.30f;

        [Header("Stamina")]
        [Min(0f)] public float staminaMax              = 1f;
        [Min(0f)] public float baseDrainRate           = 0.20f;
        [Min(0f)] public float pauseDrainRate          = 0.25f;
        [Min(0f)] public float regenRate               = 0.15f;
        [Min(0f)] public float regenRateDuringRecovery = 0.30f;
        [Range(0f, 1f)] public float baselineTolerance = 0.05f;

        [Header("Recovery")]
        [Min(0f)] public float recoveryDuration = 3f;

        [Header("Pause Boost")]
        public bool pauseBoostEnabled = true;
        [Min(1f)] public float pauseBoostMultiplier = 2f;
        [Min(0f)] public float pauseBoostDecayRate  = 3f;
    }
}
