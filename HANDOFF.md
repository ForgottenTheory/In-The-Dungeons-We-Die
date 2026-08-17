# HANDOFF.md

For the next Claude session. Read `CLAUDE.md`, then this.

**`docs/GDD.md` is the best single overview of the game.** For this session's two big arcs read
`docs/presentation-architecture.md` (D30 — the three languages, source of truth) and
`docs/how-it-plays.md` (the experience-arc doc, ch. 1 Crafting). Decisions D28–D30 are in
`DECISIONS.md`.

## Repo / build state
- Branch `main`. This context's commits: the design-track + presentation/affix code commit(s)
  and the docs commit (see `git log`). Everything green at handoff:
  **`dotnet build InTheDungeonsWeDie.slnx` clean (0 warnings) · `dotnet test` → 765 passing.**
- **`GDD/` (untracked) is the user's personal folder. Not project context — leave it alone.**
- Godot is **not** on PATH — verify with `dotnet build`/`dotnet test`. The user runs the game
  from their Godot 4.7.1 editor and checks UI visually.
- ⚠ **Save schema is v6** (genome + rolled affixes on instances). Older saves are broken for
  three separate reasons by now. **Delete `user://save.json` before running.**
- **Workflow (user-set):** concise reports, plan → approve → build one slice → report. Commit
  only when asked. Do not update this HANDOFF mid-session — only when the user says the
  context is ending.

---

## ⭐ START HERE — C2c's playtest half (user-driven)

