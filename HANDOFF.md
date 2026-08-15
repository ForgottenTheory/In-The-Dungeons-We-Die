# HANDOFF.md

For the next Claude session. Read `CLAUDE.md`, then this, then `PROJECT_STATE.md` / `SYSTEM_INDEX.md` / `DECISIONS.md` / `ROADMAP.md`. Then inspect the repo.

## Where we just were
Building the **equipment + item-instance system** in phases on top of the completed MVP vertical slice. This session did: design docs → Phase 1 (item/property/instance + equipment model) → Phase 2 (weapon-driven combat, armor, starter loadout, gear safe on death) → Phase 3 (crafting produces derived instances) → **Phase 4a (save persistence of instances + equipment)** — the last thing committed.

## Current objective
Finish **Phase 4** of the equipment/crafting work, then make crafting real. Remaining Phase 4 items, in recommended order:
1. ✅ **Content validation at load** — done. `core/Content/ContentValidator` (+ `ContentProblem`, `ContentValidationException`) validates content cross-references; wired into `GameRoot._Ready` via `ValidateContentOrThrow`. Covered by `tests/Content/ContentValidatorTests.cs`.
2. ✅ **Unify item+quantity shapes** — done. `ItemStack(ItemId, Quantity=1)` is the single item+quantity shape; new `ItemChance(ItemId, Chance, Quantity=1)` (in `core/Items/`, exposes `.Stack`) replaces the old `ItemChanceData`; `ItemAmountData` removed. Profession action JSON unchanged.
3. ✅ **Equipment/inventory UI** — done. `MainMvpUI` EQUIPMENT section (per-slot rows + Unequip, Stash list + Equip, debug Grant-to-stash), backed by `GameRoot.EquipFromStash`/`UnequipToStash`/`GrantToStash` + queries. Godot-side — user verifies visually.

**Phase 4 complete; material library authored (~470 defs); emergent-item-system P0 done.**

The crafting system is now driven by **`docs/emergent-item-system.md`** (ACCEPTED, supersedes `docs/crafting.md §17`). **P0 shipped** (see ROADMAP): `family:value` tag namespace across all materials, `PropertyDefinition` registry + roles (`game/data/properties/`), `resonance` property, `ResistanceCalculator` (derived resistances), and tag-family/property validation. New Core: `PropertyDefinition`, `ResistanceCalculator`, `TagFamilies`. The tag-migration script is in the scratchpad (`migrate_tags.ps1`) if you need the exact mapping.

**Next is P1** (§20): `ProcessDefinition` + the universal reaction algebra (convergence/off-channel/opposition) + potency + integrity (destruction/byproducts/pre-commit UI) + archetype registry + naming + Reaction Log, with **zero authored signatures/traits** — prove the emergent core first. Do NOT hardcode crafting combinations. The old `CraftingExperimentSystem` stays until P1 replaces it. Confirm scope with the user before building P1 (it's large).

The user stated: after Phase 4 we **populate items** and then build the **crafting reaction simulation**. So do 1 & 2 before big item authoring.

## Repo/git state
- Branch `main`, latest commit `05a9f29` (equipment P4 + base-resource ecosystem). Uncommitted (not yet committed — commit on request): the tabbed/themed `MainMvpUI` refresh, and **emergent-item-system P0** (tag namespacing migration of all 7 material files, `PropertyDefinition` registry + `game/data/properties/`, `ResistanceCalculator`, `TagFamilies`, validator rules).
- `dotnet build InTheDungeonsWeDie.slnx` clean; `dotnet test` → **182 passing** (0 failing). Godot is not on PATH — verify via dotnet only; the user runs the game from their Godot 4.7.1 editor.
- Recent commits: `afa2d05` P4a save · `0377872` P3 crafting instances · `4d1ccc8` P1–2 equipment · `988357f` M9 · … (see `git log`).

## Files changed most recently (this session)
- New Core: `core/Items/*` (PropertySet, ItemProperties, ItemType, IItemDefinition, ItemInstance, InstanceIdSource), `core/Equipment/*` (EquipmentDefinition, Equipment, EquipmentResolver), `core/Combat/AttackProfile.cs`, `core/Crafting/CraftingDerivation.cs`.
- Modified Core: `core/Combat/{Combatant,CombatCalculator,CombatEncounter,CombatTuning}.cs`, `core/Content/MaterialDefinition.cs`, `core/Inventory/Inventory.cs`, `core/Realms/RealmExtraction.cs`, `core/Crafting/{CraftingInteractionDefinition,CraftingExperimentSystem,ExperimentOutcome}.cs`, `core/Persistence/{SaveData,SaveMapper}.cs`.
- Godot: `game/GameRoot.cs` (equipment load/starter/resolve/save wiring, instance inventory display), `game/ui/MainMvpUI.cs` (equip buttons), `game/data/equipment/*.json` (4), `game/data/crafting_interactions/barkbound_iron.json` (resultIsInstance).
- Docs: `docs/itemization.md` (rewritten), `docs/crafting.md §17`, `docs/current-state.md` (audit). Handoff docs (this set).
- Tests: `tests/Items/*`, plus updates to Combat/Crafting/Persistence/Realms tests.

## Outstanding problems / questions
- ~~Content validation is tests-only~~ — **fixed**; validated at load by `ContentValidator`. (Note: character-component refs — rule ids, class ability ids incl. the known-dead `ability.guard`/`hex_bolt` — are intentionally NOT in the validator; they're resolved/validated by the `CharacterComposer` path instead.)
- ~~Two item+quantity shapes~~ — **fixed**; unified on `ItemStack` + `ItemChance`.
- **Crafting recursion input half not wired** — `CraftingExperimentSystem.Experiment` matches submitted *stackable* ids; it can't yet consume an `ItemInstance` as an input. Needed for true recursive crafting.
- **`GameRoot` ~850 lines** — composition root + app glue + report formatting; extract an Application layer before piling on more systems.
- **Reaction simulation is architecture-only** — `CraftingDerivation` is a trivial additive merge; the real rules are deferred by user instruction. Don't hardcode per-combination recipes.
- **Dead content**: `ability.guard`/`ability.hex_bolt` referenced by classes but unimplemented; Mana unused; material properties on non-instance materials are mostly inert except via derivation.
- The old verbose `CLAUDE.md` (full design bible) was replaced with a concise rules file this session; the detailed design lives in `/docs` and these handoff files. If you need the old vision text, see `docs/*` and git history.

## Exact recommended next steps
1. `dotnet test InTheDungeonsWeDie.slnx` — confirm 155 green.
2. Skim `docs/itemization.md` + `docs/crafting.md §17` (the item/crafting model) and `SYSTEM_INDEX.md`.
3. **Phase 4 is complete.** Next is "make crafting real" — the emergent **reaction simulation** (`docs/crafting.md §17.3`), which the user described as "basically Conway's Game of Life" for crafting. The user wants to design/build this **together** with item population. Read `docs/crafting.md §17` thoroughly and discuss the model before writing rules. Do NOT hardcode per-combination recipes; the sim slots in behind `CraftingDerivation` + the crafting matcher. Also note the "input half" gap: `CraftingExperimentSystem` can't yet consume an `ItemInstance` as an input (recursion) — likely a prerequisite.
4. Keep tests green; commit only when the user asks.
