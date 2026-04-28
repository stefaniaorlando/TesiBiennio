# Environment System — Implementation Design

## Overview

The environment is the hostile medium within which the holobiont exists. It is defined by four continuous variables that drift over time with occasional crisis spikes. The environment cannot be controlled, only endured.

The system follows three-layer separation: Configuration (ScriptableObjects) → Simulation (Manager + State) → Presentation (PostProcessing, Particles, FlowField, Audio).

---

## Data Layer

### EnvironmentConfig (ScriptableObject)

One asset. Defines the world's baseline personality.

```
EnvironmentConfig
├── Temperature
│   ├── baseValue : float (starting temperature)
│   ├── minValue : float
│   ├── maxValue : float
│   ├── driftSpeed : float (how fast it oscillates)
│   ├── driftAmplitude : float (how far it swings)
│   └── driftCurve : AnimationCurve (shape of oscillation)
├── Humidity
│   ├── (same fields as Temperature)
├── Toxicity
│   ├── (same fields as Temperature)
├── Light
│   ├── (same fields as Temperature)
```

Each variable has its own drift curve, speed, and amplitude. This allows temperature to oscillate slowly with large swings while toxicity drifts fast with small fluctuations — or any other combination. Curves are AnimationCurves evaluated against time, giving full inspector control over oscillation shape.

### EnvironmentEventConfig (ScriptableObject)

One asset per event type. Create as many as needed: "HeatWave", "ColdSnap", "ToxicBloom", "Drought", etc.

```
EnvironmentEventConfig
├── eventName : string
├── affectedVariables : EnvironmentVariable[] (enum flags: Temp, Humidity, Toxicity, Light)
├── intensityDelta : float (how much the variable changes at peak)
├── rampUpDuration : float (seconds to reach peak)
├── sustainDuration : float (seconds at peak)
├── rampDownDuration : float (seconds to return to baseline)
├── envelopeCurve : AnimationCurve (shape of the spike — allows asymmetric ramps)
```

The envelope curve is evaluated from 0→1 over the event's total lifetime. Multiply its output by intensityDelta to get the current delta at any moment. This gives ADSR-like control: fast attack + slow release, slow build + sudden drop, etc.

### EnvironmentEventScheduleConfig (ScriptableObject)

One asset. Controls event pacing and difficulty.

```
EnvironmentEventScheduleConfig
├── minInterval : float (minimum seconds between events)
├── maxInterval : float (maximum seconds between events)
├── eventPool : EnvironmentEventEntry[]
│   └── EnvironmentEventEntry
│       ├── eventConfig : EnvironmentEventConfig
│       └── weight : float (probability weight for selection)
├── allowOverlap : bool (can multiple events be active simultaneously)
├── maxSimultaneousEvents : int
├── difficultyEscalation : AnimationCurve (x = session time normalized, y = multiplier)
│   // Multiplies event intensity and/or shortens intervals over time
```

---

## Simulation Layer

### EnvironmentVariable (Enum)

```csharp
public enum EnvironmentVariable
{
    Temperature,
    Humidity,
    Toxicity,
    Light
}
```

### EnvironmentState (Plain C# Class)

The single source of truth for current environmental conditions. Not a MonoBehaviour. Owned by EnvironmentManager.

```csharp
public class EnvironmentState
{
    // Raw values
    public float Temperature { get; private set; }
    public float Humidity { get; private set; }
    public float Toxicity { get; private set; }
    public float Light { get; private set; }

    // Normalized values (0–1, mapped from min/max per variable)
    public float TemperatureNormalized { get; private set; }
    public float HumidityNormalized { get; private set; }
    public float ToxicityNormalized { get; private set; }
    public float LightNormalized { get; private set; }

    // Convenience: get as Vector4 for distance calculations
    public Vector4 AsVector => new Vector4(Temperature, Humidity, Toxicity, Light);
    public Vector4 AsNormalizedVector => new Vector4(TemperatureNormalized, HumidityNormalized, ToxicityNormalized, LightNormalized);

    // Events
    public event System.Action<EnvironmentVariable, float> OnVariableChanged;
    public event System.Action<EnvironmentVariable> OnVariableEnteredExtreme;
    public event System.Action<EnvironmentVariable> OnVariableLeftExtreme;

    // Called only by EnvironmentManager
    public void SetValues(float temp, float humidity, float toxicity, float light,
                          EnvironmentConfig config) { /* clamp, normalize, fire events */ }
}
```

### EnvironmentManager (MonoBehaviour)

Owns EnvironmentState. Computes base drift. Receives event deltas from EnvironmentEventSystem.

```
Responsibilities:
- Initialize EnvironmentState from EnvironmentConfig base values on Start
- Every tick: compute base drift per variable from config curves and elapsed time
- Receive event deltas from EnvironmentEventSystem (via public method or direct reference)
- Sum base drift + event deltas
- Clamp to min/max per variable
- Write final values to EnvironmentState
- EnvironmentState fires change events for subscribers

Public interface:
- EnvironmentState State { get; } (read-only access for all other systems)
- void ApplyEventDelta(EnvironmentVariable variable, float delta) (called by EventSystem)
```

**Drift calculation per variable:**

```
float driftValue = config.driftAmplitude * config.driftCurve.Evaluate(
    (Time.time * config.driftSpeed) % 1.0f
);
float baseValue = config.baseValue + driftValue;
```

The curve is evaluated cyclically. Different speeds per variable create non-repeating combined patterns.

### EnvironmentEventSystem (MonoBehaviour)

Separate from EnvironmentManager. Owns the event scheduling and lifecycle.

