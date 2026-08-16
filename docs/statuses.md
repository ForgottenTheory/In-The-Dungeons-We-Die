# Statuses, Ailments & Crowd Control

> **DECIDED** — settled by the 27 decisions in `effect-foundation.md` §12. Not yet built. Part of the effect-foundation package (`effect-foundation.md`).
> **Replaces the retired `combat-spec.md` §25.** Amends GDD §5.9.
> Labels: **[EXISTING/PRESERVE]** · **[DECIDED]** · **[UNRESOLVED]**

---

# 1. Current state

**Nothing exists.** No `StatusDefinition`, no `StatusInstance`, no controller, no handler for
`applyStatus`. Meanwhile:

- **14 status ids are authored in shipped content** and land in `TriggerRuleEngine.Unhandled`:
  `status.dissonance` `status.fault` `status.feint_ready` `status.filed_intent`
  `status.illuminated` `status.latched` `status.liability` `status.liability_credit`
  `status.phased` `status.planted_charge` `status.recalled_move` `status.rooted_growth`
  `status.spreading` `status.toxin`
- `GameEvents.StatusApplied` / `StatusExpired` exist and are never raised.
- GDD §5.9 lists bleed/poison/burn/stun/slow/vulnerable/guarded as designed and inert.

This is the largest authored-content-to-implemented-system gap in the project, and it is why
statuses come **before** moves in the build order (`effect-foundation.md` §10).

---

# 2. Taxonomy — four categories **[DECIDED]**

The category is not flavour. It determines the stacking rule, the removal rule, and what resists
it. Getting this table right is most of the design.

| Category | Purpose | Stacking | Resisted by | Cleanse group | Examples |
|---|---|---|---|---|---|
| **Ailment** | damage over time | independent instances, capped count | the **lane resistance** at application | `ailment` | Bleed · Poison · Burn · Wither |
| **Impairment** | debuff, no damage | refresh-highest | **status duration/effect** modifiers | `impairment` | Chill · Shock · Corroded · Weaken · Brittle |
| **Control** | prevents or redirects action | never stacks; gated by **Resolve** | **Resolve** (§4) | `control` | Stun · Freeze · Fear · Silence · Root |
| **State** | tactical marker, often self-applied | unique per id | — | `state` | Guarded · Vulnerable · Barrier · Empowered |

**The rule that keeps ailments simple:** an ailment's damage is damage in a lane, so the target's
lane resistance reduces it exactly as it reduces a hit. No separate "damage over time reduction"
stat, no special case in the pipeline. One number answers "why is my Burn weak against this
enemy?"

---

# 3. The roster **[DECIDED — D-09]**

Fourteen **core** statuses ship in v1, plus the **13 authored ids** in §3.5 — ~27 definitions in
E2. Each core status earns its place by doing something no other status does.

## 3.1 Ailments

| Status | Lane | Stacking | Duration | Behaviour |
|---|---|---|---|---|
| **Bleed** | physical | stacks, max 5 | medium | Physical DoT. **Ticks harder while the target is acting** (in windup or execution) — a real interaction with the tick engine and the reason Bleed is the *aggressive* ailment. Magnitude scales with the physical damage of the applying hit. |
| **Poison** | toxin | stacks, max 20 | **long** | Low per-tick, very long, stacks freely. The *attrition* ailment. Pairs naturally with the Venomous prefix's existing toxin-stack mechanic. |
| **Burn** | heat | **refresh-highest** | **short** | High per-tick, short, does not stack — a stronger application overwrites a weaker one. The *burst* ailment. |
| **Wither** | decay | stacks, max 3 | long | DoT **plus** reduced Barrier gain and healing received. The anti-sustain ailment. **Ships later** with the decay lane. |

**Burn vs Poison is the load-bearing contrast:** high/short/no-stack against low/long/stack. That
single pairing is what makes heat and toxin play differently rather than being reskins. It is
PoE's ignite/poison distinction and it is proven.

**[DECIDED — D-09, GDD §5.9 amended] "Ignite" is cut.** In PoE, *ignite* is the application and
*burning* the damage, and the split confuses players permanently. **One concept: Burn.** An
affix reads "12% chance to Burn", the status is Burn, the damage is Burn damage.

## 3.2 Impairments

