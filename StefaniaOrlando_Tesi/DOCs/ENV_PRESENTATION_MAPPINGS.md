# Environment Presentation Mappings

*Single source of truth for which environment variable drives which presentation output. Update this doc **before** wiring a new presentation surface, not after.*

---

## Principle

Each environment variable (**Temperature**, **Humidity**, **Toxicity**, **Light**) claims a subset of presentation outputs across the available presentation surfaces (background shader, post-FX, 2D lights, camera, particles, audio).

**One driver per output.** No presentation parameter has two environment variables fighting for it. Parameters with independent sub-properties (Vignette: intensity vs color vs smoothness; White Balance: temperature vs tint) may have different drivers *per sub-property* — the actual output remains single-driver.

**Variables don't all own equal share.** Some variables own many outputs across surfaces; some own few. Distribution follows what the GDD asks each variable to *feel like*, not symmetry. Toxicity should feel pervasive across many channels; humidity reads through fewer but stronger ones.

---

## Conventions for presentation views

Every script that drives a presentation surface from `EnvironmentManager` must:

1. **List its inputs in the doc-comment header.** First line of the file's `/* */` block names the variables it reads and the outputs it drives.
2. **Group inspector fields by input variable.** Use `[Header("← Temperature")]`, `[Header("← Humidity")]`, etc. Opening the component shows the variable→output relationships at a glance.
3. **Not expose fields for variables it doesn't read.** A view that doesn't read humidity has no humidity-related field. Implicit absence is the contract.
4. **Use one setter per output on the corresponding controller.** Composite setters that touch two parameters at once are forbidden — they hide multi-input mappings and break the matrix.

`EnvironmentPostFXView` + `EnvironmentPostFXController` is the reference implementation. Any new presentation surface should match its shape: a Controller façade with one-setter-per-output, and a View that reads env state and writes through the setters.

---

## The matrix

### Post-Processing (URP Volume) — ✅ wired

| Output | Driven by |
|---|---|
| White Balance — Temperature | **Temperature** |
| Color Adjustments — Saturation | **Temperature** |
| SMH — Midtones tint | **Temperature** |
| Chromatic Aberration | **Temperature** |
| Depth of Field — Max Radius | **Humidity** |
| SMH — Shadows tint | **Toxicity** |
| Vignette — Color | **Toxicity** |
| Vignette — Smoothness | **Toxicity** |
| Film Grain | **Toxicity** |
| Bloom — Intensity | **Light** |
| Vignette — Intensity | **Light** |

*Static (authored on the Volume Profile asset, not driven by env state):* WB tint, exposure, contrast, hue shift, bloom threshold, SMH highlights.

### Background Shader (PetriDishBackground) — ⏳ pending

| Output | Proposed driver |
|---|---|
| Time scale (master) | **Temperature** |
| Cell jitter | **Temperature** |
| Cell scale | **Humidity** |
| Warp strength | **Humidity** |
| Channel intensity | **Light** |
| Pulse strength | **Light** |
| Accent amount | **Toxicity** |
| Particulate grain | **Toxicity** |

### 2D Lights — 🟡 partial

| Output | Driven by | Status |
|---|---|---|
| Global Light 2D — Intensity | **Light** | ✅ `EnvironmentLightingView` |
| Global Light 2D — Color | **Temperature** | ⏳ pending |
| Global Light 2D — Per-light flicker | **Toxicity** | ⏳ pending |
| Sprite Light 2D — Intensity (caustic shimmer) | **Humidity** | ✅ `EnvironmentCausticView` |
| Sprite Light 2D — Position / rotation drift | autonomous (Perlin) | ✅ `EnvironmentCausticView` |

When the **second** env-driven output on a *single* Light2D is wired (e.g. the global light's color landing alongside its intensity), extract a `EnvironmentLightingController` façade so each output gets its own setter (per the convention) and the View stops writing directly to `Light2D`. With each Light2D currently having only one env-driven output, direct writes are still fine.

### Camera (Cinemachine) — ✅ wired

| Output | Driven by | Status |
|---|---|---|
| Multi-Channel Perlin — frequency gain | **Temperature** | ✅ `EnvironmentCameraView` |
| Multi-Channel Perlin — amplitude gain | **Toxicity** | ✅ `EnvironmentCameraView` |
| Impulse on event start | Event lifecycle (not a variable) | ✅ `EnvironmentCameraView` |

The View polls `EnvironmentEventSystem.ActiveEvent` and fires a start impulse when a new event becomes active. Magnitude scales with Σ|intensityDelta| × IntensityMultiplier. A second peak-impulse hook can be added later (the V-progress detection scaffolding has been removed for now to keep the surface minimal).

### Particles — ⏳ pending

Three independent particle layers, each with its own driving variable:

| Layer | Driven by |
|---|---|
| Temperature particles (speed, trails, emission) | **Temperature** |
| Suspended matter (density) | **Humidity** |
| Suspended matter (turbulence) | **Temperature** |
| Toxicity particles (threshold-gated emission, color, speed) | **Toxicity** |

### Flow Field — ✅ wired (sim-side)

`EnvironmentFlowFieldView` writes per-frame multipliers into the FlowField from env state.

| Output | Driven by |
|---|---|
| Base flow speed × humidity multiplier | **Humidity** |
| Base flow speed × temperature multiplier | **Temperature** |
| Turbulence amplitude | **Temperature** |

### Audio — ⏳ deferred (needs sound assets)

| Output | Proposed driver |
|---|---|
| Ambient pitch | **Temperature** |
| Lowpass cutoff | **Humidity** |
| Distortion | **Toxicity** |
| Volume / brightness | **Light** |

---

## Per-variable overview

Stepping back, what each variable owns across the whole presentation:

**Temperature** — WB temperature, saturation, midtones tint, CA, bg time scale, bg cell jitter, suspended-matter turbulence, light color, camera noise frequency, temperature particle layer, audio pitch.

**Humidity** — DoF, sprite-light caustic intensity, bg cell scale, bg warp strength, suspended-matter density, flow viscosity, audio lowpass.

**Toxicity** — Shadows tint, vignette color, vignette smoothness, film grain, bg accent, bg grain, light flicker, camera amplitude, toxicity particle layer, audio distortion.

**Light** — Bloom intensity, vignette intensity, bg channel intensity, bg pulse, global light intensity, audio brightness.

---

## Implementation status (as of 2026-05-06)

| Surface | Status | Reference |
|---|---|---|
| Post-FX | ✅ Implemented | `EnvironmentPostFXView` + `EnvironmentPostFXController` |
| Flow Field | ✅ Implemented | `FlowField` + `EnvironmentFlowFieldView` |
| Background shader | ⏳ Pending | shader exists, no driver script |
| 2D lights | 🟡 Partial | `EnvironmentLightingView` (global intensity) + `EnvironmentCausticView` (sprite caustic) |
| Camera | ✅ Implemented | `EnvironmentCameraView` |
| Particles | ⏳ Pending | — |
| Audio | ⏳ Deferred | needs sound assets |

---

## Process for adding a new presentation surface

1. **Update this doc first.** Add a new table for the surface; fill in proposed drivers per output. No output may have two drivers.
2. **Sanity-check the per-variable overview.** If one variable just gained five new outputs, it may now be overloaded — consider whether some of those should be static instead.
3. **Implement the surface as Controller + View.** Follow the four conventions in *Conventions for presentation views* above.
4. **Update the status table.** ⏳ → ✅ once wired.

When changing an existing allocation, update the matrix in the same PR as the code change. The doc and the code are the contract; they must move together.
