# DECISIONS.md

Important architectural & gameplay decisions, **why**, and rejected alternatives (so we don't relitigate them).

## Architecture

### D1 — Two projects (Core + Godot), not the docs' 4-layer tree
`core/` (pure .NET, no Godot) + `game/` (Godot) + `tests/`. **Why:** the assembly boundary *enforces* the two hard rules — Domain never references Godot, and Domain is unit-testable headless — better than the literal `Domain/Application/Infrastructure/Godot` folder sketch in `docs/architecture.md §7`. **Rejected:** single Godot assembly with folders (can't enforce no-Godot-in-domain; tests would drag in GodotSharp); separate Application/Infrastructure assemblies (unneeded for MVP — orchestration lives in `GameRoot`, infra in `game/Infrastructure`).

### D2 — `GameRoot` is composition root + application layer
All wiring, orchestration, and UI-facing report formatting live in one autoload. **Why:** fastest path for the MVP; keeps Core clean. **Known cost:** it's ~850 lines and trending toward a god object (the docs warn against this). **Planned:** extract an Application/use-case layer *before* adding many more systems. Not yet done.

### D3 — `net8.0` everywhere; `.slnx` solution
Only the .NET 10 SDK is installed locally, but Godot 4.7's baseline is net8.0 and a net8.0 Godot project can't reference a net10 library — so Core/tests target net8.0 too and rely on build-time reference packs + runtime roll-forward. Solution is `.slnx` (the .NET 10 default). **Rejected/deferrable:** classic `.sln` (regenerate if an older IDE needs it); net10 targeting (Godot may not host it).

### D4 — One shared `TickEngine` for combat + passive gathering
`GameRoot._Process` advances it at 20 ticks/s while "running"; starting a passive action or a fight auto-starts it. **Why:** single deterministic clock, no parallel timers, everything reproducible/testable. Combat telegraphs and passive intervals both schedule onto it.

### D5 — Content is data-driven via `DataStore<T>`, path-agnostic
Core `DataStore<T>` consumes JSON **text**; the Godot `ContentLoader` owns `res://`/`user://` file access. **Why:** keeps Core engine-independent and content loading unit-testable. **Resolved:** cross-reference validation now runs at load time via `core/Content/ContentValidator` (called from `GameRoot._Ready`, fails loudly), not only in tests. It takes the loaded stores and returns a `ContentProblem` list — staying path-agnostic and engine-independent. Scope is content→content refs; character-component refs (rule ids, class ability ids incl. intentionally-unimplemented ones) stay with the `CharacterComposer` path on purpose.

### D6 — Item **instances** over definition-only gear (the pivotal item decision)
Raw materials stay quantity-based stacks; **any equipment, and any generated/processed material whose properties differ from its definition, becomes a unique `ItemInstance`** (id, quality, derived `PropertySet`, provenance, traits). **Why:** crafting is the point of the game — the player must be able to make unique gear and *recursively* craftable generated materials (Iron Ingot + Bloodmoss → Bloodmoss Iron Ingot → …). Retrofitting instances later would be a painful inventory refactor. **Rejected:** definition-only gear (every "Iron Sword" identical; material properties stay cosmetic; can't support emergent crafting).

**⚠️ Partly superseded by D20** — the instance half was right for equipment and wrong for materials.

### D20 — Emergent materials are **stackable runtime definitions**, not instances (supersedes half of D6)
A crafted material's state is quantized, hashed into a canonical **signature**, and registered as a runtime `MaterialDefinition` under that id (`emergent.7f3a91c4`). Identical results **stack**. `ItemInstance` survives for **equipment only**. **Why:** under D6, forty units of the same alloy were forty unique objects — inventory, save, UI and the future codex all break at that scale; and crafting variance would have produced *random stats on one material* instead of a genuinely different material, which is a far worse fit for a discovery game. With signatures, two players who reach the same state get the same material and the same name, so discovery is shareable. **How it stays invisible:** archetypes register into the *same* `DataStore<MaterialDefinition>` as the authored library, so they flow through `Inventory`, lookups, crafting inputs and loot with no special-casing — nothing needs to know whether an input was authored or generated. **Registry lives in the save** (`SaveData` v4) behind `IEmergentRegistry`; it is a deterministic cache, not progress, so it can move to an install-level store later. The codex (what *this character* discovered) stays per-save and is P6. **Rejected:** per-unit instances (D6's original rule — save/UI blowup); a separate parallel store for emergent materials (every lookup site would need to check both). Source: `docs/emergent-item-system.md` §0 Decision 3, §12.

**Known tension, deliberately left as the spec has it:** integrity is *excluded* from the signature (§12.1 lists what is hashed and integrity is not there), so an archetype records the integrity of its first discovery and all units share it. Reaching the same quantized state by a cheaper path would therefore inherit the wrong remaining budget. In practice the paths self-balance — integrity cost is proportional to Δstate, and a gentler process needs more steps to reach the same place — so this is filed rather than fixed. Including integrity in the hash is the fix if it ever bites; the cost is many more near-duplicate stacks.

### D21 — The reaction algebra replaces recipe matching; the old interaction system is a shim
`ReactionEngine` (Core) resolves every craft through one universal pipeline (§8.7). `interaction.barkbound_iron` is **deleted** — that combination now goes through the algebra like any other. `CraftingExperimentSystem` + `interaction.healing_salve` survive **only** because consumables are produced by fabrication (P5c) and no emergent path to one exists yet; delete that whole path when P5c lands. **Why:** hardcoding even one combination reintroduces the recipe table the design exists to avoid (§0 Decision 1), but leaving the game with no way to brew its only consumable would be a regression. `GameRoot` gained `Craft`/`ProjectCraft` as **thin forwards** — every rule lives in `ReactionEngine` — so the deferred Application-layer extraction does not get harder.

### D7 — Crafting is an emergent **property simulation**, not an authored recipe table
Long-term, ingredients carry properties (Hardness/Mass/Affinity/Heat/Toxicity/Growth/…, string-keyed), and universal interaction rules decide outcomes; discovered recipes are *records*, not the source of truth (`docs/crafting.md §17`, `docs/itemization.md`). **Why:** unusual player experimentation should produce legitimate materials/gear without hand-authoring every combination. **Current status:** architecture only — `ItemInstance` + `PropertySet` + `CraftingDerivation` (a trivial additive-merge seam) exist; the reaction rules/thresholds/instability/mutation/catalysts are **deliberately deferred** (user's explicit instruction). Do not build hundreds of reaction rules yet; build the model so they slot in behind `CraftingDerivation` and the crafting matcher.

### D8 — Combat reads neutral `AttackProfile`/`ArmorProfile`, never equipment types
`EquipmentResolver` turns definition + instance properties into these profiles; `CombatEncounter`/`CombatCalculator` consume only them. **Why:** the material→combat rules (derived damage, on-hit effects, resistances) can grow behind one seam without ever touching the encounter, and it avoids a Combat↔Equipment dependency cycle. `CombatCalculator.Resolve` takes `(damageType, baseDamage)` so weapon attacks and enemy abilities share one pipeline.

### D9 — Namespace ≠ type name
The `Inventory` and `Equipment` **classes** live in namespace `Dungeons.Items` (not `Dungeons.Inventory`/`Dungeons.Equipment`). **Why:** a class named identically to its namespace makes `new Inventory()` ambiguous in consumers. Hit twice; codified here.

### D15 — Material properties: flat 0–100 map, category array files
`MaterialDefinition.Properties` is a flat `Dictionary<string,double>` (was a `[{property,value}]` list), matching `EquipmentDefinition` and far more ergonomic for a ~200-item library. Values use a **0–100** scale; only properties a material has are listed (absent = 0). The library lives in `game/data/materials/` as **category array files** (flora/fauna/…), loaded via `DataStore.LoadDocuments` (auto-detects array vs single-object). Load-time validation enforces range + known names (`ItemProperties.All`). **Why:** authoring ergonomics + one consistent property shape + fail-loud on typos. **Known debt (accepted):** material properties (0–100) and equipment base properties (~0–5, drives combat via `EquipmentResolver`) are on different scales; they don't interact yet (crafting yields material instances, not gear), and the resolver seam will be recalibrated when crafted materials reach combat. **Rejected:** keeping the list shape (clunky for 200 items); one-file-per-material (~200 files); unifying the equipment scale now (out of scope, would touch combat tuning). `MaterialProperty` (the old list item type) survives only to report crafting-outcome derived properties.

## Gameplay

### D10 — Gear safe on death (default) + guaranteed starter loadout
Death forfeits only the **unsecured run inventory**; the Stash and equipped gear survive. A weak starter weapon/armor is always equipped so a fresh/broke character can never be bricked. **Why:** avoid early frustration before the game is tuned. **Deferred (not rejected):** a "gear at risk on death" difficulty toggle — architecture should keep it switchable.

### D11 — Extraction risk model
Realm loot (materials + generated instances + drops) is **unsecured** in a per-run inventory until extraction moves it to the Stash. **Why:** the core extraction-game tension ("leave with the loot, or push deeper?"). Loss is quantity/instance-based; equipment loss deferred (D10).

### D12 — Suffixes as rule-breakers, but combat hooks deferred
Two suffixes have live code rules (health-conditional attribute bonuses that affect combat via effective attributes). The combat-specific rule-hooks (e.g. Exploding Kneecaps on crit) are **not** implemented — combat has no rule-hook surface yet. Class abilities (`ability.guard`/`hex_bolt`) are placeholder ids; everyone uses their weapon attack. Mana exists but nothing spends it.

### D13 — MVP scope discipline
Depth before breadth: 3 professions, 2 enemies, 1 realm, single-enemy/single-position combat, one debug UI page. Loot = single guaranteed drop (no tables/rarity/currency). Realm Knowledge is a bare counter (no info-unlocks yet). All intentional for the vertical slice; see ROADMAP for what to expand.

## Persistence

### D14 — Single-slot JSON save, ids not definitions
`user://save.json`, schema versioned (currently v3). Saves ids + runtime values (build, stash stacks+instances, equipment, instance-id counter, professions, knowledge, discoveries), never definitions. **Rejected/deferred:** save migration, multi-slot, mid-run save (load is blocked during a run), RNG/tick persistence.

## Cleanup pass (pre-expansion audit)

### D16 — The JSON-vs-code dividing line
**Code owns *structure and closed vocabularies*; data owns *content instances*.** Concretely: definition **shapes** are C# records; fixed **vocabularies** are enums (`DamageType`, `EquipmentSlot`, `ItemType`, `ItemQuality`, `PropertyRole`, `ProfessionCategory`, `RealmLocationType`, `Resource/Attribute/StatId`) or code-owned registries (`TagFamilies` closed families, `RuleRegistry`); **content instances** (materials, equipment, abilities/moves, actors, professions+actions, crafting/reactions, realms, species/classes/prefixes/suffixes, loot, consumables) stay JSON, loaded once into POCOs. **Why:** the vocabulary is what must not drift (enums/registries prevent typos); the content is what must stay extensible without recompiling. **Rejected:** moving content into code (kills data-driven authoring); making open sets (item/ability/biome ids, property *names*, form/class/part tag values) into enums (kills extensibility) — those stay data, and cross-category safety comes from typed-id wrappers instead (D18).

### D17 — Property names have one source of truth (the JSON registry)
`game/data/properties/*.json` (`PropertyDefinition`) is authoritative for valid property names; `ContentValidator` derives its known-property set from the loaded registry, not from `ItemProperties.All`. The `ItemProperties` constants remain only for direct code references (e.g. `EquipmentResolver`), and a bijection test keeps them in sync with the JSON so drift fails a test. **Why:** P0 briefly had two lists (code + JSON) that could diverge silently. **Rejected:** deleting `ItemProperties` (code needs a few named constants); keeping the code list as the validation authority (JSON-authored properties couldn't validate).

### D18 — Content loading via `ContentBundle` + typed ids for the persisted, positional `CharacterBuild`
All definition stores live in one `ContentBundle`; `ContentLoader.LoadAll` centralizes the folder paths; `ContentValidator.Validate(bundle)` takes the bundle instead of N positional args. Adding a content type is one store + one load line, not a field + path + validator-signature + call-site edit. **Typed ids** were introduced only for `CharacterBuild`'s four ids (`SpeciesId`/`BaseClassId`/`PrefixId`/`SuffixId`) — positional **and** persisted, so a swap is silent save corruption; they serialize as bare strings (converters), so the save format is unchanged. **Rejected:** a full typed-id sweep across every id (`ItemId`/`AbilityId`/…) — real value but too much ceremony for now; revisit if cross-category mixups actually bite. Extracting an Application/use-case layer out of `GameRoot` is deferred (report/formatting split partially done via `ItemFormat`); it pairs with the next expansion.

### D19 — ID naming convention: `type.slug`
Ids are namespaced by type: `material.*`, `equip.*`, `ability.*`, `actor.*`, `profession.*`, `action.*`, `interaction.*`, `discovery.*`, `realm.*`, `species.*`/`class.*`/`prefix.*`/`suffix.*`, and `consumable.*` (renamed from the inconsistent `item.*`). Realm-location ids (`loc.*`) are realm-scoped, not globally unique. Property ids are bare (`hardness`) — they are keys, not entities.
