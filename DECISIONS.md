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

## Character identity

### D22 — The class combinator: Base + Prefix + Suffix, composed never authored
The roster from `docs/classes.md` was **replaced** with 15 Bases, 25 Prefixes and 50 Suffixes producing 18,750 builds, **none of them hand-written**. Four rules make that tractable and each is enforced by validation and test:
1. **Every Base distributes the same growth budget** (4.0/level); only the shape differs. Otherwise Base choice is a menu where some options are strictly larger, not a trade.
2. **A Prefix may never reference a Base.** Prefixes hook *events*, so a Bastion galvanises by blocking and a Wizard by releasing a hold — emergent, not authored. Breaking this turns 25 mechanics into 375.
3. **An expressed Suffix has exactly one expression per channel** (Strike/Guard/Surge). A partial one looks usable and turns out to be for someone else's build.
4. **Formatting never touches mechanics.** A Suffix's `format` is read by `ClassNameFormatter` and nowhere else.

**Channels are keyed to events, not attribute archetypes.** Might/Finesse/Focus was rejected: it distributed badly (six of fifteen Bases landed in Focus) and left hybrid builds ambiguous. Every build strikes, defends and runs a resource, so event channels are universal.

**Why one Base, not three.** A three-Foundation "NBA 2K" model was explored and dropped: it stacks additively into beige soup, and three simultaneous gauges is unreadable. The 2K feel that *was* kept is the **fixed budget with real opportunity cost**, plus attribute-threshold gating as the badge analogue (designed, not built).

**Rejected:** per-combination authoring; bespoke gauges per Base (blocks compositional prefixes); gauges as a hard requirement (seven Bases are deliberately gaugeless — a bar for everyone flattens the distinctions the roster exists to create). **Save impact:** `CharacterBuild` ids are persisted, so retiring the old roster is a save break; taken deliberately while there was one test save.

### D23 — Modifier vocabulary is data; the event bus is the extension surface
`StatId`'s closed 10-value enum could not name the things a progression game modifies — action intervals, preservation, yield, typed resistance, extraction bonuses. It is replaced *as the modifier target vocabulary* by a data-defined `ModifierKeyDefinition` registry (51 keys); `StatId` survives as the attribute enum and bridges in, so there is **one** modifier system. Clamps live on the key, which makes the minimum-interval rule data rather than a scattered guard. Contributions carry **provenance** so "why is this number what it is?" is answerable.

