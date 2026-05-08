# Designer Guide — Editing & Balancing the Game

This guide is for an intermediate Unity student who wants to **tune, rebalance, reskin, and extend** the game without writing C#. Everything described here is done in the Unity Editor (Inspector, Project window, Scene view).

It assumes you already know the Unity basics: opening a project, the Inspector, Project / Hierarchy / Scene / Game windows, Play mode, and prefabs. If you don't, watch any 30-minute "Unity for absolute beginners" video first.

---

## 1. Mental model — how this game is wired

The game is split into three layers. Knowing where each thing lives saves you a lot of clicking.

| Layer | What it is | Where to edit |
| --- | --- | --- |
| **Configs (data)** | ScriptableObject `.asset` files that hold balancing numbers, curves, colors. **Persistent — survive Play mode.** | `Assets/Configs/` |
| **Scene objects (visuals & wiring)** | GameObjects in `Assets/Scenes/Game.unity` with View/Manager components. Inspector fields here are also persistent. | Hierarchy → select GameObject |
| **Prefabs (creature visuals)** | The Nutrice / Scudo / Hub prefabs the spawner instantiates. | `Assets/Prefabs/` |

**Three core systems orchestrate everything:**

- **Holobiont** — the player-controlled creature. Has energy, capacity, breath field, tendril network, force field.
- **Environment** — four simulated variables (temperature, light, humidity, toxicity) that drift over time and react to events.
- **Spawner** — drops creatures (Nutrici / Scudi / Hubs) into the world over time. The player attracts or repels them with breath.

A creature is "good" when the environment matches its **affinity**. A bonded creature gives the holobiont a benefit (energy, defense, capacity); a stressed bonded creature drains energy and eventually dies.

---

## 2. Workflow basics (read this once, save yourself an hour)

### 2.1 Editing a config asset

1. In the Project window, navigate to `Assets/Configs/`.
2. Click any `.asset` (e.g. `HolobiontConfig.asset`).
3. Its fields appear in the Inspector. Edit them like any component.
4. **Press Ctrl+S** (saves the project, not the scene). Configs only persist when the project is saved.

### 2.2 Play-mode caveat

> **Anything you change while Play mode is running gets reverted when you press Stop** — for scene objects.
> **Configs (`.asset` files) are the exception**: edits made during Play mode persist.

This is your main superpower. To rebalance:

1. Press **Play**.
2. Open a config in the Inspector and tweak it live — see results immediately.
3. The change is saved, no copy-paste needed.

A safer habit: edit configs in Play mode for *fast iteration*, then verify in a fresh Play session that the new values still feel right.

### 2.3 The AnimationCurve editor

Many config fields are **AnimationCurve** (a wavy graph icon). Click it to open the curve editor.

- **X axis** = an input (normalized time, distance, difficulty, breath phase). The Inspector tooltip says which.
- **Y axis** = the output multiplier or value.
- Right-click a key to change tangent type (Linear / Smooth / Constant).
- Hold Shift while dragging a tangent to break it.

You don't need to draw fancy curves. A straight line from (0,0) to (1,1) is fine for most starts; bend it later when you want non-linear feel.

### 2.4 Don't break references

If you delete a config asset that is referenced from somewhere else (a Scene component, another config, a prefab), the reference becomes "Missing". Right-click an asset → **Find References in Project** before deleting anything.

---

## 3. Config inventory — where every number lives

All configs live in `Assets/Configs/`. Here is what each one controls, in plain language. The **bold** fields are the ones a designer typically wants first; the rest are deeper tuning.

### 3.1 `HolobiontConfig.asset` — the player creature

