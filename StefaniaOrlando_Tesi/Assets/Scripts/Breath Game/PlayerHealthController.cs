using UnityEngine;
using UnityEngine.Events;

/*
 * ============================================================================
 * PLAYER HEALTH CONTROLLER
 * ============================================================================
 *
 * WHAT THIS DOES:
 * Bridges the PlayerHealthSO data asset to the player GameObject.
 * - Drains health passively each second (configurable, set to 0 to disable)
 * - Heals the player each time FieldAbility_ParticlePolarity kills a particle
 * - On death: stops physics, disables listed abilities, tints the sprite
 *
 * HOW TO SET UP:
 * 1. Add this component to the Player GameObject (requires CharacterController2D).
 * 2. Create a PlayerHealthSO asset (right-click → Create → Breath Game → Player Health)
 *    and drag it into "Health".
 * 3. Drag your FieldAbility_ParticlePolarity into "Polarity Field".
 * 4. Drag your SpriteRenderer into "Sprite Renderer".
 * 5. In "Abilities To Disable", add every ability MonoBehaviour that should
 *    stop working on death (Wander, Pulse, Tension, ParticlePolarity, etc.).
 * 6. (Optional) Wire OnDeath / OnHealthChanged to UI or sound in the Inspector.
 *
 * ============================================================================
 */

[RequireComponent(typeof(CharacterController2D))]
public class PlayerHealthController : MonoBehaviour
{
    // =========================================================================
    // REFERENCES
    // =========================================================================

    [Header("References")]
    [Tooltip("The health data asset shared across the game.")]
    [SerializeField] private PlayerHealthSO health;

    [Tooltip("The polarity field that fires OnParticlesKilled when particles are absorbed.")]
    [SerializeField] private FieldAbility_ParticlePolarity polarityField;

    [Tooltip("SpriteRenderer to tint on death.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    // =========================================================================
    // HEALTH SETTINGS
    // =========================================================================

    [Header("Health Settings")]
    [Tooltip("Health restored per particle killed. Set to 0 to disable heal-on-kill.")]
    [SerializeField] private float healthPerParticle = 1f;

    [Tooltip("Health drained per second passively. Set to 0 to disable passive drain.")]
    [SerializeField] private float drainRatePerSecond = 0.5f;

    // =========================================================================
    // DEATH SETTINGS
    // =========================================================================

    [Header("Death")]
    [Tooltip("Color the sprite changes to when the player dies.")]
    [SerializeField] private Color deathColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    [Tooltip("Ability MonoBehaviours to disable when the player dies. " +
             "Add all movement and field ability components here.")]
    [SerializeField] private MonoBehaviour[] abilitiesToDisable;

    // =========================================================================
    // INSPECTOR EVENTS
    // =========================================================================

    [Header("Events")]
    [Tooltip("Fired when the player dies. Hook up UI, sound, or game-over logic here.")]
    public UnityEvent OnDeath;

    [Tooltip("Fired whenever health changes. Argument is normalized health (0-1). Use for UI bars.")]
    public UnityEvent<float> OnHealthChanged;

    // =========================================================================
    // RUNTIME STATE
    // =========================================================================

    private CharacterController2D controller;
    private bool isDead = false;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    private void Awake()
    {
        controller = GetComponent<CharacterController2D>();
    }

    private void OnEnable()
    {
        isDead = false;

        if (health != null)
        {
            health.ResetHealth();
            health.OnDeath.AddListener(HandleDeath);
            health.OnHealthChanged.AddListener(HandleHealthChanged);
        }

        if (polarityField != null)
            polarityField.OnParticlesKilled.AddListener(HandleParticlesKilled);
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDeath.RemoveListener(HandleDeath);
            health.OnHealthChanged.RemoveListener(HandleHealthChanged);
        }

        if (polarityField != null)
            polarityField.OnParticlesKilled.RemoveListener(HandleParticlesKilled);
    }

    private void Update()
    {
        if (isDead || health == null) return;

        if (drainRatePerSecond > 0f)
            health.TakeDamage(drainRatePerSecond * Time.deltaTime);
    }

    // =========================================================================
    // HANDLERS
    // =========================================================================

    private void HandleParticlesKilled(int count)
    {
        if (isDead) return;
        health.Heal(count * healthPerParticle);
    }

    private void HandleHealthChanged(float normalized)
    {
        OnHealthChanged?.Invoke(normalized);
    }

    private void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        // Stop all physics movement
        controller.Stop();
        controller.Rigidbody.bodyType = RigidbodyType2D.Static;

        // Tint the sprite
        if (spriteRenderer != null)
            spriteRenderer.color = deathColor;

        // Disable each listed ability
        foreach (var ability in abilitiesToDisable)
        {
            if (ability != null)
                ability.enabled = false;
        }

        OnDeath?.Invoke();
    }
}
