# Creature System — Implementation Design

## Overview

Creatures are the atomic units of the holobiont. Each creature has a type (nutrici, scudo, hub), an environmental affinity set at spawn, and a stress level computed from affinity-environment mismatch. Creatures exist in two regimes: unbound (drifting in the medium) and bound (attached to the holobiont). The creature itself is simple — it reports its state. All strategic logic lives in the holobiont.

Visual identity: circle = nutrici, triangle = scudo, square = hub. Color encodes affinity.

---

## Data Layer

### CreatureType (Enum)

```csharp
public enum CreatureType
{
    Nutrici,    // Energy production
    Scudo,      // Environmental resistance
    Hub         // Metabolic infrastructure (capacity + storage)
}
```

### CreatureConfig (ScriptableObject)

One asset for all creature types. Type-specific parameters gated by enum.

```
CreatureConfig
├── Shared
│   ├── baseDrainPerTick : float (energy cost of existing in the network)
│   ├── affinityFalloffCurve : AnimationCurve
│   │   // x = normalized distance between affinity and environment (0 = perfect match, 1 = max mismatch)
│   │   // y = efficiency multiplier (1 = full efficiency, 0 = zero contribution)
│   │   // Same curve used for nutrici efficiency AND scudo effectiveness AND stress calculation
│   ├── stressDeathThreshold : float (stress level at which creature auto-detaches and dies)
│   ├── affinityScatter : float (random deviation from environment at spawn — prevents identical creatures)
│   ├── unboundLifetime : float (seconds an unbound creature survives before despawning — prevents accumulation)
│
├── Nutrici
│   ├── baseConversionRate : float (energy produced per tick at perfect affinity match, per unit breath input)
│
├── Scudo
│   ├── baseResistanceContribution : float (resistance added per scudo at perfect affinity match)
│
├── Hub
│   ├── energyCapacityBonus : float (additional max energy per hub)
│   ├── carryingCapacityBonus : int (additional creature slots per hub)
```

### CreatureAffinityData (Struct)

```csharp
[System.Serializable]
public struct CreatureAffinityData
{
    public float idealTemperature;
    public float idealHumidity;
    public float idealToxicity;
    public float idealLight;

    public Vector4 AsVector => new Vector4(idealTemperature, idealHumidity, idealToxicity, idealLight);
}
```

Generated at spawn: sample current EnvironmentState values, add random scatter per axis (range from CreatureConfig.affinityScatter). Creature is "born" adapted to current-ish conditions.

---

## Simulation Layer

### BondStatus (Enum)

```csharp
public enum BondStatus
{
    Unbound,    // Drifting in medium, subject to flow field and holobiont forces
    Bound       // Attached to holobiont, subject to spring physics
}
```

### CreatureSimulation (MonoBehaviour — on creature GameObject)

Owns creature runtime state. Computes stress. Handles movement in both regimes.

```
State (internal):
├── creatureType : CreatureType (set at spawn, immutable)
├── affinity : CreatureAffinityData (set at spawn, immutable)
├── bondStatus : BondStatus
├── currentStress : float (0 = no stress, stressDeathThreshold = death)
├── unboundTimer : float (time remaining before despawn, only used when unbound)

Dependencies (set via inspector or injection):
├── creatureConfig : CreatureConfig (SO reference)
├── environmentState : EnvironmentState (reference to singleton/manager)
├── flowField : FlowField (reference for unbound movement)

Public read-only interface (consumed by HolobiontManager and CreatureView):
├── CreatureType Type { get; }
├── CreatureAffinityData Affinity { get; }
├── BondStatus Status { get; }
├── float Stress { get; }
├── float AffinityEfficiency { get; } // Current efficiency based on affinity-environment match

Public methods (called by HolobiontManager and ForceField):
├── void SetBound()    // Switch to bound regime
├── void SetUnbound()  // Switch to unbound regime
├── void ApplyForce(Vector2 force)  // External force from holobiont force field
├── void SetSpringTarget(Vector2 target, float strength)  // For bound positioning
```

**Tick logic (Update or FixedUpdate):**

