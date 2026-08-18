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

## Itemization & the loot loop

### D28 — Gear comes from the bench; realms drop inputs
*(Adopted 2026-08-16, first "how it plays" design session. D27 is deliberately skipped so this can never be confused with the effect-foundation package's D-27.)*

**The model:** fabrication is the **primary** source of equipment; realm loot is predominantly **inputs** — anatomy materials (GDD §12.4 ecology), salvage, rare property-profile materials, essence-bearing parts, technique items, catalysts. The loop statement to preserve: **extraction converts risk into materials; fabrication converts materials into permanence.** Unsecured loot is inputs; the forged answer to them is safe by default (D10) — that asymmetry is what makes the risk model legible.

**Four sub-rulings:**
1. **Relic materials are the chase-item design.** Boss/elite chase drops are *materials* with impossible property profiles, pre-attuned essence, or traits nothing else can birth — uniques as inputs. They feed the genome/affix machinery (E5) instead of bypassing it. Content is post-slice.
2. **Rare authored unique gear exists** — the one sanctioned exception, fenced so it can never undermine pillar 1: a unique is a **rule-breaker with a drawback, never a generically better stat-stick**; it is **sealed** (no genome, no affix rolls, no operations, no Overreach — what drops is what it is); it sits **below relic materials in drop frequency** (the rarest class); and its end-of-life is **Fracture** (E7) into components including a relic-grade material — so even the exception terminates at the bench. *(The sealed/Fracture fencing is the session's proposal riding on the user's "primary + rare uniques" call; relax it deliberately, never by drift.)*
3. **Enemy-wielded equipment drops as salvage materials**, never as equippable gear — the Brute's crude blade arrives as scrap metal and rawhide. At M6 scale salvage maps onto existing materials; bespoke salvage materials can come later.
4. **Consumables are crafted-primary** (Cooking/Alchemy); found consumables are rare and situational, same logic.

**Consequences:** M6 loot tables (the three goblins) carry anatomy + salvage + technique items — no finished-gear entries. Authored basic equipment survives only as the D10 starter floor. Future vendors trade materials, techniques and services, never competitive gear. Item valuation stays genome-computed; uniques carry authored value.

**Why:** the design's centre of gravity already pointed here — E5 affixes hang entirely off fabricated genomes, §12.4 bans "Enemy Loot", valuation is defined in crafted-item terms, and every found sword worth equipping is an argument against visiting the bench. Bench-primacy also gives the Fighter ("only as good as their gear") and the Artificer ("consumes crafted gear more than anyone") real teeth.

**Rejected:** strict bench-only (the session recommendation; the user kept the unique crack open deliberately — the fencing above is the price of the crack); found-gear-as-salvage-only as the chase design (shredding loot you can't use is a weaker jackpot than a great material dropping directly).

### D29 — The crafting arc: affixes always roll, forms are acquired, essence is the realm's export
*(Adopted 2026-08-16, same session as D28. The full arc these rules pace: `docs/how-it-plays.md` ch. 1.)*

Three pacing rules for how the crafting layers enter a playthrough:

1. **Every fabricated item rolls its affixes from the very first craft.** There is no "affixes unlock later" mode switch — the total-function philosophy extends to the modifier layer, and pacing is emergent: weak early genomes roll 0–1 minor modifiers. **Assay gates legibility, never capability** — before the player can Assay, a rolled modifier renders as an unreadable mark (the standing advertisement for the knowledge layer). Assay is one skill action with two surfaces: material proximity hints (`emergent-item-system.md` §15.4) and the fabricated-item Genome Readout (`affixes.md` §2.3), hint depth scaling by profession level. **Rejected:** a threshold that switches the modifier layer on (adds a mode to a total-function system; makes early items retroactively "wrong").

2. **Forms are acquired, not free.** A starter set is always known (the shipped Longsword/Buckler/Vest are the natural candidates); most forms sit on profession-level ladders (Melvor-consistent); a few exotic forms arrive as **schematics — a knowledge loot class symmetric with techniques** (techniques teach moves, schematics teach forms). D28-consistent: knowledge is an input, not gear. Supersedes the current ungated `forms.json`; needs an acquisition field, a persisted known-forms list and a validator rule when it lands (natural home: M6, alongside loot tables, on the learned-list precedent). **Rejected:** all-free forever (removes forms as a progression carrot); ladder-only (realm loot loses its knowledge class).

3. **Essence is the realm's export — professions may only ever yield trace amounts.** Hideout professions can produce essence-bearing material as a *rare outcome*, but **"trace profession essence must never compete economically with Realm extraction"** (user's phrasing — a standing authoring/tuning constraint, not a numbers suggestion). The first meaningful essence craft is therefore a post-extraction milestone by construction; extraction keeps its practical monopoly on the supernatural tier. **Consequence:** the 38 essence-authored materials need a source audit against this rule (which are realm-gathered vs profession-produced), filed for the C2c/M6 window.

## Presentation

### D30 — The three languages: presentation is a one-way semantic layer
*(Adopted 2026-08-16 from a user design directive; full specification in `docs/presentation-architecture.md` — that document is source of truth.)*

**The rule:** simulation language (0–100 properties, rates, severities, coefficients) → player crafting language (icon + qualitative tier + intensity + direction + context) → gameplay/item language (damage, Armour, Crit, Thorns, statuses, triggers, Move modification). Raw simulation values never appear on normal play surfaces — Advanced/Assay/labs only. The semantic layer lives in Core (`Dungeons.Presentation`), reads simulation state through one seam, never writes back, and is deterministic and unit-tested. Display tiers never touch identity quantization (`QuantizationTuning` unread by presentation, forever). Items speak gameplay language; material properties are causes, shown as influence. **A player-facing modifier ships only when its mechanic resolves in play** — D23's visibly-inert rule covers internal content, never player-offered content. Display metadata (glyphs, glosses) is data on `PropertyDefinition`, never code switches.

**Why:** "Charge 72" is simulation data, not a reward — the game was presenting causes as payoffs. Complexity belongs underneath; clarity belongs in the player's hands. The audit (`presentation-architecture.md` §1) found the algebra already computes every fact the player language needs (typed `PropertyChange` kinds, integrity projections, trait conditions, `stat_map`/apertures) — so this is a translation architecture, not a simulation change.

**Consequences:** slices R0–R4 precede the C2c playtest (moved, user call 2026-08-16); R4 absorbs E5's front half (genome/eligibility/innates/rolling + representative affix families, each paired with its combat mechanic + the modifier-key lane-alignment pass — which is also D-07's natural execution moment). `ItemFormat.InstanceLabel` retires from player surfaces (labs/debug keep it). **Rejected:** icons-as-numbers ("⚡⚡⚡⚡ is the same problem wearing a hat" — the directive's phrase); a second player-facing simulation (the semantic layer may only translate, never recompute); gating the grammar behind knowledge (D29: Assay deepens precision, never switches features on).

## Loot

### D31 — One loot table shape, composed rather than duplicated
*(Adopted 2026-08-17, the M6 loot pass. Full system doc: `docs/loot.md`. Executes D28's input-only rule and D29.3's essence-is-the-realm's-export rule.)*

**The shape.** One `LootTableDefinition` serves every source in the game — enemies, gathering nodes, event chests, profession actions. Three drop rules as **separate named lists** (`alwaysDrops` / `chanceDrops` / `weightedDraws`), an entry that sets **exactly one** of `itemId` / `tableId` / `dropsNothing`, quantity ranges, a small `when` condition (depth + context tags), and gold. **Rejected:** one list with a `kind` discriminator (a table stops being readable at a glance and starts needing decoding); a bespoke reward type per source (four places to fix the same bug).

**Six sub-rulings, each of which is a fence rather than a feature:**

1. **Composition over duplication, at both levels.** Entries nest tables, so the shared library (creature remains, salvage, reagents, catalysts, knowledge, techniques, essence) is authored once. And **enemy loot composes family + role + actor** through the existing D26 fold — accumulating, unlike armour or Resolve, because what a body is made of and what it carries are different claims. **Consequence:** a creature that does not exist yet is made lootable by one line of JSON. That was the explicit brief. **Rejected:** loot as an override layer (a goblin brute would have to restate goblin anatomy).

2. **Rarity is read, never authored twice.** A dropped material's own `rarity:` tag decides; only items with no tag (techniques, schematics) may declare `rarity` on the entry, and the reverse is a validation error. **Why:** two sources of truth for rarity is the "one name per concept" mistake in its most expensive form — the tag drifts from the table and nobody can tell which is lying. **Rejected:** a rarity per entry (lets the same material be rare in one table and common in another, which is a bug that looks like design).

3. **Active play beats passive play structurally, not numerically.** A gathering table's second draw is gated `requiresTags: ["active"]`; the passive path cannot reach it **at any rate**. Same trick as the profession pass's opportunities, and for the same reason: it makes "fewer rare outcomes when idle" a fact about the code rather than a tuning number nobody remembers to protect. **Rejected:** a probability multiplier on the passive path.

4. **The condition vocabulary stays tiny, and everything else is a tag.** `LootContext` carries depth, tier and a tag bag; the code guarantees `active`/`passive`/`in_realm` and passes through realm tags, the enemy's identity tags and each rolled table's own tags. **Consequence:** elite/boss support needed no code — `loot.shared.rank_spoils` is nested by every family table and fires on the `elite`/`boss` tag, which comes from the actor's identity tags. Authoring the first elite is a tag. **Rejected:** a field per question (an ever-growing condition type); reusing the rule engine's `ConditionSpec` (drags the event/effect vocabulary into content that has no events).

5. **Beast Lore/Hunting influence enters as a tag, not a multiplier.** When those professions come to affect what anatomy is recovered, they add a context tag that unlocks a richer nested table — "what" and "how much" through one mechanism, with every number staying in content. **Rejected:** a quantity/rarity multiplier on `LootContext` (a second scaling model to balance, and plumbing that would sit unread until it did).

6. **Gold lives on `Inventory`.** Not a separate purse. **Why:** it makes coin obey the extraction risk model for free — unsecured in a Realm, lost on death, secured by extraction, saved with the Stash — with no second code path to keep in step. Save **v8**; a v7 save loads with none, no migration. Coin is a Realm export like essence: enemy/node/chest tables pay it, profession drop tables do not (tested). **Rejected:** a `long Gold` on `GameRoot` (would have made coin silently safe, which is exactly the wrong default for an extraction game); gold as a material (it would become a crafting reagent).

**Why a drop table is not just more `bonusOutputs`.** Both exist on a profession action and mean different things: a bonus output is *more of the same work* and scales with mastery and active performance — a progression lever; a drop table is *something else entirely*, does not scale, and expresses weights, ranges, nesting and conditions. Keeping both is one concept each rather than one concept overloaded.

**Consequences:** `ActorDefinition.LootItemId` and `RealmLocationDefinition.RewardItemId`/`RewardQuantity` are deleted (both were M5 placeholders). The Dark Forest grew five nodes so the tables have somewhere to live. `game/data/loot_tables/` ships 34 tables and **zero new materials** — the 559-material library already had the whole ecology, which is the strongest evidence the profession pass got the material set right.

### D32 — Seven slots, and the body slot is called Body
*(Adopted 2026-08-17, the Phase 4 equipment-breadth pass. Forms table: `docs/game-overview.md` §9.)*

**The expansion:** `EquipmentSlot` grows from `Weapon`/`Armor` to **Weapon · Offhand · Head · Body · Hands · Feet · Trinket**, and `forms.json` from three forms to **eight**. Fabrication itself is untouched — slots, apertures, stat maps, dormancy, projection, genome and modifier rolls all run exactly as before. This is breadth over an existing engine, not a redesign.

**Four sub-rulings:**

1. **`Armor` → `Body`, with a real migration.** The torso slot's name stopped being true the moment a helm existed — a helm is armour too. Slot names are save keys and content fields, so this is the project's **first save migration** (v9): `SaveMapper.TryReadSlot` maps the legacy string, for both worn equipment and fabricated archetypes, and `EquipmentSlots.LegacyBodySlotName` is the only place the old name survives. **Why pay for it:** without it a v8 save silently drops whatever the player was wearing, and with it the slot vocabulary is coherent forever. **Rejected:** keeping `Armor` as the body slot (one slot named after its category while six are named after body locations — the "one name per concept" mistake, permanent, in the vocabulary a reader meets first).

2. **Armour is the sum of the loadout, and coverage is authored.** `EquipmentResolver.ResolveWornArmor` adds armour and per-lane resistance across every worn piece, raw and uncapped (the cap stays in the pipeline, D-05a). How much a piece contributes is **the weight its own `stat_map` gives hardness** — a vest reads harder than gauntlets — not a per-slot multiplier in code. **Rejected:** a `CoverageBySlot` table (a second balance surface in C#, invisible from the form you are actually editing).

3. **Armour-bearing is stated, not inferred.** `EquipmentSlots.ArmorBearing` names the five slots that mitigate. Deriving it as "not a weapon" would have quietly turned every trinket into a breastplate the moment the Trinket slot existed.

4. **A form must read something no other form reads.** Enforced by test, not convention. The Focus is the extreme case and the reason the rule is worth stating: it is the only form whose `stat_map` reads `resonance`, which is what gives ley crystal, runes and mana prisms anywhere to be excellent. Delete that one read and every resonant material in the game becomes decoration. **Corollary:** the Buckler and the Longsword read the same two properties and that is fine — a form is distinguished by what it reads *or* how it is built, and one is a single component while the other is three.

**Four new validation rules**, each catching a form that loads cleanly and is still broken: mass shares must sum to 1; every slot gate must be satisfiable by some shipped material; a weapon must grant moves (since E4 a weapon *is* its moves); and a form must carry the tag its modifier pool gates on (`weapon`/`armor`/`shield`) or it rolls nothing and looks merely unlucky.

**The trinket needed no new affix content.** Nine shipped modifier families carry no `forms_any` gate at all — resources, regeneration, effort — and those are exactly a focus's identity. Widening existing affixes was available and declined: the pipeline was to be preserved, and it turned out to already say the right thing.

**Consequences:** a full head-to-foot loadout is fabricable and pinned by test; five armour pieces instead of one means total mitigation rises sharply, which is a **balance** question and stays with the parked backlog. `ItemType` still reports a trinket as `Armor` for inventory routing — whether a piece *mitigates* is `EquipmentSlots.GrantsArmor`'s question, never `ItemType`'s.

### D33 — Two ring positions, one ring form
*(Adopted 2026-08-17, extending D32. Forms table: `docs/game-overview.md` §9.)*

**The expansion:** `EquipmentSlot` grows to nine — D32's seven plus **`Ring1` and `Ring2`** — and `forms.json` gains a ninth form, the Ring. Fabrication is again untouched.

**Appending slots is free, and that is a fact about the format rather than luck.** Slots persist **by name** (`SaveMapper` writes `slot.ToString()`), so a save written before the rings existed carries no ring keys and loads as a character wearing no rings — which is what a character who has never owned one is. **No migration, no schema bump.** Contrast D32, where a *rename* cost the project its first migration. Appending and renaming are not the same operation and should never be priced the same.

**Four sub-rulings:**

1. **`Ring1`/`Ring2`, not `RingLeft`/`RingRight`.** The user's call. Numeric names are honest that the two positions are interchangeable; left/right would imply a handedness that means nothing mechanically and would invite someone to give it meaning later. **Cost accepted:** this is the one place the slot vocabulary stops reading like the body-location prose D32 established.

2. **One form fills both positions.** A definition must name one slot, so every ring names `Ring1`; `EquipmentSlots.InterchangeablePositions` states that rings may occupy either, and `Equipment.EquipInFirstFreePosition` routes every equip path through it. Without this the second ring displaces the first and the player owns a slot they can never fill. **Rejected:** authoring a second near-identical ring form whose `type` is `Ring2` — content duplication to work around a code gap, and exactly the "do not author material-specific gear" mistake in a different costume.

3. **A third ring always displaces `Ring1`.** Stated, not left to dictionary order, so eviction is predictable rather than whichever position happened to enumerate first.

4. **The Ring reads `conductivity` and `affinity`.** Before it, **no form read either** — which meant the most conductive metals in the library (silver, copper, electrum) were strictly worse swords and nothing else. This is the same move the Focus made for resonance: a property with nowhere to be excellent is a property the player has no reason to care about. **Deliberately not resonance** — a ring that read resonance would be a small focus, and the two would compete for the same materials instead of wanting different ones. Pinned by test: the Ring is the sole reader of both, as the Focus is of resonance.

**Consequences:** rings are **not** armour-bearing (the trinket ruling, D32 §3, applies unchanged) so nine slots still means five that mitigate. `EveryShippedFormCoversASlotAndTheSlotsAreAllCovered` now expands declared types through `InterchangeablePositions` — comparing declared types alone would demand a duplicate ring form to satisfy a slot that is already reachable. Like the Focus, the Ring needed **no new affix content**: it draws the same nine ungated modifier families.

**D33 addendum — slot names are presentation too.** `Ring1` is a good save key and unacceptable as something the player reads, so D30's one-way rule was extended to cover slots: `Presentation/EquipmentSlotNames.cs` is now the only path from an `EquipmentSlot` to text. It draws a distinction the UI had been eliding — **`CategoryOf` is what kind of place a piece is worn** ("ring") and **`PositionOf` is which one** ("Ring I" / "Ring II"). An item card gets the category, because a ring in the stash has not taken a position yet and every ring definition declares `Ring1` regardless of where it ends up — "Ring I" on the card would be wrong for whichever one goes on the other hand. The character sheet gets the position, because two rings in a list have to be tellable apart. **Roman numerals, not left/right**, for the same reason the enum avoided handedness. A test asserts no slot reaches the player with a digit in its name, over every slot rather than the two that exist today — the fallback would pass a future `Ring3` straight through.

### D34 — Ten weapon archetypes, and ~180 weapon names that are names
*(Adopted 2026-08-17. Forms table: `docs/game-overview.md` §9.)*

**The problem.** A ~180-entry weapon-type list cannot become 180 forms. `NoTwoFormsAreTheSameForm` rejects duplicate-shaped blueprints, and it is right to: an Arming Sword, a Broadsword and a Bastard Sword read identically through a stat map. They are one weapon with three names.

**The split.** `forms.json` grows to **17 forms, ten of them weapons** — Longsword, Warspear, Dagger, Greatsword, Battle Axe, Maul, Longbow, Crossbow, Quarterstaff, Flail. Each reads something the others do not, or is built differently. The remaining ~120 names become `name_variants` on whichever archetype they *are*.

**A variant is cosmetic by construction.** Nothing reads `NameVariants` — not the stat map, not `ItemPotential`, not modifier eligibility — so a variant can never quietly become a mechanical difference. The pick is derived from the item's **signature**, not the RNG, which buys two things: the pre-commit projection promises the noun the bench will actually mint (§6.2c fairness), and identical materials always read the same way, so two identical blades are never a Falchion and a Scimitar. **Rejected:** rolling the variant, which would have broken projection; and authoring one form per name, which would have discarded the constraint that makes material choice matter.

**Two tests guard the seam:** no noun may be claimed by two forms (a name's one job is telling the player what they are holding), and no form may repeat a noun.

**The Longbow is the point of the whole pass.** It reads flexibility off the limb at 1.10 and hardness at 0.20 — the only weapon where every instinct the Longsword teaches is wrong, and a bow limbed in iron is a bad bow. The Maul is the same argument on a different property: mass off the head at 1.40, so lead is a bad sword and a fine maul. Without forms like these, ten weapons would have been ten skins on one stat map.

**A bug this uncovered.** `form.warspear` granted `move.skewer`, which requires `equippedTag: sword` — a tag no spear carries — so Skewer sat in the spear's moveset unusable for the whole of C2a. A new validator rule now refuses any form granting a move gated on a tag the form does not carry, mirroring the actor-side `equippedTag` rule. The spear got `move.spear_thrust` instead.

**Consequences:** nine new moves, each gated on its archetype's own tag, because since E4 a weapon *is* its moves — what separates a maul from a greatsword is not a damage number on the form, it is that only one of them can Pulverize. Ranged weapons are now expressible at all, which they were not before the enemy pass added the first non-magical ranged attacks. **None of it is balanced**, and it stays with the parked backlog.

**D34 addendum — the second archetype wave.** The exotic and martial-arts entries in the weapon list did not fit the first ten forms, and forcing them onto one would have been the "same form with a new name" mistake in reverse. Six more archetypes, each earning its place by what it **refuses** to read:

- **Halberd** — the Warspear's opposite. A spear flexes on the thrust and reads its haft for flex; a halberd is a weight on a lever and wants the haft stiff, so it reads *no* flexibility off the haft at all. A test pins that, because the day it stops being true the pair has collapsed into one weapon.
- **Shortsword** — reads hardness *and* flexibility off one blade. Short blades bend rather than break and a brittle one snaps, which no other blade form says.
- **Javelin** — thrown, so mass is entirely a cost (0.20) while the point still has to be hard.
- **Sling** — reads **no hardness whatsoever**. It is cord and a pouch; the stone is not part of the weapon.
- **Whip** — flexibility off the lash at 1.35, the hardest any form reads flexibility off a single component. The Longbow still reads more in *total* and should: being the flexibility weapon is the bow's identity, and the whip's is being one flexible thing on a handle.
- **Knuckles** — mass at 0.10, the smallest weapon in the game. The form for a material you have almost none of.

**Two claims I wrote were false and the tests caught both.** The whip was first authored as out-reading every weapon on flexibility (the Warspear ties it on total) and then as "the hardest any form reads any property" (the Maul reads mass at 1.40). The fix was to narrow the claim to what is true rather than inflate a number to match the prose — a stat weight exists to make the item behave, not to win an argument in a comment.

**Consequences:** 23 forms, 16 of them weapons; every name in the design list is placed. Four new archetype moves (Hew, Flurry, Lash, Pummel). Nunchaku became a Flail variant rather than an archetype, because mechanically that is what it is.

### D35 — The library triples, and the anti-tiering rule becomes code
*(Adopted 2026-08-17. Counts: `docs/game-overview.md` §18, `docs/crafting-overview.md` §3.)*

**The expansion:** materials 559 → **1448** (582 plants, 307 ores/gems/stones), and realms 1 → **164**.

**Generated, and said so plainly.** ~890 materials cannot be hand-authored in one pass, so they were produced by a keyword generator from the supplied name lists. That is a defensible way to get breadth and a terrible way to get balance, and the difference is stated here rather than discovered later: **every property in the new half is a first guess.**

**The generator encodes two CLAUDE.md rules rather than hoping for them:**

1. **Never MMO tiering.** A keyword shifts the property *profile*, it never scales it. Fire adds heat, Frost adds cold, Storm adds charge, Void adds arcane + instability. "Ancient / Elder / King" adds **harvest_resistance** — rare because it is hard to reach, not because its numbers are bigger. A `[Theory]` asserts the claim from the other side: anything named Frost carries cold, anything named Venom carries toxicity. A name is a claim about what a material *does*.
2. **Mundane-majority.** Both lists split at a `#fantasy` marker; everything above it is a real plant or mineral and receives only *physical* modifiers. This was not cosmetic — before the split, Elderberry was read as "elder" (very_rare) and Rosemary as "rose" (a flower). A test pins that the real-world half carries no arcane and no resonance, because that contrast is what makes Voidleaf legible.

**Realms ship as a roster, not as content.** Each of the 163 new realms is a name, a biome tag set, a tier band and a walkable two-depth graph — entrance → fork → descent, with a way out at each depth. **No combat or gather nodes**, per the instruction to leave encounters alone: those need actors and profession actions, and a node referencing neither cannot go stale. The Dark Forest keeps its hand-authored wiring; the generator refuses to shadow an existing realm id.

**What the tests found.** Reachability had to be asserted *per depth*, not per realm: descending is not an edge, it drops the party at `EntranceForDepth(depth + 1)`, so a whole-realm walk reported all seven of the Dark Forest's depth-2 nodes as stranded. The model was wrong, not the content — and the corrected rule now also proves every depth has its own way out.

**Consequences:** the new materials are **not gatherable** — no profession action produces them, and wiring ~890 into the profession ladders is its own pass. Nothing in the library is balanced. `ProfessionEcosystemTests` pins the count so the docs cannot drift from it again.

**D35 addendum — the library is gatherable.** All 889 expansion materials now have a source: **117 new gathering actions** across Farming (57), Mining (42), Forestry (17) and Fishing (1), taking the roster from 194 actions to **311**.

**Bucketed by theme, not by material.** One action per material would be 889 actions, and a ladder nobody can read is not a ladder. Each action gathers a *family*: one guaranteed output plus up to nine chance outputs from the same theme, so "Forage Ember Growth" hands back the ember moss, cap, flower and root together — which is how they grow. An earlier cut bucketed by theme *and* form and produced 227 actions, many of them one item long; that is a list, not a ladder.

**Deliberately no new loot tables.** `LootEcosystemTests.EveryLootSourceHandsBackSomethingASystemNames` requires every loot *table* to yield something a named system wants, and "wanted" is decided by form-slot tags — metal, wood, crystal, gem, stone, fiber. A table of pure herbs would fail it. Outputs and bonus outputs are not tables, so the herbs reach the player without tripping a rule aimed at a different problem; they remain usable because the transformation bench accepts any material as a reagent.

**Gate by the hardest thing in the bucket.** Required level comes from the bucket's rarest member — common 1, uncommon 12, rare 30, very_rare 50, exceptional 70 — so you do not stumble onto Voidleaf at level one. The five bands are populated 17 / 26 / 33 / 22 / 19.

**A pre-existing gap this surfaced, and did not fix.** `NoNewMaterialArrivesWithoutASource` counts raw materials nothing can hand the player: **229**, every one of them predating this expansion. They were authored across several milestones and never wired to anything. The test is a **ratchet**, not a clean bill of health — it pins the number so it cannot grow, and the 229 stay on the backlog as real work.

### D36 — Every raw material has a source, and where it comes from is a design decision
*(Adopted 2026-08-18, closing the gap D35 opened.)*

**The 229 stranded legacy materials are wired.** Actions 311 → **348**, plus drops added to six enemy family tables. `EveryRawMaterialHasASource` is now an **exact** assertion rather than a ratchet: the count is zero, and a material authored without a source fails the build.

**Three destinations, chosen by what the thing is:**

1. **Anatomy goes on the creature it came off.** 51 materials — panther fang, mammoth tusk, ghoul claw, scorpion venom — became chance drops on `loot.family.beast/vermin/aquatic/draconic/reptilian/undead`. D28 already says enemies drop inputs; a "Forage Panther Growth" action would have been absurd and would have quietly made hunting pointless.
2. **Essence comes out of a Realm.** 21 of the stranded materials carry essence, and the first cut put them behind Farming/Mining/Fishing — which `C2cAuditTests` caught immediately. That audit is not bookkeeping: **a profession that hands essence over at the Hideout removes the reason to enter a Realm at all.** They moved to `loot.family.elemental` (motes, cores, shards, essences), `loot.family.plant` (frostbark, frostpine), `loot.family.undead` (holy water) and `loot.family.ooze` (witchbog brine). Four actions lost their only output in the process and were deleted rather than shipped empty.
3. **Everything else is gathered**, keyed off what it physically is — logs and resins to Forestry, ores/stone/gems/liquids/gases to Mining, waters to Fishing, plants and fungi to Farming.

**A latent bug this surfaced.** The loot file already carried a trailing comma inside `loot.family.beast`'s `chanceDrops`, tolerated only because the loader runs with `AllowTrailingCommas`. Appending to that array produced `,,` and broke the parse. Both the double comma and every trailing comma in the file are now gone.

**Consequences:** the library is fully reachable for the first time. **None of it is balanced** — 348 actions carry formula-derived intervals and XP, and the 0.12 anatomy drop chances are a first guess.

### D37 — The Dark Forest becomes a place, and Realm Knowledge finally buys something
*(Adopted 2026-08-18, Phase 6. Realm: `game/data/realms/dark_forest.json`; insights: `core/Realms/RealmInsight.cs`.)*

**The Dark Forest goes 15 locations / 2 depths → 31 locations / 3 depths**, and carries every node kind the architecture has. Four new `RealmLocationType` members — **Camp, Shrine, Merchant, Hazard** — plus a `Hidden` flag. Adding kinds was cheap and stays cheap: a run is transient and never persisted, so a location type is content, not a save key.

**Realm Knowledge unlocks options, never damage.** Five insights at 6 / 12 / 20 / 30 / 42: enemy weaknesses → hazards → rich nodes → **hidden routes** → extraction routes. A percentage would have made Knowledge a second power curve and quietly made the realm easier for reasons the player cannot see. Instead the realm stays exactly as lethal and the player stops walking into it blind. **The order is the design; the numbers are a first pass.**

**Hidden nodes are the payoff, and they are load-bearing rather than bonuses.** Three of them: the cache behind the depth-1 thornwall, the stump off the depth-2 kill site, and the back door out of the boss room. `RealmRun.IsReachable` is the single rule for both "can I see it" and "can I walk there", so the map and the movement can never disagree. Knowledge is held **on the run** and updated live, so a shortcut earned on the way down is one you can take on the way back.

**Three sub-rulings on the new node kinds:**

1. **A camp spends safety instead of banking it** — restores a fraction of every pool, once, so the decision is "use it now or carry it deeper".
2. **The merchant spends UNSECURED coin.** It is the first gold sink in the game, and it takes the coin you would lose by dying on the way out — which makes buying the extraction decision in miniature. It sells **inputs and knowledge only, never finished equipment**, because the whole identity is that you craft your own (D28).
3. **A hazard is paid on arrival, not as an action.** There is no "decline to be in the bog". What Knowledge buys is seeing it on the map first, which turns an ambush into a route choice.

**The rank seam is finally occupied.** `loot.shared.rank_spoils` has gated on the `elite`/`boss` tags since the loot pass, carried by every family table, with **no actor wearing one**. Grask, the Warlord (elite, depth 2) and Thornheart, the Old Growth (boss, depth 3) are the first — and tagging them was the entire wiring, because combat already passes resolved actor tags into the loot context. Thornheart is deliberately `family.plant` in a goblin realm: everything learned on the way down is the wrong lesson.

**A test I got wrong, and what it taught.** `DeeperGroundPaysBetter` first measured *distinct drops reachable* and failed — depth 1 has more nodes and nests into the broad shared tables, so the shallows won on breadth while paying in bark and scrap. **Breadth is not reward.** Rewritten to measure average rarity: depth 1 scores 0.78, depth 3 scores 1.75.

**Consequences:** four validator rules (a camp with no restore, a merchant with no price or stock, a shrine with no words, a hazard that costs nothing — each loads cleanly and does nothing when stood on), plus one that refuses a hidden node no visible node connects to. **Nothing is balanced**: hazard damage, restore fractions, the 40-coin price and all five knowledge thresholds are first guesses.

### D38 — The Dark Forest's first balance pass: coherence, not feel
*(Adopted 2026-08-18. Sheet: `DarkForestBalanceTests.RenderTheBalanceSheet`.)*

**What this pass is and is not.** `HANDOFF.md` carries a standing decision — *do not retune before the user reports play feel* — and it is right: nobody has played this realm, so no number here is claimed to be fun. What was done instead is **coherence**: measuring the realm's numbers against each other and against a fresh character, and fixing what is provably wrong. Feel is still the user's to report.

**The measurement first.** A fresh character (the shipped default build, uniform-5) is **59 HP / 55 STA / 69 MP**. Against that: hazards cost 6 / 12 / 20 (10% / 20% / 34% of health), camps restore 50% and 65%, fights run 30 → 36 → 60 → 130 → 290 HP, the trader asks 40 against a route that can pay 85.

**Two things were provably broken, and both are now pinned by test.**

1. **Depth 2's first fight was easier than depth 1's.** The Goblin Hexer resolved to **24 HP** — `role.caster` subtracts health from the goblin family's 30 — while the depth-1 Raider had 30. The first fight past the descent was a step backwards. Fixed with a per-actor `+12 health` tweak (36 HP), not by touching `role.caster`, which is shared by 91 actors across the game. `NoDeeperFightIsWeakerThanTheOnesAboveIt` now forbids the whole class of inversion.

2. **The knowledge ladder was a formality.** Thresholds shipped at 6/12/20/30/42 against a measured yield of **~71 knowledge for one thorough run** — so a single first expedition unlocked everything, including the hidden routes that are supposed to be the reward for learning the place. Rescaled to 30/75/160/320/560, an arc of roughly **eight thorough runs** (0.4 / 1.1 / 2.3 / 4.5 / 7.9). The test pins the **ratio to per-run yield**, not the raw numbers, so retuning a node's grant cannot silently trivialise it again.

**What the sheet says is fine.** Hazards escalate and none is survivable-but-trivial or lethal-on-arrival; each depth's total stays under one character's health. Camps give back more than their depth's hazards cost, and never a full heal. Thornheart at 290 HP is ~29 swings of an iron sword — long enough to be an event, not an afternoon. The trader's 40 sits inside the 85 the route to her can pay.

**Explicitly not done:** combat pacing, XP curves, gather rates, loot weights, and every number outside this realm. Those stay in the parked backlog, and they stay there until someone plays.
