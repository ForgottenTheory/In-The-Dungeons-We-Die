# PROJECT_STATE.md

Snapshot of what actually exists in code. Verify against the repo. (`docs/current-state.md` was deleted — it predated the equipment system; this file supersedes it.)

- **Solution**: `InTheDungeonsWeDie.slnx` → `core/` (Core, net8.0), `game/` (Godot 4.7.1 .NET), `tests/` (xUnit, Core only).
- **Tests**: 580 passing cases. Core-only; no Godot/UI tests.
- **`docs/GDD.md` is the best single overview** of the whole game and supersedes scattered design notes.
- **Cleanup/audit pass done** (pre-expansion): `ContentBundle` + `ContentLoader.LoadAll` centralize loading; `ContentValidator.Validate(bundle)` (property names sourced from the JSON registry, not a code list; validates character-component abilities, equipment property keys, realm consumable rewards); id convention fixed (`consumable.*`); weapon timing unified onto the nested `AbilityTiming`; leaked gameplay moved to Core (`AttackProfile.Unarmed`, `RealmTuning`, `ProfessionTuning.TimingPerformance`); `ItemFormat` extracted; `CharacterBuild` uses typed ids. See DECISIONS D16–D19. (Application-layer extraction from `GameRoot` deferred.)
- **Milestones 1–9 (MVP vertical slice): COMPLETE.** Equipment/item-instance system: phases 1–3 + save persistence complete; UI + content-validation remain.
- Build/verify: `dotnet build InTheDungeonsWeDie.slnx` && `dotnet test`.

Status legend: ✅ functional · 🟡 partial/prototype · 🧱 scaffolded (architecture only) · ⬜ planned/missing.

## Simulation & content plumbing
- ✅ `TickEngine` (deterministic schedule/advance/cancel + events); one shared instance drives combat + passive gathering at 20 ticks/s.
- ✅ `DataStore<T>` (JSON→definitions, duplicate-id fails loudly, path-agnostic). `ContentLoader` (Godot) reads `res://data`.
- ✅ `ContentValidator` (Core): load-time cross-reference validation of the loaded stores (actors→abilities/loot, actions→profession/materials, crafting→materials/consumables/professions, realm nodes→actors/actions/rewards + symmetric edges, equipment slot↔stat block, **material property ranges + tag-family cardinality**). `GameRoot._Ready` runs it and fails loudly (logs + throws `ContentValidationException`). Character-component refs stay validated by `CharacterComposer`.
- ✅ **Emergent item system — P0 done** (`docs/emergent-item-system.md §20`): material tags migrated to `family:value` namespace (origin/comp/form/state/rarity/class/part); `PropertyDefinition` registry with roles (structural/reactive/response/sourcing) as data in `game/data/properties/`; `resonance` property added (no values yet); `ResistanceCalculator` derives resistances from `resisted_by` (authored `*_resistance` values are overrides). **No reaction engine / traits / essence / potency / integrity / fabrication yet** — those are P1–P5.
- ✅ Seeded `IRandomSource`/`SeededRandom`.

## Character identity — the class combinator (COMPLETE, data-driven)
- ✅ **Base + Prefix + Suffix**, composing into **18,750 builds with none authored** (`docs/classes.md`, GDD §3).
  - **15 Bases** (`game/data/classes/bases.json`) — growth weights on a fixed 4.0/level budget, optional gauge, default expression channel, engine + weakness. 8 have gauges, 7 deliberately don't.
  - **25 Prefixes** (`prefixes.json`) — one mechanic each, authored as event hooks. **A Prefix may never name a Base** (validator-enforced); 7 bring a gauge.
  - **50 Suffixes** (`suffixes.json`) — 10 fully expressed with one expression per **Strike/Guard/Surge** channel + a stated drawback; 40 are roster entries with name/format/fantasy only.
  - **9 name formats** (`name_formats.json`) — dynamic grammar (`standard`/`citation`/`investigation`/`warning`/`medical`/`liability`/`bureaucratic`/`consequence`/`notice`). **Presentation only**, verified never to affect mechanics.
