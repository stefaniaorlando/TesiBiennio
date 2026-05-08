# Breath Simulator — Design Document

---

## 1. Purpose

This component simulates player breathing input for a 2D game, allowing development and testing of breath-dependent mechanics without requiring a physical sensor. It provides a keyboard-driven oscillator whose output mimics the data stream a real breathing sensor would produce.

The simulator serves two roles: a development tool for hardware-free iteration, and potentially a standalone input metaphor for builds where the sensor cannot be deployed.

---

## 2. Core Concepts

### 2.1 The Oscillator

The breath simulator is fundamentally a **constrained oscillator** producing a continuous value representing lung displacement. This value cycles between empty (0) and full (1) at a variable rate.

The oscillator does not follow a pure sine wave. It cycles through discrete phases—inhale, pause-full, exhale, pause-empty—allowing asymmetric timing and optional holds. For the initial implementation, however, we simplify to a continuous back-and-forth motion without explicit pause states (the pause button will freeze the oscillator externally rather than inserting pause phases into the waveform).

### 2.2 Depth as Amplitude

Breath depth represents where the breathing originates—shallow thoracic breathing versus deep diaphragmatic breathing. Mechanically, depth acts as an **amplitude scalar** on the oscillator. At minimum depth, the displacement range is compressed (oscillates within a narrow band). At maximum depth, the full 0–1 range is utilized.

Depth has a **baseline** value the system wants to return to. Player input pushes depth toward extremes; releasing input allows it to drift back toward baseline.

### 2.3 Frequency as Speed

Frequency determines how fast the oscillator completes a full breath cycle. Like depth, frequency has a baseline, minimum, and maximum. Player input accelerates or decelerates the cycle; releasing input allows gradual normalization toward baseline.

### 2.4 Stamina as Constraint

Pushing frequency or depth toward extremes costs **stamina**—a shared resource pool. Remaining near baseline regenerates stamina. Pushing both axes simultaneously drains stamina faster than pushing either alone.

When stamina depletes entirely, the system enters **recovery**: player input is ignored, all parameters drift toward baseline, and a recovery timer begins. Input remains locked until recovery completes.

### 2.5 Pause Boost (Breath Holding Reward)

Holding breath (pause) is not just a freeze—it charges energy for the next phase. When the player pauses during one breath phase and releases into the **opposite phase**, the system applies a **velocity boost**.

- Pause during inhale → release → boosted exhale
- Pause during exhale → release → boosted inhale

This rewards intentional breath control. The boost is applied directly to the `velocity` output, so any game system reading velocity automatically benefits without needing special handling.

---

## 3. State Variables

### 3.1 Oscillator State

| Variable | Type | Description |
|----------|------|-------------|
| `phase` | float (0–1) | Current position in breath cycle. 0 = lungs empty, 1 = lungs full. Wraps continuously. |
| `direction` | int (±1) | Current oscillation direction. +1 = inhaling, −1 = exhaling. |
| `isPaused` | bool | Whether the oscillator is frozen. |

### 3.2 Frequency State

| Variable | Type | Description |
|----------|------|-------------|
| `frequencyCurrent` | float | Active cycles per second (or period—implementation choice). |
| `frequencyBaseline` | float | Resting frequency the system normalizes toward. |
| `frequencyMin` | float | Lower bound (slowest breathing). |
| `frequencyMax` | float | Upper bound (fastest breathing). |

### 3.3 Depth State

| Variable | Type | Description |
|----------|------|-------------|
| `depthCurrent` | float (0–1) | Active depth scalar. |
| `depthBaseline` | float | Resting depth the system normalizes toward. |
| `depthMin` | float | Lower bound (shallowest). |
| `depthMax` | float | Upper bound (deepest). |

### 3.4 Stamina State

| Variable | Type | Description |
|----------|------|-------------|
| `staminaCurrent` | float (0–1) | Current stamina level. 1 = full, 0 = depleted. |
| `staminaMax` | float | Maximum stamina (normalized to 1). |

### 3.5 Recovery State

| Variable | Type | Description |
|----------|------|-------------|
| `isRecovering` | bool | Whether input is currently ignored (replaces `isLockedOut`). |
| `recoveryTimer` | float | Time remaining until recovery ends. |
| `recoveryDuration` | float | Total duration of recovery period. |

### 3.6 Pause Boost State

| Variable | Type | Description |
|----------|------|-------------|
| `pausedInDirection` | int (±1) | Which phase the player paused in. +1 = inhale, −1 = exhale. |
| `currentBoost` | float | Active velocity multiplier. 1 = no boost, >1 = boosted. |

