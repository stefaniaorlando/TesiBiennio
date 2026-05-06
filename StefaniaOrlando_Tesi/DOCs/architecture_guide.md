# Architecture Guide

## Starry Dynamo — Game Dev 2, 2025–26

*How the project is structured — the layers, the patterns, the rules, and the reasoning behind them. This is the project's architectural contract: all code, whether hand-written or AI-generated, should follow this structure.*

---

## Overview

The architecture separates every game system into three concerns — **Data**, **Simulation**, and **Presentation** — inspired by MVC but adapted for Unity and real-time game development. Systems are built as reusable modules, connected by small glue scripts, and communicate through ScriptableObject-based channels rather than direct cross-references.

```
┌─────────────────────────────────────────────────────────────────┐
│                        GAME PROJECT                             │
│                                                                 │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                   PRESENTATION (View)                     │  │
│  │  Visual feedback, UI, audio, particles, animation         │  │
│  │  Reads from simulation. Never writes to game state.       │  │
│  │  Binders observe controllers and update visuals.          │  │
│  └─────────────────────────┬─────────────────────────────────┘  │
│                            │ observes                           │
│  ┌─────────────────────────▼─────────────────────────────────┐  │
│  │                 SIMULATION (Logic)                         │  │
│  │                                                           │  │
│  │  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐        │  │
│  │  │ Health  │ │Detection│ │   AI    │ │ Spawner │  ...    │  │
│  │  │ Module  │ │ Sensor  │ │  State  │ │         │        │  │
│  │  │         │ │         │ │ Machine │ │         │        │  │
│  │  └────┬────┘ └────┬────┘ └────┬────┘ └────┬────┘        │  │
│  │       │           │           │           │              │  │
│  │  Reusable modules (70%) — independent, no cross-refs     │  │
│  │                                                           │  │
│  │  ┌───────────────────────────────────────────────────┐   │  │
│  │  │              GLUE (30%)                           │   │  │
│  │  │  Managers, connectors, binders                    │   │  │
│  │  │  Small, project-specific, wires systems together  │   │  │
│  │  └───────────────────────────────────────────────────┘   │  │
│  └─────────────────────────┬─────────────────────────────────┘  │
│                            │ reads from                         │
│  ┌─────────────────────────▼─────────────────────────────────┐  │
│  │                      DATA                                 │  │
│  │  ScriptableObjects: containers, event channels,           │  │
│  │  shared variables, runtime sets                           │  │
│  │  The single source of truth for all game parameters.      │  │
│  └───────────────────────────────────────────────────────────┘  │
│                                                                 │
│             ═══ SEQUENCING (Time / Execution Order) ═══         │
│             State machines, tick controllers, coroutines        │
│             Threading through all layers                        │
└─────────────────────────────────────────────────────────────────┘
```

---

## Layer 1 — Data

All game parameters, configuration, and shared runtime state live in **ScriptableObject assets**, separate from the code that reads them. Code never hard-codes balancing values. A designer should be able to find and modify any game parameter without opening a script.

### Three Roles of ScriptableObjects

#### Data Containers

Static configuration — entity stats, ability parameters, environment settings. Authored in the editor, read at runtime, never modified by gameplay code.

```csharp
[CreateAssetMenu(fileName = "NewCreatureData", menuName = "Data/CreatureData")]
public class CreatureData : ScriptableObject
{
    [Header("Stats")]
    public float MaxHealth;
    public float MoveSpeed;
    public float DetectionRange;

    [Header("Identity")]
    public string DisplayName;
    public Sprite Icon;
}
```

**Usage:** Create multiple assets — `ShadowCreature.asset`, `HollowCreature.asset` — each with different values. The same controller code drives all creature types; only the data changes.

#### Event Channels

ScriptableObject assets that act as named broadcast addresses. Any script can raise an event on the channel. Any script can listen. Neither the raiser nor the listener knows the other exists.

```csharp
[CreateAssetMenu(fileName = "NewGameEvent", menuName = "Events/GameEvent")]
public class GameEvent : ScriptableObject
{
    private readonly List<GameEventListener> _listeners = new();

    public void Raise()
    {
        // Iterate backwards — listeners may remove themselves
        for (int i = _listeners.Count - 1; i >= 0; i--)
            _listeners[i].OnEventRaised();
    }

    public void RegisterListener(GameEventListener listener)
        => _listeners.Add(listener);

    public void UnregisterListener(GameEventListener listener)
        => _listeners.Remove(listener);
}
```

