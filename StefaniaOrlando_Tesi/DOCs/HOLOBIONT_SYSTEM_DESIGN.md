# Holobiont System — Implementation Design

## Overview

The holobiont is the collective organism at screen center. It is composed of the player's core organism plus all bonded creatures. It is sessile — the world moves around it, it does not move. The player's breath drives its energy production, its spatial reach, and its composition changes.

The holobiont has three simulation responsibilities: energy management (the metabolic loop), force field management (breath-driven spatial interaction with unbound creatures), and composition management (bonding, releasing, cascade failure). Presentation is the visual expression of these states.

---

## Data Layer

### HolobiontConfig (ScriptableObject)

One asset. All tuning for holobiont behavior.

```
HolobiontConfig
├── Energy
│   ├── baseEnergyCapacity : float (max energy with zero hubs)
│   ├── startingEnergy : float (energy at game start)
│   ├── baseDrainPerCreaturePerTick : float (metabolic cost of each bonded creature)
│   ├── stressCostMultiplier : float (additional drain per unit of creature stress)
│   ├── environmentMismatchCostMultiplier : float
│   │   // Additional drain from gap between resistance profile and environment
│   │   // This is the collective cost beyond individual creature stress
│
├── Composition
│   ├── baseCarryingCapacity : int (max creatures with zero hubs)
│   ├── energyCapacityPerHub : float (added to max energy per hub creature)
│   ├── carryingCapacityPerHub : int (added to creature cap per hub creature)
│
├── Force Field
│   ├── baseAttractionRadius : float (reach at minimum breath depth)
│   ├── maxAttractionRadius : float (reach at maximum breath depth)
│   ├── attractionStrength : float (force magnitude at radius edge)
│   ├── attractionFalloff : AnimationCurve (distance from center normalized → force multiplier)
│   ├── repulsionStrength : float (force during inhale phase)
│   ├── bondingRange : float (distance within which hold-exhale captures)
│
├── Cascade Failure
│   ├── cascadeTickInterval : float (seconds between creature shedding during cascade)
│   ├── shedSelectionMethod : enum { MostStressed, MostExpensive, Random }
│
├── Breath Mapping
│   ├── depthToEnergyMultiplier : AnimationCurve (breath depth normalized → energy inflow multiplier)
│   ├── frequencyToMetabolicRate : AnimationCurve (breath frequency normalized → global rate multiplier)
│   │   // Affects BOTH inflow and drain — fast breathing speeds everything up
│   ├── breathPhaseToFieldRadius : AnimationCurve (phase -1 to +1 → radius multiplier)
│   │   // -1 = full inhale (contracted), +1 = full exhale (expanded)
```

### BreathInput (Interface / Data Contract)

The breath sensor system provides data through a clean interface. The holobiont consumes it without knowing about hardware.

```csharp
public interface IBreathInput
{
    float Depth { get; }           // 0–1 normalized amplitude
    float Frequency { get; }       // 0–1 normalized rate
    float Phase { get; }           // -1 (full inhale) to +1 (full exhale)
    bool IsHolding { get; }        // true during pause/hold
    bool IsExhaleHold { get; }     // holding while in exhale phase
    bool IsInhaleHold { get; }     // holding while in inhale phase
    float Stamina { get; }         // 0–1 remaining stamina
    bool InRecovery { get; }       // true during refractory period
}
```

This interface is implemented by the actual breath sensor wrapper. For testing, a keyboard-driven mock implementation allows development without hardware.

---

## Simulation Layer

### HolobiontPhase (Enum)

```csharp
public enum HolobiontPhase
{
    Stable,          // Energy inflow >= drain
    Declining,       // Energy inflow < drain, but energy > 0
    CascadeFailure,  // Energy == 0, shedding creatures
    Dead             // All creatures lost, game over
}
```

### ResistanceProfile (Struct)

```csharp
[System.Serializable]
public struct ResistanceProfile
{
    public float temperatureResistance;
    public float humidityResistance;
    public float toxicityResistance;
    public float lightResistance;

    public Vector4 AsVector => new Vector4(
        temperatureResistance, humidityResistance,
        toxicityResistance, lightResistance);

    // How much the environment exceeds our resistance
    public float GetMismatchCost(EnvironmentState env)
    {
        float tempGap = Mathf.Max(0, Mathf.Abs(env.Temperature) - temperatureResistance);
        float humGap  = Mathf.Max(0, Mathf.Abs(env.Humidity)    - humidityResistance);
        float toxGap  = Mathf.Max(0, env.Toxicity               - toxicityResistance);
        float litGap  = Mathf.Max(0, Mathf.Abs(env.Light)       - lightResistance);
        return tempGap + humGap + toxGap + litGap;
    }
}
```

