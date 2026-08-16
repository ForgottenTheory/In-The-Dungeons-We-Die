# SYSTEM_INDEX.md

Map of systems → key files → how they connect. Paths are relative to repo root. Core namespaces are `Dungeons.*`; Godot is `Dungeons.Game.*`.

## Layout
```
core/    InTheDungeonsWeDie.Core.csproj   (net8.0, RootNamespace "Dungeons", NO Godot ref)
game/    InTheDungeonsWeDie.csproj         (Godot.NET.Sdk/4.7.1, references Core); project.godot here
tests/   InTheDungeonsWeDie.Core.Tests.csproj (xUnit, references Core only)
docs/    design docs; `effect-foundation.md` is the settled architecture package
game/data/<type>/*.json   all content
InTheDungeonsWeDie.slnx   root solution
```

## Composition root (how everything connects)
- **`game/GameRoot.cs`** — Godot autoload (`[autoload] GameRoot`). Constructs every Core service in `_Ready()`, loads all content in one call via `ContentLoader.LoadAll("res://data") → ContentBundle`, validates it (`ContentValidator.Validate(bundle)`), owns run/combat/equipment state, and exposes commands + query strings + C# events to the UI. This is the single wiring point and the application layer. (~960 lines; extracting an Application/use-case layer is the flagged next step — report formatting partly lifted into `ItemFormat`.)
- **`game/ui/MainMvpUI.cs` + `MainMvpUI.tscn`** — the one debug/test console (main scene). Builds all controls in code with a code-only dark theme (no assets): a persistent **header** (title + tick/sim status + Play/Advance/Save/Load), a **TabContainer** (Character, Equipment, Professions, Crafting, Realm, Combat, Inventory), and an always-visible **event-log panel** on the right (with Clear). Calls `GameRoot` methods and subscribes to its events (`LogEmitted`, `CharacterChanged`, `InventoryChanged`, `RunningChanged`, `DiscoveryChanged`, `CombatChanged`, `RealmChanged`). Reports sit in styled "cards"; key actions are colour-coded (accent/positive/danger). Dynamic control groups (realm, equipment) are rebuilt on their change events. Purely presentation — no gameplay logic.
- **`game/Infrastructure/ContentLoader.cs`** — reads `res://data/**.json`, feeds text to `DataStore<T>`. **`SaveStore.cs`** — `user://save.json` read/write, delegates (de)serialization to Core.

## Simulation — `core/Simulation/`
`TickEngine`, `ScheduledAction`. Deterministic clock; `Schedule(delay, cb)`/`Advance(n)`/`Cancel`/`TickAdvanced`. One instance shared by combat + `PassiveProfessionRunner`; advanced by `GameRoot._Process` at `TicksPerSecond=20`.

## Content — `core/Content/`
`IDefinition`, `DataStore<T>` (load/lookup/dup-id validation, enum+case-insensitive JSON; `LoadDocuments` auto-detects single-object vs array files), `MaterialDefinition` (implements `IItemDefinition`; flat `Dictionary<string,double> Properties` on a 0–100 scale; `family:value` tags), `MaterialProperty` (name→value pair, used only for crafting-outcome reporting), `DuplicateDefinitionException`. `ContentBundle` — carrier holding every definition store (the single registration point for a content type). **Emergent item system P0** (`docs/emergent-item-system.md`): `PropertyDefinition`/`PropertyRole`/`ResistContributor` (property registry, loaded from `game/data/properties/`; the **single source of truth** for valid property names), `ResistanceCalculator` (derives resistance from `resisted_by`, authored `*_resistance` = override), `TagFamilies`/`TagFamily`/`TagCardinality` (the `family:value` tag namespace). `ContentValidator.Validate(ContentBundle)` — cross-reference/well-formedness over the bundle (material/equipment property range + known-name from the property registry; tag-family cardinality; actors/professions/crafting/realm refs; character-component `abilityIds` vs a known-unimplemented allowlist) → `ContentProblem` list; `ContentValidationException`. **Connects:** `GameRoot._Ready` validates the bundle after `ContentLoader.LoadAll`, failing loudly.

