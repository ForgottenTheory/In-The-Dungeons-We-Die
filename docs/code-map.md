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
| `core/Characters/` | `Dungeons.Characters` | Attributes, resources, gauges, character rules |
| `core/Characters/Composition/` | `Dungeons.Characters.Composition` | The class combinator |
| `core/Combat/` | `Dungeons.Combat` | Encounter, hit pipeline, moves, statuses, enemies |
| `core/Content/` | `Dungeons.Content` | Definition loading, the bundle, validation |
| `core/Crafting/` | `Dungeons.Crafting` | The emergent registry, byproducts, forms, the legacy salve path |
| `core/Crafting/Identity/` | `Dungeons.Crafting.Identity` | The identity crafting stack: state, verbs, composer, effect pipeline |
| `core/Equipment/` | **`Dungeons.Items`** ⚠ | Equipment container, definitions, the combat seam |
| `core/Events/` | `Dungeons.Events` | The game event bus |
| `core/Hideout/` | `Dungeons.Hideout` | Station definitions — the Hideout's routing table |
| `core/Inventory/` | **`Dungeons.Items`** ⚠ | The inventory container |
| `core/Items/` | `Dungeons.Items` | Item instances, property sets, stacks |
| `core/Modifiers/` | `Dungeons.Modifiers` | The modifier key vocabulary, scopes, resolution |
| `core/Persistence/` | `Dungeons.Persistence` | Save DTOs, serializer, mapper |
| `core/Presentation/` | `Dungeons.Presentation` | The semantic read-model (D30) |
| `core/Professions/` | `Dungeons.Professions` | Profession definitions, progress, execution, offline payout, Farming plots, the Agility course |
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
 ├─ build professions   (ProfessionSystem, PassiveProfessionRunner, FarmingPlots, TrainingCourse) on the shared TickEngine
 ├─ build crafting      (EmergentRegistry, VerbActionRunner, IdentityFabricationEngine,
 │                       ItemEffectResolver)
 ├─ build combat        (StatusController, CombatantModifiers, HitPipeline, CombatEncounter)
 ├─ RegisterCombatHandlers(encounter, rng)     ← effect kinds stop landing in Unhandled
 └─ wire the condition world + subscribe to encounter events
