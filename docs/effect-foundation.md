# The Effect Foundation — Design Package

> **Status: PROPOSAL. Nothing here is built.** This is the entry document for a design package
> that establishes one universal gameplay-effect vocabulary for combat, statuses, moves, item
> affixes and professions.
>
> Written against commit `5bdab2e`, 459 passing tests. Every claim about the current code was
> verified against the code, not the docs.
>
> **Labels used throughout:** **[EXISTING/PRESERVE]** · **[PROPOSED]** · **[UNRESOLVED]**

## The package

| Doc | Covers |
|---|---|
| **`effect-foundation.md`** (this) | Audit · the architecture · triggers/conditions/effects · modifiers & stacking · proc safety · tags · implementation order · decisions needing approval |
| `damage-and-defense.md` | Damage types × aspects · the resolution pipeline · defence layers · resistance/penetration/inversion · avoidance vs mitigation · thorns |
| `statuses.md` | Status taxonomy · ailments/impairments/controls/states · Resolve & the CC problem · the status data contract |
| `moves.md` | The Move model · move modification · the shared Action vocabulary |
| `affixes.md` | Item affix architecture · Material Genetics → eligibility/weight/tier · crafting operations · Overreach |
| `profession-tools.md` | The profession outcome pipeline · tool forms and affixes |
| `effect-catalog.md` | 254 starter modifier concepts for design review |
| `worked-examples.md` | 10 build examples · 4 tool examples · 8 worked resolution traces |

**Supersession.** This package supersedes `combat-spec.md` §15–16 and §22–25 (damage types,
pipeline, blocking, dodging, crits, statuses) and extends `emergent-item-system.md` §16
(fabrication) rather than replacing it. `docs/GDD.md` §5 and §6 get amended, not rewritten.
Nothing here contradicts the GDD without saying so explicitly — see §11.

---

# 1. Audit — what actually exists

## 1.1 The verdict up front

**You already built the right spine and then didn't connect it to anything.**

`GameEvent` + `TriggerRule` + `ConditionSpec` + `EffectSpec` + `ModifierKeyDefinition` +
`ModifierSet` is, structurally, the universal effect vocabulary this brief asks for. It has
provenance, seeded chance, cooldowns, data-defined targets with clamps, and a deliberate
"unhandled effects are recorded, not dropped" policy that is exactly right for a system being
built in slices.

The problem is not the design. It is that **the spine has no organs**:

- `CombatEncounter` raises **zero** `GameEvent`s. Verified: no `Publish`, no `GameEvent`
  reference anywhere in `core/Combat/`. Every combat trigger rule in the game is dead.
- **14 status ids are authored in `prefixes.json`/`suffixes.json` and none exist.** There is no
  `StatusDefinition`, no `StatusInstance`, no status controller anywhere in Core.
- `EffectSpec` has **one** effect, not a list, and **no target selector**. `areaDamage` has no
  way to say who it hits.
- `ModifierSet` is never consulted by `CombatCalculator`. The 51-key registry currently
  influences nothing in a fight.
- `EquipmentResolver` maps exactly two properties (Mass → damage/windup, Hardness → armour).
  The seam is real and correctly placed; it is simply empty.

So this package is mostly **connection and generalisation**, not replacement. That is the good
news, and it is why the phasing in §10 can start with a 20-line slice.

## 1.2 Combat — `core/Combat/`

| Item | State | Detail |
|---|---|---|
| `CombatEncounter` (329 lines) | ✅ works | Tick-driven; enemy self-schedules telegraph→execute→recovery; player Attack/Block/Dodge/Wait/UseItem. Single enemy, single position. Enemy AI is `_rng.NextInt` over `AbilityIds` — uniform random. |
| `CombatCalculator.Resolve` | 🟡 placeholder | 30 lines, one method, fixed order: dodge check → STR/INT scaling → crit → flat armour → typed resistance → block multiplier → clamp. No hooks, no trace, no packets, no events. |
| `DamageType` | ✅ preserve | Slashing/Crushing/Piercing/Magic + `IsPhysical`. |
| `AttackProfile` / `ArmorProfile` | ✅ preserve the *seam* | Neutral profiles; combat never sees equipment types (D8). `ArmorProfile.Resistances` is a `Dictionary<string,double>` keyed by damage-type name — already lane-shaped. |
| `AbilityDefinition` | 🧱 scaffold | 6 fields: id, name, damageType, baseValue, staminaCost, timing. Three exist (`Strike`, `Rusty Slash`, `Overhead Smash`). This is the degenerate Move. |
| `AbilityTiming` | 🟡 partial | `TelegraphTicks`/`WindupTicks`/`RecoveryTicks` are all present, but `TimeToImpactTicks` collapses telegraph+windup, so "interrupt during windup" is inexpressible. |
| `CombatTuning` | 🟡 placeholder | 14 constants. `MaxResistance = 0.75`, `BlockDamageMultiplier = 0.4`, `MinimumDamage = 1`. |
| `Combatant` | ✅ preserve | Shares the player `Character`'s real pools (attrition persists); reads `EffectiveAttributes`; holds `BlockUntilTick`/`DodgeUntilTick`. Block/dodge as **timed stances** is the single best thing in combat today. |

**What this proposal supersedes:** `CombatCalculator.Resolve`'s body and signature.
**What it preserves:** the neutral-profile seam, the timed stances, the tick lifecycle, `Combatant`.

## 1.3 Moves / abilities

Nothing beyond `AbilityDefinition`. **No class in the game has a class ability** — the 15 Bases
declare growth, gauges and channels but no `abilityIds`. `ContentValidator` still carries a
`KnownUnimplementedAbilities` allowlist for `ability.guard`/`ability.hex_bolt`, which now exist
nowhere (noted in HANDOFF as stale).

The Move plan in `HANDOFF.md` is **correct and I endorse it**, with two amendments in
`moves.md` §2.

## 1.4 Modifiers — `core/Modifiers/`

**[EXISTING/PRESERVE — this is the best-built thing in the repo for our purposes.]**

- `ModifierKeyDefinition`: id, name, kind (additive/multiplicative/flag), default, min, max,
  family, description, `lower_is_better`. **Clamps live on the key**, so combat-spec §11's
  minimum-interval rule is data.
- `ModifierSet`: contributions carry `Source` provenance; `Resolve(key, base)` honours kind and
  clamps; unknown keys **throw**.
- 51 keys registered across attribute/resource/offence/timing/defence/rule/profession/craft/realm/loot.

**Three gaps this package closes:**
1. No **scope** on a contribution — cannot express "+10 damage *with swords*" or "−12% interval
   *for Fishing*". This is PoE's local-vs-global problem and Melvor's per-skill problem, and one
   mechanism solves both (§4.2).
2. Only three stacking kinds. Missing `highest_only` and `diminishing`, both of which are
   required to make avoidance and preservation safe (§4.3).
3. Nothing in combat reads it.

## 1.5 Event hooks — `core/Events/`

**[EXISTING/PRESERVE.]** `GameEvent(Kind, Source, Target, Amount, Tags, Values)` — deliberately
uniform so JSON rules can match without a C# case per event. 30 event constants. `GameEventBus`
is **synchronous, ordered, and re-entrancy-safe** (handler-raised events queue and drain after).
That last property is already half of proc safety and was clearly designed for determinism.

Gap: nothing publishes combat events; the vocabulary needs ~8 additions (§3.1).

## 1.6 Prefix/Suffix rules — `core/Rules/`

**[EXISTING/PRESERVE the shape.]** `TriggerRule { id, event, when[], effect, cooldown_ticks,
chance, description }`, interpreted by `TriggerRuleEngine`. 11 condition kinds, 12 effect kinds.
Chance is rolled **after** conditions so unrelated content changes don't shift a seeded run —
a genuinely thoughtful detail worth keeping.

Authoring is already excellent. From `prefixes.json`:

```jsonc
{ "id": "discharge", "event": "DamageDealt",
  "when": [ { "kind": "gaugeAtLeast", "value": 0.5 } ],
  "effect": { "kind": "areaDamage", "amount": 0.3, "scales_with": "amount" },
  "cooldown_ticks": 30 }
```

**Four gaps:**
1. `effect` is singular. "Apply Shock **and** restore Stamina" needs two rules with duplicated
   conditions and independent chance rolls — wrong semantics.
2. No **target selector**. Who does `areaDamage` hit?
3. No **proc context**. Nothing tracks depth, chain identity, or whether an effect's own output
   may re-trigger. Thorns→Shock→retaliate→Thorns is currently only prevented by the fact that
   none of it exists.
