# HANDOFF.md

For the next Claude session. Read `CLAUDE.md`, then this.

**`docs/GDD.md` is the best single overview of the game** — vision, every system, what's real vs
planned, and the unresolved questions. Read it before `PROJECT_STATE.md` / `SYSTEM_INDEX.md` /
`DECISIONS.md` / `ROADMAP.md`.

## Repo / build state
- Branch `main`, latest commit **`6eb8dad`** (E4 moves). Before it: `ce9d75a` (E3c-3),
  `9f56828` (E3c + E3c-2), `bfd7cdc` (E3b), `2bf9902` (E3a), `99cdceb` (E1 + E2), `90faf27` (E0).
- `dotnet build InTheDungeonsWeDie.slnx` clean (**0 warnings**); `dotnet test` → **602 passing**.
- ⚠ **Uncommitted, docs only** (no code): `docs/GDD.md` (the E0–E4 status-marker update),
  `ROADMAP.md` (rewritten; M2′ per D25), `DECISIONS.md` (D25 added), this file.
- **`GDD/` (untracked) is the user's personal folder. Not project context — leave it alone.**
  `docs/GDD.md` is the project's GDD.
- Godot is **not** on PATH — verify with `dotnet build`/`dotnet test`. The user runs the game
  from their Godot 4.7.1 editor and checks UI visually.

---

## ⭐ START HERE — the D25 migration is DONE; next is M1 → M2′

**D25 (recorded in `DECISIONS.md`):** *A Base is a growth archetype plus a starting kit — never
a license.* Nothing is Base-exclusive: gauges, moves and mechanics are universal definitions any
layer may grant; attributes/resources/modifiers do the specializing (soft specialization, never
hard permission). The pure "Base = stats only" form was **explicitly rejected**. Read D25 in
full before touching Base/move design — it carries the per-engine dispositions and the rejected
alternatives so they are not relitigated.

