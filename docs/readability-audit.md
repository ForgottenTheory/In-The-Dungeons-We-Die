# Code Readability Audit — 2026-08-16

> **Scope.** Every `.cs` file in `core/` and `game/` (~150 files, ~35,750 lines including tests).
> Baseline at audit time: `dotnet build` clean (0 warnings), `dotnet test` → 765 passing.
>
> **Standard applied.** *A competent developer should be able to read the code and understand
> what it is doing without constantly jumping between files.* Expressive names over shorthand;
> obvious responsibility; straightforward control flow.
>
> **Companion:** the fixes applied from this audit are listed in §8, and the permanent rule that
> comes out of it is in `CLAUDE.md`.

---

# 1. Verdict

**This codebase is in good shape.** The audit did not find the usual suspects — there is no dead
code, no god-object business logic, no copy-pasted algorithm, no `Utils` dumping ground, and
essentially every non-trivial type carries an XML doc comment that explains *why*, not just what.
Naming at the type level is consistent and domain-accurate (`ReactionEngine`, `HitPipeline`,
`MovesetBuilder`, `ActorResolver`, `CombatantModifiers`), and the `*Resolver` / `*Calculator` /
`*Tuning` families are used consistently enough to be predictable.

Three structural habits are doing most of the heavy lifting and should be protected:

1. **Every tuning number lives in a `*Tuning` class.** Twelve of them. This is why the "magic
   numbers" section below is short.
2. **Comments explain rejected alternatives**, not mechanics. `HitPipeline`'s note on why crit
   is ordered before `increased` is worth more than any name.
3. **Content is data.** Most systems have no per-content branching to be unreadable about.

So the findings below are refinements, not rescue work. They fall into five buckets, in
descending order of real cost to a reader:

| # | Bucket | Findings | Why it costs a reader |
|---|---|---|---|
| A | **Misleading names and stale comments** | 9 | Actively sends you the wrong way |
| B | **Vague names in long-lived scopes** | 21 | Forces you to re-derive meaning line by line |
| C | **Unclear responsibility / near-duplicates** | 5 | You cannot tell which of two things to call |
| D | **Magic numbers and repeated literals** | 4 | You cannot tell whether a number is meaningful |
| E | **Long methods and dense expressions** | 6 | You must hold too much in your head at once |

---

# 2. Bucket A — misleading names and stale comments

*These are the highest-value fixes: each one currently tells a reader something false.*

### A1 — `GameRoot._crafting` points at the **legacy** crafting system
`game/GameRoot.cs:81`

```csharp
private CraftingExperimentSystem _crafting = null!;   // the retired fixed-recipe shim
private IReactionEngine _reactions = null!;           // the ACTUAL crafting engine
```

The field named `_crafting` is the deprecated Healing-Salve shim kept alive by D21; the real
crafting engine is `_reactions`. A reader looking for "how does crafting work" opens the wrong
one. **Fix:** `_crafting` → `_legacyInteractionCrafting`, `_reactions` → `_reactionEngine`.

### A2 — `ApplyStatus`'s doc comment sits on `ReduceWithBarrier`
`core/Combat/CombatEncounter.cs:1442-1448`