```csharp
public class GameEventListener : MonoBehaviour
{
    [SerializeField] private GameEvent gameEvent;
    [SerializeField] private UnityEvent response;

    private void OnEnable() => gameEvent.RegisterListener(this);
    private void OnDisable() => gameEvent.UnregisterListener(this);

    public void OnEventRaised() => response.Invoke();
}
```

**Usage:** Create assets like `OnPlayerDamaged.asset`, `OnEnemySpotted.asset`, `OnDynamoCharged.asset`. Wire listeners in the Inspector — no code changes needed to add new responses.

#### Shared Variables

ScriptableObject assets that hold a single runtime value accessible by multiple systems. Acts as an indirect communication channel — a shared blackboard.

```csharp
[CreateAssetMenu(fileName = "NewFloatVariable", menuName = "Variables/Float")]
public class FloatVariable : ScriptableObject
{
    public float InitialValue;

    [System.NonSerialized] public float RuntimeValue;

    private void OnEnable()
    {
        RuntimeValue = InitialValue;
    }
}
```

**Usage:** `PlayerEnergy.asset` — the energy system writes to it, the UI reads from it, the environment system reads from it. None of these systems reference each other.

<!-- EXPANSION POINT: Runtime Sets, typed event channels with payloads -->

---

## Layer 2 — Simulation (Logic)

### Reusable Modules (~70%)

Each game system is a self-contained module: an independent cluster of scripts that handles one concern. Modules reference nothing outside themselves. They could be dropped into a different project unchanged.

**Module structure:**

```
Health/
├── HealthData.cs           ScriptableObject — configuration
├── HealthController.cs     MonoBehaviour — logic and state
└── HealthView.cs           MonoBehaviour — visual response (see Presentation)
```

The controller reads from its data SO, maintains runtime state, and raises events when state changes. It never touches visuals, audio, or UI directly.

```
Detection/
├── DetectionData.cs        ScriptableObject — range, layers, frequency
├── DetectionSensor.cs      MonoBehaviour — raycasts/overlaps, maintains detected list
└── DetectionView.cs        MonoBehaviour — debug gizmos, visual indicators
```

**Sensor pattern:** A sensor detects based on some input (raycast, trigger overlap, spherecast) and holds the result as data — a list of what's in range, the nearest target, whether line-of-sight exists. Other systems read from the sensor; the sensor itself has no opinion about what to do with the information.

```csharp
public class DetectionSensor : MonoBehaviour
{
    [SerializeField] private DetectionData data;

    private readonly List<Transform> _detected = new();

    public IReadOnlyList<Transform> Detected => _detected;
    public bool HasTargets => _detected.Count > 0;
    public Transform NearestTarget { get; private set; }

    // Called by orchestrator or in Update
    public void Tick()
    {
        _detected.Clear();
        // Physics.OverlapSphere, filtering, LOS checks...
        // Populate _detected, compute NearestTarget
    }
}
```

### Glue Scripts (~30%)

Small, explicit, project-specific scripts that wire modules together. Glue is *where the project-specific mess lives*, and that's intentional. Glue is not reusable, and that's fine.

**Two forms:**

#### Managers (Singleton Components)

Scene-level MonoBehaviours that orchestrate specific game subsystems. Each manager handles one domain. No god objects — many small managers instead of one large one.

```csharp
public class EnemySpawnManager : MonoBehaviour
{
    public static EnemySpawnManager Instance { get; private set; }

    [SerializeField] private SpawnerSystem spawner;
    [SerializeField] private FloatVariable timeOfDay;
    [SerializeField] private CreatureData[] nightCreatures;

    private void Awake()
    {
        if (Instance is not null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Responds to time-of-day changes, tells spawner what to create
}
```

**Singleton discipline:** Use the simplest possible singleton — a static `Instance` property with a duplicate check in `Awake`. No DontDestroyOnLoad unless truly needed. Singletons are acceptable for managers because they represent genuinely unique scene-level services, not because they're convenient global access points.