```

`_Process(delta)` is the **only** thing that drives simulation time: it accumulates real seconds
into ticks at `TicksPerSecond = 20` and calls `TickEngine.Advance(1)` per whole tick, but only
while `_running` is true.

Everything else in `GameRoot` is one of three things — and nothing else belongs there:

1. **Commands** the UI calls (`RunVerbAction`, `RunIdentityFabrication`, `EquipFromStash`, `RealmTravel`…).
2. **Queries** the UI reads (`MaterialsOnHand`, `PlayerMoveset`, `RealmReport`…).
3. **C# events** the UI subscribes to (`LogEmitted`, `CharacterChanged`, `InventoryChanged`,
   `RunningChanged`, `DiscoveryChanged`, `CombatChanged`, `RealmChanged`).

> **Known debt, recorded in D2.** `GameRoot` is ~1,650 lines and is both the composition root and
> the application layer. Extracting an Application/use-case layer is deferred, not forgotten.
> The mitigation that keeps it survivable: **every gameplay rule is a thin forward into Core.**
> `RunVerbAction` builds an invocation, calls `VerbActionRunner`, and formats the outcome — that is all.
> Keep it that way; if you find yourself writing a `if` about game rules in `GameRoot`, it belongs
> in Core.

### 2.2 `game/ui/MainMvpUI.cs` + `.tscn` — the one screen

The main scene. Builds every control **in code** with a code-only dark theme (no assets):
a persistent header, a `TabContainer` (Character · Char Lab · Equipment · **Hideout** ·
Realm · Combat · Inventory), and an always-visible event-log panel.

> **The Realm tab is two screens that swap** (D39): `RealmPreparationPanel` out of a run, the
> report + travel/fight controls inside one. `RefreshRealm` owns the swap; `RebuildRealmControls`
> has no out-of-realm branch any more.

Its shape is uniform and worth knowing:

- `BuildXSection(root)` — constructs the controls for a tab, once.
- `RebuildX()` / `RefreshX()` — re-renders a dynamic group in response to a `GameRoot` event.
- It calls `GameRoot` methods and reads `GameRoot` strings. **It contains no gameplay logic.**
  Colour and layout are the only decisions it makes.

> The name `MainMvpUI` is historical — the MVP shipped long ago. Renaming it means renaming the
> C# file, the `.uid`, and the script reference inside `MainMvpUI.tscn`, which cannot be verified
> without running the Godot editor. Recorded as a deferred rename, not an oversight.

**The Hideout tab is not one section — it is a host.** A monolithic Crafting tab used to put all
eight crafting actions and every blueprint on one screen regardless of where any of it belonged;
it is gone, and so is the Professions tab, whose ladder now lives at the station that trains it.
What replaced them is one fixed **activity strip** (the passive bar, the active-timing sweep and
the Discover → Pursue card — global, so they must be co-located with the button that raises them)
over a **station index ⟷ one station page**.

The station page is composed from the station's own definition, which is why twenty destinations
cost one class:

| File | Owns |
|---|---|
| `ui/ConsoleTheme.cs` | The palette and the `Row`/`Card`/`MakeButton`/`SectionTitle` vocabulary. Imported with `using static`, so call sites read unchanged |
| `ui/StationPanel.cs` | Composes one station's page from what its definition routes to |
| `ui/ProfessionLadderPanel.cs` | One profession's level-gated ladder, with Passive/Active |
| `ui/VerbBenchPanel.cs` | The identity bench: action/material/identity pickers shaped by the verb, inspector, preview |
| `ui/IdentityForgePanel.cs` | The identity forge: form + per-slot pickers, the projection, an Advanced toggle |
| `ui/FarmingPlotsPanel.cs` · `TrainingCoursePanel.cs` · `AssayBenchPanel.cs` | The three professions that are a system rather than a list. Drawn because of **which profession the station hosts**, never a flag |
| `ui/CraftingInteractionsPanel.cs` | The legacy fixed-interaction list (the Healing Salve). Dies with P5c |

A panel takes `GameRoot` plus the slice it renders, and exposes one `Refresh()`. Station pages are
built on first visit and kept, so walking away from a half-assembled reagent chain does not
discard it; only the open page is refreshed on an inventory change, and every page refreshes when
it is opened.

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
│      ├── Crafting ───── IdentityCraftingEngine, IdentityFabricationEngine     │
│      ├── Characters ─── CharacterComposer, BuildResolver              │
│      ├── Realms ─────── RealmRun, RealmExtraction                     │
│      ├── Items ──────── Inventory, Equipment, ItemInstance            │
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

### 4.3 Vocabularies have exactly one source of truth (D17's heir)

Every string a payload, profile or form references resolves against a loaded registry —
identities, triggers, behaviors, payloads, modifier keys, statuses, moves — **never** a code
list. The two authored equipment property keys (`mass`, `hardness`) are the one closed
code-level set (`ItemProperties`), validated as such.

### 4.4 Id convention (D19)

`type.slug` — `material.oak_bark`, `equip.iron_sword`, `move.heavy_strike`, `status.burn`,
`identity.dense`, `profession.mining`, `action.mine_iron`, `actor.goblin_raider`,
`technique.*`, `craft.*`, `form.*`, `realm.*`, `class.*`/`prefix.*`/`suffix.*`/`species.*`.
Realm-location ids (`loc.*`) are realm-scoped, not globally unique. **Sentence-vocabulary ids
are bare** (`on_block`, `store`, `bulwark`) — they are keys, not entities.

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
| `VerbActionRunner` | `Dungeons.Crafting.Identity` | Every bench act: gates → verb → deposit |
| `IdentityFabricationEngine` | `Dungeons.Crafting.Identity` | Materials → minted equipment |
| `ItemEffectResolver` | `Dungeons.Crafting.Identity` | The item-effect pipeline + the equip-time compile |
| `EmergentRegistry` | `Dungeons.Crafting` | Fingerprint → registered runtime material |
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
| `IdentityMaterialState` | `core/Crafting/Identity/` | A material's full crafting state: identities, latents, capacity, condition, quality, roots — stability derived |
| `TickEngine` | `core/Simulation/` | Integer ticks, deterministic ordering, cancellable schedules |
| `IRandomSource` | `core/Randomness/` | Injected, seeded. **No global RNG anywhere** |
| `GameEvent` / `IGameEventBus` | `core/Events/` | The 31-event vocabulary |
| `TriggerRule` / `ConditionSpec` / `EffectSpec` | `core/Rules/` | The universal hook |
| `EffectContext` | `core/Rules/` | Chain id + depth + proc rules |
| `ModifierContribution` / `ModifierSet` / `ModifierContext` | `core/Modifiers/` | The modification vocabulary |
| `ActionTiming` / `ActionCost` | `core/Actions/` | Telegraph/windup/recovery, and costs that may name a **gauge**. Shared by Moves and (later) Profession actions — components, deliberately **not** an `abstract class Action` |
| `ResolvedMove` | `core/Combat/` | A move after grants + modifiers, with full provenance |
| `ItemEffectSentence` | `core/Crafting/Identity/` | What a minted item carries; grants recompile from it deterministically |

---

# 8. How systems communicate

Five mechanisms, and only five.

| # | Mechanism | Used for | Example |
|---|---|---|---|
| 1 | **Direct construction + injection** | Everything wired at startup | `new IdentityFabricationEngine(content, () => ActiveInventory, …)` |
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
  all created in `GameRoot._Ready()`: rules, professions, the bench, the forge, combat.
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

## 10.3 Materials — the identity model

**PURPOSE** — The ingredient set the crafting system operates on (D42–D52; the 0–100 property
model died with Phase 7/D54).

**IMPORTANT FILES**
- `core/Content/MaterialDefinition.cs` — capacity, `identities` (ranked grants), `latent`,
  `base` (Heft/Bite/Toughness/Give, 0–10), `signature_profile`, `family:value` tags
- `core/Content/IdentityDefinition.cs` — the 24-identity roster (pinned by test)
- `core/Content/SignatureVocabulary.cs` — triggers, behaviors, themes, payloads (+ bindings)
- `core/Crafting/Identity/IdentityMaterialState.cs` — the runtime eight facets: identities,
  latents, capacity, Condition, quality, carrier flag, roots; Stability always derived
- `core/Crafting/Identity/IdentityStateResolver.cs` — definition → starting state
- `core/Crafting/Identity/RootDerivations.cs` — merged profile + base from provenance roots
- `core/Crafting/Identity/Fingerprint.cs` — the stacking hash
- `core/Content/TagFamilies.cs` — the closed `family:value` namespace (+ `StructuralForms`/
  `EdgeCapableForms`, the D52 structural fences)

**DATA** — `game/data/materials/` (1,448; 53 with active identities) · `identities/` (24) ·
`signature_triggers/` (22) · `signature_behaviors/` (11) · `signature_payloads/` (29) ·
`signature_themes/` (16, never player-facing).

**ENTRY POINT** — `docs/identity-foundation.md` first; then `IdentityMaterialState`.

---

## 10.4 The identity bench — verbs as content

**PURPOSE** — Every crafting act is one of ten verbs, offered as content actions, with risk
only where the crafter chose it (overfill → fracture; Fragile deep work → destruction).

**IMPORTANT FILES**
- `core/Crafting/Identity/VerbCraft.cs` — the verb enum, requests, projections, outcomes
- `core/Crafting/Identity/IdentityCraftingEngine.cs` — the ten executors; preview = commit − dice
- `core/Content/VerbActionDefinition.cs` — verb + gates + identity scope + Process output + XP
- `core/Crafting/Identity/VerbActionRunner.cs` — gates → verb → consume → register → deposit;
  awards XP/mastery through `ProfessionProgress`; mastery shaves risk (`RiskReduction`, capped)
- `core/Crafting/Identity/IdentityNameGenerator.cs` — identity adjectives + "-bound" roots
- `core/Crafting/EmergentRegistry.cs` — minted materials register under fingerprint ids
- `core/Crafting/Identity/IdentityCraftTuning.cs` — every bench number (all provisional)

**DATA** — `game/data/verb_actions/` (53 actions, 11 professions) routed by `stations/`.

**RUNTIME FLOW** — `GameRoot.PreviewVerbAction`/`RunVerbAction` → runner → engine →
`EmergentRegistry` + inventory. Authored equivalence: plain smelted ore deposits as
`material.iron_ingot`, never an emergent twin; Process merges the output's innate identities
(preparation = activation).

**ENTRY POINT** — `docs/transformation-verbs.md`, then `VerbActionRunner`.

---

## 10.5 The identity forge — materials become equipment

**PURPOSE** — The terminal boundary. Compose the item side (D51 union/cap/dormancy + D46 base
delivery), then generate its effects (D50), with the preview drawn from the same computation.

**IMPORTANT FILES**
- `core/Crafting/Identity/IdentityEquipmentComposer.cs` — union, form cap, readable selection
  (priority → rank → contribution → id), dormancy, base delivery, quality, the item name
- `core/Crafting/Identity/IdentityFabricationEngine.cs` — gates → compose → resolve → consume
  → register the derived definition (`equip.emergent.i<hash>`) → mint; also `FormNoun` (the
  deterministic D34 name-variant pick)
- `core/Crafting/EquipmentBlueprintDefinition.cs` — forms: slots (tags, mass share, identity
  priority), `identity_cap`, `base_reads`, `generation_profile`, moves, name variants

**DATA** — `game/data/forms/forms.json` (23 forms, all identity-forgeable — D54; six
`has_assembly` stations host the forge, forms need no per-station routing).

**ENTRY POINT** — foundation §8.1/§11.5, then `IdentityEquipmentComposer.Compose`.

---

## 10.6 The item-effect pipeline — sentences

**PURPOSE** — One generator for everything a minted item does, in three categories kept apart
(floor / generated / Signature) plus the volatile drawback.

**IMPORTANT FILES**
- `core/Crafting/Identity/ItemEffectResolver.cs` — `Project` (floor + scored table + odds; the
  table IS the draw distribution) and `Resolve` (the same projection, plus dice);
  `SignatureResolver` (chance + the coherent bundle); `FloorPayloadOf` (the shared floor rule);
  `CompileAll` (sentences → grants, deterministic — grants are never stored)
- `core/Crafting/Identity/ItemEffectSentence.cs` — what persists: category + vocabulary ids +
  magnitude + chance
- `core/Crafting/Identity/SentenceAssemblers.cs` — one compiler per behavior onto machinery
  that already resolves (drain = damage+restore, store = gauge+band — the D30 fence, stated
  in code)
- `core/Crafting/Identity/ItemEffectTuning.cs` — every pipeline number (all provisional)

**RUNTIME FLOW (worn)** — `GameRoot.AttachBuildRules`/`EquippedIdentityGrants`: sentences
recompile per rebuild into stat contributions, `TriggerRule`s, gauges and move-modifier
grants — the same seams character components use, so effects swap with the gear.

**ENTRY POINT** — foundation §7–§8, then `ItemEffectResolver.Project`.

---

## 10.7 Items, inventory, equipment

**PURPOSE** — Own what the player has and what they are wearing, and hand combat a **neutral**
shape rather than an equipment type.

**IMPORTANT FILES** — `core/Items/ItemInstance.cs`, `PropertySet.cs`, `ItemStack.cs`,
`InstanceIdSource.cs` · `core/Inventory/Inventory.cs` ·
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
same seam. New slots go on the `EquipmentSlot` enum plus the `Equipment` container — **appending
is free** (slots persist by name, so an older save simply arrives with that slot empty); renaming
one is a save migration, which is the whole of D32. If the new slot comes in a set whose members
are interchangeable, add it to `EquipmentSlots.InterchangeablePositions` rather than authoring one
form per position — see the rings, D33.

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
modifiers: character components + worn equipment + worn identity sentences
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

**EXTENSION POINTS** — New behaviour is normally **content**: a move, a status, an AI rule, a
sentence payload. Touch `CombatEncounter` only for genuinely new *lifecycle* mechanics.

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

## 10.13b Auto-combat (Phase 10, D41)

**PURPOSE** — Play the player's side without the player. GDD §5.7, and the D-07 consequence in
`docs/damage-and-defense.md` §5.1.1.

**IMPORTANT FILES** — `core/Combat/AutoCombatPilot.cs`, `core/Combat/AutoCombatProfileDefinition.cs`
(also holds `DefenceRuleSpec` and `AutoCombatTuning`) · `ContentValidator.ValidateAutoCombatProfiles`.

**DATA** — `auto_combat/profiles.json` (3 brains: Steady, Aggressive, Cautious).

**RUNTIME FLOW**
```
GameRoot.SetAutoCombatEnabled(true) → EngagePilot()
  AutoCombatPilot.Engage
     └─ Combatant.Ai ← profile.Rules          the player IS an AI-profile actor now
  every tick (DecisionPollTicks):
     ├─ AnswerIncomingAttack   reads CombatEncounter.Intents (the SAME read-model the UI shows)
     │     stance goes up at max(noticed + R, impact − R)  ← R = reaction_ticks
     │     └─ encounter.Block() / encounter.Dodge()
     └─ Attack                 encounter.ChooseMoveFor(player) → encounter.UseMove(id)