- ✅ **`BuildResolver`** — resolves a build into growth, gauges (max 2), attached hooks with provenance, modifiers, and the generated name. `BuildResolver.Diff` powers the Character Lab.
- ✅ **Character Lab tab** — swap any component, live diff + full readout. *Layout bug fixed; needs re-verification in the editor.*
- ⬜ **No moves.** Bases contribute growth/gauges/channels but no abilities — see HANDOFF for the Move-system plan.
- 🟡 **Species** is out of this pass — still 3 thin stat packages against a designed roster of 10.

## Core mechanisms behind it (new, reusable)
- ✅ **Modifier vocabulary** (`core/Modifiers/`) — 51 data-defined keys replacing `StatId` as the modifier *target* surface. Five kinds (additive / multiplicative / flag / **diminishing** / **highest_only**), clamps on the key, contributions carry provenance. `StatId` bridges in, so there is one modifier system.
- ✅ **Scoped contributions** (E3b, D-12) — a contribution may carry one `ModifierScope` over eight closed dimensions, and `Resolve` takes a `ModifierContext` that is never defaulted. A key declaring `scoped_by` **throws** if resolved without that dimension, rather than returning a plausible wrong number. A **context supplies a set per dimension** (a swing is `melee` and `attack` and `light` at once), so matching is membership, not equality.
- ✅ **Game event bus** (`core/Events/`) — 30 events spanning combat, crafting, loot and extraction. Synchronous and ordered (a determinism requirement); handler-raised events queue rather than re-enter.
- ✅ **Declarative trigger rules** (`core/Rules/`) — `event + conditions + effect + cooldown + chance`, interpreted generically. Prefixes, Suffix expressions and gauge feeds are all just lists of these. Effects with no registered handler land in `Unhandled`, so content referencing unbuilt systems is **visibly inert rather than silently missing**.
- ✅ **Combat raises them** (E0). `CombatEncounter` publishes 14 existing event kinds; `GameRoot` owns the bus + `TriggerRuleEngine` and re-attaches the build's hooks on every rebuild.
- ✅ **Effects execute** (E3c / E3c-2). Seven combat handlers — `damage`, `areaDamage`, `heal`, `applyStatus`, `grantResource`, `grantModifier`, `interrupt` — plus `EffectTargetResolver` for E3a's target selectors. `EffectContext` propagates through combat and statuses, so the proc budget bounds real chains. `spawnEntity`, `grantItem`, `reposition` and `revealInfo` still land in `Unhandled` — visibly inert, not missing.
- ✅ **Gauges are live** (E3c). `GaugePool`/`GaugeController` — per-encounter reset, decay with a grace window, reconfigured on a build swap. Every authored `grantResource` names a gauge, so this is what made that effect kind mean anything.
- ✅ **Modifiers are read** (E3c-2). `CombatantModifiers` assembles build statics + status `while_active` + gauge bands + timed `grantModifier` grants into one `ModifierSet` per combatant; `TimedModifiers` expires the grants. The hit pipeline resolves damage/crit/armour/block/damage-taken and the scheduler resolves windup — **which is what makes Chill slow anything**. Before this, all four contributors were authored and inert because nothing in combat ever read a modifier.

## Character & professions
- ✅ Attributes (7), resources (HP/Mana/Stamina, no auto-regen), `ResourceCalculator`.
- ✅ Modifier pipeline (base→add→multiply→clamp).
- ✅ Composition: Species+Prefix+BaseClass+Suffix → `CharacterBlueprint` → runtime `Character`.
- 🟡 Character **rules** (`ICharacterRule`): only 2 live (health-conditional suffix bonuses). Other suffixes/prefixes are tags+modifiers only. Class abilities (`ability.guard`/`hex_bolt`) are **dead ids**; Mana is unused.
- ⬜ No character level, no XP-driven attribute growth, no character-creation UI (build is hardcoded in `GameRoot`, cycled by a debug button, saved/loaded).
- ✅ Professions: Forestry/Herblore/Smithing; passive (`PassiveProfessionRunner` on TickEngine) + active (timing-performance) + XP/level + per-action mastery. ⬜ No Mining (ore is seeded), no offline progress.

