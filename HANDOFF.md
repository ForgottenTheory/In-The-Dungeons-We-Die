# HANDOFF.md

For the next Claude session. Read `CLAUDE.md`, then this, then `PROJECT_STATE.md` / `SYSTEM_INDEX.md` / `DECISIONS.md` / `ROADMAP.md`. For crafting work, **`docs/emergent-item-system.md`** is the accepted spec and supersedes `docs/crafting.md §17`.

## Repo / build state
- Branch `main`. `dotnet build InTheDungeonsWeDie.slnx` clean (0 warnings); `dotnet test` → **364 passing**.
- Godot is **not** on PATH — verify via `dotnet build`/`dotnet test` only; the user runs the game from their Godot 4.7.1 editor and verifies UI visually.

## Where we are
MVP vertical slice complete; equipment/item-instance system done; ~474-material library; emergent item system **P0 and P1 complete**.

**P1 shipped the whole emergent crafting core**: `ProcessDefinition` + 7 starter processes, the universal reaction algebra, potency/integrity/variance, destruction + byproducts, quantization → signature → archetype registry (`SaveData` v4), naming v1, the Reaction Log, tag derivation, and the Crafting tab. There are **no recipes** — `ReactionEngine.Resolve` is the single path. See `SYSTEM_INDEX.md` § Crafting for the file map and **DECISIONS D20/D21** for the two decisions that shaped it.

---

## ⚠️ FIRST: the Crafting tab has never been seen running

Everything in P1 is covered by Core tests **except the Godot UI**, which was written without being run (Godot isn't on PATH here). Before building anything new:

1. Open the Crafting tab, hit **Grant Test Mats** (grants 12 materials ×20 and Herblore/Smithing 15, unlocking every process).
2. Pick a process → base → add 1–3 steps → check the projection panel updates live → Craft.
3. Watch for: duplicated reagent rows after reordering, picker selections jumping after a craft, the projection not refreshing, and layout overflow in the ordered-chain card.

Specific things to sanity-check because they were reasoned about rather than observed: `RebuildReagentChain` does `RemoveChild` **then** `QueueFree` (deferred free alone duplicates rows for a frame); `RefreshCraftingPickers` reads the previous selection **before** replacing `_onHand` (index→id mapping is snapshot-relative).

## THEN: tune from play before starting P2

Two numbers are provisional and can only be judged by playing. Both are single constants.

1. **Quantization bucket size** — `QuantizationTuning.PropertyBucket` (currently 5). §21 calls this *the single highest-risk tuning number in the design*. Measured: 2,800 varied crafts → 913 distinct archetypes (67% of distinct states collapsed). Too coarse and the space collapses; too fine and the registry floods with neighbours nobody can tell apart. Judge it by whether crafts feel like they land on *meaningfully* different materials.
2. **How weak the integrity budget feels** — `RefinementTuning.StateDeltaCost` (currently 12, the value §19's arithmetic uses). P1 allows roughly **20–40 meaningful refinements** before destruction, which is looser than §6.2's "commit-or-lose decision made once per material" implies. That is faithful to the spec: the expensive cost terms are **traits (+4 each, P2)** and **signature reactions (+6, P4)**. So either accept it and let P2/P4 restore the tension, or raise the constant to make P1 tense on its own. Don't do both.

Also worth reviewing once played: whether "1 in 3 crafts is a new material" feels right, and whether the generated names read well in bulk (they were spot-checked, not surveyed).

## Known, filed, deliberately not fixed
- **Integrity is excluded from the signature** (§12.1 lists what is hashed; integrity isn't there), so an archetype keeps the integrity of its *first* discovery and all units share it. Reaching the same state by a cheaper path would inherit the wrong budget. In practice the paths self-balance — cost is proportional to Δstate and gentler processes need more steps. Fix if it bites: hash integrity too, at the cost of many more near-duplicate stacks. (DECISIONS D20.)
- **`PropertyDefinition.transferable` is unconsumed.** Structural properties are authored `false`, yet §19 has Forge Infusion moving `hardness` on-channel; the channel is authoritative (§7.2) and off-channel reagents transfer nothing anyway, so the field has no semantics left. Give it a job or drop it. (`dilutes` *is* used and drives the §8.3 split.)
- **`§15.3`'s sample log shows conductivity rising 55 → 57** on a craft where the reagent has no conductivity. §8.3 says off-channel structural blends *toward the mass-weighted mixture*, which must move it down; the implementation follows the rule, not the illustration.
- **Response properties are dropped on transformation**, so resistance is always derived (§2.2) rather than a stale authored override perpetuating itself. Visible consequence: Iron Ingot's authored `heat_resistance: 60` becomes a derived ~14 after any craft. Arguably the more honest number (iron conducts heat), but it is a discontinuity between authored and crafted materials.
- **Legacy shim**: `CraftingExperimentSystem` + `interaction.healing_salve` survive only because consumables come from fabrication (P5c). Delete that whole path when P5c lands (DECISIONS D21).

## Next phases (`docs/emergent-item-system.md §20`)
**P2** state traits (~15) + cap 3 + displacement + supersession · **P3** essence + resonance strain + `Attune` · **P4** signature reactions (~10) + chain signatures · **P5a/b/c** fabrication (single-slot → multi-component → consumables) · **P6** codex/journal/assay/rename.

P2 is the natural next step: traits are what make results *qualitative*, they restore the integrity tension, and the trait slot in the name grammar is already reserved (the adjective currently comes from the dominant property; traits take priority when they exist). The signature already reserves empty `|traits=|essence=` slots, so adding them will not re-key existing archetypes.

## Guardrails
- Keep `dotnet test` green (364 now) in tested increments.
- Core stays Godot-free; nothing authoritative in `GameRoot`/UI. `GameRoot`'s crafting surface is deliberately thin forwards — keep it that way; the Application-layer extraction is still deferred and `GameRoot` is ~1,050 lines.
- Do **not** hardcode per-combination recipes or reaction rules. The algebra is the source of truth. Adding one is how the whole design fails (§0 Decision 1).
- Commit only when the user asks; on `main`, end messages with the Co-Authored-By trailer.
