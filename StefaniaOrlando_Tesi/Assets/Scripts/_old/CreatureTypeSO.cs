using UnityEngine;

namespace HolobiontLegacy
{

/*
 * ============================================================================
 * CREATURE TYPE - ScriptableObject
 * ============================================================================
 *
 * WHAT THIS DOES:
 * Defines the base stats for one species of creature in the holobiont.
 * Create one asset per type via:
 *   Right-click in Project → Create → Holobiont → Creature Type
 *
 * CREATURE ROSTER:
 *   Idro   — high-humidity nurturer  (highHumidityEnergyBoost > 0)
 *   Xero   — low-humidity nurturer   (lowHumidityEnergyBoost  > 0)
 *   Foto   — high-light nurturer     (highLightEnergyBoost    > 0)
 *   Scoto  — low-light nurturer      (lowLightEnergyBoost     > 0)
 *   Crio   — cold-resistant          (coldResistance          > 0)
 *   Termo  — heat-resistant          (heatResistance          > 0)
 *   Filtro — toxicity-resistant      (toxicityResistance      > 0)
 *
 * ENERGY BOOST (Idro, Xero, Foto, Scoto):
 *   effectiveEnergy = baseEnergy
 *       * (1 + highHumidityEnergyBoost * highHumidityFavor)
 *       * (1 + lowHumidityEnergyBoost  * lowHumidityFavor)
 *       * (1 + highLightEnergyBoost    * highLightFavor)
 *       * (1 + lowLightEnergyBoost     * lowLightFavor)
 *   where each favor value is 0 at the stress threshold and 1 at the extreme.
 *
 * ============================================================================
 */

[CreateAssetMenu(fileName = "CreatureType", menuName = "Holobiont/Creature Type")]
public class CreatureTypeSO : ScriptableObject
{
    [Header("Identity")]
    public string creatureName = "Creature";

    [Header("Base Energy (per creature)")]
    [Min(0f)]
    public float baseEnergy = 5f;

    [Header("Temperature Resistance (per creature)")]
    [Tooltip("Reduces heat stress drain. Negative = vulnerable to heat.")]
    public float heatResistance = 0f;

    [Tooltip("Reduces cold stress drain. Negative = vulnerable to cold.")]
    public float coldResistance = 0f;

    [Header("Humidity (per creature)")]
    [Tooltip("Idro: set > 0. Multiplies energy contribution as humidity rises above the high threshold.")]
    [Min(0f)]
    public float highHumidityEnergyBoost = 0f;

    [Tooltip("Xero: set > 0. Multiplies energy contribution as humidity falls below the low threshold.")]
    [Min(0f)]
    public float lowHumidityEnergyBoost = 0f;

    [Tooltip("Reduces high-humidity stress drain. Negative = vulnerable.")]
    public float highHumidityResistance = 0f;

    [Tooltip("Reduces low-humidity stress drain. Negative = vulnerable.")]
    public float lowHumidityResistance = 0f;

    [Header("Light (per creature)")]
    [Tooltip("Foto: set > 0. Multiplies energy contribution as light rises above the high threshold.")]
    [Min(0f)]
    public float highLightEnergyBoost = 0f;

    [Tooltip("Scoto: set > 0. Multiplies energy contribution as light falls below the low threshold.")]
    [Min(0f)]
    public float lowLightEnergyBoost = 0f;

    [Tooltip("Reduces high-light stress drain. Negative = vulnerable.")]
    public float highLightResistance = 0f;

    [Tooltip("Reduces low-light stress drain. Negative = vulnerable.")]
    public float lowLightResistance = 0f;

    [Header("Toxicity Resistance (per creature)")]
    [Tooltip("Reduces toxicity stress drain. Negative = vulnerable.")]
    public float toxicityResistance = 0f;
}

}
