# Guida per il Designer — Modificare e Bilanciare il Gioco

Questa guida è pensata per uno studente Unity di livello intermedio che vuole **regolare, ribilanciare, riadattare graficamente ed estendere** il gioco senza scrivere codice C#. Tutto ciò che è descritto qui si fa nell'Editor di Unity (Inspector, finestra Project, Scene view).

Si presume che tu conosca le basi di Unity: aprire un progetto, l'Inspector, le finestre Project / Hierarchy / Scene / Game, la modalità Play e i prefab. In caso contrario, guarda prima un qualsiasi video di 30 minuti del tipo "Unity per principianti assoluti".

---

## 1. Modello mentale — come è strutturato il gioco

Il gioco è diviso in tre livelli. Sapere dove vive ciascuna cosa ti risparmia tanti clic.

| Livello | Cos'è | Dove modificarlo |
| --- | --- | --- |
| **Config (dati)** | File `.asset` ScriptableObject che contengono numeri di bilanciamento, curve, colori. **Persistenti — sopravvivono alla modalità Play.** | `Assets/Configs/` |
| **Oggetti di scena (visuali e collegamenti)** | GameObject in `Assets/Scenes/Game.unity` con componenti View/Manager. I campi nell'Inspector qui sono anch'essi persistenti (fuori da Play). | Hierarchy → seleziona il GameObject |
| **Prefab (visuali delle creature)** | I prefab di Nutrice / Scudo / Hub che lo spawner istanzia. | `Assets/Prefabs/` |

**Tre sistemi principali orchestrano tutto:**

- **Olobionte** — la creatura controllata dal giocatore. Ha energia, capacità, campo del respiro, rete di tendrilli, campo di forza.
- **Ambiente** — quattro variabili simulate (temperatura, luce, umidità, tossicità) che oscillano nel tempo e reagiscono agli eventi.
- **Spawner** — fa apparire creature (Nutrici / Scudi / Hub) nel mondo nel tempo. Il giocatore le attrae o respinge col respiro.

Una creatura sta "bene" quando l'ambiente combacia con la sua **affinità**. Una creatura legata dà un beneficio all'olobionte (energia, difesa, capacità); una creatura legata sotto stress consuma energia e alla fine muore.

---

## 2. Basi del flusso di lavoro (leggile una volta, ti risparmi un'ora)

### 2.1 Modificare un asset di config

1. Nella finestra Project, vai in `Assets/Configs/`.
2. Clicca un qualsiasi `.asset` (es. `HolobiontConfig.asset`).
3. I suoi campi appaiono nell'Inspector. Modificali come faresti con qualsiasi componente.
4. **Premi Ctrl+S** (salva il progetto, non la scena). I config persistono solo se il progetto è salvato.

### 2.2 Trabocchetto della modalità Play

> **Tutto ciò che modifichi durante la modalità Play viene annullato quando premi Stop** — per gli oggetti di scena.
> **I config (file `.asset`) sono l'eccezione**: le modifiche fatte in Play mode rimangono.

Questo è il tuo superpotere principale. Per ribilanciare:

1. Premi **Play**.
2. Apri un config nell'Inspector e ritocca dal vivo — vedi i risultati subito.
3. La modifica è salvata, niente copia-incolla.

Abitudine più sicura: modifica i config in Play mode per *iterazione veloce*, poi verifica in una nuova sessione di Play che i nuovi valori funzionino davvero.

### 2.3 L'editor di AnimationCurve

Molti campi dei config sono **AnimationCurve** (icona con grafico ondulato). Cliccaci per aprire l'editor della curva.

- **Asse X** = un input (tempo normalizzato, distanza, difficoltà, fase del respiro). Il tooltip dell'Inspector dice quale.
- **Asse Y** = il moltiplicatore o valore in uscita.
- Click destro su un punto chiave per cambiare il tipo di tangente (Linear / Smooth / Constant).
- Tieni premuto Shift mentre trascini una tangente per spezzarla.

Non serve disegnare curve complesse. Una linea retta da (0,0) a (1,1) va bene per iniziare; piégala dopo, quando vuoi un feel non lineare.

