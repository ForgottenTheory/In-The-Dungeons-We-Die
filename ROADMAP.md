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
- ✅ **M1 — editor verification + tuning** (combat-tab move buttons/gauges/hit trace wired and
  user-verified; D-20 floor applied; balance findings recorded, deferred).
- ✅ **M2′ — the universal move library + acquisition + enemy framework** (D25/D26): 27 moves,
  19 technique items → persisted learned list (save v5), per-effect target overrides, and
  Family+Role+AiProfile enemy composition via `ActorResolver` — 3 data-composed goblins incl.
  the Hexer.
- ✅ **P1–P3 — professions to the §7.1 slice target**: 8 professions / 26 actions, Mining kills
  the iron-ore seed, cross-feeding pinned by test, prepared materials for Cooking/Alchemy.

## The plan to the full-fantasy slice
Target: prepare → enter → fight *as your build* → extract → **fabricate gear from what you
brought back** → feel the difference next run. Dependency-ordered; each milestone is one
coherent run.

1. ⬜ **C1 — Crafting P2 traits + P3 essence**. State traits with cap/displacement/supersession;
   essence + resonance strain + Attune. Prerequisite genetics for everything below.
2. ⬜ **C2 — Fabrication + the scale reconciliation** *(highest risk)*. Form templates with named
   slots/apertures/stat maps; materials → equipment instances; **the 0–100 ↔ 0–5 combat
   rebalance**. Ends at a mandatory playtest checkpoint. **The deferred balance backlog lands
   here or in a dedicated tuning pass before it**: Fireball one-shots, Bastion damage, the
   casting-speed decision (GDD §18 #16), profession interval/XP numbers.
3. ⬜ **E5 — Item modifiers (affixes)**. Genome → eligibility/weight/tier → rolling; innates;
   Genome Readout; ailment application chances finally get their source; move-modifier pools get
   their author (incl. chains for `mech:chain` moves). Item Lab.
4. ⬜ **M6 — Loop closers**. Loot tables on the three goblins per §12.4 ecology (technique items
   join them — they are debug-granted today), character XP/levels + a minimal build-selection
   screen (the build is debug-cycled today). Makes the slice self-sustaining end to end.
5. ⬜ **E6 — Profession tools + yield pipeline**. Tool slots, tool forms, the outcome pipeline +
   Yield Log ("mostly free once scoped modifiers exist" — they do).
6. ⬜ **E7 — Crafting operations + Overreach**. Anneal/Etch/Scour/Reforge/Bind/Temper/Fracture +
   the escalating-Ruin casino and Anomalous modifiers. Caps the crafting fantasy.

## After the slice (unordered)
Realm breadth (affixes, tiers, location types, preparation screen) · enemy roster to 8–10 + the
elite/boss variant layer (the D26 fold seam exists) · auto-combat (player on the AI-profile
machinery) · offline progress · economy/vendors (NEEDS DESIGN) · Hideout · species roster ·
remaining 40 suffix mechanics · the Fighter identity hook (§18 #15) · crafting P4/P6 ·
Application-layer extraction from `GameRoot` · production UI.

## Guardrails
Keep `dotnet test` green (626 now) and the build at 0 warnings. Content is data; never author a
combination. Nothing authoritative in `GameRoot`/UI. Commit only when asked; on `main`.
