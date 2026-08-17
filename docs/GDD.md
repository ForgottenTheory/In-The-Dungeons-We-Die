# In The Dungeons We Die — Game Design Document

> **Consolidated GDD.** Written against the project as it actually stands (revised after the
> effect-foundation build E0–E4; 602 passing tests). Where older documents conflict with newer
> decisions, the newer decision is recorded here and the older one is marked superseded.
>
> **Status marks:** **BUILT** (in the game, tested) · **PLANNED** (designed and settled, not yet
> built) · **NEEDS DESIGN** (little or no design exists). Anything undecided is marked rather
> than invented. §19 is the full status summary.

---

# 1. Vision & Pillars

**In The Dungeons We Die** is a progression-heavy extraction RPG. The player builds a strange
character, trains persistent professions, invents materials nobody designed, forges those
materials into gear, and takes them into a hostile Realm — where the defining question is always
*"I have valuable loot and I'm still alive. Do I leave, or push my luck?"*

### The five pillars

**1. Emergent crafting, not recipes.**
Materials have numeric properties. A universal reaction algebra decides what happens when you
combine them. There is **no recipe table** — every combination produces a real, named, stackable
material, and unusual experimentation yields legitimate results rather than "nothing happens".

**2. Builds you assemble, not classes you pick.**
Base + Prefix + Suffix produces 18,750 characters, none of them hand-authored. The Suffix layer
is deliberately absurd — the player should think *"wait, my character can do WHAT?"* rather than
*"+7% damage."*

**3. Tick-based tactical combat.**
Continuous, interval-driven simulation. **Not turn-based.** The clock runs while the player
decides. Skill is reading telegraphs and answering them in time.

**4. Layered, persistent progression.**
Melvor Idle is the explicit reference for the profession/skill architecture: skill levels,
per-action mastery, interval reduction, preservation, doubling, unlocks, offline progress. A
player should have *many overlapping reasons* they got better at Mining, not just "Mining 37".

**5. Extraction risk.**
Realm loot is unsecured until extracted. Death forfeits it. Persistent progression survives.

### The guiding question

Applied to every proposed feature:

> *"How does this make preparing for, exploring, surviving, mastering, or extracting from a
> Realm more interesting?"*

If there is no convincing answer, the feature is reconsidered.

### Tone

Grim-funny. Dungeon Crawler Carl is the tonal reference — a lethal dungeon administered by
something with a sense of humour and a filing system. This is why character modifiers read like
citations, lawsuits, medical warnings and workplace incidents rather than fantasy titles.

---

# 2. Core Gameplay Loop

```
HIDEOUT
  ├─ Train professions (active or passive/idle)
  ├─ Gather and process resources
  ├─ Experiment in the crafting bench → discover an emergent material
  ├─ Fabricate that material into equipment            [PLANNED]
  ├─ Choose Species + Base + Prefix + Suffix
  └─ Prepare loadout                                    [PLANNED]
        ↓
REALM RUN
  ├─ Enter → explore a spatial location graph
  ├─ Gather biome resources (time passes; risk accrues)
  ├─ Fight varied enemies using your moveset            [PLANNED]
  ├─ Harvest creature anatomy → crafting inputs         [PLANNED]
  └─ Reach a depth checkpoint
        ↓
   ┌─ EXTRACT ──→ loot secured to Stash
   └─ GO DEEPER ─→ better rewards, escalating danger
        ↓
   (death forfeits unsecured loot; progression survives)
        ↓
BACK TO HIDEOUT — better materials, better professions, another stupid experiment
```

The loop is **closed but not yet continuous**: every stage exists in some form, but equipment
fabrication and combat depth are the two links that don't yet carry weight.

---

# 3. Character Identity

## 3.1 Structure

```
Species  +  Base  +  Prefix  +  Suffix
```

- **Species** — physiology. *Currently 3 authored, mechanically shallow. Needs Design.*
- **Base** — the progression chassis: what grows, plus a starting kit — never a license (D25).
- **Prefix** — one mechanic that mutates that playstyle.
- **Suffix** — a rule-breaking modifier, with three expressions so any build can use it.

> **Superseded:** `docs/classes.md`'s original roster (Hexslinger, Wayfarer, Pitfighter,
> Gravetender, Haruspex, Warden, Wretch; the Pyromaniac/Bloodbound prefixes; the "Of The …"
> suffixes). Only Bastion, Exploding Kneecaps, The Last Laugh and Unreasonable Confidence
> survived the roster replacement.

## 3.2 Attributes

Seven, shared by all characters: **Strength · Dexterity · Intelligence · Constitution · Wisdom ·
Endurance · Luck.**

Three resources derive from them: **Health · Mana · Stamina.** Health does **not** regenerate in
combat — damage is Realm attrition and healing costs resources.

## 3.3 The growth budget — the load-bearing rule

**Every Base distributes exactly the same attribute growth per level (4.0 points). Only the
shape differs.**

This is what makes Base choice a *trade* rather than a menu where some options are strictly
larger. Three attributes pushed hard means four left behind. Authored weights name the notable
attributes and the remainder trickles evenly; fractions accumulate so a 0.8 secondary still
grows. By level 21 a Juggernaut has roughly 3× a Wizard's Strength and the Wizard has 3× the
Intelligence — with identical totals.

Enforced by content validation and by test.

## 3.4 The 15 Bases

A Base is distinguished by its **engine** — how resource flows and how its loop feels against a
ticking clock — not by its flavour. Two Bases with the same engine and different themes are the
same Base.

**An engine is the Base's *starting kit and affinity*, never an exclusive license (D25).** The
gauge, hooks and starting moves a Base begins with are universal definitions any layer may grant
later — equipment, Prefix, Species, a learned specialization (flagship: a tower shield granting
Guard). What keeps the Wizard best at the Held Spell is growth weights, resources and modifiers —
soft specialization, never hard permission. The interesting question is *"how well can this build
make Fireball work?"*, never *"is this Base allowed to cast Fireball?"*

> **Standing rule (D25, enforce forever):** move requirements are physical and conditional —
> equipped tags, costs, statuses. A class-check condition kind may never be added to the rule
> vocabulary.

| Base | Growth (pri / sec) | Resource | Channel | Engine | Weakness |
|---|---|---|---|---|---|
| **Fighter** | STR·DEX / END | Stamina, *no gauge* | Strike | Moveset comes from the **weapon**; reconfigures by re-equipping | Only as good as their gear |
| **Juggernaut** | STR·CON / END | **Momentum** | Strike | Builds from damage dealt *and taken*; shortens windups, ignores interrupts; decays on inaction | Lulls and forced repositioning kill the engine |
| **Operative** | DEX·LUK / INT | *lives on the target* | Strike | Creates openings, then dumps enormous damage into the window it made | Nothing banked between fights |
| **Outlander** | DEX·WIS / CON | consumables | Strike | Finite prepared resources — ammo, traps, ground — spent at range | Collapses once closed on |
| **Kineticist** | INT·STR / DEX | **Force** | Strike | Charge Force, release as displacement; damage from **collision and geometry**, not the spell | Needs terrain and bodies to work with |
| **Vitalist** | WIS·CON / END | *Health itself* | Guard | No second bar — banks and moves life between targets | Every cast is a real risk |
| **Wizard** | INT·WIS / DEX | **Held Spell** | Strike | Charge one big spell and *hold* it, releasing on the right tick | A wasted hold is a wasted fight |
| **Invoker** | INT·END / CON | **Intensity** | Surge | Sustained channels that ramp the longer they're held, draining continuously | Fragile mid-channel; all-or-nothing |
| **Druid** | WIS·STR / CON | **Form** | Guard | Forms swap the entire moveset and survivability profile at a transformation cost | Caught in the wrong form is fatal |
| **Bastion** | CON·END / STR | **Guard** | Guard | Rechargeable block pool; precise blocks refund it | Very low damage; must hold ground |
| **Bard** | LUK·WIS / DEX | songs | Surge | Manipulates **the clock** — intervals, telegraph length, recovery | Low damage; needs uptime |
| **Necromancer** | WIS·INT / CON | **Thralls** | Surge | Kills make bodies; thralls act on independent intervals | Nothing at the start; long ramp |
| **Artificer** | INT·DEX / END | **Charges** | Surge | Deployables ticking on their own timers; consumes crafted gear more than anyone | Setup time; deployables destroyable |
| **Warlock** | INT·LUK / CON | **Debt** | Surge | Power is cheap in mana, expensive in Debt — which grows, empowers, then collects | Self-destructive |
| **Vanguard** | STR·END / DEX | **Threat** | Guard | Forces enemy targeting; dictates *where and when* the fight happens | Fragile the moment it stops dictating |

**Gauges are optional and seven Bases have none.** Giving everyone a bar would flatten exactly
the distinctions the roster exists to create. A gauge is a meter with generation rules, decay,
capacity and threshold bands; its generation is expressed as ordinary event hooks, so it needs
no bespoke machinery.