## Items / Inventory / Equipment — `core/Items/`, `core/Inventory/`, `core/Equipment/` (all namespace `Dungeons.Items`)
- `PropertySet` (string-keyed, immutable, `Combine`), `ItemProperties` (property-name constants for code refs; the JSON registry is authoritative), `ItemType`/`ItemQuality`, `IItemDefinition`, `ItemInstance`, `InstanceIdSource`, `ItemStack` (the one item+quantity shape, `Quantity` defaults to 1), `ItemChance` (`ItemStack` + drop chance; exposes `.Stack`), `ItemFormat` (pure item-display formatting, shared by UI/codex).
- `Inventory` — stacks (`Add`/`TryRemove`/`Snapshot`) + instances (`AddInstance`/`RemoveInstance`/`Instances`).
- `EquipmentDefinition` (Weapon/Armor stats + base properties), `Equipment` (slot→instance container), `EquipmentResolver` → `Combat.AttackProfile`/`ArmorProfile` (the material→combat seam, currently Mass/Hardness only).
- **Connects:** professions/crafting/combat deposit into `Inventory`; `EquipmentResolver` output is passed by `GameRoot` into `Combatant.FromCharacter`.

## Modifiers / Events / Rules — `core/Modifiers/`, `core/Events/`, `core/Rules/`
The spine the class combinator (and, later, professions and equipment) is built on.
- **`ModifierKeyDefinition`** (`game/data/modifier_keys/`, 51 keys) + **`ModifierSet`** + **`ModifierKeys`** — an open, validated vocabulary of modifier *targets*. Kinds: additive / multiplicative / flag. Clamps live on the key, so the minimum-interval rule is data. Every `ModifierContribution` carries its source. `ModifierKeys.From(StatId)` bridges the legacy attribute enum, so there is one modifier system rather than two.
- **`GameEvent`/`GameEvents`/`GameEventBus`** — 31 events. Uniform shape (kind + source + target + amount + tags + values) so JSON rules can match on them, **plus `ChainId` / `Depth` / `CanTrigger`** for proc safety (E3a). **Synchronous and ordered**; events raised inside a handler queue and drain afterwards. **Combat publishes 14 of these** since E0.
- **`TriggerRule`/`ConditionSpec`/`EffectSpec`/`RuleVocabulary`** + **`TriggerRuleEngine`** — declarative hooks: 11 condition kinds, 12 effect kinds, cooldowns, seeded chance. `IEffectHandler` is registered by the system owning the behaviour; unhandled effects are recorded, not dropped.
  - **E3a additions:** `Effects[]` (one chance roll, N effects) alongside the legacy single `Effect` — **read `rule.Payload`, never `rule.Effect`**. `EffectTarget` selectors. `ProcRules` per rule.
- **`EffectContext`/`ProcRules`/`ProcSafety`** (`core/Rules/EffectContext.cs`) — the recursion model: chain identity, depth budget (2; Anomalous may reach 3), once-per-chain **on by default**, per-target ICD, and a 64-effect fuse. Chain ids are **sequential, not GUIDs**, because the sim must replay from a seed.
  - ⚠ **`IEffectHandler` implementations must propagate `invocation.Context`** onto any event they raise. Forget it and the chain restarts at depth 0, making the entire budget decorative.

**Connects:** Prefix rules, Suffix expressions and Base gauge feeds are all `TriggerRule`s. `ContentValidator.ValidateTriggerRule` checks every one at load. `GameRoot` owns the one `GameEventBus` + `TriggerRuleEngine` and re-attaches the build's hooks on every `RebuildCharacter()`.

## Characters — `core/Characters/`
- `AttributeSet`, `AttributeType`, `ResourcePool`, `ResourceType`, `ResourceCalculator`.
- `Modifiers/` — `StatId`, `ModifierOperation`, `StatModifier`, `ModifierData`, `ModifierPipeline`.
- `Composition/` — `CharacterBuild` (4 typed ids in `ComponentIds.cs`, serialize as bare strings), `CharacterComponentDefinition` (+ Species/Prefix/Suffix/BaseClass), `CharacterComposer` → `CharacterBlueprint`; runtime `Character`.
  - **The class combinator:** `BaseIdentity.cs` (`ExpressionChannel`, `GaugeDefinition`, `GaugeBand`, `GaugeBehaviour`, `AttributeGrowth` — the fixed 4.0/level budget rule); `BuildResolver` → `ResolvedBuild` + `AttachedRule`, with `BuildResolver.Diff` for the Character Lab; `ClassNameFormatter` + `NameFormatDefinition` (9 templated clauses, presentation only).
  - `BaseClassDefinition` carries growth/gauge/channel/engine/weakness; `PrefixDefinition` carries a mechanic + rules + optional gauge; `SuffixDefinition` carries a fantasy, a format, and channel-selected `SuffixExpression`s. **`SuffixExpression.Channel` is the single coupling point to the composition model** — if channels change, that one field changes.