#### Connectors / Binders

Tiny scripts that do nothing but connect point A to point B. Often just a few lines.

```csharp
/// <summary>Connects detection results to AI decision-making.</summary>
public class DetectionToAIConnector : MonoBehaviour
{
    [SerializeField] private DetectionSensor sensor;
    [SerializeField] private AIStateMachine ai;

    private void Update()
    {
        if (sensor.HasTargets)
            ai.SetTarget(sensor.NearestTarget);
        else
            ai.ClearTarget();
    }
}
```

These are *explicit spaghetti* — visible, named, easily found. Infinitely better than hidden dependencies buried inside systems.

---

## Layer 3 — Presentation (View)

The presentation layer reads from the simulation and produces everything the player perceives — visuals, audio, particles, UI. It never writes to game state.

### View Components

Every module that has a visual representation includes a View MonoBehaviour. The view observes its corresponding controller and responds.

```csharp
public class HealthView : MonoBehaviour
{
    [SerializeField] private HealthController health;
    [SerializeField] private Renderer meshRenderer;
    [SerializeField] private Gradient healthGradient;

    private void Update()
    {
        float ratio = health.HealthRatio;
        meshRenderer.material.color = healthGradient.Evaluate(ratio);
    }
}
```

### Presentation Binders

For cross-system visual responses — effects that aren't tied to a single module — use binder scripts that subscribe to event channels and trigger feedback.

```csharp
/// <summary>Plays VFX and audio when player takes damage.</summary>
public class DamageFeedbackBinder : MonoBehaviour
{
    [SerializeField] private ParticleSystem hitParticles;
    [SerializeField] private AudioSource hitSound;

    // Wired to OnPlayerDamaged event channel via GameEventListener
    public void OnDamaged()
    {
        hitParticles.Play();
        hitSound.Play();
    }
}
```

**The separation principle:** The simulation doesn't know it's being rendered. The view doesn't modify game state. You can replace the entire visual layer without touching gameplay. You can test gameplay without any visuals at all.

<!-- EXPANSION POINT: Data binding system, world-space UI patterns, diegetic feedback -->

---

## Sequencing — Execution and Time

### State Machines

The primary tool for controlling behavior over time. Used for player states, entity AI, world progression, and any system where "what should happen next" depends on "what's happening now."

**Implementation:** Enum defines states. A dedicated StateMachine MonoBehaviour routes to state-specific logic via switch. Enter/exit methods handle transitions cleanly.

```csharp
public enum CreatureState { Idle, Patrol, Chase, Attack, Flee }

public class AIStateMachine : MonoBehaviour
{
    [SerializeField] private AIData data;

    private CreatureState _currentState;

    public CreatureState CurrentState => _currentState;

    private void Update()
    {
        switch (_currentState)
        {
            case CreatureState.Idle:    UpdateIdle(); break;
            case CreatureState.Patrol:  UpdatePatrol(); break;
            case CreatureState.Chase:   UpdateChase(); break;
            case CreatureState.Attack:  UpdateAttack(); break;
            case CreatureState.Flee:    UpdateFlee(); break;
        }
    }

    public void TransitionTo(CreatureState newState)
    {
        ExitState(_currentState);
        _currentState = newState;
        EnterState(_currentState);
    }

    private void EnterState(CreatureState state) { /* ... */ }
    private void ExitState(CreatureState state) { /* ... */ }

    private void UpdateIdle() { /* ... */ }
    private void UpdatePatrol() { /* ... */ }
    private void UpdateChase() { /* ... */ }
    private void UpdateAttack() { /* ... */ }
    private void UpdateFlee() { /* ... */ }
}
```

**Where state machines appear:**
- Player: Wandering → Empowered → Ghost → Overloaded
- Entities: Idle → Patrol → Chase → Attack → Flee
- World: Phase 1 (Wandering) → Phase 2 (Gathering) → Phase 3 (Return)
- Dynamo: Unfound → Found → Charging → Charged → Installed

### Tick Controllers

For systems where execution order matters, a tick controller calls system updates in a defined sequence rather than relying on Unity's arbitrary Update order.

