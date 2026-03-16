using UnityEngine;
using UnityEngine.Events;

/*
 * ============================================================================
 * PLAYER HEALTH - ScriptableObject
 * ============================================================================
 *
 * WHAT THIS DOES:
 * A data container for the player's health. By living in a ScriptableObject,
 * the health value can be read and displayed by any component (UI bars,
 * visual effects, etc.) without tight coupling — just drag this asset into
 * any Inspector slot that needs it.
 *
 * RUNTIME RESET:
 * currentHealth is reset to maxHealth every time Play mode starts (OnEnable).
 * Events are [NonSerialized] so listeners registered at runtime never pollute
 * the saved asset.
 *
 * HOW TO CREATE THE ASSET:
 * Right-click in the Project window → Create → Breath Game → Player Health
 *
 * HOW TO USE FROM CODE:
 *   [SerializeField] private PlayerHealthSO health;
 *
 *   health.Heal(2f);                   // heal 2 hp
 *   health.TakeDamage(1f);             // deal 1 damage
 *   float fill = health.Normalized;    // 0-1 for UI bars
 *   bool dead  = health.IsDead;
 *
 * HOW TO REACT TO EVENTS:
 *   Subscribe from any MonoBehaviour:
 *   health.OnDeath.AddListener(MyDeathHandler);
 *   health.OnHealthChanged.AddListener(fill => myBar.value = fill);
 *
 * ============================================================================
 */

[CreateAssetMenu(fileName = "PlayerHealth", menuName = "Breath Game/Player Health")]
public class PlayerHealthSO : ScriptableObject
{
    // =========================================================================
    // SETTINGS
    // =========================================================================

    [Header("Settings")]
    [Tooltip("Maximum health the player can have.")]
    [SerializeField] private float maxHealth = 10f;

    // =========================================================================
    // RUNTIME STATE (visible in Inspector during Play mode)
    // =========================================================================

    [Header("Runtime State (Read Only)")]
    [SerializeField] private float currentHealth;

    // =========================================================================
    // RUNTIME EVENTS
    // [NonSerialized] ensures listeners added at runtime are never saved to disk.
    // =========================================================================

    /// <summary>Fired whenever health changes. Argument is normalized health (0-1).</summary>
    [System.NonSerialized] public UnityEvent<float> OnHealthChanged = new();

    /// <summary>Fired once when health reaches zero.</summary>
    [System.NonSerialized] public UnityEvent OnDeath = new();

    /// <summary>Fired when health reaches maximum.</summary>
    [System.NonSerialized] public UnityEvent OnFullyHealed = new();

    // =========================================================================
    // PUBLIC PROPERTIES
    // =========================================================================

    /// <summary>Maximum possible health.</summary>
    public float MaxHealth => maxHealth;

    /// <summary>Current health value.</summary>
    public float CurrentHealth => currentHealth;

    /// <summary>Health as a 0-1 fraction. Use for UI bars.</summary>
    public float Normalized => maxHealth > 0f ? currentHealth / maxHealth : 0f;

    /// <summary>True when health is at or below zero.</summary>
    public bool IsDead => currentHealth <= 0f;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    private void OnEnable()
    {
        // Always start at full health when entering Play mode or reloading the asset.
        currentHealth = maxHealth;
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>
    /// Restore health by amount. Clamped to maxHealth.
    /// Has no effect if the player is already dead.
    /// </summary>
    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        float previous = currentHealth;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);

        if (currentHealth != previous)
        {
            OnHealthChanged?.Invoke(Normalized);

            if (currentHealth >= maxHealth)
                OnFullyHealed?.Invoke();
        }
    }

    /// <summary>
    /// Deal damage to the player. Fires OnDeath when health reaches zero.
    /// Has no effect if already dead.
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;

        currentHealth = Mathf.Max(currentHealth - amount, 0f);
        OnHealthChanged?.Invoke(Normalized);

        if (currentHealth <= 0f)
            OnDeath?.Invoke();
    }

    /// <summary>
    /// Reset health to maximum and fire OnHealthChanged.
    /// Use this to restart the game without reloading the scene.
    /// </summary>
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(Normalized);
    }
}