```
1. Compute affinity-environment distance:
   float distance = Vector4.Distance(affinity.AsVector, environmentState.AsVector);
   float normalizedDistance = distance / maxPossibleDistance;  // normalize to 0–1

2. Compute efficiency (used by holobiont for energy/resistance calculations):
   affinityEfficiency = creatureConfig.affinityFalloffCurve.Evaluate(normalizedDistance);

3. Compute stress:
   currentStress = 1.0f - affinityEfficiency;  // inverse of efficiency
   // Or a separate stress curve if stress and efficiency should diverge

4. If unbound:
   - Sample flow field: Vector2 flowForce = flowField.GetFlowAtPosition(transform.position);
   - Apply flow force to Rigidbody2D
   - Any external force (from holobiont force field) already applied via ApplyForce()
   - Decrement unboundTimer. If <= 0, despawn (destroy or pool).

5. If bound:
   - Spring toward spring target:
     Vector2 delta = springTarget - (Vector2)transform.position;
     rigidbody2d.AddForce(delta * springStrength);
   - Stress death check: if currentStress >= stressDeathThreshold, fire OnStressDeath event
     (HolobiontManager listens and handles release)
```

**Movement details:**

*Unbound regime:*
- Rigidbody2D is dynamic, low drag
- Flow field provides continuous gentle force
- Holobiont force field provides attraction/repulsion via ApplyForce()
- Linear drag prevents infinite acceleration
- Creature drifts naturally through the medium

*Bound regime:*
- Rigidbody2D is still dynamic (for spring physics) but with higher drag for stability
- Spring target is set by HolobiontManager based on arrangement logic
- Additional breath-phase modulation: spring target distance from center oscillates with breath
  (HolobiontManager handles this — creature just follows its target)
- Flow field forces are NOT applied

*Transition:*
- SetBound(): increase drag, disable flow sampling, enable spring
- SetUnbound(): decrease drag, enable flow sampling, disable spring, reset unboundTimer

---

## Presentation Layer

### CreatureView (MonoBehaviour — on creature GameObject)

Reads from CreatureSimulation. Handles all visual expression. Never writes to simulation.

```
Dependencies:
├── creatureSimulation : CreatureSimulation (same GameObject)
├── spriteRenderer : SpriteRenderer (same GameObject)
├── sprites : CreatureVisualConfig (SO or direct references)
│   ├── nutriciSprite : Sprite (circle)
│   ├── scudoSprite : Sprite (triangle)
│   └── hubSprite : Sprite (square)

Configuration (in inspector):
├── Type color palettes
│   ├── nutriciBaseColor : Color
│   ├── scudoBaseColor : Color
│   └── hubBaseColor : Color
├── affinityTintStrength : float (how much affinity shifts the base color)
├── stressPulseSpeedCurve : AnimationCurve (stress → pulse frequency)
├── stressShrinkCurve : AnimationCurve (stress → scale multiplier)
├── stressOpacityCurve : AnimationCurve (stress → alpha)
├── breathScaleAmplitude : float (how much bound creatures scale with breath phase)
```

**Initialization (on spawn):**

```
1. Set sprite from type:
   spriteRenderer.sprite = type switch {
       Nutrici => sprites.nutriciSprite,
       Scudo   => sprites.scudoSprite,
       Hub     => sprites.hubSprite
   };

2. Compute color from type + affinity:
   Color baseColor = type switch { ... };
   // Shift hue/saturation based on affinity — warm affinity shifts warm, cold shifts cool
   // Simple approach: lerp toward warm tint based on affinity.idealTemperature
   // and toward blue tint based on inverse. Keep it subtle.
   spriteRenderer.color = ComputeAffinityTintedColor(baseColor, affinity);
```

**Tick logic (Update):**

```
1. Read stress from CreatureSimulation.Stress

2. Pulse: oscillate alpha or scale at a frequency driven by stress
   float pulseSpeed = stressPulseSpeedCurve.Evaluate(stress);
   float pulse = Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2);

3. Scale: base scale * stressShrinkCurve.Evaluate(stress)
   If bound: also modulate by breath phase
   float breathMod = 1.0f + (breathPhase * breathScaleAmplitude);
   // breathPhase: -1 (full inhale) to +1 (full exhale), provided by HolobiontManager

4. Opacity: stressOpacityCurve.Evaluate(stress)
   // High stress = low opacity = creature fading

5. Apply:
   transform.localScale = Vector3.one * baseScale * stressScale * breathMod;
   Color c = spriteRenderer.color;
   c.a = opacityFromStress * (0.5f + 0.5f * pulse);  // pulse modulates around the stress opacity
   spriteRenderer.color = c;
```