| Status | Lane | Stacking | Behaviour |
|---|---|---|---|
| **Chill** | cold | refresh-highest | Increases the target's windup and recovery intervals by X% (10–35%). **Only Chill accumulates Freeze buildup** (§3.3). The tempo ailment. |
| **Shock** | charge | refresh-highest | Increases **charge damage taken** by X% and gives a small chance to interrupt the target's action on each hit. The disruption ailment, and the enabler for chaining effects. |
| **Corroded** | corrosion | stacks, max 5 | Reduces **Armour** and **physical resistance** per stack. The answer to armoured enemies, and the reason corrosion is its own lane. |
| **Weaken** | — | refresh-highest | Reduced damage dealt. Plain, useful, no lane. |
| **Brittle** | — | refresh-highest | Increased **critical** damage taken. Distinct from Vulnerable (all damage). **Low priority; ships later.** |

**[DECIDED — D-09, GDD §5.9 amended] "Slow" is folded into Chill.** GDD §5.9 lists both. Slow
is the mechanic (increased action intervals); Chill is Slow with the `cold` lane plus Freeze
buildup. Shipping both means one is strictly a worse version of the other.

**Shock vs Vulnerable, differentiated:** Shock is *lane-specific* (charge only) and *disruptive*;
Vulnerable is *generic* increased damage taken and is applied by moves, not by an aspect. If
Shock were generic it would simply be a better Vulnerable, and the charge lane would have no
identity of its own.

## 3.3 Controls

All are gated by **Resolve** (§4). None is ever applied by a direct roll — the roll decides whether
*buildup* is applied; Resolve decides whether the control *lands*.

| Status | Behaviour | Distinction |
|---|---|---|
| **Stun** | Cannot act; current action is interrupted. **Short.** | The *impact* control. Primary source is stagger damage from heavy hits, not an aspect. |
| **Freeze** | Cannot act; takes **increased physical damage**; **breaks early on a large physical hit (Shatter)**. **Longer than Stun.** | The *cold* control. Longer but conditional — freeze to survive, or freeze and shatter for burst. Genuinely different tactical use from Stun. |
| **Fear** | Cannot use `action:attack` moves; AI biases hard toward defensive/utility moves; the next offensive action is delayed. | The *soft* control. Doesn't stop the target acting, changes **what** it does. |
| **Silence** | Cannot use `action:spell` moves. | Selective; matters because casters exist (Wizard, Invoker, Warlock, Necromancer). |
| **Root** | Cannot move. | **DEFERRED** — meaningless without positioning (GDD §18 Q5, U-2). |

**Freeze requires Chill.** Freeze buildup only accumulates on a target that is currently
Chilled.
This makes cold a **two-step** aspect — apply tempo pressure, then convert it into a lock — which
is mechanically distinct from heat (burst DoT) and charge (disruption). It also answers the
brief's Example D directly.

**Fear without positioning** behaves exactly as the brief proposes: interrupt offence, bias AI
defensive, delay the next attack. If positioning ever lands, Fear gains forced movement and
nothing else changes.

## 3.4 States

| Status | Behaviour |
|---|---|
| **Guarded** | Damage taken reduction. Self-applied by defensive moves and the Bastion's Guard bands. **[EXISTING in GDD]** |
| **Vulnerable** | Damage taken increase. The generic "open" marker — applied by moves, parries, and the Operative's opening-creation engine. **[EXISTING in GDD]** |
| **Barrier** | A temporary absorbing pool with a decay rate. Modelled as a status so it gets duration, stacking, cleansing and triggers for free. See `damage-and-defense.md` §5.7. |
| **Empowered** | Generic "your next X is stronger" carrier, used by *spending Stamina empowers the next attack*-style affixes. Ships when something needs it. |

## 3.5 The 14 already-authored ids **[DECIDED — D-09b]**

These are referenced by shipped `prefixes.json` / `suffixes.json` content and currently land in
`TriggerRuleEngine.Unhandled`. **They are authored in E2 alongside the core roster** — most are
three to five lines of JSON, because the data contract (§6) does all the work.

