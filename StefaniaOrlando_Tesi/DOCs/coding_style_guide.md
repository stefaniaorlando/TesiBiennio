# Coding Style Guide

## Starry Dynamo — Game Dev 2, 2025–26

*Foundational conventions for writing clean, readable, maintainable C# in Unity. This guide governs all project code — instructor systems, student modifications, and AI-generated scripts alike.*

---

## Guiding Principles

### KISS — Keep It Simple

Prefer the simplest solution that works. Every line of code is a liability — it must be read, understood, maintained, and debugged. Complexity is not a sign of sophistication; clarity is.

### YAGNI — You Aren't Gonna Need It

Don't build abstractions for problems that don't exist yet. Write code for the current requirement. If a future need arises, refactor then. Premature generalization produces code that's harder to read and harder to change — the opposite of its intent.

### DRY — Don't Repeat Yourself

When the same logic appears in more than two places, extract it into a method or a shared component. But don't over-apply this — two similar-but-not-identical blocks are often better left separate than forced into a brittle shared abstraction.

### Single Responsibility (SOLID — the S)

Every script does one thing. Every method does one thing. A `HealthController` manages health. It does not play sounds, spawn particles, or update UI. Those are separate scripts with separate responsibilities. When you're tempted to add "just one more thing" to a script, make a new script instead.

### Open/Closed (SOLID — the O)

Design systems that can be *extended* without being *modified*. ScriptableObjects are the primary tool for this: new enemy types are new SO assets, not new code branches. New behaviors are new modules, not new if-statements in existing modules.

---

## Naming Conventions

Follow standard C# naming conventions for Unity. Consistent naming is not cosmetic — it's how you navigate a project without memorizing every file.

### Scripts and Classes

```
PascalCase — one class per file, filename matches class name.

HealthController.cs       → public class HealthController
DetectionSensor.cs        → public class DetectionSensor
DynamoChargeManager.cs    → public class DynamoChargeManager
EntityData.cs             → public class EntityData : ScriptableObject
```

### Variables and Fields

```
Serialized fields:        camelCase (no underscore — these are Inspector-visible)
Debug fields:             camelCase + [ReadOnly, SerializeField] (visible but not editable)
Private fields:           _camelCase with underscore prefix (truly internal state)
Public properties:        PascalCase
Local variables:          camelCase
Constants:                UPPER_SNAKE_CASE
```

The distinction matters: serialized fields are wired in the Inspector, so they're semi-public by nature. Debug fields are serialized purely for Inspector visibility — they use `[ReadOnly]` to prevent editing. The underscore prefix marks fields that are purely internal — never seen or set outside the script.

```csharp
public class HealthController : MonoBehaviour
{
    // --- Serialized (visible in Inspector) — no underscore ---
    [SerializeField] private HealthData healthData;
    [SerializeField] private GameEvent onDamaged;

    // --- Truly private state — underscore prefix ---
    private float _currentHealth;
    private bool _isDead;

    // --- Public access (read-only where possible) ---
    public float CurrentHealth => _currentHealth;
    public float HealthRatio => _currentHealth / healthData.MaxHealth;
    public bool IsDead => _isDead;

    // --- Constants ---
    private const float MIN_DAMAGE_THRESHOLD = 0.1f;
}
```

### Methods

```
PascalCase — verb-first, descriptive.

public void TakeDamage(float amount)
public void RestoreHealth(float amount)
private void Die()
private bool IsInRange(Transform target)
```

### Unity Callbacks

```
Standard Unity names — Awake, Start, Update, OnEnable, OnDisable, etc.
Place them at the top of the class, in lifecycle order.
```

### ScriptableObjects

```
Name the asset type with a Data/Event/Set suffix:

EntityData.cs             → data container
CreatureData.cs           → data container
GameEvent.cs              → event channel
FloatVariable.cs          → shared variable
EntityRuntimeSet.cs       → runtime set
```

### Folders