**Bond/release visual events:**

```
OnBonded:
- Brief scale pop (punch scale to 1.3x, ease back to 1.0x over 0.3s)
- Optional: small particle burst at creature position

OnReleased:
- Fade alpha to 0 over 0.5s while drifting away
- Or: instant dim + drift (cheaper)

OnStressDeath:
- Rapid flicker (3-4 frames) then fade
- Distinct from voluntary release — this one looks like failure
```

---

## Creature Spawning

Spawning is not part of the creature system — it belongs to a separate SpawnSystem that reads EnvironmentState to determine what appears and when. But the creature must be spawn-ready:

### Creature Prefab Structure

```
[Creature] (Prefab)
├── CreatureSimulation      (simulation)
├── CreatureView            (presentation)
├── SpriteRenderer          (rendering)
├── Rigidbody2D             (physics — Dynamic, low mass, gravity scale 0)
├── CircleCollider2D        (trigger — for force field detection and bonding range)
│   └── isTrigger = true
```

### Spawn Initialization

The spawn system creates the prefab instance and calls an Initialize method:

```csharp
public void Initialize(CreatureType type, EnvironmentState currentEnvironment, CreatureConfig config)
{
    this.creatureType = type;
    this.affinity = GenerateAffinity(currentEnvironment, config.affinityScatter);
    this.bondStatus = BondStatus.Unbound;
    this.currentStress = 0f;
    this.unboundTimer = config.unboundLifetime;
    // CreatureView reads type and affinity on next frame and sets visuals
}

private CreatureAffinityData GenerateAffinity(EnvironmentState env, float scatter)
{
    return new CreatureAffinityData {
        idealTemperature = env.Temperature + Random.Range(-scatter, scatter),
        idealHumidity    = env.Humidity    + Random.Range(-scatter, scatter),
        idealToxicity    = env.Toxicity    + Random.Range(-scatter, scatter),
        idealLight       = env.Light       + Random.Range(-scatter, scatter)
    };
}
```

Creatures spawned in cold conditions are cold-adapted. Creatures spawned during a toxic spike are toxicity-resistant. The scatter prevents clones but preserves environmental influence.

---

## Spawn System (Brief Spec)

Separate MonoBehaviour. Not part of creature architecture but documented here for completeness.

```
SpawnConfig SO:
├── spawnInterval : float (seconds between spawn attempts)
├── spawnRadius : float (distance from screen edge where creatures appear)
├── typeWeights : float[3] (probability weight per creature type)
├── maxUnboundCreatures : int (cap to prevent medium from flooding)
├── spawnDirectionBias : float (how much flow field direction influences spawn location)

SpawnManager:
├── Reads EnvironmentState (for affinity generation)
├── Reads FlowField (for spawn positioning — creatures enter from upstream)
├── Maintains count of active unbound creatures
├── Every spawnInterval: if under cap, select type from weighted random, instantiate at edge, initialize
```

---

## Key Design Decisions

1. **Creatures have no individual energy pool.** Energy is global at holobiont level. Creatures contribute to or drain from the collective — they don't manage their own resources.

2. **Stress is the only dynamic individual state.** Everything else (type, affinity) is immutable after spawn. This keeps the per-creature tick extremely cheap.

3. **Creatures don't make decisions.** They drift or spring. They report stress and efficiency. The holobiont decides what to do with them. This centralizes game logic and prevents emergent bugs from distributed AI.

4. **Unbound creatures have a lifetime.** They don't persist forever in the medium. If not captured, they drift away and despawn. This prevents accumulation and ensures the medium feels like a flow, not a stockpile.

5. **Affinity is born from environment.** Creatures that spawn during a cold phase are cold-adapted. This means the environment indirectly controls what's available — crisis events produce crisis-adapted organisms, which is both mechanically useful and thematically coherent.
