# Project Conventions

*Project-specific rules and overrides. Read **together with** `coding_style_guide.md` and `architecture_guide.md`, which provide the general baseline. Where this doc conflicts with those, this doc wins.*

---

## Overrides on coding_style_guide.md

### Section dividers

Use a **single-line** divider with the section name in the middle. Short dashes either side, no extra block lines.

```csharp
// ----- Dependencies (injected via Initialize) -----
private CreatureConfig config;

// ----- Lifecycle -----
private void Awake() { }
```

Do **not** use the three-line block (`// ---\n// Header\n// ---`) and do **not** use the banner format (`//==================== HEADER =====================`).

### Private field naming

All private fields use **plain camelCase, no underscore prefix**.

```csharp
// ✓
private CreatureConfig config;
private Rigidbody2D rb;
private float currentStress;

// ✗
private CreatureConfig _config;
private float _currentStress;
```

This overrides the `_underscored` convention in the style guide. The distinction between debug-visible state and truly internal state is carried by the `[ReadOnly, SerializeField]` attribute combination, not by naming.

```csharp
[Header("Debug")]
[SerializeField, ReadOnly] private float currentStress;   // visible, not editable
[SerializeField, ReadOnly] private BondStatus bondStatus;

private Rigidbody2D rb;     // truly internal — not visible in inspector
private bool initialized;
```

Other naming rules from the style guide stand: PascalCase for properties / classes / methods, camelCase for serialized fields and locals, UPPER_SNAKE for constants.

---

## Project-specific rules

### Namespaces

All project code under `Assets/Game/` lives in a single namespace: **`Holobiont`**.

No sub-namespaces per system, no umbrella prefix (no `Game.Holobiont`). One flat namespace keeps lookup, refactoring, and student onboarding simple. Editor scripts use the same `Holobiont` namespace.

If a future subsystem grows large enough to justify isolation, revisit this rule then — until that's a real problem, don't pre-split.

### Time and pause: GameClock

Any code that should pause when the game pauses reads time from `GameClock.Instance`:

```csharp
var clock = GameClock.Instance;
if (clock is null) return;
float dt = clock.DeltaTime;
```

| What | How |
|---|---|
| Simulation systems (creature stress, holobiont metabolism, environment drift, event scheduling) | `GameClock.Instance.DeltaTime` — pause-aware, scaled |
| Visual systems (HUD, view scripts, gizmos) that should keep updating while paused | `Time.deltaTime` directly |
| Defensive guard against missing clock in test scenes | no-op or fall back to `Time.deltaTime`; never crash |

### FixedUpdate vs Update

| Use | Method |
|---|---|
| Rigidbody2D forces, collisions, spring physics | `FixedUpdate` (use `Time.fixedDeltaTime`) |
| Visual updates (sprite swaps, HUD bindings, gizmos) | `Update` |
| Deterministic simulation with no physics (e.g. holobiont energy loop) | `Update`, gated on `GameClock` |
| Event-driven one-shots | events / coroutines |

### Initialization patterns

Two valid modes — choose by entity origin.

**Runtime-spawned entities** (creatures, projectiles, anything `Instantiate`d): expose a parameterised `Initialize(...)` method. Wires references, generates parametric data, sets initial state. Set an `initialized` flag and guard ticks on it.

```csharp
public void Initialize(CreatureType type, CreatureConfig cfg, EnvironmentManager env)
{
    creatureType = type;
    config = cfg;
    environment = env;
    affinity = GenerateAffinity(env, cfg.affinityScatter);
    initialized = true;
}

private void FixedUpdate()
{
    if (!initialized) return;
    // ...
}
```

**Scene-placed managers** (HolobiontManager, EnvironmentManager, GameClock): use `[SerializeField]` references for dependencies and validate in `OnEnable`. Disable the component on missing config rather than throwing.

```csharp
[SerializeField] private HolobiontConfig config;

private void OnEnable()
{
    if (config is null)
    {
        Debug.LogError($"{nameof(HolobiontManager)} missing config.", this);
        enabled = false;
        return;
    }
    // ...
}
```

### Events: pair C# Action + UnityEvent

Every outward-facing event exposes both:

- a C# `event Action` (or `Action<T>`) for code subscribers
- a `[SerializeField] UnityEvent` (or `UnityEvent<T>`) for inspector wiring

Always fire the C# event **first**, then the UnityEvent.

```csharp
// ----- Outputs -----
public event System.Action<Creature> OnStressDeath;

[Header("Events")]
[Tooltip("Fired while bound when stress reaches the death threshold.")]
[SerializeField] private UnityEvent<Creature> stressDeathEvent;

// ----- Private -----
private void HandleStressDeath()
{
    OnStressDeath?.Invoke(this);
    stressDeathEvent?.Invoke(this);
}
```

**Naming:**
- C# event: `OnPascalCase` — `OnStressDeath`, `OnCreatureBonded`, `OnDeath`
- UnityEvent field: `camelCaseEvent` — `stressDeathEvent`, `creatureBondedEvent`, `deathEvent`

This applies to all outward-facing events (events that other systems / inspector / designers consume). Internal fire-and-forget callbacks within a single class don't need the dual pattern.

### When to use C# events vs ScriptableObject event channels

- **C# events on the owning class** — in-system state changes where the publisher is obvious and subscribers are nearby. Examples: `Creature.OnStressDeath`, `HolobiontState.OnCreatureBonded`. Cheap, no asset to author.
- **SO event channels** (per `architecture_guide.md`) — cross-system broadcasts with no single owner, where multiple unrelated views / audio / UI want to react. Examples: a future `OnCrisisEventStarted` that the HUD, post-processing, audio, and analytics all consume.

Default to C# events on the owning class. Reach for an SO channel only when the broadcast crosses system boundaries with no clean owner. Don't proliferate channels for every event.

### `[ContextMenu]` for editor testing

Parameterless public input methods get `[ContextMenu("Method Name")]` so they're callable from the inspector cog at edit-time and runtime. **Names match the method — no `Test_` or `Debug_` prefix.**

```csharp
[ContextMenu("Bond")]
public void Bond() { ... }

[ContextMenu("Reset State")]
public void ResetState() { ... }
```

If a method needs parameters in normal use but you want an editor handle, add a parameterless wrapper that reads from serialized debug fields:

```csharp
[Header("Debug")]
[SerializeField] private CreatureType debugSpawnType = CreatureType.Nutrici;

[ContextMenu("Spawn Debug Creature")]
public void SpawnDebugCreature() => Spawn(debugSpawnType);
```

### ScriptableObject menu paths

All `[CreateAssetMenu]` paths root under `Game/`. Subgrouping by system is encouraged for discoverability.

```csharp
[CreateAssetMenu(fileName = "HolobiontConfig",   menuName = "Game/Holobiont Config")]
[CreateAssetMenu(fileName = "CreatureConfig",    menuName = "Game/Creature Config")]
[CreateAssetMenu(fileName = "EnvironmentConfig", menuName = "Game/Environment Config")]
[CreateAssetMenu(fileName = "DriftProfile",      menuName = "Game/Environment Drift Profile")]
```

Don't put project SOs under generic roots like `Data/` or `Holobiont/` — `Game/` is the single root.

---

*This doc takes precedence over the imported style and architecture guides where they conflict. Update this doc — not the imported ones — when adding project-specific rules.*