`ICharacterRule` (attribute bonuses only) could not express a single documented suffix, so behaviour hooks moved to a **typed game event bus** (30 events, `architecture.md` §14's vocabulary) plus declarative `TriggerRule`s. **Why synchronous and ordered:** an async or queued bus would make combat outcomes depend on scheduling, and the simulation must replay from a seed. **Why unhandled effects are recorded rather than dropped:** content routinely references systems that don't exist yet (statuses, summons, repositioning), and it must be **visibly inert rather than silently missing**.

### D19 — ID naming convention: `type.slug`
Ids are namespaced by type: `material.*`, `equip.*`, `ability.*`, `actor.*`, `profession.*`, `action.*`, `interaction.*`, `discovery.*`, `realm.*`, `species.*`/`class.*`/`prefix.*`/`suffix.*`, `consumable.*` (renamed from the inconsistent `item.*`), and `technique.*` (M2′ learnable technique items). Realm-location ids (`loc.*`) are realm-scoped, not globally unique. Property ids are bare (`hardness`) — they are keys, not entities.

### D25 — A Base is a growth archetype plus a starting kit — never a license
*(Adopted 2026-08-16 after a full design audit; D24 is deliberately skipped so this can never be confused with the effect-foundation package's D-24.)*

**The model:** Base owns its growth weights (the 4.0 budget), a default expression channel (a *selector* for Suffix expressions, never a permission), and optionally a **starting** engine (gauge + hooks) and starting moves drawn from the shared move library. **Nothing is Base-exclusive.** Gauges, moves and mechanics are universal definitions any layer may grant — equipment, Prefix, Species, learned specialization, affix. Effectiveness comes from attributes, resources and modifiers; soft specialization, not hard permission. The interesting question is *"how well can this build make Fireball work?"*, never *"is this Base allowed to cast Fireball?"*

**Standing rule (enforce forever):** move requirements are physical/conditional (`equippedTag`, costs, statuses) — **a class-check condition kind may never be added to the rule vocabulary.** The soft gates already exist and suffice: `MaxMana = 10 + INT×5 + WIS×3`, INT scales Magic packets, STR scales physical, and scoped modifiers let gear compensate weak natural attributes.

**Why:** the audit found the architecture never had class permissions — movesets compose from eight sources and nothing checks identity. The drift was in the *content plan*: the "13 Base signature moves" milestone would have authored 13 class walls into a game whose every other system is a permission-free total function. Universal move content also serves all 18,750 builds instead of 1/15th of them, and E5's "engineered caster Bastion" payoff needs the walls absent.

**Why the hybrid and not the pure form (rejected):** Base-as-pure-stats was explicitly rejected. Seven attributes under an equal budget yield ~6–8 feelable distributions, not 15 identities — the roster collapses; and the Prefix pitch ("a Juggernaut galvanises by swinging, a Wizard by releasing a hold — five feels, one design") requires Bases to *do* mechanically different things. Engines stay on Base as **starting kits**. Also rejected: the status-quo signature-move plan (see above).

**Per-engine dispositions:** the 8 built gauges (Momentum, Force, Held Spell, Intensity, Guard, Charges, Debt, Threat) remain Base starting kits, explicitly grantable by other layers later (flagship: a tower shield granting Guard). Held Spell/Intensity/Songs are ultimately *channel-move* mechanics with the gauge as tracker. **Form (Druid) is a Move mechanic** — E4's `replaces` grants are the machinery. Thralls/openings/ammo/deployables are unbuilt systems; author universal, Base = default access. **Fighter's engine ("moveset from the weapon") was universalized for everyone in E4 — Fighter needs a new identity hook (NEEDS DESIGN).** Nothing is removed.

**Costs accepted:** a move-acquisition layer becomes mandatory (technique items → learned list, v1); the max-two-gauges cap moves from "structurally impossible" to a composition-time rule when a third grantor type appears; casting-speed attribute scaling doesn't exist yet, so "slowly and inefficiently" is currently only "weakly and expensively" — a flagged follow-up decision, not invented.

### D26 — Enemy identity is composed from Family + Role + Actor layers
*(Adopted 2026-08-16 with M2′c; user-directed. The mandate: future enemies are primarily data/configuration, never bespoke C#.)*

**The model:** `EnemyFamilyDefinition` (physiology — baseline attributes/resources, lane resistances, Resolve; never behaviour), `CombatRoleDefinition` (combat archetype — attribute/resource **deltas** over any family, armour, the armoured-physique vulnerability pair, a default AI brain; family-agnostic on purpose), `ActorDefinition` (identity — family + role refs, moveset, final tweaks/overrides, loot). `ActorResolver` folds the three with one merge rule set: baselines + deltas for attributes/resources; per-key later-layer-wins for resistances/vulnerabilities/armour/Resolve; tags union; AI = referenced profile's rules + inline extras. **A future Elite/Realm/depth variant is one more delta through the same fold**, never a duplicated definition.

**AI brains are shared content** (`AiProfileDefinition`): weighted rules over the existing shared condition vocabulary; a rule matches a move by id **or by tag** (`moveTag` — "the big telegraphed hit, whatever it is on this body"), which is what lets an Undead Brute and a Goblin Brute share behaviour without sharing biology. `avoid_repeat_weight` reshapes weights deterministically. AI chooses intent only; the tick lifecycle resolves timing, unchanged.

**Why:** the pre-framework path already had lane resistances, D-02 vulnerabilities, weighted AI and shared movesets — but every actor was a standalone blob, AI rules were id-bound (unshareable), and `FromActor` hardcoded armour to 0 (enemy armour was structurally unusable — found and fixed here). The framework is composition over those existing systems; no parallel engines were built.

**Proved by:** three shipped goblins (~8 data lines each). The Hexer is the acceptance test — ranged/spell/status behaviour from pure configuration over universal library moves. **Constraints:** no class/enemy-name branches; no `equippedTag` moves granted to enemies (validated — they carry no equipment); vulnerability stays keyed by damage type in D-02's [0.50, 1.50]; the lanes stay the established eight. **Rejected:** per-enemy bespoke classes; duplicated full definitions for variants; a separate resistance stat per physical damage type (the D-02 collapse stands).