```
Organize by system, not by type:

Systems/
├── Health/
│   ├── HealthData.cs
│   ├── HealthController.cs
│   └── HealthView.cs
├── Detection/
│   ├── DetectionSensor.cs
│   ├── DetectionData.cs
│   └── DetectionView.cs
├── AI/
│   ├── AIStateMachine.cs
│   ├── AIData.cs
│   └── AIView.cs
└── ...

Glue/
├── EnemySpawnManager.cs
├── DynamoChargeManager.cs
└── ...

Core/
├── GameEvent.cs
├── GameEventListener.cs
├── FloatVariable.cs
└── ...
```

---

## Code Structure

### Script Anatomy

Every MonoBehaviour follows a consistent six-section layout:

```csharp
public class ExampleSystem : MonoBehaviour
{
    //==================== CONFIG =====================
    [Header("Config")]
    [Tooltip("Data asset defining system parameters")]
    [SerializeField] private ExampleData data;

    [Tooltip("Start automatically when the object is enabled")]
    [SerializeField] private bool autoStart = true;

    //==================== STATE =====================
    [Header("Debug")]
    [ReadOnly, SerializeField] private bool isActive;    // visible in Inspector, not editable

    private float _timer;                                 // truly internal — underscore prefix

    public bool IsActive => isActive;

    //==================== OUTPUTS =====================
    public event Action OnActivated;                      // C# event (for code wiring)

    [Header("Events")]
    [Tooltip("Fired when the system activates")]
    [SerializeField] private UnityEvent activatedEvent;   // UnityEvent (for Inspector wiring)

    //==================== LIFECYCLE =====================
    private void Awake() { }
    private void OnEnable()
    {
        if (autoStart) Activate();
    }
    private void Update() { }
    private void OnDisable() { }

    //==================== INPUTS =====================
    [ContextMenu("Activate")]
    public void Activate()
    {
        isActive = true;
        OnActivated?.Invoke();
        activatedEvent?.Invoke();
    }

    //==================== PRIVATE =====================
    private void HandleInternal() { }

    private void OnDrawGizmosSelected() { }
}
```

The six sections, in order:

1. **CONFIG** — Serialized fields set in the Inspector. No underscore prefix.
2. **STATE** — Runtime state. Debug-visible fields use `[ReadOnly, SerializeField]`. Truly internal fields keep the `_underscore` prefix. Public properties go here too.
3. **OUTPUTS** — C# events first, then UnityEvents. Always provide both (see *Dual-Output Events* below).
4. **LIFECYCLE** — Unity callbacks in execution order (`Awake` → `OnEnable` → `Start` → `Update` → `OnDisable`).
5. **INPUTS** — Public methods that other scripts or UnityEvents call to drive this system.
6. **PRIVATE** — Internal helpers, gizmos.

### Small Scripts, Small Methods

A script should fit on one screen. A method should fit in your head. If you're scrolling to understand a method, it's too long — extract a sub-method with a descriptive name. Aim for methods under 15 lines. Prefer many small, named methods over few large anonymous blocks.

```csharp
// ✗ Too long, too nested
void Update()
{
    if (_isActive)
    {
        if (_currentHealth > 0)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0)
            {
                // 20 lines of spawning logic...
            }
        }
    }
}

// ✓ Small methods, clear names
void Update()
{
    if (!_isActive) return;
    if (_currentHealth <= 0) return;

    _timer -= Time.deltaTime;
    if (_timer <= 0) SpawnNextWave();
}
```

### Auto-Start Convention

Scripts that can start themselves automatically use a single pattern: a serialized bool named `autoStart`, checked in `OnEnable`.

```csharp
[Tooltip("Start automatically when the object is enabled")]
[SerializeField] private bool autoStart = true;

private void OnEnable()
{
    if (autoStart) Run();
}
```

Always `OnEnable`, never `Start` — so the behavior restarts correctly when the GameObject is re-enabled. Always default to `true` so the component works out of the box.

