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

**Settled order (D-19):** ✅ **E0 done** → E1 hit pipeline → E2 statuses → E3 effect-vocabulary
upgrade → E4 moves → C1 traits/essence → C2 fabrication + scale reconciliation → E5 affixes →
E6 tools → E7 Overreach.

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