| Id | Owner | Category | Shape |
|---|---|---|---|
| `status.toxin` | Venomous | Ailment | toxin lane, stacks to 20, long — **this is Poison under another name; alias it** |
| `status.planted_charge` | Explosive | State | 60t, `on_expire: damage` (area) |
| `status.feint_ready` | Trickster | State | 20t, marker tag — permits cancelling a telegraph |
| `status.illuminated` | Radiant | State | marker tag; `revealInfo` on apply |
| `status.phased` | Spectral | State | `while_active`: `damage_taken.mult 0` **and** `damage.mult 0` — intangible both ways |
| `status.rooted_growth` | Sylvan | State | stacking counter; `on_apply` clears on a movement-tagged action |
| `status.latched` | Parasitic | Ailment | DoT + `on_expire: applyStatus` to a new host |
| `status.spreading` | Improper Safety | State | marker; the spread effect reads it at `StatusApplied` |
| `status.dissonance` | Dissonant | Impairment | marker; corrupts statuses applied nearby |
| `status.fault` | Glitched | State | marker; biases outcome rolls |
| `status.filed_intent` | Bureaucratic | State | marker + declared move id in `Text` |
| `status.liability` | Personal Liability | State | stacking counter |
| `status.liability_credit` | Personal Liability | State | stacking counter, spent by its rule |
| ~~`status.recalled_move`~~ | Mnemonic | — | **⚠ NOT authorable in E2** — it stores a Move to replay, and the Move system is E4 |

**Finding worth recording:** 13 of 14 are authorable in E2. `status.recalled_move` genuinely
depends on `MoveDefinition` existing, so **Mnemonic stays inert until E4** — and that is now a
known, dated gap rather than a mystery.

**`status.toxin` should be aliased to `status.poison`, not duplicated.** Venomous was authored
before the roster existed; two toxin-lane stacking DoTs would be the beginning of exactly the
sprawl this taxonomy exists to prevent.

---

# 4. Resolve — the one crowd-control mechanism **[DECIDED — D-08]**

## 4.1 The problem

The brief states it exactly: *Freeze → Freeze → Freeze → Freeze → boss becomes decorative
furniture.* It then proposes five candidate solutions — boss Resolve, CC resistance, diminishing
returns, temporary immunity, buildup thresholds.

**Those are five systems for one problem. Resolve does all five jobs.**

## 4.2 The model

Every combatant has a **Resolve** pool.

```
Resolve        = base(actor) × (1 + Σ resolve modifiers)
ControlBuildup accumulates per control attempt, tracked per control type
Buildup decays at RESOLVE_DECAY_PER_TICK while no control is being applied

When buildup(type) ≥ Resolve:
    → the control lands, at its authored duration
    → buildup(type) resets to 0
    → the target gains Control Immunity for CONTROL_IMMUNITY_TICKS  (blocks ALL controls)
    → the target's Resolve rises by RESOLVE_ESCALATION (+25%) for the rest of the encounter
```

**Buildup is tracked per control type; the threshold, the immunity window and the escalation are
shared.** That combination is what produces the properties below.

Every job, done by one mechanism:

| The brief wanted | Resolve provides it via |
|---|---|
| Boss Resolve | a high base value on boss actors |
| CC resistance | `resolve` modifiers on gear and species |
| Diminishing returns | `RESOLVE_ESCALATION` — each landed control makes the next 25% harder, uncapped |
| Temporary CC immunity | the immunity window after every landed control |
| Buildup thresholds | the buildup pool itself |

## 4.3 What each tier feels like

| Enemy | Resolve | Experience |
|---|---|---|
| Trash | very low | The first Freeze lands almost immediately. CC feels great and is the correct answer to swarms. |
| Elite | moderate | Two or three applications. Requires investment; landing it is a genuine play. |
| **Boss** | high + long immunity + escalation | CC is **a window you earn, then earn again more expensively.** Never a lock, never useless. |

The escalation term is what makes chain-locking self-defeating **without ever printing "immune"
at the player**: the fourth Freeze needs roughly twice the buildup of the first, so a CC build
naturally transitions from *locking* to *punctuating* as a fight goes on. That in-fight arc is the
thing no flat diminishing-returns ladder can produce.

## 4.4 Stagger folds in

Stagger is **not** a separate system. A move's `StaggerPower` is simply control buildup toward
**Stun**. So:

- Heavy weapons stun through the same pool that Freeze and Fear compete for.
- `combat.stagger.vulnerable` (an existing modifier key) becomes "reduced Resolve".
- The Juggernaut's *"ignores interrupts"* is high Resolve plus `combat.interrupt.immune`
  (existing flag key).