| Field | What it does |
| --- | --- |
| **`baseEnergyCapacity`** | Max energy with no Hubs bonded. Higher = more forgiving. |
| **`startingEnergy`** | Energy you begin a run with. |
| **`baseDrainPerCreaturePerSecond`** | How much energy each bonded creature costs. Big lever for difficulty. |
| `stressCostMultiplier` | Extra drain when bonded creatures are stressed. |
| `environmentMismatchCostMultiplier` | Extra drain when the environment punishes the holobiont. |
| **`baseCarryingCapacity`** | How many creatures you can bond before needing Hubs. |
| `energyCapacityPerHub` / `carryingCapacityPerHub` | Hub bonuses (currently 0 in Phase 1 — turn on if you want Hubs to matter mechanically). |
| `cascadeTickInterval` | Speed at which the holobiont sheds creatures during failure. Smaller = faster death spiral. |
| **`depthToEnergyMultiplier`** *(curve)* | Maps **breath depth → energy inflow rate**. Make it steep at the high end to reward deep breathing. |
| **`frequencyToMetabolicRate`** *(curve)* | Maps **breath frequency → metabolic multiplier** (scales both inflow and drain). |
| `baseOrbitRadius` / `breathPhaseToOrbitRadius` *(curve)* | How bonded creatures sit and pulse around you. Mostly visual feel. |
| `boundSpringStrength` | How tightly bonded creatures snap to their orbit. Higher = stiffer. |
| `breathField` | Reference to the BreathFieldConfig (see below). |

### 3.2 `BreathConfig.asset` — the breathing mechanic

Controls how breath frequency, depth, and stamina behave. Player input (mic / keyboard) drives target values; these fields control how the system responds.

| Group | Fields | Why you'd touch them |
| --- | --- | --- |
| Frequency | `frequencyBaseline`, `frequencyMin/Max`, `frequencyApproachRate`, `frequencyDecayRate` | Make breathing feel snappier or more sluggish. |
| Depth | `depthBaseline`, `depthMin/Max`, `depthApproachRate`, `depthDecayRate` | Same but for depth/amplitude. |
| Stamina | `staminaMax`, `baseDrainRate`, `pauseDrainRate`, `regenRate`, `regenRateDuringRecovery`, `baselineTolerance`, `recoveryDuration` | Lung-capacity feel. Big drain + slow regen = punishing. |
| Pause boost | `pauseBoostEnabled`, `pauseBoostMultiplier`, `pauseBoostDecayRate` | Reward (or remove) the bonus you get for holding breath. |

### 3.3 `BreathFieldConfig.asset` — the breath ring around the holobiont

Controls the radius, force, and color of the field that attracts/repels creatures.

- **`baseRadius` / `maxRadius`** — reach at min and max breath depth.
- `radiusPerHub` — extra reach per bonded Hub.
- `breathPhaseToRadius` *(curve)* — visual contraction on inhale, expansion on exhale.
- **`attractionStrength` / `repulsionStrength`** — how forcefully you pull creatures in / push them out.
- `attractionFalloff` *(curve)* — how force scales with distance.
- **`idleColor` / `captureColor` / `shedColor`** — ring tints for the three states.
- `minRingAlpha` / `maxRingAlpha` — ring opacity at full inhale / exhale.

### 3.4 Creature configs (`Assets/Configs/Creatures/`)

Three concrete assets, all derived from `CreatureConfig`:

- **`NutriciConfig.asset`** — produces energy. `baseConversionRate` is the only unique field.
- **`ScudoConfig.asset`** — provides defense. `baseResistanceContribution` is the only unique field.
- **`HubConfig.asset`** — expands capacity. `energyCapacityBonus`, `carryingCapacityBonus`.

All three share the **base creature fields** (edit them on each asset individually):