⚠ **Fighter's engine is stale.** "Moveset comes from the weapon" was universalized for *everyone*
in E4 — every build's moveset composes weapon-first now. Fighter needs a new identity hook that
is not a license. **NEEDS DESIGN** (§18 #15), targeted at M2′.

**Gauge behaviour taxonomy** (a design lens for balance, not a hard rule): Build & Spend ·
Charge & Hold · Sustain & Ramp · Deplete & Recover · Debt & Collect.

### Why the martial four aren't one class

Answering a big telegraph: **Fighter** swaps to the weapon that counters it · **Juggernaut** eats
it and profits · **Bastion** blocks precisely and refunds Guard · **Vanguard** makes it target
someone else.

### Why the caster four aren't one spell list

**Wizard** burst-on-timing · **Invoker** ramp-on-duration · **Warlock** debt-for-power ·
**Necromancer** scale-on-attrition. Four different fights at identical damage numbers.

## 3.5 Expression channels

Suffixes express through one of three channels, keyed to **events every build produces** rather
than to attribute archetypes — so no Suffix is ever unusable by a given Base.

| Channel | Fires when you… |
|---|---|
| **Strike** | land a discrete damaging hit |
| **Guard** | avoid, absorb, mitigate, or protect |
| **Surge** | spend, accumulate, or sustain a resource |

This was chosen over a Might/Finesse/Focus archetype model because archetype channels distribute
badly (six of fifteen Bases landed in "Focus") and leave hybrid builds ambiguous. Event channels
are universal — a caster blocks sometimes, a Bastion hits sometimes, everyone runs a resource.

The Base declares a default. Prefixes or equipment shifting it is a designed extension point.

## 3.6 The 25 Prefixes

Each adds **one recognizable mechanic**, not ten small bonuses.

**Hard rule: a Prefix may never reference a Base.** Galvanic hooks "resource spent" — so a
Juggernaut charges by swinging, a Wizard by releasing a hold, a Bastion by absorbing, a Vitalist
by paying in blood, and a Warlock fastest and most dangerously of all. Five feels, one design.
Without this rule the roster would cost 15 × 25 = 375 hand-authored combinations.

| Prefix | Mechanic |
|---|---|
| **Trickster** | **Feint** — cancel a telegraph into something else, punishing whoever committed |
| **Galvanic** | **Charge** from any resource spend, discharging as a chain arc |
| **Explosive** | **Planted charges** detonating on a timer, or instantly on death |
| **Venomous** | **Toxin stacks** that grow, burstable early by a heavy action |
| **Gravitic** | **Wells** — actions leave pull/slow zones |
| **Vampiric** | **Siphon** — banked life that must be *claimed*, never auto-healed |
| **Clockwork** | **Cadence** — consistent action intervals build a bonus |
| **Spectral** | **Phase** — intangible, and unable to affect anything either |
| **Sylvan** | **Rooting** — holding position grows stacks; moving resets them |
| **Abyssal** | **Devour** — kills steal a trait for the encounter |
| **Radiant** | **Illumination** — damaged enemies are lit and cannot hide |
| **Seismic** | **Aftershock** on any long-windup action |
| **Chrono** | **Rewind** — snapshots you can spend to restore |
| **Psionic** | **Overload** built by *observing* enemy telegraphs, spent to interrupt |
| **Crystalline** | **Lattice** — huge mitigation that shatters into area damage |
| **Bureaucratic** | **Filed intent** — declare your next action, then comply or be penalized |
| **Recursive** | **Echo** — moves re-fire at reduced power, chaining |
| **Dissonant** | **Interference** — statuses near you corrupt |
| **Parasitic** | **Latch** — a parasite that drains and leaps to a new host on death |
| **Infested** | **Brood** — damage in either direction spawns autonomous swarmlings |
| **Masochistic** | **Suffering** — damage taken becomes currency; healing destroys it |
| **Mnemonic** | **Recall** — replay a stored move instantly, no windup |
| **Biomechanical** | **Grafts** — implanted materials contribute properties to *you* |
| **Glitched** | **Fault** — actions misfire into wrong, occasionally better, outcomes |
| **Quantum** | **Superposition** — two outcomes collapsing on a steerable condition |

Seven bring a gauge. **A build runs at most two meters** — one from the Base, one from the
Prefix. Three would stop being readable.

### Known overlaps, accepted

- *Clockwork / Recursive / Mnemonic* all touch repetition (consistent timing / automatic delayed
  echo / manual instant replay). Tightest cluster; Mnemonic is the first cut if one must go.
- *Glitched / Quantum* both branch outcomes — uncontrolled vs steerable. Keep both only if
  Quantum's collapse condition stays genuinely readable.
- *Masochistic / Juggernaut* both profit from damage taken; a spendable pool vs passive tempo.
  Intentionally a strong pairing — watch whether it's *too* strong.

## 3.7 The 50 Suffixes

Rule-breakers, and explicitly allowed to reach **outside combat** — harvesting, extraction,
crafting, Realm danger, loot. This is where the tone lives.

**Every expressed Suffix carries one expression per channel**, so no build looks at it and finds
a mechanic meant for someone else. Every expression states a drawback.

**Ten are fully expressed:** Exploding Kneecaps · Improper Safety Procedures · The Last Laugh ·
Questionable Ethics · Mandatory Overtime · Unlicensed Surgery · The Emergency Exit ·
Personal Liability · Terminal Curiosity · Absolutely No Refunds.

**Forty are roster entries** — named, formatted, given a one-line fantasy, awaiting mechanical
design. This is deliberate: the naming system and the roster ship ahead of 150 authored
mechanics, and the ten prove the three-expression model before it's multiplied.

Worked example — **Exploding Kneecaps**:

| Channel | Trigger | Effect |
|---|---|---|
| Strike | a heavy hit lands | detonates at the target |
| Guard | a block lands | detonates against the attacker |
| Surge | a large resource dump | detonates around you |

> *Drawback: the blast does not discriminate.*

**A deliberate anti-synergy, preserved by test:** Absolutely No Refunds makes actions
uncancellable; The Trickster's entire mechanic is cancelling.

## 3.8 Dynamic naming

Not every character reads as "Prefix Base of the Suffix". Each Suffix carries **format metadata**
that changes the grammar of the sentence. Nine styles:

| Format | Example |
|---|---|
| standard | The Galvanic Bastion of Questionable Ethics |
| citation | The Explosive Vanguard [Cited for Improper Safety Procedures] |
| investigation | The Venomous Operative, Currently Under Investigation for Unscheduled Violence |
| warning | The Trickster Bard (Warning: Excessive Enthusiasm) |
| medical | The Recursive Warlock, Against Medical Advice |
| liability | The Seismic Juggernaut (Personal Liability Accepted) |
| bureaucratic | The Bureaucratic Artificer [Subject to Mandatory Overtime] |
| consequence | The Quantum Wizard, Due to Repeated Bad Decisions |
| notice | The Glitched Necromancer — Absolutely No Refunds |

**Formatting never touches mechanics.** Changing how a name reads can never change how a
character plays. Verified across all 18,750 combinations.

## 3.9 Species — **Needs Design**

Three exist (Human, Undead, Fey-Touched) as small stat packages. The original design intends
Species to define *physiology*: resistances, vulnerabilities, resource behaviour, innate
abilities, environmental and profession interactions — with **one coherent identity each**, not
fifteen unrelated bonuses.

Designed-but-unbuilt pool: Human · Undead · Automaton · Fey-Touched · Beastkin · Hollowborn ·
Stoneblood · Ashborn · Deepkin · Veilborn.

Species was explicitly held out of the class-combinator pass and remains the least-developed
identity layer.

## 3.10 Respec

**Decided:** costly but reasonably accessible, priced in Realm currency. Permanent locking was
rejected as too punishing for a game with hundreds of hours of persistent progression; free
respec was rejected as removing the weight of the choice. **Not implemented.**

---

# 4. Progression

Progression runs on **multiple independent tracks** so no single "Character Level" represents
everything.

| Track | Persistent? | Lost on death? |
|---|---|---|
| Character level & attributes | Yes | No |
| Profession levels & XP | Yes | No |
| Per-action Mastery | Yes | No |
| Realm Knowledge (per realm) | Yes | No |
| Crafting discoveries / codex | Yes | No |
| Equipment owned | Yes | Gear is safe by default |
| **Unsecured Realm loot** | **No** | **Yes** |

**Horizontal over vertical.** Progression should unlock new tactical options, recipes, material
combinations, routes and preparation strategies — not merely bigger numbers.

**The circle:**
```
Profession progress → better preparation → better Realm performance →
better extraction → better materials → better crafting → deeper Realms →
new profession opportunities → …
```

## 4.1 Melvor-style progression layers

Melvor Idle is the explicit architectural reference. Target layers:

| Layer | Status |
|---|---|
| Skill XP and levels | **Built** |
| Action intervals | **Built** |
| Active vs passive training | **Built** |
| Mastery XP / levels | *Stored but unused* |
| Mastery Pool + checkpoint rewards | Needs Design |
| Interval reduction | Planned |
| Resource preservation | Planned |
| Doubling / increased yield | Planned |
| Level-based unlocks | Partial (gating exists, rewards don't) |
| Mastery-based unlocks | Planned |
| Equipment affecting skills (tools) | Planned |
| Cross-skill bonuses | Planned |
| Global/account passives | Planned |
| Offline progression | Designed, not built |
| Progression milestones | Needs Design |

**Additional layers worth considering, flagged but undecided:** a purchasable upgrade/shop axis
as a resource sink; skill-completion milestones as horizontal goals; equipment set bonuses; and
whether combat adopts a **melee/ranged/magic advantage triangle** (cheaper to decide before
classes and enemies are finished).

**Active vs passive** is a standing rule everywhere: passive is automatic, reliable, lower yield,
lower quality ceiling, fewer rare outcomes. Active rewards actual performance — not merely
clicking "Active Mode".

---

# 5. Combat

## 5.1 The model — tick-based, not turn-based

**This is a hard constraint.** Combat is a continuous, interval-driven simulation on a shared
deterministic tick engine. The clock keeps running while the player deliberates. There are no
turns, no initiative rounds, no "player acts, enemy acts" alternation.

For The King 2 is a reference for *tactical readability and presentation* — clear combatants,
clear moves, readable targeting, strong telegraphs, understandable statuses — **not** for its
turn structure.

## 5.2 Action lifecycle

```
QUEUE → TELEGRAPH → WINDUP → EXECUTION → RECOVERY → READY
```

Not every action uses every stage. Telegraph communicates intent ("Goblin Brute: OVERHEAD
SMASH"); windup is the window where an action can be interrupted, dodged, blocked or countered;
recovery creates the counterattack window.

> **BUILT (E2a).** Telegraph and windup are separate scheduler states; an interrupt records
> *which phase it cut*, so content can distinguish "stopped them before they swung" from
> "stopped them mid-swing". Landed with zero existing tests changed. Interrupt-immunity is a
> move property (`interruptible: false`) and a modifier flag, both live (E4).

## 5.3 Skill expression

Reading telegraphs · timing defence · target prioritisation · resource management · interrupt
timing · ability selection · consumable decisions · knowing enemies · building appropriate gear.

**Player knowledge and character progression both matter; neither should fully replace the
other.** Active play must earn its advantage through better decisions — never a hidden
"+50% active damage".

## 5.4 Resources

- **Health** — no natural regeneration in combat. Damage is attrition.
- **Mana** — may regenerate; Wisdom influences maximum, regen and efficiency.
- **Stamina** — physical action economy: attacking, blocking, dodging, moving. Endurance-driven.
- **Class gauges** — up to two (one Base, one Prefix).

## 5.5 Damage & defence

> **Settled by the effect-foundation decisions D-01 – D-07; the core is BUILT (E1–E4).** Full
> specification in `docs/damage-and-defense.md`.
>
> **BUILT:** packets and lanes, the ordered traced pipeline (the Hit Log), diminishing armour
> (`armour/(armour + packet)`, D-27), per-lane resistance with cap/floor **plus
> `combat.resist.<lane>` contributions (R4a — D-07 executed)**, enemy vulnerability
> multipliers, timed block/dodge stances, **Perfect Block**, **Parry (gear-granted, R4c-2)**,
> **Evade** (untelegraphed only) and **lane avoidance**, **flat lane penetration after the
> cap**, crit, the INCREASED stage reading real modifiers, damage-taken and block-strength
> modifiers, **Barrier absorption** (`BarrierBroken` live), **thorns/retaliation as triggered
> rules**, **ailment application chances (affix-sourced, R4b)**, status potency/duration keys,
> and the `capped / raw` display on the armour summary.
> **PLANNED (E7/Exotic tier):** exposure-as-debuff content, inversion, ignore-fraction,
> stored retaliation; the full `capped / raw` character sheet and preparation screen.

**A hit is a list of packets**, each carrying exactly one **damage type**
(*Slashing · Crushing · Piercing · Magic*) and zero-or-one **aspect**
(*heat · cold · charge · toxin · corrosion · decay · arcane*). A flaming sword resolves as
80 Slashing plus 20 Slashing/Heat — never as one relabelled hybrid.

**One resistance per packet.** `Lane = aspect ?? type`. Armour applies wherever the packet's
*delivery type* is physical, whatever its aspect — so an aspect can never be used to bypass
armour, and hybrid damage is never taxed twice.

**Eight resistance lanes:** `physical · magic · heat · cold · charge · toxin · corrosion · decay`.
Slashing/Crushing/Piercing share **one** physical lane; per-type weakness lives on the **enemy**
as a two-way vulnerability multiplier, which is what Realm Knowledge reveals. **`arcane` has no
lane at all** — unresistable, and structurally unamplifiable in exchange.

**Essence** (fire/frost/storm/…) never becomes a lane. It empowers its anchor aspect, gates
supernatural affixes, modifies ailments, and tags effects for conditional logic — *identity and
metadata, never a mitigation calculation.*

**Resistance order:** sum → exposure → cap (75%, 90% with rare max-res affixes) → **inversion** →
penetration → floor (−100%). Overcapping absorbs debuffs but not penetration, so resistances
display as `capped / raw`.

**Defence layers:** evasion (timed) · block (timed, mitigation) · **perfect block** (tight window,
avoidance, refunds Guard) · **parry** (gear-granted — a form must declare it — avoidance plus a
counter-window) · **armour** (`armour/(armour + 5×packet)`, so it is strong against attrition and
weak against spikes, while resistance is the reverse) · resistance · lane avoidance (rare,
hard-capped) · **Barrier** · damage-taken modifiers · **Resolve** (controls).

**Blocking** is an intentional timed decision that costs stamina; holding block forever must
never be optimal. **Dodging** trades action time and stamina for avoidance. Both are timed
stances that only matter near an incoming attack's execution tick — this is the core skill test
and it works today. **Gear buys the window and the cost, never a passive dodge roll**, so the
skill test can never be priced out. Auto-combat uses the same stances and is disadvantaged by
*reaction latency* rather than by a damage penalty.

**Recovery is Barrier, not healing.** No affix grants passive Health regeneration; Health remains
Realm attrition. Barrier renders as an overlay on the Health bar, so it does not spend the
two-meter readability budget of §3.6.

## 5.6 Action intervals

```
EffectiveInterval = max(MinimumInterval, BaseInterval × modifiers)
```

Hard minimums prevent degenerate zero-time actions — this is now enforced as data on the
modifier key itself, so no combination of haste sources can bypass it. **BUILT**, with one
recorded debt: D-20 tightened the interval floor to **0.55** and the shipped registry still says
0.25 — an unapplied decision awaiting a balance pass, not an open question.

## 5.7 Auto-combat

Passive Realm runs will eventually need automatic combat. **Auto-combat uses the same rules** —
automation chooses actions, the domain resolves them normally. There is deliberately no separate
"fake" combat calculator, so passive and active never become two unrelated balance models.

**PLANNED — and closer than it was:** enemies now run weighted AI rules over the shared
condition vocabulary (E4), and auto-combat is designed as *the player driven by the same profile
shape*, disadvantaged by reaction latency rather than a damage penalty (D-07). The profile
machinery exists; pointing it at the player does not.

## 5.8 Positioning — deferred

Production combat is intended to support tactical positioning (a small per-side grid) affecting
melee range, ranged distance, area effects, hazards, protection and targeting. **Explicitly
deferred**; the current model is single-position. Deferring is a conscious choice — adding it
later multiplies the design space of every move and every enemy.

## 5.9 Statuses — **BUILT (E2–E4)** · Hazards — **PLANNED**

> **Settled by D-08 – D-10.** Full specification in `docs/statuses.md`.

Hazards operate on ticks and telegraph their resolution so players can react (poison clouds,
fire, falling debris, trap tiles, freezing pulses). **Hazards remain PLANNED** — nothing places
one in a Realm yet.

**Statuses are fully data-driven** — one definition type, no bespoke class per ailment — in four
categories whose rules differ:

| Category | Ships in v1 |
|---|---|
| **Ailments** (damage over time) | Bleed · Poison · Burn |
| **Impairments** (debuff, no damage) | Chill · Shock · Corroded · Weaken |
| **Controls** (prevent or redirect action) | Stun · Freeze · Fear · Silence |
| **States** (tactical markers) | Vulnerable · Guarded · Barrier |

**Burn supersedes Ignite** and **Chill supersedes Slow** — shipping both of either pair means one
is strictly a worse version of the other. Root, Wither and Brittle are deferred.

The load-bearing contrast is **Burn vs Poison**: high damage / short / no stacking against low /
very long / stacks to 20. That single pairing is what makes heat and toxin play differently
rather than being reskins. **Freeze requires Chill**, making cold a two-step aspect.

**Crowd control is gated by Resolve** — a pool every combatant has. Controls apply *buildup*;
crossing Resolve lands the control, opens a **Control Immunity** window blocking all controls, and
raises Resolve **+25% for the rest of the encounter**. So the first Freeze lands in seconds, the
third takes fifteen, and a boss is **never locked and never immune** — CC becomes a window you
earn, then earn again more expensively. **Stagger folds in** as buildup toward Stun, so a build
cannot Stun-lock *and* Freeze-lock. Player-facing text still reads "12% chance to Freeze"; the
Resolve bar shows the truth.

**All of the above is BUILT**: 28 status definitions (the 14 core plus **all 14**
previously-dangling authored ids — `status.recalled_move` went live with the Move system),
DoT ticks that cannot proc anything (proc-safety rule 4), `while_active` modifiers that combat
actually reads (Chill genuinely slows windups; Corroded genuinely strips armour), Resolve with
shared buildup, control immunity and per-encounter escalation, and stagger folding into Stun
buildup exactly as designed. **One PLANNED remainder:** ailment application *chances* have no
source until E5 affixes grant them, so Bleed/Burn/Poison do not yet fire from ordinary hits —
the plumbing and the post-mitigation magnitude rule are in and tested.

---

# 6. Moves & Movesets — **BUILT (E4)**; library + acquisition **BUILT (M2′)**

> **Settled by D-18; the engine is BUILT.** Full specification in `docs/moves.md`.

A universal **Move** represents melee attacks, ranged attacks, spells, defensive actions,
utility, reactions, channels, summons, class abilities and enemy abilities — one data shape, no
hardcoded system per category, no giant class of nullable fields.

**A Move is tags + timing + costs + requirements + targeting + packets + effect riders**, where
effects and requirements reuse the same vocabulary the Prefix/Suffix hook system uses. An attack
and a spell differ only in their data — Heavy Strike, Fireball and Shield Bash ship as exactly
that demonstration. `MoveKind` exists for dispatch and filtering; behaviour never switches on it.

**What the build delivered:**

- **`AttackProfile` and `AbilityDefinition` are deleted**, converged into `MoveDefinition`
  (amends DECISIONS D8; its intent survives — combat reads a neutral resolved *Move*, never an
  equipment type). Weapons author the moves they grant; instance mass adjusts them once, split
  by packet share.
- **Movesets compose** weapon-first, then Species · Base · Prefix · Suffix, every grant with
  provenance, `replaces` reported never silent. Species grant Bare Fists, so no one is moveless.
  Runtime grants (`grantMove`), instant triggers (`triggerMove`, at chain depth+1, nesting
  refused at load) and duration-limited modification (`modifyMove`) are live effects.
- **Move modification** is 11 declarative ops matched by tag or move id, applied in a **fixed
  order** proved source-order-independent — *"Heavy Strike gains additional Heat damage"* and
  *"an item grants a Move"* are data, never `if item == ThunderSword`. `convert` always states a
  fraction (D-01); `addTag` is the composition lever.
- **Enemies use the same system**: a moveset plus weighted AI rules over the shared condition
  vocabulary — intent chosen by rule, timing resolved by the tick engine. The old uniform
  random draw is gone; an actor with no profile behaves uniformly, so old content ports clean.
- **Combat Moves and Profession Actions share components, not a base class** — `ActionTiming`
  and `ActionCost` (a gauge name is a legal cost) live in the shared `Dungeons.Actions`
  vocabulary. Professions adopt them in E6.
- **The Mnemonic loop closes**: `status.recalled_move` stores the executing move's id; Recall
  replays it instantly through `recallMove`, bounded by its cooldown.

**M2′ is BUILT — the library and its acquisition:**

- **27 moves ship** (the 9 E4 moves + 16 library moves + 2 enemy-flavoured universals), soft-
  gated only: costs, `equippedTag`, cooldowns — never class (D25). Coverage: all four damage
  types, six aspects, ten statuses exercised, interrupt and heal handlers, a gauge-cost
  exemplar (Crash spends 40 Momentum), and one arcane-lane "always lands" spell.
- **Technique items** (`technique.*`, 19 shipped) teach moves into a **persisted learned list**
  (save v5, learn-order-preserving, once-per-move — a duplicate refuses without consuming).
  Learned moves join moveset composition as their own grant source with `learned` provenance.
  Loot/vendor faucets arrive with M6; a debug grant button is the interim source.
- **One vocabulary extension:** `EffectSpec` takes an optional per-effect `target` override
  (rule payloads and move riders alike) — Drain lands decay on the enemy while its heal rider
  names `TriggerSource`. Lifesteal shapes are authorable everywhere now.
- Bases keep **0–1 starting moves** as kit (Wizard's Fireball, Bastion's Shield Bash — both
  also exist as findable grimoires for everyone else).

Move-count growth from here is ordinary content work; E5's affix pools author the move
*modifiers*.

---

# 7. Professions

Persistent, Melvor-inspired progression. Trainable in the Hideout and, where appropriate, inside
Realms — where time passing carries real risk.

## 7.1 Designed roster (19)

**Gathering** — Mining · Forestry · Fishing · Herblore · Farming
**Crafting** — Smithing · Alchemy · Cooking · Enchanting · Fletching · Tailoring · Medicine
**Utility** — Beast Lore · Sleight of Hand · Agility · Campcraft · Wayfinding · Devotion ·
Summoning

**Built: 8** — Mining, Forestry, Fishing, Herblore, Smithing, Alchemy, Cooking, Beast Lore —
**26 actions** against the real material library (the P1–P3 pass hit §7.1's recommended slice
target exactly). Mining landed first and the startup iron-ore seed is deleted; a test pins that
some action produces `material.iron_ore` so it can never quietly return. Cooking/Alchemy
outputs that didn't exist were authored as **prepared materials** (`form:meal`/`form:tincture`,
carrying `growth` — the recovery property that will gate them as healing consumables later).
Intervals/XP are relative placeholders for the balance pass; a test asserts the professions
cross-feed in ≥ 4 chains.

## 7.2 Interconnection is the point

Professions must not exist in an isolated fake economy:

```
Forestry → Oak Bark
Herblore → understands its properties
Smithing → infuses iron with treated bark
        → Barkbound Iron
```

Mining feeds the ore ecosystem; Forestry feeds biome flora; Beast Lore feeds creature anatomy;
Smithing and Alchemy feed the emergent crafting engine.

## 7.3 Mastery

Per-*action* mastery, 1–99, granting interval reduction, increased yield, reduced costs, rare
material chance and improved active interactions.

> **Documentation vs implementation:** mastery is tracked and increments, but **nothing reads
> it**. It is currently a number that goes up and does nothing.

## 7.4 Offline progress — designed, not built

```
CompletedActions = floor(ElapsedTime / EffectiveInterval)
```
subject to resource, inventory and action caps, aggregated rather than tick-replayed.

---

# 8. Gathering & Resources

## 8.1 The material library

**474 materials** on a 0–100 property scale, authored biome-by-biome as a design lens —
temperate forest, arctic, volcanic, desert, jungle, swamp, mountain, cavern, necrotic, coastal.
There is deliberately **no biome field**; biome was a brainstorming device for producing varied
property profiles, not a system.

Deliberately **mundane-majority** (oak, iron, salt, spring water) so the rare materials (Storm
Core, Glacial Heart, Mana Prism) stand out by their property profiles rather than by tier.

**Never MMO tiering.** Oak / Ironwood / Emberwood / Frostpine / Bogwillow all fill the "wood"
role and behave differently. There is no "Iron < Better Iron".

Creatures and plants yield **multiple parts** as separate materials where the parts differ
meaningfully — a wolf gives hide, fur, meat, bone, fang and blood.

## 8.2 Properties (21)

| Role | Properties | Behaviour in crafting |
|---|---|---|
| **Structural** | hardness, mass, flexibility, affinity, conductivity, insulation, solubility, resonance, instability | Blend slowly toward a mass-weighted mixture. Define *what the material is* |
| **Reactive** | heat, cold, charge, toxicity, growth, decay, corrosion, arcane | Transfer along a process channel; subject to opposition. Define *what it does to things* |
| **Response** | heat_resistance, cold_resistance, toxin_resistance | Derived, never a reaction input |
| **Sourcing** | harvest_resistance | Inert in crafting; read only by gathering |

Key distinctions that must stay intact: `heat`/`cold`/`charge` (influence introduced) vs
resistances (influence resisted) · `charge` (energy) vs `conductivity` (transmission) ·
`toxicity` (attacks life) vs `corrosion` (attacks material) · `affinity` (willingness to bond) vs
`instability` (unpredictability).

**Affinity is the single most important gate in the crafting engine.**

## 8.3 Tags

Namespaced `family:value`, derived on emergent materials rather than inherited wholesale:

`origin:` (flora/fauna/fungal/mineral/elemental/arcane/synthetic) · `comp:` (organic/inorganic) ·
`form:` (metal/wood/liquid/crystal/powder/…) · `state:` (raw/refined/processed/alloy/extract/
distillate/composite/spent) · `rarity:` (common → exceptional) · `class:` (magical/fuel/venomous/
edible/monster) · `part:` (mote/core/gland/horn/sap)

**Rarity means availability, not power.** A rare material is unusual for its property
*combination*, not for having every stat at 90.

---

# 9. Emergent Crafting

The signature system, and the most complete one in the game.

## 9.1 The core claim

**The engine is a total function, not a lookup.** Every combination of substrate, reagents and
process produces a result — always — computed by a universal algebra. There is no recipe table
and no per-combination rule anywhere. Authored content is **seven processes**, a byproduct table
and a name grammar.

## 9.2 Structure of a craft

```
Substrate (the thing being transformed)
  ← Step 1: Reagent A   ⇒ intermediate state
  ← Step 2: Reagent B   ⇒ intermediate state
  ← Step 3: Reagent C   ⇒ final state
  + optional Catalyst (not consumed; modifies rates, transfers nothing)
  + a Process (decides which properties react at all)
```

**Order matters automatically**, because step 2 acts on a different intermediate state. Six
outcomes from three reagents, with zero authored triples — and it's *reasonable*: the player can
inspect the intermediate and predict the next step.

## 9.3 Processes

A process declares its **channel** (which of 21 properties participate), its **medium** (what
governs how readily a reagent releases what it carries), its **severity** (how violent), role
weights, gates and tag effects.

| Process | Profession | Medium | Severity | Opens |
|---|---|---|---|---|
| Grind | *ungated* | mechanical | 0.30 | solubility, mass, hardness |
| Steep | Herblore 1 | solvent | 0.20 | heat, toxicity, growth, cold, decay |
| Distill | Herblore 12 | solvent | 0.50 | toxicity, arcane, decay, corrosion, solubility |
| Smelt | Smithing 1 | thermal | 0.60 | hardness, mass, conductivity, heat |
| Quench | Smithing 5 | thermal | 0.35 | cold, hardness, flexibility |
| Alloy | Smithing 10 | thermal | 0.45 | hardness, mass, conductivity, flexibility, affinity |
| Forge Infusion | Smithing 15 | thermal | 0.55 | heat, charge, hardness, affinity |

*(An 8th, `Attune`, raises resonance for the essence layer and is deferred.)*

**Media** explain why an ingredient suits a process: solvent releases by `solubility`, thermal by
`instability`, mechanical by inverse `hardness`, arcane by `resonance`. This is why Ember Sap is
an alchemy reagent and Ember Core is a forge reagent.

## 9.4 The algebra

Per reagent step:

1. **Acceptance / release** — how willingly the substrate takes anything on (`affinity`), how
   readily the reagent gives it up (the medium's property).
2. **Channel convergence** — properties move a *fraction of the remaining gap* toward the
   reagent's value. **They never add, and can never exceed the strongest input.** This single
   rule kills unbounded stat escalation permanently, without caps or fudge factors.
3. **Off-channel handling** — structural properties blend slightly toward a mass-weighted
   mixture; reactive properties **dilute toward zero and receive nothing**. This is what stops
   deep materials carrying twenty-five nonzero properties: each transformation *focuses* the
   material and washes out the rest.
4. **Opposition** — opposed pairs (heat/cold, growth/decay) mutually annihilate, releasing
   strain. **You cannot stockpile opposites.**
5. **Floor pruning** — trace values are pruned to zero.

## 9.5 Meta fields

**Potency (1–100)** — how strongly the material expresses when used. A **weighted mean, never a
sum**, so adding a junk input *lowers* it. Capped at `best input + 8 × skill`. Consequence: *a
high-potency mundane material beats a low-potency exotic one*, so base resources stay relevant
forever.

**Integrity (0–100)** — a **transformation budget**, not durability. Cost scales with how violent
the change was, so gentle, well-chosen steps cost little and brute force costs a lot. **Elegant
crafting is mechanically rewarded** — this is the main skill axis of the system.

**Integrity 0 destroys the material.** This is only fair because three things are guaranteed:
the craft UI shows the projected cost and result **before** commitment; below ~25 integrity it
shows a destruction *chance* rather than a false certainty; and destruction yields
**byproducts** (Slag, Cinders, Dross, Residue by form tag) that are useful reagents in their own
right. A blown craft is a setback and a consolation prize, never a zero.

The tension is the point: *push one more transmutation, or commit and forge it now?* — the same
shape as the extract-or-go-deeper decision.

**Generation** — a depth counter for naming and valuation. Not a gate; integrity is the gate.

## 9.6 Identity

A crafted result is quantized, hashed into a canonical **signature**, and registered as a
**stackable runtime material**. Identical results **stack**.

> **This reverses an earlier decision.** `docs/itemization.md` originally made any material whose
> properties diverged from its definition a unique per-unit instance. Under that rule, forty
> units of the same alloy were forty objects and the inventory, save file and UI all break.
> **`ItemInstance` is now equipment-only.**

Consequences: two players who reach the same state get the same material with the same name, so
discovery is shareable · saves stay small · emergent materials flow through *every* existing path
(inventory, lookups, crafting inputs, loot) with no special-casing.

**Variance produces different materials, not random stats.** A bad roll gives you a *different,
weaker material with its own name* — possibly one nobody has seen — rather than "Emberveined Iron
(bad roll)". High skill narrows variance to zero; low skill scatters you across neighbouring
buckets and you find things by accident.

## 9.7 Naming

A pure function of final state, never of history — history-based names grow without bound.
Hard constraints: **maximum 3 words**, at most one intensity adjective, **no "of X"
constructions**, **no tier words** (Greater/Superior/Lesser), and **numbers never appear**.

Intensity comes from vocabulary ladders, not adjectives-of-adjectives:
`heat → Warmed · Emberlit · Cindered · Searing`.

Real output: *Emberlit Iron · Warmed Iron Tincture · Chilled Iron · Tainted Oak Tincture ·
Quickened Sageleaf Tincture · Lightning-Veined Copper · Hardened Granite Dust · Lunith Iron*
(the last being the collision fallback — a stable syllable coinage, never a number).

## 9.8 The Reaction Log

Required scope, not polish: *"a system this deep is only playable if it explains itself."* Every
craft emits a structured, human-readable trace that is simultaneously the tutorial, the debugger
and future codex content. Every line states **why**:

```
Forge Infusion — Iron Ingot ← Ember Core
  Acceptance 0.48 — Iron Ingot resists bonding (affinity 30)
  Release 0.93 — Ember Core gives freely under thermal (instability 90)
  hardness        65 → 62  (channel, rate 0.25)
  heat             0 → 36  (channel, rate 0.80)
  conductivity    55 → 53  (structural blend)
  Integrity 90 → 87  (cost 2.6: Δstate 0.50 × severity 0.55)
  Potency 39, 65 → 49
✦ First discovery: Emberlit Iron ×1
```

## 9.9 Crafting layers — P2 traits and P3 essence **BUILT (C1)**; P4/P6 planned

| Layer | Description | Status |
|---|---|---|
| **P2 — Traits** | Named, discrete, capped qualitative states (Emberveined, Bound Opposition). Cap 3, weakest displaced, authored merge rules let pairs **supersede** into stronger traits. Every trait carries a drawback | Planned |
| **P3 — Essence** | A rare *supernatural* layer (fire/frost/storm/nature/necrotic/radiant/abyssal) distinct from mundane reactive properties. Capacity governed by `resonance`; excess becomes **strain**, not a cap — *powerful magic needs a worthy vessel* | Planned |
| **P4 — Signature reactions** | 30–80 authored spikes matched against *abstract conditions*, never item ids. Plus ~10–20 **chain signatures** matching an ordered sequence | Planned |
| **P5 — Fabrication** | Materials → equipment and consumables | Planned |
| **P6 — Codex & Assay** | Discovery journal, known-rules journal, proximity hints, player renaming | Planned |

**`arcane` is a property, never an essence** — it is not an element, it is the medium elements
travel through. It gates magical effects, amplifies essence expression, loads against resonance,
and types otherwise-untyped damage.

---

# 10. Items & Equipment

## 10.1 Two-tier model

- **Definitions** — what a kind of item *is*. Shared, never mutated.
- **Instances** — a specific owned item. **Equipment only.** Materials always stack.

## 10.2 Current equipment — thin

Two slots (Weapon, Armor), four hand-authored items (Rusty Sword, Iron Sword, Tattered Armor,
Iron Armor). **Since E4 a weapon is its moves**: it authors the moves it grants (the Iron Sword
grants Iron Slash and Heavy Strike), and re-equipping reconfigures the moveset — the Fighter's
identity, working today. Material properties still barely reach combat: only mass →
damage/speed (applied to the move's packets) and hardness → armour.

## 10.2a Modifiers, genetics and the casino — **front half BUILT (R4b/R4c, 2026-08-16)**

> **BUILT:** the Genome (stat-map-weighted pressure, persisted — save v6), `AffixDefinition`
> with the three genetic levers + potency as roll-quality, §4 rolling on the seeded source,
> **innates as a deterministic affix class** (never rerollable, U-7), 43 representative
> affixes across offence/character/defence/resource/ailment/retaliation/avoidance/
> penetration/trigger/status/move-mod families, equip-lifecycle grants (scoped contributions
> + attached rules + move-modifier grants), the §8 validator rules, seeded distribution
> tests, the pre-roll genome translation on the fabrication preview, and a debug reroll.
> **STILL PLANNED:** operations + Overreach + Anomalous (E7), Exotic rare-roll, Signature
> affixes (P4), the full 150–250 catalog, the §2.3 full Genome Readout panel with Advanced
> pressures (the semantic supports-line ships; the numeric panel is the roll inspector).

> **Settled by D-21 – D-23.** Full specification in `docs/affixes.md` and
> `docs/profession-tools.md`. Slices E5 (modifiers/affixes), E6 (tools), E7 (operations +
> Overreach), after C1/C2 supply traits, essence and fabrication. The effect vocabulary these
> pools express themselves in — statuses, scoped modifiers, move modification, proc limits — is
> **BUILT**, which is the whole reason the pools were sequenced last (D-19).

A fabricated item carries a **Genome** — stat-map-weighted property *pressure*, essence,
expressed and dormant traits, tags, potency, signatures. The genome decides three things about
every modifier: **eligibility** (can it roll), **weight** (how likely), and **tier ceiling** (how
strong). Potency decides where in the tier the value lands. All four are pure functions, and the
player sees them **before rolling** in a Genome Readout — because "engineer the casino" is a lie
if you gamble blind.

**Structure:** 1–3 **innates** computed from the genome and never rolled, + ≤3 modifier-prefixes
+ ≤3 modifier-suffixes from weighted tiered pools, + Exotic / Signature / Anomalous above.
The innate layer is what makes material invention *guarantee* a result rather than only shift a
distribution — a well-engineered item is never a total loss.

> **Item modifiers are not the Character Prefix/Suffix system.** In code they live in
> `Dungeons.Affixes` with ids `affix.*`; in player-facing text they are always called
> **modifiers**, never "prefixes". The bare word *Prefix* means only the character layer.

**Crafting operations** (Anneal · Etch · Scour · Reforge · Bind · Temper · Fracture) are paid for
with materials the game already produces — chiefly the **destruction byproducts** of failed
crafts. Every operation respects the genome, so the gambling is bounded by the engineering.

**Overreach** is the final casino: Ruin · Brick · Mutation · Elevation · Exotic Mutation ·
Transcendence, drawn **only from the item's own genetic families** — a poison dagger can never
Overreach into a lightning effect, at any odds. It is **repeatable with escalating Ruin odds**,
which makes it the fourth verse of the risk rhyme in §13.2: *push once more, or stop?*
**Anomalous modifiers exist only here** — the only content permitted to bend the proc-recursion
rules, so the safety valve is also the top-end reward.

**Profession tools are equipment.** Two worn slots (`Tool.Gathering`, `Tool.Crafting`), fabricated
from the same forms system with the same genome, affixes, operations and Overreach. A Bogwillow
Log is a bad sword and an excellent rod shaft — same library, different stat map, opposite
verdict, zero tool-specific content.

## 10.3 Form templates + fabrication — **BUILT (C2a+C2b)**; consumable forms (P5c) planned

The intended model authors **forms**, never material variants:

```
Equipment Form  +  Material(s)  →  Equipment Instance
```

So `Sword` is authored once; Iron Sword, Emberveined Iron Sword and Necrotic Storm Sword are
never authored at all.

**Multi-component from v1.** A form declares named slots (edge / core / binding) each with
required tags, a mass share, and an **aperture** governing how much of each trait category that
slot can express. **Stats read from named slots, never from a blend** — a hard brittle edge on a
flexible core is a genuinely different weapon from the reverse, and the system computes that
without any authored combination.

A **stat map** is what makes the same material excellent in one form and useless in another: a
robe reads flexibility and insulation, plate reads hardness and mass, a staff reads resonance and
arcane. This is what stops a single "best material" existing.

Proposed property → gameplay mappings (**for review, not final**):

| Property | Weapon | Armour |
|---|---|---|
| Hardness | armour penetration, edge retention | mitigation; brittle if flexibility is low |
| Mass | damage and stagger up; interval and stamina cost up | mitigation up, dodge down |
| Flexibility | good for bows; bad for rigid weapons | good for light armour |
| Conductivity | boon for charge-channelling gear | liability vs lightning |
| Resonance | the caster stat; gates essence expression | — |
| Instability | higher output variance | — |

Slots should expand to roughly Weapon / Offhand / Head / Body / Hands / Feet / Trinket, with
~6–8 forms — enough that material choice visibly matters.

**Unexpressed trait magnitude becomes *dormant*** — shown in the tooltip, counted in value, and
fully available if the material is used in a different form later. Dormant traits make one
material interesting in several directions rather than optimal in one.

> ⚠ **Known blocker.** Material properties are 0–100; equipment base properties are ~0–5 and
> drive current combat tuning. Reconciling them is a **combat rebalance**, not a mapping change,
> and must be budgeted as its own piece of work.

**Durability** — the design assumes it; the game has none. **Recommended deferred** — the
extraction loop already supplies the risk pressure.

## 10.4 Consumables

One exists (Healing Salve). Planned as **the same fabrication system with an effect map** rather
than new machinery: slots become base / active / stabiliser, output stacks like a material.
Consumables are the natural home for *negative* emergent outcomes — a botched draught that heals
*and* corrodes is the cheapest source of memorable results in the whole design.

---

# 11. Realms & Exploration

## 11.1 Structure

Realms are **spatial location graphs**, closer to For The King 2 than to Slay the Spire node
selection. Movement is adjacency-gated; **Depth** measures progression within a run; **Tiers**
are escalating versions of a realm.

## 11.2 The one built realm

**The Dark Forest** — themes of fey, toxins, giant trees, bogs, predators and hidden crossings.
Ten locations across two depths: entrance, travel, two gathering nodes, two combat nodes, an
event node, a descent, and an extraction portal.

Designed but unbuilt realms: Tiered Deserts · Tundra · Wastelands (→ Ashlands → Volcano) ·
Garden Maze · City of Infinite Alleys.

**Deliberate direction: make the Dark Forest much richer before adding a second realm.** Once one
realm is genuinely good, adding realms is a content problem instead of repeatedly rebuilding
unfinished systems.

## 11.3 Location types

Built: Entrance · Travel · Gather · Combat · Event · Descent · Extraction.
Designed, unbuilt: Camp · Shrine · Merchant · Elite · Boss · Hidden · Hazard.

## 11.4 Realm Knowledge

Persistent per realm, earned through exploration, encounters, resources, events, extraction and
bosses.

**Knowledge unlocks information and options, not raw damage** — reveal enemy resistances,
identify likely hazards, show resource-rich areas, reveal extraction routes, discover hidden
locations, unlock portal targeting.

> Currently a bare counter that unlocks nothing.

## 11.5 Affixes — **Not built**

Realm modifiers affecting both danger and opportunity: Undead Infested · Volatile · Toxic Bloom ·
Treasure Rich · Eternal Night · Predator's Domain · Shattered Paths · Arcane Storm.

## 11.6 Campsite — **Not built**

Limited, strategically valuable field sanctuary: cook, recover, repair, craft emergency supplies,
prepare ammunition. Modified by the Campcraft profession.

## 11.7 Preparation — **Not built**

The portal screen should communicate known information and let the player choose equipment, food,
consumables, ammunition and tools. **Knowledgeable preparation should materially improve
survival** — this is a core intended reward for Realm Knowledge and profession investment.

---

# 12. Enemies & Encounters

## 12.1 Current state — the framework is BUILT (M2′c); the roster is three

**Enemy identity composes from reusable layers** — the Enemy Framework (D26):

```
Family (physiology)  +  Role (combat archetype)  +  Actor (identity + overrides)
```

- **`family.*`** (`enemy_families/`) — what a creature IS: baseline attributes, resource
  silhouette, biological resistances (lanes), Resolve. Never behaviour.
- **`role.*`** (`enemy_roles/`) — what a creature DOES, as **deltas over any family**: attribute
  and resource tweaks, armour, the armoured-physique vulnerability pair, a default AI brain.
  `role.brute` is one definition whether the body is goblin, undead or construct.
- **`ai.*`** (`ai_profiles/`) — named reusable brains: weighted rules over the shared condition
  vocabulary, matching moves **by id or by tag** (`moveTag: "mech:stagger"` = "the big hit,
  whatever it is on this body"), plus `avoid_repeat_weight`. AI chooses intent only.
- **`ActorResolver`** folds family → role → actor with one merge rule set; a future
  Elite/Realm/depth **variant is one more delta through the same fold**, never a duplicated
  definition.

**Three shipped enemies, all pure data (~8 lines each):** the Raider (skirmisher — pressure,
Expose Weakness openers, punish-while-vulnerable), the Brute (armoured at last — `FromActor`
had hardcoded armour to 0 — heavy telegraphs, Overhead Crush as the stagger threat, Brace when
hurt), and the **Hexer** (caster — venom/wither/dart entirely from the universal move library;
the framework proof). Validation covers refs, layer conflicts, unusable moves, tag rules
matching nothing, and D-02 vulnerability ranges.

The Brute still matters disproportionately: its telegraphed heavies are the thing proving the
core combat idea — *"I see something dangerous coming. What do I do before it lands?"*

## 12.2 Remaining enemy breadth — PLANNED

Loot tables · harvestable resources · biome/depth availability · the elite/boss **variant
layer** (the fold seam exists, unbuilt) · roster growth per §12.3. AI may consider target,
health, cooldowns, statuses and player state today; threat, position and Realm modifiers arrive
with their systems. Unique bosses may break the composable rule where necessary.

## 12.3 Target roster for the slice

**8–10 enemies in the Dark Forest**, selected so each **exercises a distinct combat mechanic**
rather than for variety's own sake: melee pressure, ranged pressure, heavy telegraphs, rapid
attacks, blocking, dodging, statuses, spellcasting, interrupts, armour, resistances, unusual
movement, group behaviour. Roughly five normal archetypes, two specials, 1–2 elites, one boss.

## 12.4 Ecology

**A creature must not merely drop "Enemy Loot".** Anatomy maps into the real material library —
hide, bone, gland, venom — so Beast Lore harvesting feeds crafting, which feeds equipment, which
feeds the next run.

---

# 13. Death, Risk & Reward

## 13.1 The rules

- Death **ends the run**.
- **Unsecured Realm loot is lost** — materials, generated materials, drops.
- **Equipped gear is safe** (default). A "gear at risk" difficulty toggle is designed but off.
- **Persistent progression always survives**: professions, Realm Knowledge, discoveries, unlocks,
  the Stash.
- A **starter loadout** is always available, so a fresh or broke character can never be bricked.

Death should remove run-specific gains without deleting hundreds of hours.

## 13.2 The three risk decisions

The game deliberately repeats one decision shape at three scales:

| Scale | Decision |
|---|---|
| **Realm** | Extract now, or go deeper? |
| **Crafting** | Refine once more, or commit this material to a form before it's destroyed? |
| **Combat** | Spend the resource now, or hold it for the telegraph you can see coming? |

That rhyme is intentional and should be preserved.

## 13.3 Extraction

Extraction opportunities should be **valuable decisions, not ubiquitous escape buttons**. Some
mechanics deliberately reward refusing extraction (the "Of No Return" pattern).

---

# 14. The Hideout

The persistent home base. Currently implicit — it is where crafting and profession training
happen, with no dedicated screen or systems.

Intended (all **Needs Design**): Hideout upgrades · crafting stations · Farming as a Hideout-based
renewable resource profession · storage management · the portal/preparation screen.

---

# 15. Economy

**Largely undesigned.** What exists is a barter-of-materials economy: gather → process → craft →
use.

Missing and needed:

- **Currency** — none exists. Respec is priced in "Realm currency" that has not been designed.
- **Merchants / vendors** — a designed location type, unbuilt.
- **Loot tables** — currently one guaranteed drop per enemy; no rarity, weighting or conditions.
- **Item valuation** — emergent items have no author to price them, so value must be *computed*
  from potency, trait rarity, essence, generation and integrity.
- **Resource sinks** — Melvor's purchasable-upgrade axis is the obvious candidate and is
  unconsidered so far.

---

# 16. UI & Game Flow

## 16.1 Current state

One code-built developer console with a persistent header (tick/sim status, Play/Advance/Save/
Load), a tabbed body, and an always-visible event log. Dark, code-only theme; no art or audio.

**Tabs:** Character · Char Lab · Equipment · Hideout · Realm · Combat · Inventory.

The **Hideout** tab replaced the Professions and Crafting tabs. Profession training, the
material-transformation bench and equipment assembly are all reached the same way now — *choose a
station, then use what that station is for* — over one fixed activity strip (passive bar, active
timing sweep, Discover → Pursue card). Twenty stations, one per profession; see
`docs/game-overview.md` §14.

> ⚠ **Unverified in the editor:** several Core-complete surfaces have never been rendered —
> the Character Lab "Live hooks" panel, the Hit Log toggle, the gauge readout, and the E4
> moveset readout / `CombatUseMove` command. All presentation-only; a single visual pass in
> Godot covers them.

> ⚠ **Being re-voiced (D30, slices R0–R4):** the bench, projection, fabrication and item
> surfaces below currently speak simulation numbers as their primary language. The corrected
> presentation architecture — the three-languages rule, the hybrid semantic grammar, the item
> reveal hierarchy — is specified in `docs/presentation-architecture.md` and supersedes the
> raw-number presentation described here wherever they conflict.

Two are genuinely designed rather than debug scaffolding:

**The Crafting Bench** — process picker (showing medium, severity, gate and the channel it
opens), base picker, an **ordered reagent chain with reordering controls**, optional catalyst,
and a live **pre-commit projection** (expected result, potency, integrity after, cost ± spread,
destruction warning or percentage). The chain is deliberately literal — "Step 1: Ember Sap,
Step 2: Stormglass" — because order is the mechanic and it has to be visible.

**The Character Lab** — three pickers, a **live diff panel**, and a full readout (engine,
weakness, growth per level with a level-21 projection, gauges, every attached hook with its
origin, the active suffix expression's drawback). Swapping any one component immediately reports
what changed.

## 16.2 Planned developer labs

Testing tools are treated as real deliverables, not overhead — the combination count makes
balancing impossible without them. Planned: Item Lab · Crafting Lab (property override sandbox,
A→B vs B→A comparison, lineage walker) · Equipment Lab · Profession Lab · Combat Lab · Enemy Lab
· Move Viewer.

## 16.3 Production UI — **Not started**

No art, audio, animation or telegraph visuals. The path from one debug page to real screens is
unplanned.

---

# 17. Design Principles Worth Preserving

These recur across systems and have each been argued for explicitly:

1. **Total functions over lookup tables.** Every input produces a result. Authored content is
   spikes on top of a universal rule, never the rule itself.
2. **Data owns content; code owns structure and closed vocabularies.** Property names, materials,
   processes, prefixes and suffixes are data. Damage types, item types and roles are code.
3. **Determinism.** Given the same inputs and seed, the same outcome. Randomness is confined to a
   seeded source and to as few points as possible — in crafting, exactly two.
4. **Every power has a cost.** Traits consume properties and carry drawbacks; suffix expressions
   state their risk; potency is a mean so junk dilutes it.
5. **Legibility is required scope.** The Reaction Log and the pre-commit projection are not
   polish — a system that silently eats eight hours of refinement is one players stop
   experimenting with.
6. **Fail loudly at load.** Bad content references break at startup, never mid-play.
7. **Never author a combination.** If a feature needs N × M hand-written entries, the design is
   wrong.
8. **Move requirements are physical and conditional, never identity checks (D25).** Equipped
   tags, costs and statuses gate moves; attributes, resources and modifiers do the specializing.
   A class-check condition kind may never be added to the rule vocabulary.
9. **Three languages, one direction (D30).** Simulation (0–100 values, rates, weights) →
   player crafting language (icon + qualitative state + intensity + direction + context) →
   gameplay payoff (damage, Crit, Thorns, Shock…). Raw simulation values never lead a normal
   play surface; the semantic layer is a one-way, unit-tested read-model in Core; a
   player-facing modifier ships only when its mechanic resolves. Complexity belongs
   underneath; clarity belongs in the player's hands. `docs/presentation-architecture.md`.

---

# 18. Major Unresolved Design Questions

| # | Question | Why it matters |
|---|---|---|
| 1 | **Quantization bucket size** for emergent material identity | *The single highest-risk tuning number in the design.* Too coarse collapses the space; too fine floods the registry with indistinguishable neighbours. Measured at 67% collapse over 2,800 crafts — provisional, needs play |
| 2 | **Integrity budget strength** | Currently allows ~20–40 meaningful refinements. Looser than the "commit-or-lose" fantasy implies, because the expensive cost terms are traits and signature reactions, which don't exist yet. Accept and wait, or tighten now? |
| 3 | **The 0–100 vs 0–5 scale mismatch** | Blocks materials driving equipment. Resolving it is a combat rebalance — **scheduled into slice C2**, deliberately *after* the damage pipeline is settled so it calibrates against the final pipeline rather than a placeholder |
| 4 | **Combat triangle** — melee/ranged/magic advantage? | Still open, but **leaning no**: enemy vulnerability multipliers (§5.5) already provide the counter-play without a global rule |
| 5 | **Positioning** — in or out? | Deferred. If added later it touches every move and every enemy. Root is deferred with it; Fear gains retreat behaviour if it lands |
| 6 | **Durability** | Recommended deferred; the design assumes it exists |
| 7 | **Species mechanical role** | The least-developed identity layer |
| 8 | ~~**Movesets**~~ | ✅ **Answered — D-18.** Architecture settled; `AttackProfile`/`AbilityDefinition` converge into `MoveDefinition` in slice E4. See §6 |
| 9 | **Currency and resource sinks** | Nothing designed; respec pricing depends on it |
| 10 | **Integrity is excluded from material identity** | An archetype keeps the integrity of its *first* discovery, so reaching the same state by a cheaper path inherits the wrong budget. Judged self-balancing in practice; filed, not fixed |
| 11 | **Remaining 40 suffix mechanics** | ~120 expressions once the model is validated |
| 12 | **Suffix Guard expressions skew defensive** | ✅ **Largely answered — D-06.** The Guard channel now has six distinct events to hook (`HitLanded`, `Blocked`, `Parried`, `HitAvoided`, `DamageMitigated`, `BarrierBroken`) instead of only "a block landed", so the six block-triggered expressions can diversify without widening the channel itself |
| 13 | **`transferable` property flag is unused** | Structural properties are marked non-transferable, yet processes move them on-channel. Give it a job or drop it |
| 14 | **Response properties drop on transformation** | Iron's authored heat resistance of 60 becomes a derived ~14 after any craft. Arguably the more honest number, but it's a visible discontinuity |
| 15 | **Fighter's identity hook** | Its engine — "moveset comes from the weapon" — was universalized for everyone in E4. Fighter needs a new hook that is not a license (D25). **NEEDS DESIGN — deliberately deferred out of M2′** (user call, 2026-08-16): the library is authored without a Fighter kit; candidates on the table were technique breadth and swap fluency (the latter needs a live re-resolve seam in `CombatEncounter`) |
| 16 | **Casting-speed attribute scaling** | A low-INT caster today is weak and mana-poor but not *slow* — "slowly and inefficiently" is currently only "weakly and expensively". **Deferred to the balance pass** (user call, 2026-08-16) alongside the recorded Fireball/Bastion findings: M2′ spells author plain windups; costs and damage scaling gate for now |

---

# 19. Current State Summary

## 19.1 Firmly designed

Decided, argued through, and stable enough to build against.

- **The five pillars** and the guiding question
- **Emergent crafting**: the total-function algebra, convergence as the anti-inflation rule,
  off-channel dilution, opposition, potency-as-weighted-mean, integrity-as-transformation-budget,
  destruction-with-byproducts, signature identity and stacking, state-based naming, the Reaction
  Log as required scope
- **Character identity**: Base + Prefix + Suffix; the fixed growth budget; engines over flavour;
  Strike/Guard/Surge channels; "a Prefix may never name a Base"; one suffix expression per
  channel; formatting separated from mechanics
- **Combat direction**: tick-based, not turn-based; the six-stage lifecycle; active play earning
  its advantage through decisions; auto-combat sharing the same rules
- **Risk model**: unsecured loot, gear safe by default, persistent progression survives, starter
  loadout guarantee, extract-or-deeper as the defining question
- **Progression philosophy**: multiple independent tracks, horizontal over vertical, Melvor as the
  architectural reference, active vs passive as a standing rule
- **Respec**: costly but accessible

## 19.2 Exists in-game — BUILT

Built, tested, and runnable today. 654 passing tests, zero build warnings.

| System | What's real |
|---|---|
| **Crafting traits + essence (C1)** | 16-trait library (birth/cap/displacement/supersession, `id:tier` identity), seven typed essences with anchors/opposition, resonance capacity → strain → instability, Attune |
| **Fabrication (C2a+C2b)** | Form templates (3-slot Longsword, Buckler, Vest), aperture-gated trait expression with dormancy, derived equipment archetypes persisted, 0–100 → combat-unit reconciliation pinned by iron-sword parity, per-slot component UI |
| **The semantic layer (R0–R3, D30)** | `Dungeons.Presentation`: tiers/pips/wear words, trends from typed change kinds, risk bands, slot-fit readings, material readings, the typed projection lines, item cards/strips — the only path from simulation state to player-facing text; raw values behind Advanced. Bench, preview, fabrication and reveal all speak it |
| **Affixes + Genome (R4, E5 front half)** | Genome persisted (save v6), eligibility/weight/tier + potency roll-quality, deterministic innates, seeded rolling, 43 affixes with live grants (contributions/rules/move-mods), lane-key alignment (D-07 executed), thorns/parry/evade/avoidance/penetration/barrier/status-depth mechanics, §8 validation + distribution tests |

| System | What's real |
|---|---|
| **Tick simulation** | Deterministic shared clock driving combat and passive gathering |
| **Materials** | 474 definitions, 21 typed properties, namespaced tags, load-time validation |
| **Emergent crafting** | The complete P1 engine: 7 processes, the full algebra, potency, integrity, destruction, byproducts, signature registry, naming, Reaction Log, pre-commit projection |
| **Crafting bench UI** | Process/substrate/ordered-reagent/catalyst selection with live projection |
| **Character identity** | 15 Bases, 25 Prefixes, 50 Suffixes (10 fully expressed), 9 name formats, growth budget, gauges, channels — 18,750 resolvable builds, **hooks live in combat** |
| **Character Lab UI** | Component swapping with a live diff |
| **Modifier system** | 51 data-defined keys, five stacking kinds (incl. diminishing), clamps and `danger` caps as data, **scoped contributions** (local-vs-global, per-profession, per-move-tag) with the wrong-context guard, and a combat read path assembling build + status + gauge-band + timed contributions |
| **Events & rules** | 31 events; declarative trigger rules — 17 conditions (incl. stateful world reads), 16 effects, one-roll-`effects[]`, target selectors, cooldowns, seeded chance; **full proc safety** (chain ids, depth 2, once-per-chain, ICD, `CanTrigger`, 64-effect fuse) |
| **Effect handlers** | 11 combat handlers — damage, area, heal, status, resource, modifier, interrupt, and the four move-granting kinds; unhandled kinds visibly recorded |
| **The hit pipeline** | Packets × lanes, traced Hit Log, diminishing armour, resistances, enemy vulnerability, crit, Perfect Block, modifier-driven INCREASED/block/damage-taken stages |
| **Statuses** | 28 data-driven definitions across all four categories; DoTs, timed modifiers combat reads, **Resolve** (buildup, shared immunity, escalation), stagger→Stun |
| **Moves** | `MoveDefinition` for both sides; weapon-granted movesets with provenance; 11-op move modification in fixed order; `grantMove`/`triggerMove`/`modifyMove`/`recallMove`; the Mnemonic loop; **27 shipped moves** (M2′) with per-effect target overrides |
| **Techniques** | 19 technique items teaching moves into a persisted, learn-order-preserving learned list (save v5); learned grants compose with `learned` provenance; Learn UI + validator rules |
| **Enemy framework** | Family + Role + Actor composition via `ActorResolver` (D26); reusable AI brains matching by move id or tag, `avoid_repeat_weight`; enemy armour real; 3 data-composed goblins incl. the caster Hexer |
| **Combat** | Tick-driven encounter, telegraph → windup → execute → recovery as real states, timed block/dodge, queue-time costs/cooldowns/requirements, consumable use, death |
| **Professions** | **8 professions, 26 actions** (P1–P3), active + passive, XP/levels, level-gated ladders, cross-feeding pinned by test; Mining killed the iron-ore seed |
| **Realm** | Dark Forest — 10 locations, 2 depths, travel, descend, extract, forfeit-on-death |
| **Persistence** | Single-slot save (v4) covering build, stash, instances, equipment, professions, knowledge, discoveries, emergent archetypes |

## 19.3 PLANNED — designed, not built

Direction and specifics are settled (mostly by the 27 effect-foundation decisions); the build
has not reached them.

- **Item modifiers, genetics, operations, Overreach** (§10.2a) — designed in full; slices
  E5/E7. The effect vocabulary they express themselves in is already built
- ~~Equipment fabrication~~ ✅ **BUILT (C2a+C2b)** — form templates with named slots, per-slot
  apertures, dormancy, derived equipment archetypes persisted, and the **scale reconciliation
  done**: stat maps land material properties on instances in combat units, pinned by an
  iron-sword parity test. Awaiting the C2c playtest checkpoint for tuning
- ~~Crafting P2–P3~~ ✅ **BUILT (C1)** — the 16-trait library (cap/displacement/supersession)
  and the seven-essence layer with resonance capacity/strain and Attune
- **Profession tools** (§10.2a) — two worn slots, same genome/affix machinery; slice E6, cheap
  once E5 exists
- **Crafting P4 + P5c + P6** — signature reactions, consumable forms, and the codex remain
  specified and deferred
- **Realm depth** — location types, affixes, campsite, preparation and Knowledge-unlocks are
  designed and none are built
- **Hazards** — the tick/telegraph model is designed; nothing places one
- **Auto-combat** — the profile shape it runs on now exists (enemies use it); pointing it at
  the player does not
- **Offline progress** — a formula with no implementation

## 19.3a NEEDS CONTENT — the engine is ahead of the data

Built machinery waiting on authored content, not on code.

- ~~The universal move library + acquisition~~ ✅ **BUILT (M2′)** — 27 moves, 19 technique
  items, the learned list. Library growth from here is ordinary content authoring
- **Enemy roster breadth** — the framework (D26) and three data-composed goblins exist; the
  §12.3 target is 8–10 enemies plus the elite/boss variant layer (the fold seam exists, unbuilt)
- **Technique-item faucets** — techniques are debug-granted until M6's loot tables; migrate the
  Wizard/Bastion starting-grant exemplars to acquisition sources then too if desired (D25 allows
  either)
- **Move modifiers** — the 11-op system is live and empty; E5's affix pools are its intended
  author
- **Remaining 40 suffix mechanics** (~120 expressions) and the Species roster (3 thin of 10)

## 19.4 NEEDS DESIGN — little or no design exists

- **Economy** — no currency, no vendors, no loot tables, no valuation rules, no sinks. Respec
  pricing has no economy to price against
- **The Hideout** — upgrades, stations, storage, the portal screen
- **Positioning** — deferred without a decision on whether it's ever coming
- **Character level and XP** — attribute *growth weights* exist, but nothing awards character XP
  or levels; the build is currently hardcoded and cycled by a debug button
- **Production UI** — no art, audio, animation or telegraph visuals; no plan from debug console
  to real screens
- **Multiplayer** — noted as a long-term possibility the domain architecture supports; entirely
  undesigned

---

## Appendix: Superseded documents

| Document | Status |
|---|---|
| `docs/crafting.md` §17 | **Superseded** by `docs/emergent-item-system.md` |
| `docs/itemization.md` D6 (materials as instances) | **Reversed** — materials stack; instances are equipment-only. Header notes what survives |
| `docs/classes.md` original roster | **Replaced** — rewritten for the current roster |
| ~~`docs/current-state.md`~~ | **DELETED** (D-24a) — stale, superseded by `PROJECT_STATE.md` |
| ~~`docs/combat-spec.md`~~ | **DELETED** (D-24a) — §15–16 and §22–25 superseded by `damage-and-defense.md`/`statuses.md`; the action-lifecycle detail folded into `moves.md` §2.3; the rest duplicated §5 above |
| `docs/expansion-plan.md` | **Largely superseded** — its P2 shipped (D23); P3–P10 replaced by the E0–E7 slice plan. Kept for its audit |
| Fixed crafting interactions | **Retired** — only a Healing Salve shim remains until fabrication lands |

## Appendix: the effect-foundation package

The design settled by the 27 decisions in `docs/effect-foundation.md` §12. **Slices E0–E4 are
BUILT** (events → hit pipeline → lifecycle split + statuses → rule engine + scoped modifiers +
handlers + stateful conditions → moves); **C1/C2 and E5–E7 remain** (traits/essence,
fabrication + scale reconciliation, affixes, tools, Overreach).

| Doc | Covers |
|---|---|
| **`effect-foundation.md`** | **Entry point** — audit, architecture, triggers/conditions/effects, modifiers & stacking, proc safety, tags, slice plan, the decision log |
| `damage-and-defense.md` | Packets, lanes, the resolution pipeline, defence layers, resistance/penetration/inversion, thorns |
| `statuses.md` | The status taxonomy, the 14-status roster, Resolve and crowd control, the data contract |
| `moves.md` | The Move model, move modification, the shared Action vocabulary |
| `affixes.md` | Material Genetics → eligibility/weight/tier, crafting operations, Overreach |
| `profession-tools.md` | The yield pipeline, tools as fabricated equipment |
| `effect-catalog.md` | 254 starter modifier concepts |
| `worked-examples.md` | 10 builds, 4 tools, 8 resolution traces |