```

**THE ONE RULE.** *It chooses; it never resolves.* Timing, telegraphs, costs, statuses, damage,
defences, cooldowns and triggers all run in the one encounter on the one tick engine. **There is
no simplified offline combat calculator and there must never be one** — the moment automated play
has its own maths, passive and active are two balance models wearing one name.

**WHY IT IS WEAKER.** One number. A hand R ticks behind the eye must commit R ticks before impact,
and every tight window is measured from when the stance went up:

| Window | Ticks | R = 8 |
|---|---|---|
| Block | 16 | reliably |
| Dodge | 10 | reliably |
| Perfect Block | 4 | **never** |
| Parry | 3 | **never** |

Attacks arriving sooner than 2R after they appear are unanswerable — which is what the small
untelegraphed-only `evade` passive is for. **No damage multiplier exists anywhere in the pilot**;
`AutoCombatTests.AnAutomatedSwingHitsForExactlyWhatAManualOneDoes` fails the day one is added, and
`AutoCombatTuning.MinimumReactionTicks` (derived from the windows) refuses a brain fast enough to
parry.

**DEPENDENCIES** — Combat, Simulation, Rules, Randomness.

**EXTENSION POINTS** — A new brain is one JSON entry. Rules match moves **by tag**, never by id,
because a player's moveset comes from their weapon — a test holds that for shipped content.

**ENTRY POINT** — `AutoCombatPilot.Decide`.

---

## 10.14 Professions

**PURPOSE** — Persistent skill progression and the gathering/processing economy. **20
professions**; the design lives in `docs/professions.md`.

**IMPORTANT FILES** — `ProfessionDefinition.cs`, `ProfessionActionDefinition.cs`,
`ProfessionOpportunityDefinition.cs`, `ProfessionProgress.cs`, `ProfessionLeveling.cs`,
`ProfessionTuning.cs`, `ActionResolver.cs`, `ProfessionSystem.cs`,
`PassiveProfessionRunner.cs`, `OfflineProgressCalculator.cs`, `AwayProgress.cs`,
`ProfessionBenefits.cs`, `ProfessionSynergies.cs`, `FarmingPlots.cs`,
`TrainingCourse.cs` · plus `Presentation/AssayLens.cs`, `Presentation/AwayReadout.cs`.

**DATA** — `professions/` (20) · `profession_actions/` (348 actions, 36 nested opportunities) ·
`training_obstacles/` (12) · `mastery/` (6 rungs) · `synergies/` (15: 13 cross-profession + 2 global).

**RUNTIME FLOW**
```
ProfessionSystem.Execute(actionId, performance, isActive)      ← the ONE execute path
   ├─ CheckExecutable  (profession level, inputs on hand)
   ├─ ActionResolver   → ResolvedYield
   │     ├─ success roll (SuccessChance < 1 only for Hunting and Thieving)
   │     ├─ guaranteed outputs + rolled bonus outputs
   │     └─ opportunity discovery  ← ACTIVE ONLY, by construction
   ├─ consume inputs, deposit outputs into the provider's Inventory (ActiveInventory)
   ├─ award XP (active gets a timing bonus; a miss pays MissedAttemptXpFraction)
   └─ ActionOutcome  →  ActionCompleted event