---

## 4. Derived Output

These values are computed each frame and exposed for other game systems to consume.

| Output | Type | Derivation |
|--------|------|------------|
| `displacement` | float (0–1) | `phase × depthCurrent` — the actual "lung fill" value at this instant. |
| `velocity` | float | Rate of change of displacement × `currentBoost`. Positive = inhaling, negative = exhaling, zero = paused. **Includes pause boost automatically.** |
| `boost` | float | Current pause boost multiplier (1 = none, >1 = boosted). Exposed for UI/visualization. |
| `normalizedFrequency` | float (0–1) | Where current frequency sits within min–max range. |
| `normalizedDepth` | float (0–1) | Where current depth sits within min–max range. |

**Important:** The `velocity` property already includes the pause boost. Game systems using velocity get the boost automatically without needing to read and apply `boost` separately.

---

## 5. Input Controls

Five input actions, each mapped to a key (configurable):

| Action | Default Key | Behavior |
|--------|-------------|----------|
| Increase Frequency | W | While held: drift `frequencyCurrent` toward `frequencyMax`. |
| Decrease Frequency | S | While held: drift `frequencyCurrent` toward `frequencyMin`. |
| Increase Depth | D | While held: drift `depthCurrent` toward `depthMax`. |
| Decrease Depth | A | While held: drift `depthCurrent` toward `depthMin`. |
| Pause | Space | While held: freeze oscillator (`isPaused = true`). Triggers pause boost on release. |

All inputs are ignored during recovery.

---

## 6. Behavior Rules

### 6.1 Parameter Drift (While Input Held)

When the player holds an input, the corresponding parameter drifts toward its target extreme at a defined **approach rate**. The drift is linear per frame:

```
parameter += direction × approachRate × deltaTime
parameter = clamp(parameter, min, max)
```

### 6.2 Parameter Normalization (While Input Released)

When no input is held for a given axis, that parameter drifts back toward its baseline at a defined **decay rate**. The drift should feel organic—not instant, but perceptible:

```
parameter = moveToward(parameter, baseline, decayRate × deltaTime)
```

The decay rate is exposed in the Inspector as a ratio or absolute value, allowing tuning of how "sticky" extremes feel versus how quickly the system relaxes.

### 6.3 Stamina Drain

Stamina drains whenever either parameter is away from baseline. Drain rate scales with **distance from baseline** on each axis:

```
frequencyDeviation = abs(frequencyCurrent − frequencyBaseline) / (frequencyMax − frequencyMin)
depthDeviation = abs(depthCurrent − depthBaseline) / (depthMax − depthMin)

drainRate = baseDrainRate × (frequencyDeviation + depthDeviation)
```

If both axes are pushed simultaneously, deviations stack, causing faster drain. This is the "budget tension" mechanic.

Holding pause also drains stamina at a defined rate (potentially higher than other actions, since breath-holding is physiologically taxing).

### 6.4 Stamina Regeneration

When both parameters are at or near baseline (within a tolerance threshold) and the oscillator is not paused, stamina regenerates:

```
if frequencyDeviation < tolerance AND depthDeviation < tolerance AND NOT isPaused:
    staminaCurrent += regenRate × deltaTime
    staminaCurrent = clamp(staminaCurrent, 0, staminaMax)
```

### 6.5 Recovery Trigger

When `staminaCurrent` reaches zero:

1. Set `isRecovering = true`
2. Set `recoveryTimer = recoveryDuration`
3. Force `isPaused = false` (release any held breath)
4. Begin forced normalization of all parameters toward baseline

### 6.6 Recovery Behavior

During recovery:

- All player input is ignored
- Parameters continue drifting toward baseline at decay rate
- `recoveryTimer` decrements each frame
- Stamina regenerates (possibly at an accelerated rate to ensure it refills during recovery)

### 6.7 Recovery Release

When `recoveryTimer` reaches zero:

1. Set `isRecovering = false`
2. Input is re-enabled

Note: Stamina need not be fully regenerated to exit recovery—only the timer matters. However, if recovery duration is tuned properly, stamina should be substantially restored by the time recovery ends.

### 6.8 Pause Boost Behavior

When pause begins:

