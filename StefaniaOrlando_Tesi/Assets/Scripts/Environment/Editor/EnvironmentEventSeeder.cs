using UnityEditor;
using UnityEngine;

namespace Holobiont
{
/*
 * One-shot menu command that creates a curated library of EnvironmentEventConfig
 * assets and a default schedule pointing at them. Re-runnable: existing assets
 * are loaded and updated in place so GUIDs are preserved.
 *
 * Run via: top menu → Game → Environment → Seed Event Library
 *
 * Library is organised in four tiers, gated by DifficultyManager.Difficulty:
 *   Tier 1  (mono-parameter, 7 events)   — d ∈ [0.00, 0.40]
 *   Tier 2  (di-parameter,   8 events)   — d ∈ [0.25, 0.75]
 *   Tier 3  (tri-parameter,  8 events)   — d ∈ [0.60, 1.00]
 *   Tier 4  (all four,       2 events)   — d == 1.00
 *
 * Tweak this file and re-run when you want to revise the library wholesale.
 */

public static class EnvironmentEventSeeder
{
    private const string EVENTS_DIR    = "Assets/Game/Configs/Events";
    private const string SCHEDULES_DIR = "Assets/Game/Configs";

    // Per-tier defaults. Individual events can override via the MakeEvent overloads.
    private const float T1_RAMP_UP = 3f, T1_SUSTAIN = 5f,  T1_RAMP_DOWN = 4f,  T1_PEAK = 20f, T1_WEIGHT = 5f;
    private const float T2_RAMP_UP = 4f, T2_SUSTAIN = 8f,  T2_RAMP_DOWN = 5f,  T2_PEAK = 30f, T2_WEIGHT = 3f;
    private const float T3_RAMP_UP = 6f, T3_SUSTAIN = 14f, T3_RAMP_DOWN = 8f,  T3_PEAK = 45f, T3_WEIGHT = 1.5f;
    private const float T4_RAMP_UP = 10f,T4_SUSTAIN = 30f, T4_RAMP_DOWN = 15f, T4_PEAK = 70f, T4_WEIGHT = 1f;

    private const float T1_MIN = 0.00f, T1_MAX = 0.40f;
    private const float T2_MIN = 0.25f, T2_MAX = 0.75f;
    private const float T3_MIN = 0.60f, T3_MAX = 1.00f;
    private const float T4_MIN = 1.00f, T4_MAX = 1.00f;