ProfessionSystem.PursueOpportunity(actionId, opportunityId)     ← the player said yes
   └─ risk roll (mastery talks it down) → payoff or nothing, either way XP

PassiveProfessionRunner: holds the STANDING selection (SelectedActionId) and schedules the
                         effective interval on the TickEngine. Idle → Working → Waiting:
                         running out of inputs WAITS and resumes by itself (Phase 10
                         auto-repeat); only Stop() clears the selection.

OfflineProgressCalculator.Apply(system, actionId, elapsedRealSeconds)
   └─ loops the SAME Execute at performance 0 — so offline can never drift from live passive.
      Bounded by MaxOfflineTicks (12h) and MaxOfflineCompletions.

AwayProgress.Resolve(system, selectedActionId, elapsedSeconds, plots, currentTick) → AwayReport
   └─ aggregates ONE absence: the offline payout + crops lifted + items merged per id + levels
      gained. Aggregates, never resolves. Rendered by Presentation/AwayReadout.
```

**Active and passive share one path.** Passive's "fewer rare outcomes" is structural, not a
tuning number: only the active path rolls for opportunities at all.

**THE BENEFIT SEAM (Phase 10).** `ProfessionBenefits` answers the one question the execution path
asks — *what is this benefit worth, right now, for this action?* — by folding together:

| Source | Reads | Content |
|---|---|---|
| `MasteryBenefits` | per-**action** mastery | `mastery/` |
| `ProfessionSynergies` | another **profession's** level, or the **total** across the roster | `synergies/` |
| *(E6)* worn tools | — | — |

All three pay into the same six `ProfessionBenefitKind` quantities, so adding the second source
changed **no line** of `ActionResolver` or `ProfessionSystem`. A synergy with no `source` is the
global bonus and reads total level; source and target must differ (validated).

**WHO OWNS THE CLOCK.** Core resolves an opportunity's gamble instantly and deterministically;
*when* the result arrives is the client's business — `GameRoot.PursuePendingOpportunity`
schedules the `extraIntervalTicks` on the shared `TickEngine`. Keep it that way: putting the
scheduling in Core would drag `TickEngine` into every profession test.

**THE TWO BESPOKE SYSTEMS** (nothing else needed one)
- `FarmingPlots` — the only profession that runs in parallel with itself. Plant takes the seed;
  harvest is **prepaid** (`ProfessionSystem.CompletePrepaidAction`) so it does not charge twice.
  Growth is absolute ticks, so crops finish while the game is closed;
  `GameRoot.RebasePlantedCrops` moves remaining grow time onto the new session's clock on load.
- `TrainingCourse` — Agility. Five slots, one obstacle each; `ActiveBonuses()` is what the rest
  of the game reads (`CourseBonusKeys`). Nothing consumes those bonuses yet.

**DEPENDENCIES** — Content, Items (`Inventory` provider), Simulation, Randomness.

**OUTPUT** — `ActionOutcome` (now carrying `AttemptMissed`, `DiscoveredOpportunity`,
`RealmKnowledgeGained`), `OpportunityOutcome`, `OfflineProgressReport`, XP, deposited items,
`ActionCompleted` / `OpportunityResolved` / `LeveledUp` events.

**EXTENSION POINTS** — A new action is one JSON entry. A new opportunity is a nested entry on an
action. A new profession is one entry plus its action file — and it must cross-feed, because
`ProfessionEcosystemTests` fails a profession that consumes nothing or feeds nothing.

**ENTRY POINT** — `ProfessionSystem.Execute`.

---

## 10.15 Hideout stations

**PURPOSE** — Give every profession, crafting action and blueprint a *place*, so the player
reaches them the way the fiction describes: **choose a station, then use what it is for.**

**IMPORTANT FILES** — `core/Hideout/StationDefinition.cs` · `ContentValidator.ValidateStations`
· `GameRoot.StationsIn` / `CraftingActionsAt` / `BlueprintsAt` / `InteractionsAt` ·
`game/ui/StationPanel.cs` and the panels listed in §2.2.

**DATA** — `game/data/stations/stations.json` (20 — one per profession).

**RUNTIME FLOW**
```
StationDefinition  { professions[], crafting_actions[], blueprints[] }
        │
        │  GameRoot resolves ids → definitions (routing only; no rules, no gates)
        ▼
StationPanel  ── ProfessionLadderPanel  per hosted profession
              ├─ FarmingPlots / TrainingCourse / AssayBench   ← keyed on WHICH profession
              ├─ CraftingInteractionsPanel                    ← interactions gated on a hosted profession
              ├─ VerbBenchPanel            (only if verb_actions is non-empty)
              └─ IdentityForgePanel        (only if has_assembly)
```

**A station owns no rules.** Hosting is *where you stand*, never *whether you may* — a hosted
crafting action keeps whatever gate it always had, and the picker line says so. An action may
have several homes (Grind is ungated: a mortar at the Apothecary, a mill at the Workbench).

> ⚠ **Temporary (2026-08-17):** `process.distill` and `process.attune` are **ungated for
> playtesting**. The split gave each its own station (Alchemy Lab, Runic Altar) while their gates
> still named Herblore 12 and Alchemy 10, so neither station could be exercised without levelling
> someone else's profession. The designed gates are unchanged in the docs; the override is marked
> in `processes.json` and named in `CraftingActionContentTests.OnlyGrindIsUngated`, which goes red
> the moment the exception list stops matching the content.

**DEPENDENCIES** — Content only (it references professions, crafting actions and blueprints by id).

**OUTPUT** — Nothing. It is a routing table read by the client.

**EXTENSION POINTS** — A new station is one JSON entry. The validator enforces reachability in
both directions, which is the whole value of the type:

- **every profession is hosted by exactly one station** — no orphan, no ladder drawn twice;
- **every station hosts at least one profession** — no unreachable furniture;
- **every crafting action and every blueprint is offered somewhere** — the same "orphan content"
  standard the move vocabulary is held to.

So a new profession, process or blueprint cannot ship without a place to use it — the suite goes
red, deliberately.

**ENTRY POINT** — `StationDefinition`, then `StationPanel`'s constructor.

---

## 10.16 Realms and extraction

**PURPOSE** — The spatial run: travel, depth, clearing, and the extract-or-lose rule.

**IMPORTANT FILES** — `RealmDefinition.cs`, `RealmLocationDefinition.cs`, `RealmRun.cs`,
`RealmExtraction.cs`, `RealmTuning.cs`

**DATA** — `game/data/realms/dark_forest.json` (15 locations, 2 depths).

**RUNTIME FLOW**
```
GameRoot.EnterRealm  → new RealmRun(realm, tier)     (run inventory created here)
        RealmTravel  → RealmRun.TravelTo             (adjacency-gated; knowledge on first visit)
        RealmAction  → by location type:
                         Combat  → StartCombatInternal(actorId), remembering the location
                         Gather  → ProfessionSystem.Execute(active) + the node's loot table
                         Event   → narrate + the node's loot table + mark cleared
        RealmGoDeeper→ RealmRun.Descend
        RealmExtract → RealmExtraction.Secure(run, stash)   ← stacks, instances AND coin move
        death        → RealmExtraction.Forfeit(run)         ← unsecured loot lost
