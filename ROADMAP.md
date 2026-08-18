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
- ✅ **P4 — the 20-profession expansion pass** (`docs/professions.md`): the roster to **20 /
  194 actions**, the **Discover → Pursue/Ignore** active layer (32 opportunities), **offline
  progression** as a first-class path (save **v7**), success chance for Hunting/Thieving,
  Farming plots, the Agility training course, Cartography → Realm Knowledge, and the Assay
  reveal ladder. 79 new materials; ecosystem enforced by test.

## The plan to the full-fantasy slice
Target: prepare → enter → fight *as your build* → extract → **fabricate gear from what you
brought back** → feel the difference next run. Dependency-ordered; each milestone is one
coherent run.

1. ✅ **C1 — Crafting P2 traits + P3 essence** (`64418bf`): 16-trait library with cap 3 /
   displacement / supersession; the seven typed essences, resonance capacity/strain, Attune.
2. ✅ **C2a+C2b — Fabrication + the scale reconciliation** (`bcb0c6e`): form templates
   (3-slot Longsword, Buckler, Vest), aperture-gated traits with dormancy, derived equipment
   archetypes persisted, iron-sword parity pinning the 0–100 → combat-unit calibration,
   per-slot component UI.
3. 🔄 **R0–R4 — the presentation correction (D30)** *(absorbs E5)*. The three-languages
   architecture (`docs/presentation-architecture.md`): raw simulation numbers off normal play
   surfaces, a semantic crafting grammar, items that pay off in gameplay language.
   **R0 ✅** audit + doc. **R1 ✅** the Core semantic layer (tiers/trends/risk bands/slot-fit;
   glyph+gloss on `PropertyDefinition`; typed changes on `CraftProjection`). **R2 ✅** bench UX
   (grouped preview panel, glyph strips, Advanced toggle). **R3 ✅** fabrication `Project` +
   slot-fit lines + the §6 reveal (`InstanceLabel` retired from player surfaces). **R4a ✅**
   the lane alignment (`combat.resist.physical` + six aspect keys wired into the pipeline with
   cap/floor; **D-07 executed** — evade.chance + avoid.lane, dodge.chance retired). **R4b ✅**
   the Genome (persisted, **save v6**) → eligibility/weight/tier → seeded rolling; innates as
   the deterministic layer; 28 representative affixes over already-resolving families —
   **ailment application chances finally have their source**; triggered + stat grants through
   the existing rule/modifier seams; preview genome translation ("Supports: …"); affix
   validator rules + seeded distribution tests; debug reroll. **R4c-1 ✅** the retaliation
   family as pure content over the rule engine (when-hit / on-block / after-dodge / poison
   barbs, e2e-pinned — thorns needed **zero** new combat machinery); Evade live (untelegraphed
   only, D-07) + lane avoidance (per-packet negation) + flat lane penetration after the cap
   (overcap-vs-exposure semantics pinned); capped/raw on the armour summary (D-05a minimal);
   the on-crit trigger family (CriticalLanded existed all along). 37 affixes shipped.
   **R4c-2 ✅** Parry (gear-granted per D-26 — `parry` tag on the Buckler form, 3-tick window,
   negation + heavy stagger + the `Parried` event; UI button appears only when gear grants
   it), **Barrier absorption** (the HitPipeline debt closed — soaks before Health,
   `BarrierBroken` on shatter), status potency/duration keys wired at the encounter seam,
   the `DamageMitigated` event + reflect-% retaliation, move-modifier affix grants (the
   11-op system's third grantor — Emberbrand ships as the first data move-mod), and the
   parry/reflect/ward/potency/duration/emberbrand affixes — **43 affixes total**. Stored
   retaliation + inversion/ignore stay with E7 (Exotic tier).
4a. 🔄 **C2c — the playtest checkpoint**: the machine half is **done** (per-rule
   `ValidateForms` failing-content tests; the D28/D29 first-session sufficiency audit —
   which caught and then confirmed the boar-hide bonus faucet closes the binding chain; the
   D29.3 essence source audit — overlap pinned to the shock-eel rung, flagged for the
   noncompete check). **The playtest half is the user's by standing decision**: play the
   full loop in the new language (mine → smelt → infuse → attune → fabricate → roll → fight
   with thorns/ailments/parry live), then land the whole balance backlog in one pass —
   Fireball, Bastion, casting-speed (§18 #16), profession interval/XP, fabrication
   constants, affix-roll odds and counts, the eel-rung essence rates — plus, after P4,
   profession intervals/XP across all twenty, opportunity odds/risk/cost, offline caps, plot
   grow times and the course's bonus magnitudes. Save is **v7** (a v6 save still loads).
4. ⬜ **C2c — the playtest checkpoint** *(user-driven; moved after R4, user call 2026-08-16)*.
   Play the full loop in the new language (mine → smelt → infuse → attune → fabricate → fight);
   land the whole parked balance backlog in one pass: Fireball one-shots, Bastion damage, the
   casting-speed decision (GDD §18 #16), profession interval/XP, fabrication calibration
   constants, affix-roll odds. Also close the small gap: per-rule failing-content tests for
   `ValidateForms`. Plus the D28/D29 audits: first-session sufficiency, essence sources.
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
   **Still open in M6:** form acquisition (D29.2) — the schematic *items* drop today, but
   `forms.json` still needs its acquisition field, a persisted known-forms list and a validator
   rule, on the learned-list precedent. Character XP/levels + a minimal build-selection screen
   (the build is debug-cycled today).
6. ⬜ **E6 — Profession tools + yield pipeline**. Tool slots, tool forms, the outcome pipeline +
   Yield Log ("mostly free once scoped modifiers exist" — they do). **P4 already ships the
   components** (Smithing's tool head, Artifice's haft/mechanisms/lenses) and the Agility
   course's `CourseBonusKeys`, which nothing reads yet — E6 is where both get consumed.
7. ⬜ **E7 — Crafting operations + Overreach**. Anneal/Etch/Scour/Reforge/Bind/Temper/Fracture +
   the escalating-Ruin casino and Anomalous modifiers. Caps the crafting fantasy.

## After the slice (unordered)
Realm breadth (affixes, tiers, location types, preparation screen) · enemy roster to 8–10 + the
elite/boss variant layer (the D26 fold seam exists) · auto-combat (player on the AI-profile
machinery) · economy/vendors (NEEDS DESIGN — Thieving deliberately ships no currency) · Hideout ·
species roster ·
remaining 40 suffix mechanics · the Fighter identity hook (§18 #15) · crafting P4/P6 ·
Application-layer extraction from `GameRoot` · production UI.

## Guardrails
Keep `dotnet test` green (**969** now) and the build at 0 warnings. Content is data; never author
a combination. Nothing authoritative in `GameRoot`/UI. Commit only when asked; on `main`.
