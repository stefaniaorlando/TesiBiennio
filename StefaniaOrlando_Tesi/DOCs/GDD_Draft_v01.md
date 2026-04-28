# Game Design Document — Draft v0.1

## Working Title: [TBD]

---

## 1. Vision and Conceptual Framework

### 1.1 The Problem

Eco-anxiety — the sense of uncertainty, unpredictability, and lack of control in relation to the climate crisis — is an increasingly documented psychological phenomenon, especially among younger generations (Pihkala, 2020). The American Psychological Association has identified that building resilience at both individual and collective levels is essential for confronting this historical moment (APA & ecoAmerica, 2017).

This project begins from a parallel between two experiences of powerlessness: living inside a body affected by a chronic condition (PCOS) — where the body feels unresponsive, unpredictable, almost foreign — and living inside a planetary ecosystem undergoing crisis. In both cases, the instinct is to seek control. The proposition of this game is that the answer is not control, but **symbiosis**.

### 1.2 The Core Metaphor

The core metaphor is **symbiosis**: distinct beings that remain distinct but become interdependent — unable to survive alone, capable of surviving together. The holobiont is not a merged entity where individuals dissolve. It is a network where each creature keeps its identity, its vulnerabilities, its specific contribution, but none of them are viable in isolation.

The player's breath is the living thread — biological, rhythmic, involuntary-yet-shapeable — through which a human body and a collective of non-human organisms become entangled enough to sustain each other. The boundary between self and other is not erased but *thinned* — made porous enough for mutual dependence to flow through. In the student's language: **"assottigliare il confine."**

### 1.3 Bidirectional Symbiosis

The relationship between player and holobiont is reciprocal:

- **Player → Holobiont**: the player's breath provides metabolic energy — the fuel that keeps the network alive and expanding.
- **Holobiont → Player**: the network's survival demands reshape the player's breathing patterns. To keep the holobiont alive under shifting conditions, the player must learn to modulate their breath — slower, deeper, held, rhythmic. The holobiont's needs train the player's respiration. It becomes a biofeedback loop disguised as a game.

This bidirectionality is physiological, not merely narrative. Both organisms — digital and biological — are changed by the connection.

### 1.4 Theoretical Foundations

The project draws on:

- **Lynn Margulis** — symbiogenesis and the holobiont as a unit of evolution. Separate organisms that, through sustained mutual exchange, form a collective entity.
- **Donna Haraway** — the Chthulucene and the concept of *ongoingness*: the possibility of going forward by weaving multispecies entanglements, rejecting both techno-optimism and defeatism.
- **Karen Barad** — agential realism and the concept of *intra-action*: there are no pre-existing individuals that then interact; entities emerge through their entanglement.
- **Solarpunk** — as an aesthetic and ideological frame: a future built on mutual aid, ecological awareness, and shared resources, in opposition to both dystopian fatalism and naive techno-utopianism.

### 1.5 Narrative Frame

The game does not over-narrate. There is no lore, no cutscenes, no text-heavy worldbuilding. The world is hostile because worlds are hostile. The holobiont exists because life assembles under pressure. The player's breath is the connection because breath is what living things share.

The experience should feel like looking into a living system — abstract, microscopic, indifferent to the player's expectations — where survival is only possible through composition, cooperation, and rhythmic attention.

---

## 2. Systems Overview

### 2.1 Three Nested Scales

The game operates on three nested scales:

1. **Environment** — uncontrollable, hostile, variable. The medium within which everything exists. It shifts, spikes, oscillates. It cannot be defeated, fled from, or negotiated with.
2. **Holobiont** — the collective network. Emergent, manageable (but not controllable), composed of the player and all bonded creatures. Its properties are the aggregate of its members.
3. **Individual creatures + player** — local, mortal, specialized. Each has an identity and environmental affinity. Each contributes to and draws from the collective.

### 2.2 The Holobiont Is Sessile

The holobiont does not move. The world moves around it — organisms drift through the environment carried by currents, environmental conditions shift and oscillate. The player cannot flee or chase. They can only adapt to what comes.

This is thematically essential (survival through adaptation, not domination) and scope-appropriate (no pathfinding, no navigation systems required).

---

## 3. Environment

### 3.1 Environmental Variables

The environment is defined by a set of continuous variables that shift over time:

- **Temperature** (cold ↔ hot)
- **Humidity** (dry ↔ wet)
- **Toxicity** (clean ↔ toxic)
- **Light** (dark ↔ bright)

These four variables create a multidimensional condition space. The environment's state at any moment is a point in this space, and it drifts continuously with occasional spikes.

### 3.2 Environmental Dynamics

The environment changes through two overlapping patterns:

- **Background drift**: slow oscillation with rhythm — like seasons or tides. The player can learn to anticipate these shifts over time.
- **Crisis events**: sudden, extreme spikes in one or more variables. Unpredictable. These demand rapid recomposition of the holobiont or acceptance of losses.

### 3.3 Environmental Visualization

The environment is not represented through UI readouts or color-coded indicators. It is the **medium** — the substance the organisms exist within. The player reads environmental state through layered visual systems, each tied to a different variable:

**Temperature → Particle behavior.** Cold: slow-drifting crystalline particles, small, sharp-edged, tight Brownian motion. Hot: fast, expansive, turbulent particles with longer trails, convection-like currents. Particle speed, trail length, and movement pattern interpolate continuously along the temperature axis.

**Humidity → Medium density.** High humidity: the space feels thick. Particles drag, cluster, leave condensation-like trails. Organisms move slower. A subtle fog layer is present. Low humidity: the space feels sparse, brittle. Particles scatter and evaporate quickly. Sharp empty gaps between things.

**Toxicity → Geometric disruption.** Noise displacement on organisms and particles. Subtle at low levels, increasingly aggressive. Organisms in toxic zones flicker, their edges destabilize. Organic interference patterns appear in the medium — not glitch-aesthetic, but like looking through contaminated water.

**Light → Visibility and perception.** Low light: organisms beyond a certain radius fade, the player's awareness shrinks. Foto-nutrici dim and weaken. High light: everything is legible, photo-dependent organisms thrive.

**Key principle**: each variable owns a different visual channel. They layer and combine without competing. A hot-humid-toxic moment looks and feels categorically different from a cold-dry-clean one — not because of a color code but because every visual system is behaving differently simultaneously.

### 3.4 The Flow Field

A vector field — implemented as a Perlin noise velocity field — gently pushes everything: particles, unbound organisms, ambient debris. It makes the medium feel like a substance with currents rather than empty space.

The flow field's behavior shifts with conditions: in high humidity the flow is slow, heavy, laminar. In heat it becomes turbulent, chaotic. In cold it nearly stops. Unbound organisms drift on these currents. Bound organisms resist them.

### 3.5 Suspended Matter

Ambient debris — tiny, semi-transparent, slightly out of focus. Not organisms, not VFX — just material suspended in the medium. Density scales with humidity. Turbulence scales with temperature. Toxicity makes it clump or behave erratically. This is the primary depth cue: it tells the player they are looking *into* something, not *at* a surface.

### 3.6 Technical Implementation

- **Post-processing effects**: per-variable global mood (mapped to URP post-processing stack)
- **Particle systems**: for temperature visualization and suspended matter
- **Flow field**: Perlin noise velocity field applied to rigidbodies and particles
- **Shader effects**: screen-space displacement for toxicity, visibility masking for light
- **Sound design**: a low ambient texture that shifts in pitch and grain with conditions, providing anticipatory feedback

---

## 4. Creatures

### 4.1 Design Philosophy

There are no subtypes or subspecies. There are three functional categories, and within each category, every individual creature is parametrically unique — its specific environmental affinity is determined at spawn, shaped by the conditions it emerged from. Creatures are not born into fixed species. They are shaped by their environment.

### 4.2 Creature Types

**Nutrici — Energy Producers**

Nutrici metabolize the player's breath into usable energy for the holobiont. Their conversion efficiency depends on how well their individual environmental affinity matches current conditions. A nutrici that spawned in bright, dry conditions operates at peak efficiency in bright, dry environments — and poorly in dark, wet ones. The same breath input yields very different energy output depending on which nutrici are present and what the environment is doing.

**Scudo — Environmental Resistance**

Scudo provide the holobiont with resistance to environmental stress. Each scudo's affinity determines what conditions it protects against. A cold-affinity scudo contributes cold resistance; a toxicity-affinity scudo filters contamination. The holobiont's overall resistance profile is the aggregate of all its scudo members — not a single number but a *shape* across the environmental variable space.

**Hub — Metabolic Infrastructure**

Hubs are capacitors. They do not produce energy or resist environmental conditions. They increase the holobiont's **capacity to store and efficiently use energy**, and they expand the **maximum number of creatures** the network can sustain.

Without hubs, the holobiont has a small energy buffer and a low creature cap. Breath must be constant or the system crashes quickly. With hubs, the energy ceiling rises — the holobiont can coast through brief pauses, survive the player's refractory periods after holding breath, weather momentary environmental spikes by drawing on reserves.

A large holobiont with many nutrici and scudo but no hubs has no metabolic inertia — the moment breath input dips below drain, it collapses instantly. A large holobiont with hubs can absorb fluctuation. Growth without hubs is reckless. Growth with hubs is sustainable.