1. Record `pausedInDirection = currentDirection` (which phase we're in)

When pause ends:

1. Check if `currentDirection ≠ pausedInDirection` (releasing into opposite phase)
2. If so, set `currentBoost = pauseBoostMultiplier`
3. Reset `pausedInDirection = 0`

Each frame while not paused:

```
if currentBoost > 1:
    currentBoost -= pauseBoostDecayRate × deltaTime
    currentBoost = max(1, currentBoost)
```

The boost decays back to 1 over time if not consumed by velocity-reading systems.

---

## 7. Visual Feedback

Four UI elements, all implemented as simple sliders/bars for initial pass.

### 7.1 Breath Displacement Bar (Vertical)

- **Represents:** `displacement` (computed output)
- **Behavior:** Fill level oscillates continuously as the breath cycle runs. When paused, fill level freezes.
- **Orientation:** Vertical. Bottom = empty, top = full.
- **Additional feedback:** During recovery, bar could dim or desaturate to indicate loss of control.

### 7.2 Depth Bar (Vertical)

- **Represents:** `depthCurrent` relative to its range
- **Behavior:** Slider handle moves up/down as depth changes. A **baseline marker** (line or notch) indicates where the handle drifts toward when input is released.
- **Orientation:** Vertical. Bottom = shallow (thoracic), top = deep (diaphragmatic).
- **Range visualization:** The bar itself spans min to max; the handle shows current position.

### 7.3 Stamina Bar (Horizontal)

- **Represents:** `staminaCurrent`
- **Behavior:** Depletes left-ward as stamina drains, fills right-ward as it regenerates.
- **Color shift (optional):** Green when healthy, yellow when low, red when critical (near recovery threshold).
- **Recovery visualization:** When recovery triggers, the bar's appearance changes (gray overlay, stripe pattern, or pulsing) to indicate locked state.

### 7.4 Recovery Timer Bar (Horizontal)

- **Represents:** `recoveryTimer` during recovery
- **Behavior:** Only visible (or only filled) during recovery. Depletes as recovery progresses. When empty, recovery ends and bar resets/hides.
- **Alternative:** Could be overlaid on the stamina bar as a different-colored fill sweeping across. Decide during implementation based on visual clarity.

### 7.5 Boost Indicator (Optional)

- **Represents:** `currentBoost` value
- **Behavior:** Only visible when boost > 1. Could be a glow, particle effect, or small indicator near the displacement bar.
- **Purpose:** Gives player feedback that their pause charged energy for the next action.

---

## 8. Inspector-Exposed Parameters

All tuning values should be serialized and adjustable without code changes.

### 8.1 Frequency Parameters

- `frequencyBaseline` — default resting frequency
- `frequencyMin` — slowest allowed
- `frequencyMax` — fastest allowed
- `frequencyApproachRate` — how fast input pushes toward extreme
- `frequencyDecayRate` — how fast it returns to baseline

### 8.2 Depth Parameters

- `depthBaseline` — default resting depth
- `depthMin` — shallowest allowed
- `depthMax` — deepest allowed
- `depthApproachRate` — how fast input pushes toward extreme
- `depthDecayRate` — how fast it returns to baseline

### 8.3 Stamina Parameters

- `staminaMax` — maximum pool (can be 1 if normalized)
- `baseDrainRate` — stamina lost per second at full deviation on one axis
- `regenRate` — stamina gained per second when at baseline
- `regenRateDuringRecovery` — possibly faster, ensuring recovery

### 8.4 Recovery Parameters

- `recoveryDuration` — how long input stays locked after depletion
- `baselineTolerance` — how close to baseline counts as "resting" for regen purposes

### 8.5 Pause Parameters

- `pauseDrainRate` — stamina cost per second of holding breath

### 8.6 Pause Boost Parameters

- `pauseBoostEnabled` — toggle feature on/off (default: true)
- `pauseBoostMultiplier` — velocity multiplier when boost active (default: 2)
- `pauseBoostDecayRate` — how fast boost fades back to 1 (default: 3)

### 8.7 Input Bindings

- Key assignments for each action (or use Unity's Input System and expose action references)

---

## 9. Component Breakdown

For implementation, the system splits into the following logical components:

### 9.1 BreathOscillator

Responsible for the core oscillation logic. Owns `phase`, `direction`, `isPaused`. Exposes `displacement` and `velocity`. Receives `frequencyCurrent` and `depthCurrent` as inputs each frame to compute output.

**Location:** `Assets/Scripts/BreathOscillator.cs`

### 9.2 BreathParameters

Holds current, baseline, min, max for both frequency and depth. Handles drift logic (approach and decay). Exposes methods like `PushFrequencyUp()`, `ReleaseFrequency()`, `Tick(deltaTime)`.

**Location:** `Assets/Scripts/BreathParameters.cs`

### 9.3 StaminaSystem

Manages `staminaCurrent`, drain, and regeneration. Receives deviation values from BreathParameters each frame. Fires an event or sets a flag when depleted.

**Location:** `Assets/Scripts/StaminaSystem.cs`

### 9.4 RecoveryController

Listens for stamina depletion. Manages `isRecovering` and `recoveryTimer`. Blocks input propagation during recovery. Signals when recovery ends.

**Location:** `Assets/Scripts/RecoveryController.cs`

### 9.5 BreathInputHandler

Reads player input. Routes to BreathParameters and BreathOscillator (for pause). Respects recovery state—does nothing if recovering.

**Location:** `Assets/Scripts/BreathInputHandler.cs`

### 9.6 BreathVisualizer

UI slider references. Each frame, reads from the relevant system and updates visual state. No game logic—pure presentation.

**Location:** `Assets/Scripts/BreathVisualizer.cs`

### 9.7 BreathSimulator (Coordinator)

MonoBehaviour that owns or references all the above. Runs the tick order each frame:

1. BreathInputHandler processes input (if not recovering)
2. BreathParameters updates drift
3. StaminaSystem updates drain/regen
4. RecoveryController checks depletion / advances recovery
5. BreathOscillator advances phase
6. **Pause boost updates (decay)**
7. BreathVisualizer refreshes visuals

Also manages pause boost state and integrates it into the `Velocity` property.

**Location:** `Assets/Scripts/BreathSimulator.cs`

---

## 10. Events

The BreathSimulator exposes UnityEvents for other systems to react to breath state changes without polling:

| Event | When Fired |
|-------|------------|
| `OnInhaleStart` | When direction changes to +1 (start inhaling) |
| `OnExhaleStart` | When direction changes to −1 (start exhaling) |
| `OnPauseStart` | When breath is held (Space pressed) |
| `OnPauseEnd` | When breath hold ends (Space released) |
| `OnStaminaLow` | When stamina drops below 25% |
| `OnStaminaDepleted` | When stamina hits zero |
| `OnStaminaFull` | When stamina is fully restored |
| `OnRecoveryStart` | When recovery begins |
| `OnRecoveryEnd` | When recovery ends |

---

## 11. Open Questions / Future Considerations

**Frequency visualization:** Currently implicit (you see/feel the oscillation speed). If playtesting reveals confusion, add an optional frequency indicator.

**Asymmetric breath phases:** Real breathing often has unequal inhale/exhale durations. The current design assumes symmetric oscillation. Could later add `inhaleRatio` parameter to skew timing.

**Sensor integration:** When real hardware is connected, the BreathOscillator would be replaced (or bypassed) by a sensor-reading component that produces the same `displacement` and `velocity` outputs. The rest of the system (stamina, UI, mechanics consuming breath data) remains unchanged. This is the abstraction layer.

**Multiple breath channels:** Two sensors (thoracic and diaphragmatic) could be separate oscillators. Current design unifies them via the depth parameter. If true independence is later needed, BreathOscillator could be duplicated per channel with separate phase tracking.

**Pause boost curve:** Currently linear decay. Could experiment with exponential decay or a curve for different feel.

---

## 12. Summary

The breath simulator is a five-input, single-oscillator system constrained by a shared stamina resource. Players can modulate breath speed and depth within limits, but sustained extremes deplete stamina and trigger a forced recovery period. Holding breath during one phase and releasing into the opposite phase triggers a **pause boost** that amplifies the velocity of the next breath action.

Visual feedback via UI bars communicates oscillator state, depth position, stamina level, and recovery progress. Events allow other game systems to react to breath state changes.

The architecture separates concerns cleanly: oscillation logic, parameter management, resource management, input handling, recovery control, pause boost, and presentation. This modularity supports future extension (sensor integration, multiple channels, additional derived metrics) without restructuring.

---

## 13. File Structure

```
Assets/Scripts/
├── BreathSimulator.cs       # Central coordinator
├── BreathOscillator.cs      # Core oscillation logic
├── BreathParameters.cs      # Frequency/depth management
├── BreathInputHandler.cs    # Keyboard input routing
├── StaminaSystem.cs         # Stamina drain/regen
├── RecoveryController.cs    # Recovery state management
└── BreathVisualizer.cs      # UI updates
```

---

Ready for implementation.