### HolobiontState (Plain C# Class)

```csharp
public class HolobiontState
{
    // Energy
    public float Energy { get; set; }
    public float MaxEnergy { get; set; }         // baseCapacity + hub bonuses
    public float EnergyNormalized => Energy / MaxEnergy;
    public float NetEnergyFlow { get; set; }     // last tick's inflow - drain (for presentation)

    // Composition
    public List<CreatureSimulation> BondedCreatures { get; }
    public int CreatureCount => BondedCreatures.Count;
    public int NutriciCount { get; set; }
    public int ScudoCount { get; set; }
    public int HubCount { get; set; }
    public int CarryingCapacity { get; set; }    // base + hub bonuses
    public bool AtCapacity => CreatureCount >= CarryingCapacity;

    // Resistance
    public ResistanceProfile Resistance { get; set; }

    // Phase
    public HolobiontPhase Phase { get; set; }

    // Events
    public event System.Action<CreatureSimulation> OnCreatureBonded;
    public event System.Action<CreatureSimulation> OnCreatureReleased;
    public event System.Action OnCascadeFailureStarted;
    public event System.Action OnDeath;
}
```

### HolobiontManager (MonoBehaviour)

The core simulation controller. Screen center. Owns HolobiontState.

```
Dependencies:
├── holobiontConfig : HolobiontConfig (SO)
├── breathInput : IBreathInput (injected or found)
├── environmentState : EnvironmentState (from EnvironmentManager)
├── creatureConfig : CreatureConfig (SO, for drain/stress calculations)

Owns:
├── HolobiontState state
├── HolobiontForceField forceField (sibling component)
```

**Tick logic (FixedUpdate recommended for physics consistency):**

```
// 0. Get metabolic rate modifier from breath frequency
float metabolicRate = config.frequencyToMetabolicRate.Evaluate(breathInput.Frequency);
// During recovery, metabolicRate could be forced to a low value

// ── ENERGY INFLOW ──

// 1. Base breath energy from depth
float breathEnergy = config.depthToEnergyMultiplier.Evaluate(breathInput.Depth);
// During recovery, breathEnergy is reduced or zero

// 2. Nutrici conversion
float nutriciInflow = 0f;
foreach (var creature in state.BondedCreatures)
{
    if (creature.Type == CreatureType.Nutrici)
    {
        nutriciInflow += creature.AffinityEfficiency * creatureConfig.nutrici.baseConversionRate;
    }
}

// 3. Total inflow
float totalInflow = breathEnergy * nutriciInflow * metabolicRate;
// Note: if no nutrici, inflow is zero regardless of breathing

// ── ENERGY DRAIN ──

// 4. Base metabolic cost
float baseDrain = state.CreatureCount * config.baseDrainPerCreaturePerTick;

// 5. Individual stress cost
float stressDrain = 0f;
foreach (var creature in state.BondedCreatures)
{
    stressDrain += creature.Stress * config.stressCostMultiplier;
}

// 6. Collective mismatch cost
float mismatchDrain = state.Resistance.GetMismatchCost(environmentState)
                      * config.environmentMismatchCostMultiplier;

// 7. Total drain
float totalDrain = (baseDrain + stressDrain + mismatchDrain) * metabolicRate;

// ── APPLY ──

// 8. Net energy
state.NetEnergyFlow = totalInflow - totalDrain;
state.Energy = Mathf.Clamp(state.Energy + state.NetEnergyFlow * Time.fixedDeltaTime,
                           0f, state.MaxEnergy);

// 9. Phase update
if (state.Energy > 0f)
{
    state.Phase = state.NetEnergyFlow >= 0 ? HolobiontPhase.Stable : HolobiontPhase.Declining;
}
else
{
    if (state.Phase != HolobiontPhase.CascadeFailure)
    {
        state.Phase = HolobiontPhase.CascadeFailure;
        state.OnCascadeFailureStarted?.Invoke();
        StartCoroutine(CascadeFailureLoop());
    }
}
```