Hubs make growth survivable. The carrying capacity isn't artificial — it's thermodynamic.

### 4.3 Creature State (Individual Level)

Individual creatures carry minimal state:

- **Type**: nutrici, scudo, or hub
- **Environmental affinity**: a point in the environmental variable space, set at spawn, representing the conditions in which this creature thrives
- **Stress level**: a continuous value derived from the mismatch between the creature's affinity and current environmental conditions. High stress means the creature is an active metabolic burden on the network.

Creatures do not have individual energy pools. Energy is shared globally at the holobiont level. A creature is either functioning within the network or dying because its stress has become unsustainable.

### 4.4 Visual Identity

Each creature type has a distinct visual family (shape language or color family). Within each type, environmental affinity is expressed through tint, pulsation rhythm, or subtle morphological variation. The player should be able to read a creature's type at a glance and intuit its affinity through observation.

---

## 5. The Holobiont

### 5.1 Holobiont State (Collective Level)

The holobiont's state is the emergent aggregate of its members:

- **Energy**: a single global pool. Inflow from breath (metabolized by nutrici). Outflow from base metabolic cost (proportional to creature count) plus environmental stress cost (proportional to how poorly the collective composition matches current conditions). Hub creatures increase the maximum energy capacity.
- **Resistance profile**: the aggregate of all scudo affinities. Determines how much environmental stress costs the network. A well-composed holobiont in matching conditions drains slowly. A poorly composed one hemorrhages energy.
- **Carrying capacity**: determined by the number of hub creatures. Sets the maximum number of creatures the network can sustain.

### 5.2 The Fundamental Tension

More creatures means more capability and resistance, but also higher base metabolic drain. Growth is not free. The holobiont has an optimal size for any given environmental state, and that optimum shifts constantly as conditions change.

The player's strategic problem is never simply "get more energy." It is **composition management**: the right creatures in the right conditions are energy-positive; the wrong creatures in the wrong conditions bleed the network dry. Breathing is necessary but not sufficient. You can breathe perfectly and still die if your holobiont is poorly composed for current conditions.

### 5.3 Energy Depletion and Cascade Failure

When the holobiont's energy reaches zero, death is not instant. It is **erosion**.

The holobiont begins shedding creatures — the most energy-expensive first (those under greatest environmental stress), then progressively. Creatures detach and drift away into the medium. The holobiont shrinks. If the player recovers their breathing, the smaller holobiont may stabilize — less drain, less need. Some shed creatures may still be drifting nearby and can be re-attracted before they drift away on the current.

If energy remains at zero, creatures continue to detach until only the player's core organism remains. If that core runs out of energy — game over.

The body doesn't die all at once. It loses capacity piece by piece.

---

## 6. Breath Mechanics

### 6.1 Breath Input Parameters

The breath sensor captures four parameters:

- **Depth** (amplitude): how deep each breath is
- **Frequency**: how fast the player breathes
- **Pause/Hold**: deliberate cessation of breath
- **Phase**: whether the player is currently inhaling or exhaling

A **stamina** system governs how long the player can sustain strenuous breathing combinations (deep + fast, prolonged holds). When stamina depletes, the system enters a **refractory/recovery period** during which breathing input is reduced or locked. This is the natural governor that prevents the player from brute-forcing the system and rewards sustainable, rhythmic breathing.

### 6.2 Breath-to-System Mapping

Each breath parameter owns a distinct system function:

**Depth → Energy magnitude.** Deeper breaths produce more raw energy per cycle. This is the volume knob — simple, intuitive, physiologically direct.

**Frequency → Metabolic rate.** Fast breathing accelerates everything: energy production, energy consumption, creature activity, connection speed. It is a tempo control. The holobiont becomes more reactive but burns hotter. Slow breathing makes the system economical but sluggish.

**Phase (inhale/exhale) → Spatial behavior.** This is the holobiont's pulse.

- *Exhale*: the holobiont **expands**. An attractive force field radiates outward. Nearby drifting organisms are pulled toward the holobiont. Deeper exhale means stronger pull, wider radius. Bonded creatures push outward within the cluster. This is how the player reaches for new organisms.
- *Inhale*: the holobiont **contracts**. The attractive field collapses or becomes mildly repulsive. Unbound organisms that haven't bonded are pushed away or released. Bonded creatures are pulled tighter, consolidated. The holobiont becomes denser, more compact.

The player is constantly pulsing between openness and closure. To reach for something new, you must temporarily loosen what you have.

**Pause/Hold → Discrete special actions.** Holding breath is deliberate and costly (drains stamina rapidly).

