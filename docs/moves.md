# Moves, Move Modification & the Shared Action Vocabulary

> **DECIDED** — settled by the 27 decisions in `effect-foundation.md` §12. Not yet built. Part of the effect-foundation package (`effect-foundation.md`).
> Supersedes `AbilityDefinition` and `AttackProfile`; absorbs the retired `combat-spec.md` §5–§9, §21. Amends GDD §6 and DECISIONS **D8**.
> Labels: **[EXISTING/PRESERVE]** · **[DECIDED]** · **[UNRESOLVED]**

---

# 1. Current state

> **2026-08-19 — the spell-library expansion.** The library this document designed now holds
> **517 moves** (474 of them spells across 16 family files, `game/data/moves/spells_*.json`),
> one technique item per learnable move (493 total), and 11 move modifiers including the ten
> weapon imbues. The full manifest — every requested name's fate, shipped / parked-by-blocking-
> system / skipped-as-duplicate — is **`docs/spell-library.md`**. Expansion techniques are
> deliberately in no loot table until the balance pass. The table below is the pre-E4 state
> this document was written against, kept for the reasoning.

| Thing | Reality |
|---|---|
| `AbilityDefinition` | 6 fields: id, name, damageType, baseValue, staminaCost, timing. Three exist. |
| `AttackProfile` | 5 fields, the neutral view of an equipped weapon. Structurally *the same thing* as `AbilityDefinition`. |
| Class abilities | **None.** All 15 Bases declare zero `abilityIds`. |
| `ContentValidator.KnownUnimplementedAbilities` | Stale allowlist for `ability.guard`/`ability.hex_bolt`, which now exist nowhere. Delete it. |
| Enemy AI | `_rng.NextInt(0, AbilityIds.Count)` — uniform random. |

GDD §19.3: *"**This is the largest single gap: no class currently has a class ability.**"*

The `HANDOFF.md` plan is correct. This document endorses it with three amendments and adds the
piece it did not cover: **move modification**.

---

# 2. `MoveDefinition` **[DECIDED]**

## 2.1 The insight, restated

A Move's payload is **exactly what a Prefix or Suffix hook already emits**. So a Move is not 25
fields of bespoke combat data — it is timing plus costs plus a list of `EffectSpec`s from the
same 24-effect vocabulary everything else uses.

```csharp
sealed class MoveDefinition : IDefinition
{
    string Id, Name, Description;
    MoveKind Kind;                       // closed enum — dispatch/filter ONLY, never behaviour
    IReadOnlyList<string> Tags;          // action:* delivery:* form:* mech:*  (the 59-tag vocabulary)

    ActionTiming Timing;                 // telegraph / windup / execution / recovery   [shared]
    IReadOnlyList<ActionCost> Costs;     // resource + amount, incl. gauges              [shared]
    IReadOnlyList<ConditionSpec> Requires;  // the same condition vocabulary             [shared]

    Targeting Targeting;                 // self | enemy | allEnemies | ally | ground
    int  MaxTargets;                     // 1 default; +N from move modifiers
    int  CooldownTicks;
    bool Interruptible;

    IReadOnlyList<Packet> Packets;       // base damage, typed and aspected
    double StaggerPower;
    IReadOnlyList<EffectSpec> Effects;   // riders: applyStatus, applyBarrier, grantResource…
}
```

`MoveKind` exists for dispatch and UI filtering (`Attack`, `Spell`, `Defensive`, `Utility`,
`Reaction`, `Channel`, `Summon`, `ProfessionAction`). **Behaviour never switches on it** — that
is what tags are for. This is the same bargain `ItemType` struck (`emergent-item-system.md` §1:
*"if `Ore` is a type you will write `if (type == Ore)` and the system calcifies"*).

## 2.2 Attack vs Spell is a difference of data, not of engine

Exactly as the brief demands. No `SpellEngine`.