## Items, inventory, equipment  (the current focus)
- ✅ Item model: `IItemDefinition`, `MaterialDefinition`, `EquipmentDefinition`, `ItemInstance` (id/base/quality/derived-props/provenance/traits), `InstanceIdSource`, `PropertySet` (string-keyed), `ItemProperties` (Physical/Processing/Reactive/Response constants).
- ✅ Material library: **~470 raw/processed material definitions** on a 0–100 property scale, flat `properties` map, in `game/data/materials/` category array files (flora 108 / fauna 114 / fungal 45 / minerals 84 / environmental 54 / elemental 36 / processed 31). Authored biome-by-biome (as a design lens — **no biome type/field**, just variety), mundane-majority, multi-part creatures/plants, rarity as a tag (common→exceptional). Load-time validated (range + known names + one rarity tag each). This is the ingredient set the future crafting reaction sim will operate on — **no reaction rules / derived combinations authored yet** (by design).
- ✅ `Inventory` holds both stacks (quantity) and unique instances. Stash vs run inventory split; extraction moves both.
- ✅ Equipment: `Equipment` slot container (Weapon/Armor), `EquipmentResolver` → neutral `AttackProfile`/`ArmorProfile`. Starter loadout (Rusty Sword/Tattered Armor) auto-equipped; gear safe on death.
- 🧱 **Property → combat effects**: `EquipmentResolver` maps only Mass→damage/speed and Hardness→armor as an illustrative seam. The rest (Heat/Cold/Charge/Toxicity/Growth/Decay/Arcane → on-hit effects, resistances, status) is NOT built.
- ✅ Save persists stash stacks + instances + equipment + the instance-id counter (SaveData v3).
- ✅ Equipment UI: `MainMvpUI` EQUIPMENT section — per-slot rows (weapon/armor) with resolved stat summaries + Unequip, a Stash list of unequipped instances each with Equip, and a debug "Grant to stash" row. Backed by `GameRoot.EquipFromStash`/`UnequipToStash`/`GrantToStash` + `EquippedWeapon`/`EquippedArmor`/`StashEquipment`/`InstanceLabel`. (Still code-built debug shell, no art.)

