# ROADMAP.md

Status: ✅ done · 🔄 in progress · ⬜ not started. Kept short on purpose — history lives in git,
system detail in `PROJECT_STATE.md`/`SYSTEM_INDEX.md`, status in `docs/GDD.md` §19.

## Done (compressed)
- ✅ **MVP vertical slice** (Milestones 1–9): professions, crafting, tick combat, Dark Forest
  realm, extraction/run-loss, save/load, full-loop test.
- ✅ **Equipment/item-instance system** (P1–P4): instances, resolver seam, combat integration,
  save, validation, UI.
- ✅ **Emergent crafting P0–P1**: the universal reaction engine — algebra, potency, integrity,
  destruction/byproducts, signatures, naming, Reaction Log, projection, Crafting bench.
- ✅ **Class combinator**: 15 Bases · 25 Prefixes · 50 Suffixes → 18,750 builds, none authored.
- ✅ **Effect foundation E0–E4** (`docs/effect-foundation.md` §10): combat on the event bus →
  traced packet pipeline → telegraph/windup split + 28 statuses + Resolve → rule engine
  (`effects[]`, proc safety) + scoped modifiers + 11 effect handlers + stateful conditions →
  **the Move system** (movesets, move modification, enemy AI profiles, Mnemonic; D-18 bridge
  deleted).

## The plan to the full-fantasy slice
Target: prepare → enter → fight *as your build* → extract → **fabricate gear from what you
brought back** → feel the difference next run. Dependency-ordered; each milestone is one
coherent run.

1. ⬜ **M1 — Editor verification + tuning pass** *(small, user-driven)*. Render the unverified
   surfaces (Hit Log, Live hooks, gauges, moveset UI + `CombatUseMove`), play the E0–E4 combat,
   tune from play: the two provisional crafting constants, the D-20 interval floor (0.55 vs the
   shipped 0.25), stagger/Resolve feel.
2. ⬜ **M2′ — The universal move library + acquisition v1** *(replaces "Base signature moves"
   per D25 — Bases never own moves)*. Author ~15–20 moves as **shared** content, soft-gated by
   costs, attribute scaling and physical requires — never by class. Acquisition v1: **technique
   items** (grimoires/manuals) that teach a move into a persisted learned list; droppable now,
   purchasable/craftable later (E5+: *fabricate the tome*). Bases keep 0–1 starting moves from
   the library as kit; migrate the Wizard/Fireball + Bastion/Shield-Bash grants to the library's
   acquisition sources. Includes: enemy AI profiles for the goblins, the **Fighter identity
   redesign** (its engine was universalized in E4 — NEEDS DESIGN), and the casting-speed
   attribute-scaling decision.
3. ⬜ **C1 — Crafting P2 traits + P3 essence**. State traits with cap/displacement/supersession;
   essence + resonance strain + Attune. Prerequisite genetics for everything below.
4. ⬜ **C2 — Fabrication + the scale reconciliation** *(highest risk)*. Form templates with named
   slots/apertures/stat maps; materials → equipment instances; **the 0–100 ↔ 0–5 combat
   rebalance**. Ends at a mandatory playtest checkpoint. Kill the seeded iron ore here if Mining
   (M6) hasn't landed first.
5. ⬜ **E5 — Item modifiers (affixes)**. Genome → eligibility/weight/tier → rolling; innates;
   Genome Readout; ailment application chances finally get their source; move-modifier pools get
   their author. Item Lab.
6. ⬜ **M6 — Loop closers**. Mining (ore has no source — delete the stash seed), loot tables on
   the two goblins per §12.4 ecology, character XP/levels + a minimal build-selection screen
   (the build is debug-cycled today). Makes the slice self-sustaining end to end.
7. ⬜ **E6 — Profession tools + yield pipeline**. Tool slots, tool forms, the outcome pipeline +
   Yield Log ("mostly free once scoped modifiers exist" — they do).
8. ⬜ **E7 — Crafting operations + Overreach**. Anneal/Etch/Scour/Reforge/Bind/Temper/Fracture +
   the escalating-Ruin casino and Anomalous modifiers. Caps the crafting fantasy.

## After the slice (unordered)
Realm breadth (affixes, tiers, location types, preparation screen) · enemy roster to 8–10 ·
auto-combat (player on the AI-profile machinery) · offline progress · economy/vendors (NEEDS
DESIGN) · Hideout · species roster · remaining 40 suffix mechanics · crafting P4/P6 ·
Application-layer extraction from `GameRoot` · production UI.

## Guardrails
Keep `dotnet test` green (602 now) and the build at 0 warnings. Content is data; never author a
combination. Nothing authoritative in `GameRoot`/UI. Commit only when asked; on `main`.