**Cascade Failure Coroutine:**

```
IEnumerator CascadeFailureLoop()
{
    while (state.Phase == HolobiontPhase.CascadeFailure)
    {
        if (state.BondedCreatures.Count == 0)
        {
            state.Phase = HolobiontPhase.Dead;
            state.OnDeath?.Invoke();
            yield break;
        }

        // Select creature to shed
        var toShed = SelectCreatureToShed();
        Release(toShed);

        // Check if reduced network can now sustain itself
        // (Recalc happens inside Release, so next tick will compute new drain)
        // If energy recovers above 0 next tick, Phase exits CascadeFailure in the main loop

        yield return new WaitForSeconds(config.cascadeTickInterval);
    }
}
```

**Bond / Release methods:**

```csharp
public bool TryBond(CreatureSimulation creature)
{
    if (state.AtCapacity) return false;

    creature.SetBound();
    state.BondedCreatures.Add(creature);
    creature.transform.SetParent(bondedCreaturesParent);

    UpdateTypeCounts();
    RecalculateDerivedState();

    state.OnCreatureBonded?.Invoke(creature);
    return true;
}

public void Release(CreatureSimulation creature)
{
    creature.SetUnbound();
    state.BondedCreatures.Remove(creature);
    creature.transform.SetParent(null);

    UpdateTypeCounts();
    RecalculateDerivedState();

    state.OnCreatureReleased?.Invoke(creature);
}

private void RecalculateDerivedState()
{
    // Max energy
    state.MaxEnergy = config.baseEnergyCapacity + (state.HubCount * config.energyCapacityPerHub);
    state.Energy = Mathf.Min(state.Energy, state.MaxEnergy);  // clamp if hub lost

    // Carrying capacity
    state.CarryingCapacity = config.baseCarryingCapacity + (state.HubCount * config.carryingCapacityPerHub);

    // Resistance profile — sum of scudo contributions
    var resistance = new ResistanceProfile();
    foreach (var creature in state.BondedCreatures)
    {
        if (creature.Type == CreatureType.Scudo)
        {
            float effectiveness = creature.AffinityEfficiency;
            resistance.temperatureResistance += Mathf.Abs(creature.Affinity.idealTemperature) * effectiveness;
            resistance.humidityResistance    += Mathf.Abs(creature.Affinity.idealHumidity)    * effectiveness;
            resistance.toxicityResistance    += creature.Affinity.idealToxicity                * effectiveness;
            resistance.lightResistance       += Mathf.Abs(creature.Affinity.idealLight)       * effectiveness;
        }
    }
    state.Resistance = resistance;
}

private void UpdateTypeCounts()
{
    state.NutriciCount = state.BondedCreatures.Count(c => c.Type == CreatureType.Nutrici);
    state.ScudoCount   = state.BondedCreatures.Count(c => c.Type == CreatureType.Scudo);
    state.HubCount     = state.BondedCreatures.Count(c => c.Type == CreatureType.Hub);
}
```

**Bound creature positioning:**

HolobiontManager assigns spring targets to all bound creatures. Simple approach: distribute evenly in a circle around center, radius modulated by breath phase.

```
void UpdateBoundCreaturePositions()
{
    float breathRadius = baseOrbitRadius *
        config.breathPhaseToFieldRadius.Evaluate(breathInput.Phase);

    int count = state.BondedCreatures.Count;
    for (int i = 0; i < count; i++)
    {
        float angle = (i / (float)count) * Mathf.PI * 2f;
        // Add slight per-creature offset for organic feel
        angle += creature.GetHashCode() * 0.1f;
        Vector2 target = new Vector2(
            Mathf.Cos(angle) * breathRadius,
            Mathf.Sin(angle) * breathRadius
        );
        state.BondedCreatures[i].SetSpringTarget(target, springStrength);
    }
}
```

This creates the visual breathing effect: creatures orbit closer on inhale, farther on exhale. The hash-based angle offset prevents perfectly symmetric arrangements.

---

### HolobiontForceField (MonoBehaviour — sibling to HolobiontManager)

Computes the breath-driven force field and handles capture/shed triggers.

