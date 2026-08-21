# HANDOFF.md

For the next Claude session. Read `CLAUDE.md`, then this.

## ⭐ THE BIG THING — migration Phases 3, 4 and 5 landed in this context

The **Identity + Signature redesign** (D42–D52) advanced three phases in one context, all beside
the untouched old system, suite green throughout:

- **Phase 3 — item generation (D50/D51).** ONE unified pipeline, `ItemEffectResolver`, emitting
  three categories kept apart on the item: **identity floor expressions** (guaranteed,
  deterministic, rank-deepened) · **ordinary generated effects** (weighted draws from a scored
  table the preview shows — the table IS the draw distribution) · **optional Signatures**
  (earned via theme resonance/quality/overfill; 1–N coherent sentences). Built: the **payload
  registry** (bare keys, machinery-proven bindings, families+rungs, one-floor-per-identity
  discipline — all validator-enforced), form identity fields (`identity_cap`, `base_reads`,
  per-slot `identity_priority`, `generation_profile`), `IdentityEquipmentComposer` (D51
  union/cap/dormancy; base delivery parity-pinned to the authored Iron Sword through the live
  `EquipmentResolver` seam), 11 behavior **assemblers** compiling sentences into existing
  grants, `IdentityFabricationEngine` (mint → `ItemInstance` carrying sentences/delivery/
  identity split), **save v13**, the **Identity Forge** panel beside the old assembly, and the
  full equip seam (stat grants, rules, gauges, move modifiers attach like affixes and swap with
  gear).
- **Phase 4 — the material database (D52).** The expected cull was **consciously declined**
  (measured: 1,448 materials, 1,446 referenced by shipped profession/loot content). All 1,448
  migrated: a throwaway line-oriented derivation tool (deleted; its rules live in
  `MaterialLibraryMigrationTests`) drafted capacity/base/latents; hand tiers added **53
  active-identity materials** (motes r1 · essences/hearts/runes r2 · cores r3; Earthen got
  elemental earth, Resonant the catalyst, Pure the salts), **a floor payload for every one of
  the 24 identities** (28 payloads), **46 curated profiles**, plant-true seed latents, and the
  **acquisition fence** (D29.3 translated: gathering faucets never passively pay
  active-identity stock — it caught `quarry_arcane` paying an arcane core every completion;
  three new opportunities took the evicted payouts, pinned scale 36→39).
- **Phase 5 — professions.** **The bench trains**: `VerbActionDefinition.experience`
  (validated two-sided: gated⇒pays, ungated⇒cannot), awards through the shared
  `ProfessionProgress` ledger on success/fracture/destruction alike (refusals pay nothing),
  level-ups surface at the bench, previews name the pay. **Mastery steadies the hand**: per-
  action mastery shaves both risk chances via `VerbRequest.RiskReduction` (built in the shared
  gate path — preview/commit parity free; engine-clamped at 45% — skill narrows variance,
  never deletes it). **The D48 matrix is content**: 53 verb actions across 11 professions at
  their own stations, pinned (incl. Runecrafting as the ONLY identity-scoped profession);
  domain Restores eat salvage stock; Expand costs its catalysts; **preparation = activation**
  (Process merges the output's innate identities: drying raw emberleaf lands on the authored
  dried form with Ember active — pinned).

Decisions this context: **D50** (one pipeline, three categories — "Signature" is the special
layer, never a rename of affixes), **D51** (union + form cap + dormancy; readable slot
priorities, never percentage apertures; material capacity ≠ form cap), **D52** (no cull;
derivation + hand tiers). All recorded in DECISIONS.md and mirrored in
`docs/identity-foundation.md` (§8, §8.1, §15) and `docs/transformation-verbs.md` (§4, §8.3).

## Repo / build state

- Branch `main`, working tree **clean**. This context's work — Phases 3, 4 and 5 together —
  landed as **one combined commit** on top of `16fed3f` (Phase 2 complete): the three phases
  interleave inside shared files (DECISIONS, the foundation doc, the validator), so a
  per-phase split could not have kept every commit green. Pushing is the user's job.