```

**DEPENDENCIES** — Content, Items, Professions, Combat (all orchestrated by `GameRoot`).

**OUTPUT** — Run state; an `ExtractionSummary`; realm knowledge.

**EXTENSION POINTS** — A new realm is one JSON entry (a location graph with symmetric edges —
validated). A new **location type** needs an enum value plus a case in `GameRoot.RealmAction`.

**ENTRY POINT** — `RealmRun` for the rules; `GameRoot.RealmAction` for the orchestration.

---

## 10.16b Realm preparation and the loadout (D39)

**PURPOSE** — The bridge from Hideout to run: what you take, where you go, and what you already
know about it.

**IMPORTANT FILES** — `RunLoadout.cs`, `LoadoutCheck.cs` (both `core/Realms/`),
`RealmBriefing.cs`, `RealmFieldwork.cs`, `PreparationText.cs` (all `core/Presentation/`),
`game/ui/RealmPreparationPanel.cs`

**THE MODEL IS TWO FIELDS, ON PURPOSE.** `RunLoadout` holds a **destination** and a **pack** —
nothing else. Worn `Equipment` already *is* the gear half of a loadout, it already persists, and
combat already resolves from it; a second copy would give the game two answers to "what is the
player wearing". The screen edits the real equipment through `EquipFromStash`/`UnequipToStash`.

**RUNTIME FLOW**
```
GameRoot.SelectRealm        → RunLoadout.SelectRealm
        PackConsumable      → RunLoadout.Pack           (a declaration; the Stash still holds it)
        Briefing()          → RealmBriefing.Compile(bundle, realm, knowledge)
        Fieldwork()         → RealmFieldwork.Survey(...)
        LoadoutStatus()     → LoadoutCheck.Inspect(worn, stash, loadout, definitionIsKnown)
        IssueStarterKit     → EquipStarterLoadout       (only when no weapon exists ANYWHERE)
        EnterPreparedRun    → EnterRealm + move LoadoutCheck.PackableFrom(...) into the run bag
```

**THREE RULES THAT MUST NOT ERODE:**

1. **The door is never locked.** Every gear problem is a `LoadoutIssue` warning; `CanEnter` fails
   only for "no realm selected". GDD §13.1 promises the player can never be stuck, and refusing
   entry is the easy way to break that. Pinned by
   `LoadoutCheckTests.NoAmountOfMissingGearEverStopsThePlayerEntering`.
2. **The pack clamps, never fails.** A standing plan that outruns the Stash takes what is left.
   Entering does **not** empty the pack — a loadout you rebuild every run is not a loadout.
3. **Every briefing gate goes through `RealmKnowledgeLevels.Reveals`.** No second threshold
   table, ever, or the screen and the in-run intel drift apart. Node visibility is
   `RealmLocationDefinition.IsVisibleAt` — shared with travel, so the map and the movement agree.

**WHAT PACKING FIXED** — `CombatUseConsumable` reads the *active* bag, which inside a Realm is the
run inventory, and `EnterRealm` created it empty. A Healing Salve in the Stash was **unreachable
during a run**. Packing transfers at entry, so supplies obey the extraction risk model for free.

**PROFESSION TOOLS ARE NOT HERE** — they are E6 (tool slots, tool forms, the yield pipeline, and
the Agility course's unread `CourseBonusKeys`). The Tools panel shows `RealmFieldwork` readiness
instead. A tool slot with nothing reading it is a surface whose mechanic does not resolve (rule
7). **When E6 lands, tool slots belong on this reading.**

**ENTRY POINT** — `LoadoutCheck.Inspect` for the rules; `GameRoot.EnterPreparedRun` for the
orchestration; `RealmPreparationPanel` for the screen.

---

## 10.16c Progression — the seven tracks (D40)

**PURPOSE** — Layered progression that stays layered. Seven persistent tracks, each changing what
the player can do, and **no single number representing all of them** (GDD §4).

| Track | Where it lives | What reads it |
|---|---|---|
| Profession levels + XP | `ProfessionProgress`, `ProfessionLeveling` | Action gates, Farming plots, the Agility course, Assay depth |
| **Per-action mastery** | `ProfessionProgress.Masteries`, `MasteryLeveling` | `MasteryBenefits` → interval, preservation, doubling, rare finds, opportunity odds/risk, and `required_mastery` gates |
| **Realm Knowledge** | `_realmKnowledge` (per realm), `RealmKnowledgeLevels` | `RealmBriefing`, `GameRoot.KnowledgeIntel`, `RealmRun.IsReachable`, `RealmRun.DeepestReachableEntry` |
| **Character XP + attributes** | `CharacterProgress`, `CharacterLeveling` | `GameRoot.RebuildCharacter` → `ResolvedBuild.GrowthAt` → the Base's own growth weights |
| Crafting discoveries | `DiscoverySystem` | Bench interaction gating |
| Techniques | `LearnedMoves` | Moveset composition |
| Assay | Profession level | `AssayLens.DepthFor` → how much of a material reading is legible |

**THE RULE THAT HOLDS IT TOGETHER** — **character XP comes from Realm activity only.** Nothing in
the Hideout feeds it. If fishing raised combat attributes, every track would collapse into one
power number and GDD §4 would be a comment rather than a rule.
`ProgressionEcosystemTests.ProfessionWorkAwardsNoCharacterXp` fails at *compile* time if
`ActionOutcome` ever grows the field.

**MASTERY IS CONTENT** — `game/data/mastery/`, one shared six-rung ladder. Mastery level is
completions, linear, ceiling 99: a bending curve would reprice every action in the game, which is
a balance decision and balance is parked. Preservation (20) and doubling (40) carry unlock levels
so they *start happening* rather than creeping up from zero.

**LEVELLING NEVER HEALS** — `RebuildCharacter` composes a fresh `Character`, and a fresh one
starts full. Pools carry across, clamped to the new maxima. Loading a save is the one deliberate
exception, because that is a rest.

**THE FENCE** — `tests/Progression/ProgressionEcosystemTests.cs` names every track and asserts
something reads it, in the spirit of `NoProfessionIsADeadEnd`. Every gap Phase 8 closed had been
found by *reading*, months late. **Form acquisition is exempt by name** (D29.2, M6); when it
ships, delete the exemption and the roll-call should still pass.

**ENTRY POINT** — `MasteryBenefits.ValueOf` for mastery; `CharacterLeveling` for the character;
`RealmKnowledgeLevels.Reveals` for knowledge.

---

## 10.16a Loot (`Dungeons.Loot`)

**PURPOSE** — What a source drops. One table shape for every payer in the game, so "how loot
works" is one readable method rather than a rule per source. Full doc: **`docs/loot.md`**.

**IMPORTANT FILES** — `LootTableDefinition.cs` (table/entry/draw/gold/condition),
`LootResolver.cs` (every roll), `LootContext.cs`, `LootResult.cs`, `LootReachability.cs`
(walks the graph without rolling it), `LootRarity.cs`, `LootTuning.cs`

**DATA** — `game/data/loot_tables/` — `shared.json` (the nested library), `enemies.json`,
`gathering.json`, `realm_dark_forest.json`. 34 tables, **zero new materials**.

**THE THREE DROP RULES**, as separate named lists rather than a kind field:
`alwaysDrops` · `chanceDrops` (each rolls its own chance) · `weightedDraws` (`picks` from a
weighted set; `dropsNothing` is a real miss). An entry sets exactly one of
`itemId` / `tableId` / `dropsNothing` — validated.

**WHO POINTS AT A TABLE**
```
EnemyFamilyDefinition.LootTableId ─┐
CombatRoleDefinition.LootTableId  ─┼→ ActorResolver → ResolvedActor.LootTableIds → Combatant
ActorDefinition.LootTableId       ─┘   (accumulates across layers — it does NOT override)

