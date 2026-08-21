# HANDOFF.md

For the next Claude session. Read `CLAUDE.md`, then this.

## ⭐ THE BIG THING — the identity migration is COMPLETE (Phases 6 + 7 landed here)

The **Identity + Signature redesign** (D42–D54) finished in this context. Phase 6 gave the
system its player language; Phase 7 deleted the old property system whole. **The identity
system is now the only crafting system in the repo** — there is no coexistence seam left.

- **Phase 6 — Presentation + UI (D53).** All in `Dungeons.Presentation`, one-way,
  unit-tested (docs/presentation-architecture.md §5.2): `SentenceReadings` (one effect
  sentence → one player line, truthful to what `SentenceAssemblers` compiles; modifier units
  derived from the key registry, never a key list) · the item card/strip identity layer
  (`Identities:` with §4 rung words — *improved/advanced/build-changing*, numerals banned by
  D44; `Guaranteed:`/`Signature:`/`Drawback:` labels keep D50's taxonomy apart) ·
  `MintReadings` (the forge preview: **likelihood words** for the draw table measured against
  the uniform share — D53; breach rows say "beyond its families"; exact scores behind an
  Advanced toggle) · `VerbReadings` (refusals in words; preview/outcome lines **diffed from
  the engine's own states** — awakens/settles in/deepens/is ejected; engine step text is the
  Advanced voice) · `IdentityMaterialReadings` (the bench inspector: every §11.2 facet in a
  sentence) · `AssayLens` re-aimed (D45/D48): **Vessel → Latency → Latents → Leanings →
  Potential** on the same five rungs; stakes + overfill never gated; themes never shown;
  Potential quotes `ItemEffectResolver.FloorPayloadOf` — the same rule generation mints from.
  Phase 6 also caught shipped `bulwark` authoring a delta (0.08–0.2) on a multiplicative key
  (an 85% Block *nerf* wearing a buff's text) — fixed to factors behind a new validator fence.
- **Phase 7 — Deletion + docs (D54).** Sequenced discovery first: only Longsword+Buckler had
  identity fields, so **all 23 forms** got caps/base_reads/priorities/generation profiles
  (every-form-forgeable pinned in `IdentityContentTests`), and the D34 ~120-noun name library
  was ported (`IdentityFabricationEngine.FormNoun`, deterministic from the derived id — "Iron
  Spatha" every time). Then the deletion: reaction engine/algebra/quantization, genome +
  affix layer, traits, essences, `MaterialState(+Resolver)`, old fabrication, the property
  presentation stack, `CraftingBenchPanel` + `EquipmentAssemblyPanel`, six content folders
  (`processes/ traits/ essences/ affixes/ name_grammar/ properties/`), ~30 test files.
  Material JSON stripped of `properties`/`essence` (1,448 entries); forms stripped of
  `stat_map`/`trait_expression`/`trait_cap`; stations route `verb_actions` + `has_assembly`
  only (the forge offers every form at the 6 assembly stations — no per-station form lists).
  The legacy fixed-interaction path (Healing Salve) survives, slimmed to stackable results.
  **Save v14**: `ItemInstance` is identity-only; the v9 Armor→Body shim retired as
  unreachable. ContentStudio registry/schemas/balance views follow (14/14 green). Docs: six
  superseded docs + `PROJECT_STATE`/`SYSTEM_INDEX` deleted; `crafting-overview.md` rewritten
  as the identity-stack map with real counts; CLAUDE/code-map/game-overview (crafting
  chapters rewritten)/GDD/ROADMAP/loot/json-schema/how-it-plays/presentation-architecture all
  refreshed.
- **The deletion caught a real Phase 3 bug:** `SentenceAssemblers` aimed retaliate at
  `TriggerSource`, but on the defensive events it fires from (struck/blocked/parried/dodged)
  the wearer is the event's SOURCE and the attacker its TARGET — worn retaliation pointed at
  the wearer's own side and had never been live-fired. Fixed to `TriggerTarget` (matching the
  old bramble affix); the probe lives in `tests/Combat/RetaliationAndBarrierTests.cs`.

Decisions this context, all recorded in DECISIONS.md + the foundation doc same-turn:
**D53** (profiles read as leanings in words; the draw table speaks likelihood words; scores
Advanced-only) · **D54** (full item reset at v14; superseded docs deleted, not bannered;
PROJECT_STATE/SYSTEM_INDEX deleted and folded; forms finish the migration before anything
dies).

## Repo / build state

- Branch `main`. Phases 6 + 7 land as **one commit** on top of `e2569d2` (Phases 3–5) — the
  two phases interleave through the same files and the deletion only builds as a whole.
  Pushing is the user's job.
- Green at handoff: **`dotnet build InTheDungeonsWeDie.slnx` clean (0 warnings) ·
  `dotnet test` → 1,011 passing** (1,378 → 1,011: 367 old-system tests retired with their
  systems; Phase 6/7 added ~50 new ones). ContentStudio builds clean, its 14 tests pass.
- **Save schema is v14** (D49/D54 executed): loading any pre-v14 save keeps every progression
  section — professions/mastery, realm knowledge, character XP, learned moves, discoveries,
  plots, course, **gold** — and drops every item section (stacks, instances, worn gear, both
  emergent registries, the packed loadout); the starter-kit rule re-equips.
  **⚠ The user's existing save will item-reset on next load. This is D54, approved — do not
  "fix" it.**
- Godot is **not** on PATH — verify with `dotnet build`/`dotnet test`; the user runs the game
  from the Godot 4.7.1 editor.
- **Workflow (user-set):** concise reports; plan → approve → build → report; decisions
  through AskUserQuestion sign-offs, landing in DECISIONS + the foundation doc the same turn.
  Commit only when asked. **Do not spawn subagents unless the user asks** (standing).
  PowerShell 5.1: no double quotes inside `git commit -m` here-strings (bash heredoc works).

## ⭐ START HERE next session

1. **The editor-verification backlog is now Phases 2–7, none of it done** (user's side):
   the identity bench at its stations (53 actions, inspector, Advanced toggle) · the forge
   (all 23 forms, likelihood table, noun variants) · the Assay ladder's new facets · item
   cards/strips for minted gear · a mint → save → reload → sentences-intact roundtrip ·
   **the v14 item-reset load of their existing save** (expected: progression + gold survive,
   items gone, starter kit re-equipped).
2. **Next milestones (ROADMAP):** #4 the playtest checkpoint (user-driven — every
   identity-system number is provisional: all three `*Tuning` classes say so; plus the
   pre-redesign residue listed in ROADMAP) · E6 profession tools · E7 operations + Overreach
   (**now to be designed over the sentence vocabulary** — the affix layer it targeted is gone).
3. If the user reports anything broken in the editor pass, suspect the Phase 7 seams first:
   station panel composition (`has_assembly`), the slimmed `CraftingExperimentSystem`
   (salve), `EquipmentResolver` (definition-properties only now — instance properties are
   gone), and the v14 load path.

## The rules that must not erode (new ones first)

- **D54's reset is total and simple:** pre-v14 → items reset, progression survives. Never
  add partial-survival carve-outs; the identity bench re-mints in minutes.
- **Retaliate aims at `TriggerTarget`** — on defensive events the attacker is the event's
  target. The equip seam's sentences deserve live-fire probes; compile-shape tests alone
  missed this for two phases.
- **Multiplicative payload ranges are factors** (1.1 = +10%), never deltas — validator-fenced
  (`MultiplicativePayloadRangeFloor`); bulwark is the cautionary tale.
- **D53's voices:** the draw table speaks likelihood words derived one-way from the scores it
  hides; profiles read as the strongest few leanings in words; exact scores/weights live only
  behind Advanced. Stakes and the overfill word are **never** Assay-gated (D42 + §4 fairness);
  themes are never visible at any depth.
- **D50's taxonomy on every surface:** Guaranteed/generated/Signature/Drawback stay
  distinguishable; a Signature is *earned*, never the blanket word.
- **D44:** ranks render as rung words (basic unmarked → improved → advanced →
  build-changing), never numerals. Condition/Stability enum member names are the player words
  AND future save keys — rename only with a migration.
- **The equipment property channel is closed:** authored gear may carry only `mass` and
  `hardness` (validated) — the two the resolver reads. Identity mints carry `ItemBaseDelivery`
  on the instance instead.
- **The assembler D30 fence:** drain compiles as damage+restore, store as gauge-feed+band
  (release-on-full waits for a gauge-spend effect kind); `SentenceReadings` must stay
  truthful to the compiled shape, not the designed one.
- All prior invariants stand: floor discipline (one rung-1 floor per owning identity) · the
  preview's table IS the draw distribution · D51 selection stays readable (priority → rank →
  contribution → id; no percentage apertures) · the acquisition fence (gathering never
  passively pays active-identity stock; loot edition re-anchored on `Identities.Count > 0`) ·
  steadiness capped in the shared gate path · authored equivalence + preparation=activation ·
  roster pinned to D44's 24 · solo-complete, chain-enhanced · themes never player-facing ·
  preview parity everywhere · 0 warnings · validator-before-content with failing-content
  tests.

## Deferred / parked — do not relitigate

- Grammar gaps by name: behaviors detonate/spread/bloom; effect kinds `consumeStatus`,
  area-status, delayed, `cleanse`; the `LootResolver` luck seam (Charmed's real consumer).
- Named identity evolution (§14 #4) · Emergent Phenomena (the seam in `ItemEffectResolver`
  is named and empty; overfill raising Signature odds is its only designed input).
- Verb-level open details (transformation-verbs §7): Fuse derived capacity/condition, Restore
  ceiling, fracture-targets-newest — all provisional.
- Pre-redesign deferrals: form/schematic acquisition (D29.2 — the one track
  `ProgressionEcosystemTests` exempts by name), profession tools (E6), consumable forms
  (P5c — the salve's interaction path exists solely for this wait), `MainMvpUI` rename,
  economy.
- **Every identity-system number is provisional** until the playtest checkpoint.

## Known debt and filed items

- ContentStudio still has no registry-vs-`LoadAll` parity test (cheap fence, unbuilt).
- `VerbBenchPanel`/`IdentityForgePanel` pickers reset selection on refresh (filed).
- Two identical sentences on one item share a `RuleId` (harmless while sentences don't cool
  down); two store sentences with the same payload would share a gauge name.
- `docs/expansion-plan.md`, `docs/how-it-plays.md` ch.1, `docs/readability-audit.md`,
  `docs/json-schema.md` are banner-marked historical rather than rewritten — deliberate.
- GDD's crafting sections are superseded-in-place (its header says so); a full GDD crafting
  rewrite was consciously not done in Phase 7.

## The method notes that keep paying

Everything from earlier contexts stands (worked examples · perturbation tests · tests that
express design rules · validator before data · structural fences over exception lists ·
fences catch their author · trace first, the mechanism may already be the design ·
documentation is a claim, so measure it). This context added three:

- **A truthful renderer flushes lying data.** Writing the sentence voice forced bulwark's
  delta-vs-factor bug into the open; writing the live-fire retaliation probe caught the
  TriggerSource aim. Presentation and deletion passes are audit passes if you let them be.
- **Delete in dependency order, and let the compiler drive.** Forms-before-engines was the
  load-bearing sequencing insight (deleting the old forge first would have stranded 21
  forms); after that, one big cut + compile-error triage beat file-by-file caution.
- **A migration isn't done until the data can't express the old model.** The C# deletion was
  half the job; the JSON strips (materials/forms/stations) plus `DataStore`'s
  unknown-field rejection are what make the old system genuinely unrepresentable.