- *Hold during exhale (expanded state)*: **capture**. The attractive field freezes. Any organism currently within bonding range locks in and joins the network. This is the commitment moment. The player has pulled something close; now they hold to seal the connection.
- *Hold during inhale (contracted state)*: **release/shed**. The weakest or most stressed creature is ejected from the network. This is voluntary recomposition — letting go of an organism that has become a liability in current conditions.

### 6.3 Force Field Dynamics

The breath-driven force field defines the spatial relationship between the holobiont and the unbound organisms drifting in the medium:

- **Exhale phase**: attractive radial force, strength and radius proportional to breath depth
- **Inhale phase**: force field collapses; mild repulsion on unbound organisms
- **Hold-exhale**: field freezes at current radius; bonding occurs
- **Hold-inhale**: internal contraction intensifies; shedding occurs

Breathing frequency determines the **oscillation rhythm** of this field. Fast breathing creates rapid attract-repel cycles — nearby organisms experience turbulence, jittering in and out of range. This is useful for sorting: poorly compatible creatures get shaken loose, compatible ones gradually settle into bonding range. Slow breathing creates long, sustained pulls and consolidations — deliberate, controlled recruitment.

### 6.4 Bonded Organism Behavior

Creatures bonded to the holobiont are not static. They orbit gently around the holobiont's center, jostling and rearranging. Exhale pushes them outward within the cluster. Inhale pulls them in. The whole holobiont visually breathes with the player.

Implementation: simple spring forces toward holobiont center, modulated by breath phase. Low computational cost, high expressive value.

---

## 7. Game Loop

### 7.1 Core Loop

1. **Environment shifts** (continuous background drift + occasional crisis events)
2. **Organisms drift** through the medium on the flow field
3. **Player breathes** — producing energy, pulsing the holobiont, attracting or repelling nearby organisms
4. **Nutrici metabolize** breath into energy (efficiency based on affinity–environment match)
5. **Energy drains** from metabolic cost (creature count) and environmental stress (composition–environment mismatch)
6. **Player decides**: recruit new creatures (exhale + hold), shed costly ones (inhale + hold), or sustain current composition
7. **Holobiont adapts** — its resistance profile, energy balance, and carrying capacity shift with every creature gained or lost
8. Loop returns to step 1.

### 7.2 Win/Loss Conditions

There is no traditional "win." The game is about **persistence** — how long the holobiont survives, how complex it becomes, how gracefully it adapts.

**Loss**: the holobiont's energy depletes completely and cascade failure reduces the network to nothing. The player's core organism is the last to go.

**Possible progression arc**: over time, if the holobiont reaches sufficient stability and diversity, it may **reproduce** — releasing a seed or fragment that drifts away to begin elsewhere. This is not "winning" but continuation — ongoingness. The game continues with the remaining holobiont or with the new fragment. [This mechanic is optional and scope-dependent.]

---

## 8. Technical Foundation

### 8.1 Platform and Engine

- **Engine**: Unity (URP)
- **Platform**: Desktop (primary), scope permitting mobile
- **Input**: Breath sensor (hardware), providing real-time depth, frequency, pause, and phase data

### 8.2 Core Systems Required

- Breath input processing and parameter extraction
- Creature spawning, drifting, and AI (simple drift on flow field + response to holobiont force field)
- Holobiont state management (global energy, resistance profile, carrying capacity)
- Environmental variable oscillation and event system
- Force field system (breath-driven attraction/repulsion)
- Spring-based bonded creature positioning

### 8.3 Visual Systems Required

- URP post-processing per environmental variable
- Particle systems (temperature visualization, suspended matter)
- Flow field (Perlin noise velocity applied to rigidbodies and particles)
- Screen-space distortion shader (toxicity)
- Visibility/fog system (light variable)
- Creature visual differentiation (type + affinity expression)

### 8.4 Breath Sensor Integration

[To be defined based on hardware specifications. Requires: real-time amplitude stream, frequency detection, phase detection, pause/hold detection. Stamina and recovery are game-side systems computed from breath input patterns.]

---

## 9. Open Questions

- **Working title**: to be decided
- **Starting creature**: does the player begin bonded to a random creature, or alone?
- **Reproduction mechanic**: if included, what triggers it and what does the player experience?
- **Sound design**: how does the ambient soundscape respond to environmental variables and holobiont state?
- **Session length**: is this an endless experience, or are there natural session boundaries?
- **Difficulty curve**: how do environmental oscillation amplitude and crisis frequency scale over time?
- **Creature visual design**: abstract (circles, blobs) or more biologically suggestive? What level of visual differentiation is achievable in scope?
- **Data visualization**: should the player have access to any holobiont state data (energy level, resistance profile) through UI, or is all feedback embedded in the visual/spatial system?

---

*Document version 0.1 — First draft for supervisor review*
*Project supervisor: Petar*
*Student: [Name]*
