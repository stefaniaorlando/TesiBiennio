using UnityEngine;

/*
 * ============================================================================
 * CREATURE TYPE - ScriptableObject
 * ============================================================================
 *
 * WHAT THIS DOES:
 * Defines the base stats for one species of creature in the holobiont.
 * Create one asset per type (Nurturer, Crio, Termo) via:
 *   Right-click in Project → Create → Holobiont → Creature Type
 *
 * STATS:
 *   baseEnergy      — life energy contributed per creature
 *   heatResistance  — heat resistance per creature (negative = vulnerable to heat)
 *   coldResistance  — cold resistance per creature (negative = vulnerable to cold)
 *
 * EXAMPLES:
 *   Nurturer:  high baseEnergy, heatResistance = -1, coldResistance = -1
 *   Crio:      low  baseEnergy, heatResistance = -2, coldResistance = +3
 *   Termo:     low  baseEnergy, heatResistance = +3, coldResistance = -2
 *
 * ============================================================================
 */

[CreateAssetMenu(fileName = "CreatureType", menuName = "Holobiont/Creature Type")]
public class CreatureTypeSO : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Display name for this creature type.")]
    public string creatureName = "Creature";

    [Header("Contributions (per creature)")]
    [Tooltip("Life energy this creature contributes to the holobiont.")]
    [Min(0f)]
    public float baseEnergy = 5f;

    [Tooltip("Heat resistance contributed per creature. Negative values mean this creature is vulnerable to heat.")]
    public float heatResistance = 0f;

    [Tooltip("Cold resistance contributed per creature. Negative values mean this creature is vulnerable to cold.")]
    public float coldResistance = 0f;
}