```
Responsibilities:
- Maintain a timer that fires at random intervals within [minInterval, maxInterval]
- On fire: select an event from the weighted pool
- Create a runtime event instance tracking: elapsed time, total duration, current phase
- Every tick: update all active events, compute current delta from envelope curve
- Send deltas to EnvironmentManager via ApplyEventDelta()
- Remove events when their envelope completes
- Apply difficulty escalation: multiply intensity and/or shorten intervals based on session time

Runtime event tracking (internal class):
- eventConfig : EnvironmentEventConfig
- startTime : float
- totalDuration : float (rampUp + sustain + rampDown)
- GetCurrentDelta(float currentTime) : float
    // Evaluate envelopeCurve at (currentTime - startTime) / totalDuration
    // Multiply by intensityDelta
```

---

## Presentation Layer

All presentation reads from EnvironmentState. Never writes to simulation.

### EnvironmentPostProcessingController (MonoBehaviour)

Reads EnvironmentState normalized values. Drives URP Volume post-processing parameters.

```
Configuration (in inspector):
├── Temperature mappings
│   ├── targetParameter : e.g., Color Adjustments → Temperature
│   └── responseCurve : AnimationCurve (normalized env value → parameter value)
├── Humidity mappings
│   ├── targetParameter : e.g., Vignette → Intensity
│   └── responseCurve : AnimationCurve
├── Toxicity mappings
│   ├── targetParameter : e.g., Chromatic Aberration → Intensity
│   └── responseCurve : AnimationCurve
├── Light mappings
│   ├── targetParameter : e.g., Bloom → Intensity, Exposure
│   └── responseCurve : AnimationCurve
```

Each environmental variable can drive multiple post-processing parameters. The AnimationCurve per mapping allows nonlinear responses — toxicity might be invisible below 0.4 then ramp sharply.

### FlowField (MonoBehaviour or plain class)

A 2D velocity field that gives the medium directional movement. Used by both particles and unbound creature rigidbodies.

```
Configuration:
├── resolution : Vector2Int (grid size, e.g., 20x20)
├── noiseScale : float (Perlin noise sampling scale)
├── baseFlowSpeed : float
├── temperatureTurbulenceMultiplier : float
├── humidityViscosityMultiplier : float

Runtime:
├── velocityGrid : Vector2[,] (computed every tick)

Public interface:
├── Vector2 GetFlowAtPosition(Vector2 worldPosition)
│   // Sample the grid with bilinear interpolation
├── void UpdateField(EnvironmentState state)
│   // Recompute grid based on current conditions
```

Every tick:
1. Sample Perlin noise at each grid point (offset by time for animation)
2. Convert noise to angle, create direction vector
3. Multiply by baseFlowSpeed
4. Modulate magnitude by humidity (high humidity = slow, viscous flow)
5. Add turbulence amplitude proportional to temperature (high temp = chaotic)
6. Store in velocityGrid

Creatures and particles call GetFlowAtPosition() to get the local velocity.

### EnvironmentParticleController (MonoBehaviour)

Manages particle systems that visualize the medium. Reads EnvironmentState.

```
Particle layers:
├── temperatureParticles : ParticleSystem
│   // Speed, trail length, emission rate from temperature
│   // Cold: slow, sharp, tight motion
│   // Hot: fast, expansive, long trails
│   ├── speedCurve : AnimationCurve (temp normalized → particle speed)
│   ├── trailLengthCurve : AnimationCurve
│   ├── emissionRateCurve : AnimationCurve
│   └── noiseCurve : AnimationCurve (temp → particle noise strength)
│
├── suspendedMatter : ParticleSystem
│   // Density from humidity, turbulence from temperature
│   // Ambient debris that gives depth
│   ├── emissionFromHumidity : AnimationCurve
│   ├── sizeFromHumidity : AnimationCurve
│   └── noiseFromTemperature : AnimationCurve
│
├── toxicityParticles : ParticleSystem
│   // Only visible above toxicity threshold
│   // Erratic, clumping, visually disruptive
│   ├── emissionFromToxicity : AnimationCurve
│   ├── speedFromToxicity : AnimationCurve
│   └── colorFromToxicity : Gradient
```

Particles should also be influenced by the FlowField — either through Particle System velocity-over-lifetime modules pointing at the flow direction, or by using a custom particle update that samples the flow field per particle (more expensive but more accurate).

### EnvironmentAudioController (MonoBehaviour)

Reads EnvironmentState. Drives ambient soundscape.

```
Configuration:
├── ambientSource : AudioSource (looping base texture)
├── pitchFromTemperature : AnimationCurve
├── lowpassFromHumidity : AnimationCurve
├── distortionFromToxicity : AnimationCurve
├── volumeFromLight : AnimationCurve
```

Sound shifts before visuals become obvious, giving the player anticipatory information about environmental changes.

---

## Dependency Graph

```
EnvironmentConfig SO ─────→ EnvironmentManager
EventConfig SOs ──────→ EnvironmentEventSystem ──→ EnvironmentManager
EventScheduleConfig SO ───→ EnvironmentEventSystem         │
                                                            ↓
                                                    EnvironmentState
                                                            │
                                    ┌───────────────────────┼───────────────────────┐
                                    ↓                       ↓                       ↓
                        PostProcessingController      FlowField           ParticleController
                                                            │                       │
                                                            ↓                       ↓
                                                    (sampled by              (driven by
                                                     creatures &              flow field +
                                                     particles)               env state)
                                                                    AudioController
```

---

## Implementation Priority

1. EnvironmentState + EnvironmentConfig + EnvironmentManager with basic drift (no events)
2. Debug UI: four sliders showing current values (temporary, remove later)
3. FlowField with visible debug gizmos (draw velocity vectors in scene view)
4. EnvironmentEventSystem with one test event
5. PostProcessingController with one variable mapped
6. ParticleController with temperature particles
7. Remaining presentation layers
8. AudioController (last — needs sound assets)