Everything machine-verifiable shipped this session. What remains of C2c is **the user playing
the full loop** in the new language: mine → smelt → infuse → attune → fabricate (watch the
preview promise innates + "Supports:") → roll → equip → fight (thorns, ailments, parry with a
buckler, barrier, evade all live). Then the **balance backlog lands in one conversation**:
Fireball one-shots, Bastion damage, the casting-speed decision (GDD §18 #16), profession
interval/XP, fabrication calibration (`FabricationTuning`), the two provisional crafting
constants, affix roll ranges/count weights (`AffixTuning` — all provisional), and the
shock-eel essence rates (see the D29.3 audit note below). **Do not retune anything before the
user reports play feel** — standing decision.

**Editor verification pending (written, never user-verified):** the entire R-track UI — bench
semantic grammar + Advanced toggle, glyph rendering (any tofu box = one JSON edit in
`game/data/properties/properties.json`), fabrication slot-fit lines + preview card, the
Latest-work reveal + debug Reroll, the Parry button (appears only with a buckler equipped),
item strips in Equipment/Inventory. Plus the older list: Techniques panel, the three goblin
fights, Professions tab, combat card height floor, Char Lab hooks panel.

## This context, in order (all committed)

1. **The "how it plays" design track** — `docs/how-it-plays.md` ch. 1 (the crafting arc:
   drop taxonomy, seven stages, knowledge arc). **D28**: gear comes from the bench; realms
   drop inputs; relic materials are the chase design; sealed uniques the fenced exception.
   **D29**: affixes always roll (Assay gates legibility, never capability); forms are
   acquired (starter set + ladder + schematics — an M6 loot class); essence is the realm's
   export ("trace profession essence must never compete economically with Realm extraction").
2. **D30 + R0–R3 — the presentation correction** (user directive: "complexity underneath,
   clarity in the player's hands"). `docs/presentation-architecture.md` is source of truth.
   `Dungeons.Presentation` is the ONLY path from simulation state to player-facing text
   (CLAUDE.md rule 7): tiers (Trace→Extreme) + pips + wear words, trends from the algebra's
   own typed `PropertyChangeKind`s, risk bands (SAFE→DESTROYS, §6.2c preserved), trait
   proximity ("Within reach: Emberveined — needs more Heat"), slot-fit readings, material
   readings, typed `ProjectionLine`s, item cards/strips. `FabricationEngine.Project` added
   (side-effect-free preview — one composition, both callers). `ItemFormat.InstanceLabel` retired
   from player surfaces. `PropertyDefinition` gained `glyph`/`gloss` (data).
3. **R4a — lane alignment.** `combat.resist.physical` + six aspect keys, wired into the
   pipeline with cap/floor; **D-07 executed** (`dodge.chance` retired → `evade.chance` +
   `avoid.lane`, both diminishing + danger).
4. **R4b — the Genome + affix engine** (E5's front half; affixes.md is the spec). Genome per
   §2.2 persisted (**save v6**); `Dungeons.Affixes` (definition/roller/grants); rolling per
   §4; **innates as deterministic `class:"innate"` affixes** (top ≤3 by weight, potency-
   positioned, no variance, never rerollable); Exotic/Signature/Anomalous excluded from v1
   pools (decisions). Grants reuse the Grant vocabulary: stat → scoped contributions with
   affix provenance (concat into `buildModifiers`), rule → TriggerRules attached in
   `AttachBuildRules` (equip/unequip re-runs it), moveModifier → the moveset builder's third
   grantor. **Ailment application chances finally have their source.** §8 validator rules
   (the $roll-parity rule caught seven of my own descriptions) + 20k-roll distribution tests.
5. **R4c-1** — thorns as pure content over the rule engine (when-hit/on-block/after-dodge/
   poison-barbs; e2e: raider slashes, vest bites back); Evade (untelegraphed only, no RNG
   draw without a source); per-packet lane negation (arcane exempt); flat lane pen after the
   cap (eats overcap — pinned against exposure); capped/raw on the armour summary; on-crit
   triggers (`CriticalLanded` existed all along).
6. **R4c-2** — Parry (gear-granted via the Buckler form's `parry` tag; 3-tick window; negate
   + `Parried` + heavy stagger; auto-combat can never hit it, by design §5.1.1); **Barrier
   absorption** (the old HitPipeline debt — `ReduceWithBarrier` covers hits and effect
   damage; `BarrierBroken`); `DamageMitigated` event + reflect-% retaliation;
   status potency (applier) / duration (receiver) keys wired at the encounter seam;
   move-modifier affix grants (Emberbrand: Heavy Strike +25% as heat — first data move-mod).
   **43 affixes shipped.**
7. **C2c machine half** — `tests/Integration/C2cAuditTests.cs`: per-rule ValidateForms
   failing-content tests (the old debt); the first-session sufficiency audit (**it caught a
   real gap** — guaranteed outputs alone had no binding-legal hide until level 20 — then
   confirmed Track Boar's level-1 30% boar-hide bonus closes the chain); the D29.3 essence
   audit (overlap pinned to exactly the shock-eel rung: `eel_skin` + `shock_eel_gland`,
   flagged for the noncompete check at playtest).

## ⚠️ Deferred by explicit decision — do not relitigate, do not silently fix

- **Balance, wholesale** (see START HERE). All affix numbers are breadth-not-balance.
- **Casting-speed scaling** (GDD §18 #16) — at the balance pass.
- **The Fighter identity hook** (§18 #15) — NEEDS DESIGN, parked.
- **Stored retaliation, inversion, ignore-fraction, Exotic rare-roll** — E7 (Exotic tier).
- **Signature affixes** — need P4. **Operations + Overreach + Anomalous** — E7.
- **Sealed uniques + relic materials** — post-slice content (D28 fencing recorded).

## Known debt and filed items

- **The eel rung** (D29.3): two storm-trace faucets in Fishing. Allowed as "rare outcome";
  whether the rates compete economically is a C2c playtest call. Pinned by test.
- Full `capped/raw` on a character sheet + preparation screen (D-05a's other two surfaces);
  the §2.3 numeric Genome Readout panel (semantic supports-line ships; numbers are in the
  debug roll path).
- Two provisional crafting constants; `PropertyDefinition.transferable` unconsumed; response
  properties drop on transformation; `InappropriateOptimismRule` orphaned;
  `StatusController.ModifierTotal` display-only; **`GameRoot` is ~1,600 lines now** —
  Application-layer extraction deferred again (thin forwards only).
- `PROJECT_STATE.md` / `SYSTEM_INDEX.md` predate the R-track — they need Presentation +
  Affixes rows next docs pass (GDD/ROADMAP/DECISIONS/this file are current).
- Form acquisition (D29) lands at M6 (`forms.json` gate + schematics loot class).

## The rules that keep this tractable — do not erode

- **Three languages (D30, CLAUDE.md rule 7):** raw simulation values never on normal play
  surfaces; `Dungeons.Presentation` is the one path to player-facing text, one-way,
  unit-tested; **a player-facing modifier ships only when its mechanic resolves.** Display
  tiers never touch identity quantization.
- **No recipe, ever.** **A Prefix may never reference a Base.** **Every Base distributes the
  same growth budget.** **No class-check condition kind (D25).** **Enemy identity composes
  (D26).** **Innates never reroll (U-7).** One affix per family per item (§3.5).
- **D-12** never default a `ModifierContext` · **D-08** Resolve, not per-control chances ·
  **D-06** on-block hooks listen to `Blocked` (both outcomes) · **D-01** lane movement is
  always `convert`/`addAsExtra` with a fraction · ailment ticks never proc.
- Keep `dotnet test` green (**765**) and the build at **0 warnings**. Content is data; every
  new content type ships with validator rules + failing-content tests. Commit only when
  asked; `main`; Co-Authored-By trailer.

## The method note that keeps paying

Rendering a worked example and reading the output caught bugs/gaps **nine, ten and eleven**
this session (the `None`-tier leak in emerging-property lines, response properties polluting
material identity lines, the level-20 binding gap the sufficiency audit surfaced). The habit:
after building any surface or content system, render the real thing (a failing-test probe
works) and READ it before trusting the tests. Do it again when the user reports C2c findings.