| Field | What it does |
| --- | --- |
| **`displayName`** | Label in debug overlays. |
| **`prefab`** | The visual prefab the spawner instantiates (see §6 for reskinning). |
| **`baseColor`** | Tint before affinity/stress modulate it. |
| **`affinityFalloffCurve`** *(curve)* | The most important balance lever. X = how far the environment is from this creature's preference (0 = perfect match, 1 = total mismatch). Y = efficiency 0..1. A steep curve = creatures only thrive in narrow conditions. A flat curve = creatures are robust. |
| `stressDeathThreshold` | Stress at which a bonded creature gives up and dies. Lower = more fragile. |
| `affinityScatter` | Random per-creature deviation from the species average preference. Higher = more variety per individual. |
| `unboundLifetime` | Seconds an unbound creature drifts before despawning. 0 = forever. |
| `unboundLinearDamping` / `boundLinearDamping` | Physics damping. Bound is high (springy & stable); unbound is low (free drift). |

### 3.5 `EnvironmentConfig.asset` — the world's four variables

Four nested groups (Temperature / Light / Humidity / Toxicity), each with:

- **`baseValue`** — value at game start.
- `minValue` / `maxValue` — clamped range.
- `extremeLowNormalized` / `extremeHighNormalized` — thresholds (in 0..1 normalized space) for "low extreme" and "high extreme" states.

Defaults are sensible (e.g. temperature -50..+50, baseline 0). Touch these to redefine the world's "neutral".

### 3.6 `DifficultyConfig.asset` — how the game ramps up

- **`initialDifficulty`** — start value (0..1). 0 means you start as easy as possible.
- **`timeToMax`** — seconds until difficulty hits 1.0. Large = long, gentle ramp.
- **`progressEasing`** *(curve)* — shape the ramp. A curve that stays flat then jumps gives a "calm intro then sudden ramp" feel.
- `eventIntensityMul` *(curve)* — multiplies event impact by difficulty.
- `eventFrequencyMul` *(curve)* — multiplies event spawn frequency by difficulty.
- `driftAmplitudeMul` *(curve)* — multiplies environment-drift amplitude by difficulty.

These three curves are the "what does harder mean?" sliders. If you want harder = more frequent events but not more intense, raise `eventFrequencyMul` and keep `eventIntensityMul` flat.

### 3.7 `DefaultEventSchedule.asset` — what events can happen and when

This is the **event pool**. It contains:

- **`minInterval` / `maxInterval`** — how long between events at base difficulty.
- **`eventPool[]`** — a list of `EnvironmentEventConfig` references.

Events are picked by **weighted random** from this pool, filtered by the current difficulty.

### 3.8 Event configs (`Assets/Configs/Events/`)

There are 25 events (`01_WarmBreeze` … `25_Cataclysm`). Each is its own asset and has:

- **`eventName`** — display label.
- **`weight`** — how often it gets picked (relative to other eligible events). Set to 0 to disable.
- **`minDifficulty` / `maxDifficulty`** — only eligible inside this difficulty band. Cataclysm-tier events should have high `minDifficulty` (e.g. 0.8); gentle events should have low `maxDifficulty`.
- **`effects[]`** — one or more `(affected variable, intensityDelta)` pairs. Negative delta pushes the variable down, positive pushes up.
- **`rampUpDuration` / `sustainDuration` / `rampDownDuration`** — shape of the event's lifetime.
- **`envelopeCurve`** *(curve)* — overall intensity-over-time. Default is a smooth bell.

To **add a new event**:
1. In the Project window, right-click in `Assets/Configs/Events/` → **Create → Stefania → Environment Event** (or whatever the menu label is — Unity uses the `[CreateAssetMenu]` from the script).
2. Fill in the fields above.
3. Open `DefaultEventSchedule.asset` and drag the new event into `eventPool[]`.

### 3.9 `FlowFieldConfig.asset` — how unbound creatures drift

The world has a Perlin-noise flow field that moves unbound creatures.

- **`noiseScale` / `turbulenceNoiseScale`** — eddy size. Small = giant smooth currents; large = noisy chop.
- **`temporalScale`** — how fast the field evolves. 0 = static, 1 = swirling.
- **`baseFlowSpeed`** — how strong the wind is.
- **`enableInwardBias` / `inwardBiasByDistance`** *(curve)* — pulls creatures toward the center, useful so they don't drift off-screen.
- The `gizmo*` fields only affect the Scene-view debug overlay (the colored arrows). They never appear in-game.