| | **Heavy Strike** | **Fireball** | **Shield Bash** |
|---|---|---|---|
| Tags | `action:attack` `delivery:melee` `form:sword` | `action:spell` `delivery:ranged` `delivery:projectile` | `action:attack` `action:defensive` `delivery:melee` `form:shield` |
| Costs | Stamina 12 | Mana 18 | Stamina 8 |
| Timing | tel 4 / wind 10 / rec 18 | tel 6 / wind 14 / rec 12 | tel 2 / wind 6 / rec 14 |
| Packets | Crushing 24 | Magic/heat 30 | Crushing 10 |
| Stagger | 28 | 4 | **45** |
| Effects | — | `applyStatus status.burn` @ 20% | — (stagger does the stunning via Resolve) |
| Requires | `equippedTag form:sword` | — | `equippedTag form:shield` |

Three completely different feels, one data shape, zero new code per move.

## 2.3 Three amendments to the HANDOFF plan **[DECIDED]**

**1. Split telegraph and windup properly — in E2, ahead of statuses.**
`AbilityTiming.TimeToImpactTicks` currently collapses them, so "interrupt during windup" is
inexpressible (GDD §5.2 flags this as *"the riskiest single change in the combat roadmap"*).
HANDOFF slices it as M3.

> **Corrected [D-27].** This originally said "do it in E1, alongside the pipeline" — which
> contradicted the reasoning used for D-18, where converging `AttackProfile` in E1 was rejected
> for putting two high-risk rewrites in one slice with no green checkpoint between them. The
> same objection applies here and the plan was inconsistent. **Nothing in E1 needs the split;
> the first thing that genuinely does is Stun interrupting an action in E2.** So it moves to the
> front of E2, lands on its own, and statuses build on it.

The four phases become real scheduler states:

```
QUEUE → TELEGRAPH → WINDUP → EXECUTION → RECOVERY → READY
        ▲ visible    ▲ interruptible    ▲ hit resolves    ▲ counter-window
```

### The six phases  *(folded from the retired `combat-spec.md` §5–§9, §21 — D-24a)*

| Phase | What happens | Owned by |
|---|---|---|
| **Queue** | The actor commits. Validated: actor alive · target valid · resources available · move available · actor state permits it (not Stunned, not Feared for `action:attack`, not Silenced for `action:spell`). | `Requires[]` + `Costs[]` |
| **Telegraph** | Intent becomes visible: target, action type, time to impact, area, damage category. **More dangerous enemies may obscure some of it** — a designed lever, not a UI limitation. | `ActionTiming.TelegraphTicks` |
| **Windup** | The committed window. The move can be **interrupted, dodged, blocked, parried or countered** here. This is the phase the current `TimeToImpactTicks` collapse makes inexpressible. | `ActionTiming.WindupTicks`, `Interruptible` |
| **Execution** | The authoritative result — damage, healing, movement, status, resource cost, interrupt. Feeds the Hit pipeline (`damage-and-defense.md` §3). | `Packets[]` + `Effects[]` |
| **Recovery** | The actor cannot immediately act again. **This is what creates the counterattack window**, and it is why Perfect Block and Parry are worth the risk. | `ActionTiming.RecoveryTicks` |
| **Ready** | Available again, subject to `CooldownTicks`. | |

**Interrupts are a move property, not a global rule.** A move declares what an interrupt does to
it: cancel outright · delay · increase recovery · partially refund resources · refund nothing.
That belongs on the move because "the Juggernaut ignores interrupts" and "this channel is fragile
mid-cast" are the same mechanic with different data.

**Player action set.** Today: Attack · Block · Dodge · Wait · Use Item. With moves: everything
above becomes a Move, and the set widens to class abilities, interrupt, counter, taunt, guard
ally, cast, channel — none of which need new engine code, only new `MoveDefinition` rows.

**2. Converge `AttackProfile` and `AbilityDefinition` in E4, not earlier — but accept D8 is
amended. [DECIDED — D-18]** `AttackProfile` cannot express packets, aspects, stagger or riders,
so it cannot survive the affix system. DECISIONS **D8**'s *intent* survives intact: combat reads a
**neutral resolved Move**, never an equipment type; `EquipmentResolver` keeps its seam and starts
producing `MoveDefinition`s instead of `AttackProfile`s. The dependency cycle D8 exists to prevent
is still prevented; only the noun changes.

**The bridge, E1 → E3.** The packet pipeline accepts the old shapes by wrapping them:

```csharp
// temporary, deleted in E4
static IReadOnlyList<Packet> ToPackets(AttackProfile a) =>
    [ new Packet(a.DamageType, Aspect: null, a.BaseDamage) ];
```