### Debug Fields

Runtime state that's useful to monitor during play mode gets `[ReadOnly, SerializeField]` with a `[Header("Debug")]` group. These fields are visible in the Inspector but not editable.

```csharp
//==================== STATE =====================
[Header("Debug")]
[ReadOnly, SerializeField] private bool isRunning;
[ReadOnly, SerializeField] private int currentTick;

private float _elapsed;     // truly internal — no need to see this

public bool IsRunning => isRunning;
public int CurrentTick => currentTick;
```

Rules:
- `[Header("Debug")]` goes on the **first** debug field only.
- Debug fields use **camelCase** (no underscore) since they're serialized.
- Truly internal fields (buffers, caches, intermediate values) stay `_underscored` and private.
- The `[ReadOnly]` attribute is defined in `Assets/Scripts/Utility/Attributes/ReadOnlyAttribute.cs` — it disables editing but keeps the field visible.

### Dual-Output Events

Every system that fires events exposes **both** a C# `event Action` (for code wiring) and a `UnityEvent` (for Inspector wiring). Always fire both, C# event first.

```csharp
//==================== OUTPUTS =====================
public event Action OnCompleted;

[Header("Events")]
[Tooltip("Fired when the timer completes all ticks")]
[SerializeField] private UnityEvent completedEvent;

// In the method that fires:
private void Complete()
{
    OnCompleted?.Invoke();
    completedEvent?.Invoke();
}
```

Naming convention:
- C# event: `OnPascalCase` (e.g., `OnDied`, `OnSignalAdded`)
- UnityEvent: `camelCaseEvent` (e.g., `diedEvent`, `signalAddedEvent`)
- Typed events use `Action<T>` / `UnityEvent<T>` (e.g., `Action<float>` + `UnityEvent<float>`)

This pattern applies to **all** scripts — modules and glue alike.

---

## Defensive Coding

### Early Returns (Guard Clauses)

Eliminate nesting by returning early. Guard clauses belong at the top of a method as one-liners. The main logic flows at the base indentation level.

```csharp
// ✗ Nested
public void TakeDamage(float amount)
{
    if (!_isDead)
    {
        if (amount > 0)
        {
            _currentHealth -= amount;
            if (_currentHealth <= 0)
            {
                Die();
            }
        }
    }
}

// ✓ Guard clauses
public void TakeDamage(float amount)
{
    if (_isDead) return;
    if (amount <= 0) return;

    _currentHealth -= amount;

    if (_currentHealth <= 0) Die();
}
```

### Null Checks — Modern C# Style

Use pattern matching and the not operator for null checks. They read more naturally and are less error-prone than `== null`.

```csharp
// ✗ Old style
if (target == null) return;
if (target != null) { ... }

// ✓ Modern C#
if (target is null) return;
if (target is not null) { ... }

// For Unity Objects (where null has special behavior):
// Use the implicit bool operator
if (!target) return;
if (target) { ... }
```

### Avoid Deep Nesting

Maximum two levels of indentation inside a method. If you need more, extract a method.

```csharp
// ✗ Three levels deep
foreach (var entity in _entities)
{
    if (entity.IsAlive)
    {
        if (entity.IsInRange(_detectionRadius))
        {
            entity.Alert();
        }
    }
}

// ✓ Flat
foreach (var entity in _entities)
{
    if (!entity.IsAlive) continue;
    if (!entity.IsInRange(_detectionRadius)) continue;

    entity.Alert();
}
```

---

## Comments and Documentation

### When to Comment

Comment *why*, never *what*. The code says what it does. Comments explain decisions, tradeoffs, and non-obvious reasoning.

```csharp
// ✗ Useless
// Subtract damage from health
_currentHealth -= amount;

// ✓ Useful — explains a decision
// Clamp to zero to prevent negative health triggering double-death
_currentHealth = Mathf.Max(0f, _currentHealth - amount);
```