**The migration (docs-only, as the audit predicted) is complete:** GDD §3.1/§3.4/§6/§17/§18
(#15 Fighter redesign, #16 casting-speed scaling)/§19.3a rewritten; `docs/classes.md` reframed
(engines as starting kits, movesets marked BUILT, Fighter's stale engine flagged);
`PROJECT_STATE.md`'s Base-moves line now carries the M2′ frame. No code changed — the
architecture already complied.

**Content, deliberately deferred:** the two E4 exemplar grants (`class.wizard` → fireball,
`class.bastion` → shield_bash in `bases.json`) **stay until M2′ exists** — they satisfy the
reachability validator and are legitimate "starting kit" under D25. Migrate them to
technique-item sources when acquisition lands.

**M1 is done** (wiring verified in the editor by the user; balance findings recorded below).
**M2′ is underway:**
- ✅ **M2′a — acquisition machinery** (uncommitted at time of writing): `TechniqueDefinition`
  content type (`technique.*`, D19 updated) + `game/data/techniques/` with 3 items;
  `LearnedMoves` (learn-order-preserving, once-per-move); learned grants join moveset
  composition with `learned` provenance; save **v5** (`LearnedMoves` field, older saves load
  empty); `LearnTechnique`/`OwnedTechniques`/`GrantTestTechniques` on `GameRoot`; a Techniques
  panel with Learn buttons in the Inventory tab; validator rules (teaches-resolves,
  teaches-nothing, techniques-count-as-reachability-source) each with tests. 610 tests.
  **Found and fixed in passing: `GameRoot` never passed `_emergentRegistry` to
  `SaveMapper.Capture/Apply` — emergent archetypes were silently not persisted (v4's whole
  point). Two-arg fix at both call sites.**
- **Both M2′ design questions were deliberately deferred by the user (2026-08-16):** the
  Fighter identity hook (GDD §18 #15 — library authored without a Fighter kit) and
  casting-speed scaling (§18 #16 — decided at the balance pass; spells author plain windups).
- **Next: M2′b — the library content pass** (~15–20 universal moves + technique items,
  soft-gated only), then **M2′c — goblin AI profiles**. Then C1 → C2 → E5 → M6 → E6 → E7,
  unchanged.

### Standing constraints from D25 (do not erode)
- **No class-check condition kind, ever.** The soft gates are attributes, costs and scoped
  modifiers — they exist and they suffice.
- **Max two gauges stays a readability cap.** Today it is structural (one Base + one Prefix);
  when a third grantor type appears (equipment/learned — a tower shield granting Guard is the
  flagship), enforcement moves to a composition-time rule in `BuildResolver`/validation.
- **Nothing is removed.** All 8 built gauge engines stay as Base starting kits; Form (Druid) is
  a Move mechanic when built (E4's `replaces` grants are the machinery); Thralls/openings/ammo/
  deployables get authored as universal systems with Base as default access.

## ✅ M1 editor pass — verified by the user; balance findings recorded
The M1 wiring (move buttons + tooltips on the Combat tab and Realm fight row, gauge readout,
Hit-trace toggle with the pinned monospace card, Live-hooks panel) **was verified in the editor
by the user — it works**.

**Balance findings from that session, deliberately deferred (the user's call):**
- **Fireball one-shots.** The 40-damage spell against goblin HP pools is a kill button.
- **Bastion does almost no damage and its fights are hard** — the "very low damage" weakness
  currently reads as "no win condition", not as a trade.
- General damage tuning is pending anyway — fold these into the next balance-touching slice
  (C2's scale reconciliation is the natural home, or a dedicated tuning mini-pass if it gets
  painful before then). Do not silently retune; the user wants to drive balance from play.

## ⚠️ Before running the game
**Delete `user://save.json`.** `CharacterBuild` ids are persisted and half the roster was
retired — `class.hexslinger`, `prefix.frenzied`/`ironbound`/`pyromaniac`,
`suffix.inappropriate_optimism`/`the_bigger_hammer` no longer exist.

---

## Where we are

**The combat foundation is being built, slice by slice.** Design settled first (27 decisions),
then E0 → E1 → E2 → E3a → E3b → E3c → E3c-2 → E3c-3. Full plan and rationale: `docs/effect-foundation.md` §10 and §12.

Standing before that, unchanged and not touched by any of it:

**Emergent crafting P1** (`9917d68`) — the universal reaction engine. No recipes; 7 processes,
the full algebra, potency/integrity, destruction with byproducts, signature registry, naming,
Reaction Log, pre-commit projection, and a Crafting bench UI. See `docs/emergent-item-system.md`
for the spec and `DECISIONS.md` D20/D21.

**The class combinator** (`5bdab2e`) — 15 Bases, 25 Prefixes, 50 Suffixes (10 fully expressed),
composing into 18,750 builds with none of the combinations authored. **Its hooks are now live** —
E0 put combat on the event bus and E2 gave 13 of its 14 dangling status ids real definitions. See
`docs/classes.md`.

### The rules that keep both tractable — do not erode these
- **No recipe, ever.** The crafting algebra is a total function. Adding one hardcoded
  combination is how the whole design fails.
- **A Prefix may never reference a Base.** Enforced by `ContentValidator`. Breaking it turns 25
  authored mechanics into 375.
- **Every Base distributes the same growth budget.** Only the shape differs.
- **An expressed Suffix has one expression per channel.** A partial one looks usable and isn't.
- **Formatting never touches mechanics.** A Suffix's `format` is read by `ClassNameFormatter`
  and nowhere else.

---

*(Godot-side verification is listed at the top, under "Godot-side work that has never been run".
The one Godot detail worth keeping: autowrapping `Label`s inside an `HBoxContainer` collapse to
one character per line — there is a `Wrapping()` helper with the rule documented.)*

---

## ✅ THE EFFECT FOUNDATION PACKAGE — **all 27 decisions settled**; E0–E3c-3 built

A full design package for a **universal gameplay effect vocabulary** (combat, statuses, moves,
item affixes, profession tools). **It supersedes the old M0–M6 Move-system plan**, which has
been deleted from this file — statuses and the damage pipeline come first. Read the entry doc:

| Doc | Covers |
|---|---|
| **`docs/effect-foundation.md`** | **START HERE** — audit, architecture, triggers/conditions/effects, modifiers & stacking, proc safety, tags, build order, **27 settled decisions** (§12) + §12.1 change log |
| `docs/damage-and-defense.md` | damage types × aspects · the resolution pipeline · defence layers · resistance/penetration/inversion · thorns |
| `docs/statuses.md` | status taxonomy · **Resolve** (the CC answer) · the status data contract |
| `docs/moves.md` | the Move model · move modification · the shared Action vocabulary |
| `docs/affixes.md` | Material Genetics → affix eligibility/weight/tier · crafting operations · Overreach |
| `docs/profession-tools.md` | the yield pipeline · tools as real fabricated equipment |
| `docs/effect-catalog.md` | 254 starter modifier concepts |
| `docs/worked-examples.md` | 10 builds · 4 tools · 8 resolution traces |

**The three audit findings that drove it:**
1. `CombatEncounter` raises **zero** `GameEvent`s — every combat trigger rule in the game is dead.
2. **14 status ids are authored in `prefixes.json`/`suffixes.json` and no status system exists.**
3. `ModifierContribution` has no **scope**, so "+10 damage with swords" and "−12% Fishing
   interval" are inexpressible. One change fixes both (D-12).

**Settled order (D-19):** ✅ **E0** → ✅ **E1** → ✅ **E2** → ✅ **E3** → ✅ **E4 moves** →
C1 traits/essence → C2 fabrication + scale reconciliation → E5 affixes → E6 tools → E7 Overreach.

> **The current build order is `ROADMAP.md`** (rewritten after E4, amended by D25): an editor
> verification/tuning pass (M1), then **M2′ — the universal move library + acquisition** (which
> replaced "Base signature moves"; Bases never own moves), then C1 → C2 → E5 → M6 loop-closers →
> E6 → E7.

### ✅ E4 shipped — the Move system, and the GDD's largest gap closes

`dotnet test` → **602 passing** (was 592), build 0 warnings, clean rebuild verified.

**One shape, everything an action is.** `MoveDefinition` = timing + costs + requires + packets +
`EffectSpec` riders. Attack vs Spell is a difference of data, not of engine — Heavy Strike,
Fireball and Shield Bash ship as the §2.2 exemplars, one JSON file apart. 9 moves shipped.

- **The shared Action vocabulary** (`core/Actions/`): `ActionTiming` (was `AbilityTiming`) and
  `ActionCost` (pools *and* gauges — a gauge name is a legal cost). Professions adopt in E6.
- **`MovesetBuilder` → `ResolvedMove`**, with provenance on every grant and modifier.
  Composition: **weapon first** (so `Attack()` is the weapon's swing — the Fighter's identity),
  then Species → Base → Prefix → Suffix. Species grant `move.unarmed`, so nobody is ever
  moveless. Replacement (`replaces`) reported, never silent.
- **`MoveModifier`**: match (id and/or tags) + **11 ops in a fixed application order** —
  `scaleDamage` before `convert`, so increases apply to the lane the damage started in.
  Source-order independence is proved by test, not assumed. `addTag` is the composition lever;
  a tag added this pass matches modifiers on the *next* rebuild (cached-resolution bargain,
  pinned by test as a decision).
- **The D-18 deletion, complete:** `AttackProfile`, `AbilityDefinition`, `CombatCalculator`,
  `Hit.ToPackets`, `WeaponStats`, the `abilities/` folder, and both stale validator allowlists
  are **gone**. Weapons author `moves: [...]`; `EquipmentResolver.ResolveWeaponMoves` applies
  instance mass to packets **once, split by share** (the E1 attribute-scaling rule).
- **Enemies run the same system** (§5.2): `ActorDefinition.Moves` + weighted `Ai` rules over the
  shared `ConditionSpec` vocabulary. Empty profile = uniform — exactly what `_rng.NextInt` did,
  so unprofiled actors behave as before. AI determinism under the seed is pinned.
- **Move riders go through the same handler registry** as rule effects, via `IEffectSink`
  (the rule engine wearing a second hat). A rider starts its own chain at depth 0;
  **`triggerMove` executes at depth+1** and a triggerMove whose target can itself trigger is
  refused at load. `EffectSpec.Chance` added for riders ("Burn @ 20%").
- **The Mnemonic closes end-to-end** — the fourteenth dangling status id. `status.recalled_move`
  is authored (`stores_move: true`), the store rule captures the executing move's id off the
  event's `move:` tag, and the granted `move.recall` replays it through `recallMove`. The replay
  re-stores itself — bounded by Recall's 150-tick cooldown, deliberately.
- **Tags:** moves author namespaced tags only (validated against §5's closed vocabulary);
  combat derives the **bare aliases** (`attack`, `melee`, `heavy`, type names) at event time, so
  all 23 pre-vocabulary `hasTag` hooks keep working. `heavy` stays derived, never authored.
  Events also carry `move:<id>` and per-packet `lane:` tags.
- **Validator (§6):** move tags/costs/requires/riders, modifier ops with per-op shape checks,
  convert totals ≤100%, dead modifiers, orphan moves (everything must be granted by something),
  weapon-grants-no-moves, AI rules select the actor's own moves.

**The trace-read pass caught two real engine bugs — five slices, five catches:**
1. **A recalled/triggered move resolved the raw store definition**, silently dropping the
   caster's modifiers (the replayed slash lost its stormbrand charge packet). `TriggerMove` now
   prefers the caster's own resolved version and falls back to the store.
2. **Triggered moves did not advance chain depth.** §3.4 says depth+1; now they do, so a
   triggered move's riders sit at the ceiling and nothing procs off them.

**Deliberate scope notes:**
- **Base moveset authoring is a content pass, not an engine gap.** Wizard has Fireball and
  Bastion has Shield Bash as the pattern exemplars; the other 13 Bases await a design pass.
- **`modifyMove` applies at execution time** (not cached) — "the next attack is empowered"
  cannot be pre-baked. Runtime `grantMove` grants skip build modifiers; documented in code.
- **`Targeting` declared, range deferred** (U-2) — unused authored numbers rot.
- **No `abstract class Action`** — professions share `ActionTiming`/`ActionCost`/conditions/
  events in E6, never a base class.

### ✅ E3 is complete — the effect foundation is built

E3c and E3c-2 share commit `9f56828`; E3c-3 is `ce9d75a`.

**What E3 adds up to:** the class combinator is no longer theoretical. A Prefix's hook fires
against a real fight, its effect executes, its gauge fills, the modifiers it grants change the
numbers, and a condition can gate on the state of the world. Every one of those was authored
content firing into nothing when the package started.

### ✅ E3c shipped — effects finally do things

`dotnet test` → **569 passing** (was 554), build 0 warnings. Committed in `9f56828`.

Everything before this fired into nothing: E0 raised events, E1–E2 gave them damage and
statuses, E3a gave rules targets and a proc budget, and every effect a shipped Prefix or Suffix
declared was recorded and thrown away.

- **Six handlers** — `damage`, `areaDamage`, `heal`, `applyStatus`, `grantResource`, `interrupt`.
  Registered by `CombatEffects.RegisterCombatHandlers`. That is 49 of the 80 authored effect
  instances; the rest belong to systems that do not exist and stay visibly in `Unhandled`.
- **`EffectTargetResolver`** turns E3a's selectors into combatants and filters the dead, so an
  effect firing on the killing blow does not land on a corpse.
- **The gauge runtime** (`GaugePool` / `GaugeController`). **This was the surprise:** all fifteen
  authored `grantResource` effects name a *gauge* — Charge, Momentum, Threat, Debt — and no
  runtime gauge state existed anywhere. The whole gauge layer was authored, validated and inert.
  Gauges reset per encounter, ride the status sweep for decay, and reconfigure on a build swap.
- **`gauge_fraction` is now produced.** Nothing ever set it, so all **7 authored `gaugeAtLeast`
  conditions were silently always-false**. See the known limitation below.
- **Context propagation** through `CombatEncounter.Publish` *and* `StatusController.Apply`, which
  is what makes E3a's budget real: a proc's proc is depth 2 and stops there.
- **Ailment ticks now publish `canTrigger: false`** (proc-safety rule 4). Inert before this
  slice, load-bearing after it — a 20-second Poison would otherwise proc every rule in the build
  dozens of times from one application.

**A pre-existing bug found by rendering a fight and reading the trace** — the third time this
method has paid. `ApplyResult` called `tags.Add("blocked")` on the **`ActionInFlight`'s own tag
set**, and a `GameEvent` holds the reference rather than a snapshot. Events published *before*
the block — `ActionQueued`, `ActionTelegraphed` — retroactively acquired a `blocked` tag. Live
rule matching never saw it (the bus dispatches synchronously, so dispatch had already happened),
but everything that *records* events read a history that never happened: the Hit Log, the Lab's
recent-firings panel, any future replay. Fixed by copying the set; pinned by a test.

**Known limitations, deliberate:**
- **`gaugeAtLeast` names no gauge** and a build can run two (Base + Prefix). It reads the highest
  fill, which is right for every shipped single-gauge build and ambiguous for a two-gauge one.
  The fix is a gauge name on the condition, which belongs with E3c-3.
- **`areaDamage` ignores the target selector** and hits every living enemy, because positioning
  does not exist. That is exactly right for both of Exploding Kneecaps' expressions today; the
  selector starts doing work here when positions arrive.
- **`EffectTarget.Self` resolves to the player**, because every attached rule comes from the
  player's build. Same debt `CombatEncounter.SelfId` already carries.
- **Gauge `bands` are computed but unconsumed** (`GaugePool.ActiveBands`). They contribute
  modifiers, and nothing reads modifiers during combat yet — that is E3c-2's job.
- **Ordering artifact, not a bug:** a retaliation's damage events land between `Blocked` and the
  triggering hit's own `DamageDealt`, because the bus dispatches synchronously from inside
  `ApplyResult`. Worth knowing when reading a trace.

### ✅ E3c-2 shipped — the modifier read path

`dotnet test` → **580 passing** (was 569), build 0 warnings. Committed in `9f56828`.

Scoped as "`grantModifier`", and the investigation found something bigger: **nothing in combat
ever read a modifier at all.** Four systems were producing contributions into a void —
`ResolvedBuild.Modifiers` had no consumer, `StatusController.ModifierTotal` was called only by
its own tests, gauge `bands` were declared and ignored, and `grantModifier` was `Unhandled`. One
missing seam, four inert systems.

- **`CombatantModifiers`** assembles all four into one `ModifierSet` per combatant, per query
  (not cached — a stale cache mid-proc-chain costs more than the assembly does).
- **`TimedModifiers`** holds `grantModifier`'s grants and expires them on the sweep. Deliberately
  *not* modelled as anonymous statuses: a status is named, visible and cleansable, and "+20%
  damage for 40 ticks" from a proc is none of those.
- **The pipeline reads them.** A new INCREASED stage (`combat.damage.mult`), plus `crit.chance`,
  `crit.mult`, `armor`, `block.mult` and `damage_taken.mult`. The scheduler reads
  `windup.mult` — **which is what Chill is**, authored in E2 and without effect until now.
- **Build modifiers are the owner's**, not everyone's in the fight.

**Two judgement calls worth knowing:**
- **Block strength scales what the guard *eats*, not what gets through**: `1 − (1 − 0.4) × s`.
  Multiplying throughput directly would make "+30% block strength" *increase* damage taken.
- **The crit tuning cap bounds Luck alone**, with modifiers adding on top and answering to the
  key's clamp. Capping the total would make every crit-chance affix past 50% worth exactly zero
  without ever saying so.

**A correction to E3b, found here:** `ModifierContext` supplied **one value per dimension**, but
one swing is `melee` *and* `attack` *and* `light`, and a `move_tag`-scoped modifier must match
any of them. Equality would have silently dropped every move-tag modifier on every move. The
context now holds a **set** per dimension and `Matches` is membership. A contribution still
carries at most one scope — that part of D-12 is unchanged.

**Left alone deliberately:** `StatusController.ModifierTotal` survives as a *status-only*
subtotal for display ("what is Chill doing to me?" is a different question from "what is my
windup?"). Nothing authoritative may read it. **Collapse it if it stays unused.**

### ✅ E3c-3 shipped — the stateful conditions

`dotnet test` → **592 passing** (was 580), build 0 warnings. Committed in `ce9d75a`.

Every condition through E3c was a pure function of the `GameEvent`, which is why the evaluator
could be static. "Only while the target is Chilled" is not answerable from an event, and writing
that state into every event instead would mean every publisher guessing what every future
condition might want.

- **`IConditionWorld`** — deliberately **four questions, no entity graph, no queries**. A
  condition vocabulary that can ask anything becomes a query language in content, and the point
  of the closed vocabularies is that content combines mechanics rather than inventing them.
  Identity is the event's string id, so `Dungeons.Rules` still knows nothing about combat types.
- **New kinds:** `targetHasStatus`, `selfHasStatus`, `resourceAbove`/`Below`, `equippedTag`,
  `hitHasLane`. `gaugeAtLeast` now takes a gauge name, closing E3c's ambiguity for two-gauge
  builds.
- **`Evaluate` stays static** with an optional world, so the four existing call sites and the
  roster tests are untouched.
- **The failure mode is visible, not silent.** A condition the engine cannot answer is recorded
  in `TriggerRuleEngine.UnevaluatedConditions` (surfaced as `GameRoot.UnevaluatedConditions`)
  and returns false. A rule whose condition can never pass is exactly as dead as one whose
  effect goes nowhere, and D23 says that must be *visible*.

**Three judgement calls:**
- **`actionHasTag` was not added.** It would be a synonym for `hasTag`. D-11's standing rule:
  a new *derived tag* is the right answer to a real gap; a new condition kind is not.
- **`hitHasLane` needs no world.** Combat tags hit events `lane:physical` etc. in the existing
  `family:value` convention — a hit already knows what it arrived as, and a lane tag can never
  collide with an ordinary one. This is also the first time D-02's physical-lane collapse shows
  up in content: a Slashing hit satisfies `hitHasLane: physical`.
- **A *named* `gaugeAtLeast` refuses to answer without a world** rather than falling back to the
  fullest meter. An unnamed one still reads the event value, so the seven authored conditions
  that predate naming keep working untouched.

**No shipped content uses any of the new kinds yet** — they are capability for future content,
which is why this came last in E3.

### ✅ E3b shipped — scoped modifier contributions (D-12)

`dotnet test` → **554 passing** (was 532), build 0 warnings. Committed in `bfd7cdc`.

The change called "highest leverage in the package", and the one that introduced the package's
only *silently wrong answer* failure mode. Both halves landed together.

- **`ModifierScope(Dimension, Value)`** on `ModifierContribution`, over eight closed dimensions
  (`lane aspect essence profession move_tag form item status`). A contribution carries **at most
  one** scope; two dimensions means two contributions from one source, both of which must match.
- **`ModifierContext`** — the situation being resolved in. `ModifierSet.Resolve(key, context,
  baseValue?)` takes one and **there is no overload that defaults it**. Callers with genuinely no
  situation pass `ModifierContext.None`, which greps; an omitted argument does not.
- **`scoped_by` and `danger`** on `ModifierKeyDefinition`.
- **`diminishing`** (`1 − Π(1−x)`, base value included as one more term) and **`highest_only`**.
  Authored as snake_case; a property-level converter reads both spellings, because a converter in
  `JsonSerializerOptions.Converters` outranks a `[JsonConverter]` on the enum type.
- **All three §4.2.2 guards, each with its own test:** resolving a `scoped_by` key with a context
  lacking that dimension **throws** (not the unscoped subtotal, not the baseline); a contribution
  whose scope dimension disagrees with the key is rejected at `Add`; a `danger` key without a
  `max` fails content validation.
- **Registry:** the six `profession.*` keys are `scoped_by: profession` — this is the whole of
  D-12's leverage, and without it the registry forks per skill and ~55 keys becomes ~330.
  `combat.damage.flat` is `scoped_by: move_tag`. Preservation and double-output became
  `diminishing` + `danger`, matching §4.4's "(exists)" rows exactly.

**Two things worth not re-deriving:**
- **`Resolve` on a multiplicative key is meant to return the *multiplier*, which the caller then
  applies.** Passing an absolute `baseValue` hands the key's floor a product to clamp, and the
  floor exists to stop stacked haste rather than to stop short intervals. The §4.2.2 worked trace
  is pinned as a test in the correct shape.
- **A key's "nothing contributed" value is its baseline *after clamps*, not its baseline.**
  `resource.max_health` has `min: 1` precisely so that nothing-contributed can never mean zero
  health. A test asserted the raw baseline and was wrong, not the code.

**Two registry contradictions found and deliberately left alone** — both are settled decisions the
shipped data does not yet implement, and both are balance numbers that want the user's call:
- **D-20 sets the `combat.interval.mult` floor at 0.55**; the registry still has **0.25**. D-20 is
  ✅ DECIDED, so this is an unapplied decision rather than an open question.
- **D-07 retires `combat.dodge.chance`** in favour of `combat.avoid.lane` (diminishing, max 0.25)
  and `combat.evade.chance` (diminishing, max 0.15). Neither key exists. The dodge key was
  therefore left additive — re-shaping a key scheduled to stop existing is balancing a ghost.

**E3a — the rule engine upgrade.**
- **`effects[]`**: one chance roll, N effects. "25% to Shock *and* restore 8 Stamina" was
  previously two rules with duplicated conditions and *independent* rolls — a different mechanic,
  not a formatting difference. The legacy single `effect` stays valid, so **no content migrated**;
  read `rule.Payload`, never `rule.Effect`.
- **`EffectTarget`**: `TriggerTarget` (default) · `TriggerSource` · `Self` · `AllEnemies` ·
  `AllAllies` · `RandomEnemy` · `LowestHealthEnemy`. Exploding Kneecaps' Guard expression
  detonates against the attacker and its Surge expression around you — same effect kind, and the
  target selector is the whole difference.
- **Proc safety, complete**: `EffectContext` (chain id, origin, depth, origin tags), depth budget
  of 2, once-per-chain **on by default**, per-target ICD, `GameEvent.CanTrigger`, and a
  64-effect-per-chain fuse. Chain ids are **sequential, not GUIDs** — the sim must replay from a
  seed.
- **Validator**: proc depth above the default is rejected outside Anomalous content, so "may
  recurse one level further" stays something you win from Overreach rather than a field anyone
  can type.

**Two properties worth not breaking:**
- **Handlers must propagate `invocation.Context`** onto any event they raise. If one forgets, the
  chain restarts at depth 0 and the entire budget becomes decorative. E3c's handlers exercised
  this for real and it holds — `AProcsProcInheritsTheChainAndStopsAtTheDepthBudget` is the test
  that would catch a regression, and it asserts inheritance *within* one chain rather than
  counting chains (several origin chains per fight is normal — every `MoveExecuted` starts one).
- **`OncePerChain` defaults to true.** Content opts *into* risk; it never opts out of safety by
  omission.

### A method note worth repeating

**Three of the best bugs in this whole stretch were found by rendering a worked example and
reading the numbers, not by a test.** E1/E2 gave two: attribute scaling applied per packet (so
splitting a hit was free damage), and crit ordering contradicting its own spec. E3c gave the
third: a `GameEvent`'s tag set being mutated after publication, rewriting the history of events
that had already fired. None of the three would have been caught by the tests that existed.

Do it again for E3c-2: wire a fight with a `grantModifier` build, print the modifier trace with
each contribution's scope, and read it. Cheap, and it has now paid three times.

### ✅ E2 shipped — lifecycle split, then statuses

`dotnet test` → **519 passing** (was 493). Committed in `99cdceb`.

**E2a — the telegraph/windup split.** GDD §5.2 called this "the riskiest single change in the
combat roadmap"; it landed with **zero existing tests changed**, because total time-to-impact is
identical. `ActionInFlight` now unifies player and enemy actions on one model, `ActionPhase`
distinguishes Telegraph from Windup, and `Interrupt(actor)` cuts an action and **tags which
phase it cut** — so content can tell "stopped them before they swung" from "stopped them
mid-swing". That distinction is the entire reason the split exists.

**E2b — statuses.** `StatusDefinition` + `StatusController` + **27 status definitions**: the 14
core (D-09) plus **13 of the 14 previously-dangling authored ids**. Every one is data — Chill is
literally `{ key: combat.windup.mult, value: 1.25 }` — which is why 27 statuses cost roughly what
3 would. `ContentValidator` now **proves every `applyStatus` in shipped content resolves**, so
the fourteen-dangling-ids situation cannot recur.

**Two real bugs found by the tests, both fixed:**
- **Control buildup never decayed** when the target had no active status — i.e. exactly the case
  that matters, part-way to a Stun. The decay was nested under the status loop.
- **DoTs ticked one time short.** A `duration 60, interval 15` Burn ticked three times, not four,
  because expiry pre-empted the final tick. Authored numbers should mean what they look like.

**Resolve escalation is linear, not compounding** — +25% *of base* per landed control
(100 → 125 → 150 → 175). Compounding reaches 9× after ten controls, which stops being a curve and
becomes a wall.

**Still inert, deliberately:** `status.recalled_move` stores a Move, so **Mnemonic stays dark
until E4**. `ContentValidator.KnownUnimplementedStatuses` holds exactly that one id — delete the
allowlist when `MoveDefinition` lands.

**Ailment application chances have no source yet** (E5 affixes grant them), so ailments do not
fire in play. The plumbing and the magnitude rule are pinned: an ailment is a fraction of the
*post-mitigation* damage in its own lane, so lane resistance reduces hit and ailment with one
number.

### ✅ E1 shipped — the hit pipeline

`dotnet test` → **493 passing** (was 471). Committed in `99cdceb`.

- **`CombatCalculator` is now a thin façade over `HitPipeline`.** Resolution is an ordered,
  traced sequence over `Packet`s. The old `(DamageType, double)` entry point survives only as the
  D-18 bridge and is deleted in E4.
- **Two bugs found in the design while building it**, both recorded in the docs:
  - **Crit ordering contradicted its own rationale.** §3.2 said "crit multiplies base+flat" but
    the stage list had CRIT at 10 and FLAT ADDED at 11 — so crit would have ignored attribute
    scaling. Order is now flat → crit → increased.
  - **Attribute scaling was applied per packet**, so a hybrid hit got the STR bonus twice and
    adding a 1-damage heat rider was free damage. It is now granted once per hit and split by
    share. Found by rendering a worked trace, not by a test — worth doing again in E2/E3.
- **`ArmourK = 1`** (D-27). Iron armour goes 89% → 53% against a light hit; the `max(1, …)` cliff
  is gone. Recalibrate in C2.
- **Perfect Block is live.** Blocking within 4 ticks of impact negates the hit and still raises
  `Blocked`, so on-block hooks fire on both outcomes (D-06).
- **`ArmorProfile.Resistances` is keyed by LANE, not damage-type name.** `"Slashing": 0.15`
  silently resists nothing now; `ContentValidator` rejects it at load and a test pins the runtime
  behaviour so the two cannot disagree.
- **The Goblin Brute has vulnerabilities** (`Crushing 1.25, Slashing 0.85`) — the first live
  content for D-02, and what makes "swap to the weapon that counters it" real.
- **The Hit Log is wired** to `CombatEncounter.HitResolved` / `LastHit`, surfaced via
  `GameRoot.LastHitLog` and a `ShowHitLog` toggle (off by default — seven lines a swing would
  drown the narration). **Needs a visual check in the editor.**

Sample trace:
```
Flaming Sword — You -> Frost Drake
  Packets       Slashing 80 · Slashing/heat 20
  Scaling       attributes  100 → 105
  Crit          no (50% chance)
  Armour        Slashing — armour 14.4 vs 84 → −15%  84 → 71.71
  Resistance    physical 30%  71.71 → 50.2
  Vulnerability Slashing ×1.2  50.2 → 60.23
  Armour        Slashing/heat — armour 14.4 vs 21 → −41%  21 → 12.46
  Resistance    heat 60%  12.46 → 4.98
  Vulnerability Slashing/heat ×1.2  4.98 → 5.98
  Applied       66 Slashing
```

### ✅ E0 shipped — combat is on the bus

`dotnet test` → **471 passing** (was 459). Committed in `90faf27`.

- **`CombatEncounter` now takes an `IGameEventBus`** and publishes 14 event kinds. **No new
  vocabulary** — only constants that already existed in `GameEvents`. `HitLanded`/`HitAvoided`
  and the rest of §3.1 land in E1 with the packet semantics that distinguish them.
- **`GameRoot` gained the half the audit missed:** it constructed *neither* a `GameEventBus` nor
  a `TriggerRuleEngine`, so `BuildResolver`'s `AttachedRule`s went nowhere. It now owns both and
  re-attaches the build's hooks in `RebuildCharacter()` — swapping a component in the Character
  Lab swaps its live hooks.
- **`heavy` is derived, not authored** — `CombatTuning.HeavyTimeToImpactTicks = 24`. Overhead
  Smash is 48 ticks to impact; everything else is 10–16. That single threshold is what makes
  Exploding Kneecaps and the Venomous burst reachable today.
- **The player is the literal string `"self"` in events**, because shipped Trickster content
  matches `{ "kind": "sourceIsSelf", "text": "self" }`. Known debt: it does not survive allied
  NPCs or multiplayer. `CombatEncounter.SelfId`.
- **`Blocked` is raised from the defender's side** (source = who blocked) so Exploding Kneecaps'
  Guard expression can reach the attacker it detonates against.
- **Effects still land in `Unhandled`** — that is correct until E3 registers handlers, and it is
  now *visible* (`GameRoot.FiredHooks` / `UnhandledHooks`, and a "Live hooks" section appended to
  the Character Lab's `BuildReport()`). **Needs a visual check in the editor.**

Two tests load real `game/data` content and assert the payoff directly: Galvanic charges 6 from a
5-stamina attack (`scales_with: "amount"`), and Exploding Kneecaps detonates on a block.

**All 26 decisions are recorded in `docs/effect-foundation.md` §12, with §12.1 listing the eleven
that changed shape during review. `docs/GDD.md` §5.5, §5.9, §6, §10.2a and §18 have been revised
to match — the GDD does not contradict the decisions anywhere.**

### The handful that will bite if forgotten

- **D-08 is Resolve.** One pool per combatant; controls apply **buildup**, crossing it lands the
  control, opens an immunity window blocking *all* controls, and raises Resolve +25% for the rest
  of the encounter. Stagger is buildup toward Stun, so a build cannot Stun-lock *and* Freeze-lock.
  A direct-chance model was briefly selected in error and reverted — do not reintroduce
  `stagger_threshold` or a per-enemy `control` profile; Resolve already covers both.
- **D-12 introduces the one dangerous failure mode:** a wrong modifier context silently produces a
  wrong number. Closed structurally — `scoped_by` on the key, and **resolving a scoped key without
  its context dimension throws**. Do not add a convenience overload that defaults the context.
- **D-06:** on-block affixes hook `Blocked`, not `HitLanded`. Hooking `HitLanded` means a *perfect*
  block produces no retaliation — punishing the better play.
- **D-01:** lane movement is always `convert` with an explicit fraction. There is deliberately no
  `addAspect` op, so an affix can never quietly relabel a whole strike.
- **D-18:** `AttackProfile` survives on a single-packet bridge through E1–E3 and is deleted in E4.
  Tests written against the bridge are known throwaway.
- **`status.recalled_move` cannot be authored until E4** — it stores a Move. Mnemonic stays inert
  until then, deliberately.

---

## Known debt and filed decisions

Not bugs — deliberate, recorded, and worth not rediscovering.

- **Two provisional tuning constants** that can only be judged by play:
  `QuantizationTuning.PropertyBucket` (the spec calls it the highest-risk number in the design;
  measured at 67% collapse over 2,800 crafts) and `RefinementTuning.StateDeltaCost` (integrity
  currently allows ~20–40 meaningful refinements, looser than the commit-or-lose fantasy implies
  — because the expensive cost terms are traits and signatures, which are P2/P4).
- **Integrity is excluded from material identity** (per spec §12.1), so an archetype keeps the
  integrity of its first discovery. Judged self-balancing; filed in D20, not fixed.
- **D-20 is applied** (M1): the `combat.interval.mult` floor is **0.55** in the registry; the
  two tests that pinned 0.25 moved with it. **D-07 remains unapplied**: it retires
  `combat.dodge.chance` for `combat.avoid.lane` (max 0.25) and `combat.evade.chance` (max 0.15),
  neither of which exists — the dodge key stays additive because re-shaping a key scheduled to
  stop existing is balancing a ghost.
- **Other §4.4 floors the registry does not implement** (flagged during M1, not numbered
  decisions): `resource.cost.mult` ships `min: 0` where §4.4 says floor 0.40, and
  `combat.damage_taken.mult` ships `min: 0` where §4.4 says floor 0.50. User's call.
- **`PropertyDefinition.transferable` is unconsumed.** Give it a job or drop it.
- **Response properties drop on transformation** — iron's authored heat resistance of 60 becomes
  a derived ~14 after any craft. Arguably the more honest number, but a visible discontinuity.
- **`ContentValidator.KnownUnimplementedAbilities` is stale** — the new Bases declare no
  `abilityIds`, so `ability.guard`/`ability.hex_bolt` exist nowhere. Delete the allowlist.
- **`InappropriateOptimismRule` is orphaned** — no suffix references it. Left registered
  deliberately (a working example of a conditional rule); re-attach or remove.
- ~~`docs/current-state.md`~~ **deleted** (D-24a), as was `combat-spec.md`. `PROJECT_STATE.md`
  supersedes it. Recommend deleting.
- **`GameRoot` is ~1,140 lines.** The Application-layer extraction has now been deferred four
  times. Crafting and Character-Lab commands were added as *thin forwards* to keep it from
  getting worse — keep doing that.
- **Iron ore is seeded into the stash at startup** because Mining doesn't exist. Remove when it
  does.

---

## Guardrails
- Keep `dotnet test` green (**602** now) and the build at **0 warnings**, in tested increments.
- Core stays Godot-free. Nothing authoritative in `GameRoot` or the UI.
- Content is data; code owns structure and closed vocabularies (D16). Adding a content type is
  one store on `ContentBundle` plus one line in `ContentLoader.LoadAll`.
- Add a `ContentValidator` rule *and a failing test for it* with every new content type.
- Commit only when the user asks; on `main`, end messages with the Co-Authored-By trailer.
- The user prefers **concise reports and small approved slices**. Present a plan, get approval,
  build one slice, report, repeat. Long planning documents have been explicitly rejected twice.
