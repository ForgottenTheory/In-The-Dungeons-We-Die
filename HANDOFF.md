# HANDOFF.md

For the next Claude session. Read `CLAUDE.md`, then this.

**`docs/GDD.md` is the best single overview of the game** — vision, every system, what's real vs
planned, and the unresolved questions. Read it before `PROJECT_STATE.md` / `SYSTEM_INDEX.md` /
`DECISIONS.md` / `ROADMAP.md`.

## Repo / build state
- Branch `main`, latest commit **`5bdab2e`** (class combinator).
- `dotnet build InTheDungeonsWeDie.slnx` clean (**0 warnings**); `dotnet test` → **459 passing**.
- **Uncommitted:** `docs/GDD.md` and the doc updates in this handoff. The user declined to commit
  the GDD; leave that to them.
- **`GDD/` (untracked) is the user's personal folder. Not project context — leave it alone.**
  `docs/GDD.md` is the project's GDD.
- Godot is **not** on PATH — verify with `dotnet build`/`dotnet test`. The user runs the game
  from their Godot 4.7.1 editor and checks UI visually.

## ⚠️ Before running the game
**Delete `user://save.json`.** `CharacterBuild` ids are persisted and half the roster was
retired — `class.hexslinger`, `prefix.frenzied`/`ironbound`/`pyromaniac`,
`suffix.inappropriate_optimism`/`the_bigger_hammer` no longer exist.

---

## Where we are

Two large systems landed this session, both data-driven and both complete enough to play with.

**Emergent crafting P1** (`9917d68`) — the universal reaction engine. No recipes; 7 processes,
the full algebra, potency/integrity, destruction with byproducts, signature registry, naming,
Reaction Log, pre-commit projection, and a Crafting bench UI. See `docs/emergent-item-system.md`
for the spec and `DECISIONS.md` D20/D21 for what changed.

**The class combinator** (`5bdab2e`) — 15 Bases, 25 Prefixes, 50 Suffixes (10 fully expressed),
composing into 18,750 builds with none of the combinations authored. Built on three new Core
mechanisms: an open modifier vocabulary, a game event bus, and declarative trigger rules. See
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

## Two UI tabs need eyes in the editor

Everything is covered by Core tests **except the Godot UI**, which was written without being run.

- **Crafting bench** — the user has verified this. Looks good.
- **Character Lab** — a layout bug was found and fixed (autowrapping `Label`s inside an
  `HBoxContainer` collapse to one character per line; there is now a `Wrapping()` helper with the
  rule documented). **Re-verify.** Best check: swap the Base from Wizard to Bastion and confirm
  the diff panel reports the channel flip, Held Spell dropping, Guard appearing, and the growth
  deltas.

---

## ✅ THE EFFECT FOUNDATION PACKAGE — **all 26 decisions settled**, nothing built yet

A full design package for a **universal gameplay effect vocabulary** (combat, statuses, moves,
item affixes, profession tools) was written this session. **It supersedes the Move-system plan
below** by putting statuses and the damage pipeline in front of it. Read the entry doc first:

| Doc | Covers |
|---|---|
| **`docs/effect-foundation.md`** | **START HERE** — audit, architecture, triggers/conditions/effects, modifiers & stacking, proc safety, tags, build order, ****26 settled decisions** (§12) + §12.1 change log |
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

**Settled order (D-19):** ✅ **E0** → ✅ **E1** → ✅ **E2** → E3 effect-vocabulary upgrade →
E4 moves → C1 traits/essence → C2 fabrication + scale reconciliation → E5 affixes → E6 tools →
E7 Overreach.

### ✅ E2 shipped — lifecycle split, then statuses

`dotnet test` → **519 passing** (was 493), build 0 warnings. Not committed.

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

`dotnet test` → **493 passing** (was 471), build 0 warnings. Not committed.

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

`dotnet test` → **471 passing** (was 459), build 0 warnings. Not committed.

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