### XML Documentation

Public methods on reusable systems should have a one-line `<summary>` tag. Keep it brief.

```csharp
/// <summary>Apply damage. Ignored if already dead.</summary>
public void TakeDamage(float amount) { ... }
```

### TODO and HACK Markers

Use `// TODO:` for planned work and `// HACK:` for known shortcuts. These are searchable and honest.

```csharp
// TODO: Add damage resistance from armor
// HACK: Using fixed value until data SO is wired
private float _damageMultiplier = 1.0f;
```

---

## Unity-Specific Conventions

### SerializeField Over Public Fields

Never use public fields for Inspector exposure. Use `[SerializeField]` on private fields. Public access goes through properties.

```csharp
// ✗ Exposes internals
public float maxHealth = 100f;

// ✓ Controlled access
[SerializeField] private float maxHealth = 100f;
public float MaxHealth => maxHealth;
```

### Header and Tooltip Attributes

Use `[Header]` to group related fields. **Every** `[SerializeField]` gets a `[Tooltip]` — this is the student's inline documentation in the Inspector.

```csharp
[Header("Movement")]
[Tooltip("Movement speed in units per second")]
[Min(0f)]
[SerializeField] private float moveSpeed = 5f;

[Tooltip("Rotation speed in degrees per second")]
[Min(0f)]
[SerializeField] private float rotationSpeed = 120f;

[Header("Detection")]
[Tooltip("Radius in world units for sensing nearby entities")]
[Min(0f)]
[SerializeField] private float detectionRadius = 10f;
```

### Range and Min Constraints

Constrain numeric fields where the valid range is obvious. This prevents invalid Inspector values and communicates intent.

```
[Min(0f)]           — durations, radii, distances, speeds, counts (non-negative)
[Min(0.01f)]        — values used as divisors (must be positive to avoid divide-by-zero)
[Range(0f, 1f)]     — normalized values, thresholds, ratios, probabilities
[Range(0f, 180f)]   — angle fields
[Min(0)]            — integer counts
```

Don't constrain values where the valid range isn't obvious (force magnitudes, position offsets, colors).

### RequireComponent

If a script depends on another component, declare it. This prevents misconfigured GameObjects.

```csharp
[RequireComponent(typeof(Rigidbody))]
public class PhysicsInteraction : MonoBehaviour { ... }
```

### Caching Component References

Get component references in `Awake`, not in `Update`. Store them in private fields.

```csharp
private Rigidbody _rb;

private void Awake()
{
    _rb = GetComponent<Rigidbody>();
}
```

### Avoid Find and GetComponent in Loops

`Find`, `FindObjectOfType`, and `GetComponent` are expensive. Never call them per-frame. Cache the result once in `Awake` or `Start`.

---

## What Clean Code Looks Like

A complete, minimal example following all conventions:

```csharp
using UnityEngine;

/// <summary>Tracks health and processes damage for an entity.</summary>
public class HealthController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private HealthData data;

    [Header("Events")]
    [SerializeField] private GameEvent onDamaged;
    [SerializeField] private GameEvent onDeath;

    private float _currentHealth;
    private bool _isDead;

    public float CurrentHealth => _currentHealth;
    public float HealthRatio => _currentHealth / data.MaxHealth;
    public bool IsDead => _isDead;

    private void Awake()
    {
        _currentHealth = data.MaxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (_isDead) return;
        if (amount <= 0f) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        onDamaged?.Raise();

        if (_currentHealth <= 0f) Die();
    }

    public void RestoreHealth(float amount)
    {
        if (_isDead) return;

        _currentHealth = Mathf.Min(data.MaxHealth, _currentHealth + amount);
    }

    private void Die()
    {
        _isDead = true;
        onDeath?.Raise();
    }
}
```

---

*This guide establishes the syntactic and stylistic baseline. For architectural decisions — how systems are structured, how they communicate, and how the project is organized — see `architecture_guide.md`.*
