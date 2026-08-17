# Code Map — the developer's technical architecture overview

> **Who this is for.** You, six months from now, opening this repository and needing to find
> your way around without re-reading 35,000 lines.
>
> **What it documents.** The repository *as it actually is*. Not an ideal architecture, not a
> plan. If this document and the code disagree, the code is right and this document is a bug.
>
> **Companion documents.** `docs/game-overview.md` is the same territory from the player's side;
> `docs/crafting-overview.md` takes the crafting stack in one piece. `DECISIONS.md` says *why*.
> This says *where*.
>
> Last synced with the repo: **2026-08-16** — build clean (0 warnings), 765 tests passing.

**Jump to:** [Layout](#1-project-layout) · [Entry points](#2-runtime-entry-points) ·
[Layers](#3-architectural-layers) · [Data architecture](#4-the-data-driven-architecture) ·
[Services](#5-the-services-and-who-owns-them) · [Events & effects](#6-the-eventeffect-architecture) ·
[Shared abstractions](#7-the-shared-abstractions) · [How systems talk](#8-how-systems-communicate) ·
[Subsystems](#10-the-subsystems) · [**Where do I change X?**](#11-where-do-i-change-x)

---

# 1. Project layout

```
InTheDungeonsWeDie.slnx          the solution

core/     InTheDungeonsWeDie.Core.csproj
          net8.0 · RootNamespace "Dungeons" · nullable enabled · NO Godot reference
          ALL authoritative gameplay logic lives here.

game/     InTheDungeonsWeDie.csproj
          Godot.NET.Sdk 4.7.1 · namespace Dungeons.Game.* · references Core
          project.godot lives here. Client only: UI, input, file access, presentation.

tests/    InTheDungeonsWeDie.Core.Tests.csproj
          xUnit · references Core ONLY (never Godot, so tests run headless)

docs/     design documents
game/data/<type>/*.json          ALL content
```

**The assembly split is the enforcement mechanism.** Core cannot reference Godot because the
project file does not let it. That is deliberately stronger than a folder convention: the domain
*cannot* accidentally depend on the engine, and tests *cannot* accidentally drag in GodotSharp.
(`DECISIONS.md` D1.)

### `core/` folder → namespace map

| Folder | Namespace | Owns |
|---|---|---|
| `core/Actions/` | `Dungeons.Actions` | The shared Action vocabulary (timing, costs) |
| `core/Affixes/` | `Dungeons.Affixes` | Item modifiers: definitions, rolling, grants |
| `core/Characters/` | `Dungeons.Characters` | Attributes, resources, gauges, character rules |
| `core/Characters/Composition/` | `Dungeons.Characters.Composition` | The class combinator |
| `core/Combat/` | `Dungeons.Combat` | Encounter, hit pipeline, moves, statuses, enemies |
| `core/Content/` | `Dungeons.Content` | Definition loading, the bundle, validation |
| `core/Crafting/` | `Dungeons.Crafting` | Reaction engine, traits, essence, fabrication, genome |
| `core/Equipment/` | **`Dungeons.Items`** ⚠ | Equipment container, definitions, the combat seam |
| `core/Events/` | `Dungeons.Events` | The game event bus |
| `core/Inventory/` | **`Dungeons.Items`** ⚠ | The inventory container |
| `core/Items/` | `Dungeons.Items` | Item instances, property sets, stacks |
| `core/Modifiers/` | `Dungeons.Modifiers` | The modifier key vocabulary, scopes, resolution |
| `core/Persistence/` | `Dungeons.Persistence` | Save DTOs, serializer, mapper |
| `core/Presentation/` | `Dungeons.Presentation` | The semantic read-model (D30) |
| `core/Professions/` | `Dungeons.Professions` | Profession definitions, progress, execution |
| `core/Randomness/` | `Dungeons.Randomness` | The seeded RNG abstraction |
| `core/Realms/` | `Dungeons.Realms` | Realm graph, run state, extraction |
| `core/Rules/` | `Dungeons.Rules` | Trigger rules, conditions, effects, proc safety |
| `core/Simulation/` | `Dungeons.Simulation` | The tick engine |

> ⚠ **Namespace ≠ type name (D9).** The `Inventory` and `Equipment` **classes** deliberately live
> in namespace `Dungeons.Items`, not `Dungeons.Inventory` / `Dungeons.Equipment`. A class named
> identically to its namespace makes `new Inventory()` ambiguous for callers. This bit twice.
> Do not "fix" it.

---

# 2. Runtime entry points

There are exactly three, and they are all small.

### 2.1 `game/GameRoot.cs` — the composition root (Godot autoload)

Registered as the autoload `GameRoot` in `project.godot`. `_Ready()` is where the entire game is
constructed, once, in a fixed order:

```
_Ready()
 ├─ ContentLoader.LoadAll("res://data")        → one ContentBundle
 ├─ ContentValidator.Validate(bundle)          → throws on any problem (fail loudly)
 ├─ build the character services               (RuleRegistry, CharacterComposer, BuildResolver)
 ├─ construct the TriggerRuleEngine over the event bus
 ├─ RebuildCharacter()  +  EquipStarterLoadout()
 ├─ build professions   (ProfessionSystem, PassiveProfessionRunner) on the shared TickEngine
 ├─ build crafting      (MaterialProfileResolver, EmergentRegistry, ReactionEngine,
 │                       FabricationEngine, PropertyGlossary)
 ├─ build combat        (StatusController, CombatantModifiers, HitPipeline, CombatEncounter)
 ├─ RegisterCombatHandlers(encounter, rng)     ← effect kinds stop landing in Unhandled
 └─ wire the condition world + subscribe to encounter events
```

`_Process(delta)` is the **only** thing that drives simulation time: it accumulates real seconds
into ticks at `TicksPerSecond = 20` and calls `TickEngine.Advance(1)` per whole tick, but only
while `_running` is true.

Everything else in `GameRoot` is one of three things — and nothing else belongs there:

1. **Commands** the UI calls (`Craft`, `FabricateItem`, `EquipFromStash`, `RealmTravel`…).
2. **Queries** the UI reads (`MaterialsOnHand`, `PlayerMoveset`, `RealmReport`…).
3. **C# events** the UI subscribes to (`LogEmitted`, `CharacterChanged`, `InventoryChanged`,
   `RunningChanged`, `DiscoveryChanged`, `CombatChanged`, `RealmChanged`).

> **Known debt, recorded in D2.** `GameRoot` is ~1,650 lines and is both the composition root and
> the application layer. Extracting an Application/use-case layer is deferred, not forgotten.
> The mitigation that keeps it survivable: **every gameplay rule is a thin forward into Core.**
> `Craft` builds a `CraftRequest`, calls `ReactionEngine`, and formats the outcome — that is all.
> Keep it that way; if you find yourself writing a `if` about game rules in `GameRoot`, it belongs
> in Core.

### 2.2 `game/ui/MainMvpUI.cs` + `.tscn` — the one screen

The main scene. Builds every control **in code** with a code-only dark theme (no assets):
a persistent header, a `TabContainer` (Character · Char Lab · Equipment · Professions · Crafting ·
Realm · Combat · Inventory), and an always-visible event-log panel.

Its shape is uniform and worth knowing:

- `BuildXSection(root)` — constructs the controls for a tab, once.
- `RebuildX()` / `RefreshX()` — re-renders a dynamic group in response to a `GameRoot` event.
- It calls `GameRoot` methods and reads `GameRoot` strings. **It contains no gameplay logic.**
  Colour and layout are the only decisions it makes.

> The name `MainMvpUI` is historical — the MVP shipped long ago. Renaming it means renaming the
> C# file, the `.uid`, and the script reference inside `MainMvpUI.tscn`, which cannot be verified
> without running the Godot editor. Recorded as a deferred rename, not an oversight.

### 2.3 `tests/` — the headless entry point

xUnit over Core only. Content-validation tests load the **real** `game/data` JSON through
`TestPaths.DataDir`, so shipped content is verified by the same rules the game uses at startup.
`tests/Integration/FullLoopTests.cs` runs the whole loop headless.

---

# 3. Architectural layers

```
┌──────────────────────────────────────────────────────────────────────┐
│  PRESENTATION (Godot)         game/ui/MainMvpUI.cs                   │
│  Controls, colour, layout, input. No rules.                          │
└───────────────────────────┬──────────────────────────────────────────┘
                            │ commands ↓ / queries ↑ / events ↑
┌───────────────────────────┴──────────────────────────────────────────┐
│  APPLICATION + COMPOSITION    game/GameRoot.cs                       │
│  Constructs services, owns run/combat/equipment state, forwards.     │
└───────────────────────────┬──────────────────────────────────────────┘
                            │
┌───────────────────────────┴──────────────────────────────────────────┐
│  INFRASTRUCTURE (Godot)       game/Infrastructure/                   │
│  ContentLoader (res://), SaveStore (user://). File access only.      │
└───────────────────────────┬──────────────────────────────────────────┘
                            │ raw JSON text ↓
┌───────────────────────────┴──────────────────────────────────────────┐
│  DOMAIN (core/)                                                       │
│                                                                       │
│   Content ── DataStore<T>, ContentBundle, ContentValidator            │
│      │                                                                │
│      ├── Simulation ── TickEngine ──────────┐                         │
│      │                                       │ drives                 │
│      ├── Professions ── ProfessionSystem ────┤                         │
│      ├── Combat ─────── CombatEncounter ─────┘                         │
│      ├── Crafting ───── ReactionEngine, FabricationEngine             │
│      ├── Characters ─── CharacterComposer, BuildResolver              │
│      ├── Realms ─────── RealmRun, RealmExtraction                     │
│      ├── Items ──────── Inventory, Equipment, ItemInstance            │
│      ├── Affixes ────── AffixRoller, AffixGrants                      │
│      ├── Presentation ─ the semantic read-model (one-way)             │
│      └── Persistence ── SaveMapper, SaveData                          │
│                                                                       │
│   Cross-cutting spine: Events (bus) · Rules (triggers) ·              │
│                        Modifiers (vocabulary) · Randomness (seeded)   │
└───────────────────────────────────────────────────────────────────────┘
```

**Four rules that define the layering. They are hard invariants.**

1. **Core never references Godot.** Enforced by the project file.
2. **Godot is the client.** UI, input, scenes, file access, presentation. Never authoritative
   rules.
3. **`GameRoot` wires; it does not decide.** Composition + glue + formatting only.
4. **Definitions are never mutated.** A definition describes a *kind*; runtime state is separate.

---

# 4. The data-driven architecture

### 4.1 The dividing line (D16)

> **Code owns *structure and closed vocabularies*. Data owns *content instances*.**

| Concretely | Lives as |
|---|---|
| Definition **shapes** | C# records/classes in Core |
| Fixed **vocabularies** — `DamageType`, `EquipmentSlot`, `ItemType`, `PropertyRole`, `ProfessionCategory`, `RealmLocationType`, `StatusCategory`, tag families, move ops | C# enums or code-owned registries |
| **Content instances** — materials, moves, statuses, modifiers, actors, professions, realms, classes | JSON under `game/data/` |
| **Open sets** — item ids, property *names*, form/class/part tag values | data, never enums |

### 4.2 The load path

```
game/data/<type>/*.json
        │
        │  ContentLoader.ReadJsonFiles(dir)          (Godot: DirAccess/FileAccess, recursive)
        ▼
    raw JSON text
        │
        │  DataStore<T>.LoadDocuments(texts)          (Core: path-agnostic, never sees a path)
        │     · auto-detects single object vs array per file
        │     · case-insensitive property names, enum-as-string, comments + trailing commas OK
        │     · duplicate id → DuplicateDefinitionException (fails loudly)
        ▼
    DataStore<T>   ──►   ContentBundle   ──►   ContentValidator.Validate(bundle)
                                                       │
                                       problems? ──────┴──► ContentValidationException
```

**`ContentBundle` is the single registration point.** Adding a content type is:
1. a `DataStore<T>` property on `ContentBundle`,
2. one line in `ContentLoader.LoadAll` (convention: folder name == content type),
3. validation rules in `ContentValidator` if it has cross-references,
4. a failing-content test per rule.

Nothing else — no positional argument threaded through five call sites.

### 4.3 Property names have exactly one source of truth (D17)

`game/data/properties/properties.json` is authoritative for what a valid property name is.
`ContentValidator` derives its known-property set from that loaded registry — **not** from a code
list. `ItemProperties` survives only as convenience constants for direct code references, and a
bijection test fails if the two ever drift.

### 4.4 Id convention (D19)

`type.slug` — `material.oak_bark`, `equip.iron_sword`, `move.heavy_strike`, `status.burn`,
`affix.emberbrand`, `profession.mining`, `action.mine_iron`, `actor.goblin_raider`,
`technique.*`, `trait.*`, `essence.*`, `form.*`, `realm.*`, `class.*`/`prefix.*`/`suffix.*`/
`species.*`. Realm-location ids (`loc.*`) are realm-scoped, not globally unique. **Property ids
are bare** (`hardness`) — they are keys, not entities.

Generated ids follow the same shape: `emergent.7f3a91c4` (materials), `equip.emergent.<hash>`
(fabricated equipment).

---

# 5. The services, and who owns them

Everything below is constructed once in `GameRoot._Ready()` and lives for the session.

| Service | Type | What it owns |
|---|---|---|
| `TickEngine` | `Dungeons.Simulation` | The one clock. Shared by combat + passive gathering |
| `GameEventBus` | `Dungeons.Events` | The one bus. Synchronous, ordered |
| `TriggerRuleEngine` | `Dungeons.Rules` | Attached rules, effect dispatch, proc safety |
| `CharacterComposer` | `Dungeons.Characters.Composition` | Build ids → `CharacterBlueprint` |
| `BuildResolver` | `Dungeons.Characters.Composition` | Build ids → growth, gauges, hooks, name |
| `GaugeController` | `Dungeons.Characters` | The build's live gauges (reconfigured per rebuild) |
| `StatusController` | `Dungeons.Combat` | Status lifetimes + Resolve gating |
| `CombatantModifiers` | `Dungeons.Combat` | **The modifier read path** — the only authoritative one |
| `HitPipeline` | `Dungeons.Combat` | Damage resolution, stage by stage |
| `CombatEncounter` | `Dungeons.Combat` | The tick-driven fight |
| `ProfessionSystem` | `Dungeons.Professions` | The single execute path (active + passive) |
| `PassiveProfessionRunner` | `Dungeons.Professions` | Repeating passive action on the tick engine |
| `ReactionEngine` | `Dungeons.Crafting` | Every craft, through one pipeline |
| `FabricationEngine` | `Dungeons.Crafting` | Materials → equipment instances |
| `EmergentRegistry` | `Dungeons.Crafting` | Signature → registered runtime material |
| `MaterialProfileResolver` | `Dungeons.Content` | `MaterialDefinition` → `MaterialProfile` |
| `PropertyGlossary` | `Dungeons.Presentation` | Property → glyph + gloss (data-driven) |
| `Inventory` (×2) | `Dungeons.Items` | The Stash, and the per-run inventory |
| `Equipment` | `Dungeons.Items` | Slot → worn `ItemInstance` |
| `SeededRandom` (×4) | `Dungeons.Randomness` | Rules, professions, crafting, combat, affixes |

**Where loot goes** is one property, and it is worth knowing by heart:

```csharp
private Inventory ActiveInventory => _run is { Active: true } ? _run.RunInventory : _stash;
```

Everything that produces an item deposits into `ActiveInventory`. That single expression *is* the
extraction risk model: in a Realm you fill the unsecured bag; in the Hideout you fill the Stash.

---

# 6. The event/effect architecture

This is the spine. The class combinator, statuses, item modifiers and enemy behaviour are all
built on it, and none of them has bespoke machinery.

### 6.1 The four pieces

```
  GameEvent          "what happened"     kind + source + target + amount + tags + values
      │                                  + ChainId + Depth + CanTrigger
      ▼
  GameEventBus       synchronous, ordered. Events raised inside a handler QUEUE and drain
                     afterwards — never re-enter. (Determinism: the sim must replay from a seed.)
      ▼
  TriggerRuleEngine  matches attached TriggerRules:  event + conditions + chance + cooldown
      ▼
  IEffectHandler     the system that owns the behaviour registers for an effect kind.
                     No handler? → recorded in Unhandled. Visibly inert, never silently missing.
```

### 6.2 A `TriggerRule` is the universal hook shape

```jsonc
{
  "event": "HitLanded",
  "when":  [ { "kind": "hasTag", "text": "heavy" } ],   // 17 condition kinds
  "chance": 0.25,
  "cooldown_ticks": 40,
  "effects": [ { "kind": "applyStatus", "text": "status.burn", "amount": 8 } ]   // 16 effect kinds
}
```

The same shape is used by: Prefix mechanics · Suffix expressions · gauge feeds · status hooks ·
item modifier `rule` grants · move riders. **Read `rule.Payload`, never `rule.Effect`** — the
former unifies the legacy single-effect and the `effects[]` forms.

### 6.3 Proc safety (`core/Rules/EffectContext.cs`)

The recursion model, because a system this composable will otherwise eat itself:

| Guard | Rule |
|---|---|
| Chain identity | Each root effect starts a chain with a **sequential** id (not a GUID — the sim replays from a seed) |
| Depth budget | 2 (Anomalous modifiers may reach 3) |
| Once per chain | On by default |
| Per-target ICD | Internal cooldown per rule per target |
| Fuse | 64 effects per chain, hard stop |
| `CanTrigger: false` | Ailment ticks and retaliation set this — they can never match a rule at all |

> ⚠ **The single easiest bug to introduce in this codebase.** An `IEffectHandler` **must**
> propagate `invocation.Context` onto any event it raises. Forget it once and the chain restarts
> at depth 0, making the entire budget decorative.

### 6.4 The modifier vocabulary (`core/Modifiers/`)

51 data-defined keys (`game/data/modifier_keys/`) replace a closed enum as the *target* of a
modification.

- **Five kinds:** additive · multiplicative · flag · **diminishing** (`1 − Π(1−x)`) ·
  **highest_only**.
- **Clamps live on the key**, so "the minimum action interval" is data, not a scattered guard.
- **Every contribution carries provenance** — "why is this number what it is?" is answerable.
- **Scoped contributions:** a contribution may carry one `ModifierScope` over eight closed
  dimensions (`lane aspect essence profession move_tag form item status`). A `ModifierContext`
  supplies a *set* per dimension (one swing is `melee` **and** `attack` **and** `light`), so
  matching is membership, not equality.
- **A key declaring `scoped_by` throws when resolved without that dimension** (D-12). Deliberate:
  the alternative is a plausible wrong number. **There is no overload that defaults the context.
  Do not add one.**

`CombatantModifiers` is the **only authoritative read path**. Per query it assembles:

```
build statics  +  worn items' modifier grants  +  status `while_active`
               +  gauge bands  +  timed `grantModifier` grants
                                  ▼
                            one ModifierSet
```

Uncached on purpose — a stale cache mid-proc-chain costs more than the assembly.
`StatusController.ModifierTotal` survives as a status-only subtotal **for display only**;
nothing authoritative may read it.

---

# 7. The shared abstractions

The handful of types that appear everywhere. Learn these and most of the codebase reads itself.

| Abstraction | Where | What it is |
|---|---|---|
| `IDefinition` | `core/Content/` | Anything with a stable `Id`. The `DataStore<T>` constraint |
| `DataStore<T>` | `core/Content/` | Id-keyed registry parsed from JSON *text*. Never touches files |
| `ContentBundle` | `core/Content/` | Every store, in one carrier |
| `PropertySet` | `core/Items/` | Immutable, case-insensitive `name → value`. Zero == absent |
| `ItemStack` | `core/Items/` | The one item+quantity shape. `ItemChance` = stack + drop chance |
| `ItemInstance` | `core/Items/` | A specific owned item. **Equipment only** (D20) |
| `MaterialProfile` | `core/Content/` | A material's full crafting state: properties, potency, integrity, traits, essence, lineage, signature |
| `TickEngine` | `core/Simulation/` | Integer ticks, deterministic ordering, cancellable schedules |
| `IRandomSource` | `core/Randomness/` | Injected, seeded. **No global RNG anywhere** |
| `GameEvent` / `IGameEventBus` | `core/Events/` | The 31-event vocabulary |
| `TriggerRule` / `ConditionSpec` / `EffectSpec` | `core/Rules/` | The universal hook |
| `EffectContext` | `core/Rules/` | Chain id + depth + proc rules |
| `ModifierContribution` / `ModifierSet` / `ModifierContext` | `core/Modifiers/` | The modification vocabulary |
| `ActionTiming` / `ActionCost` | `core/Actions/` | Telegraph/windup/recovery, and costs that may name a **gauge**. Shared by Moves and (later) Profession actions — components, deliberately **not** an `abstract class Action` |
| `ResolvedMove` | `core/Combat/` | A move after grants + modifiers, with full provenance |
| `Genome` | `core/Crafting/` | An item's genetic profile; the input to every modifier decision |

---

# 8. How systems communicate

Five mechanisms, and only five.

| # | Mechanism | Used for | Example |
|---|---|---|---|
| 1 | **Direct construction + injection** | Everything wired at startup | `new ReactionEngine(content, () => ActiveInventory, …)` |
| 2 | **`Func<T>` providers** | Late-bound state that changes | `() => ActiveInventory`, `id => professionLevel(id)` |
| 3 | **The game event bus** | Gameplay facts anyone may react to | Combat publishes `HitLanded`; a Prefix rule hooks it |
| 4 | **C# events** | System → application → UI notification | `Inventory.Changed`, `CombatEncounter.StateChanged` |
| 5 | **The tick engine** | Anything that happens *later* | telegraph → windup → execute; passive action intervals |

**What is deliberately absent:** no service locator, no DI container, no static mutable state, no
async in the domain, no message queue. Ordering is a determinism requirement, so everything is
synchronous.

**The one indirection worth internalising:** systems that produce items never know where the
items go. They are handed `Func<Inventory>` and deposit into whatever it returns.

---

# 9. The seeded-determinism contract

The simulation must reproduce from a seed. That constrains several things you might otherwise do
casually:

- All randomness comes from an injected `IRandomSource`. There are exactly five seeded sources,
  all created in `GameRoot._Ready()`: rules, professions, crafting, combat, affixes.
- Chain ids are **sequential**, never GUIDs.
- The event bus is **synchronous and ordered**; handler-raised events queue and drain after.
- The tick engine resolves due actions in **schedule order**, with a snapshot taken before any
  callback runs, so a callback cannot disturb its own tick's resolution set.
- Statuses and gauges ride **one periodic sweep**, not independent timers.
- Crafting uses randomness in exactly **two** places (variance perturbation, and quality-driven
  spread), which is why `Project()` can run the identical pipeline with variance off and show the
  player the truth.

---

# 10. The subsystems

Each card answers the same eight questions.

---

## 10.1 Simulation (the tick engine)

**PURPOSE** — The single deterministic clock. All authoritative timing is integer ticks; Godot
converts ticks to seconds for display only.

**IMPORTANT FILES** — `core/Simulation/TickEngine.cs`, `ScheduledAction.cs`

**DATA** — None. Pure code. `GameRoot.TicksPerSecond = 20`.

**RUNTIME FLOW**
```
GameRoot._Process(delta) → accumulate delta × 20 → TickEngine.Advance(1) per whole tick
TickEngine.Advance → CurrentTick++ → resolve due actions (snapshot first, sorted by schedule
                     sequence) → raise TickAdvanced
```

**DEPENDENCIES** — none.

**OUTPUT** — `Schedule(delay, callback) → ScheduledAction` (cancellable by id); `TickAdvanced`.

**EXTENSION POINTS** — Anything that must happen later schedules onto this one engine. Do not
introduce a second clock or a real-time timer.

**ENTRY POINT** — `TickEngine.Advance` / `ResolveDueActions`. 111 lines; read all of it once.

---

## 10.2 Content loading and validation

**PURPOSE** — Turn JSON on disk into validated, id-keyed definition stores, and **fail loudly at
startup** rather than producing a mid-play `KeyNotFoundException`.

**IMPORTANT FILES**
- `core/Content/DataStore.cs` — the registry, path-agnostic
- `core/Content/ContentBundle.cs` — every store, one carrier
- `core/Content/ContentValidator.cs` — ~1,480 lines of cross-reference rules
- `core/Content/ContentProblem.cs`, `ContentValidationException.cs`
- `game/Infrastructure/ContentLoader.cs` — the Godot file bridge

**DATA** — everything under `game/data/`.

**RUNTIME FLOW** — see §4.2. `GameRoot._Ready` → `LoadAll` → `Validate` → throw on problems.

**DEPENDENCIES** — Godot (`ContentLoader` only). Everything else is engine-independent.

**OUTPUT** — A populated `ContentBundle`, or a hard failure listing every problem.

**EXTENSION POINTS** — Adding a content type: bundle property → `LoadAll` line → validator rules
→ failing-content test. Adding a *rule*: a `ValidateX` method called from `Validate`.

**ENTRY POINT** — `ContentValidator.Validate` (line ~63) is a table of contents for the whole
validation surface; each `ValidateX` below it is self-contained.

---

## 10.3 Materials and properties

**PURPOSE** — The ingredient set the entire crafting engine operates on.

**IMPORTANT FILES**
- `core/Content/MaterialDefinition.cs` — flat `Dictionary<string,double>` on a 0–100 scale,
  `family:value` tags, optional essence
- `core/Content/PropertyDefinition.cs` — the property registry: role, opposites, `resisted_by`,
  `grants_tags`, **`glyph` + `gloss`** (display metadata as data, never code switches)
- `core/Content/MaterialProfile.cs` — the *runtime* view: properties + potency + integrity +
  traits + essence + lineage + signature
- `core/Content/MaterialProfileResolver.cs` — definition → profile (authored materials get
  derived defaults; emergent ones carry their stored profile)
- `core/Content/TagFamilies.cs` — the closed `family:value` namespace and its cardinality rules
- `core/Content/ResistanceCalculator.cs` — derives resistance from `resisted_by`
- `core/Items/ItemProperties.cs` — code-reference constants (the JSON registry is authoritative)

**DATA** — `game/data/materials/*.json` (~474 across 9 category files),
`game/data/properties/properties.json` (21).

**RUNTIME FLOW** — `MaterialDefinition` → `MaterialProfileResolver.Resolve` → `MaterialProfile` →
consumed by `ReactionEngine`, `FabricationEngine` and the presentation readings.

**DEPENDENCIES** — Content only.

**OUTPUT** — `MaterialProfile`.

**EXTENSION POINTS** — A new property is **one entry in `properties.json`**: name, role, glyph,
gloss, opposite, thresholds. Code changes only if something must *read it by name*.

**ENTRY POINT** — `PropertyDefinition` first (it explains the vocabulary), then
`MaterialProfileResolver`.

---

## 10.4 Emergent crafting — the reaction engine

**PURPOSE** — Resolve **every** craft through one universal pipeline. No recipes, no
per-combination rules, ever.

**IMPORTANT FILES**
| File | Owns |
|---|---|
| `core/Crafting/ReactionEngine.cs` | The pipeline and the only public entry (`Project` / `Resolve`) |
| `ReactionAlgebra.cs` | Per-reagent: converge → drift → oppose → prune |
| `ReactionCoefficients.cs` | Acceptance/release; the medium → property map |
| `ReactionStepResult.cs` | What moved and **why** (typed `PropertyChangeKind`) |
| `PotencyCalculator.cs` | Weighted mean + `best input + 8 × skill` ceiling |
| `IntegrityCalculator.cs` | Cost, effective instability, variance, `IntegrityProjection` |
| `CraftQuality.cs` | Skill + instability + performance → a quality factor |
| `MaterialSignature.cs` | Quantize → SHA-256 → `emergent.7f3a91c4` |
| `VariancePerturbation.cs` | The seeded scatter (one of crafting's two RNG draws) |
| `EmergentRegistry.cs` / `IEmergentRegistry.cs` | Signature → registered runtime material |
| `NameGenerator.cs` | Pure function of final state; ≤3 words; deterministic coinage on collision |
| `TagDeriver.cs` | Process assertion → state thresholds → lineage carry |
| `ByproductResolver.cs` | Destruction → Slag / Cinders / Dross / Residue |
| `TraitResolver.cs` / `TraitDefinition.cs` | Birth → supersede → cap 3 |
| `EssenceAlgebra.cs` / `EssenceDefinition.cs` | Additive transfer, opposition → strain, capacity |
| `ReactionLog.cs` / `ReactionLogBuilder.cs` | The structured "why" trace |
| `*Tuning.cs` | Every magic number, named and in one place |

**DATA** — `processes/` (8) · `byproducts/` (4) · `traits/` (16) · `essences/` (7) ·
`name_grammar/` (44 words).

**RUNTIME FLOW**
```
CraftRequest(process, substrate, [reagents…], catalyst?)
   │
   ├─ AcceptRequest   profession level, substrate tags, inputs on hand
   │                  → CraftFailure, or an AcceptedCraft with every input resolved
   │
   ├─ RunReaction(accepted, applyVariance):
   ├─ per reagent, in order:
   │     effective instability (integrity + essence strain)
   │       → craft quality → variance magnitude
   │       → ReactionAlgebra.ApplyReagent  (converge, drift, oppose, prune)
   │       → EssenceAlgebra.Apply          (transfer, opposition → strain)
   │       → integrity cost                (Δstate × severity, minus skill)
   │       → integrity ≤ 0 ⇒ DESTROYED, stop
   │
   ├─ variance perturbation (Resolve only; Project runs with it off)
   ├─ ApplyTraitPass       birth → supersede → cap; births charge integrity and CAN destroy
   ├─ tag derivation       from the POST-trait state
   ├─ potency              weighted mean over substrate/reagents/catalyst
   ├─ lineage merge        roots by weight, renormalised, trace pruned
   └─ signature            quantize + hash
         │
   Project ─┴─► CraftProjection   (integrity projection, potency, name, typed steps, log)
   Resolve  ──► consume inputs → register archetype → deposit → CraftOutcome (+ byproducts)
```

The two entry points differ **only** in the `applyVariance` flag and what they do with the
result. Same gate, same pipeline, same numbers — which is what makes the pre-commit projection
incapable of lying.

**DEPENDENCIES** — Content, Items (`Inventory`), Randomness, Professions (level provider).

**OUTPUT** — A registered `MaterialDefinition` (or byproducts), a `ReactionLog`, and a
`CraftProjection` that is *the same computation* with variance off.

**EXTENSION POINTS** — A new **process** is one JSON entry (channel, medium, severity, role
weights, gates, tag effects). A new **trait** is one JSON entry. A new **essence** likewise.
Changing *behaviour* means the algebra or a `*Tuning` constant — and every tuning number is
already isolated in a `*Tuning` class for exactly that reason.

**ENTRY POINT** — `ReactionEngine.Resolve` → `RunReaction`, then follow the reagent loop. `ReactionAlgebra` is
the mathematics; `ReactionEngine` is the orchestration.

---

## 10.5 Fabrication — materials become equipment

**PURPOSE** — The terminal boundary. Consume materials in named slots and mint an
`ItemInstance` over a **derived** `EquipmentDefinition`.

**IMPORTANT FILES** — `core/Crafting/FabricationEngine.cs`,
`core/Crafting/FormTemplateDefinition.cs` (`FormSlot`, `StatContribution`, `FabricationTuning`)

**DATA** — `game/data/forms/forms.json` (3: Longsword, Buckler, Vest).

**RUNTIME FLOW**
```
FabricationRequest(formId, { slotName → materialId })
   │
   └─ Compose()   ← ONE side-effect-free computation, used by BOTH Project and Fabricate
        ├─ resolve + tag-gate every slot; check inputs on hand
        ├─ stats:    stat_map contributions (per-slot, or FormSlots.AllSlots mass-weighted)
        │            × contribution.Weight
        │            ÷ 100 × FabricationTuning.CombatUnitScale     ← the 0–100 ↔ combat scale,
        │                                                            HERE and nowhere else
        ├─ traits:   magnitude × the slot's aperture for that trait's category
        │            → top N expressed, the rest DORMANT
        ├─ essence:  mass-share weighted, arcane-amplified
        ├─ armour:   response properties → lane resistances (armour forms only)
        ├─ name:     dominant trait adjective + primary material root + form noun
        ├─ signature: form + component ids + stats → SHA-256 → equip.emergent.<hash>
        └─ genome:   GenomeCalculator.Pressure(form, components) + essence + traits + tags
                     + potency (mass-weighted mean) + generation depth
   │
   Project  ──► FabricationProjection  (+ deterministic innates — the preview may promise these)
   Fabricate ─► consume · register the derived EquipmentDefinition (if new)
                · roll prefixes + suffixes · mint ItemInstance · deposit
```

**DEPENDENCIES** — Content, Crafting (profiles, traits, genome), Items, Affixes, Randomness.

**OUTPUT** — `ItemInstance` with `Genome` + `Affixes`; a registered `EquipmentDefinition`
persisted in the save.

**EXTENSION POINTS** — A new **form** is one JSON entry: slots (required tags, mass share,
aperture), `stat_map`, `trait_cap`, granted moves, tags. No code.

**ENTRY POINT** — `FabricationEngine.Compose`. Everything interesting happens there; `Project`
and `Fabricate` are thin wrappers around it, which is what makes the preview incapable of
drifting from the truth.

---

## 10.6 The Genome and item modifiers (affixes)

**PURPOSE** — Decide what an item *can* roll, how likely, how strong, and where in the range —
all as pure functions of the genome.

**IMPORTANT FILES**
- `core/Crafting/Genome.cs` — the record + `GenomeCalculator.Pressure`
- `core/Affixes/AffixDefinition.cs` — eligibility / weight / tiers / grants / description
- `core/Affixes/AffixRoller.cs` — `AffixTuning`, `AffixRoller`, `AffixGrants`

**DATA** — `game/data/affixes/affixes.json` (43).

**RUNTIME FLOW**
```
Genome ─┬─ IsEligible(affix)        hard gate: form/tags, pressure minimums, essence, family
        ├─ WeightOf(affix)          base + Σ (pressure or essence)/10 × per10
        ├─ TierFor(affix)           best (lowest-numbered) tier the genome qualifies for
        └─ PotencyPosition(potency) where in the tier's [lo,hi] the value lands

Innates   deterministic: eligible → weight rank → top ≤3 above the floor → zero variance
Rolled    weighted count → per pick: build pool → weighted choice → tier → value ± variance
                                     (one affix per family per item)
```

Once rolled, `AffixGrants` turns each grant into live game state:

| Grant type | Becomes | Attached where |
|---|---|---|
| `stat` | `ModifierContribution` (scoped, with provenance) | `GameRoot.EquippedAffixContributions()` → `CombatantModifiers.buildModifiers` |
| `rule` | `TriggerRule` | `GameRoot.AttachBuildRules()` → `TriggerRuleEngine.Attach` |
| `moveModifier` | `MoveModifierGrant` | `GameRoot.ResolvePlayerMoveset()` → `MovesetBuilder` |

Equipping or unequipping re-runs `AttachBuildRules`, so an item's rules stop firing the moment it
comes off — exactly like a retired Prefix's would.

**`$roll` is substituted in exactly one place** (`AffixGrants`), so the tooltip and the mechanics
can never drift. A validator rule enforces the parity.

**DEPENDENCIES** — Crafting (genome), Modifiers, Rules, Randomness.

**OUTPUT** — `RolledAffix` (id + tier + value) — this is what persists.

**EXTENSION POINTS** — A new modifier is one JSON entry. It ships only when its mechanic already
resolves in play (D30).

**ENTRY POINT** — `AffixRoller.Roll` for the casino; `AffixGrants` for how a roll becomes real.

---

## 10.7 Items, inventory, equipment

**PURPOSE** — Own what the player has and what they are wearing, and hand combat a **neutral**
shape rather than an equipment type.

**IMPORTANT FILES** — `core/Items/ItemInstance.cs`, `PropertySet.cs`, `ItemStack.cs`,
`InstanceIdSource.cs`, `ItemFormat.cs` · `core/Inventory/Inventory.cs` ·
`core/Equipment/Equipment.cs`, `EquipmentDefinition.cs`, `EquipmentResolver.cs`

**DATA** — `game/data/equipment/*.json` (4 authored; fabricated ones are generated + persisted).

**RUNTIME FLOW**
```
Inventory  =  stacks (id → quantity)  +  instances (unique ItemInstances)
Equipment  =  slot → ItemInstance

EquipmentResolver.ResolveWeaponMoves(def, instance, moveStore)
    → the weapon's MoveDefinitions with instance MASS applied once, split by packet share
EquipmentResolver.ResolveArmor(def, instance)
    → ArmorProfile { Armor (+ hardness), Resistances keyed by LANE }
```

**This resolver is the material → combat seam (D8).** Combat consumes `ResolvedMove` and
`ArmorProfile` and never sees an equipment type. Today the seam maps only Mass → damage/speed and
Hardness → armour; fabrication now lands richer stats through it without combat changing.

**DEPENDENCIES** — Content, Combat (move definitions).

**OUTPUT** — moves + `ArmorProfile`; inventory change events.

**EXTENSION POINTS** — Richer property → combat mappings go in `EquipmentResolver`, behind the
same seam. New slots go on the `EquipmentSlot` enum plus the `Equipment` container.

**ENTRY POINT** — `EquipmentResolver`. It is 105 lines and it is the whole seam.

---

## 10.8 Character identity — the class combinator

**PURPOSE** — Compose 18,750 playable builds from 4 authored component types, with none of the
combinations hand-written.

**IMPORTANT FILES**
- `CharacterBuild.cs` + `ComponentIds.cs` — four **typed** ids (positional *and* persisted, so a
  swap would be silent save corruption); they serialize as bare strings
- `CharacterComponentDefinition.cs` — Species / Prefix / Suffix / BaseClass definitions
- `BaseIdentity.cs` — `ExpressionChannel`, `GaugeDefinition`, `GaugeBand`, `AttributeGrowth`
  (the fixed 4.0/level budget rule)
- `CharacterComposer.cs` → `CharacterBlueprint` → runtime `Character`
- `BuildResolver.cs` → `ResolvedBuild` (growth, gauges, attached hooks with provenance,
  modifiers, generated name) + `BuildResolver.Diff` for the Character Lab
- `ClassNameFormatter.cs` — nine grammars. **Presentation only, verified by test.**
- `core/Characters/GaugeController.cs` — `GaugePool` + `GaugeController`

**DATA** — `classes/` (15) · `prefixes/` (25) · `suffixes/` (50) · `species/` (3) ·
`name_formats/` (9).

**RUNTIME FLOW**
```
CharacterBuild (4 ids)
   ├─ CharacterComposer.Compose → CharacterBlueprint → new Character()
   └─ BuildResolver.Resolve     → ResolvedBuild
                                    ├─ growth per level (budget-checked)
                                    ├─ gauges          → GaugeController.Reconfigure
                                    ├─ attached rules  → TriggerRuleEngine.Attach (with source)
                                    ├─ modifiers       → CombatantModifiers.buildModifiers
                                    └─ generated name  → ClassNameFormatter
```

`GameRoot.RebuildCharacter()` runs this and then `AttachBuildRules()`, which **detaches
everything first** — otherwise a swapped-out Prefix keeps firing.

**DEPENDENCIES** — Content, Rules, Modifiers, Characters.

**OUTPUT** — `Character` (attributes, resources, blueprint) + `ResolvedBuild`.

**EXTENSION POINTS** — New Base / Prefix / Suffix = one JSON entry. The validator enforces the
growth budget and "a Prefix may never name a Base".

**ENTRY POINT** — `BuildResolver.Resolve`. It is the whole combinator in ~55 lines.

---

## 10.9 Moves and movesets

**PURPOSE** — One data shape for everything a combatant does, both sides of the fight.

**IMPORTANT FILES**
- `core/Combat/MoveDefinition.cs` — kind (dispatch only) + namespaced tags + timing + costs +
  `Requires` + targeting + packets + `StaggerPower` + effect riders
- `core/Combat/MoveTags.cs` — the closed tag vocabulary
- `core/Combat/MoveModifier.cs` — `MoveMatch` + the closed 11-op vocabulary + `MoveOps`
- `core/Combat/Moveset.cs` — `MovesetBuilder` → `ResolvedMove`, and `Apply` (the op interpreter)
- `core/Combat/LearnedMoves.cs`, `TechniqueDefinition.cs`

**DATA** — `moves/` (27) · `move_modifiers/` (1) · `techniques/` (19).

**RUNTIME FLOW**
```
grants: weapon FIRST → species → base → prefix → suffix → learned    (each with provenance)
modifiers: character components + worn equipment + worn affixes
        │
        └─ MovesetBuilder.Build → for each move: apply matching ops in MoveOps.ApplicationOrder
                                  (fixed order, proved independent of source order)
                                → ResolvedMove (cached, with provenance and conflicts reported)

At execution: ResolvedMove.Snapshot() re-enters the op interpreter, which is how a runtime
              `modifyMove` stacks on top of a cached resolution.
```

**DEPENDENCIES** — Content, Actions, Rules (`Requires`, riders), Items (weapon grants).

**OUTPUT** — `IReadOnlyList<ResolvedMove>`.

**EXTENSION POINTS** — A new move is one JSON entry. A new move *modifier* is one JSON entry over
the existing 11 ops. **`addTag` is the composition lever** — prefer it to a new op.

**ENTRY POINT** — `MovesetBuilder.Apply` (the op interpreter) and `MoveOps.ApplicationOrder`
(the ordering rule).

---

## 10.10 Combat — the encounter

**PURPOSE** — Run the fight on the shared clock: both sides queue, telegraph, wind up, execute
and recover; publish everything that happens.

**IMPORTANT FILES** — `core/Combat/CombatEncounter.cs` (the orchestrator, plus `ActionPhase`,
`ActionInFlight`, `DefensiveStance`), `Combatant.cs`, `CombatTuning.cs`,
`CombatConditionWorld.cs`, `EffectTargetResolver.cs`, `CombatEffectHandlers.cs`

**RUNTIME FLOW**
```
Start(player, enemies) → reset gauges/statuses/cooldowns → schedule stamina regen + the status
                         sweep → each enemy BeginEnemyDecision

PLAYER  UseMove(id) → CanUse (readiness, cooldown, Requires, costs) → pay + start cooldown
                    → set ReadyTick → Commit(ActionInFlight)
ENEMY   BeginEnemyDecision → ChooseMove (weighted AI rules, avoid-repeat) → pay → Commit

Commit → [telegraph ticks] → EnterWindup (× the windup modifier — this is what Chill IS)
       → [windup ticks]    → Execute
                               ├─ WithTimedModifiers (execution-time `modifyMove`)
                               ├─ HitPipeline.Resolve  → HitResult + HitLog
                               ├─ ApplyResult          → publish, barrier, narrate
                               ├─ stagger → Stun buildup vs Resolve
                               ├─ chains  → falloff per jump
                               └─ riders  → IEffectSink (their own chains)
                            → recovery → next decision
```

**DEPENDENCIES** — Simulation, Events, Rules, Modifiers, Statuses, Content, Randomness.

**OUTPUT** — `CombatOutcome`, 14 published event kinds, `HitResult` traces, `Logged` narration.

**EXTENSION POINTS** — New behaviour is normally **content**: a move, a status, an AI rule, an
affix rule. Touch `CombatEncounter` only for genuinely new *lifecycle* mechanics.

**ENTRY POINT** — `ResolveMove` (line ~684). It is the centre of the system: pipeline → result →
stagger → chains → riders.

---

## 10.11 The hit pipeline

**PURPOSE** — Resolve one hit, stage by stage, and explain itself.

**IMPORTANT FILES** — `core/Combat/HitPipeline.cs`, `Hit.cs` (`Packet`, `DamageLanes`,
`DamageAspects`), `HitLog.cs` (`HitStages`), `ArmorProfile.cs`

**RUNTIME FLOW — the order IS the specification**
```
 packets
   ├─ AVOIDANCE (binary; any success ends resolution, so an avoided hit produces NO packets
   │             and therefore no ailment, no thorns, no on-hit)
   │     dodge → perfect block → parry → evade (untelegraphed only)
   ├─ flat added        (attribute scaling: once per hit, split by packet share)
   ├─ crit              (multiplies base+flat and stops — putting it later would make crit
   │                     builds scale quadratically with everything else)
   ├─ increased         (combat.damage.mult)
   ├─ PER PACKET:  lane avoidance → armour (armour/(armour+K·amount)) → resistance
   │               (sum → cap → penetration after the cap → floor) → vulnerability
   ├─ block             (block STRENGTH scales how much the guard eats, not what gets through)
   ├─ damage taken
   └─ floor             (applied to the hit TOTAL, never per packet)
```

Golden tests assert **the whole trace**, not the final number, so a reordering cannot pass
silently.

**DEPENDENCIES** — Modifiers (`CombatantModifiers`, optional), Randomness.

**OUTPUT** — `HitResult` (type, amount, packets, crit/blocked/avoided, mitigated) + `HitLog`.

**EXTENSION POINTS** — A new stage goes in the ordered body with its own `HitStages` entry and a
golden test. New *sources* for an existing stage are modifier contributions — no pipeline change.

**ENTRY POINT** — `HitPipeline.Resolve`, top to bottom. Then `Mitigate` for the per-packet half.

---

## 10.12 Statuses

**PURPOSE** — Status lifetime, stacking, ticking, cleansing — and the **Resolve** pool that gates
every control.

**IMPORTANT FILES** — `core/Combat/StatusDefinition.cs` (definition + `StatusInstance`),
`StatusController.cs`

**DATA** — `game/data/statuses/*.json` (28). **There is no C# class per ailment.** A status's
`while_active` is a list of modifier contributions and its hooks are ordinary `EffectSpec`s,
which is why 28 statuses cost roughly what 3 would.

**RUNTIME FLOW**
```
CombatEncounter.ApplyStatus(target, id, source, magnitude)
   ├─ × applier's status.potency.mult   (scoped by status id)
   ├─ × receiver's status.duration.mult
   └─ StatusController.Apply
         controls → buildup vs the target's Resolve
                    crossing lands it + opens Control Immunity + Resolve +25% for the encounter
         others   → apply/stack per StackPolicy

one periodic sweep (CombatTuning.StatusTickIntervalTicks) advances statuses, gauges,
timed modifiers and expiring move grants — deterministic ordering under a seed
```

**DoT ticks publish `DamageTaken` with `CanTrigger: false`** — a Poison tick is not a hit and can
never proc anything. That single rule kills an entire class of proc loops.

**DEPENDENCIES** — Events, Modifiers, Content.

**OUTPUT** — status instances; `Ticked` (which combat turns into damage); `ControlOutcome`.

**EXTENSION POINTS** — A new status is one JSON entry: category, stack policy, duration,
magnitude basis, `while_active` modifiers, hooks.

**ENTRY POINT** — `StatusController.Apply` → `ApplyControl` (the Resolve gate) →
`AttachOrRefresh` (the stack policy).

---

## 10.13 Enemies

**PURPOSE** — Compose enemy identity from reusable layers so a new enemy is data, never C#.

**IMPORTANT FILES** — `core/Combat/EnemyComposition.cs` (`EnemyFamilyDefinition`,
`CombatRoleDefinition`, `AiProfileDefinition`), `ActorResolver.cs`, `ActorDefinition.cs`

**DATA** — `enemy_families/` (1) · `enemy_roles/` (3) · `ai_profiles/` (3) · `actors/` (3).

**RUNTIME FLOW**
```
ActorResolver.Resolve(actor, families, roles, profiles) → ResolvedActor
   attributes/resources : family baseline + role delta + actor delta
   resistances / vulnerability / armour / Resolve : per key, later layer wins
   tags : union
   AI   : referenced profile's rules + the actor's inline extras
        ▼
Combatant.FromActor(resolved, moveset)

CombatEncounter.ChooseMove: for each weighted AI rule whose conditions pass, expand by move id
   OR by moveTag, drop unusable moves, apply AvoidRepeatWeight to the last move used,
   then make a seeded weighted pick.
```

**A future Elite/Boss variant is one more delta through the same fold**, never a duplicated
definition.

**DEPENDENCIES** — Content, Combat, Rules (the shared condition vocabulary).

**OUTPUT** — `ResolvedActor` → `Combatant`.

**EXTENSION POINTS** — A new enemy is ~8 lines of JSON: family ref + role ref + moves + tweaks.
A new *brain* is one `ai_profiles` entry. **No class or enemy-name branches anywhere.**

**ENTRY POINT** — `ActorResolver.Resolve` (the merge rules), then `CombatEncounter.ChooseMove`.

---

## 10.14 Professions

**PURPOSE** — Persistent skill progression and the gathering/processing economy.

**IMPORTANT FILES** — `ProfessionDefinition.cs`, `ProfessionActionDefinition.cs`,
`ProfessionProgress.cs`, `ProfessionLeveling.cs`, `ProfessionTuning.cs`, `ActionResolver.cs`,
`ProfessionSystem.cs`, `PassiveProfessionRunner.cs`

**DATA** — `professions/` (8) · `profession_actions/` (26).

**RUNTIME FLOW**
```
ProfessionSystem.Execute(actionId, performance, isActive)      ← the ONE execute path
   ├─ CheckExecutable  (profession level, inputs on hand)
   ├─ ActionResolver   → ResolvedYield (guaranteed outputs + rolled bonus outputs)
   ├─ consume inputs, deposit outputs into the provider's Inventory (ActiveInventory)
   ├─ award XP (active gets a timing-performance bonus) → maybe LeveledUp
   └─ ActionOutcome  →  ActionCompleted event

PassiveProfessionRunner: schedules the action's effective interval on the TickEngine and
                         re-schedules on completion; stops (Stalled) when it cannot proceed.
```

**Active and passive share one path.** There is deliberately no hidden "+x% active" bonus —
active earns its advantage through timing performance only.

**DEPENDENCIES** — Content, Items (`Inventory` provider), Simulation, Randomness.

**OUTPUT** — `ActionOutcome`, XP, deposited items, `ActionCompleted` / `LeveledUp` events.

**EXTENSION POINTS** — A new action is one JSON entry (profession, level gate, interval, inputs,
outputs, bonus outputs, XP). A new profession is one entry plus its actions.

**ENTRY POINT** — `ProfessionSystem.Execute`.

---

## 10.15 Realms and extraction

**PURPOSE** — The spatial run: travel, depth, clearing, and the extract-or-lose rule.

**IMPORTANT FILES** — `RealmDefinition.cs`, `RealmLocationDefinition.cs`, `RealmRun.cs`,
`RealmExtraction.cs`, `RealmTuning.cs`

**DATA** — `game/data/realms/dark_forest.json` (10 locations, 2 depths).

**RUNTIME FLOW**
```
GameRoot.EnterRealm  → new RealmRun(realm, tier)     (run inventory created here)
        RealmTravel  → RealmRun.TravelTo             (adjacency-gated; knowledge on first visit)
        RealmAction  → by location type:
                         Combat  → StartCombatInternal(actorId), remembering the location
                         Gather  → ProfessionSystem.Execute (existing wiring deposits the loot)
                         Event   → narrate + reward + mark cleared
        RealmGoDeeper→ RealmRun.Descend
        RealmExtract → RealmExtraction.Secure(run, stash)   ← stacks AND instances move
        death        → RealmExtraction.Forfeit(run)         ← unsecured loot lost
```

**DEPENDENCIES** — Content, Items, Professions, Combat (all orchestrated by `GameRoot`).

**OUTPUT** — Run state; an `ExtractionSummary`; realm knowledge.

**EXTENSION POINTS** — A new realm is one JSON entry (a location graph with symmetric edges —
validated). A new **location type** needs an enum value plus a case in `GameRoot.RealmAction`.

**ENTRY POINT** — `RealmRun` for the rules; `GameRoot.RealmAction` for the orchestration.

---

## 10.16 The presentation layer (`Dungeons.Presentation`)

**PURPOSE** — The **only** path from simulation state to player-facing text (D30, CLAUDE.md rule
7). One-way, deterministic, unit-tested.

**IMPORTANT FILES**
| File | Owns |
|---|---|
| `PropertyTier.cs` (`Tiers`) | 0–100 → Trace…Extreme, pips, wear words |
| `Trend.cs` (`Trends`, `PropertyMovement`) | Direction, aggregated from the algebra's typed change kinds |
| `RiskBand.cs` (`Risk`) | Integrity projection → SAFE…DESTROYS |
| `PropertyGlossary.cs` | Property → glyph + gloss, **from data** |
| `MaterialReading.cs` | A material as leading properties, receptiveness, traits, essence |
| `CraftReading.cs` | A projection as grouped movements, opposition, risk, emergence |
| `SlotReading.cs` | Why a material suits (or does not suit) a form slot |
| `ItemReading.cs` | An item as stats, modifier lines, moves, genome support |
| `TraitProximity.cs` | "Within reach: Emberveined — needs more Heat" |
| `SemanticFormat.cs` | **All wording.** Readings → strings and typed `ProjectionLine`s |
| `AdvancedFormat.cs` | The numeric voice, behind the Advanced toggle |
| `PresentationTuning.cs` | Presentation thresholds — never gameplay ones |

**RUNTIME FLOW**
```
simulation state  →  XReadings.From(...)  →  XReading (a typed read-model, no strings)
                                          →  SemanticFormat.X(reading, glossary)  →  text
                                          →  ProjectionLine(kind, text)  → the UI colours by kind
```

**THE RULES — do not erode**
1. Raw simulation values never lead a normal play surface. Advanced / Assay / labs only.
2. The layer may **translate, never recompute**. No second simulation.
3. **Display tiers never touch identity quantization** (`QuantizationTuning` is unread here,
   forever).
4. Glyphs and glosses are **data on `PropertyDefinition`**, never code switches.
5. A player-facing modifier ships only when its mechanic resolves in play.

**DEPENDENCIES** — Content, Crafting, Items, Affixes. **Nothing depends on it except the UI**,
which is what makes it safe.

**OUTPUT** — Strings and typed lines. Never game state.

**EXTENSION POINTS** — New wording goes in `SemanticFormat`; new *facts* go in a `XReading`.
Never let the UI compose meaning out of raw values.

**ENTRY POINT** — `SemanticFormat`, then the reading type of whatever surface you are changing.

---

## 10.17 Persistence

**PURPOSE** — Save and restore progression. **Ids and runtime values only — never definitions.**

**IMPORTANT FILES** — `core/Persistence/SaveData.cs` (the DTOs), `SaveSerializer.cs`
(System.Text.Json), `SaveMapper.cs` (live systems ↔ DTOs), `game/Infrastructure/SaveStore.cs`
(`user://save.json`)

**RUNTIME FLOW**
```
SaveGame → SaveMapper.Capture(build, stash, professions, discoveries, knowledge, tick,
                              equipment, instanceIds, emergentRegistry, learnedMoves,
                              emergentEquipment)
         → SaveSerializer → SaveStore.Save → user://save.json

LoadGame → SaveStore.Load → SaveMapper.Apply(...) → RebuildCharacter → EquipStarterLoadout
           (blocked during a realm run)
```

**Schema v6.** Older schemas load forward-compatibly: a missing field arrives empty rather than
failing. v4 added emergent archetypes, v5 learned moves, v6 the genome + rolled affixes.

**The one thing the save stores that is definition-shaped** is the emergent archetype (material)
and the derived equipment definition — not an exception to "ids never definitions" so much as a
consequence of it: a generated archetype *has* no authored definition to point back at. It is a
deterministic cache.

> ⚠ **`SaveData` and every `*Save` class property name IS a save-file key.** Renaming one breaks
> every existing save. See §12.

**EXTENSION POINTS** — New persisted state: a property on the DTO, capture + apply in
`SaveMapper`, bump `CurrentSchemaVersion`, and document the forward-compatible default.

**ENTRY POINT** — `SaveMapper.Capture` / `Apply`, side by side.

---

# 11. Where do I change X?

The navigation table. **"Data only" means you should not need to open the C# at all.**

### Content — data only

| I want to… | Do this |
|---|---|
| **Add a material** | One entry in `game/data/materials/<category>.json`: id `material.*`, name, tags (`family:value`), 0–100 properties, optional `essence`. Validation checks ranges, known property names, and exactly one `rarity:` tag |
| **Add a material property** | One entry in `game/data/properties/properties.json`: name, role, glyph, gloss, opposite, `resisted_by`, `grants_tags`. Add a constant in `core/Items/ItemProperties.cs` **only if code must name it** (a bijection test keeps the two in sync) |
| **Add a crafting process** | One entry in `game/data/processes/processes.json`: channel, medium, severity, role weights, profession gate, substrate tags, tag effects, essence rate |
| **Add a trait** | One entry in `game/data/traits/traits.json`: property conditions, magnitude, category (which aperture gates it), drawback, optional merge rule |
| **Add an essence** | One entry in `game/data/essences/essences.json`: anchor aspect, opposites |
| **Add an item Form** | One entry in `game/data/forms/forms.json`: `type`, `slots` (each: `requires_tags`, `mass_share`, `aperture` per trait category), `stat_map` (each read: `slot`, `property`, `weight`), `trait_cap`, `moves`, `tags` |
| **Add an affix (item modifier)** | One entry in `game/data/affixes/affixes.json`: slot (prefix/suffix/innate), family, class, `eligibility`, `weight`, `tiers`, `grants`, `description` with `$roll`. **Ship it only when its mechanic resolves in play** |
| **Add a Move** | One entry in `game/data/moves/*.json`: namespaced tags, `timing`, `costs`, `requires`, `targeting`, `packets`, `stagger_power`, effect riders. Reachability is validated — grant it from something |
| **Add a Status** | One entry in `game/data/statuses/*.json`: category, stack policy, duration, `magnitude` (basis + coefficient), `while_active` modifiers, hooks. **No C# class** |
| **Add an enemy** | `game/data/actors/<name>.json`: `family`, `role`, `moves`, per-key tweaks, loot. Reuse an `ai_profile` or add inline rules. Never write a C# class |
| **Add an enemy family / role / AI brain** | One entry in `enemy_families/`, `enemy_roles/`, `ai_profiles/`. Roles are **deltas** and must stay family-agnostic |
| **Add a profession action** | One entry in `game/data/profession_actions/<profession>.json`: profession, level gate, interval, inputs, outputs, bonus outputs (`ItemChance`), XP |
| **Add a profession** | One entry in `professions/` plus its action file. Make it cross-feed another profession — a test asserts ≥4 chains |
| **Add a Base / Prefix / Suffix** | One entry in `classes/`, `prefixes/`, `suffixes/`. Bases must spend exactly the 4.0 growth budget. **A Prefix may never name a Base.** An expressed Suffix needs one expression per channel |
| **Add a technique item** | One entry in `techniques/` naming the move it teaches |
| **Add a realm or location** | One entry in `realms/`. Edges must be symmetric and content refs must resolve — both validated |
| **Add a modifier key** | One entry in `modifier_keys/`: kind, clamps, `scoped_by`, `danger` (which then requires a `max`) |

### Behaviour — code

| I want to… | Start here |
|---|---|
| **Change combat damage calculation** | `core/Combat/HitPipeline.cs` — `Resolve` for the whole-hit stages, `Mitigate` for per-packet. Constants in `CombatTuning`. **Update the golden traces in `tests/Combat/HitPipelineTests.cs`** — they assert the whole trace by design |
| **Change how a stat scales** | Prefer a modifier contribution over a pipeline change. Only attribute scaling is hard-coded (`HitPipeline.ApplyAttributeScaling`) |
| **Change crafting behaviour** | `core/Crafting/ReactionAlgebra.cs` for the mathematics; `ReactionEngine.RunReaction` for the orchestration; `ReactionTuning` / `RefinementTuning` / `QuantizationTuning` for numbers. Worked examples are pinned in `tests/Crafting/ReactionAlgebraTests.cs` |
| **Change fabrication behaviour** | `core/Crafting/FabricationEngine.Compose` — **one method, used by both the preview and the mint**. The 0–100 ↔ combat scale is `FabricationTuning.CombatUnitScale`, pinned by the iron-sword parity test |
| **Change item generation (what rolls)** | `core/Affixes/AffixRoller.cs` (eligibility / weight / tier / position) and `AffixTuning` (counts, variance, innate floor). `GenomeCalculator.Pressure` if the *inputs* to those decisions should change |
| **Change enemy AI** | Usually `ai_profiles/` data. For the *selection* mechanism: `CombatEncounter.ChooseMove` |
| **Change the action lifecycle** | `CombatEncounter.Commit` / `EnterWindup` / `Execute`, and `CombatTuning` for the windows |
| **Change what a weapon does to combat** | `core/Equipment/EquipmentResolver.cs` — the whole material → combat seam, 105 lines |
| **Change player-facing wording** | `core/Presentation/SemanticFormat.cs`. If you need a new *fact*, add it to the relevant `XReading` first. **Never format in the UI** |
| **Change a number the player feels** | Find the `*Tuning` class: `CombatTuning`, `ReactionTuning`, `RefinementTuning`, `EssenceTuning`, `FabricationTuning`, `AffixTuning`, `ProfessionTuning`, `RealmTuning`, `EquipmentTuning`, `PresentationTuning`, `QuantizationTuning`, `MaterialProfileTuning` |
| **Add a new effect kind** | Define it in `RuleVocabulary`, implement an `IEffectHandler`, register it (combat's live in `CombatEffectHandlers.RegisterCombatHandlers`). **Propagate `invocation.Context`** |
| **Add a new condition kind** | `TriggerRuleEngine.Evaluate`; if it must read world state, extend `IConditionWorld` and `CombatConditionWorld`. Prefer a derived tag over a new kind (D-11). **Never add a class check** (D25) |
| **Add a new game event** | `GameEvents` constant, publish it from the authoritative system, and note it in the docs |
| **Add a content type** | `ContentBundle` property → `ContentLoader.LoadAll` line → `ContentValidator` rules → a failing-content test per rule |
| **Add a validation rule** | A `ValidateX` in `ContentValidator`, called from `Validate`, plus a broken-content test |
| **Persist new state** | A `*Save` DTO property → `SaveMapper.Capture`/`Apply` → bump `SaveData.CurrentSchemaVersion` → document the forward-compatible default |
| **Add a UI surface** | `MainMvpUI`: a `BuildXSection` for construction, a `RefreshX`/`RebuildX` for updates, and a `GameRoot` query for the content. Colour and layout only |
| **Add a command the UI can call** | A method on `GameRoot` that forwards into Core and raises the right change event. If it contains a game rule, it is in the wrong place |

---

# 12. Persistent identifiers — the do-not-rename list

Some names are data, not code. Renaming them silently corrupts saves or breaks content.

### 🚫 Never rename without a migration

| What | Why |
|---|---|
| Every property of `SaveData` and every `*Save` class in `core/Persistence/SaveData.cs` | These **are** the JSON keys in `user://save.json` |
| The four `CharacterBuild` id properties | Positional **and** persisted |
| `EquipmentSlot`, `ItemType`, `ItemQuality` enum **member names** | Serialized as strings in the save |
| `SaveData.CurrentSchemaVersion` semantics | Bump it; never repurpose a version |
| The `emergent.<hash>` / `equip.emergent.<hash>` scheme, and anything feeding `MaterialSignature` or the fabrication signature | Changing what is hashed re-identifies every stored archetype |

### ⚠️ Rename only with the data, in the same commit

| What | Why |
|---|---|
| Content ids (`material.*`, `move.*`, `status.*`, `affix.*`, …) | Referenced across JSON files and by save data (stash stacks are keyed by item id) |
| The **values** of `ItemProperties` constants (`"hardness"`) and property ids in `properties.json` | Property names are keys in materials, saved instances and saved archetypes |
| Modifier key ids (`combat.damage.mult`, …) | Referenced by affixes, statuses, class components |
| Damage lane / aspect strings, tag families and values | Referenced by content and by saved archetype tags |
| `[JsonPropertyName]` values and the JSON key of any definition property | Referenced by every content file of that type |

### ✅ Free to rename

C# locals, parameters, private fields, private methods, and any public member **not** listed
above — including definition *class* names, service names, and public methods on Core services.

---

# 13. Testing

`tests/` mirrors the Core namespaces. Conventions worth keeping:

- **Content-validation tests load the real `game/data` JSON** via `TestPaths.DataDir`, so shipped
  content is checked by the same rules the game uses at startup — plus a deliberately-broken
  store per rule, so the rule itself is proven to fire.
- **Golden traces** — `HitPipelineTests` asserts the whole hit trace, not the final number.
  `ReactionAlgebraTests` reproduces the documented worked examples exactly.
- **Distribution tests** — `AffixRollerTests` runs 20,000 seeded rolls and asserts the shape.
- **Parity pins** — the iron-sword fabrication parity test pins the 0–100 ↔ combat-unit scale.
- **`tests/Integration/FullLoopTests.cs`** runs the whole loop headless.

Commands:

```bash
dotnet build InTheDungeonsWeDie.slnx
```

```bash
dotnet test
```

Godot is **not** on PATH in this environment — the game window is run from the editor. Verify
with build + tests; verify UI visually in Godot.

---

# 14. Known structural debt

Recorded so it is a decision rather than a surprise.

| Debt | Status |
|---|---|
| `GameRoot` is ~1,650 lines (composition root + application layer + report formatting) | Deferred by D2. Mitigated by keeping every gameplay rule a thin forward into Core |
| `MainMvpUI` is ~1,430 lines and named after a milestone that shipped | Rename deferred (Godot `.tscn`/`.uid` coupling cannot be verified headless) |
| `ContentValidator` is ~1,480 lines in one file | Acceptable: it is a flat list of independent `ValidateX` rules, each self-contained |
| The legacy fixed-interaction crafting path (`CraftingExperimentSystem`, `CraftingInteractionDefinition`, `DiscoverySystem`, `CraftingDerivation`, `ExperimentOutcome`) | Alive only to keep the Healing Salve brewable until consumable forms land. **Delete the whole path then** (D21) |
| `PropertyDefinition.transferable` is unconsumed | Open question — give it a job or drop it |
| `StatusController.ModifierTotal` is display-only | Enforced by convention, not by the type system |
| Response properties drop on transformation | Filed, not fixed |
| Mastery is tracked but nothing reads it; Realm Knowledge unlocks nothing | Content/feature gaps, not structural |
