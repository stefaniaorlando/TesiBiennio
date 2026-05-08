# Circle 2D Movement & Ability System

## Overview

This document describes the modular movement and ability system for the 2D circle character. The system is built on top of the Breath Simulator and allows breath mechanics to drive character movement and interactions.

---

## Architecture

```
┌─────────────────────────────────────────┐
│         CharacterController2D           │
│      (Base: physics, state, API)        │
└─────────────────┬───────────────────────┘
                  │
      ┌───────────┴───────────┐
      ▼                       ▼
┌─────────────┐       ┌──────────────┐
│ Movement    │       │ Field        │
│ Abilities   │       │ Abilities    │
│ (pick one)  │       │ (stackable)  │
├─────────────┤       ├──────────────┤
│ • Wander    │       │ • Polarity   │
│ • Pulse     │       │ • Absorb     │
│ • Tension   │       │              │
└─────────────┘       └──────────────┘
```

### Design Principles

1. **Separation of Concerns**: Base controller handles physics only. Abilities add behavior.
2. **Composability**: Mix and match abilities freely.
3. **Breath Integration**: Abilities read from `BreathSimulator` - no direct input handling.

---

## Components

### CharacterController2D (Base)

The foundation. Does nothing on its own - just a physics vessel.

**Location**: `Assets/Scripts/Circle2D/CharacterController2D.cs`

**What it provides**:
- Owns the `Rigidbody2D`
- Tracks state (velocity, facing direction)
- Exposes API for abilities to use

**Public API for Abilities**:

| Method | Purpose |
|--------|---------|
| `AddForce(force, mode)` | Apply movement force |
| `SetDragModifier(multiplier)` | Set drag multiplier for this frame |
| `AddDragModifier(extra)` | Stack additional drag |
| `SetVelocity(velocity)` | Directly set velocity (use sparingly) |
| `Stop()` | Halt all movement |

**Properties**:

| Property | Type | Description |
|----------|------|-------------|
| `Velocity` | Vector2 | Current velocity |
| `Speed` | float | Velocity magnitude |
| `FacingDirection` | Vector2 | Last non-zero movement direction |
| `Position` | Vector2 | Current position |
| `BaseDrag` | float | Base drag value |

**Setup**:
1. Add to GameObject
2. Requires `Rigidbody2D` (auto-added)
3. Set `Gravity Scale = 0` on Rigidbody2D
4. Adjust `baseDrag` (default: 2)

---

## Movement Abilities

Movement abilities control HOW the character moves. **Use only one at a time** - they are mutually exclusive by design (though technically stackable).

### MovementAbility_Wander

**Purpose**: Autonomous drifting movement using Perlin noise.

**Location**: `Assets/Scripts/Circle2D/Abilities/MovementAbility_Wander.cs`

**How it works**:
- Samples 2D Perlin noise each frame
- Applies force in the noise-derived direction
- Creates smooth, organic wandering paths

**Settings**:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `wanderForce` | 5 | Strength of wandering |
| `noiseSpeed` | 0.3 | How fast direction changes |
| `noiseScale` | 1 | Affects path curvature |

**Use when**: You want the character to move on its own without player input.

---

### MovementAbility_Pulse (Jellyfish)

**Purpose**: Breath-driven rhythmic movement like a jellyfish.

**Location**: `Assets/Scripts/Circle2D/Abilities/MovementAbility_Pulse.cs`

**How it works**:
- **Inhale**: Increases drag (slows down, contracts)
- **Exhale**: Bursts forward in facing direction
- **Pause**: Drifts naturally

**Settings**:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `inhaleDragMultiplier` | 2 | How much drag increases during inhale |
| `burstForce` | 8 | Force applied during exhale |
| `directionAlignment` | 0.7 | 0 = random burst, 1 = aligned with facing |

**Breath integration**:
- Force scales with `breath.Velocity` (faster exhale = bigger burst)
- Force scales with `breath.Depth` (deeper breath = stronger effect)
- Pause boost automatically amplifies the next exhale

**Use when**: You want continuous, rhythmic propulsion tied to breathing.

---

### MovementAbility_Tension (Bow & Arrow)

**Purpose**: Charge-and-release movement. Build up energy, then burst.

**Location**: `Assets/Scripts/Circle2D/Abilities/MovementAbility_Tension.cs`

**How it works**:
- **Inhale**: Charges tension, increases drag
- **Exhale**: Releases stored tension as force
- **Pause**: Holds current tension

**Key difference from Pulse**: Tension ACCUMULATES. Longer inhale = more stored energy = bigger release.

**Settings**:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `chargeRate` | 2 | How fast tension builds |
| `maxTension` | 1 | Maximum storable tension |
| `chargeDragMultiplier` | 2.5 | Drag while charging |
| `releaseForce` | 15 | Force multiplier on release |
| `releaseRate` | 3 | How quickly tension depletes |
| `directionAlignment` | 0.5 | 0 = random, 1 = aligned |

**Properties**:

| Property | Description |
|----------|-------------|
| `Tension` | Current stored tension (0 to max) |
| `NormalizedTension` | Tension as 0-1 value (useful for UI) |

**Use when**: You want strategic, intentional bursts rather than continuous rhythm.

---

## Field Abilities

Field abilities affect OBJECTS AROUND the character. They are stackable - you can use multiple.

### FieldAbility_Polarity (Attract/Repel)

**Purpose**: Create a force field that pushes or pulls nearby objects based on breath.

**Location**: `Assets/Scripts/Circle2D/Abilities/FieldAbility_Polarity.cs`

**How it works**:
- **Inhale**: Attracts objects toward character
- **Exhale**: Repels objects away from character
- **Pause**: Configurable (sustain, off, always attract, always repel)

**Settings**:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `fieldRadius` | 5 | How far the field reaches |
| `baseForce` | 10 | Force strength |
| `affectedLayers` | Everything | Which objects respond |
| `distanceFalloff` | 1 | 0 = constant, 1 = linear, 2 = inverse square |
| `minDistance` | 0.5 | Prevents extreme forces when very close |
| `pauseBehavior` | SustainLast | What happens during pause |

**Pause behaviors**:
- `NoForce`: Field turns off
- `SustainLast`: Keep last polarity
- `AlwaysAttract`: Always pull during pause
- `AlwaysRepel`: Always push during pause

**Affected objects must have**:
- `Rigidbody2D` component
- Be on a layer included in `affectedLayers`

---

### FieldAbility_Absorb (Consume & Grow)

**Purpose**: Eat objects on contact and grow.

**Location**: `Assets/Scripts/Circle2D/Abilities/FieldAbility_Absorb.cs`

**How it works**:
- When a consumable object touches the character, it's destroyed
- Character scale increases slightly

**Settings**:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `consumableTag` | "Consumable" | Tag for edible objects |
| `growthAmount` | 0.05 | Scale increase per object |
| `maxScale` | 3 | Maximum size cap |

**Setup for consumable objects**:
1. Add a `Collider2D` (any type)
2. Set tag to "Consumable" (or your custom tag)
3. That's it!

**Properties**:

| Property | Description |
|----------|-------------|
| `TotalConsumed` | Count of objects eaten |

---

## Pause Boost System

Built into `BreathSimulator`. Rewards intentional breath holding.

**How it works**:
1. Pause during inhale
2. Release pause (breath transitions to exhale)
3. The exhale is BOOSTED (faster velocity)

Or vice versa: pause during exhale → boosted inhale.

**Settings** (in BreathSimulator):

| Parameter | Default | Description |
|-----------|---------|-------------|
| `pauseBoostEnabled` | true | Toggle feature on/off |
| `pauseBoostMultiplier` | 2 | How much stronger |
| `pauseBoostDecayRate` | 3 | How fast it fades |

**Integration**: The boost is baked into `breath.Velocity`. Abilities using velocity automatically benefit - no extra code needed.

---

## Example Configurations

### Autonomous Creature
- `CharacterController2D`
- `MovementAbility_Wander`
- `FieldAbility_Polarity`
- `FieldAbility_Absorb`

Creature wanders on its own, breath controls attract/repel, eats things it touches.

### Player-Controlled Jellyfish
- `CharacterController2D`
- `MovementAbility_Pulse`
- `FieldAbility_Polarity`

No auto-movement. Player breathes to move and manipulate objects.

### Strategic Predator
- `CharacterController2D`
- `MovementAbility_Tension`
- `FieldAbility_Polarity`
- `FieldAbility_Absorb`

Charge up, burst toward prey, attract them in, consume.

---

## Quick Setup Guide

1. **Create character GameObject**
   - Add `SpriteRenderer` with circle sprite
   - Add `CircleCollider2D`
   - Add `Rigidbody2D` (Gravity Scale = 0)

2. **Add base controller**
   - Add `CharacterController2D`

3. **Add movement ability** (pick one)
   - Add `MovementAbility_Wander`, OR
   - Add `MovementAbility_Pulse`, OR
   - Add `MovementAbility_Tension`

4. **Add field abilities** (any combination)
   - Add `FieldAbility_Polarity`
   - Add `FieldAbility_Absorb`

5. **Wire up references**
   - Assign `CharacterController2D` reference in abilities
   - Assign `BreathSimulator` reference in breath-driven abilities

6. **Create boundaries** (optional)
   - Add box colliders around play area
   - Create bouncy Physics Material 2D
   - Assign to boundary colliders

7. **Create consumables** (if using Absorb)
   - Create small objects with `Collider2D`
   - Tag them as "Consumable"

---

## File Structure

```
Assets/Scripts/Circle2D/
├── CharacterController2D.cs          # Base physics controller
└── Abilities/
    ├── MovementAbility_Wander.cs     # Autonomous drift
    ├── MovementAbility_Pulse.cs      # Jellyfish movement
    ├── MovementAbility_Tension.cs    # Charge/release movement
    ├── FieldAbility_Polarity.cs      # Attract/repel field
    └── FieldAbility_Absorb.cs        # Consume and grow
```