- Green at handoff: **`dotnet build InTheDungeonsWeDie.slnx` clean (0 warnings) ·
  `dotnet test` → 1,330 passing.** ContentStudio builds clean, its 14 tests pass (registry got
  the `signature_payloads` type; material/form/verb editors regenerate from the Core types).
- **Save schema is v13** (v12 + identity-minted item fields: sentences, base delivery,
  expressed/dormant; derived equipment definitions ride the existing `EmergentEquipment` list
  via the shared `equip.emergent.` prefix — `equip.emergent.i<hash>` for identity mints).
  The D49 break (progression survives, items reset) still waits for Phase 7.
- Godot is **not** on PATH — verify with `dotnet build`/`dotnet test`; the user runs the game
  from the Godot 4.7.1 editor.
- **Workflow (user-set):** concise reports; plan → approve → build → report; decisions through
  AskUserQuestion sign-offs, landing in DECISIONS + the foundation doc the same turn. Commit
  only when asked. **Do not spawn subagents unless the user asks** (standing). PowerShell 5.1:
  no double quotes inside `git commit -m` here-strings.

## ⭐ START HERE next session

1. **The user was about to say "start phase 6" when the context ended — Phase 6 is next:
   Presentation + UI** (§15): identity/signature readings replace property readings; Assay
   re-aimed at detecting latents and reading potential. Known scope waiting for it:
   - The **semantic pass over the bench and forge** — `VerbBenchPanel` shows the engine's step
     text and `IdentityForgePanel` shows engine vocabulary (`on_block → store → Bulwark 0.15`)
     by explicit Phase 6 deferral (D30: the panels deliberately invented no second vocabulary).
     The one-way `Dungeons.Presentation` layer is where the player language lives.
   - **Item tooltips**: `ItemReading` renders affixes/properties but NOT identity sentences —
     an equipped identity sword currently shows nothing about its effects. Sentences are
     gameplay-language already (damage, Burn, Barrier); Phase 6 gives them player wording.
   - **Assay re-aim** (D48: information only — latents, profile hints, capacity/condition
     readouts; §14 #3 profile-visibility is the open decision that belongs to this phase).
   - Dormant identities, stability/condition ladders, and the scored candidate table all need
     their player-facing surfaces decided.
2. **Editor verification backlog (user's side, never done for Phases 2–5):** the Identity
   Bench at five stations (now 11 stations routing 53 actions); the Identity Forge at assembly
   stations (plain iron longsword should swing like the authored Iron Sword); XP/level-up
   lines at the bench; save v13 roundtrip (mint → save → reload → sentences intact).

## The identity-system rules that must not erode (new ones first)

- **D50's taxonomy:** Floor/Generated/Signature/Drawback stay distinct on the item; a
  Signature is *earned*, never the blanket word for generated effects.
- **D51's selection stays readable:** slot priority → rank → contribution → id. No percentage
  apertures on the floor, ever. Material capacity and form `identity_cap` are different
  concepts; neither implies the other.
- **The assembler D30 fence:** `drainResource` has NO handler, so **drain compiles as
  damage+restore** and **store as gauge-feed+band** (release-on-full waits for a gauge-spend
  effect kind — documented in `SentenceAssemblers` and foundation §7.3). `craft.quality` and
  `loot.quantity.mult` are **declared-but-unread** modifier keys — nothing may bind them
  (Pure floors on `profession.preserve.chance`, Charmed on `attr.luck`).
- **Floor discipline:** every identity that owns payloads has exactly ONE rung-1 floor
  (validator); every roster identity owns one (test). The preview's scored table IS the draw
  distribution (per-payload diversity cap keeps breaches visible).
- **The structural discipline (D52):** migrated structural stock must author base; only
  structural stock may; Bite only on edge-capable forms (raw ore holds no edge — smelting
  earns it). `TagFamilies.StructuralForms`/`EdgeCapableForms` are the closed sets.
- **The acquisition fence:** no gathering faucet passively pays active-identity materials —
  active stock is opportunities, Realm loot, or processing chains whose inputs already paid.
- **Steadiness is capped** (`RiskReductionCeiling` 0.45) and lives in the shared gate path —
  never let preview and commit compute mastery separately.
- **Authored equivalence + preparation=activation:** mundane chains land on authored ids;
  Process merges the output's innate identities (that's the mechanism, don't "fix" it).
- All prior invariants stand: the grammar's D30 fence (triggers→published events;
  detonate/spread/bloom parked BY NAME) · roster pinned to D44's 24 · solo-complete,
  chain-enhanced (D48) · rank never decays passively · stability derived, never stored ·
  fingerprint excludes history/profile/base · themes never player-facing · numerals never
  player-facing · D20 stacking · preview parity everywhere · 0 warnings · validator-before-
  content with failing-content tests.

## Deferred / parked — do not relitigate

- Grammar gaps by name: behaviors detonate/spread/bloom; effect kinds `consumeStatus`,
  area-status, delayed, `cleanse`; the `LootResolver` luck seam (Charmed's real consumer).
- Named identity evolution (§14 #4) · Emergent Phenomena (seam named in `ItemEffectResolver`,
  deliberately empty; overfill raising Signature odds is its only designed input).
- Verb-level open details (transformation-verbs §7): Fuse derived capacity/condition,
  Restore ceiling (Worked, virgin-only Pristine), fracture-targets-newest — all provisional.
- Phase 7: old-system deletion, doc rewrites, the D49 save break, schema settling.
- Pre-redesign deferrals: form/schematic acquisition (D29.2), profession tools (E6),
  consumable forms (P5c — Cooking's named dead-end), `MainMvpUI` rename, economy.
- **Every identity-system number is provisional** (all three `*Tuning` classes say so).

## Known debt and filed items

- `PROJECT_STATE.md` / `SYSTEM_INDEX.md` predate the redesign entirely — refresh or delete.
- Old-system surfaces are still the primary UI (assembly panel beside the Identity Forge,
  property readings, Assay unredacting numbers) — Phases 6/7 retire them.
- ContentStudio still has no registry-vs-`LoadAll` parity test (cheap fence, unbuilt).
- `VerbBenchPanel`/`IdentityForgePanel` pickers reset selection on refresh (filed).
- Two identical sentences on one item share a `RuleId` (cooldown bookkeeping collision —
  harmless while sentences don't cool down; noted in `ItemEffectSentence`).
- Two store-behavior sentences with the same payload would share a gauge name (noted in
  `SentenceAssemblers.StoreGauge`; unlikely with current content).
- Verb-action XP values and all payload magnitude ranges are first guesses; the §8.3 gate
  waves (L1/5/12/40/60/70) likewise.

## The method notes that keep paying

Everything from earlier contexts stands (worked examples · perturbation tests · tests that
express design rules · validator before data · structural fences over exception lists ·
documentation is a claim, so measure it — this context's rarity grep missed `very_rare` and
the doc said "~60" where the measure said 53). This context added three:

- **Fences catch their author.** The floor-discipline validator rejected my own first payload
  file (a rung-2 floor); the acquisition fence's first run caught a real leak the old essence
  fence could not see (`quarry_arcane`'s guaranteed arcane core). Write the fence before the
  content it polices, then believe it over yourself.
- **Trace thresholds must agree across surfaces.** Assembly dilutes provenance by mass share —
  the item-side name threshold AND the item-side profile trace both had to drop below the
  material-side ones, or the sword said "Oakbound" while generation forgot oak. When a derived
  value crosses a boundary, check every consumer of the threshold.
- **When the engine already does the right thing, let it.** Preparation=activation was not
  designed — it fell out of Process's output-innate merge, discovered by tracing the
  fingerprint math before authoring the Process pairs. Trace first; the mechanism may already
  be the design.
