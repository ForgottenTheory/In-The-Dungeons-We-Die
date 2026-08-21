# ROADMAP.md

Status: ✅ done · 🔄 in progress · ⬜ not started. Kept short on purpose — history lives in git,
system detail in `docs/game-overview.md`/`docs/code-map.md`, status in `docs/GDD.md` §19.

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
- ✅ **P4 — the 20-profession expansion pass** (`docs/professions.md`): the roster to **20 /
  194 actions**, the **Discover → Pursue/Ignore** active layer (32 opportunities), **offline
  progression** as a first-class path (save **v7**), success chance for Hunting/Thieving,
  Farming plots, the Agility training course, Cartography → Realm Knowledge, and the Assay
  reveal ladder. 79 new materials; ecosystem enforced by test.

- ✅ **The identity crafting redesign, Phases 1–7** (D42–D54, 2026-08-20/21): the identity
  model beside the old system → the ten-verb bench → the item-effect pipeline and the Identity
  Forge → the full 1,448-material migration (no cull) → professions train at the bench → the
  Phase 6 presentation pass (player language everywhere, D53) → **Phase 7 deletion**: the
  property algebra, genome/affixes, trait/essence layers, their UI, tests, content and docs
  removed whole; all 23 forms identity-forgeable; save settles at **v14** (progression
  survives, items reset — D49/D54). The old system no longer exists in the repo.

## The plan to the full-fantasy slice
Target: prepare → enter → fight *as your build* → extract → **fabricate gear from what you
brought back** → feel the difference next run. Dependency-ordered; each milestone is one
coherent run.

1. ✅ **C1 / C2a+C2b / R0–R4 — the first crafting stack and the presentation correction.**
   Superseded and deleted by the identity redesign (D42–D54, above); what carried forward:
   the three-languages architecture (D30, `docs/presentation-architecture.md`), the lane
   alignment + D-07, Parry, Barrier absorption, status potency/duration keys, the
   `DamageMitigated` event, move-modifier grants, and the combat machinery every retired
   affix used to prove — all alive, now fed by identity sentences.
