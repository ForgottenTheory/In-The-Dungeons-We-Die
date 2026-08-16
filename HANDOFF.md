# HANDOFF.md

For the next Claude session. Read `CLAUDE.md`, then this.

**`docs/GDD.md` is the best single overview of the game** — vision, every system, what's real vs
planned, and the unresolved questions. Read it before `PROJECT_STATE.md` / `SYSTEM_INDEX.md` /
`DECISIONS.md` / `ROADMAP.md`.

## Repo / build state
- Branch `main`. This context's commits, in order: `b5b51ce` (D25 migration) → `94d6c51` (M1)
  → `e636d69` (M2′a) → `26c5600` (M2′b+c) → `686fe13` (P1–P3 professions) → `28884fb` (docs)
  → `64418bf` (C1 traits+essence) → `bcb0c6e` (C2a+C2b fabrication) → the final docs commit
  (this file).
- `dotnet build InTheDungeonsWeDie.slnx` clean (**0 warnings**); `dotnet test` → **654 passing**.
- **`GDD/` (untracked) is the user's personal folder. Not project context — leave it alone.**
  `docs/GDD.md` is the project's GDD.
- Godot is **not** on PATH — verify with `dotnet build`/`dotnet test`. The user runs the game
  from their Godot 4.7.1 editor and checks UI visually.
- ⚠ **Old saves are broken twice over**: the roster replacement retired persisted ids, and the
  session moved the save schema to **v5**. Delete `user://save.json` before running.
- **Workflow (user-set):** concise reports, plan → approve → build one slice → report. Commit
  only when asked. **Do not update this HANDOFF mid-session** — only when the user says the
  context is ending.

---

## ⭐ START HERE — next is C2c, the mandatory playtest checkpoint (user-driven)