### 3.10 `SpawnerConfig.asset` — who spawns, how often

- **`spawnInterval`** — seconds between spawn attempts.
- **`maxAlive`** — soft cap on simultaneously **unbound** creatures (bound ones don't count).
- `upstreamSampleCount` — how many perimeter points to sample so spawns favor the windward edge. Leave alone.
- `useSpawnInset` / `spawnInset` — keep spawns away from the rim.
- `useSpawnInwardKick` / `spawnInwardSpeed` — give a small push toward the center at spawn.
- **`nutriciConfig` / `nutriciWeight`**, **`scudoConfig` / `scudoWeight`**, **`hubConfig` / `hubWeight`** — which creature configs are in the pool and at what relative ratio. **Set a config to None to disable that species.**

### 3.11 `InstructionsConfig.asset` — text shown in menus

- **`controlsText`** — multiline rich-text body for the Controls panel.
- **`howToPlayText`** — multiline rich-text body for the How to Play panel.

You can use TMP rich-text tags (`<b>…</b>`, `<color=#ff0000>…</color>`, `<size=120%>…</size>`).

### 3.12 `DriftProfile.asset` & `2D Camera noise.asset`

`DriftProfile` controls how environment values drift on their own (random walk). The exact fields depend on the profile asset; tune the four per-variable groups for noise amplitude/frequency.

`2D Camera noise.asset` is a **Cinemachine NoiseSettings** asset — controls camera shake/breathing. Edit via Cinemachine docs if you want a different camera feel.

---

## 4. Quick-start balancing recipes

Use these as starting points. Always tweak one thing at a time.

### "The game is too hard / too easy"
- `DifficultyConfig.timeToMax` — raise to 1200 (20 min) for a gentle ramp; lower to 180 (3 min) for a brutal one.
- `HolobiontConfig.baseEnergyCapacity` — bigger pool = more forgiving.
- `HolobiontConfig.baseDrainPerCreaturePerSecond` — lower = creatures cost less to keep around.
- `SpawnerConfig.spawnInterval` and `maxAlive` — fewer spawns = less pressure.

### "Events feel monotonous"
- Raise the `weight` of underused events in `Assets/Configs/Events/`.
- Set `weight = 0` on overused events to retire them.
- Add `minDifficulty` to keep big events away from early game.

### "Bonded creatures die too fast / never die"
- `CreatureConfig.stressDeathThreshold` (per species) — raise toward 1.0 = harder to kill, lower = more fragile.
- `CreatureConfig.affinityFalloffCurve` — flatter curve = creatures tolerate mismatched environments better.

### "Breath feels unresponsive"
- `BreathConfig.frequencyApproachRate` and `depthApproachRate` — raise both for snappier response.
- `BreathFieldConfig.attractionStrength` — raise to feel more "powerful".

### "Want a calmer, more meditative session"
- Lower `DifficultyConfig.eventFrequencyMul` curve.
- Raise `BreathConfig.regenRate`.
- Lower `FlowFieldConfig.baseFlowSpeed` and `temporalScale`.

---

## 5. Scene-level tuning (visuals & feel that aren't in configs)

Open `Assets/Scenes/Game.unity`. Some fields live on **scene GameObjects** rather than configs — these are visual/animation tweaks tied to specific instances.

**Find a GameObject in the Hierarchy, look for these components:**

### `HolobiontView` (on the holobiont GameObject)
- **`stableColor` / `cascadeColor`** — core color when healthy vs failing.
- **`coreBreathAmplitude`** — how much the core scale pulses with breath.
- `decliningPulseRate` / `decliningPulseAmount` — pulse when energy is dropping.
- `cascadePulseRate` / `cascadePulseAmount` — frantic pulse during cascade death.
- `deadDarkenAmount` / `deadShrinkAmount` — visual collapse on death.

### `HolobiontTendrilNetwork`
The "spaghetti" connecting bonded creatures.
- **`rendererMode`** — `SpriteQuad` (preferred, custom shader) or `LineRenderer` (simpler).
- **`ribbonWidth`** — how thick the tendrils are.
- **`tendrilMaterial` / `tendrilSprite`** — swap to restyle (see §6).
- `widthAlongLength` *(curve)* — taper. A 1→0 curve gives pointy tips.
- **`waveAmplitude` / `waveSpatialFreq` / `waveTimeScale`** — how much they wobble.
- **`healthyColor` / `stressedColor`** — tint endpoints; the network blends between them per pair stress.
- `breathMin` / `breathMax` — alpha modulation by breath phase.
- **`kNeighbors`** — how many neighbors each creature connects to. 1 = sparse, 3+ = web.

### `HolobiontForceField`
- `creatureLayers` — which physics layers count as creatures. Keep at "Everything" unless you know what you're doing.
- `overlapBufferSize` — raise above 32 only if you expect dense crowds.

### `CreatureSpawner`
- **`spawnArea`** (BoxCollider2D) — drag this in the Scene view to resize where creatures appear.
- `spawnParent` — optional folder for spawned creatures (keeps the Hierarchy tidy).

### `MainMenuView` / `StartMenuView`
- **`toggleKey`** (default Escape) — what opens the pause menu.
- `survivalFormat` — text format for the run timer.
- `menuOrthoSize` / `gameOrthoSize` / `zoomDuration` — camera framing on start and during gameplay.

---

## 6. Adding custom graphics (no code)

Three workflows depending on what you want to change.

### 6.1 Reskin a creature (Nutrici / Scudo / Hub)

1. Drop your sprite (PNG with alpha) anywhere under `Assets/`. Recommended folder: `Assets/Textures/Creatures/`.
2. Click the imported sprite. In the Inspector:
   - **Texture Type**: Sprite (2D and UI)
   - **Pixels Per Unit**: match the project's existing sprites (look at one of the working sprites for reference).
   - **Filter Mode**: `Bilinear` for smooth, `Point (no filter)` for pixel art.
   - Click **Apply**.
3. Open the prefab to edit:
   - For **Nutrici**: open `Assets/Prefabs/Nutrice.prefab`.
   - For **Scudo**: `Assets/Prefabs/Scudo.prefab`.
   - For **Hub**: `Assets/Prefabs/Hub.prefab`.
4. Find the `SpriteRenderer` component and drag your new sprite into the **Sprite** slot.
5. (Optional) Adjust the **Color** tint, scale, and any particle children.
6. Save the prefab. Done — every creature spawned now uses your new look.

> If you want a brand-new creature variant (e.g. "GiantNutrici"): right-click `Nutrice.prefab` → Duplicate, rename, restyle. Then in `Assets/Configs/Creatures/`, right-click → Create → … → CreatureConfig (Nutrici), set its `prefab` field to the new prefab. Add the new config + a weight to `SpawnerConfig.asset`. **No code needed.**

### 6.2 Restyle the holobiont core

1. Open the Game scene.
2. Select the **Holobiont** GameObject in the Hierarchy.
3. On its `HolobiontView` component, drag your sprite into **`coreSprite`** (a SpriteRenderer reference) and **`fieldRingSprite`** if you also want a custom ring.
4. Tweak `stableColor` / `cascadeColor` to match.

### 6.3 Restyle tendrils, the breath ring, and other shaders

The tendrils and field ring use **materials** in `Assets/Materials/`:

- `Force Field Ring.mat` — the breath ring.
- (Tendril material — listed in `HolobiontTendrilNetwork.tendrilMaterial`.)

To recolor a material, click it, change the color/texture properties in the Inspector, save. Both materials use shaders in `Assets/Shaders/` — those *are* code, but you almost never need to edit them; their **exposed properties** show up in the material Inspector.

### 6.4 UI graphics

- UI sprites live under `Assets/UI/Generics/`.
- Replace by dragging a new image and re-pointing the relevant `Image` component on the UI GameObject (Hierarchy → find the Canvas).
- Fonts live in `Assets/Fonts/Font Assets/`. Use TextMesh Pro: select a TMP_Text component, change its **Font Asset**.

### 6.5 Particles & FX

The project has FlareEngine in `Assets/TwoBitMachines/FlareEngine/`. Most of that is sample content. Look for `ParticleSystem` components on scene objects and edit them in the Inspector — Unity's particle docs cover everything.

---

## 7. Adding a brand-new event (no code)

1. In Project, navigate to `Assets/Configs/Events/`.
2. Right-click → **Create** → Stefania → Environment Event Config (or duplicate an existing one and rename).
3. Set:
   - `eventName` (e.g. "Solar Flare").
   - `weight`, `minDifficulty`, `maxDifficulty`.
   - Add entries to `effects[]`. Each entry picks one or more environment variables and sets an `intensityDelta`. To make a "Toxic Heat", add two effects: Temperature +20, Toxicity +30.
   - `rampUpDuration` / `sustainDuration` / `rampDownDuration` — usually 3 / 5 / 4 is a good default.
   - `envelopeCurve` — leave at default unless you want a sharp spike.
4. Open `Assets/Configs/DefaultEventSchedule.asset`.
5. Drag your new event into the `eventPool[]` array.

The next session will roll it in.

---

## 8. Common pitfalls

- **"My change disappeared!"** You edited a *scene* GameObject during Play mode. Configs persist; scene objects don't.
- **"Inspector shows None for a config field."** The reference broke — drag the asset back in.
- **"Curve looks ignored."** Many curves expect input clamped to 0..1. If your curve has keys outside that range, behavior is undefined. Right-click in the curve editor → **Clamp** if needed.
- **"My new event never fires."** Check (a) it's in `DefaultEventSchedule.eventPool`, (b) `weight > 0`, (c) current difficulty is between `minDifficulty` and `maxDifficulty`.
- **"My new creature never spawns."** Check `SpawnerConfig`: the config slot is filled, the weight is > 0, and the creature's `prefab` field is set.
- **"Unity says 'Missing Reference' on a component."** Find the field, drag the right asset in. Don't ignore yellow/red console warnings during Play.

---

## 9. Where to look when you want to go further

- **`Assets/Scenes/Game.unity`** — main gameplay scene. Almost every Inspector field is documented above.
- **`Assets/Scripts/`** — only open when you want to *understand* what a config does, not to edit. Each `*Config.cs` has `[Tooltip]` attributes that show up as hover hints in the Inspector.
- **Unity Inspector tooltips** — hover over any field name. Most have a one-line description.
- **Console window (Window → General → Console)** — first place to check if something stops working after an edit.
- **Debug overlays** — many components draw helpful gizmos in the Scene view (flow field arrows, spawner bounds, breath field rings). Toggle the **Gizmos** button at the top of the Scene view if you don't see them.

---

## 10. Suggested first session

1. Open the project, open `Assets/Scenes/Game.unity`, press Play. Get a feel for the baseline.
2. Stop. Open `HolobiontConfig.asset`. Halve `baseDrainPerCreaturePerSecond`. Press Play. Note the difference.
3. Stop. Open `DifficultyConfig.asset`. Set `timeToMax` to 1200. Play for a few minutes. Notice the gentler ramp.
4. Stop. Open `Assets/Configs/Events/01_WarmBreeze.asset`. Change its `weight` to 10. Play. Warm breezes should now dominate early game.
5. Reskin one creature: replace the Nutrice sprite (§6.1).
6. Add one new event (§7). Watch it appear in a run.

After this, you will know enough to balance the rest by ear.
