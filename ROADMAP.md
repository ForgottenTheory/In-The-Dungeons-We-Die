# ROADMAP.md

Status: ✅ done · 🔄 in progress · ⬜ not started.

## Completed
- ✅ **MVP vertical slice (Milestones 1–9)** — foundation, character composition, professions (passive+active), crafting+discovery, tick combat (telegraph/block/dodge), Dark Forest realm + travel + depth, extraction + run-loss, save/load, consumables, full-loop integration test. Commits `77ca49b`→`988357f`.
- ✅ **Equipment/item-instance system — Phases 1–3 + save** (commits `4d1ccc8`, `0377872`, `afa2d05`):
  - P1 item/property/instance model + equipment data model + resolver seam.
  - P2 combat integration (weapon-driven attack, armor/resist, starter loadout, gear-safe-on-death).
  - P3 crafting produces derived instances (Barkbound Iron is an instance; `CraftingDerivation` seam).
  - P4a save persistence of instances + equipment + id counter.

## 🔄 Current phase — Equipment/crafting, Phase 4 remainder
Do in this order (the first two de-risk the item population that follows):
1. ⬜ **Content validation at load** — validate cross-references when `DataStore`s load (actors→abilities, recipes→materials, realm nodes→actors/actions, equipment→valid, etc.) instead of only in tests. Fail loudly early. *(Directly de-risks authoring the big item batch next.)*
2. ⬜ **Unify item+quantity shapes** — one representation instead of `ItemStack` (crafting/stash) vs `ItemAmountData`/`ItemChanceData` (profession actions).
3. ⬜ **Equipment/inventory UI** — a real equip/unequip-from-stash panel showing instances + a reorganized shell (still no art). Currently only debug "Equip <name>" buttons.

## Next — make crafting real (the emergent simulation)
Ordered so each step is testable before the next:
1. ⬜ **Crafting with instance inputs** — let `CraftingExperimentSystem` accept `ItemInstance`s as inputs (recursion "input half"; generation half already works). Match/consume instances, derive from their properties.
2. ⬜ **Populate items** — author the real material/property/equipment content (raw materials with Physical/Processing/Reactive properties, more equipment). *User plans to do this together after Phase 4.* Content-validation (above) should be in place first.
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
Keep `dotnet test` green (137 cases now). Add Core tests for new deterministic behavior. Don't hardcode crafting combinations. Don't put gameplay rules in `GameRoot`/UI. Commit only when asked; on `main`.
