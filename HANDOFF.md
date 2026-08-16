# HANDOFF.md

For the next Claude session. Read `CLAUDE.md`, then this.

**`docs/GDD.md` is the best single overview of the game** — vision, every system, what's real vs
planned, and the unresolved questions. Read it before `PROJECT_STATE.md` / `SYSTEM_INDEX.md` /
`DECISIONS.md` / `ROADMAP.md`.

## Repo / build state
- Branch `main`, latest commit **`2bf9902`** (E3a). Before it: `99cdceb` (E1 + E2),
  `90faf27` (E0), `f55349c` (the effect-foundation design package).
- `dotnet build InTheDungeonsWeDie.slnx` clean (**0 warnings**); `dotnet test` → **554 passing**.
- ⚠ **Uncommitted: E3b is complete and green in the working tree.** Files:
  `core/Modifiers/ModifierScope.cs` (new) · `core/Modifiers/ModifierKeyDefinition.cs` ·
  `core/Modifiers/ModifierSet.cs` · `core/Content/ContentValidator.cs` ·
  `game/data/modifier_keys/modifier_keys.json` · `tests/Modifiers/ModifierSetTests.cs` ·
  `tests/Content/ContentValidatorTests.cs` · doc updates. Commit it before starting E3c.
- **`GDD/` (untracked) is the user's personal folder. Not project context — leave it alone.**
  `docs/GDD.md` is the project's GDD and *is* committed (as of `f55349c`).
- Godot is **not** on PATH — verify with `dotnet build`/`dotnet test`. The user runs the game
  from their Godot 4.7.1 editor and checks UI visually.

## ⚠️ Godot-side work that has never been run
Three things were written without being executed, because Godot is not on PATH here. All are
presentation-only and low risk, but none is verified:
- **Character Lab "Live hooks" panel** (E0) — fired/unhandled counts + the last six firings,
  appended to `BuildReport()`.
- **The Hit Log** (E1) — `GameRoot.LastHitLog` and a `ShowHitLog` toggle, wired but unrendered.
  Worth deciding whether the Combat tab shows the last trace permanently or behind the toggle.
- **Crafting bench / Character Lab layout** (pre-existing) — the user has verified Crafting; the
  Character Lab had an autowrap bug fixed but not re-checked.

## ⚠️ Before running the game
**Delete `user://save.json`.** `CharacterBuild` ids are persisted and half the roster was
retired — `class.hexslinger`, `prefix.frenzied`/`ironbound`/`pyromaniac`,
`suffix.inappropriate_optimism`/`the_bigger_hammer` no longer exist.

---

## Where we are

**The combat foundation is being built, slice by slice.** Design settled first (27 decisions),
then E0 → E1 → E2 → E3a. Full plan and rationale: `docs/effect-foundation.md` §10 and §12.

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

## ✅ THE EFFECT FOUNDATION PACKAGE — **all 27 decisions settled**; E0–E3a built

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

**Settled order (D-19):** ✅ **E0** → ✅ **E1** → ✅ **E2** → 🔄 **E3** (a and b done, c next) →
E4 moves → C1 traits/essence → C2 fabrication + scale reconciliation → E5 affixes → E6 tools →
E7 Overreach.

### 🔄 E3 — E3a committed, E3b done and **uncommitted**; E3c is next

> ⚠ **Uncommitted work in the tree: E3b.** Complete and green. **Commit it before starting
> E3c**, or the two slices tangle the way E1/E2 did (they had to share a commit because they
> interleaved in four files). File list is under "Repo / build state" above.

### ✅ E3b shipped — scoped modifier contributions (D-12)

`dotnet test` → **554 passing** (was 532), build 0 warnings. **Uncommitted.**

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
  chain restarts at depth 0 and the entire budget becomes decorative. E3c's handlers are the
  first real test of this.
- **`OncePerChain` defaults to true.** Content opts *into* risk; it never opts out of safety by
  omission.

### E3c — the effect handlers  ← **START HERE**

Register handlers so effects stop landing in `Unhandled`: `damage`, `applyStatus`,
`grantResource`, `heal`, `areaDamage`, `interrupt` at minimum, plus the new condition kinds
(`targetHasStatus`, `selfHasStatus`, `resourceAbove`/`Below`, `equippedTag`, `hitHasLane`,
`actionHasTag`). **This is where Galvanic's Charge finally accumulates.**

⚠ **Every handler must propagate `invocation.Context`** onto any event it raises. Forget it once
and the chain restarts at depth 0, making the whole proc budget decorative. This is the first
slice where that discipline is actually exercised.

⚠ **E3c is also the first code that resolves modifiers, so it is where `ModifierContext` gets
built for real.** `ModifierSet.Resolve` has had **no production caller** through E3b — a handler
that reaches for `combat.damage.flat` must supply a `move_tag`, and one that reaches for anything
`profession.*` must supply a `profession`, or it throws. That throw is the feature. Build the
context from the event/hit being handled; do not reach for `ModifierContext.None` to make a call
compile.

### A method note worth repeating

The two best bugs of E1/E2 were found by **rendering a worked example and reading the numbers**,
not by a test: attribute scaling applied per packet (so splitting a hit was free damage), and the
crit ordering contradicting its own spec. Before calling E3 done, wire a real fight with statuses
and scoped modifiers on and *read the Hit Log*. Cheap, and it has paid twice.

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
- **Two settled decisions the modifier registry does not implement yet** (found during E3b, left
  alone because both are balance numbers): **D-20** sets the `combat.interval.mult` floor at
  **0.55** and the registry has **0.25**; **D-07** retires `combat.dodge.chance` for
  `combat.avoid.lane` (max 0.25) and `combat.evade.chance` (max 0.15), neither of which exists.
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
- Keep `dotnet test` green (**554** now) and the build at **0 warnings**, in tested increments.
- Core stays Godot-free. Nothing authoritative in `GameRoot` or the UI.
- Content is data; code owns structure and closed vocabularies (D16). Adding a content type is
  one store on `ContentBundle` plus one line in `ContentLoader.LoadAll`.
- Add a `ContentValidator` rule *and a failing test for it* with every new content type.
- Commit only when the user asks; on `main`, end messages with the Co-Authored-By trailer.
- The user prefers **concise reports and small approved slices**. Present a plan, get approval,
  build one slice, report, repeat. Long planning documents have been explicitly rejected twice.