RealmLocationDefinition.LootTableId   Gather: on top of the action, only when it lands
                                      Event:  the node itself
ProfessionActionDefinition.LootTableId  rolled inside ProfessionSystem.Execute, via the
                                        RollActionDropTable delegate (null = no loot wired)
```

**THE ACTIVE/PASSIVE SEAM** — `LootContext` carries `active` or `passive`, and gathering tables
gate their second draw on `active`. Passive play cannot reach those entries **at any rate** —
the same structural trick opportunities use. Do not "fix" it into a probability.

**RARITY IS READ, NEVER AUTHORED TWICE** — a dropped material's own `rarity:` tag decides; only
items with no tag (techniques, schematics) may declare `rarity` on the entry. The other
direction is a validation error.

**GOLD** — lives on `Inventory`, so coin obeys the extraction risk model for free (unsecured in
a Realm, safe in the Stash). Save **v8**. Nothing spends it — there is no economy yet.

**EXTENSION POINTS** — A new enemy becomes lootable with **one line**: point `loot_table` at the
shared tables that already ship. Elite/boss support is already wired — `loot.shared.rank_spoils`
is nested by every family table and fires on the `elite`/`boss` context tag, which comes from
the actor's own identity tags. `loot.template.beast_anatomy` is the ready-made creature table.

**ENTRY POINT** — `LootResolver.Roll` for the rules; `GameRoot.GrantLoot` for the orchestration.

---

## 10.17 The presentation layer (`Dungeons.Presentation`)

**PURPOSE** — The **only** path from simulation state to player-facing text (D30, CLAUDE.md rule
7). One-way, deterministic, unit-tested.

**IMPORTANT FILES**
| File | Owns |
|---|---|
| `SentenceReadings.cs` | One effect sentence → one player line, truthful to what the assemblers compile; modifier units derived from the key registry |
| `ItemReading.cs` + `SemanticFormat.cs` | The item card and strip: moves, armour, identities in rung words, sentences under D50's category labels |
| `MintReadings.cs` | The forge preview: likelihood words for the draw table (D53), the Advanced voice with exact scores |
| `VerbReading.cs` (`VerbReadings`) | Bench refusals in words; preview/outcome change lines diffed from engine states |
| `IdentityMaterialReading.cs` | The bench inspector: stakes and slots, latents, carrier, condition/workmanship/overfill meanings; the D53 leanings and potential phrases |
| `IdentityPhrases.cs` | Rung words (never numerals — D44), quality words, state markers and meanings |
| `AssayLens.cs` | The reveal ladder: Vessel → Latency → Latents → Leanings → Potential; stakes and overfill never gated |
| `EquipmentSlotNames.cs` | `EquipmentSlot` → player text. **Slot enum members are save keys and read as data, not English** — `Ring1` is the reason this file exists |
| `RealmBriefing.cs` / `RealmFieldwork.cs` / `AwayReadout.cs` / `PreparationText.cs` | The Realm and away voices |
| `PresentationTuning.cs` | Presentation thresholds — never gameplay ones |

**RUNTIME FLOW**
```
simulation state  →  XReadings.From(...)  →  a typed read-model / player line
                                          →  SemanticFormat / the panel  →  text