Both shapes therefore coexist for three slices. **Accepted cost:** E1–E3 tests written against the
bridge get rewritten in E4. Taken deliberately — E1 is already the slice replacing the entire
damage pipeline, and stacking the moveset rewrite on top would put two high-risk changes in one
slice with no green checkpoint between them.

**Rejected: keeping both permanently.** It looks cheapest and isn't — every affix, move modifier
and effect rider would need to handle both shapes forever, so `AttackProfile` would accrete costs,
tags, effects and packets until it *was* `MoveDefinition` with a different name and its own bugs.

**3. Declare `Targeting` now; defer `range` entirely.** Do **not** author range as unused data —
per GDD §18 Q5 positioning is undecided (U-2), and unused authored numbers rot. `delivery:melee`
and `delivery:ranged` carry enough meaning today for AI and affix matching.

---

# 3. Move modification **[DECIDED — D-18; the new piece]**

The brief's requirement: *"design a clean Move-modification system instead of hardcoding
`if item == ThunderSword`."*

## 3.1 Shape

A `MoveModifier` is a **match + a list of ops**. It is granted exactly like a `StatGrant` or a
`RuleGrant` — by an affix, a class prefix, a status, or a form.

```jsonc
{
  "id": "mod.stormbrand",
  "match": { "tags_all": ["action:attack"], "tags_any": ["form:sword","form:dagger"] },
  "ops": [
    { "op": "convert",     "from": "physical", "to": "charge", "fraction": 0.30 },
    { "op": "addEffect",   "effect": { "kind": "applyStatus", "text": "status.shock" }, "chance": 0.15 },
    { "op": "scaleTiming", "field": "windup", "value": 0.92 }
  ]
}
```

`match` may also name a specific `move_id` — that is how "**Heavy Strike** gains additional Heat
damage" is authored without a code branch.

## 3.2 The op vocabulary — 11 ops cover the brief's entire list

| Op | Effect | Brief's example it satisfies |
|---|---|---|
| `addPacket` | **append a new packet** | *+flat damage*, *adds heat damage* |
| `scaleDamage` | scale packets (lane/type filtered) | *% increased damage to Spells* |
| `convert` | move a fraction between lanes, **always with an explicit fraction** | *convert 30% damage to Charge*, *add Heat aspect* (= fraction 1.0) |
| `addAsExtra` | duplicate a fraction into a new lane | *gain 20% of physical as extra heat* |
| `scaleTiming` | scale telegraph/windup/recovery | *reduced Windup*, *increased Recovery* |
| `scaleCost` | scale a resource cost | *reduced Mana cost* |
| `addTargets` | `MaxTargets += n` | *+1 target* |
| `addChain` | chain to n further targets at a falloff | *a spell chains to another target* |
| `addEffect` | append an `EffectSpec` (with its own chance) | *apply Poison*, *chance to repeat*, *Dodge cleanses* |
| `addTag` | append a tag | makes the move eligible for other modifiers — **the composition lever** |
| `setFlag` | uninterruptible / unblockable / unavoidable | rule switches |

**`addTag` is the quiet one that matters.** An affix that adds `mech:chain` to your Melee moves
lets a *different* affix that matches `mech:chain` fire on them. That is how affixes compose into
builds instead of stacking into a list.

## 3.3 Resolution — cached, not per-hit

```
MovesetBuilder:
  sources (in order):  Base → Species → Prefix → Suffix → equipped forms → affixes →
                       learned → active statuses
  1  collect MoveGrants     → the candidate move list (with provenance and replacement rules)
  2  collect MoveModifiers  → ordered by source
  3  for each move: apply matching ops in a fixed order   → ResolvedMove
  4  cache; invalidate on equipment change, build change, or status apply/expire
```

**Ops apply in a fixed order regardless of source order**, or the same three affixes on different
items would produce different results:

```
addPacket → scaleDamage → convert → addAsExtra → addTargets/addChain
          → scaleTiming → scaleCost → addEffect → addTag → setFlag
```

Note `convert` runs **after** `scaleDamage`, matching the pipeline rule in
`damage-and-defense.md` §3.2: increases apply to the lane the damage started in.