4. ⬜ **The playtest checkpoint** *(user-driven)*. Play the full loop in the identity
   language (gather → bench verbs → forge → fight with sentences live), then land the whole
   parked balance backlog in one pass: **every identity-system number** (all three `*Tuning`
   classes say provisional — verb costs/risks, payload ranges and weights, Signature odds,
   XP), plus the pre-redesign residue: Fireball one-shots, Bastion damage, the casting-speed
   decision (GDD §18 #16), profession intervals/XP across all twenty, opportunity
   odds/risk/cost, offline caps, plot grow times, course bonus magnitudes, synergy rates,
   the auto-combat brains.
5. 🔄 **M6 — Loop closers**.
   **Loot ✅ (2026-08-17, `docs/loot.md`, D31).** One data-driven table shape for every source —
   enemies (composed family+role+actor), gathering nodes, event chests and profession actions.
   Guaranteed/chance/weighted-draw rules, quantity ranges, depth+tag conditions, nested shared
   tables, rarity read from the material's own tag. 34 tables over the **existing** material
   library (zero new materials). D28 held by test — no table yields finished equipment; D29.3
   held by test — no profession drop table reaches essence or coin. Active gathering reaches
   entries passive cannot, structurally. Elite/boss spoils wired and tested before any elite
   exists. **Gold** ships as the sole currency on `Inventory` (save **v8**) and nothing spends it
   yet, by design. The Dark Forest grew to 15 nodes so the tables have somewhere to live.
   **Equipment breadth ✅ (2026-08-17, D32).** `forms.json` 3 → **8 forms across all 7 slots**
   (`EquipmentSlot` gained Offhand/Head/Hands/Feet/Trinket; `Armor` → `Body` with the project's
   **first save migration**, v9). Fabrication itself untouched — slots, apertures, stat maps,
   dormancy, projection, genome and modifier rolls all unchanged. Worn armour is now the sum of
   the loadout, with coverage authored in each form's `stat_map` rather than coded per slot.
   Four new form validation rules; the Focus needed no new affix content.
   **Enemy roster complete ✅.** Every name in the design list ships: **481 actors** across 26
   families and 7 roles. Wave 2 needed no new families -- the layer built for wave 1 absorbed all
   ~350 remaining names, which is the layering paying for itself.
   **Rings ✅ (D33).** `Ring1`/`Ring2` appended (free — slots persist by name, so no migration)
   and a ninth form, the Ring, reading `conductivity`/`affinity` — the two properties no form
   read before it. One form fills both positions via `EquipmentSlots.InterchangeablePositions`;
   authoring a second near-identical ring form to fill `Ring2` is explicitly rejected.
   **Weapons ✅ (D34).** 9 → **17 forms, ten of them weapons**; ~120 further weapon names ship as
   `name_variants`, picked deterministically from the item signature and cosmetic by
   construction. Nine archetype moves. A new rule catches forms granting moves they cannot fire
   — which is how the Warspear's dead Skewer was found.
   **Weapon list complete ✅ (D34 addendum).** 23 forms, **16 weapons**; every name in the
   design list is placed. Six more archetypes — Halberd, Shortsword, Javelin, Sling, Whip,
   Knuckles — each earning its place by what it refuses to read.
   **Content library ✅ (D35).** Materials 559 → **1448** (582 plants, 307 ores/gems) and realms
   1 → **164**. Generated from name lists with the anti-tiering rule encoded in the generator and
   asserted by test. Realms ship as walkable rosters with **no encounters wired** — deliberate.
   **Gatherable ✅.** 117 new gathering actions (194 → **311**) put every one of the 889 behind
   Farming, Mining, Forestry or Fishing, bucketed by theme and gated by the rarest member.
   **Legacy gap closed ✅ (D36).** The 229 stranded legacy materials are wired too — 348 actions
   plus anatomy on six enemy family tables, with essence routed to Realm drops rather than
   professions. `EveryRawMaterialHasASource` is now exact. **Still unbalanced.**
   **Phase 6 — the Dark Forest is finished ✅ (D37).** 15 locations / 2 depths → **31 / 3**, with
   Camp, Shrine, Merchant and Hazard node kinds, three hidden nodes, an elite (Grask) and the
   first boss (Thornheart). **Realm Knowledge now unlocks five insights** — options, never
   damage. Deeper pays rarer: average drop rarity 0.78 at depth 1, 1.75 at depth 3.
   **First balance pass ✅ (D38).** Coherence, not feel: measured against a 59 HP fresh
   character. Fixed a depth-2 fight weaker than depth 1 (Hexer 24 → 36 HP) and a knowledge
   ladder one run completed (thresholds ×~10, now ~8 thorough runs). **Feel is still unplayed.**
   **Phase 7 — Realm Preparation / loadouts ✅ (D39).** The bridge the loop diagram has carried
   as `[PLANNED]` since the first commit: `Hideout → Realm Preparation → Enter Realm`. A
   `RunLoadout` holds **only** the destination and the pack — worn `Equipment` is already the
   gear half and is not copied. Packing closed a real hole: a Healing Salve in the Stash was
   **unreachable inside a Realm**, because combat consumes from the run bag and the run started
   empty; supplies now transfer at entry and are unsecured from that moment. `RealmBriefing`
   +`RealmFieldwork` are knowledge-redacted read-models in `Dungeons.Presentation` — every gate
   through `RealmKnowledgeLevels.Reveals`, so the screen and the in-run intel cannot disagree.
   The Realm tab is now two screens that swap. Save **v10** (a v9 save loads with no loadout).
   Gear problems never block entry — the anti-soft-lock fence is a test. **Profession tools are
   deliberately absent**: they are E6, and a slot with no mechanic behind it breaks rule 7.
   **Phase 8 — the progression pass ✅ (D40).** Every progression track now changes what the
   player can do, and none of them merged into one power number. **Mastery**: the numbers moved
   into `game/data/mastery/` as one shared six-rung ladder, mastery level is completions (linear,
   ceiling 99 — a bending curve would have been a balance pass in disguise), and **preservation
   and doubling ship as unlocks** rather than creeping percentages. `RequiredMasteryLevel` gates
   four high-risk opportunities — below the gate they are *not rolled at all*. **Realm Knowledge**
   gained the two GDD §11.4 items it was missing, bracketing D38's five thresholds without moving
   them: `CommonResources` (what a place yields, walked out of existing loot tables) and
   `DeepEntry` (portal targeting — start a run at a deeper door). **Character XP** finally has a
   source: Realm work only, so `AttributeGrowth` and the 4.0-point budget run for the first time.
   Levelling raises the ceiling and never heals. Save **v11**. The deliverable that outlasts the
   pass is `ProgressionEcosystemTests` — no dead track, form acquisition exempt **by name**.
   **Still open in M6:** form acquisition (D29.2) — the schematic *items* drop today, but
   `forms.json` still needs its acquisition field, a persisted known-forms list and a validator
   rule, on the learned-list precedent. **This is the one track `ProgressionEcosystemTests`
   exempts by name.** Also a minimal build-selection screen (the build is debug-cycled today) —
   character XP and levels themselves landed in Phase 8.
   **Phase 10 — offline + automation ✅ (D41).** The two halves of the game finally coexist.
   **Professions:** `ProfessionBenefits` folds the mastery ladder and a new **synergy table**
   (`synergies/` — 13 cross-profession rows following existing material chains, plus 2 global rows
   that read **total** profession level) into the one question the execute path asks — so
   cross-skill and account passives arrived with **no change** to `ActionResolver` or
   `ProfessionSystem`, and E6's tools are a third field on the same seam.
   **Auto-repeat:** the passive selection is standing — it waits when the materials run out and
   resumes by itself; only Stop clears it. Temporary problems wait, permanent ones refuse.
   **The return:** `AwayProgress` + `Presentation/AwayReadout` + a summary panel — completions,
   crops, merged items, XP, **levels gained**, and an honest sentence about what cut the payout
   short. Autosave on quit (guarded), because offline time is measured from the save stamp.
   **Auto-combat (GDD §5.7, D-07):** `AutoCombatPilot` puts an authored brain on the *player*
   combatant and asks `CombatEncounter.ChooseMoveFor` — the enemy method — then presses the same
   buttons a hand would. **No second resolver and no damage multiplier**: its whole handicap is
   `reaction_ticks`, which forces a stance `R` ticks early and therefore outside every tight
   window. `MasteryBenefitKind` → `ProfessionBenefitKind` (members untouched). **Save unchanged at
   v11.** Still unbalanced: synergy rates, global thresholds, all three brains.
6. ⬜ **E6 — Profession tools + yield pipeline**. Tool slots, tool forms, the outcome pipeline +
   Yield Log ("mostly free once scoped modifiers exist" — they do). **P4 already ships the
   components** (Smithing's tool head, Artifice's haft/mechanisms/lenses) and the Agility
   course's `CourseBonusKeys`, which nothing reads yet — E6 is where both get consumed.
7. ⬜ **E7 — Crafting operations + Overreach**. Anneal/Etch/Scour/Reforge/Bind/Temper/Fracture +
   the escalating-Ruin casino and Anomalous effects — now to be designed over the identity
   system's sentence vocabulary (the affix layer it originally targeted is gone, D54). Caps
   the crafting fantasy.

## After the slice (unordered)
Realm breadth (affixes, tiers, location types) · enemy roster to 8–10 + the
elite/boss variant layer (the D26 fold seam exists) · **fully unattended Realm runs** (auto-combat
landed in Phase 10; travel, extraction decisions and the run bag did not) · economy/vendors (NEEDS DESIGN — Thieving deliberately ships no currency) · Hideout ·
species roster ·
remaining 40 suffix mechanics · the Fighter identity hook (§18 #15) ·
Application-layer extraction from `GameRoot` · production UI.

## Guardrails
Keep `dotnet test` green (**1,011** now — the identity migration retired the old system's suites) and the build at 0 warnings. Content is data; never author
a combination. Nothing authoritative in `GameRoot`/UI. Commit only when asked; on `main`.