4. Conditions can only read the *event*, never the *world* ("target is Frozen", "equipped form
   is Sword").

**[PROPOSED]** All four are additive changes to existing types. No rewrite.

## 1.7 Attributes / resources — `core/Characters/`

**[EXISTING/PRESERVE.]** 7 attributes, 3 resources (Health/Mana/Stamina, no auto-regen in
combat), `ResourceCalculator`, `ModifierPipeline` (base→add→multiply→clamp), and the class
combinator (`BuildResolver`, 15/25/50 with gauges and channels).

Gap relevant here: **gauges are declared but nothing consumes them in combat**, and `Mana` is
spent by nothing. `resource.gauge.*` keys exist.

## 1.8 Equipment — `core/Equipment/`, `core/Items/`

- `ItemInstance` — 8 fields including `PropertySet Properties`, `Provenance`, and
  `IReadOnlyList<string> Traits` (**reserved, unused**). **This is where affixes will live.**
- `EquipmentDefinition` — slot + Weapon/Armor stat blocks + flat `properties`.
  Four items authored. Base properties are on a **~0–5 scale**.
- `EquipmentResolver` — Mass→damage/windup, Hardness→armour. 44 lines. Explicitly documented as
  "the single place the future material→combat rules will grow".

⚠ **The 0–100 vs 0–5 scale mismatch is real and blocks affixes.** `equip.iron_sword` has
`hardness: 4`; `material.iron_ingot` has `hardness: 65`. GDD §18 Q3 flags this as "a combat
rebalance, not a mapping change". §10 budgets it explicitly.

## 1.9 Emergent crafting — `core/Crafting/`

**[EXISTING/PRESERVE — do not touch.]** P1 is complete and it is the strongest system in the
game: `ReactionEngine` as a total function, convergence as the anti-inflation rule, off-channel
dilution, opposition, potency-as-weighted-mean, integrity-as-budget, destruction with
byproducts, signature identity, naming, and the Reaction Log.

**Not built (P2–P6):** traits, essence, signature reactions, **fabrication**, codex/assay.

⚠ **Dependency that shapes the whole plan: item affixes require fabrication (P5a).** There is
currently no path from a material to a piece of equipment. See §10.

## 1.10 Professions — `core/Professions/`

`ActionResolver.Resolve` is 20 lines: copy outputs → roll bonus outputs with a mastery+performance
bonus → scale XP. That is the entire outcome pipeline.

- **Mastery is stored and read by exactly one thing** (`MasteryBonusChance`).
- No interval modifiers, no preservation, no doubling, no quality, no rare-weighting.
- The `profession.*` modifier keys exist (interval/yield/preserve/double/xp/mastery) and
  **nothing reads any of them**.
- No tool concept at all. `EquipmentSlot` is `{ Weapon, Armor }`.

**[PROPOSED]** This is the least-built system relative to its documented ambition, and it is
also the cheapest to fix once scoped modifiers exist.

## 1.11 Statuses

**Nothing exists.** Not a class, not an enum, not a handler. Meanwhile:

- 14 status ids are authored in shipped content and land in `TriggerRuleEngine.Unhandled`.
- `applyStatus` is in `RuleVocabulary.Effects` with no handler.
- `GameEvents.StatusApplied`/`StatusExpired` exist and are never raised.
- GDD §5.9 lists bleed/poison/burn/stun/slow/vulnerable/guarded as "authored and currently inert".

**This is the single largest ratio of authored content to implemented system in the project.**

## 1.12 Summary table

| System | Exists | Placeholder | Planned | Superseded by this package |
|---|---|---|---|---|
| Tick engine, event bus, modifier registry, trigger rules | ✅ | | | — (extended) |
| Combat encounter, timed stances | ✅ | | | — |
| `CombatCalculator` | | 🟡 | | **yes — replaced by the Hit pipeline** |
| `AbilityDefinition`, `AttackProfile` | | 🟡 | | **yes — converge into `MoveDefinition`** |
| Statuses | | | ⬜ | new |
| Damage aspects, resistance lanes, penetration | | | ⬜ | new |
| Item affixes | | | ⬜ | new (needs fabrication P5a) |
| Profession outcome pipeline / tools | | 🟡 | ⬜ | **yes — `ActionResolver` replaced** |
| Emergent crafting P1 | ✅ | | | — (never touched) |
| Class combinator | ✅ | | | — (finally becomes live) |
| Three physical resistance modifier keys | | 🟡 | | **yes — collapsed to one `physical` lane** |
| `combat.dodge.chance` as a primary defence | | 🟡 | | **yes — demoted, see D-07** |

---

# 2. The core architectural decision

## 2.1 One authoring vocabulary, two runtime paths **[PROPOSED]**

The most tempting mistake here is to make everything a trigger rule so there is "one system".
**Do not.** `+10 Maximum Health` must not travel through an event bus.

The correct unification is at the **authoring** layer, not the **execution** layer:

```
                         AffixDefinition / StatusDefinition / PrefixDefinition /
                         SuffixExpression / MoveEffect / ToolAffix
                                        │
                          all of them carry a list of
                                        ▼
                                    Grant[]
                                   /        \
                      StatGrant  ◄─┘          └─►  RuleGrant
              (modifier key + value + scope)        (a TriggerRule)
                          │                                │
                          ▼                                ▼
                    ModifierSet                     TriggerRuleEngine
              summed table, read O(1)          event dispatch, ordered, proc-guarded
                          │                                │
                          └──────────► the Hit pipeline ◄──┘
                                    reads both, traces both
```

**A `Grant` is the atom of the entire game's effect vocabulary.** An item affix, a status's
while-active effect, a class prefix mechanic, a move's rider and a fishing rod's bonus are all
lists of `Grant`s. They differ only in what attaches them and when they detach.

Why two paths:

| | `StatGrant` | `RuleGrant` |
|---|---|---|
| Shape | key + value + optional scope | event + conditions + effects |
| Cost | summed once when equipment changes | evaluated per matching event |
| Answers | "how much?" | "what happens when?" |
| Player reads it as | `+42 Maximum Health` | `On Block: 25% chance to Shock the attacker` |
| Provenance | `ModifierContribution.Source` (exists) | `EffectInvocation.Source` (exists) |

Both already exist. Both already carry provenance. This decision is mostly *recognising* what
you built.

## 2.2 Why not the schema in the brief

The brief proposes `Trigger + Conditions + Source + Target + Effect + Magnitude + Chance +
Duration + Cooldown + Tags` as one flat shape. That is very close to `TriggerRule` and it is a
good shape — but as *the* universal shape it has two problems:

1. **It forces every static stat into a fake always-on trigger.** 60% of the affix catalog is
   `+X to a number`. Routing those through an event bus is a performance and legibility loss for
   zero gain.
2. **`Magnitude` and `Chance` belong to the effect, not the rule**, once a rule has multiple
   effects. "25% chance to Shock **and** restore 8 Stamina" — one roll, two effects — is
   expressible only if chance sits on the rule and magnitude sits on each effect.

So: **keep the brief's shape for `RuleGrant`, and let `StatGrant` be the degenerate case that
skips the bus.** That is the design.

## 2.3 The three legibility artefacts **[PROPOSED — required scope]**

The Reaction Log is the most successful design decision in this project. GDD principle §17.5
says legibility is *required scope*, not polish. This package holds itself to the same standard
with three artefacts, each the direct analogue of the Reaction Log:

| Artefact | Answers | Where |
|---|---|---|
| **Hit Log** | "why did that hit for 43?" | `damage-and-defense.md` §3 |
| **Genome Readout (Assay)** | "what *can* this item roll, and up to what tier?" | `affixes.md` §4 |
| **Yield Log** | "why did I get 3 fish?" | `profession-tools.md` §2 |

Each is an ordered, structured trace emitted by its pipeline, human-readable, and asserted by
golden tests. **A pipeline that cannot explain itself is not finished.**

---

# 3. Triggers, conditions, effects

## 3.1 Triggers — the event vocabulary **[EXISTING + 8 additions]**

The brief lists ~29 candidate triggers. Most already exist or collapse into an existing event
plus a condition. **Do not create `OnCriticalHit`** — that is `DamageDealt` + `hasTag: critical`.
That collapse is the whole reason the event record carries a tag set.

**Keep all 30 existing events.** Additions:

| New event | Why it can't be a condition on an existing one |
|---|---|
| `HitLanded` | Distinct from `DamageDealt`: a hit that landed but dealt 0 must still trigger thorns (§8) |
| `HitAvoided` | Dodge/parry/negate are one concept with a `via` tag; `Dodged` becomes an alias |
| `Parried` | New defensive outcome with its own counter-window |
| `DamageMitigated` | Carries `amount_prevented` — the basis for "return % of mitigated damage" |
| `BarrierBroken` | Barrier is a distinct pool; its depletion is a trigger point |
| `ControlResisted` | Resolve absorbed a control attempt — the hook for "on resisting control…" |
| `ActionStarted` / `ActionCompleted` | **Domain-neutral**: raised by combat *and* professions, tagged `domain:combat`/`domain:profession`. This is the shared-Action seam (§`moves.md` §4) |
| `OutputProduced` | Per-output, so "chance to duplicate" hooks one place for gathering, crafting and loot |

**Removed as redundant:** `Dodged` (→ `HitAvoided` + `via:dodge`), and the brief's
`OnCriticalHit` / `OnStatusReceived` / `OnRareOutput` / `OnMasteryGain` / `OnProfessionSuccess`
(all = existing event + condition).

Net: **38 events.** That is a closed, validated vocabulary — the same bargain
`ModifierKeyDefinition` and `PropertyDefinition` already struck.

## 3.2 Conditions **[EXISTING 11 + 9 additions]**

Current conditions can only read the event. Effects need to read the **world**. `ConditionSpec`
keeps its uniform `{kind, value, text, negate}` shape; the evaluator gains an
`IConditionContext` (self, target, encounter, equipment, realm) alongside the event.

| Existing (11) | keep all |
|---|---|
| `hasTag` `amountAtLeast` `amountAtMost` `valueAtLeast` `valueAtMost` `sourceIsSelf` `targetIsSelf` `selfHealthBelow` `selfHealthAbove` `gaugeAtLeast` `firstInEncounter` | |

| New | Reads | Example |
|---|---|---|
| `targetHasStatus` | world | `target is Frozen` |
| `selfHasStatus` | world | `while Guarded` |
| `targetHasTag` | world | `target is Boss` / `origin:fauna` |
| `resourceAbove` / `resourceBelow` | world | `Stamina above 75%` (text = resource) |
| `equippedTag` | world | `equipped form is Sword` |
| `hitHasAspect` | event | `incoming damage has Heat Aspect` |
| `hitHasLane` | event | for lane-specific avoidance |
| `realmHasAffix` | world | `current Realm is Volatile` |
| `actionHasTag` | event | `Profession:Fishing`, `Move has tag Spell` |

**Composition [DECIDED — D-11].** `when[]` is **pure AND, permanently**. No `anyOf`, no boolean
tree — a nested condition structure is unauthorable in JSON, breaks auto-generated tooltips, makes
the static proc-cycle analyser walk a tree, and turns "why didn't this fire?" into a debugging
session.

OR is expressed two ways, in this order of preference:
1. **A derived tag**, when the OR is really a category (§5 — `tier:notable` is the worked case).
2. **Two rules** with the same effect block, when it genuinely isn't (`health below 30% or above
   80%`).

## 3.3 Effects **[EXISTING 12 → PROPOSED 24]**

The brief lists ~45 effect primitives. Most collapse. The rule for keeping one:
**an effect kind exists only if it needs its own handler code.** Anything expressible as
`grantModifier` with a different key is *not* a new effect.

That single rule deletes 18 of the brief's candidates:

| Brief candidate | Disposition |
|---|---|
| `ModifyStat`, `ModifyResistance`, `ModifyInterval`, `ModifyOutputQuantity`, `ModifyOutputQuality`, `ModifyRareOutputWeight`, `ModifySuccessChance`, `ModifyMasteryXP`, `ModifySkillXP`, `ModifyCraftsmanship`, `ModifyMaterialIntegrityCost`, `ModifyCatalystEffect`, `ModifyHarvestResistance`, `PreserveInput`, `ModifyDamage` | **all → `grantModifier` with a different key.** Zero new handlers. |
| `AddBonusOutput` | → `DuplicateOutput` with a target selector |
| `SpendResource` | → `drainResource` (exists) |
| `RemoveStatus`, `CleanseStatusCategory` | → one `removeStatus` (Text = id **or** category) |
| `Retaliate`, `ReflectDamage` | → `damage` with `source: retaliation` tag and a scaling basis |
| `AddDamageAspect`, `ConvertDamage` | → `modifyMove` ops, not runtime effects (`moves.md` §3) |
| `BiasOutputFamily` | → `grantModifier` on a weight key with a scope |

**Final effect vocabulary (24).** 12 existing + 12 new:

| Existing (12) | New (12) |
|---|---|
| `damage` | `applyBarrier` |
| `areaDamage` | `removeStatus` |
| `heal` | `negateHit` |
| `applyStatus` | `penetrateResistance` |
| `grantModifier` | `invertResistance` |
| `grantResource` | `stealResistance` |
| `drainResource` | `duplicateOutput` |
| `spawnEntity` | `preserveInput` |
| `grantItem` | `modifyMove` |
| `revealInfo` | `grantMove` |
| `reposition` | `triggerMove` |
| `interrupt` | `delayAction` |

Each new one needs handler code: they touch the hit pipeline mid-resolution
(`negateHit`, `penetrateResistance`, `invertResistance`), a pool that isn't a modifier
(`applyBarrier`), the moveset (`modifyMove`, `grantMove`, `triggerMove`), the scheduler
(`delayAction`), or an outcome list (`duplicateOutput`, `preserveInput`).

`preserveInput` and `duplicateOutput` are borderline — they could be modifier keys — but they
need to *see the specific item* being consumed/produced to bias by material tag, so they are
handlers.

## 3.4 The upgraded `RuleGrant` **[PROPOSED]**

```jsonc
{
  "id": "storm_riposte",
  "event": "HitLanded",
  "when": [ { "kind": "targetIsSelf", "text": "self" },
            { "kind": "hitHasLane",  "text": "charge" } ],
  "chance": 0.25,
  "cooldown_ticks": 40,
  "target": "triggerSource",              // NEW
  "proc": { "max_depth": 1, "once_per_chain": true },   // NEW, defaulted
  "effects": [                            // NEW: was a single `effect`
    { "kind": "applyStatus", "text": "status.shock", "amount": 1, "duration_ticks": 60 },
    { "kind": "grantResource", "text": "Stamina", "amount": 8 }
  ],
  "description": "Charge that reaches you feeds back."
}
```

Changes from today, all additive and all backward-compatible with a deserialiser shim
(`effect` → single-element `effects`):

| Field | Values |
|---|---|
| `effects[]` | replaces `effect`; one chance roll, N effects |
| `target` | `self` · `triggerSource` · `triggerTarget` · `allEnemies` · `allAllies` · `randomEnemy` · `lowestHealthEnemy` |
| `proc` | `max_depth` · `once_per_chain` · `once_per_target` · `icd_ticks` (per-target cooldown) |

**[EXISTING/PRESERVE]** `chance` still rolls after conditions. `cooldown_ticks` still keys on
`source|ruleId`. Unhandled effects still record rather than throw.

---

# 4. Modifiers and stacking

## 4.1 Modifier keys **[EXISTING/PRESERVE, extended]**

`ModifierKeyDefinition` gains three fields:

```jsonc
{ "id": "combat.avoid.lane", "name": "Hit Avoidance", "kind": "diminishing",
  "family": "avoidance", "max": 0.25,
  "scoped_by": "lane",              // NEW — this key requires a scope
  "danger": true }                  // NEW — validator demands a cap
```

- **`kind`** gains `highest_only` and `diminishing` (`1 − Π(1 − xᵢ)`).
- **`scoped_by`** names the scope dimension (`lane`, `aspect`, `profession`, `move_tag`,
  `form`, `status`) — or absent for global keys.
- **`danger`** marks a family the validator refuses to load without a `max` (§9).

## 4.2 Scoped contributions — the local/global answer **[DECIDED — D-12]**

`ModifierContribution` gains `Scope` — a small tag predicate:

```csharp
record ModifierContribution(string Key, double Value, string Source, ModifierScope? Scope);
record ModifierScope(string Dimension, string Value);   // ("move_tag","sword")
```

`ModifierSet.Resolve(key, baseValue, context)` filters contributions whose scope doesn't match
the context. Unscoped contributions always apply.

**One mechanism, four problems solved:**

| Problem | Expression |
|---|---|
| PoE **local** weapon mod (`+20% physical damage` on the weapon) | `combat.damage.mult`, scope `item:self` |
| PoE **global** mod (`+20% physical damage` on a ring) | `combat.damage.mult`, no scope |
| Melvor **per-skill** tool bonus (`−12% Fishing interval`) | `profession.interval.mult`, scope `profession:fishing` |
| Move-tag scoping (`+8 flat damage to Melee moves`) | `combat.damage.flat`, scope `move_tag:melee` |

This is the highest-leverage single change in the package. Without it the modifier registry must
fork per profession and per weapon class, and the key count goes from 51 to roughly **330**.

### 4.2.1 The eight scope dimensions

`lane` · `aspect` · `essence` · `profession` · `move_tag` · `form` · `item` · `status`

Closed and validated, like every other vocabulary here. A contribution carries at most one scope;
a modifier needing two dimensions (*"+8 damage to Melee attacks with Swords"*) is authored as two
contributions from the same source, both of which must match.

### 4.2.2 Closing the wrong-context failure mode

**The risk this decision introduces:** resolution stops being a pure key lookup, so passing the
wrong context produces a **silently wrong number** rather than a crash. That is the worst failure
shape in the package — worse than a missing feature, because nothing surfaces it.

A required parameter prevents *forgetting* a context. It does not prevent a *wrong* one. Three
structural guards do:

**1. `scoped_by` makes the requirement declarative.** A key states which dimension it may be
scoped by:

```jsonc
{ "id": "profession.interval.mult", "scoped_by": "profession", … }
{ "id": "combat.damage.flat",       "scoped_by": "move_tag",   … }
{ "id": "resource.max_health"                                   }   // unscoped, global only
```

**2. Resolving a `scoped_by` key with a context that lacks that dimension throws.**

> `Resolve("profession.interval.mult", base, context)` where `context` carries no `profession`
> **throws**. It does not return the unscoped subtotal, and it does not return the baseline.

This converts the entire failure mode from *silently wrong* to *loud at the call site* — exactly
the bargain `ModifierSet.Add` already strikes by throwing on unknown keys. **[EXISTING/PRESERVE]**
that instinct; extend it here.

**3. A contribution whose scope dimension ≠ the key's `scoped_by` is rejected at `Add` time**, so
bad content fails at load rather than at the first fight.

**Plus one visibility guard:** the Hit Log and Yield Log render each contribution *with its
scope*, so a context bug that somehow survives all three still shows up in the trace as a line
that should have been there and isn't.

```
  Interval      120 × 0.87 [Tidecaller Rod · profession:fishing]
                    × 0.90 [Mastery 41 · profession:fishing]
                → 94 ticks
```

**Test:** resolve every registered key against every context shape and assert that no
`scoped_by` key ever silently drops to its baseline.

## 4.3 Stacking rules **[DECIDED — D-13]**

| Mode | Formula | Use for |
|---|---|---|
| `additive` | `base + Σx` | attributes, flat damage, resistance, crit chance |
| `multiplicative` | `base × Πx` | intervals, damage multipliers, costs |
| `highest_only` | `max(x)` | Barrier from multiple sources of the same id; aura-shaped effects |
| `diminishing` | `1 − Π(1 − x)` | **avoidance, preservation, doubling** — never reaches 1.0 |
| `flag` | any nonzero | rule switches |
| `unique` | first only, later ones rejected at *equip* time | Exotic/Signature/Anomalous affixes |

**`diminishing` is the load-bearing addition.** Three sources of 10% avoidance give 27.1%, not
30%, and forty sources give 98.5%, not 400%. It makes stacking *feel* additive at low values
while being mathematically incapable of reaching certainty. This is the standard solution
(Dota, WoW dodge/parry pre-DR, LE) and it is better than a hard cap because the hard cap creates
a cliff where the next affix is worth exactly zero.

**Both** apply: `diminishing` for the curve, `max` for the ceiling. Neither substitutes for the
other, and the arithmetic shows why:

| Sources of 10% | Additive | Diminishing |
|---|---|---|
| 3 | 30% | 27.1% |
| 10 | 100% | 65.1% |
| 20 | 200% | 87.8% |
| 40 | 400% | **98.5%** |

The asymptote bounds the *limit*, not the *reachable value* — 98.5% is indistinguishable from
immunity, and nothing stops one Signature affix rolling +40% on its own where the curve has barely
bent. So the cap stays. Equally, a cap alone makes the affix past the cap worth **exactly zero**,
which is how a player quietly stops caring about an entire family without the tooltip ever saying
so.

### 4.3.1 The `danger` flag

> **A key marked `danger: true` fails content validation if it has no `max`.**

This is the cheapest guard in the package: one boolean, one validator rule, and "we forgot to cap
that" becomes impossible rather than unlikely. The families that carry it are listed in §4.4 —
avoidance, maximum resistance, action interval, cooldown, resource cost, damage taken, leech and
on-hit healing, control duration taken, and defensive window sizes.

## 4.4 The dangerous families and their caps **[PROPOSED — all tunable]**

The brief is right that players will find the degenerate combinations. Every one of these is a
`danger: true` key that fails content validation without a cap.

| Family | Mode | Cap | Rationale |
|---|---|---|---|
| Hit avoidance, per lane | diminishing | 0.25 | Avoidance is strictly better than mitigation; must never approach certainty |
| Hit avoidance, global | diminishing | 0.15 | |
| Resistance, per lane | additive | 0.75 → 0.90 with max-res affixes | PoE's proven pair |
| Maximum resistance | additive | +0.15 total | hard ceiling 0.90 |
| Action interval | multiplicative | **floor 0.55** | 45% faster is the limit (D-20). Existing key floor is 0.25 — a 4× speed build fits four on-hit procs inside one enemy telegraph, which the D-14 ICDs were never designed to carry. Block/dodge windows are *fixed* tick durations, so interval reduction is the strongest stat in a tick game by a wide margin |
| Cooldown | multiplicative | floor 0.50 | |
| Resource cost | multiplicative | floor 0.40 | prevents free casting |
| Damage taken | multiplicative | floor 0.50 | prevents mitigation multiplication |
| Critical chance | additive | 0.75 | |
| Leech / on-hit healing | additive | flat per-hit cap **and** per-second cap | GDD forbids trivialising attrition |
| Block/parry window | additive | +50% of base | keeps the skill test a skill test |
| Preservation | diminishing | 0.80 (exists) | |
| Double output | diminishing | 1.00 (exists) | |
| Thorns reflected fraction | additive | 0.60 of mitigated | |
| Control buildup | additive | — | **no cap needed — Resolve handles it** (`statuses.md` §4) |
| Control duration taken | multiplicative | floor 0.35 | the defensive half; Resolve is the other |

**Recovery deserves a specific rule.** GDD §13 makes Health attrition load-bearing and §23 of
the brief says so explicitly. **[DECIDED — D-15]** *Affixes never grant passive Health regeneration.*
The recovery family grants **Barrier** (a decaying temporary pool that never touches Health) or
**conditional, capped, on-event healing**. See `damage-and-defense.md` §5.7.

---

# 5. Tags — the smallest useful closed vocabulary **[DECIDED — D-16]**

Tags are the interoperability layer. They must be *closed and validated* or they become a typo
surface. **Seven namespaces, 71 tags**, `family:value`, reusing the existing `TagFamilies`
machinery (which already parses `family:value` and enforces cardinality).

**`form:` stays one namespace with exactly-one cardinality**, covering weapons, armour *and*
tools. Splitting into `weapon:`/`armour:`/`tool:` was rejected: the values are already
unambiguous, and one namespace means *"what form is this item?"* is a single lookup and affix
eligibility reads `forms_any: ["rod"]` uniformly whether the target is a weapon or a pickaxe.
`hammer` (weapon) and `hammer_tool` (smithing) remain **separate values** precisely because they
share no affix pool and no stat map.

⚠ **These are `action:`/`lane:`/`form:` tags on moves, hits and items — a different namespace
from the *material* tag families** (`origin:`/`comp:`/`state:`/`rarity:`/`class:`/`part:`).
Both live in `TagFamilies`; validation keeps them apart by which entity carries them.

| Namespace | Cardinality | Values | Count |
|---|---|---|---|
| **`action:`** | 1+ | `attack` `spell` `defensive` `utility` `movement` `channel` `reaction` `summon` `profession` | 9 |
| **`delivery:`** | 1+ | `melee` `ranged` `projectile` `area` `direct` `dot` | 6 |
| **`form:`** | exactly 1 (on equipment) | `sword` `axe` `hammer` `dagger` `spear` `bow` `staff` `shield` `focus` `light_armour` `heavy_armour` `robe` `rod` `pick` `hammer_tool` `apparatus` `blade_tool` | 17 |
| **`lane:`** | exactly 1 (on a packet) | `physical` `magic` `heat` `cold` `charge` `toxin` `corrosion` `decay` | 8 |
| **`essence:`** | many (usually 0) | `fire` `frost` `storm` `nature` `necrotic` `radiant` `abyssal` | 7 |
| **`tier:`** | 1 authored + derived | `trash` `normal` `elite` `boss` · **derived:** `notable` | 5 |
| **`mech:`** | many | `critical` `block` `perfect_block` `parry` `dodge` `evade` `negate` `retaliation` `thorns` `healing` `barrier` `resource` `control` `ailment` `impairment` `stagger` `chain` `trigger` `overreach` | 19 |

**71 tags total.** That is the whole vocabulary. Deliberate omissions and why:

- **Derived tags are how content expresses OR (D-11).** `when[]` is pure AND and always will be —
  no `anyOf`, no boolean tree. When an OR turns out to be a *category*, the answer is a tag; when
  it genuinely isn't, the answer is two rules.

  **`tier:notable` is the worked case.** An actor authors exactly one of
  `tier:trash|normal|elite|boss`; `tier:notable` is **added at load** to anything elite or boss.
  So *"chance to negate hits from Elite or Boss enemies"* is
  `when: [ targetHasTag tier:notable ]` — one flat condition, no operator, no drift, and hand-
  authoring `notable` on every elite is impossible to forget because nobody does it.

  This reuses exactly the pattern `TagDeriver` already applies to materials (state thresholds →
  tags, `emergent-item-system.md` §4.2). **Rule going forward:** a new derived tag is the correct
  response to a real OR case; a new condition kind is not.

- **`essence:` is metadata, never mitigation (D-04).** It may ride on packets, moves and effects
  and be tested by any condition, but no resistance, avoidance or damage-taken contribution may
  be scoped to it. Damage always resolves through the packet's `lane:`. See
  `damage-and-defense.md` §2.4 — the existing `hasTag` condition covers every essence case, so
  this costs one namespace and no new condition kinds.

- **No `profession:` namespace.** Professions already have ids (`profession.fishing`); a scope
  dimension (`profession:fishing`) reuses them. Duplicating them as tags creates two sources of
  truth.
- **No `element:` namespace.** That is `lane:`.
- **No `Physical` action tag.** Damage type is on the packet, not the move; a move can produce
  packets in several lanes.
- **`weapon` is not a tag** — it is `action:attack` + a `form:` that is a weapon form. One less
  thing to keep consistent.
- **`hammer` (weapon) vs `hammer_tool` (smithing)** are separate because their affix pools and
  stat maps share nothing.

**Damage type vs lane.** `DamageType` (Slashing/Crushing/Piercing/Magic) stays a **code enum**
per D16 — it is a closed vocabulary that decides dispatch. `lane:` is the **defensive** axis and
is data. A packet has both. `damage-and-defense.md` §2 explains why they are not the same thing.

---

# 6. Proc and recursion safety

The brief's fusion example is the right thing to fear. Five mechanisms, layered.

## 6.1 `EffectContext` — the chain identity **[DECIDED — D-14]**

Every effect invocation carries:

```csharp
sealed record EffectContext(
    string ChainId,          // unique per originating player/enemy action
    string OriginSource,     // who started the chain
    string ImmediateSource,  // who fired this specific effect
    int    Depth,            // 0 = a real action, 1 = a proc, 2 = a proc's proc
    IReadOnlySet<string> OriginTags);   // e.g. { "thorns", "retaliation" }
```

Every `GameEvent` raised *by* an effect inherits the context with `Depth + 1`.

## 6.2 The five rules **[DECIDED — D-14]**

**The depth model, stated precisely:** a real action's effects run at depth 0 and raise depth-0
events. A rule matching a depth-*d* event fires its effects at depth *d+1*. **A rule may only fire
if the triggering event's depth is below `MAX_PROC_DEPTH`.**

**The three companion rules do the real work; the depth cap is the backstop.** Rules 2, 3 and 4
below are what actually break the fusion loop — a chain that ping-pongs is stopped by
once-per-chain long before it reaches the depth ceiling. Depth exists to bound the chains that
*don't* repeat a rule.


1. **`MAX_PROC_DEPTH = 2`.** Events at depth ≥ 2 are published with `can_trigger = false` and
   no `RuleGrant` matches them. The fusion chain terminates: hit(0) → thorns(1) → shock(2) →
   *stop*.
2. **Once-per-chain per rule.** A rule id fires at most once per `ChainId`. Kills A→B→A
   ping-pong even inside the depth budget. Default `true` for any rule whose effects can raise
   the event it listens for; the **validator computes this statically** (§9.2).
3. **Internal cooldowns.** `cooldown_ticks` (exists) plus optional per-target `icd_ticks`.
   **Chosen deliberately over PoE's proc coefficients** — ICDs are readable in a tooltip
   ("once every 2s"), proc coefficients are not, and the tick engine makes rate abuse visible
   in the Hit Log anyway.
4. **Retaliation damage does not proc by default.** Thorns, reflect and stored-retaliation
   packets carry `mech:retaliation` and `can_trigger = false`. Overriding that is Anomalous-tier
   only (§6.3).
5. **The fuse.** `MAX_EFFECTS_PER_CHAIN = 64`. Exceeding it aborts the chain, logs an error to
   the Hit Log, and **fails a test** — shipped content must never trip it.

## 6.2.1 What depth 2 buys, and what it forbids

| Allowed | Forbidden |
|---|---|
| hit → thorns → Shock applied → Stamina gained | thorns damage triggering on-hit effects |
| hit → on-crit rule → status → resource | any 4th-generation effect |
| **statuses applied by an affix can trigger effects** | a rule firing twice in one chain |

**Why not depth 1.** It is absolutely safe and it breaks a large slice of the catalog: every
status applied by an affix trigger becomes unable to trigger anything, which kills
*"on applying a status, gain…"* (#135, #159) and every two-step combination the brief asked for.

**Why not depth 3.** Each extra generation multiplies the combination surface balance has to
survive. Across ~250 affixes the number of reachable 4-step chains is large enough that nobody
will have modelled them — and Anomalous at depth 4 is genuinely hard to reason about when
reviewing a single Overreach outcome.

## 6.3 Where the rules may be broken **[DECIDED — D-14]**

**Anomalous affixes — obtainable only from Overreach — may raise `max_depth` by exactly 1 for
their own chain.** Never remove it, never raise it by more. This is deliberate design, not a
loophole: the top-end reward of the crafting casino is *permission to recurse one level further*,
which is exactly the "wait, my item can do WHAT?" moment the brief asks for at the top of the
power curve, and it is bounded by construction.

## 6.4 What is already safe **[EXISTING/PRESERVE]**

`GameEventBus.Publish` queues handler-raised events and drains them after the current event
finishes, so a handler can never re-enter mid-flight. That was designed for determinism and it
doubles as re-entrancy protection. Keep it exactly as it is.

---

# 7. What this package does *not* change

Explicitly, so the boundary is legible:

- **The reaction algebra.** `ReactionEngine`, convergence, opposition, potency, integrity,
  signatures, naming, the Reaction Log — untouched. Affixes read the *output* of fabrication;
  they never reach back into transmutation.
- **The class combinator.** Bases/Prefixes/Suffixes, the growth budget, "a Prefix may never name
  a Base", one expression per channel, formatting-never-touches-mechanics. This package makes
  that content *work*; it changes none of its rules.
- **Tick determinism.** One `TickEngine`, one seeded `IRandomSource`, synchronous ordered bus.
- **Domain purity.** Everything proposed is Core; nothing needs Godot.
- **`ItemInstance` is equipment-only** (D20). Affixes live on instances. Materials still stack.
- **Extraction risk, gear-safe-on-death, the starter loadout.**

---

# 8. Testing and validation strategy

## 8.1 Load-time validation — extend `ContentValidator` **[PROPOSED]**

The project's established pattern is fail-loudly-at-load with a failing test per rule. New rules:

| Rule | Catches |
|---|---|
| Every `effects[].kind` ∈ the 24-effect vocabulary | typo'd effect |
| Every `event` ∈ the 38-event vocabulary | typo'd trigger |
| Every `when[].kind` ∈ the condition vocabulary | typo'd condition |
| Every tag ∈ the 59-tag closed vocabulary, correct namespace for the carrier | `mech:sword` |
| Every `applyStatus`/`removeStatus` Text resolves to a `StatusDefinition` or category | **the 14 currently-dangling status ids** |
| Every `lane`/`aspect` resolves to a registered lane | `lane:lightning` |
| Every `grantModifier` Text resolves to a modifier key **and its scope dimension matches `scoped_by`** | scope mismatch |
| `danger: true` keys have a `max` | uncapped avoidance |
| Affix tier ranges are monotonic and non-overlapping | malformed tiers |
| Every affix is eligible for ≥1 form **and** ≥1 reachable genome | dead affix |
| Status `requires_status` targets exist (Freeze→Chill) | impossible status |
| Conversion graph is acyclic | heat→cold→heat |
| **Static proc-cycle detection** (§9.2) | the fusion chain, at author time |
| Every `MoveModifier.match` matches ≥1 move | dead move modifier |
| Stack mode is compatible with kind (`diminishing` requires values in [0,1]) | conflicting stack rules |

## 8.2 Static proc-cycle detection **[PROPOSED — worth calling out]**

Build a directed graph: *rule → events its effects can raise → rules matching those events*.
Report every cycle whose total `max_depth` budget allows a traversal. This catches the fusion
chain **at content-load time**, before anyone plays it, and it is cheap — the rule set is a few
hundred nodes. I have not seen an ARPG do this and it is a genuine advantage of having the
whole effect vocabulary as data.

## 8.3 Runtime test families

| Family | Shape |
|---|---|
| **Golden traces** | Fixed seed + fixed inputs → assert the **entire ordered Hit Log**, not just final damage. This is what stops pipeline order changing silently. One per worked example in `worked-examples.md`. |
| **Ordering** | Resistance: reduction before cap, inversion after cap, penetration after inversion. Asserted independently of damage numbers. |
| **Invariants (fuzz, 10k seeded iterations)** | final damage ≥ 0 · effective resistance ∈ [−1.0, 0.90] · no chain exceeds `MAX_PROC_DEPTH` · no resolved avoidance exceeds its cap · every `diminishing` key < 1.0 |
| **Stacking** | Table-driven per mode; `diminishing` of forty 10% sources < 1.0 |
| **Status** | Stack policies, refresh, expiry, tick timing, Resolve gating, control immunity, escalation, Chill→Freeze gate |
| **Distribution (seeded, N ≥ 100k)** | Affix weight distribution within tolerance of declared weights · tier ceilings never exceeded by genome · profession doubling/preservation rates match resolved modifiers |
| **Move modification** | Resolved move = base + mods, asserted field by field; idempotence when applied twice |
| **Genetics** | Same genome → same eligible pool (pure function); every declared affix reachable from some real material combination |

**Every test uses `SeededRandom`.** No test may depend on wall-clock or unseeded RNG — the
existing rule.

---

# 9. Developer labs **[PROPOSED]**

The GDD already commits to labs as real deliverables (§16.2). Three new ones, each built *with*
its phase, not up front.

**Combat / Effect Lab** — spawn attacker + defender; override attributes, resistances, armour,
Resolve; apply statuses; attach arbitrary `Grant`s; fire a
chosen Move; **step through the Hit
Log stage by stage** with the value before and after each stage and the provenance of every
contribution. This is the Character Lab's diff panel applied to a hit.

**Item Lab** — pick a Form; slot materials (or override raw properties); inspect the derived
**Genome**; list every eligible affix with its computed weight and tier ceiling *and the genetic
term that produced each*; force specific affixes; roll N times and show the distribution; run
crafting operations; force each Overreach outcome.

**Profession Tool Lab** — pick profession + action + tool; override modifiers; run 10k seeded
simulations; compare interval / yield / preservation / doubling / quality distributions between
two tools side by side.

---

# 10. Recommended implementation order

Small slices, each leaving `dotnet build` + `dotnet test` green, each independently valuable.

| # | Slice | Delivers | Depends on | Risk |
|---|---|---|---|---|
| **E0** | ✅ **DONE — combat raises events** | `CombatEncounter` takes an `IGameEventBus` and publishes **14 event kinds that already existed** (`EncounterStarted/Ended`, `ActionQueued/Telegraphed/Resolved`, `MoveExecuted`, `DamageDealt/Taken`, `Blocked`, `Dodged`, `Killed`, `Defeated`, `ResourceSpent`, `Healed`) — **zero new vocabulary**; the §3.1 additions arrive with the pipeline in E1. `GameRoot` gained the missing half: a `GameEventBus` + `TriggerRuleEngine`, with the resolved build's hooks attached and re-attached on every rebuild. 12 new tests. | — | very low |
| **E1** | **The Hit pipeline** | `Hit`/`Packet`/`Lane`, the ordered stage list, `HitLog`, resistance/armour/avoidance/penetration. Replaces `CombatCalculator.Resolve`. Aspects and lanes exist; content still only uses `physical`. | E0 | medium |
| **E2** | **Statuses** | `StatusDefinition` + controller + the **14 core statuses + 13 authored prefix/suffix ids** (~27 definitions) + **Resolve / control buildup** + the Resolve bar. Takes 13 of the 14 dangling ids live; `status.recalled_move` waits for E4. | E1 | medium |
| **E3** | **Effect vocabulary upgrade** | `effects[]`, target selectors, `EffectContext` + proc safety, scoped modifier contributions, `diminishing`/`highest_only`, the 12 new effect handlers, new conditions. | E1, E2 | medium |
| **E4** | **Moves** | `MoveDefinition`, moveset composition with provenance, `MoveModifier`, **`AttackProfile`/`AbilityDefinition` convergence**, Combat Lab. Closes the GDD's largest gap. | E3 | **high** |
| **C1** | *(crafting)* **P2 traits + P3 essence** | Prerequisite genetics for affixes. Existing spec, unchanged. | — | medium |
| **C2** | *(crafting)* **P5a fabrication + scale reconciliation** | Materials → equipment. ⚠ **This is the combat rebalance** (0–100 vs 0–5). Budget it as its own piece of work. | C1, E1 | **highest** |
| **E5** | **Item affixes** | `AffixDefinition`, Genome derivation, eligibility/weight/tier, rolling, Genome Readout, Item Lab. | C2, E3 | high |
| **E6** | **Profession pipeline + tools** | Outcome pipeline, Yield Log, `Tool` slots, tool forms, tool affix pools, Profession Tool Lab. Mostly free once E3's scoped modifiers exist. | E3, C2 | low |
| **E7** | **Crafting operations + Overreach** | Anneal/Etch/Scour/Reforge/Bind/Temper/Fracture + Overreach outcomes and Anomalous affixes. | E5 | medium |

## Why this order

**E0 first and standalone. ✅ Shipped.** It makes the class combinator observable in a real fight
before anything else is built, and de-risks every later slice by proving the event shapes against
real combat.

> **What E0 actually found, corrected against the estimate.** The plan said "~20 lines: wire
> `CombatEncounter` to the bus". The audit had missed that **`GameEventBus` and
> `TriggerRuleEngine` were constructed nowhere in the running game** — `BuildResolver` produced
> `AttachedRule`s and nothing consumed them. So E0 was two halves, not one: combat publishing,
> *and* `GameRoot` growing the bus + engine + attach-on-rebuild. Still small (~140 lines
> including comments), still low risk, but "wire combat to the bus" understated it.
>
> **Scope was tightened, not widened.** Checking which events shipped content actually listens
> for showed the top three are `MoveExecuted` (18 hooks), `DamageDealt` (13) and `ResourceSpent`
> (11) — all **existing** vocabulary. So E0 added **no new event kinds at all**, which is a
> cleaner boundary than the plan drew: E1 introduces `HitLanded`/`HitAvoided` *together with the
> packet semantics that make them distinct from `DamageDealt`/`Dodged`*.

**Statuses before moves (E2 before E4).** Reverses the M5/M6 ordering question in HANDOFF. The
reason: Shield Bash needs Stun, Fireball needs Burn, and 14 status ids are already authored and
inert. Building moves first means authoring move content that references statuses that don't
exist — the same mistake, twice.

**The crafting phases (C1/C2) interleave, they do not block E0–E4.** E0–E4 improve the *existing*
game with no fabrication. That matters: it means the effect foundation pays off before the
riskiest work (the scale reconciliation) begins.

**E5 cannot start before C2.** There is no path from a material to a piece of equipment today.
Affixes without fabrication would be affixes on four hand-authored items — which is a normal
ARPG, not this game.

**[DECIDED — D-19] Effects first: E0–E4 → C1/C2 → E5–E7.**

The decisive argument: **an affix pool built before the effect vocabulary can only express
numbers.** Every catalog entry marked Trigger, Exotic, Signature or Anomalous needs statuses, the
packet pipeline, scoped modifiers or the trigger upgrade to exist first — so E5 arriving early
would mean authoring `+5 damage` and `+12 Armour` and nothing else.

Two supporting reasons:
- **E0–E4 makes already-written content live.** 15 Bases, 25 Prefixes, 50 Suffixes and 13 status
  ids are authored and inert today. Highest value-per-unit-of-work in the project, and it needs no
  new content.
- **C2 carries the riskiest work** (fabrication + the 0–100 vs 0–5 reconciliation). Running it
  after E1 means calibrating against the *final* damage pipeline rather than a placeholder that is
  about to be replaced.

**Accepted cost, stated plainly:** the crafting system — the signature mechanic, and the thing
that makes this game unlike anything else — sits at P1 for the whole E0–E4 stretch, and the player
still cannot fabricate a weapon during it.

---

# 11. Where I disagree with the current GDD

Stated plainly, as requested. Each of these is a **[PROPOSED]** change that contradicts a
written design, and each is listed in §12 for approval.

**1. Three physical resistances are a mistake.** GDD §5.5 and the modifier registry have
`combat.resist.slashing/crushing/piercing`. Grim Dawn shipped nine resistances and it is widely
considered its worst readability decision. Three physical resistances triple the defensive affix
pool for one axis of decision. **Collapse to one `physical` lane; express per-type weakness as
enemy vulnerability multipliers.** The Fighter's "swap to the weapon that counters it" identity
survives — it now lives on the *enemy*, where it is discoverable and where Realm Knowledge can
reveal it. `damage-and-defense.md` §4.

**2. Passive dodge chance undermines the best thing in your combat.** `combat.dodge.chance`
exists as a modifier key. The GDD says timed block/dodge "is the core skill test and it works
today". A passive dodge roll makes the skill test optional. **Gear should improve the dodge
*window* and *cost*, not add a passive roll.** Keep a small, capped passive `evade` for
*untelegraphed* hits only. `damage-and-defense.md` §5.1.

**3. "Ignite" and "Burn" should not both exist.** GDD §5.9 lists Burn; the brief asks about
Ignite. Having both is pure confusion (in PoE, "ignite" is the application and "burning" the
damage, and it confuses players constantly). **One status: Burn.** `statuses.md` §3.

**4. "Slow" and "Chill" should not both exist.** **Chill is the cold-tagged Slow** and the only
thing that builds toward Freeze. `statuses.md` §3.

**5. Physical Damage Reduction is not a separate layer.** It is the `physical` resistance lane.
Adding both is the classic ARPG mistake of shipping two stats that do the same arithmetic.

**6. Flat armour subtraction is the wrong shape.** Current: `damage − armour`, floored at 1.
That makes armour excellent against chip damage and worthless against the Goblin Brute's
Overhead Smash — precisely backwards for a defensive investment in a telegraph-driven game.
**Adopt PoE's diminishing armour formula**, which gives armour a clear job ("attrition")
distinct from resistance ("spikes"). `damage-and-defense.md` §5.4.

**7. Healing affixes conflict with the attrition pillar.** GDD §13 and §5.4 make
non-regenerating Health load-bearing. An affix pool with `Health on Hit` quietly deletes that.
**Barrier replaces healing as the recovery family.** `damage-and-defense.md` §5.7.

**8. Crowd control needs one mechanism, not several.** The brief proposes boss Resolve *and* CC
resistance *and* diminishing returns *and* temporary immunity *and* buildup thresholds. That is
five systems for one problem. **Resolve alone does all five jobs** — and it is the only one of the
candidates that produces an *in-fight arc*, where the same build feels different at t=60 and t=600
of one fight without any authored phase change. **Adopted as D-08.** `statuses.md` §4.

---

# 12. Decisions I need from you before coding

Ordered by how much they change downstream work. My recommendation is stated on each.

| # | Decision | Status |
|---|---|---|
| **D-01** | Damage model: types × aspects with **one mitigation lane per packet** (lane = aspect ?? type), armour applying to physical *types* regardless of aspect. | ✅ **DECIDED — adopted.** Plus: a hit **splits into as many packets as it has lanes** and is never forced into one hybrid packet; flat-added aspect damage *increases* the total rather than relabelling the strike. `addAspect` cut from the move-op vocabulary — lane movement is always `convert` with an explicit fraction. `damage-and-defense.md` §1.1, §2 |
| **D-02** | Collapse three physical resistances into one `physical` lane; per-type weakness becomes an enemy vulnerability multiplier. | ✅ **DECIDED — adopted.** The multiplier is **two-way**, clamped [0.50, 1.50], so an enemy can be tough against one physical type and soft against another. Amends GDD §5.5. Migration: 3 modifier keys → 1, one line in `equip.iron_armor`, `ActorDefinition.vulnerable` added. `damage-and-defense.md` §4.1a |
| **D-03** | Resistance lanes: `physical magic heat cold charge toxin corrosion decay` (8), **`arcane` aspect has no lane and is unresistable**. | ✅ **DECIDED — adopted.** **(a)** Arcane is unresistable, and the "best-in-slot" risk is closed **structurally**: arcane accepts flat adds and *global* increases only — never a lane-specific `increased`, never a `more`/`less` multiplier, never conditional damage. It scales linearly while every other lane goes multiplicative, so it is strongest at low investment and weakest at high. Validator-enforced. **(b)** `decay` lane is **reserved in the vocabulary at E1, content authored later**. `damage-and-defense.md` §2.5.1, §4.1 |
| **D-04** | Essences never become damage lanes. | ✅ **DECIDED — adopted, widened.** Essence does four jobs: empowers its anchor aspect · gates Exotic/Signature affixes · modifies ailments · **tags packets/moves/effects as metadata any condition may read**. The invariant: *essence is identity and metadata, never an additional mitigation calculation.* No `combat.resist.*` / `combat.avoid.*` / `combat.damage_taken.*` may be essence-scoped; triggers are unrestricted. `essence:` added to the tag vocabulary (59 → 66 tags); the existing `hasTag` condition covers every case. `damage-and-defense.md` §2.4 |
| **D-05** | Resistance order: sum → exposure/reduction → cap(75/90) → **inversion** → penetration → floor(−100%). | ✅ **DECIDED — adopted.** **(a)** Overcapping matters: exposure applies **before** the cap, penetration **after**, so overcap absorbs debuffs but never penetration. **Consequence accepted: two-number display (`capped / raw`) is required scope** on the character sheet, in the Hit Log, and on the realm-preparation screen (the GDD §11.7 payoff). **(b)** Inversion sits after the cap and before penetration, fires **only on positive** resistance, is floored at **−50%**, never helps the enemy, and cannot break immunity. `damage-and-defense.md` §4.2, §4.2.1, §4.4 |
| **D-06** | `Hit` vs `Damage` vs `Avoided` semantics: block is *mitigation* (raises `HitLanded`); **Perfect Block** is *avoidance*. | ✅ **DECIDED — adopted, with a correction.** `Blocked` fires on **both** block outcomes, and on-block affixes hook `Blocked` rather than `HitLanded`. The original spec would have given a *perfect* block **no** retaliation — punishing the better play. Now a Bastion who perfect-blocks refunds Guard *and* retaliates, while generic *retaliate when hit* correctly gets nothing from a hit that never landed. `damage-and-defense.md` §6.1–6.3, Example B3 |
| **D-07** | Demote passive dodge chance; gear buys window/cost. Small capped `evade` for untelegraphed hits only. | ✅ **DECIDED — adopted.** `combat.dodge.chance` retired as an affix target → `combat.evade.chance` (diminishing, max 0.15, untelegraphed only); new keys for dodge/block/perfect-block window and dodge cost. **Auto-combat resolved:** it uses the same stances, disadvantaged by **reaction latency** (`AI.reaction_ticks`) not a damage penalty — so it blocks reliably, dodges often, and almost never lands a Perfect Block or Parry. Window affixes are therefore worth more to an automated build. `damage-and-defense.md` §5.1, §5.1.1 |
| **D-08** | How crowd control is gated. | ✅ **DECIDED — Resolve.** One pool per combatant. Controls apply **buildup**; crossing Resolve lands the control, consumes the buildup, opens a **Control Immunity** window blocking *all* controls, and raises Resolve **+25% for the rest of the encounter**. Resolve alone does all five jobs the brief listed (boss resolve · CC resistance · diminishing returns · temporary immunity · buildup thresholds). **Stagger folds in** as buildup toward Stun, so a build cannot Stun-lock *and* Freeze-lock. Player-facing text still reads "12% chance to Freeze"; **the Resolve bar is required scope** so the pool is never hidden. `statuses.md` §4 |
| **D-09** | Status roster v1. | ✅ **DECIDED — adopted.** **(a)** 14 core: Bleed Poison Burn Chill Shock Corroded Stun Freeze Fear Silence Weaken Vulnerable Guarded Barrier. **Ignite cut** (→ Burn) and **Slow cut** (→ Chill), both contradicting GDD §5.9. Root, Wither, Brittle deferred. **(b)** The 14 authored prefix/suffix ids are **authored in E2 alongside the roster** → ~27 definitions total. 13 of 14 are authorable; **`status.recalled_move` needs the Move system, so Mnemonic stays inert until E4** — a dated dependency, not a mystery. `status.toxin` is **aliased** to `status.poison`, not duplicated. `statuses.md` §3, §3.5 |
| **D-10** | Hybrid application: ailments/impairments = chance; controls = buildup vs Resolve. | ✅ **DECIDED — adopted.** Ailments and impairments roll a direct chance with magnitude from the hit; controls apply buildup against Resolve, never directly. Player-facing wording stays "12% chance to Freeze" — the roll gates the *buildup*. `statuses.md` §5 |
| **D-11** | How content expresses OR. | ✅ **DECIDED — no `anyOf`, ever.** `when[]` stays pure AND. OR is expressed by a **derived tag** when it is really a category, and by **two rules** when it isn't. Adds a `tier:` family (`trash\|normal\|elite\|boss` authored, **`notable` derived at load** for elite/boss) — reusing the `TagDeriver` pattern from `emergent-item-system.md` §4.2. Tags 66 → 71. **Standing rule: a new derived tag is the correct response to a real OR case; a new condition kind is not.** `effect-foundation.md` §3.2, §5 |
| **D-12** | Scoped modifier contributions (`ModifierScope`) as the local/global + per-profession + per-move-tag mechanism. | ✅ **DECIDED — adopted.** Eight closed dimensions: `lane aspect essence profession move_tag form item status`. Registry stays ~55 keys instead of ~330. **The wrong-context failure mode is closed structurally, not by discipline:** a key declares `scoped_by`; resolving it with a context lacking that dimension **throws** rather than returning a wrong number; a mismatched scope is rejected at `Add` time; and both traces render contributions with their scope. `effect-foundation.md` §4.2–4.2.2 |
| **D-13** | Add `diminishing` and `highest_only` stacking modes; mark dangerous keys `danger: true` with mandatory caps. | ✅ **DECIDED — adopted.** Five modes: `additive multiplicative flag diminishing highest_only`. **Diminishing for the curve, `max` for the ceiling — neither substitutes for the other** (40 stacked 10% sources reach 98.5% diminishing, and one Signature affix can roll 40% where the curve has barely bent). `danger: true` keys **fail content validation without a `max`**. `effect-foundation.md` §4.3–4.3.1, §4.4 |
| **D-14** | `MAX_PROC_DEPTH = 2`; retaliation cannot proc by default; Anomalous affixes may raise depth by exactly 1. | ✅ **DECIDED — adopted.** Depth 2 keeps *hit → thorns → Shock → Stamina* working while forbidding any 4th generation. Depth 1 was rejected as breaking every two-step combination in the catalog; depth 3 as multiplying an unmodellable surface. **The three companion rules do the real work** — retaliation `can_trigger=false`, ailment ticks never raise `HitLanded`, once-per-chain per rule — with depth as the backstop and `MAX_EFFECTS_PER_CHAIN = 64` as the fuse. Anomalous (Overreach-only) may reach 3. `effect-foundation.md` §6.2–6.3 |
| **D-15** | Barrier replaces healing as the recovery affix family; no passive Health regen from affixes ever. | ✅ **DECIDED — adopted.** Recovery grants Barrier (on kill/block/crit/status, capacity, effectiveness, decay). Healing affixes stay rare, conditional, and capped **per hit *and* per second** — never unconditional, never passive. **Barrier renders as an overlay on the Health bar, not a third meter**, so it does not spend the two-meter readability budget GDD §3.6 sets. `damage-and-defense.md` §5.7–5.7.1 |
| **D-16** | The closed tag vocabulary. | ✅ **DECIDED — adopted at 71 tags / 7 namespaces** (grown from 59 by D-04's `essence:` and D-11's `tier:`). **`form:` stays unified** with exactly-one cardinality across weapons, armour and tools — splitting into `weapon:`/`armour:`/`tool:` was rejected because one namespace keeps "what form is this?" a single lookup and affix eligibility uniform. `hammer` vs `hammer_tool` stay distinct values. `effect-foundation.md` §5 |
| **D-17** | Keeping item affixes distinct from Character Prefix/Suffix. | ✅ **DECIDED — namespace + qualification.** `Dungeons.Affixes.AffixDefinition` / `AffixSlot.Prefix` vs `Dungeons.Characters.Composition.PrefixDefinition`; ids are `affix.*` and the validator **rejects** an affix id starting `prefix.`/`suffix.`. Player-facing text calls item affixes **"modifiers"** and never "prefix", so the bare word means only the character layer. Zero migration. Renaming the *character* layer (the non-standard usage) was the tidier option and lost on cost — **recorded as the fix if the confusion ever bites**. `affixes.md` §1 |
| **D-18** | When `AttackProfile` + `AbilityDefinition` converge into `MoveDefinition`. | ✅ **DECIDED — E4, with a single-packet bridge from E1.** `ToPackets(AttackProfile)` wraps the old shapes for E1–E3; both coexist for three slices; the bridge and its tests are deleted in E4. Converging in E1 was rejected as putting two high-risk rewrites in one slice with no green checkpoint between them. Keeping both permanently was rejected as the option that looks cheapest and isn't. **Amends DECISIONS D8** — intent preserved, combat reads neutral *Moves* instead of neutral *profiles*. `moves.md` §2.3 |
| **D-19** | Build sequencing. | ✅ **DECIDED — effects first: E0–E4 → C1/C2 → E5–E7.** Decisive reason: **an affix pool built before the effect vocabulary can only express numbers.** Supporting: E0–E4 makes 15 Bases / 25 Prefixes / 50 Suffixes / 13 status ids live with no new content; and C2's scale reconciliation calibrates against the *final* pipeline rather than a placeholder. **Accepted cost:** crafting sits at P1 for the whole stretch and the player cannot fabricate a weapon during it. `effect-foundation.md` §10 |
| **D-20** | The `combat.interval.mult` floor. | ✅ **DECIDED — 0.55** (max 45% faster), tightened from the registry's current 0.25. Interval reduction is the strongest stat in a tick game because block/dodge windows and enemy telegraphs are *fixed* durations — a 4× build would fit four on-hit procs inside one telegraph, a load the D-14 ICDs were not designed to carry. Matches Melvor's ~50–60% ceiling. `effect-foundation.md` §4.4 |
| **D-21** | Item affix structure. | ✅ **DECIDED — adopted.** 1–3 **innates** computed from the genome at fabrication (never rolled, **never rerollable**) + ≤3 affix-prefixes + ≤3 affix-suffixes rolled from weighted, tiered pools + Exotic/Signature/Anomalous above. The innate layer is the part that is ours rather than PoE's: **material invention guarantees a result instead of only shifting a distribution**, and a well-engineered item is never a total loss. Slot counts stay **uniform 3+3** across forms (U-8). `affixes.md` §3 |
| **D-22** | Overreach. | ✅ **DECIDED — adopted.** **(a)** The outcome pool draws **only from the item's own genetic families** — a poison dagger can never Overreach into a lightning effect, at any odds. Weights are a pure function of the genome, so the Item Lab shows exact odds before committing. **Anomalous affixes exist only here**, keeping "proc-rule breaking is earned by risking the item" intact. **(b)** **Repeatable, with escalating Ruin/Brick odds** (Brick still ends it permanently) — completing GDD §13.2's deliberate risk rhyme with a fourth verse: *push once more, or stop?* Flat odds were rejected as making the ceiling a function of farming rather than nerve. `affixes.md` §6 |
| **D-23** | Profession tools as equipment. | ✅ **DECIDED — adopted, two worn slots.** `Tool.Gathering` (pick/axe/rod/sickle) and `Tool.Crafting` (hammer/apparatus/mortar/needle), separate from Weapon/Armor. **Worn, not auto-selected** — being worn is what makes a tool a *preparation decision*. Two slots rather than one because crafting tools are used in the Hideout and gathering tools in the Realm; sharing a slot would force a swap on every activity change. Accepted cost: fishing *and* mining on one run means choosing. `profession-tools.md` §3.1 |
| **D-24** | Documentation. | ✅ **DECIDED — adopted.** **(a)** **Retire** `current-state.md` (stale, superseded by `PROJECT_STATE.md`) and `combat-spec.md` (survivors folded into GDD §5 + `moves.md`). Supersession headers on `itemization.md` and `expansion-plan.md`. **(b)** The **GDD absorbs all decisions now**, as the closing step of this design pass, before any code — §5.5, §5.9, §6, §10 and §18 (6 of 14 unresolved questions are now answered). The GDD is what the next session is told to read first; it must not contradict the decisions for even one slice. |

| **D-25** | The armour formula. | ✅ **DECIDED — adopted.** Replace `max(1, damage − armour)` with the diminishing form `reduction = armour / (armour + 5 × packet)`. The current formula is *total* against chip damage (a 5-damage hit becomes 1) and near-irrelevant against a telegraphed smash — backwards for a defensive investment in a telegraph game, and it interacts badly with the `MinimumDamage` floor. The diminishing form gives armour and resistance **genuinely different jobs**: armour is for attrition, resistance is for spikes, and "do I fear the swarm or the smash?" becomes a real gearing question. `damage-and-defense.md` §5.4, Example H |
| **D-26** | Is Parry universal or gear-granted? *(was U-5)* | ✅ **DECIDED — gear-granted.** A form declares `grants: parry`; without a parrying weapon or shield the command does not exist. Makes weapon forms differ **defensively** rather than only in damage; proves the "an item grants a capability" mechanism before affixes need it; and gives the top of the skill ladder a gearing prerequisite. Auto-combat's 8-tick reaction can never hit the 3-tick window, which is correct. `damage-and-defense.md` §5.3 |

---

## 12.1 Decision log — 26 settled

**Nine changed shape** from what I proposed. Those are recorded above with their reasoning intact,
not quietly rewritten.

| Changed during review | What moved |
|---|---|
| D-01 | multi-packet made explicit; `addAspect` op cut |
| D-02 | vulnerability multiplier made **two-way** |
| D-03 | arcane's "best-in-slot" risk closed **structurally** |
| D-04 | widened — essence **tags** packets/effects for conditions |
| D-05 | two-number resistance display promoted to **required scope** |
| D-06 | `Blocked` fires on **both** outcomes — the original spec punished skill |
| D-07 | auto-combat resolved via **reaction latency**, not a damage penalty |
| D-09 | E2 grew to ~27 statuses; `status.recalled_move` given a dated dependency |
| D-11 | sharpened from "defer" to a standing rule about derived tags |
| D-22 | repeatability surfaced and decided (it was smuggled in implicitly) |
| D-25, D-26 | added after the first pass — the armour formula was omitted from the original 24, and U-5 was resolved |

> **D-08 note.** A direct-chance + diminishing-duration model was briefly selected in error and
> propagated through five documents before being reverted. **Resolve is the decision.** Two ideas
> from that detour were considered for retention and deliberately dropped: a derived
> `stagger_threshold` (redundant — Resolve *is* the stagger threshold) and a per-enemy
> `control` profile (redundant — base Resolve plus the immunity window already tiers enemies).

**[UNRESOLVED] — things I deliberately did not decide for you:**

- **U-1** Whether a **melee/ranged/magic advantage triangle** exists (GDD §18 Q4). It interacts
  with lanes and enemy vulnerabilities; cheaper to decide before enemies are authored. I lean
  **no** — enemy vulnerability tags already provide the counter-play without a global rule.
- **U-2** **Positioning** (GDD §18 Q5). Root, Fear's retreat behaviour, and several delivery
  tags (`projectile`, `area`) are shaped by it. Everything in this package works without it; a
  few statuses gain behaviour if it lands.
- **U-3** **Durability** (GDD §18 Q6). `stat_map.durability` is authored in the fabrication spec
  and the game has none. Affix families like "reduced Integrity damage" imply it.
- **U-4** Exact numeric caps in §4.4. All are single constants; none should be argued about
  before play.
- **U-5** Whether **Parry** is universal or gear-granted. I recommend gear-granted (it makes
  shields and specific weapon forms matter), but it is a real design fork.