```

**THE RULES — do not erode**
1. Raw simulation values never lead a normal play surface. Advanced / Assay / labs only.
2. The layer may **translate, never recompute**. No second simulation.
3. **Display tiers never touch identity quantization** (`QuantizationTuning` is unread here,
   forever).
4. Display names live on the definitions (identities, triggers, payloads), never code switches.
5. A player-facing effect ships only when its mechanic resolves in play.

**DEPENDENCIES** — Content, Crafting.Identity, Items. **Nothing depends on it except the UI**,
which is what makes it safe.

**OUTPUT** — Strings and typed lines. Never game state.

**EXTENSION POINTS** — New wording goes in `SemanticFormat`; new *facts* go in a `XReading`.
Never let the UI compose meaning out of raw values.

**ENTRY POINT** — `SemanticFormat`, then the reading type of whatever surface you are changing.

---

## 10.18 Persistence

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

**Schema v14** (D49/D54): the identity model alone. Progression sections load
forward-compatibly from any older schema; **item sections load only from v14+** — a pre-v14
save keeps its progression and loses its items, and the starter-kit rule re-equips.

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
| **Add a material** | One entry in `game/data/materials/<category>.json`: id `material.*`, name, tags (`family:value`), `capacity` (1–4), optional `identities`/`latent`/`base`/`signature_profile`. Validation checks ranges, identity references, structural-form rules (D52) and exactly one `rarity:` tag |
| **Add a bench action** | One entry in `game/data/verb_actions/`: a verb (one of the ten), name/fiction, profession gate + XP (gated ⇒ pays, validated two-sided), substrate tags or id, identity scope, Process output, extra costs — **plus a station routing it** (an unroutable action fails validation) |
| **Add an item Form** | One entry in `game/data/forms/forms.json`: `type` (an `EquipmentSlot`), `slots` (each: `requires_tags`, `mass_share` — must sum to 1, `identity_priority` where mass order would mislead), `identity_cap`, `base_reads` (damage/speed/armor from Heft/Bite/Toughness/Give — weapons need a damage read, worn armour an armor read, by test), optional `generation_profile`, `moves`, `name_variants`, `tags`. The forge offers every form wherever `has_assembly` is true |
| **Add an item effect (payload)** | One entry in `game/data/signature_payloads/`: name, `families` (identity + rung), `binding` (a registered modifier key / status / damage / heal / resource / move / moveModifier — **must already resolve in play**, validated), `[lo, hi]` range (factors for multiplicative keys — the validator catches deltas), weight, at most one `floor` per owning identity |
| **Add a Move** | One entry in `game/data/moves/*.json`: namespaced tags, `timing`, `costs`, `requires`, `targeting`, `packets`, `stagger_power`, effect riders. Reachability is validated — grant it from something |
| **Add a Status** | One entry in `game/data/statuses/*.json`: category, stack policy, duration, `magnitude` (basis + coefficient), `while_active` modifiers, hooks. **No C# class** |
| **Add an enemy** | `game/data/actors/<name>.json`: `family`, `role`, `moves`, per-key tweaks, `loot_table`. Reuse an `ai_profile` or add inline rules. Never write a C# class |
| **Add a loot table** | One entry in `game/data/loot_tables/*.json`: `alwaysDrops` / `chanceDrops` / `weightedDraws` / `gold`. Nest a shared table with `tableId` rather than copying entries. Only an item with no `rarity:` tag may declare `rarity`. See `docs/loot.md` |
| **Make something new droppable** | Point its `loot_table` at a shared table that already ships. An enemy often needs no table of its own — its family and role already pay. Elite/boss spoils need only the `elite`/`boss` tag |
| **Add an enemy family / role / AI brain** | One entry in `enemy_families/`, `enemy_roles/`, `ai_profiles/`. Roles are **deltas** and must stay family-agnostic |
| **Add a profession action** | One entry in `game/data/profession_actions/<profession>.json`: profession, level gate, interval, inputs, outputs, bonus outputs (`ItemChance`), XP. Optional: `successChance` (Hunting/Thieving only), `realmKnowledgeGain` (Cartography only) |
| **Add an active opportunity** | A nested entry in an action's `opportunities[]`: unique id, `prompt` (the offer text *is* the decision), `discoveryChance`, `extraIntervalTicks`, `riskWeight`, payoff. It must out-pay its own action — a test checks |
| **Add a training obstacle** | One entry in `game/data/training_obstacles/`: `slot` (one of the five), level, interval, XP, and `bonuses` keyed by `CourseBonusKeys`. An unknown key fails validation |
| **Add a profession** | One entry in `professions/` (id, name, category, primary attributes, a one-line description) plus its action file **plus a station in `stations/` that hosts it** — a profession with no station fails validation. It must both consume another profession's output and produce something something else wants — `ProfessionEcosystemTests` fails a dead end |
| **Add a Hideout station** | One entry in `game/data/stations/stations.json`: id `station.*`, name, description, `professions` (≥1, and no profession may appear twice across the file), optional `verb_actions` and `has_assembly`. Routing only — it cannot change a gate |
| **Move where a bench action is offered** | Edit the stations' `verb_actions` lists. Listing one action at two stations is legal and sometimes right |
| **Add a Base / Prefix / Suffix** | One entry in `classes/`, `prefixes/`, `suffixes/`. Bases must spend exactly the 4.0 growth budget. **A Prefix may never name a Base.** An expressed Suffix needs one expression per channel |
| **Add a technique item** | One entry in `techniques/` naming the move it teaches |
| **Add a realm or location** | One entry in `realms/`. Edges must be symmetric and content refs must resolve — both validated |
| **Add a modifier key** | One entry in `modifier_keys/`: kind, clamps, `scoped_by`, `danger` (which then requires a `max`) |

### Behaviour — code

| I want to… | Start here |
|---|---|
| **Change combat damage calculation** | `core/Combat/HitPipeline.cs` — `Resolve` for the whole-hit stages, `Mitigate` for per-packet. Constants in `CombatTuning`. **Update the golden traces in `tests/Combat/HitPipelineTests.cs`** — they assert the whole trace by design |
| **Change how a stat scales** | Prefer a modifier contribution over a pipeline change. Only attribute scaling is hard-coded (`HitPipeline.ApplyAttributeScaling`) |
| **Change bench behaviour** | `core/Crafting/Identity/IdentityCraftingEngine.cs` — one executor per verb, preview = commit − dice; `IdentityCraftTuning` for numbers. The §6 worked chain is pinned in `tests/Crafting/IdentityVerbEngineTests.cs` |
| **Change forge behaviour** | `core/Crafting/Identity/IdentityEquipmentComposer.Compose` — **one computation, used by both the preview and the mint** (base delivery parity-pinned to the authored Iron Sword) |
| **Change item generation (what mints)** | `core/Crafting/Identity/ItemEffectResolver.cs` (the floor, the scored table, the draws, Signature odds) and `ItemEffectTuning`. The behavior→grants compile lives in `SentenceAssemblers` |
| **Change enemy AI** | Usually `ai_profiles/` data. For the *selection* mechanism: `CombatEncounter.ChooseMove` |
| **Change the action lifecycle** | `CombatEncounter.Commit` / `EnterWindup` / `Execute`, and `CombatTuning` for the windows |
| **Change what a weapon does to combat** | `core/Equipment/EquipmentResolver.cs` — the whole material → combat seam, 105 lines |
| **Change what Realm Knowledge reveals** | `RealmKnowledgeLevels.Required` for the thresholds (pinned by ratio in `DarkForestBalanceTests`, not by value); `core/Presentation/RealmBriefing.cs` for the pre-run reading and `GameRoot.KnowledgeIntel` for the in-run one. **Never add a second threshold table** |
| **Change what mastery buys** | `game/data/mastery/mastery_benefits.json` — it is content. A new *kind* means a `ProfessionBenefitKind` member **plus its consumer** in `ActionResolver`/`ProfessionSystem`, plus a validator rule. `MasteryLeveling` owns points → level |
| **Change cross-profession or global bonuses** | `game/data/synergies/synergies.json` — it is content, and it pays into the same six quantities mastery does. A row with no `source` reads the player's **total** level. Source and target must differ. The seam is `ProfessionBenefits`; nothing downstream needs to know a synergy exists |
| **Change what automated combat does** | `game/data/auto_combat/profiles.json` for the brains (tag-matched rules + weighted stances); `AutoCombatPilot` for *when* it decides. **Never add a damage modifier** — D-07 says the handicap is `reaction_ticks` and nothing else (§10.13b) |
| **Change what the player sees on returning** | `core/Professions/AwayProgress.cs` for what an absence aggregates, `core/Presentation/AwayReadout.cs` for every word of it. The console line and the panel both read the latter |
| **Change how the character levels** | `core/Characters/CharacterLeveling.cs` (curve + what a Realm pays); `GameRoot.AwardCharacterXp` for the sources. **Realm work only** — awarding it anywhere else collapses the layered model, and `ProgressionEcosystemTests` fails |
| **Change what the player takes into a run** | `core/Realms/RunLoadout.cs` for the model, `LoadoutCheck` for the warnings and the starter-kit rule, `GameRoot.EnterPreparedRun` for the hand-off into the run bag. Worn gear is **not** stored here — it lives in `Equipment` (§10.16b) |
| **Change player-facing wording** | `core/Presentation/SemanticFormat.cs`. If you need a new *fact*, add it to the relevant `XReading` first. **Never format in the UI** |
| **Change a number the player feels** | Find the `*Tuning` class: `CombatTuning`, `IdentityCraftTuning`, `ItemEffectTuning`, `ProfessionTuning`, `RealmTuning`, `EquipmentTuning`, `PresentationTuning` |
| **Add a new effect kind** | Define it in `RuleVocabulary`, implement an `IEffectHandler`, register it (combat's live in `CombatEffectHandlers.RegisterCombatHandlers`). **Propagate `invocation.Context`** |
| **Add a new condition kind** | `TriggerRuleEngine.Evaluate`; if it must read world state, extend `IConditionWorld` and `CombatConditionWorld`. Prefer a derived tag over a new kind (D-11). **Never add a class check** (D25) |
| **Add a new game event** | `GameEvents` constant, publish it from the authoritative system, and note it in the docs |
| **Add a content type** | `ContentBundle` property → `ContentLoader.LoadAll` line → `ContentValidator` rules → a failing-content test per rule |
| **Add a validation rule** | A `ValidateX` in `ContentValidator`, called from `Validate`, plus a broken-content test |
| **Persist new state** | A `*Save` DTO property → `SaveMapper.Capture`/`Apply` → bump `SaveData.CurrentSchemaVersion` → document the forward-compatible default |
| **Add a UI surface** | `MainMvpUI`: a `BuildXSection` for construction, a `RefreshX`/`RebuildX` for updates, and a `GameRoot` query for the content. Colour and layout only |
| **Add something to a Hideout station** | A `partial class XPanel : VBoxContainer` in `game/ui/` taking `GameRoot` + the slice it renders and exposing one `Refresh()`, then compose it in `StationPanel`'s constructor. Use `using static ConsoleTheme` for the palette — do not restate colours |
| **Add a command the UI can call** | A method on `GameRoot` that forwards into Core and raises the right change event. If it contains a game rule, it is in the wrong place |

---

# 12. Persistent identifiers — the do-not-rename list

Some names are data, not code. Renaming them silently corrupts saves or breaks content.

### 🚫 Never rename without a migration

| What | Why |
|---|---|
| Every property of `SaveData` and every `*Save` class in `core/Persistence/SaveData.cs` | These **are** the JSON keys in `user://save.json` |
| The four `CharacterBuild` id properties | Positional **and** persisted |
| `EquipmentSlot`, `ItemType` enum **member names** | Serialized as strings in the save **and** the `slot` field of every `equipment/` and `forms/` definition. Adding a member is free (the v9 Armor→Body rename shim retired with D54 — pre-v14 item sections no longer load) |
| `TrainingSlot` enum **member names** | Written as strings into `SaveData.TrainingCourse` |
| Action ids (`action.*`) | Per-action **mastery** is keyed by action id in every save |
| `CourseBonusKeys` **values** | Keys in `training_obstacles/` content, and validated against |
| `ProfessionBenefitKind` **member names** | The `kind` field of every `mastery/` and `synergies/` entry. (The *enum type* was renamed from `MasteryBenefitKind` in Phase 10; the members did not move) |
| `DefensiveStance` enum **member names** | The `stance` field of every `auto_combat/` defence rule |
| `SaveData.CurrentSchemaVersion` semantics | Bump it; never repurpose a version |
| The `emergent.<hash>` / `equip.emergent.i<hash>` scheme, and anything feeding `Fingerprint` or `IdentityFabricationEngine.DerivedDefinitionId` | Changing what is hashed re-identifies every stored archetype and derived definition |
| `ItemEffectCategory`, `Condition`, `Stability` enum **member names** and the sentence vocabulary ids (bare trigger/behavior/payload keys) | Persisted on every minted item (save v13+) and referenced by profiles/forms |

### ⚠️ Rename only with the data, in the same commit

| What | Why |
|---|---|
| Content ids (`material.*`, `identity.*`, `form.*`, `craft.*`, `move.*`, `status.*`, `loot.*`, …) | Referenced across JSON files and by save data (stash stacks are keyed by item id) |
| Loot **context tag** values (`active`, `passive`, `in_realm`, `elite`, `boss`, `source:*`) | Written by the code, read by `when` conditions in `loot_tables/` content — a rename silently stops gating |
| The **values** of `ItemProperties` constants (`"hardness"`, `"mass"`) | Keys in authored equipment definitions |
| Modifier key ids (`combat.damage.mult`, …) | Referenced by payloads, statuses, class components |
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
  `IdentityVerbEngineTests` reproduces the foundation §6 worked chain exactly.
- **Parity pins** — the iron-sword parity test pins the D46 base-delivery calibration through
  the live resolver seam.
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
| Schematics drop and bind to no form (D29.2) — the one progression track nothing reads | Content/feature gap, not structural; `ProgressionEcosystemTests` exempts it by name |

---

# 15. Crafting vocabulary — one name per concept

The identity system's words, held one-per-concept project-wide (CLAUDE.md "Crafting
vocabulary"): **identity** always means *material* identity · a **sentence** is
trigger→behavior→payload · **Signature** is the earned category, never the blanket word ·
material **capacity** vs form **identity_cap** are different axes (D51) · **Condition** and
**Stability** enum words are the player words · rung words are *basic → improved → advanced →
build-changing*, never numerals (D44). The pre-redesign C# vocabulary (Workability,
MaterialStrength, ItemPotential, the reaction/assembly engines) was deleted with its system in
Phase 7 (D54) — if an old doc or commit mentions it, it is history, not the code.