    [MenuItem("Game/Environment/Seed Event Library")]
    public static void Seed()
    {
        if (!EditorUtility.DisplayDialog(
            "Seed Event Library",
            $"Create or update event assets under:\n  {EVENTS_DIR}\n  {SCHEDULES_DIR}\n\nExisting assets with the same names are updated in place (GUIDs preserved).",
            "Seed", "Cancel"))
            return;

        EnsureFolder(EVENTS_DIR);
        EnsureFolder(SCHEDULES_DIR);

        // Reusable envelope shapes.
        var arch        = EnvironmentEventConfig.MakeArchCurve();   // gentle 0→1→0 (default)
        var sharpAttack = SharpAttackCurve();   // fast attack, plateau, slow release
        var slowBuild   = SlowBuildCurve();     // slow build, plateau, slow release
        var flash       = FlashCurve();         // very fast peak, exponential fall
        var twinPeak    = TwinPeakCurve();      // two peaks separated by a small dip
        var apocalypse  = ApocalypseCurve();    // very slow rise, long sustain, very slow fall

        // ── TIER 1 — mono-parameter (7), ordered Temp / Light / Humidity / Toxicity ──
        var warmBreeze  = MakeEvent(1, "01_WarmBreeze",  "Warm Breeze",  arch,
            Effect(EnvironmentVariable.Temp,     +T1_PEAK));

        var coolBreeze  = MakeEvent(1, "02_CoolBreeze",  "Cool Breeze",  arch,
            Effect(EnvironmentVariable.Temp,     -T1_PEAK));

        var brightFlare = MakeEvent(1, "03_BrightFlare", "Bright Flare", flash,
            Effect(EnvironmentVariable.Light,    +T1_PEAK));

        var darkFlare   = MakeEvent(1, "04_DarkFlare",   "Dark Flare",   flash,
            Effect(EnvironmentVariable.Light,    -T1_PEAK));

        var dampAir     = MakeEvent(1, "05_DampAir",     "Damp Air",     arch,
            Effect(EnvironmentVariable.Humidity, +T1_PEAK));

        var dryAir      = MakeEvent(1, "06_DryAir",      "Dry Air",      arch,
            Effect(EnvironmentVariable.Humidity, -T1_PEAK));

        var toxicTrace  = MakeEvent(1, "07_ToxicTrace",  "Toxic Trace",  slowBuild,
            Effect(EnvironmentVariable.Toxicity, +T1_PEAK));

        // ── TIER 2 — di-parameter (8) ─────────────────────────────────────────
        var heatWave    = MakeEvent(2, "08_HeatWave",    "Heat Wave",    sharpAttack,
            Effect(EnvironmentVariable.Temp, +T2_PEAK),
            Effect(EnvironmentVariable.Humidity,    -T2_PEAK));

        var coldFront   = MakeEvent(2, "09_ColdFront",   "Cold Front",   sharpAttack,
            Effect(EnvironmentVariable.Temp, -T2_PEAK),
            Effect(EnvironmentVariable.Humidity,    +T2_PEAK));

        var brightHeat  = MakeEvent(2, "10_BrightHeat",  "Bright Heat",  arch,
            Effect(EnvironmentVariable.Temp, +T2_PEAK),
            Effect(EnvironmentVariable.Light,       +T2_PEAK));

        var darkCold    = MakeEvent(2, "11_DarkCold",    "Dark Cold",    arch,
            Effect(EnvironmentVariable.Temp, -T2_PEAK),
            Effect(EnvironmentVariable.Light,       -T2_PEAK));

        var toxicDamp   = MakeEvent(2, "12_ToxicDamp",   "Toxic Damp",   slowBuild,
            Effect(EnvironmentVariable.Toxicity,    +T2_PEAK),
            Effect(EnvironmentVariable.Humidity,    +T2_PEAK));

        var toxicDry    = MakeEvent(2, "13_ToxicDry",    "Toxic Dry",    slowBuild,
            Effect(EnvironmentVariable.Toxicity,    +T2_PEAK),
            Effect(EnvironmentVariable.Humidity,    -T2_PEAK));

        var toxicBright = MakeEvent(2, "14_ToxicBright", "Toxic Bright", slowBuild,
            Effect(EnvironmentVariable.Toxicity,    +T2_PEAK),
            Effect(EnvironmentVariable.Light,       +T2_PEAK));

        var toxicDark   = MakeEvent(2, "15_ToxicDark",   "Toxic Dark",   slowBuild,
            Effect(EnvironmentVariable.Toxicity,    +T2_PEAK),
            Effect(EnvironmentVariable.Light,       -T2_PEAK));

        // ── TIER 3 — tri-parameter (8) ────────────────────────────────────────
        var drought     = MakeEvent(3, "16_Drought",     "Drought",      slowBuild,
            Effect(EnvironmentVariable.Temp, +T3_PEAK),
            Effect(EnvironmentVariable.Humidity,    -T3_PEAK),
            Effect(EnvironmentVariable.Toxicity,    +T3_PEAK));

        var coldPlague  = MakeEvent(3, "17_ColdPlague",  "Cold Plague",  slowBuild,
            Effect(EnvironmentVariable.Temp, -T3_PEAK),
            Effect(EnvironmentVariable.Humidity,    +T3_PEAK),
            Effect(EnvironmentVariable.Toxicity,    +T3_PEAK));

        var inferno     = MakeEvent(3, "18_Inferno",     "Inferno",      twinPeak,
            Effect(EnvironmentVariable.Temp, +T3_PEAK),
            Effect(EnvironmentVariable.Humidity,    -T3_PEAK),
            Effect(EnvironmentVariable.Light,       +T3_PEAK));

        var blizzard    = MakeEvent(3, "19_Blizzard",    "Blizzard",     sharpAttack,
            Effect(EnvironmentVariable.Temp, -T3_PEAK),
            Effect(EnvironmentVariable.Humidity,    +T3_PEAK),
            Effect(EnvironmentVariable.Light,       -T3_PEAK));

        var smog        = MakeEvent(3, "20_Smog",        "Smog",         slowBuild,
            Effect(EnvironmentVariable.Temp, +T3_PEAK),
            Effect(EnvironmentVariable.Toxicity,    +T3_PEAK),
            Effect(EnvironmentVariable.Light,       +T3_PEAK));

        var toxicNight  = MakeEvent(3, "21_ToxicNight",  "Toxic Night",  slowBuild,
            Effect(EnvironmentVariable.Temp, -T3_PEAK),
            Effect(EnvironmentVariable.Toxicity,    +T3_PEAK),
            Effect(EnvironmentVariable.Light,       -T3_PEAK));

        var toxicMarsh  = MakeEvent(3, "22_ToxicMarsh",  "Toxic Marsh",  slowBuild,
            Effect(EnvironmentVariable.Humidity,    +T3_PEAK),
            Effect(EnvironmentVariable.Toxicity,    +T3_PEAK),
            Effect(EnvironmentVariable.Light,       +T3_PEAK));

        var dustStorm   = MakeEvent(3, "23_DustStorm",   "Dust Storm",   sharpAttack,
            Effect(EnvironmentVariable.Humidity,    -T3_PEAK),
            Effect(EnvironmentVariable.Toxicity,    +T3_PEAK),
            Effect(EnvironmentVariable.Light,       -T3_PEAK));

        // ── TIER 4 — all four, max difficulty only (2) ────────────────────────
        var apocalypseEv = MakeEvent(4, "24_Apocalypse", "Apocalypse",   apocalypse,
            Effect(EnvironmentVariable.Temp, +T4_PEAK),
            Effect(EnvironmentVariable.Humidity,    -T4_PEAK),
            Effect(EnvironmentVariable.Toxicity,    +T4_PEAK),
            Effect(EnvironmentVariable.Light,       +T4_PEAK));

        var cataclysm   = MakeEvent(4, "25_Cataclysm",   "Cataclysm",    apocalypse,
            Effect(EnvironmentVariable.Temp, -T4_PEAK),
            Effect(EnvironmentVariable.Humidity,    +T4_PEAK),
            Effect(EnvironmentVariable.Toxicity,    +T4_PEAK),
            Effect(EnvironmentVariable.Light,       -T4_PEAK));

        // ── DEFAULT SCHEDULE ──────────────────────────────────────────────────
        var schedule = LoadOrCreateAsset<EnvironmentEventScheduleConfig>(
            $"{SCHEDULES_DIR}/DefaultEventSchedule.asset");

        schedule.minInterval = 15f;
        schedule.maxInterval = 30f;

        schedule.eventPool = new[]
        {
            // Tier 1
            warmBreeze, coolBreeze, brightFlare, darkFlare, dampAir, dryAir, toxicTrace,
            // Tier 2
            heatWave, coldFront, brightHeat, darkCold, toxicDamp, toxicDry, toxicBright, toxicDark,
            // Tier 3
            drought, coldPlague, inferno, blizzard, smog, toxicNight, toxicMarsh, dustStorm,
            // Tier 4
            apocalypseEv, cataclysm,
        };
        EditorUtility.SetDirty(schedule);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = schedule;
        EditorGUIUtility.PingObject(schedule);
        Debug.Log($"Seeded 25 events (7+8+8+2) and DefaultEventSchedule under {EVENTS_DIR} / {SCHEDULES_DIR}.");
    }

