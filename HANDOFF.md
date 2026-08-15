# HANDOFF.md

For the next Claude session. Read `CLAUDE.md`, then this, then `PROJECT_STATE.md` / `SYSTEM_INDEX.md` / `DECISIONS.md` / `ROADMAP.md`. Then inspect the repo.

## Where we just were
Building the **equipment + item-instance system** in phases on top of the completed MVP vertical slice. This session did: design docs → Phase 1 (item/property/instance + equipment model) → Phase 2 (weapon-driven combat, armor, starter loadout, gear safe on death) → Phase 3 (crafting produces derived instances) → **Phase 4a (save persistence of instances + equipment)** — the last thing committed.

## Current objective
Finish **Phase 4** of the equipment/crafting work, then make crafting real. Immediate remaining Phase 4 items, in recommended order:
1. **Content validation at load** (cross-references validated when DataStores load, not only in tests).
2. **Unify item+quantity shapes** (`ItemStack` vs `ItemAmountData`/`ItemChanceData`).
3. **Equipment/inventory UI** (equip/unequip-from-stash panel; shell reorg).

The user stated: after Phase 4 we **populate items** and then build the **crafting reaction simulation**. So do 1 & 2 before big item authoring.

## Repo/git state
- Branch `main`, working tree **clean**. Latest commit `afa2d05` (save persistence).
- `dotnet build InTheDungeonsWeDie.slnx` clean; `dotnet test` → **137 passing** (0 failing). Godot is not on PATH — verify via dotnet only; the user runs the game from their Godot 4.7.1 editor.
- Recent commits: `afa2d05` P4a save · `0377872` P3 crafting instances · `4d1ccc8` P1–2 equipment · `988357f` M9 · … (see `git log`).

## Files changed most recently (this session)
- New Core: `core/Items/*` (PropertySet, ItemProperties, ItemType, IItemDefinition, ItemInstance, InstanceIdSource), `core/Equipment/*` (EquipmentDefinition, Equipment, EquipmentResolver), `core/Combat/AttackProfile.cs`, `core/Crafting/CraftingDerivation.cs`.
- Modified Core: `core/Combat/{Combatant,CombatCalculator,CombatEncounter,CombatTuning}.cs`, `core/Content/MaterialDefinition.cs`, `core/Inventory/Inventory.cs`, `core/Realms/RealmExtraction.cs`, `core/Crafting/{CraftingInteractionDefinition,CraftingExperimentSystem,ExperimentOutcome}.cs`, `core/Persistence/{SaveData,SaveMapper}.cs`.
- Godot: `game/GameRoot.cs` (equipment load/starter/resolve/save wiring, instance inventory display), `game/ui/MainMvpUI.cs` (equip buttons), `game/data/equipment/*.json` (4), `game/data/crafting_interactions/barkbound_iron.json` (resultIsInstance).
- Docs: `docs/itemization.md` (rewritten), `docs/crafting.md §17`, `docs/current-state.md` (audit). Handoff docs (this set).
- Tests: `tests/Items/*`, plus updates to Combat/Crafting/Persistence/Realms tests.

## Outstanding problems / questions
- **Content validation is tests-only** — a bad JSON ships and only throws later via `GetById`. First Phase-4 task fixes this.
- **Two item+quantity shapes** — unify before authoring lots of items.
- **Crafting recursion input half not wired** — `CraftingExperimentSystem.Experiment` matches submitted *stackable* ids; it can't yet consume an `ItemInstance` as an input. Needed for true recursive crafting.
- **`GameRoot` ~850 lines** — composition root + app glue + report formatting; extract an Application layer before piling on more systems.
- **Reaction simulation is architecture-only** — `CraftingDerivation` is a trivial additive merge; the real rules are deferred by user instruction. Don't hardcode per-combination recipes.
- **Dead content**: `ability.guard`/`ability.hex_bolt` referenced by classes but unimplemented; Mana unused; material properties on non-instance materials are mostly inert except via derivation.
- The old verbose `CLAUDE.md` (full design bible) was replaced with a concise rules file this session; the detailed design lives in `/docs` and these handoff files. If you need the old vision text, see `docs/*` and git history.

## Exact recommended next steps
1. `dotnet test InTheDungeonsWeDie.slnx` — confirm 137 green.
2. Skim `docs/itemization.md` + `docs/crafting.md §17` (the item/crafting model) and `SYSTEM_INDEX.md`.
3. Start Phase 4 item #1: add **load-time content validation**. Suggested approach: after `GameRoot` loads all `DataStore`s, run a validator that checks cross-references (actors→abilities & loot materials; profession_actions→professions & item ids; crafting_interactions→materials/consumables/equipment & professions; realm locations→actors/actions/materials; equipment well-formed). Prefer a small Core validator that takes the loaded stores and returns a list of problems, called from `GameRoot._Ready` (fail loudly / log). Add a test that the shipped content passes.
4. Then Phase 4 item #2 (unify item shapes), then #3 (equipment UI).
5. Keep tests green; commit only when the user asks.