**`addAspect` was cut deliberately (D-01).** A bare "gain the Heat aspect" op retags an entire
packet — which is mathematically 100% conversion, but reads like a free rider and quietly moves
the whole strike into another lane. Folding it into `convert` means **there is exactly one way to
move damage between lanes and it always states a fraction**, so a reviewer can see at a glance
whether an affix adds damage or relabels it. Adding a lane without touching the existing one is
`addPacket`; that distinction is now impossible to blur.

**Replacement.** A `MoveGrant` may `replaces` another move id. Druid forms swapping an entire
moveset, and "Of The Wrong Weapon"-style substitution, both use it. Precedence is source order;
conflicts are reported in the Move Viewer, never silently resolved.

## 3.4 Granting and triggering moves

| Effect | Behaviour |
|---|---|
| `grantMove` | adds a move to the moveset while the granting source is attached — *"an item grants a Move"* |
| `triggerMove` | executes a move **immediately, ignoring cost and cooldown**, at depth+1 — *"Spell triggers another effect"*. Always `once_per_chain`; the triggered move's own effects are at depth 2 and cannot proc further |
| `modifyMove` | attaches a `MoveModifier` for a duration — *"spending Stamina empowers the next attack"* |

`triggerMove` is the most dangerous effect in the vocabulary and gets the strictest proc rules:
`once_per_chain` is **forced**, not defaulted, and the validator refuses a `triggerMove` whose
target move can itself `triggerMove`.

---

# 4. The shared Action vocabulary **[DECIDED — D-18/D-23]**

The brief asks whether Combat Moves and Profession Actions should share an `Action` abstraction,
and warns against inheritance gymnastics. **They are right to warn.**

## 4.1 What is shared, and what is not

| | Combat Move | Profession Action |
|---|---|---|
| interval / timing | ✅ telegraph, windup, execution, recovery | ✅ a single interval |
| costs | ✅ stamina, mana, gauge | ✅ **item inputs**, sometimes stamina |
| requirements | ✅ | ✅ level, tool, inputs |
| tags | ✅ | ✅ |
| start / complete events | ✅ | ✅ |
| **targeting** | ✅ | ❌ |
| **telegraph / interruptibility** | ✅ | ❌ |
| **damage packets** | ✅ | ❌ |
| **outputs, yield, preservation, doubling, quality** | ❌ | ✅ |
| **mastery** | ❌ | ✅ |

## 4.2 The recommendation: share components and events, **not** a base class

**[DECIDED] Do NOT create `abstract class Action`.** A common base would be roughly 60%
nullable fields, and every consumer would branch on which kind it actually has — the god-object
failure this project has avoided everywhere else.

**Share four things instead:**

1. **`ActionTiming`** — a record used by both (`MoveDefinition.Timing`,
   `ProfessionActionDefinition.Timing`).
2. **`ActionCost[]`** — one shape covering resources *and* item inputs.
3. **`ConditionSpec[]`** as requirements — already shared.
4. **The event vocabulary** — `ActionStarted` and `ActionCompleted` are raised by **both**
   executors, tagged `domain:combat` or `domain:profession` plus the action's own tags.

That fourth point is where the payoff is. It means **one trigger vocabulary, two executors**:

```jsonc
// A combat affix
{ "event": "ActionCompleted", "when": [{ "kind": "actionHasTag", "text": "action:attack" }], … }

// A fishing rod affix — same event, same engine, different tag
{ "event": "ActionCompleted", "when": [{ "kind": "actionHasTag", "text": "profession:fishing" }],
  "chance": 0.15, "effects": [{ "kind": "duplicateOutput" }] }
```

Identical machinery. No inheritance. No nullable soup. This is the "shared vocabulary where
useful, not inheritance gymnastics" the brief asked for, and it is the reason a fishing rod and a
sword can be authored by the same person on the same afternoon.

## 4.3 Two executors, two pipelines

| | `MoveExecutor` | `ProfessionExecutor` |
|---|---|---|
| Schedules | telegraph → windup → execution → recovery on the `TickEngine` | interval on the `TickEngine` |
| Produces | a `Hit` → the damage pipeline (`damage-and-defense.md` §3) | a `ProfessionOutcome` → the yield pipeline (`profession-tools.md` §2) |
| Trace | **Hit Log** | **Yield Log** |
| Shared | timing, costs, requirements, tags, `ActionStarted`/`ActionCompleted`, the `TickEngine`, the `IRandomSource`, the `ModifierSet` | |