    // ----- Builders -----

    private static EnvironmentEventConfig MakeEvent(
        int tier, string fileName, string displayName,
        AnimationCurve envelope,
        params EnvironmentEventEffect[] effects)
    {
        TierTimings(tier, out float rampUp, out float sustain, out float rampDown);
        TierBand   (tier, out float minD,   out float maxD);
        float weight = TierWeight(tier);

        string path = $"{EVENTS_DIR}/{fileName}.asset";
        var asset = LoadOrCreateAsset<EnvironmentEventConfig>(path);

        asset.eventName        = displayName;
        asset.weight           = weight;
        asset.minDifficulty    = minD;
        asset.maxDifficulty    = maxD;
        asset.rampUpDuration   = rampUp;
        asset.sustainDuration  = sustain;
        asset.rampDownDuration = rampDown;
        asset.envelopeCurve    = CloneCurve(envelope);
        asset.effects          = effects;

        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static float TierWeight(int tier)
    {
        switch (tier)
        {
            case 1: return T1_WEIGHT;
            case 2: return T2_WEIGHT;
            case 3: return T3_WEIGHT;
            case 4: return T4_WEIGHT;
            default: throw new System.ArgumentOutOfRangeException(nameof(tier));
        }
    }

    private static void TierTimings(int tier, out float rampUp, out float sustain, out float rampDown)
    {
        switch (tier)
        {
            case 1: rampUp = T1_RAMP_UP; sustain = T1_SUSTAIN; rampDown = T1_RAMP_DOWN; return;
            case 2: rampUp = T2_RAMP_UP; sustain = T2_SUSTAIN; rampDown = T2_RAMP_DOWN; return;
            case 3: rampUp = T3_RAMP_UP; sustain = T3_SUSTAIN; rampDown = T3_RAMP_DOWN; return;
            case 4: rampUp = T4_RAMP_UP; sustain = T4_SUSTAIN; rampDown = T4_RAMP_DOWN; return;
            default: throw new System.ArgumentOutOfRangeException(nameof(tier));
        }
    }

    private static void TierBand(int tier, out float minD, out float maxD)
    {
        switch (tier)
        {
            case 1: minD = T1_MIN; maxD = T1_MAX; return;
            case 2: minD = T2_MIN; maxD = T2_MAX; return;
            case 3: minD = T3_MIN; maxD = T3_MAX; return;
            case 4: minD = T4_MIN; maxD = T4_MAX; return;
            default: throw new System.ArgumentOutOfRangeException(nameof(tier));
        }
    }

    private static EnvironmentEventEffect Effect(EnvironmentVariable variable, float delta)
        => new EnvironmentEventEffect { affected = variable, intensityDelta = delta };

    // ----- Asset helpers -----

    private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!asset)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static AnimationCurve CloneCurve(AnimationCurve src)
        => new AnimationCurve(src.keys);