```csharp
public class SimulationTick : MonoBehaviour
{
    [SerializeField] private DetectionSensor[] sensors;
    [SerializeField] private AIStateMachine[] aiSystems;
    [SerializeField] private MovementController[] movers;

    private void Update()
    {
        // Explicit order: detect → decide → move
        foreach (var sensor in sensors) sensor.Tick();
        foreach (var ai in aiSystems) ai.Tick();
        foreach (var mover in movers) mover.Tick();
    }
}
```

Individual systems disable their own `Update` and expose a `Tick()` method. The tick controller decides the order. This is optional — not every system needs it — but essential for simulation-critical sequences where order affects outcomes.

### Coroutines

For time-delayed behavior within a single system — spawning waves with intervals, ability cooldowns, phased transitions. Coroutines are local timing, not global sequencing.

```csharp
private IEnumerator SpawnWave(int count, float interval)
{
    for (int i = 0; i < count; i++)
    {
        SpawnEntity();
        yield return new WaitForSeconds(interval);
    }
}
```

---

## Communication Summary

How systems talk to each other, ordered from most to least decoupled:

```
┌────────────────────┬────────────────────────┬──────────────────────────────┐
│ Mechanism          │ Coupling               │ Use Case                     │
├────────────────────┼────────────────────────┼──────────────────────────────┤
│ SO Event Channels  │ Fully decoupled        │ Cross-system broadcast       │
│                    │ (neither side knows    │ (damage → VFX, audio, UI)    │
│                    │  the other exists)     │                              │
├────────────────────┼────────────────────────┼──────────────────────────────┤
│ Shared Variables   │ Indirect coupling      │ Continuous state multiple     │
│ (SO)               │ (shared data, no       │ systems observe              │
│                    │  direct references)    │ (player energy, time of day) │
├────────────────────┼────────────────────────┼──────────────────────────────┤
│ C# Events /       │ Local coupling         │ Within a module              │
│ Delegates          │ (subscriber knows      │ (controller → its own view)  │
│                    │  the publisher)        │                              │
├────────────────────┼────────────────────────┼──────────────────────────────┤
│ Direct Reference   │ Tight coupling         │ Tightly related components   │
│                    │ (hard dependency)      │ (script → its Rigidbody)     │
└────────────────────┴────────────────────────┴──────────────────────────────┘

Default to the most decoupled option that makes sense.
Tighten coupling only when decoupling creates more confusion than it solves.
```

---

## Pattern Reference

Patterns embedded in the architecture — learned through building, named afterward:

| Pattern | Implementation | Where It Appears |
|---------|---------------|------------------|
| **Observer** | SO event channels, C# events | Cross-system communication everywhere |
| **State** | Enum + switch state machines | Player, AI, world progression |
| **Singleton** | Static Instance on managers | Scene-level glue managers |
| **Component** | Unity's native MonoBehaviour model | Every script on every GameObject |
| **Flyweight** | SO data containers shared across instances | All entity types sharing config data |
| **Sensor** | Detection scripts that hold results as data | Detection, proximity, line-of-sight |

<!-- EXPANSION POINT: Command pattern for input/abilities, Strategy via SO delegates -->

---

## The Rules

1. **Data lives in ScriptableObjects.** Not in serialized fields scattered across prefabs.
2. **Every system has three parts: data, logic, view.** Logic reads from data. View reads from logic. View never writes game state.
3. **Systems don't know each other exist.** They communicate through event channels and shared variables.
4. **Glue is where the project-specific mess lives.** Many small managers and connectors. No god objects.
5. **State machines control behavior over time.** When asking "what happens when," the answer is a state machine.
6. **Spaghetti inside a module is acceptable. Spaghetti between modules is not.**
7. **Write systems as if you'll reuse them.** The discipline alone produces cleaner code.
8. **Sensors hold data, not decisions.** Detection scripts report what they see. Other systems decide what to do about it.
9. **The simulation doesn't know it's being rendered.** Gameplay logic and visual feedback are always separate scripts.
10. **If it works and it's readable, it's good enough.** Don't over-architect. These are guidelines, not commandments.

---

*This guide establishes the architectural contract. For syntax, naming, and coding style — see `coding_style_guide.md`. For the broader course philosophy — see `philosophy_and_principles.md`.*