- `Rules/` — `ICharacterRule`, `RuleRegistry`, `CharacterSnapshot`, `UnreasonableConfidenceRule`, `InappropriateOptimismRule`. **Connects:** `Character.EffectiveAttributes` = base + active rule bonuses; read by the player `Combatant`.

## Professions — `core/Professions/`
`ProfessionDefinition`, `ProfessionActionDefinition` (Inputs/Outputs are `ItemStack`, BonusOutputs are `ItemChance`), `ProfessionProgress` (xp/level/mastery), `ProfessionLeveling`, `ProfessionTuning`, `ActionResolver`, `ActionOutcome`, `ProfessionSystem` (single `Execute` path for passive+active; provider-supplied `Inventory`), `PassiveProfessionRunner` (on TickEngine). **Connects:** `GameRoot` gives it `() => CurrentBag` so gathering lands in the Stash or the run inventory.

## Crafting — `core/Crafting/`  (the emergent reaction engine, P1)
The one entry point is **`ReactionEngine : IReactionEngine`** — `Project(CraftRequest) → CraftProjection` (pre-commit, consumes nothing) and `Resolve(CraftRequest) → CraftOutcome`. It runs the whole `docs/emergent-item-system.md` §8.7 pipeline. **There are no recipes.**

- **Algebra (§8):** `ReactionAlgebra.ApplyReagent` (converge → off-channel drift → oppose → prune), `ReactionCoefficients` (§8.1 + the §7.3 medium→property map), `ReactionStepResult`/`PropertyChange` (what moved and *why*), `ReactionTuning`.
- **Meta (§6):** `PotencyCalculator` (weighted mean + `max(input)+8` ceiling), `IntegrityCalculator` (cost, effective instability, variance magnitude, `IntegrityProjection`), `RefinementTuning`, `CraftQuality` (§7.4).
- **Identity (§12):** `MaterialSignature` (quantize → SHA-256 → `emergent.7f3a91c4`; `Canonical()` exposed for debugging), `VariancePerturbation` (seeded), `QuantizationTuning` (**the highest-risk tuning number**), `IEmergentRegistry`/`EmergentRegistry`.
- **Presentation:** `NameGenerator` (§13), `ReactionLog`/`ReactionLogBuilder` (§15.3), `CraftFormat` (pre-commit text; pure, tested — §6.2c makes this wording a rule).
- **Support:** `TagDeriver` (§4.2), `ByproductResolver` (§6.2c).
- **Legacy shim:** `CraftingInteractionDefinition`, `CraftingExperimentSystem`, `ExperimentOutcome`, `CraftingDerivation`, `DiscoverySystem` — superseded; only `interaction.healing_salve` remains, until fabrication (P5c). See DECISIONS D21.

**Connects:** `GameRoot` constructs the engine over the `ContentBundle` + `() => CurrentBag` and exposes `Craft`/`ProjectCraft`/`Processes`/`MaterialsOnHand`/`MaterialSummary` as **thin forwards**; `MainMvpUI`'s Crafting tab drives them. Emergent archetypes register into the *same* `DataStore<MaterialDefinition>` as authored ones (D20), so inventory/lookup/loot need no special-casing. Persisted via `SaveData` v4.

## Combat — `core/Combat/`  (rebuilt by E0–E2; see `docs/effect-foundation.md` §10)

**The damage pipeline (E1).** `Hit` + `Packet` + `DamageLanes` + `DamageAspects` (`Hit.cs`) →
`HitPipeline` → `HitResult` + `HitLog`. A hit is a **list of packets**, each with one
`DamageType` and zero-or-one aspect; `Lane = aspect ?? type`, so **one resistance per packet,
never two**. Armour follows the packet's *delivery type* regardless of aspect. `arcane` has no
lane and is unresistable. **The stage order is the specification** — golden tests in
`tests/Combat/HitPipelineTests.cs` assert the whole trace, not the final number.
`CombatCalculator` is now a thin façade over the pipeline and survives only as the D-18 bridge
(deleted in E4).