    // ----- Curve presets -----

    private static AnimationCurve SharpAttackCurve()
        => Smooth(new AnimationCurve(
            new Keyframe(0.00f, 0f),
            new Keyframe(0.20f, 1f),
            new Keyframe(0.45f, 1f),
            new Keyframe(1.00f, 0f)));

    private static AnimationCurve SlowBuildCurve()
        => Smooth(new AnimationCurve(
            new Keyframe(0.00f, 0f),
            new Keyframe(0.40f, 1f),
            new Keyframe(0.70f, 1f),
            new Keyframe(1.00f, 0f)));

    private static AnimationCurve FlashCurve()
        => Smooth(new AnimationCurve(
            new Keyframe(0.00f, 0f),
            new Keyframe(0.10f, 1f),
            new Keyframe(0.40f, 0.30f),
            new Keyframe(1.00f, 0f)));

    private static AnimationCurve TwinPeakCurve()
        => Smooth(new AnimationCurve(
            new Keyframe(0.00f, 0f),
            new Keyframe(0.25f, 1f),
            new Keyframe(0.50f, 0.60f),
            new Keyframe(0.75f, 1f),
            new Keyframe(1.00f, 0f)));

    private static AnimationCurve ApocalypseCurve()
        => Smooth(new AnimationCurve(
            new Keyframe(0.00f, 0f),
            new Keyframe(0.30f, 1f),
            new Keyframe(0.70f, 1f),
            new Keyframe(1.00f, 0f)));

    private static AnimationCurve Smooth(AnimationCurve c)
    {
        for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
        return c;
    }
}
}
