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
Core `DataStore<T>` consumes JSON **text**; the Godot `ContentLoader` owns `res://`/`user://` file access. **Why:** keeps Core engine-independent and content loading unit-testable. **Known gap:** cross-reference validation (an actor's ability exists, a recipe's material exists) currently lives only in **tests**, not at load time — see ROADMAP task "content validation".

### D6 — Item **instances** over definition-only gear (the pivotal item decision)
Raw materials stay quantity-based stacks; **any equipment, and any generated/processed material whose properties differ from its definition, becomes a unique `ItemInstance`** (id, quality, derived `PropertySet`, provenance, traits). **Why:** crafting is the point of the game — the player must be able to make unique gear and *recursively* craftable generated materials (Iron Ingot + Bloodmoss → Bloodmoss Iron Ingot → …). Retrofitting instances later would be a painful inventory refactor. **Rejected:** definition-only gear (every "Iron Sword" identical; material properties stay cosmetic; can't support emergent crafting).

### D7 — Crafting is an emergent **property simulation**, not an authored recipe table
Long-term, ingredients carry properties (Hardness/Mass/Affinity/Heat/Toxicity/Growth/…, string-keyed), and universal interaction rules decide outcomes; discovered recipes are *records*, not the source of truth (`docs/crafting.md §17`, `docs/itemization.md`). **Why:** unusual player experimentation should produce legitimate materials/gear without hand-authoring every combination. **Current status:** architecture only — `ItemInstance` + `PropertySet` + `CraftingDerivation` (a trivial additive-merge seam) exist; the reaction rules/thresholds/instability/mutation/catalysts are **deliberately deferred** (user's explicit instruction). Do not build hundreds of reaction rules yet; build the model so they slot in behind `CraftingDerivation` and the crafting matcher.

### D8 — Combat reads neutral `AttackProfile`/`ArmorProfile`, never equipment types
`EquipmentResolver` turns definition + instance properties into these profiles; `CombatEncounter`/`CombatCalculator` consume only them. **Why:** the material→combat rules (derived damage, on-hit effects, resistances) can grow behind one seam without ever touching the encounter, and it avoids a Combat↔Equipment dependency cycle. `CombatCalculator.Resolve` takes `(damageType, baseDamage)` so weapon attacks and enemy abilities share one pipeline.

### D9 — Namespace ≠ type name
The `Inventory` and `Equipment` **classes** live in namespace `Dungeons.Items` (not `Dungeons.Inventory`/`Dungeons.Equipment`). **Why:** a class named identically to its namespace makes `new Inventory()` ambiguous in consumers. Hit twice; codified here.

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
