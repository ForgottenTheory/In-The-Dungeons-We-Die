# ROADMAP.md

Status: ✅ done · 🔄 in progress · ⬜ not started.

## Completed
- ✅ **MVP vertical slice (Milestones 1–9)** — foundation, character composition, professions (passive+active), crafting+discovery, tick combat (telegraph/block/dodge), Dark Forest realm + travel + depth, extraction + run-loss, save/load, consumables, full-loop integration test. Commits `77ca49b`→`988357f`.
- ✅ **Equipment/item-instance system — Phases 1–3 + save** (commits `4d1ccc8`, `0377872`, `afa2d05`):
  - P1 item/property/instance model + equipment data model + resolver seam.
  - P2 combat integration (weapon-driven attack, armor/resist, starter loadout, gear-safe-on-death).
  - P3 crafting produces derived instances (Barkbound Iron is an instance; `CraftingDerivation` seam).
  - P4a save persistence of instances + equipment + id counter.

## ✅ Equipment/crafting Phase 4 — COMPLETE
All three remainder items done (content validation, unified item shapes, equipment UI). Next up is "make crafting real" below — the emergent reaction simulation. Per the user, item population + reaction sim are done together next; do NOT hardcode crafting combinations.
1. ✅ **Content validation at load** — `core/Content/ContentValidator` checks cross-references (actors→abilities & loot, profession actions→profession & materials, crafting→materials/consumables & professions, realm nodes→actors/actions/rewards + symmetric edges, equipment slot↔stat block). Called from `GameRoot._Ready` (`ValidateContentOrThrow`); logs each problem via `GD.PushError` then throws `ContentValidationException`. Tests: `tests/Content/ContentValidatorTests.cs` (shipped content passes + one broken-store test per rule).
2. ✅ **Unify item+quantity shapes** — `ItemStack(ItemId, Quantity=1)` is now the single item+quantity shape everywhere; the old `ItemAmountData`/`ItemChanceData` classes are gone. Profession action Inputs/Outputs are `ItemStack`; BonusOutputs are the new `ItemChance(ItemId, Chance, Quantity=1)` value type (built on `ItemStack`, exposes `.Stack`). Flat content JSON is unchanged. Regression test: `ProfessionContentValidationTests.FlatJson_BindsToUnifiedItemStackAndItemChance`.
3. ✅ **Equipment/inventory UI** — `MainMvpUI` now has a real EQUIPMENT section: per-slot rows (weapon/armor) with resolved stat summaries + Unequip, a Stash list of unequipped instances each with Equip, and a debug "Grant to stash" row. Backed by `GameRoot` commands `EquipFromStash`/`UnequipToStash`/`GrantToStash` and queries `EquippedWeapon`/`EquippedArmor`/`StashEquipment`/`InstanceLabel`. (Godot-side; verify visually in the editor.)

## Next — make crafting real (the emergent simulation)
Ordered so each step is testable before the next:
1. ⬜ **Crafting with instance inputs** — let `CraftingExperimentSystem` accept `ItemInstance`s as inputs (recursion "input half"; generation half already works). Match/consume instances, derive from their properties.
2. 🔄 **Populate items** — ✅ **raw/processed material library done** (~470 defs on a 0–100 flat-property scale across `game/data/materials/` category array files; biome-diverse, multi-part creatures, rarity-tagged, load-validated; `docs/itemization.md §2`). ⬜ Still to author: more **equipment** (weapons/armor beyond the 4 starters); generated instances come from crafting. No reaction rules / derived combinations authored (by design).
3. ⬜ **Reaction simulation** — implement the universal interaction pipeline behind `CraftingDerivation`/the crafting matcher (`docs/crafting.md §17.3`): bonding/affinity, property transfer, opposing-property resolution, reactions (e.g. Growth+Toxicity→blight trait), thresholds/capacity, catalysts, instability → success/partial/failure/mutation, generate instance. Do NOT hardcode per-combination recipes.
4. ⬜ **Material → combat/effect rules** — expand `EquipmentResolver` (and a combat-effect hook) so derived properties drive on-hit effects, resistances, status. Currently only Mass/Hardness.

## Later (deferred; roughly by dependency)
- ⬜ **Combat depth**: status effects; class abilities + Mana spells; multi-enemy + targeting; interrupts; suffix combat rule-hooks; positioning.
- ⬜ **Loot & economy**: loot tables, rarity, currency/vendors.
- ⬜ **Character model**: character level/growth; character creation/selection UI (build is hardcoded); meaningful prefix/suffix roster.
- ⬜ **Realm content**: affixes, tiers>1, more realms, more location types (camp/boss/elite/hidden/merchant/hazard), Knowledge that unlocks info/options, pre-run loadout selection; maybe procedural maps.
- ⬜ **Professions breadth**: Mining (ore has no source), and the rest of the roster; offline progression aggregation.
- ⬜ **Persistence**: save migration, multi-slot, mid-run save (persist active `RealmRun`), RNG/tick persistence.
- ⬜ **Architecture**: extract an Application layer out of `GameRoot`; path from the single debug page to real Godot screens.
- ⬜ **Production**: art, audio, animation, telegraph visuals; eventual multiplayer (host-authoritative — Domain already supports it).

## Guardrails
Keep `dotnet test` green (160 cases now). Add Core tests for new deterministic behavior. Don't hardcode crafting combinations. Don't put gameplay rules in `GameRoot`/UI. Commit only when asked; on `main`.