### 2.4 Non rompere i riferimenti

Se cancelli un asset di config a cui si fa riferimento da qualche parte (un componente di scena, un altro config, un prefab), il riferimento diventa "Missing". Tasto destro su un asset → **Find References in Project** prima di cancellare qualsiasi cosa.

---

## 3. Inventario dei config — dove sta ogni numero

Tutti i config vivono in `Assets/Configs/`. Ecco cosa controlla ognuno, in linguaggio semplice. I campi in **grassetto** sono quelli che un designer di solito vuole toccare per primi; il resto è regolazione più profonda.

### 3.1 `HolobiontConfig.asset` — la creatura del giocatore

| Campo | Cosa fa |
| --- | --- |
| **`baseEnergyCapacity`** | Energia massima senza Hub legati. Più alta = più indulgente. |
| **`startingEnergy`** | Energia con cui inizi una partita. |
| **`baseDrainPerCreaturePerSecond`** | Quanta energia costa ogni creatura legata. Grande leva per la difficoltà. |
| `stressCostMultiplier` | Consumo extra quando le creature legate sono sotto stress. |
| `environmentMismatchCostMultiplier` | Consumo extra quando l'ambiente penalizza l'olobionte. |
| **`baseCarryingCapacity`** | Quante creature puoi legare prima di aver bisogno di Hub. |
| `energyCapacityPerHub` / `carryingCapacityPerHub` | Bonus degli Hub (attualmente 0 in Fase 1 — attivali se vuoi che gli Hub contino meccanicamente). |
| `cascadeTickInterval` | Velocità con cui l'olobionte rilascia creature in fase di fallimento. Più piccolo = spirale di morte più rapida. |
| **`depthToEnergyMultiplier`** *(curva)* | Mappa **profondità del respiro → tasso di afflusso energia**. Falla ripida nella parte alta per premiare i respiri profondi. |
| **`frequencyToMetabolicRate`** *(curva)* | Mappa **frequenza del respiro → moltiplicatore metabolico** (scala sia afflusso che consumo). |
| `baseOrbitRadius` / `breathPhaseToOrbitRadius` *(curva)* | Come le creature legate si dispongono e pulsano attorno a te. Soprattutto sensazione visiva. |
| `boundSpringStrength` | Quanto rigidamente le creature legate scattano nella loro orbita. Più alto = più rigido. |
| `breathField` | Riferimento al BreathFieldConfig (vedi sotto). |

### 3.2 `BreathConfig.asset` — la meccanica del respiro

Controlla come si comportano frequenza, profondità e resistenza del respiro. L'input del giocatore (microfono / tastiera) muove i valori target; questi campi controllano come il sistema risponde.

| Gruppo | Campi | Perché ci metterebbe mano |
| --- | --- | --- |
| Frequenza | `frequencyBaseline`, `frequencyMin/Max`, `frequencyApproachRate`, `frequencyDecayRate` | Per far sembrare il respiro più reattivo o più pigro. |
| Profondità | `depthBaseline`, `depthMin/Max`, `depthApproachRate`, `depthDecayRate` | Idem ma per profondità/ampiezza. |
| Resistenza | `staminaMax`, `baseDrainRate`, `pauseDrainRate`, `regenRate`, `regenRateDuringRecovery`, `baselineTolerance`, `recoveryDuration` | Sensazione di capacità polmonare. Consumo alto + recupero lento = punitivo. |
| Bonus pausa | `pauseBoostEnabled`, `pauseBoostMultiplier`, `pauseBoostDecayRate` | Premiare (o togliere) il bonus che ottieni trattenendo il respiro. |

### 3.3 `BreathFieldConfig.asset` — l'anello del respiro attorno all'olobionte

Controlla raggio, forza e colore del campo che attrae/respinge le creature.