## THE NEXT TASK (superseded — see above): the Move system

This is the largest gap in the game. **No class currently has a class ability** — Bases
contribute growth, gauges and channels but no moves, so builds compose without playing
differently. A plan was proposed and discussed but **not approved**; the user stopped to have the
GDD written. Re-present it before building.

> The effect-foundation package endorses this plan but **reorders it**: statuses before moves,
> because 14 status ids are already authored and Shield Bash needs Stun to exist.

### The key architectural insight
A Move's payload is **exactly what a Prefix or Suffix hook already emits**, so a `MoveDefinition`
is about eight fields rather than the ~25 the brief listed:

```
MoveDefinition
  id, name, moveType, tags[]
  timing        → AbilityTiming        (exists)
  requires[]    → ConditionSpec[]      (exists)
  effects[]     → EffectSpec[]         (exists — 12 kinds, dispatch, handlers)
  costs[]       → {resource, amount}   (new; uniform across stamina/mana/health/gauge)
  cooldownTicks, targeting, interruptible
```

One effect vocabulary shared by moves and hooks. `moveType` is a closed enum used for dispatch
and filtering only — behaviour lives in tags and effects, never a type switch.

### Proposed slices

| # | Slice | Why | Risk |
|---|---|---|---|
| **M0** | **Wire combat to the event bus** | ~20 lines: `CombatEncounter` raises `DamageDealt`, `Blocked`, `Dodged`, `ResourceSpent`, `Killed`. **Galvanic starts charging and Exploding Kneecaps starts detonating with no move system at all.** Makes the entire class system observable and de-risks everything after | Very low |
| M1 | `MoveDefinition` + port the 3 abilities | Data type, validation, tests. No behaviour change | Low |
| M2 | Moveset composition | Sources with provenance and replacement. **Weapon-granted moves are mandatory** — Fighter's identity is "moveset comes from the weapon". Move Viewer lab | Medium |
| M3 | Addressable lifecycle | Split time-to-impact into real telegraph/windup phases so "interrupt during windup" is expressible | **Highest** |
| M4 | Moves in combat | Player casts moves; costs, cooldowns, requirements, effect dispatch | Medium |
| M5 | Statuses | `applyStatus` has no handler; several moves and suffixes want one | Medium |
| M6 | Enemy movesets + AI profiles | Replaces uniform-random ability selection with intent | Medium |

**Strong recommendation: do M0 first and standalone.** An hour of work that makes the class
system visible in a real fight before any moves exist.

### Decisions the user still owes on this
1. M0 first, standalone?
2. **`AttackProfile` and `MoveDefinition` overlap and must converge** — an `AttackProfile` is a
   degenerate Move. Converging touches `EquipmentResolver`, `Combatant`, `CombatCalculator`,
   `CombatEncounter` and their tests, and amends DECISIONS D8 (whose *intent* survives: combat
   would read neutral `MoveDefinition`s instead of neutral `AttackProfile`s). Converge in M2
   (cleaner, riskier) or M4 (safer, temporary duplication)?
3. Range/positioning — declare `targeting` now and defer `range` entirely, or author range as
   unused data?
4. Statuses (M5) before or after enemy AI (M6)?

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
- Keep `dotnet test` green (**459** now) and the build at **0 warnings**, in tested increments.
- Core stays Godot-free. Nothing authoritative in `GameRoot` or the UI.
- Content is data; code owns structure and closed vocabularies (D16). Adding a content type is
  one store on `ContentBundle` plus one line in `ContentLoader.LoadAll`.
- Add a `ContentValidator` rule *and a failing test for it* with every new content type.
- Commit only when the user asks; on `main`, end messages with the Co-Authored-By trailer.
- The user prefers **concise reports and small approved slices**. Present a plan, get approval,
  build one slice, report, repeat. Long planning documents have been explicitly rejected twice.