**C1 and C2a+C2b shipped after the docs commit** (see "This context's additions" below).
`ROADMAP.md` is current: **C2c (checkpoint) → E5 → M6 → E6 → E7**. C2c is the user playing
the full loop (mine → smelt → infuse → attune → fabricate → fight) and the whole parked
balance backlog landing in one conversation: Fireball one-shots, Bastion damage, the
casting-speed decision (GDD §18 #16), profession interval/XP numbers, fabrication calibration
constants (`FabricationTuning`), the two provisional crafting constants, possibly D-07's key
swap. Do not retune anything before the user reports play feel. Small debt to close alongside:
per-rule failing-content tests for `ValidateForms` (shipped content exercises it; broken-store
tests don't yet).

## This context's additions (commits `64418bf`, `bcb0c6e`; 654 tests)

- **C1a traits** (§10): `TraitDefinition` (+`Category` for apertures) / `TraitResolver` —
  birth → supersede → cap-3 after the reaction settles; births eat properties sequentially in
  id order (earlier birth can starve a later condition — pinned); merges keep the stronger
  magnitude and free a slot; displacement refunds nothing. Traits join the signature as
  `id:tier` (magnitude bucketed to 5 levels) in the section P1 reserved — trait-less ids are
  bit-identical to before. `traits_created × 4` charges integrity and can destroy.
- **C1b essence** (§5/§8.4): seven typed essences (`game/data/essences/`; no arcane essence —
  arcane amplifies via `EssenceTuning.Expression`), additive transfer at the process's
  pre-authored `essence_rate` with an anchor-channel bonus, opposition annihilating overlap
  into strain (works authored one-sided), capacity = resonance × 1.5 feeding effective
  instability, the Attune process (8th, arcane medium, `state:attuned` joined the vocabulary),
  38 materials author essence and **22 author resonance** (nothing did before — capacity was
  zero everywhere). Strain warning teaches "attune first, then infuse"; e2e pinned.
- **C2a+C2b fabrication** (§16): `FormTemplateDefinition` + `FabricationEngine` — terminal,
  consumes materials, mints an `ItemInstance` over a **derived `EquipmentDefinition`**
  registered by signature into the equipment store and **persisted** (`EmergentEquipment` in
  the save, restored before instances resolve). The 0–100 → combat-unit reconciliation lives
  only in `stat_map` + `FabricationTuning.CombatUnitScale`; `EquipmentResolver` unchanged —
  **parity pinned: iron/iron/leather longsword ≈ authored Iron Sword** through the same seam.
  Longsword is the 3-slot exemplar (edge .60 / core .25 / binding .15): placement reorders
  which trait dominates and names the item ("Emberveined Traited Longsword" on the edge vs
  "Verdant Iron Longsword" in the core — pinned). Buckler (tag `shield` → Shield Bash
  reachable via crafting) and Vest stay single-slot through the same path. Armour forms derive
  lane resistances from response properties. UI: form picker + per-slot tag-filtered pickers
  (`EligibleForSlot`); debug grant now includes leather/rawhide/ley crystal.

## What this session shipped (all committed)

1. **D25 executed** — a Base is a growth archetype plus a starting kit, never a license. The
   docs migration; no code needed. Standing rule: **no class-check condition kind, ever.**
2. **M1** — the Combat tab finally shows E0–E4: per-move buttons (tooltips: id/cost/cooldown/
   provenance; also on the Realm fight row; guarded rebuild so per-frame refresh can't eat a
   click), live gauge readout, a Hit-trace toggle (streams to the event log + pins the last
   trace in a monospace card). **User-verified in the editor.** D-20's 0.55 interval floor
   applied to the registry (two pinning tests moved with it). Combat/realm report cards later
   got a 150px height floor so buttons stop shifting mid-fight.
3. **M2′a — acquisition**: `TechniqueDefinition` (`technique.*`) items teach moves into
   `LearnedMoves` (once-per-move, learn-order-preserving, save **v5**); learned grants join
   moveset composition with `learned` provenance; Learn UI + debug grant.
   **Found in passing and fixed: `GameRoot` never passed the emergent registry to
   `SaveMapper.Capture/Apply` — emergent archetypes were silently unpersisted (v4's whole
   point).**
4. **M2′b — the library**: 16 new universal moves (27 total), 19 technique items, the
   `stoneskin` status. One vocabulary extension: **`EffectSpec.Target` per-effect override**
   (rules + riders) — Drain's damage hits the enemy while its heal names `TriggerSource`.
   Ward became Stoneskin because **barrier absorption is unimplemented** (HitPipeline comment
   reserves the spot). Chains are move-modifier territory (E5); Arc Surge uses `max_targets`.
5. **M2′c — the Enemy Framework (D26)**: `EnemyFamilyDefinition` + `CombatRoleDefinition` +
   `AiProfileDefinition`, folded by `ActorResolver` (baselines + deltas; per-key later-layer-
   wins; tags union; profile rules + inline extras). AI rules match by id **or `moveTag`**;
   `avoid_repeat_weight` reshapes weights deterministically. `FromActor` armour fixed (was
   hardcoded 0). Three data-composed goblins: Raider (pressure/punish via Expose Weakness),
   Brute (armoured, Overhead Crush = the answer-this-telegraph test, Brace when hurt), and
   **Hexer — pure configuration over library moves, the framework proof.** A future
   Elite/Realm variant is one more delta through the same fold — do not duplicate definitions.
6. **P1–P3 — professions to the §7.1 slice target**: 8 professions / 26 actions (Mining,
   Forestry, Fishing, Herblore, Smithing, Alchemy, Cooking, Beast Lore), level-gated ladders,
   deliberate cross-feeding (pinned ≥ 4 chains by test). **The startup iron-ore seed is
   deleted** (a test pins that an action produces iron ore). Four prepared materials
   (`form:meal` / `form:tincture`, with byproduct coverage) carry `growth` for future healing
   consumables.

## ⚠️ Deferred by explicit user decision — do not relitigate, do not silently fix

- **Balance, wholesale.** Fireball one-shots; Bastion does almost no damage and its fights are
  hard; all M2′/P-pass numbers are relative placeholders. The user wants to drive tuning from
  play. The backlog lands at C2's scale reconciliation or a dedicated tuning pass (ROADMAP).
- **Casting-speed attribute scaling** (GDD §18 #16) — decided *at the balance pass*. Spells
  author plain windups until then.
- **The Fighter identity hook** (GDD §18 #15) — its engine was universalized in E4; NEEDS
  DESIGN, deliberately parked. Candidates on the table: technique breadth; swap fluency (needs
  a live re-resolve seam in `CombatEncounter` — the encounter snapshots the moveset at Start).

## ⚠️ Needs a look in the editor (written, not yet user-verified)

- The **Techniques panel** (Inventory tab): grant → Learn → move button appears → save/load
  keeps it and the item count drops.
- The **three goblin fights** under the framework — Brute armour/telegraphs/Brace, Hexer
  casting, Raider pressure. And the **Professions tab** with 26 action rows.
- The **combat card height floor** (150px) — buttons should no longer shift; raise the floor
  if a future multi-enemy report overflows it.
- Older unverified surfaces stand: the Character Lab "Live hooks" panel and the Lab layout
  re-check.

## Known debt and filed decisions (see also DECISIONS.md)

- **D-07 remains unapplied**: `combat.dodge.chance` still exists and stays additive;
  `combat.avoid.lane` / `combat.evade.chance` don't exist yet. Re-shaping a key scheduled to
  die is balancing a ghost.
- **Other §4.4 floors the registry doesn't implement** (flagged, not decisions):
  `resource.cost.mult` min 0 vs table 0.40; `combat.damage_taken.mult` min 0 vs 0.50.
- **Barrier absorption is unimplemented** — `status.barrier` is authored data with a
  `magnitude` nothing consumes. Ward-as-Stoneskin sidestepped it; implement before authoring
  barrier-granting content.
- **Technique faucets are debug-only** until M6 loot tables; the Wizard/Bastion starting
  grants stay in `bases.json` (legitimate kit under D25).
- Two provisional crafting constants (`QuantizationTuning.PropertyBucket`,
  `RefinementTuning.StateDeltaCost`); `PropertyDefinition.transferable` unconsumed; response
  properties drop on transformation; `InappropriateOptimismRule` orphaned;
  `StatusController.ModifierTotal` display-only (collapse if unused); **`GameRoot` is ~1,420
  lines** — Application-layer extraction deferred again; keep adding thin forwards only.

## The rules that keep this tractable — do not erode

- **No recipe, ever** (crafting is a total function). **A Prefix may never reference a Base.**
  **Every Base distributes the same growth budget.** **An expressed Suffix has one expression
  per channel.** **Formatting never touches mechanics.**
- **No class-check condition kind** (D25). **Moves are universal**; soft gates only.
- **Enemy identity composes** (D26): family + role + actor + (future) variant deltas through
  `ActorResolver` — never `if enemy == X`, never duplicated definitions, never per-type
  resistance stats (lanes stay the eight; Slashing/Crushing/Piercing live in vulnerability
  multipliers only).
- **D-12**: never default a `ModifierContext`. **D-08**: Resolve, not per-control chances.
  **D-06**: on-block hooks listen to `Blocked`. **D-01**: lane movement is always `convert`
  with a fraction. **OncePerChain defaults true**; handlers must propagate
  `invocation.Context`.
- Keep `dotnet test` green (**626**) and the build at **0 warnings**. Content is data; add a
  `ContentValidator` rule + failing test with every new content type. Commit only when asked;
  `main`; Co-Authored-By trailer.

## A method note that keeps paying

Rendering a worked example and reading the numbers has now caught **six** real bugs the tests
missed (E1 attribute-scaling, E1 crit order, E3c event-tag mutation, E4 recalled-move
modifiers, E4 chain depth, M2′a's unpersisted emergent registry — that one by reading a seam
while threading a parameter through it). Do it again in C1: run a craft with traits through
the projection and read the trace before trusting the tests.