**The action lifecycle (E2a).** `ActionPhase` (Telegraph / Windup) + `ActionInFlight`, unified
for both sides. `CombatEncounter.Commit → EnterWindup → Execute`, with
`Interrupt(actor)` cutting an action and **tagging which phase it cut**. `EnemyIntent` is now a
projection over `_inFlight` for the UI.

**Statuses (E2b).** `StatusDefinition` (data; no C# class per ailment) + `StatusInstance` +
`StatusController` — lifetime only: apply, stack, tick, expire, cleanse, plus the **Resolve**
pool gating every control. Content in `game/data/statuses/` (27 definitions: 14 core + 13
authored ids shipped prefixes referenced). A status's `while_active` is a list of modifier
contributions and its hooks are ordinary `EffectSpec`s, which is why 27 cost what 3 would.

**Still here:** `DamageType`, `AbilityDefinition`, `ActorDefinition` (now with `Resistances`,
`Vulnerable`, `Resolve`), `ConsumableDefinition`, `Combatant` (adds `Armour`,
`EffectiveResistance`, `VulnerabilityTo`, `IsPerfectBlocking`, `Resolve`), `CombatTuning`,
`AttackProfile`/`ArmorProfile` (resistances **keyed by lane**, not damage-type name).

**Connects:** runs on the shared `TickEngine`; publishes 14 event kinds to the bus (E0);
`GameRoot` bridges realm combat nodes ↔ encounter and owns the `StatusController`.

## Realms — `core/Realms/`
`RealmLocationDefinition` (type/depth/connections/content refs), `RealmDefinition` (location graph), `RealmRun` (travel/depth/clear + `RunInventory`), `RealmExtraction` (`Secure`/`Forfeit`, moves stacks+instances), `RealmTuning` (Knowledge-per-action constants). **Connects:** `GameRoot` orchestrates node actions via existing combat/gather systems.

## Persistence — `core/Persistence/`
`SaveData` (v3: build, stash stacks+instances, equipment, next-instance-id, professions, knowledge, discoveries) + `ItemInstanceSave`/`ProfessionSave` DTOs, `SaveSerializer` (System.Text.Json), `SaveMapper` (Capture/Apply between live systems ↔ SaveData). **Connects:** `GameRoot.SaveGame/LoadGame` via `SaveStore` (Godot `user://`).

## Data content — `game/data/`
`species/`(3) `classes/`(15 Bases) `prefixes/`(25) `suffixes/`(50; 10 expressed) `professions/`(3) `profession_actions/`(3) `materials/`(~474 defs across 8 category array files incl. byproducts; `family:value` tags) `properties/`(21 `PropertyDefinition`s) `processes/`(7) `byproducts/`(4) `name_grammar/`(44 words) `crafting_interactions/`(1, legacy shim) `modifier_keys/`(51) `name_formats/`(9) `abilities/`(3) `actors/`(2, now with lane resistances + per-type vulnerability + Resolve) `consumables/`(1) `equipment/`(4, resistances **keyed by lane**) `realms/`(1) **`statuses/`(27 — 14 core + 13 previously-dangling authored ids)**. Each folder auto-loads into a `DataStore<T>` in `GameRoot._Ready` (materials via array files, everything else one-object-per-file).

## Tests — `tests/`
Mirror the Core namespaces: `Simulation/`, `Content/` (incl. `ContentValidatorTests` — shipped content passes + a broken-store test per rule), `Characters/`, `Items/`, `Professions/`, `Crafting/`, `Combat/` (`HitPipelineTests` golden traces · `ActionLifecycleTests` phases + interrupts · `StatusTests` taxonomy + Resolve · `CombatEventTests` shipped hooks firing), `Rules/` (`ProcSafetyTests` — the fusion chain terminating), `Realms/`, `Persistence/`, `Integration/` (`FullLoopTests` — the whole loop headless). Content-validation tests (Content/Characters/Professions/Combat/Realms/Items) load real `game/data` JSON via `TestPaths.DataDir`.