- **`baseRadius` / `maxRadius`** — portata a profondità minima e massima del respiro.
- `radiusPerHub` — portata extra per ogni Hub legato.
- `breathPhaseToRadius` *(curva)* — contrazione visiva sull'inspirazione, espansione sull'espirazione.
- **`attractionStrength` / `repulsionStrength`** — quanto vigorosamente attiri / respingi le creature.
- `attractionFalloff` *(curva)* — come la forza scala con la distanza.
- **`idleColor` / `captureColor` / `shedColor`** — tinte dell'anello nei tre stati.
- `minRingAlpha` / `maxRingAlpha` — opacità dell'anello a piena inspirazione / espirazione.

### 3.4 Config delle creature (`Assets/Configs/Creatures/`)

Tre asset concreti, tutti derivati da `CreatureConfig`:

- **`NutriciConfig.asset`** — produce energia. `baseConversionRate` è l'unico campo specifico.
- **`ScudoConfig.asset`** — fornisce difesa. `baseResistanceContribution` è l'unico campo specifico.
- **`HubConfig.asset`** — espande la capacità. `energyCapacityBonus`, `carryingCapacityBonus`.

Tutti e tre condividono i **campi base della creatura** (vanno modificati su ogni asset singolarmente):

| Campo | Cosa fa |
| --- | --- |
| **`displayName`** | Etichetta nelle sovrapposizioni di debug. |
| **`prefab`** | Il prefab visuale che lo spawner istanzia (vedi §6 per il restyling). |
| **`baseColor`** | Tinta prima che affinità/stress la modulino. |
| **`affinityFalloffCurve`** *(curva)* | La leva di bilanciamento più importante. X = quanto l'ambiente è lontano dalla preferenza di questa creatura (0 = perfetta combinazione, 1 = totale incompatibilità). Y = efficienza 0..1. Curva ripida = la creatura prospera solo in condizioni ristrette. Curva piatta = creatura robusta. |
| `stressDeathThreshold` | Stress al quale una creatura legata si arrende e muore. Più basso = più fragile. |
| `affinityScatter` | Deviazione casuale per singola creatura rispetto alla preferenza media della specie. Più alto = più varietà tra individui. |
| `unboundLifetime` | Secondi in cui una creatura non legata vaga prima di sparire. 0 = per sempre. |
| `unboundLinearDamping` / `boundLinearDamping` | Smorzamento fisico. Quando legata è alto (elastica e stabile); quando libera è basso (deriva libera). |

### 3.5 `EnvironmentConfig.asset` — le quattro variabili del mondo

Quattro gruppi annidati (Temperatura / Luce / Umidità / Tossicità), ognuno con:

- **`baseValue`** — valore all'inizio della partita.
- `minValue` / `maxValue` — intervallo di clamp.
- `extremeLowNormalized` / `extremeHighNormalized` — soglie (in spazio normalizzato 0..1) per gli stati "estremo basso" e "estremo alto".

I default sono sensati (es. temperatura -50..+50, baseline 0). Toccali per ridefinire la "neutralità" del mondo.

### 3.6 `DifficultyConfig.asset` — come il gioco aumenta di difficoltà

- **`initialDifficulty`** — valore iniziale (0..1). 0 significa che parti il più facile possibile.
- **`timeToMax`** — secondi per arrivare a difficoltà 1.0. Grande = rampa lunga e dolce.
- **`progressEasing`** *(curva)* — modella la rampa. Una curva piatta che poi sale di colpo dà la sensazione di "intro tranquilla poi salita brusca".
- `eventIntensityMul` *(curva)* — moltiplica l'impatto degli eventi in base alla difficoltà.
- `eventFrequencyMul` *(curva)* — moltiplica la frequenza di apparizione degli eventi in base alla difficoltà.
- `driftAmplitudeMul` *(curva)* — moltiplica l'ampiezza della deriva ambientale in base alla difficoltà.

Queste tre curve sono i cursori "cosa significa più difficile?". Se vuoi che "più difficile" voglia dire eventi più frequenti ma non più intensi, alza `eventFrequencyMul` e tieni `eventIntensityMul` piatto.

### 3.7 `DefaultEventSchedule.asset` — quali eventi possono accadere e quando

Questo è il **pool di eventi**. Contiene:

- **`minInterval` / `maxInterval`** — quanto passa tra un evento e l'altro alla difficoltà di base.
- **`eventPool[]`** — una lista di riferimenti a `EnvironmentEventConfig`.