```
Dependencies:
├── holobiontConfig : HolobiontConfig (SO — shared with manager)
├── breathInput : IBreathInput
├── holobiontManager : HolobiontManager (sibling, for Bond/Release calls)

State:
├── currentRadius : float (computed from breath depth and phase)
├── currentStrength : float
├── isCapturing : bool (true during hold-exhale)
├── isShedding : bool (true during hold-inhale)
```

**Tick logic:**

```
// 1. Compute field radius from breath
float depthFactor = Mathf.Lerp(config.baseAttractionRadius, config.maxAttractionRadius, breathInput.Depth);
float phaseFactor = config.breathPhaseToFieldRadius.Evaluate(breathInput.Phase);
currentRadius = depthFactor * phaseFactor;

// 2. Determine field mode
if (breathInput.IsExhaleHold)
{
    // Freeze field — capture mode
    isCapturing = true;
    isShedding = false;
    // Field stays at current radius, no force applied
    // Check for creatures in bonding range
    CaptureCreaturesInRange();
}
else if (breathInput.IsInhaleHold)
{
    // Contraction — shed mode
    isCapturing = false;
    isShedding = true;
    // Trigger shed on first frame of hold
    if (justStartedInhaleHold)
    {
        holobiontManager.ShedMostStressed();
    }
}
else if (breathInput.Phase > 0)
{
    // Exhaling — attract
    isCapturing = false;
    isShedding = false;
    currentStrength = config.attractionStrength;
}
else
{
    // Inhaling — repel or neutral
    isCapturing = false;
    isShedding = false;
    currentStrength = -config.repulsionStrength;
    // Negative = push away
}
```

**Public interface for creatures:**

```csharp
public Vector2 GetForceAtPosition(Vector2 position)
{
    if (isCapturing) return Vector2.zero;  // field frozen during capture

    Vector2 toCenter = (Vector2.zero - position);  // holobiont at origin
    float distance = toCenter.magnitude;

    if (distance > currentRadius) return Vector2.zero;  // outside field

    float normalizedDist = distance / currentRadius;
    float falloff = holobiontConfig.attractionFalloff.Evaluate(normalizedDist);
    Vector2 direction = toCenter.normalized;

    return direction * currentStrength * falloff;
    // Positive strength = attraction (toward center)
    // Negative strength = repulsion (away from center)
}
```

**Capture logic:**

```csharp
private void CaptureCreaturesInRange()
{
    // Find all unbound creatures within bonding range
    Collider2D[] hits = Physics2D.OverlapCircleAll(Vector2.zero, config.bondingRange);
    foreach (var hit in hits)
    {
        var creature = hit.GetComponent<CreatureSimulation>();
        if (creature != null && creature.Status == BondStatus.Unbound)
        {
            holobiontManager.TryBond(creature);
        }
    }
}
```

---

## Presentation Layer

### HolobiontView (MonoBehaviour)

Reads HolobiontState and force field state. Visual expression of the collective.

```
Dependencies:
├── holobiontState : HolobiontState
├── forceField : HolobiontForceField
├── breathInput : IBreathInput

Child objects:
├── forceFieldVisual : SpriteRenderer (or LineRenderer for ring)
├── coreOrganism : SpriteRenderer (the player's visual presence at center)
```

**Force field visualization:**

```
// Ring or radial gradient that shows current reach
forceFieldVisual.transform.localScale = Vector3.one * forceField.currentRadius * 2f;

// Alpha based on breath phase — brighter during exhale, dimmer during inhale
float alpha = Mathf.Lerp(0.05f, 0.2f, Mathf.InverseLerp(-1f, 1f, breathInput.Phase));

// During capture mode: pulse brighter
if (forceField.isCapturing)
{
    alpha = 0.4f + 0.1f * Mathf.Sin(Time.time * 10f);  // rapid pulse
}

// Color: neutral during attract, warm during capture, cool during shed
Color fieldColor = forceField.isCapturing ? captureColor :
                   forceField.isShedding  ? shedColor :
                   defaultFieldColor;
fieldColor.a = alpha;
forceFieldVisual.color = fieldColor;
```

**Core organism:**

The player's visual presence at center. Pulses with breath. Scale responds to breath depth. Color/brightness reflects holobiont energy level.

```
float breathScale = 1.0f + breathInput.Phase * 0.15f;  // ±15% with breath
float energyBrightness = holobiontState.EnergyNormalized;  // dim when low

coreOrganism.transform.localScale = Vector3.one * breathScale;
coreOrganism.color = Color.Lerp(lowEnergyColor, fullEnergyColor, energyBrightness);
```