Two pipelines, one vocabulary. That is the right seam.

---

# 5. Movesets and enemies

## 5.1 Composition sources **[DECIDED]**

In precedence order, each contribution carrying provenance (the `BuildResolver.AttachedRule`
pattern, which already works):

```
Base → Species → Prefix → Suffix → equipped weapon form → equipped other forms
     → item affixes (grantMove) → learned → temporary (statuses, gauges)
```

**Weapon-granted moves are mandatory**, not optional — the Fighter's entire identity is
*"moveset comes from the weapon; reconfigures by re-equipping"* (GDD §3.4). A form declares the
moves it grants; changing weapons changes your moveset. That single rule also gives item forms
gameplay weight beyond stat blocks.

## 5.2 Enemies use the same system **[DECIDED]**

`ActorDefinition` gains a moveset and an **AI profile** — replacing `_rng.NextInt` over
`AbilityIds`, which is the single worst line in the combat code.

An AI profile is a small ordered list of weighted rules using **the same `ConditionSpec`
vocabulary**:

```jsonc
"ai": [
  { "when": [{ "kind": "targetHasStatus", "text": "status.stun" }], "move": "move.execute",  "weight": 100 },
  { "when": [{ "kind": "selfHealthBelow", "value": 0.3 }],          "move": "move.retreat",  "weight": 60 },
  { "when": [],                                                      "move": "move.smash",    "weight": 20 }
]
```

**AI chooses intent; the tick engine resolves timing** (GDD §12.2). Fear works by injecting a
temporary weight penalty on `action:attack` moves — no bespoke fear-AI code.

Auto-combat (GDD §5.7) is the *player* driven by the same profile shape. There is deliberately
no second combat resolver.

---

# 6. Validation **[DECIDED]**

| Rule | Catches |
|---|---|
| Every tag ∈ the 59-tag vocabulary, correct namespace | `mech:sword` |
| Every `Effects[].kind` ∈ the 24-effect vocabulary | typo'd rider |
| Every `Requires[].kind` ∈ the condition vocabulary | typo'd requirement |
| Every `ActionCost.resource` is a real resource or gauge | typo'd cost |
| Every `MoveModifier.match` matches ≥1 move in the game | dead modifier |
| `convert` fractions out of a lane total ≤ 1.0 per move | over-conversion |
| Conversion graph acyclic **and depth 1** | `physical→heat→cold` |
| No `triggerMove` targets a move that itself has `triggerMove` | trigger loop |
| Every `MoveGrant.replaces` names a real move | dead replacement |
| Every move is reachable from some source | orphan content |
| Every `moveKind` has ≥1 move (or is deliberately empty) | half-built dispatch |

## Runtime tests

- **Resolved-move golden tests** — base + N modifiers → assert every field.
- **Idempotence** — applying the same modifier set twice yields the same `ResolvedMove`.
- **Order independence** — shuffling the *source* order of modifiers does not change the result
  (the fixed op order guarantees this; the test proves it).
- **Cache invalidation** — equipment/status/build changes rebuild the moveset.
- **AI determinism** — same seed, same enemy, same state ⇒ same move chosen.

---

# 7. What this changes in existing code

| File | Change |
|---|---|
| `core/Combat/AbilityDefinition.cs` | → `MoveDefinition`; `AbilityTiming` → `ActionTiming`, telegraph/windup split into real scheduler states |
| `core/Combat/AttackProfile.cs` | **deleted**; `EquipmentResolver` produces `MoveDefinition`s (D8 amended, intent preserved) |
| `core/Combat/CombatEncounter.cs` | player and enemy both execute Moves; `BeginEnemyDecision` uses an AI profile |
| `core/Combat/Combatant.cs` | `AbilityIds` → a resolved `Moveset` |
| `core/Content/ContentValidator.cs` | **delete `KnownUnimplementedAbilities`** (stale); add the rules above |
| `game/data/abilities/` | → `game/data/moves/`; the 3 existing abilities port 1:1 |
| Class content | Bases finally declare moves — **the GDD's largest gap closes here** |