Gli eventi sono scelti per **selezione casuale pesata** da questo pool, filtrati dalla difficoltà attuale.

### 3.8 Config degli eventi (`Assets/Configs/Events/`)

Ci sono 25 eventi (`01_WarmBreeze` … `25_Cataclysm`). Ognuno è il suo asset e ha:

- **`eventName`** — etichetta visualizzata.
- **`weight`** — quanto spesso viene scelto (relativo agli altri eventi idonei). Imposta a 0 per disabilitarlo.
- **`minDifficulty` / `maxDifficulty`** — idoneo solo dentro questa fascia di difficoltà. Eventi tipo cataclisma dovrebbero avere `minDifficulty` alto (es. 0.8); eventi blandi dovrebbero avere `maxDifficulty` basso.
- **`effects[]`** — una o più coppie `(variabile coinvolta, intensityDelta)`. Delta negativo spinge la variabile in giù, positivo in su.
- **`rampUpDuration` / `sustainDuration` / `rampDownDuration`** — forma del ciclo di vita dell'evento.
- **`envelopeCurve`** *(curva)* — intensità complessiva nel tempo. Il default è una curva a campana.

Per **aggiungere un nuovo evento**:
1. Nella finestra Project, click destro in `Assets/Configs/Events/` → **Create → Stefania → Environment Event** (o qualunque sia l'etichetta del menu — Unity la prende da `[CreateAssetMenu]` nello script).
2. Riempi i campi qui sopra.
3. Apri `DefaultEventSchedule.asset` e trascina il nuovo evento in `eventPool[]`.

### 3.9 `FlowFieldConfig.asset` — come le creature non legate vagano

Il mondo ha un campo di flusso a rumore Perlin che muove le creature non legate.

- **`noiseScale` / `turbulenceNoiseScale`** — dimensione dei vortici. Piccolo = correnti enormi e fluide; grande = turbolenza rumorosa.
- **`temporalScale`** — quanto velocemente il campo evolve. 0 = statico, 1 = vorticoso.
- **`baseFlowSpeed`** — quanto è forte il vento.
- **`enableInwardBias` / `inwardBiasByDistance`** *(curva)* — attira le creature verso il centro, utile per evitare che vadano fuori schermo.
- I campi `gizmo*` riguardano solo la visualizzazione di debug nello Scene view (le frecce colorate). Non appaiono mai in gioco.

### 3.10 `SpawnerConfig.asset` — chi compare, e quanto spesso

- **`spawnInterval`** — secondi tra un tentativo di spawn e l'altro.
- **`maxAlive`** — limite morbido di creature **non legate** simultanee (quelle legate non contano).
- `upstreamSampleCount` — quanti punti del perimetro campionare per favorire spawn dal lato controvento. Lascia stare.
- `useSpawnInset` / `spawnInset` — tiene gli spawn lontani dal bordo.
- `useSpawnInwardKick` / `spawnInwardSpeed` — dà una piccola spinta verso il centro allo spawn.
- **`nutriciConfig` / `nutriciWeight`**, **`scudoConfig` / `scudoWeight`**, **`hubConfig` / `hubWeight`** — quali config di creatura sono nel pool e in che proporzione relativa. **Imposta un config a None per disabilitare quella specie.**

### 3.11 `InstructionsConfig.asset` — testo mostrato nei menu

- **`controlsText`** — corpo multilinea in rich-text per il pannello Comandi.
- **`howToPlayText`** — corpo multilinea in rich-text per il pannello Come Giocare.

Puoi usare i tag rich-text di TMP (`<b>…</b>`, `<color=#ff0000>…</color>`, `<size=120%>…</size>`).

### 3.12 `DriftProfile.asset` & `2D Camera noise.asset`

`DriftProfile` controlla come le variabili ambientali derivano da sole (passeggiata casuale). I campi esatti dipendono dall'asset del profilo; regola i quattro gruppi per-variabile per ampiezza/frequenza del rumore.

`2D Camera noise.asset` è un asset **NoiseSettings di Cinemachine** — controlla la vibrazione/respiro della camera. Modificalo seguendo la documentazione di Cinemachine se vuoi un feel di camera diverso.

---

## 4. Ricette rapide di bilanciamento

Usale come punti di partenza. Cambia sempre una cosa alla volta.

### "Il gioco è troppo difficile / troppo facile"
- `DifficultyConfig.timeToMax` — alzalo a 1200 (20 min) per una rampa dolce; abbassalo a 180 (3 min) per una brutale.
- `HolobiontConfig.baseEnergyCapacity` — pool più grande = più indulgente.
- `HolobiontConfig.baseDrainPerCreaturePerSecond` — più basso = le creature costano meno da tenere.
- `SpawnerConfig.spawnInterval` e `maxAlive` — meno spawn = meno pressione.

### "Gli eventi sembrano monotoni"
- Alza il `weight` degli eventi poco usati in `Assets/Configs/Events/`.
- Imposta `weight = 0` sugli eventi troppo presenti per ritirarli.
- Aggiungi un `minDifficulty` per tenere i grandi eventi lontani dall'inizio partita.

### "Le creature legate muoiono troppo in fretta / non muoiono mai"
- `CreatureConfig.stressDeathThreshold` (per specie) — alza verso 1.0 = più difficili da uccidere, abbassa = più fragili.
- `CreatureConfig.affinityFalloffCurve` — curva più piatta = le creature tollerano meglio ambienti non ideali.

### "Il respiro non è reattivo"
- `BreathConfig.frequencyApproachRate` e `depthApproachRate` — alza entrambi per risposta più scattante.
- `BreathFieldConfig.attractionStrength` — alza per avere più "potenza".

### "Voglio una sessione più calma e meditativa"
- Abbassa la curva `DifficultyConfig.eventFrequencyMul`.
- Alza `BreathConfig.regenRate`.
- Abbassa `FlowFieldConfig.baseFlowSpeed` e `temporalScale`.

---

## 5. Regolazioni a livello scena (visuali e feel che non stanno nei config)

Apri `Assets/Scenes/Game.unity`. Alcuni campi vivono su **GameObject di scena** invece che nei config — sono ritocchi visuali/animativi legati a istanze specifiche.

**Trova un GameObject nella Hierarchy, cerca questi componenti:**

### `HolobiontView` (sul GameObject dell'olobionte)
- **`stableColor` / `cascadeColor`** — colore del nucleo quando sta bene vs quando sta fallendo.
- **`coreBreathAmplitude`** — quanto la scala del nucleo pulsa col respiro.
- `decliningPulseRate` / `decliningPulseAmount` — pulsazione quando l'energia sta calando.
- `cascadePulseRate` / `cascadePulseAmount` — pulsazione frenetica durante la morte a cascata.
- `deadDarkenAmount` / `deadShrinkAmount` — collasso visivo alla morte.

### `HolobiontTendrilNetwork`
Lo "spaghetto" che collega le creature legate.
- **`rendererMode`** — `SpriteQuad` (preferito, shader custom) o `LineRenderer` (più semplice).
- **`ribbonWidth`** — quanto sono spessi i tendrilli.
- **`tendrilMaterial` / `tendrilSprite`** — sostituiscili per cambiare stile (vedi §6).
- `widthAlongLength` *(curva)* — rastrematura. Una curva da 1 a 0 dà punte affilate.
- **`waveAmplitude` / `waveSpatialFreq` / `waveTimeScale`** — quanto ondeggiano.
- **`healthyColor` / `stressedColor`** — colori agli estremi; la rete sfuma tra i due in base allo stress della coppia.
- `breathMin` / `breathMax` — modulazione dell'alpha in base alla fase del respiro.
- **`kNeighbors`** — quanti vicini collega ogni creatura. 1 = sparso, 3+ = ragnatela.

### `HolobiontForceField`
- `creatureLayers` — quali layer fisici contano come creature. Lascia su "Everything" a meno che tu non sappia cosa stai facendo.
- `overlapBufferSize` — alzalo sopra 32 solo se ti aspetti folle dense.

### `CreatureSpawner`
- **`spawnArea`** (BoxCollider2D) — trascinalo nello Scene view per ridimensionare l'area dove appaiono le creature.
- `spawnParent` — cartella opzionale per le creature spawnate (tiene la Hierarchy ordinata).

### `MainMenuView` / `StartMenuView`
- **`toggleKey`** (default Escape) — cosa apre il menu di pausa.
- `survivalFormat` — formato del testo del timer della partita.
- `menuOrthoSize` / `gameOrthoSize` / `zoomDuration` — inquadratura della camera all'avvio e in gioco.

---

## 6. Aggiungere grafica personalizzata (senza codice)

Tre flussi di lavoro a seconda di cosa vuoi cambiare.

### 6.1 Riskinare una creatura (Nutrice / Scudo / Hub)

1. Metti il tuo sprite (PNG con alpha) ovunque sotto `Assets/`. Cartella consigliata: `Assets/Textures/Creatures/`.
2. Clicca lo sprite importato. Nell'Inspector:
   - **Texture Type**: Sprite (2D and UI)
   - **Pixels Per Unit**: combacia con gli altri sprite del progetto (guarda uno degli sprite esistenti come riferimento).
   - **Filter Mode**: `Bilinear` per liscio, `Point (no filter)` per pixel art.
   - Clicca **Apply**.
3. Apri il prefab da modificare:
   - Per la **Nutrice**: apri `Assets/Prefabs/Nutrice.prefab`.
   - Per lo **Scudo**: `Assets/Prefabs/Scudo.prefab`.
   - Per l'**Hub**: `Assets/Prefabs/Hub.prefab`.
4. Trova il componente `SpriteRenderer` e trascina il tuo nuovo sprite nello slot **Sprite**.
5. (Opzionale) Regola la **Color** per la tinta, la scala, e qualsiasi figlio di particle system.
6. Salva il prefab. Fatto — ogni creatura spawnata ora ha il tuo nuovo look.

> Se vuoi una variante di creatura completamente nuova (es. "GiantNutrici"): tasto destro su `Nutrice.prefab` → Duplicate, rinomina, restile. Poi in `Assets/Configs/Creatures/`, tasto destro → Create → … → CreatureConfig (Nutrici), imposta il suo campo `prefab` al nuovo prefab. Aggiungi il nuovo config + un peso a `SpawnerConfig.asset`. **Nessun codice serve.**

### 6.2 Restile del nucleo dell'olobionte

1. Apri la scena Game.
2. Seleziona il GameObject **Holobiont** nella Hierarchy.
3. Sul componente `HolobiontView`, trascina il tuo sprite in **`coreSprite`** (un riferimento a SpriteRenderer) e **`fieldRingSprite`** se vuoi anche un anello custom.
4. Ritocca `stableColor` / `cascadeColor` per coordinare.

### 6.3 Restile di tendrilli, anello del respiro e altri shader

I tendrilli e l'anello del campo usano **materiali** in `Assets/Materials/`:

- `Force Field Ring.mat` — l'anello del respiro.
- (Materiale dei tendrilli — referenziato in `HolobiontTendrilNetwork.tendrilMaterial`.)

Per ricolorare un materiale, cliccaci, cambia colore/proprietà di texture nell'Inspector, salva. Entrambi i materiali usano shader in `Assets/Shaders/` — quelli *sono* codice, ma non hai quasi mai bisogno di toccarli; le loro **proprietà esposte** appaiono nell'Inspector del materiale.

### 6.4 Grafica UI

- Gli sprite UI vivono sotto `Assets/UI/Generics/`.
- Sostituisci trascinando una nuova immagine e ripuntando il relativo componente `Image` sul GameObject UI (Hierarchy → trova il Canvas).
- I font vivono in `Assets/Fonts/Font Assets/`. Si usa TextMesh Pro: seleziona un componente TMP_Text, cambia il suo **Font Asset**.

### 6.5 Particelle e FX

Il progetto contiene FlareEngine in `Assets/TwoBitMachines/FlareEngine/`. La maggior parte è contenuto di esempio. Cerca i componenti `ParticleSystem` sugli oggetti di scena e modificali nell'Inspector — la documentazione ufficiale Unity sulle particelle copre tutto.

---

## 7. Aggiungere un nuovo evento (senza codice)

1. In Project, vai in `Assets/Configs/Events/`.
2. Tasto destro → **Create** → Stefania → Environment Event Config (oppure duplica un evento esistente e rinominalo).
3. Imposta:
   - `eventName` (es. "Solar Flare").
   - `weight`, `minDifficulty`, `maxDifficulty`.
   - Aggiungi voci in `effects[]`. Ogni voce sceglie una o più variabili ambientali e imposta un `intensityDelta`. Per fare un "Toxic Heat", aggiungi due effetti: Temperatura +20, Tossicità +30.
   - `rampUpDuration` / `sustainDuration` / `rampDownDuration` — di solito 3 / 5 / 4 va bene come default.
   - `envelopeCurve` — lascia il default a meno che non tu voglia un picco brusco.
4. Apri `Assets/Configs/DefaultEventSchedule.asset`.
5. Trascina il nuovo evento nell'array `eventPool[]`.

La prossima sessione lo metterà in rotazione.

---

## 8. Trabocchetti comuni

- **"La mia modifica è sparita!"** Hai modificato un GameObject *di scena* in modalità Play. I config persistono; gli oggetti di scena no.
- **"L'Inspector mostra None su un campo di config."** Il riferimento si è rotto — trascina di nuovo l'asset.
- **"La curva sembra ignorata."** Molte curve si aspettano input nell'intervallo 0..1. Se la tua curva ha key fuori da quell'intervallo, il comportamento è indefinito. Tasto destro nell'editor della curva → **Clamp** se serve.
- **"Il mio nuovo evento non parte mai."** Verifica che (a) sia in `DefaultEventSchedule.eventPool`, (b) `weight > 0`, (c) la difficoltà attuale stia tra `minDifficulty` e `maxDifficulty`.
- **"La mia nuova creatura non spawna mai."** Verifica `SpawnerConfig`: lo slot del config è riempito, il peso è > 0, e il campo `prefab` della creatura è impostato.
- **"Unity dice 'Missing Reference' su un componente."** Trova il campo, trascinaci dentro l'asset giusto. Non ignorare i warning gialli/rossi nella Console durante il Play.

---

## 9. Dove guardare quando vuoi spingerti oltre

- **`Assets/Scenes/Game.unity`** — scena di gameplay principale. Quasi ogni campo dell'Inspector è documentato qui sopra.
- **`Assets/Scripts/`** — aprila solo per *capire* cosa fa un config, non per modificarlo. Ogni `*Config.cs` ha attributi `[Tooltip]` che appaiono come hint al passaggio del mouse nell'Inspector.
- **Tooltip dell'Inspector di Unity** — passa il mouse su un nome di campo. La maggior parte ha una descrizione su una riga.
- **Finestra Console (Window → General → Console)** — primo posto da controllare se qualcosa smette di funzionare dopo una modifica.
- **Sovrapposizioni di debug** — molti componenti disegnano gizmo utili nello Scene view (frecce del flow field, bordi dello spawner, anelli del campo del respiro). Attiva il pulsante **Gizmos** in alto nello Scene view se non li vedi.

---

## 10. Sessione iniziale consigliata

1. Apri il progetto, apri `Assets/Scenes/Game.unity`, premi Play. Fai pratica col baseline.
2. Stop. Apri `HolobiontConfig.asset`. Dimezza `baseDrainPerCreaturePerSecond`. Premi Play. Nota la differenza.
3. Stop. Apri `DifficultyConfig.asset`. Imposta `timeToMax` a 1200. Gioca per qualche minuto. Nota la rampa più gentile.
4. Stop. Apri `Assets/Configs/Events/01_WarmBreeze.asset`. Cambia il suo `weight` a 10. Gioca. Le brezze calde dovrebbero ora dominare l'inizio partita.
5. Riskina una creatura: sostituisci lo sprite di Nutrice (§6.1).
6. Aggiungi un nuovo evento (§7). Guardalo apparire in una run.

Dopo questo, saprai abbastanza per bilanciare il resto a orecchio.