- **A build cannot Stun-lock *and* Freeze-lock**, because both spend the same Resolve. Significant
  balance property, and free.

`ControlResisted` is raised when buildup is added but does not cross the threshold, and when a
control attempt lands into the immunity window — the hook for "when you resist control…" affixes.

## 4.5 Player-facing wording

Mechanically, controls are buildup. **Player-facing text still says "12% chance to Freeze."** The
chance roll decides whether *buildup is applied*; the buildup decides whether the freeze *lands*.
The tooltip stays one readable sentence; the Hit Log shows the truth:

```
Freeze buildup +34   [Chilled ✓]   Resolve 34/80
```

**The Resolve bar is required scope**, for the same reason the two-number resistance display is
(D-05a): a hidden pool makes CC feel random. Enemy frames show `Resolve 34/80` and a fill that
drains as buildup accumulates, so "one more heavy hit and it breaks" is *readable*.

## 4.6 Constants **[UNRESOLVED — U-4]**

| Constant | Proposed |
|---|---|
| `RESOLVE_DECAY_PER_TICK` | 1.5% of max |
| `CONTROL_IMMUNITY_TICKS` | 60 (3s at 20 t/s) |
| `RESOLVE_ESCALATION` | +25% per landed control, per encounter, uncapped |
| Base Resolve — trash / normal / elite / boss | 20 / 50 / 120 / 300 |

---

# 5. Application model **[DECIDED — D-10]**

The brief guesses correctly: **a hybrid**.

| Category | Model | Reason |
|---|---|---|
| **Ailment** | direct chance; magnitude from the hit's damage in that lane | Readable, proven, and the magnitude link makes lane resistance do double duty |
| **Impairment** | direct chance; magnitude from the applying effect | Same |
| **Control** | **buildup vs Resolve** — never a direct application | The anti-lockdown answer (§4) |

| Category | Model | Reason |
|---|---|---|
| **Ailment** | direct chance, magnitude from the hit's damage in that lane | Readable, proven, and the magnitude link makes lane resistance do double duty |
| **Impairment** | direct chance, magnitude from the applying effect | Same |
| **Control** | **buildup vs Resolve** — never a direct application | The anti-lockdown answer (§4) |

**Magnitude derivation** — one formula, no special cases:

```
ailmentMagnitude = finalLaneDamage × ailmentCoefficient(status) × (1 + Σ status effect modifiers)
```

Because `finalLaneDamage` is *post-mitigation*, the target's resistance already reduced it. So
a fire-resistant enemy takes less Burn damage automatically, with no second calculation and no
second stat.

**Ailment resistance and duration** are `status.*` modifier keys (reduced duration, reduced
effect, chance to avoid application per category or per status). "Chance to avoid Poison" is a
per-status avoidance roll at application time, capped and `diminishing` like all avoidance.

---

# 6. The status data contract **[DECIDED]**

One C# type for definitions, one for runtime instances. **No bespoke class per ailment** — the
brief's requirement, and the thing that makes 14 statuses cost roughly what 3 would.

```jsonc
{
  "id": "status.burn",
  "name": "Burn",
  "category": "ailment",                 // ailment | impairment | control | state
  "tags": ["mech:ailment", "lane:heat"],
  "lane": "heat",                        // resistance family; null for laneless

  "duration_ticks": 60,
  "tick_interval": 10,                   // 0 = no periodic tick
  "stack_policy": "refresh_highest",     // stack | refresh_highest | refresh_duration | unique
  "max_stacks": 1,

  "magnitude": { "basis": "lane_damage", "coefficient": 0.30 },

  "on_apply":    [ ],
  "per_tick":    [ { "kind": "damage", "text": "heat", "amount": 1.0, "scales_with": "magnitude" } ],
  "on_expire":   [ ],
  "while_active":[ ],                    // ModifierContribution list — see below

  "cleanse_group": "ailment",
  "control_buildup": 0,                  // controls only: buildup applied per application
  "requires_status": null,               // e.g. status.freeze requires status.chill
  "description": "Burning. Short, fierce, and it does not stack."
}
```

**The important economy:** `while_active` is a list of **`StatGrant`s** — the same atom as an
item affix (`effect-foundation.md` §2.1). So Chill is literally:

```jsonc
"while_active": [ { "key": "combat.windup.mult",   "value": 1.25 },
                  { "key": "combat.recovery.mult", "value": 1.25 } ]
```