## Crafting — emergent reaction engine (P1 complete, Core-side)
- ✅ **`ReactionEngine`** (`core/Crafting/`) resolves every craft through the one universal pipeline (`docs/emergent-item-system.md` §8.7). **No recipes, no per-combination rules.** `CraftRequest → CraftOutcome`; `Project()` returns the pre-commit `CraftProjection`.
- ✅ **`ProcessDefinition`** + 7 mundane starter processes (`game/data/processes/`): Grind · Steep · Distill · Smelt · Quench · Alloy · Forge Infusion. (`Attune` is P3.) Channels/severity/medium/role-weights/gates/tag-effects, heavily validated.
- ✅ **The algebra** (`ReactionAlgebra`): acceptance/release → channel convergence → off-channel drift → opposition/annihilation → floor pruning. Reproduces §19's worked example exactly (heat 7 steeped, heat 35 / hardness 62 forged).
- ✅ **Meta fields**: `PotencyCalculator` (weighted mean + `max(input)+8` ceiling), `IntegrityCalculator` (cost, effective instability, variance magnitude, **`IntegrityProjection` with destruction chance**), generation, destruction-at-0 with **byproducts** (`ByproductDefinition`, `game/data/byproducts/` → Slag/Cinders/Dross/Residue by form tag).
- ✅ **Identity**: `MaterialSignature` (quantize to 5-point buckets → SHA-256 → `emergent.7f3a91c4`), `VariancePerturbation` (seeded), **`IEmergentRegistry`** registering archetypes into the shared material store so they flow through every existing path. **`SaveData` v4** persists them (v3 loads forward-compatibly).
- ✅ **Naming v1** (`NameGenerator` + `game/data/name_grammar/`): `[intensity] [root] [form noun]`, ≤3 words, ladders not tier words, deterministic syllable coinage on collision.
- ✅ **Reaction Log** (`ReactionLog`/`ReactionLogBuilder`): structured + human-readable §15.3 trace; every line states *why*.
- ✅ **`TagDeriver`** (§4.2): process assertion → state thresholds (declared on properties as `grants_tags`) → lineage carry; `part:` never carries. Tag count stays ≈6–9.
- 🟡 **Legacy shim**: `CraftingExperimentSystem` + `interaction.healing_salve` survive only until fabrication (P5c) — `interaction.barkbound_iron` is deleted. See DECISIONS D21.
- ✅ **Crafting tab UI**: process picker (medium/severity/gate + the channel it opens), base picker, **ordered reagent chain with ↑/↓/✕ reordering** (§7.1 — order is legible, not abstract), optional catalyst, a live **pre-commit projection** (expected result, potency, integrity→, cost ± spread, destruction warning or %) and a Craft button that recolours and relabels to "Craft (risky)"/"Craft (destroys!)". After a successful craft the base re-points at the result, so recursion is one click. `CraftFormat` (Core, tested) owns the wording; the client only does colour and layout. **Needs visual verification in the Godot editor.**
- ⬜ Traits (P2), essence/resonance/`Attune` (P3), signature reactions (P4), fabrication → equipment/consumables (P5), codex/assay/rename (P6).

## Combat
- ✅ Tick-driven `CombatEncounter`: enemy self-scheduling telegraph→execute→recovery; player Attack/Block/Dodge/Wait/UseItem. Block/dodge are timed stances (skill test).
- ✅ **Hit pipeline** (E1): `Hit`/`Packet`/`DamageLanes`/`DamageAspects` + `HitPipeline` + `HitLog`. Ordered stages — packets → flat/attribute scaling → crit → armour (diminishing, `ArmourK=1`) → per-lane resistance → enemy vulnerability → block → floor. `CombatCalculator` is a thin façade over it. Perfect Block negates; ordinary block mitigates. Every hit emits a stage-by-stage trace.
- ✅ Player attack is the **equipped weapon's** `AttackProfile` (fallback ability if unarmed). Consumables (Heal) usable in combat, cost tempo.
- 🟡 Single enemy, single position. Enemies: Goblin Raider, Goblin Brute.
- ⬜ No positioning, status effects, interrupts, multi-enemy, class abilities, mana spells, auto-combat, suffix combat rule-hooks. Enemy armor = CON only.

## Realm & extraction
- ✅ `RealmRun` aggregate + `RealmDefinition` location graph; Dark Forest (10 nodes, depths 1–2, tier 1). Travel (adjacency-gated), Descend, combat/gather/event nodes, extract-or-go-deeper.
- ✅ Extraction: secure run inventory (stacks+instances) → Stash; death forfeits it (Stash+gear safe). Realm Knowledge = a raw per-realm counter.
- ⬜ Knowledge unlocks nothing; no affixes, tiers>1, camps, hazards, bosses/elites, other location types, other realms, procedural gen, pre-run loadout selection. Loot = single guaranteed drop per enemy (no tables/rarity/currency).

## Persistence & UI
- ✅ Save/load: single slot `user://save.json`, schema v3. Persists build, stash (stacks+instances), equipment, instance-id counter, professions, realm knowledge, discoveries. ⬜ No migration, no multi-slot, no mid-run save (blocked during a run). RNG/tick not persisted.
- 🟡 One code-built debug/test console (`MainMvpUI`) — dark code-only theme, tabbed navigation (Character/Equipment/Professions/Crafting/Realm/Combat/Inventory), persistent header + always-visible event log. Still no art/audio or production screens. `GameRoot` is ~880 lines (composition root + app glue + report formatting), flagged for an Application-layer extraction.