**Phase-dependent visual quality (driven by HolobiontState.Phase):**

```
Stable:     creatures bright, smooth orbiting, field visible
Declining:  creatures slightly dimmer, orbiting tighter, field flickers subtly
Cascade:    creatures flickering, rapid detachments visible, field collapses
Dead:       core organism fades to black
```

These are emergent from per-creature stress visualization plus the force field behavior — HolobiontView may only need to handle the core organism and field ring. The rest comes from CreatureView responding to individual stress.

**Events (subscribe to HolobiontState events):**

```
OnCreatureBonded:    brief particle burst at bond point, maybe a connection line flash
OnCreatureReleased:  brief visual snap, creature dims and drifts (handled by CreatureView)
OnCascadeFailureStarted: screen-edge vignette pulse, ambient audio shift
OnDeath:             fade to black / game over screen trigger
```

---

## GameObject Structure

```
[Holobiont] (at world origin, never moves)
├── HolobiontManager          (simulation — energy, composition, positioning)
├── HolobiontForceField       (simulation — breath→forces, capture/shed)
├── HolobiontView             (presentation — field ring, core, phase effects)
├── SpriteRenderer             (core organism visual)
├── ForceFieldRing             (child object)
│   └── SpriteRenderer         (radial gradient or ring sprite)
├── [BondedCreatures]          (empty transform, parent for bound creatures)
│   ├── [Creature]
│   ├── [Creature]
│   └── ...
```

---

## Dependency Graph

```
IBreathInput ──────→ HolobiontManager ──────→ HolobiontState
                │                                    │
                ├──→ HolobiontForceField ────────────┤ (Bond/Release calls)
                │         │                          │
                │         ↓                          │
                │    Unbound creatures               │
                │    (GetForceAtPosition)             │
                │                                    │
EnvironmentState ──→ HolobiontManager                │
                     (for mismatch cost)             │
                                                     │
CreatureSimulation ←── reads stress/efficiency ──→ HolobiontManager
                                                     │
HolobiontConfig SO ─→ HolobiontManager               ↓
                   ─→ HolobiontForceField      HolobiontState
                                                     │ (read only)
                                                     ↓
                                              HolobiontView
```

---

## Implementation Priority

1. HolobiontState + HolobiontConfig + HolobiontManager with hardcoded energy values (no breath yet)
   — Test: add creatures manually, watch energy drain, observe cascade failure
2. Mock breath input (keyboard: hold Space = exhale, release = inhale, Shift = hold)
   — This unblocks everything without requiring hardware
3. HolobiontForceField with basic attraction/repulsion
   — Test: unbound creatures drift toward holobiont on "exhale," away on "inhale"
4. Capture and release mechanics (hold-exhale, hold-inhale)
5. Energy inflow connected to breath depth and nutrici efficiency
6. Bound creature spring positioning with breath modulation
7. HolobiontView — force field ring and core organism visuals
8. Connect real breath sensor via IBreathInput implementation
9. Cascade failure polish (visual, audio, timing)

---

## Key Design Decisions

1. **Holobiont is at world origin, always.** Sessile. No movement. Screen center. Simplifies all spatial calculations — positions are effectively screen-space.

2. **Energy is global and instant.** No local pools, no distribution delay, no topology. Every nutrici contributes to one pool. Every creature drains from one pool. This keeps the system legible and the implementation simple.

3. **Composition changes trigger recalculation.** Resistance profile and carrying capacity are not recomputed every frame — only when a creature bonds or is released. This is an important optimization for larger holobionts.

4. **The force field is the player's primary verb.** Breathe to attract, hold to capture, hold-inhale to shed. Everything the player does to change the holobiont's composition flows through the force field. There are no menus, no selection UI, no explicit creature management.

5. **Cascade failure is erosion, not cliff.** Gradual shedding with possibility of recovery. The player watches the network dissolve and can fight to stabilize it. This is the game's most dramatic moment and it emerges entirely from the energy system — no special scripting needed beyond the shed loop.

6. **Metabolic rate is global.** Breath frequency speeds up both inflow and drain. This prevents fast breathing from being a pure advantage — it's a tempo change, not a power boost. The player must find sustainable rhythms.