Two `<summary>` blocks in one doc comment. The first ("Applies a status through the
controller…") describes `ApplyStatus`, which is 35 lines further down; the visible summary
belongs to `ReduceWithBarrier`. Anyone reading barrier absorption is told it applies statuses.
**Fix:** delete the orphaned block; it duplicates the real one.

### A3 — `MoveEventTags` carries an obsolete first summary
`core/Combat/CombatEncounter.cs:1325-1342`

Same defect. The orphaned first block still claims *"Known simplification: everything is `melee`
because everything currently is. Real delivery tags arrive with moves in E4."* E4 shipped; moves
author their own tags. **Fix:** delete the stale block.

### A4 — `MaterialSummary` has two summaries, one describing the wrong return
`game/GameRoot.cs:519-521`

The first says *"A material's emergent profile … Null if unknown"*; the method returns a
formatted string and never null. **Fix:** delete the stale block.

### A5 — `ActorDefinition` doubled summary
`core/Combat/ActorDefinition.cs:~28` — same pattern; keep the accurate one.

### A6 — `HitPipeline`'s class doc references a type deleted in E4
`core/Combat/HitPipeline.cs:41,49`

> *"…equipment produces `AttackProfile`, not modifier contributions, until E3 wires `ModifierSet`
> in."*

`AttackProfile` was deleted in E4; E3 shipped; those stages now have sources. The `<see
cref="AttackProfile"/>` is a dangling reference in a doc comment. **Fix:** rewrite the paragraph
to describe the pipeline as it is, and keep the (still valuable) note about *why* the ordering is
pinned.

### A7 — `local suffix` collides with the domain's `Suffix`
`core/Combat/CombatEncounter.cs:1208`

```csharp
var suffix = (result.Crit ? " (crit!)" : ...);
```

In this project *Suffix* is a character-identity component. Here it is a log-line fragment.
**Fix:** `logSuffix` → `outcomeNotes`.

### A8 — `gate` means two different things in the same subsystem
`core/Crafting/FabricationEngine.cs:246` uses `gate` for a trait **aperture factor**, while
`ReactionEngine.Gate()` means **request validation**. **Fix:** `gate` → `apertureFactor`.

### A9 — three doc comments cite a deleted document
`core/Characters/ResourcePool.cs:6`, `ResourceType.cs:3`, `core/Combat/DamageType.cs:3`,
`CombatEncounter.cs:83`, `core/Simulation/TickEngine.cs:12` reference `docs/combat-spec.md`,
which was **deleted** (recorded in the GDD's superseded-documents appendix). **Fix:** repoint at
the documents that superseded it (`damage-and-defense.md`, `statuses.md`, `moves.md`).

---

# 3. Bucket B — vague names in long-lived scopes

*Ordered by how long the name stays live. A one-line LINQ lambda called `p` is fine; a variable
that survives 60 lines is not.*

### B1 — `ReactionEngine`'s core pipeline (`core/Crafting/ReactionEngine.cs`)

| Now | Problem | Proposed |
|---|---|---|
| `Run(gate, applyVariance)` | The single most important method in the crafting system is named with the vaguest possible verb | `RunReaction` |
| `RunResult` | ditto | `ReactionRun` |
| `Gate(request)` / `GateResult` | "Gate" as a verb reads as a noun; it is validation + input resolution | `AcceptRequest` / `AcceptedCraft` |
| `qualityNorm` | "Norm" is an unexplained abbreviation of "normalised" | `craftQuality` |
| `after` | Integrity after this step | `integrityAfterStep` |
| `lookup` | The registry's get-or-register result | `registration` |
| `variance` | It is a *magnitude*, not a variance in the statistical sense | `varianceMagnitude` |
| `state` | Survives the whole reagent loop; means the evolving property state | `materialState` |
| `CraftQuality.Norm(...)` | Public API named `Norm` | `CraftQuality.Normalised(...)` |

### B2 — `AffixRoller` (`core/Affixes/AffixRoller.cs`)

| Now | Problem | Proposed |
|---|---|---|
| `Materialise(affix, genome, position)` | British-spelled, and "materialise" says nothing about rolling a value | `RollValue` |
| `position` | Position *in what*? | `positionInTierRange` |
| `var t = Math.Clamp(...)` | Single letter | `clampedPosition` |
| `pick` | It is a weighted roll cursor | `weightedRoll` |
| `WeightedCount(random)` | Count of what? | `RollAffixCount` |
| `x` in `.Where(x => x.Weight …)` | Survives a 10-line chain | `candidate` |

### B3 — `FabricationEngine` (`core/Crafting/FabricationEngine.cs`)

| Now | Problem | Proposed |
|---|---|---|
| `read` / `reads` | A `StatContribution`; "read" as a noun is ambiguous with the verb | `contribution` / `contributions` |
| `read.W` | **A single-letter public property.** Weight | `Weight` (⚠ also a JSON key — see §7) |
| `primary` | Which primary? The heaviest slot's name | `primarySlotName` |
| `expressed` (list of tuples) | Shadows `composed.Expressed`, and the tuple field `Expressed` is a *magnitude* | `weighted` / field → `ExpressedMagnitude` |
| `Signature(form, components, stats)` | A method named as a noun | `ComputeSignature` |
| `Composition` (nested record) | Collides conceptually with `Dungeons.Characters.Composition` | `ComposedItem` |

### B4 — `HitPipeline` (`core/Combat/HitPipeline.cs`)

| Now | Problem | Proposed |
|---|---|---|
| `pen` | Abbreviation | `penetration` |
| `taken` | Reads as a past participle, is a multiplier | `damageTakenMultiplier` |
| `increased` | ditto | `increasedMultiplier` |
| `innate` (in `RollCrit`) | It is the crit chance Luck alone buys | `critChanceFromLuck` |
| `Avoided(hit, log, via, why)` | `why` is a reason string | `reason` |
| `total` (in `Mitigate`) | Which total? The summed lane resistance | `resistanceTotal` |

### B5 — `GameRoot` (`game/GameRoot.cs`)

| Now | Problem | Proposed |
|---|---|---|
| `loc` ×6 | A `RealmLocationDefinition` living across a 40-line switch | `location` |
| `sb` ×8 | StringBuilder; conventional but not expressive | `report` / `lines` |
| `c` (in `CharacterReport`, `CombatReport`) | The character / the combatant | `character` / `combatant` |
| `wd` | A second weapon definition variable in the same method as `weaponDef` | (removed — see C3) |
| `props`, `reqs`, `hp`, `k` | Abbreviations | `properties`, `requirements`, `healthText`, `knowledge` |
| `_professionDefs` / `_actionDefs` / `_moveModifierStore` | Three naming conventions for the same kind of field | `_professionStore`, `_actionStore`, `_moveModifierStore` |
| `_affixRng`, `combatRng` | `rng` | `_affixRandom`, `combatRandom` |
| `guard` (in `_Process`) | An iteration cap | `safetyIterations` |

### B6 — `StatusController` (`core/Combat/StatusController.cs`)

| Now | Problem | Proposed |
|---|---|---|
| `Land(...)` | Vague verb for "attach or refresh the instance" | `AttachOrRefresh` |
| `pools` | A control-**buildup** map; "pool" means `ResourcePool` everywhere else | `buildupByStatus` |
| `added` | Buildup added | `buildupAdded` |
| `list` | The target's active statuses | `activeOnTarget` |
| `why` | Reason string | `reason` |

### B7 — `CombatEncounter` (`core/Combat/CombatEncounter.cs`)

| Now | Problem | Proposed |
|---|---|---|
| `EnterStance(verb, cost, isBlock)` | A boolean that selects behaviour | see C1 |
| `how` | The avoidance verb for narration | `avoidanceVerb` |
| `_rng`, `_bus`, `_pipeline` | `rng` is the only real abbreviation | `_random` |

### B8 — `EquipmentResolver` — `props` → `properties` (×2).

### B9 — `ReactionEngine.CatalystFactor` — the local `0.25` is unnamed (see D3).

---

# 4. Bucket C — unclear responsibility and near-duplicates

### C1 — `EnterStance(string verb, int staminaCost, bool isBlock)`
`core/Combat/CombatEncounter.cs:1099`

A boolean parameter selects between two behaviours *and* two tuning constants, and the call sites
read `EnterStance("dodge", CombatTuning.DodgeStaminaCost, isBlock: false)` — which states the
cost twice, once by name and once by flag. **Fix:** replace the flag with a `DefensiveStance`
enum (`Block` / `Dodge`) and derive the cost and duration from it, so a call site is
`EnterStance(DefensiveStance.Dodge)`.

### C2 — `CombatEncounter.Find(id)` and `CombatEncounter.ById(id)` are the same lookup
`core/Combat/CombatEncounter.cs:1048` and `1474`

Two methods, different names, different visibility, same job (event-id → `Combatant`), written
two different ways. A reader has to compare both to learn there is no difference. **Fix:** delete
the private `ById` and route its one caller through `Find`.

### C3 — `ResolvePlayerMoveset` resolves the weapon's moves **twice**
`game/GameRoot.cs:1151-1194`

`EquipmentResolver.ResolveWeaponMoves(weaponDef, weapon, _moves)` is called once to build grants
and again — via a second lookup into a second local named `wd` — to build the override store.
Two lookups, two locals for the same definition, one loop that could serve both. **Fix:** resolve
once into a local, and build both the grants and the override store from it.

### C4 — `ReactionEngine.Run` mixes orchestration with two inline sub-algorithms
The reagent loop, the trait pass and the potency/lineage/signature finish are three distinct
phases inside one 134-line method. **Fix:** extract the trait pass (`ApplyTraitPass`) so the main
method reads as *loop → variance → traits → tags → potency → identity*. (Deliberately **not**
extracting the reagent loop itself: it is the specification, and splitting it would hide the
order.)

### C5 — `_readyAt` in `TriggerRuleEngine` holds two different key shapes
`core/Rules/TriggerRuleEngine.cs:67` stores both rule cooldowns and per-target ICDs in one
dictionary under differently-built keys. It works and is efficient; it is just not obvious.
**Fix:** rename to `_cooldownReadyTick` and comment the two key shapes at the field.

---

# 5. Bucket D — magic numbers and repeated literals

The `*Tuning` habit means there are only four worth fixing.

| # | Where | Literal | Fix |
|---|---|---|---|
| D1 | `core/Combat/HitPipeline.cs` ×5, `HitLog.cs`, `GameRoot.cs` | `0.0001` as a float-comparison epsilon | `CombatTuning.MultiplierEpsilon` (and use it everywhere) |
| D2 | `core/Affixes/AffixRoller.cs:193` | `0.35 + 0.65 * …` — the potency→position curve | `AffixTuning.MinRollPosition` / `PotencyRollSpan` |
| D3 | `core/Crafting/ReactionEngine.cs:409` | `+ affinity/100 * 0.25` — the catalyst's contribution | `ReactionTuning.CatalystAffinityBonus` |
| D4 | `core/Crafting/Genome.cs:98` | `if (value > 0.5)` — the pressure trace floor | `GenomeCalculator.PressureFloor` |

*Not* flagged: `/ 100.0` conversions from the 0–100 property scale. That scale is documented
everywhere and the divisor is self-evident in context.

---

# 6. Bucket E — long methods and dense expressions

| # | Where | Lines | Verdict |
|---|---|---|---|
| E1 | `ContentValidator.ValidateMoves` | 201 | **Leave.** A flat, sequential list of independent checks with no shared state. Splitting it would add indirection without adding comprehension. Section comments would help |
| E2 | `MainMvpUI.BuildCraftingSection` | 143 | **Leave.** Sequential UI construction, top to bottom, no branching |
| E3 | `ReactionEngine.Run` | 134 | **Extract the trait pass** (see C4) |
| E4 | `GameRoot._Ready` | 114 | **Leave.** It is a construction script; its linear order *is* the dependency order, and it is already sectioned by comments |
| E5 | `HitPipeline.Resolve` | 113 | **Leave.** The order is the specification, and golden tests assert it |
| E6 | `FabricationEngine.Compose` | 111 | **Leave.** Already sectioned by `// ---- §16.3 step N` headers; each section is 8–15 lines |

**Dense expressions worth naming:**

- `GameRoot.EquippedArmorSummary` — a nested ternary inside a `Select` inside a string
  interpolation. Extract the per-lane formatting to a local function.
- `CombatEncounter.ExecuteRiders` — a three-level nested ternary computing the effect target.
  Extract to a small helper.

---

# 7. Persistent identifiers — what this pass will NOT rename

**The rule:** *code symbol renaming is not the same thing as persistent data identifier
renaming.* The following are data. They are excluded from the refactor.

### 🚫 Excluded entirely — renaming breaks existing saves

| What | Why |
|---|---|
| Every property of `SaveData` and every `*Save` class (`core/Persistence/SaveData.cs`) | They **are** the JSON keys in `user://save.json` |
| `CharacterBuild`'s four id properties | Positional *and* persisted |
| `EquipmentSlot` / `ItemType` / `ItemQuality` enum member **names** | Serialized as strings |
| Anything hashed into `MaterialSignature` or the fabrication signature | Changing the hash input re-identifies every stored archetype |

### 🚫 Excluded — renaming breaks content

| What | Why |
|---|---|
| Content ids (`material.*`, `move.*`, `status.*`, `affix.*`, `form.*`, …) | Cross-referenced across JSON *and* keyed in save data |
| The **values** of `ItemProperties` constants (`"hardness"`) | Property names are keys in materials, saved instances and saved archetypes |
| Modifier key ids, damage lane/aspect strings, tag family names and values | Referenced by content and by saved archetype tags |
| `MoveOps` op names, `RuleVocabulary` condition/effect kind strings | Authored in JSON |

### ⚠️ One deliberate content-schema rename

`StatContribution.W` → `Weight`, with the JSON key `"w"` → `"weight"` in
`game/data/forms/forms.json` (3 forms, one file).

This is safe and is being done deliberately: `forms.json` is **shipped content under version
control, not player data**. No save file stores a stat-map contribution — saves store the
*computed* stats. The C# property and the JSON key are changed in the same commit, and the
fabrication parity test pins that the computed output is unchanged.

### 📝 Deferred renames (documented, not done)

| What | Why deferred |
|---|---|
| `MainMvpUI` → e.g. `DeveloperConsole` | The Godot `.tscn` references the script by path and `.uid`. Godot is not on PATH here, so the rename cannot be verified. Do it in the editor |
| `game/data/forms/forms.json` id `form.*` conventions | Fine as-is; noted only because form acquisition (D29) will add fields here |
| `CraftingExperimentSystem` and the whole legacy interaction path | Scheduled for **deletion**, not renaming, when consumable forms land (D21). Renaming it now would be wasted work |

---

# 8. What was actually changed

**No gameplay behaviour was intentionally changed.** The build stayed at **0 warnings** and all
**765 tests** stayed green after every step. 32 files touched.

### Bucket A — misleading names and stale comments (all 9 fixed)

| Fix | Where |
|---|---|
| `_crafting` → `_legacyInteractionCrafting`, `_reactions` → `_reactionEngine` | `GameRoot` |
| Orphaned `ApplyStatus` summary removed from `ReduceWithBarrier`; the real one restored on `ApplyStatus` | `CombatEncounter` |
| Obsolete "everything is melee" summary deleted from `MoveEventTags` | `CombatEncounter` |
| Stale "returns null" summary deleted from `MaterialSummary` | `GameRoot` |
| `ActorDefinition`'s summary moved off `AiRuleSpec` and back onto `ActorDefinition` | `ActorDefinition` |
| Class doc rewritten; the dangling `<see cref="AttackProfile"/>` removed | `HitPipeline` |
| `suffix` → `outcomeNotes` (Suffix is a character component) | `CombatEncounter` |
| `gate` → `apertureFactor` (Gate meant validation elsewhere) | `FabricationEngine` |
| Five `docs/combat-spec.md` references repointed at the docs that superseded it; `ScheduledAction`'s "not modelled yet" note corrected | Characters, Combat, Simulation |
| **Bonus:** the same stale `AttackProfile` reference removed from `CLAUDE.md` rule 6 | `CLAUDE.md` |

### Bucket B — expressive renames

`ReactionEngine` (`Gate`/`GateResult`/`Run`/`RunResult` → `AcceptRequest`/`AcceptedCraft`/
`RunReaction`/`ReactionRun`; `state` → `materialState`; `after` → `integrityAfterStep`;
`variance` → `varianceMagnitude`; `lookup` → `registration`; `qualityNorm` → `craftQuality`
project-wide) · `CraftQuality.Norm` → `Normalised` · `AffixRoller` (`Materialise` → `RollValue`,
`WeightedCount` → `RollAffixCount`, `position` → `positionInTierRange`, `t` →
`clampedPosition`, `pick` → `weightedRoll`, `cut` → `dimensionEnd`, LINQ `d`/`x`/`r` →
`affix`/`candidate`/`rolled`) · `FabricationEngine` (`read`/`reads` → `contribution`/
`contributions`, `primary` → `primarySlotName`, `kept` → `expressed`, `Signature` →
`ComputeSignature`, `Composition` → `ComposedItem`) · `Genome` (`statWeights` →
`weightPerSlotByProperty`, `perSlot` → `weightPerSlot`, `value` → `propertyPressure`) ·
`HitPipeline` (`pen` → `penetration`, `taken` → `damageTakenMultiplier`, `increased` →
`increasedMultiplier`, `innate` → `critChanceFromLuck`, `total` → `resistanceTotal`, `why` →
`reason`, `via` → `avoidedVia`) · `StatusController` (`Land` → `AttachOrRefresh`, `pools` →
`buildupByStatus`, `added` → `buildupAdded`, `list` → `activeOnTarget`, `why` → `reason`,
`gate` → `prerequisiteStatusId`) · `TriggerRuleEngine` (`_readyAt` → `_cooldownReadyTick`,
`icdKey` → `perTargetCooldownKey`) · `GameRoot` (`CurrentBag` → `ActiveInventory`, `FormatBag` →
`FormatInventory`, `_professionDefs`/`_actionDefs` → `_professionStore`/`_actionStore`,
`_affixRng` → `_affixRandom`, `loc` → `location`, `sb` → `report` ×8, `c` →
`character`/`combatant`, `props`/`reqs`/`hp`/`k` spelled out, `guard` → `ticksThisFrame`) ·
`MainMvpUI` (`Repopulate` → `RepopulateMaterialPicker`, `SwapReagents(a, b)` →
`(firstIndex, secondIndex)`, `PanelCol`/`CardCol`/`TextCol`/`Bg` → full `…Color` names, `t` →
`sweep`) · `EquipmentResolver` and `ProfessionSystem`/`ActionResolver` (`props` → `properties`,
`rng`/`_rng` → `random`/`_random`; also in `CombatEncounter` and `HitPipeline`) ·
`ContentValidator` (`pools` → `poolNames`, `why` → `tagProblem`).

### Bucket C — responsibility and duplication (all 5 fixed)

- **`DefensiveStance` enum** replaces `EnterStance(verb, cost, bool isBlock)`; call sites are now
  `EnterStance(DefensiveStance.Dodge)` and the costs/durations are derived, not passed.
- **`CombatEncounter.ById` deleted**; its one caller routes through the public `Find`, which was
  hardened to keep `ById`'s null-player guard so the behaviour is byte-identical.
- **`ResolvePlayerMoveset` resolves the weapon's moves once** into `weaponMoves`, feeding both
  the grants and the override store. The duplicate lookup and the `wd` local are gone.
- **`ReactionEngine.ApplyTraitPass` extracted** (with a `TraitPass` result record), so
  `RunReaction` reads as *reagent loop → variance → traits → tags → potency → identity*. The
  reagent loop itself was deliberately left inline: it is the specification.
- **`_cooldownReadyTick`** renamed and documented with both of its key shapes.

### Bucket D — magic numbers (all 4 promoted)

`CombatTuning.MultiplierEpsilon` (replaces seven inline `0.0001`) ·
`AffixTuning.MinRollPosition` + `PotencyRollSpan` (replace `0.35 + 0.65 × …`) ·
`ReactionTuning.CatalystAffinityBonus` (replaces the inline `0.25`) ·
`GenomeCalculator.PressureFloor` (replaces `> 0.5`) ·
plus `GameRoot.MaxTicksPerFrame` and `FormSlots.AllSlots` (replacing the bare `"*"` in three
files).

### Bucket E — dense expressions

`GameRoot.EquippedArmorSummary`'s nested ternary extracted to a local
`DescribeLaneResistance` function · `CombatEncounter.ExecuteRiders`' three-level target ternary
extracted to `DefaultRiderTarget` · section headers and a four-block doc comment added to
`ContentValidator.ValidateMoves`.

### Persistent identifiers

Zero changes to save keys, content ids, property names, modifier keys, lane/tag strings or
signature inputs — with the one documented exception in §7 (`StatContribution.W` → `Weight`,
JSON `"w"` → `"weight"` in `forms.json`), verified by the fabrication parity test.

---

# 9. What to keep doing

The habits that made this audit short. These are now project rules (`CLAUDE.md`):

1. **Name the tuning constant, always.** Twelve `*Tuning` classes is not too many.
2. **Comment the *why*, especially the rejected alternative.** "Crit is ordered before
   `increased` because otherwise crit builds scale quadratically" is worth more than any name.
3. **One name per concept, project-wide.** *Suffix* is a character component. *Modifier* is the
   player-facing word for an affix. *Pool* is a `ResourcePool`. *Lane* is a resistance lane.
   Reusing one of these for something else is the most expensive kind of unclear naming here,
   because the domain vocabulary is otherwise so consistent.
4. **New behaviour should be content.** The systems that are easiest to read are the ones where
   the answer to "where do I add X?" is a JSON file.
5. **Delete stale doc comments the moment the code moves.** Every Bucket-A finding was a comment
   that used to be true.