No status-specific code. No new machinery. The `ModifierSet` and `TriggerRuleEngine` that already
exist do all the work; the status controller only manages **lifetime** — apply, stack, tick,
expire, cleanse.

`on_apply` / `per_tick` / `on_expire` are **`EffectSpec[]`** — the same 24-effect vocabulary. A
status can therefore do anything an affix can do, including applying another status, which is how
Freeze-on-Chill and status spreading work.

## 6.1 Runtime

```csharp
sealed record StatusInstance(
    string StatusId, string SourceId,
    double Magnitude, int Stacks,
    long AppliedTick, long ExpiresTick, long NextTickAt,
    EffectContext Context);           // the chain that applied it — for proc safety
```

`StatusController` per combatant: apply (resolving stack policy), advance (on the shared
`TickEngine`), expire, cleanse by group, query by id/category/tag. Raises `StatusApplied` /
`StatusExpired` — **events that already exist and are currently never raised.**

**Proc safety:** the `EffectContext` travels with the instance, so a status applied at depth 1
ticks at depth 1. Its per-tick damage raises `DamageTaken` but **never `HitLanded`** — the rule
from `damage-and-defense.md` §6 that prevents an entire class of DoT-driven proc loops.

## 6.2 Validation **[DECIDED]**

| Rule | Catches |
|---|---|
| Every `applyStatus`/`removeStatus` Text resolves to a status id or a cleanse group | **the 14 dangling ids today** |
| `lane` resolves to a registered resistance lane | `lane:lightning` |
| `requires_status` resolves and is not circular | Freeze→Chill→Freeze |
| `stack_policy` is compatible with `max_stacks` (`refresh_highest` ⇒ 1) | conflicting stack rules |
| `control_buildup > 0` ⟺ `category == control` | a control with no buildup is unappliable |
| Every `while_active` key exists and its scope dimension matches | typo'd modifier key |
| Every status is reachable — some effect, move or affix can apply it | dead content |
| Statuses in `per_tick` cannot apply themselves | infinite self-refresh |

---

# 7. Interaction matrix **[DECIDED]**

The interactions worth authoring, and the ones deliberately left out.

| Interaction | Effect |
|---|---|
| **Chill → Freeze** | Freeze buildup only accumulates on a Chilled target |
| **Freeze + heavy physical hit** | **Shatter** — Freeze breaks early, the hit gains bonus damage |
| **Shock + charge damage** | Charge damage taken increased; the chaining enabler |
| **Corroded + physical** | Armour and physical resistance reduced per stack |
| **Burn + Chill** | opposed: applying one **reduces the other's remaining duration** (mirrors the crafting algebra's heat/cold annihilation — the same idea in a different system, which is good) |
| **Poison + Wither** | Wither reduces healing/Barrier, so Poison's attrition sticks |
| **Bleed + acting target** | Bleed ticks harder while the target is in windup or execution |
| **Vulnerable + anything** | flat increased damage taken — the generic amplifier |
| **Fear + Silence** | a caster feared *and* silenced has no offensive options — deliberately strong, deliberately Resolve-gated twice |

**Deliberately not authored:** a full N×N status interaction table. That is the recipe-table
failure mode in a different costume (GDD §17.7). Nine authored interactions plus the generic
rules is the budget.

---

# 8. What this unblocks immediately

Once E2 ships, previously-inert authored content becomes live with **no content changes**:

| Content | Becomes |
|---|---|
| `prefix.venomous` | toxin stacks that actually stack and burst |
| `prefix.explosive` | planted charges that detonate |
| `prefix.trickster` | a real feint window |
| `prefix.radiant`, `prefix.parasitic`, `prefix.sylvan`, `prefix.glitched`, `prefix.spectral`, `prefix.bureaucratic`, `prefix.dissonant` | their status ids resolve (§3.5) |
| `suffix.improper_safety_procedures`, `suffix.personal_liability` | status spreading and liability counters |
| GDD §5.9's bleed/poison/burn/stun/vulnerable/guarded | exist |
| **`prefix.mnemonic`** | **still inert — `status.recalled_move` needs the Move system (E4)** |

That is the argument for building statuses before moves: **the content is already written.**
13 of the 14 dangling ids go live in E2; one has a dated dependency instead of being a mystery.
