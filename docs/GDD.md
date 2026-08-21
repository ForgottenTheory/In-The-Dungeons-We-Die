# In The Dungeons We Die — Game Design Document

> **Consolidated GDD.** Written against the project as it actually stands — last full sync
> **2026-08-19**, after the M6 loop-closers, Phases 6–10 (the finished Dark Forest, Realm
> preparation, the progression pass, offline + automation) and the D29.3 resolution;
> **1,191 passing tests, 0 build warnings, save schema v11**. Where older documents conflict
> with newer decisions, the newer decision is recorded here and the older one is marked
> superseded.
>
> ⚠ **The crafting redesign is COMPLETE (2026-08-21, DECISIONS D42–D54).** The material /
> property / trait / essence crafting model this GDD's crafting sections describe was replaced
> by the **identity system** and deleted whole in migration Phase 7. Those sections are
> history, not the game — design of record: `docs/identity-foundation.md`; the shipped stack:
> `docs/crafting-overview.md`. Do not design against the superseded sections.
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
HIDEOUT                                                          [ALL BUILT]
  ├─ Train professions (active, passive, or offline — the selection is standing)
  ├─ Gather and process resources
  ├─ Experiment in the crafting bench → discover an emergent material
  ├─ Fabricate that material into equipment → modifiers roll from its Genome
  ├─ Choose Base + Prefix + Suffix (Char Lab; Species is fixed pending §3.9)
  └─ Prepare the run: loadout, packed supplies, knowledge-redacted briefing
        ↓
REALM RUN                                                        [ALL BUILT]
  ├─ Enter (at a deeper door, once Knowledge earns it) → explore the graph
  ├─ Gather biome resources (time passes; risk accrues)
  ├─ Fight with your moveset on the clock — incl. an elite and a boss
  ├─ Creature anatomy drops as crafting inputs (composed loot tables)
  └─ Reach a depth checkpoint
        ↓
   ┌─ EXTRACT ──→ loot (and gold) secured to Stash
   └─ GO DEEPER ─→ rarer rewards, escalating danger
        ↓
   (death forfeits unsecured loot; progression survives)
        ↓
BACK TO HIDEOUT — better materials, better professions, another stupid experiment
```

**The loop is closed and runs end to end today**, both halves of it: the attended half
(prepare → enter → fight → extract → transform → fabricate → roll → equip → go again) and the
idle half (a standing passive selection that keeps working offline, waits out material shortages,
and reports what happened when you return). The links that are still thin are **profession tools**
(E6 — the trades work, worn tools don't exist) and **form acquisition** (D29.2 — schematic items
drop and bind to nothing yet).

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
is not a license. **NEEDS DESIGN** (§18 #15) — deliberately deferred out of M2′ (user call,
2026-08-16); the move library ships without a Fighter kit.

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
| **Gold** | Banked coin persists | **Coin in the run bag is lost** — it lives on the inventory (save v8) and obeys the extraction model like everything else |

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
| Mastery XP / levels | **Built** (Phase 8, D40) — points → level 1–99, deliberately linear |
| Mastery Pool + checkpoint rewards | Needs Design |
| Interval reduction | **Built** — `mastery.interval`, data-driven |
| Resource preservation | **Built** — `mastery.preservation`, unlocks at mastery 20 |
| Doubling / increased yield | **Built** — `mastery.doubling`, unlocks at mastery 40 |
| Level-based unlocks | **Built** — actions gate on profession level |
| Mastery-based unlocks | **Built** — `required_mastery` on an opportunity; below it the offer is not rolled at all |
| Equipment affecting skills (tools) | Planned (E6) — the one Melvor layer still missing |
| Cross-skill bonuses | **Built** (Phase 10, D41) — `synergies/`, 15 rows: 13 cross-profession (each following an existing material chain) + the 2 global rows below |
| Global/account passives | **Built** (Phase 10, D41) — a synergy with no source reads **total** profession level |
| Offline progression | **Built** (P4) — with auto-repeat and a return summary added in Phase 10 |
| Progression milestones | Needs Design |

> **The ladder is content**, in `game/data/mastery/`: six `ProfessionBenefitKind` rungs, each with an
> unlock level, a per-level rate and a cap. A balance pass is a JSON edit. **None of it is
> balanced** — the numbers are the placeholders they were before they moved.

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
> stored retaliation; the full `capped / raw` character sheet (the preparation screen shipped in Phase 7 with the minimal armour reading).

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

**Resistance order:** sum → exposure → cap (75%) → **inversion** → penetration (max 0.5, applied
**after** the cap) → floor (−100%). Overcapping absorbs debuffs but not penetration, so
resistances display as `capped / raw`. *(The designed rare max-res raise to 90% exists only as a
declared constant today — `MaxResistanceCeiling` is read by nothing until its affixes ship;
inversion and exposure content are E7.)*

**Defence layers:** evasion (timed) · block (timed, mitigation) · **perfect block** (tight window,
avoidance, refunds Guard) · **parry** (gear-granted — a form must declare it — avoidance plus a
counter-window) · **armour** (`armour/(armour + K·packet)` — diminishing, strong against
attrition and weak against spikes while resistance is the reverse; the shipped constant is
**K = 1.0**, and the 5× variant quoted in `docs/damage-and-defense.md` is a recorded tuning
intent awaiting the balance pass) · resistance · lane avoidance (rare, hard-capped) ·
**Barrier** · damage-taken modifiers · **Resolve** (controls).

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
modifier key itself, so no combination of haste sources can bypass it. The D-20 decision is
applied: the shipped registry floors `combat.interval.mult` at **0.55**
(windup/recovery/telegraph multipliers floor at 0.25 each). **One honesty note:** combat
consumes only `combat.windup.mult` today — moves author their own timings, and the
interval/telegraph/recovery keys are declared with their clamps but not yet read by any
pipeline stage. The clamps exist so the first consumer inherits the rule instead of
re-deciding it.

## 5.7 Auto-combat — **BUILT (Phase 10, D41)**

**Auto-combat uses the same rules** — automation chooses actions, the domain resolves them
normally. There is deliberately no separate "fake" combat calculator, so passive and active never
become two unrelated balance models.

**And it is literally the player driven by the same profile shape.** `AutoCombatPilot.Engage` puts
an authored brain's rules onto the player `Combatant.Ai` and asks `CombatEncounter.ChooseMoveFor`
— the method every enemy has always used. It then presses the same buttons a hand would:
`UseMove`, `Block`, `Dodge`. Three brains ship (Steady, Aggressive, Cautious), and their rules
match moves by **tag**, because a player's moveset comes from their weapon.

**Its whole disadvantage is reaction latency (D-07), never a damage penalty.** A hand `R` ticks
behind the eye must commit a stance `R` ticks before impact, and every tight window is measured
from when the stance went up — so at `R = 8` it blocks and dodges reliably and can never land a
Perfect Block or a Parry. An attack arriving sooner than `2R` after it appears cannot be answered
at all. `docs/damage-and-defense.md` §5.1.1 has the table.

**Live only.** It runs inside the real encounter on the real tick engine. Fully unattended Realm
runs — travel, extraction decisions, the run inventory — are a separate problem and are not built.

## 5.8 Positioning — deferred

Production combat is intended to support tactical positioning (a small per-side grid) affecting
melee range, ranged distance, area effects, hazards, protection and targeting. **Explicitly
deferred**; the current model is single-position. Deferring is a conscious choice — adding it
later multiplies the design space of every move and every enemy.

## 5.9 Statuses — **BUILT (E2–E4)** · Hazards — **PARTIAL**

> **Settled by D-08 – D-10.** Full specification in `docs/statuses.md`.

Hazards exist today as **Realm hazard nodes** (Phase 6): crossing one costs health once, on
arrival — dangerous ground, not an action — and the Hazards knowledge insight is what lets a
player see them before standing in them. The richer designed form — **ticking, telegraphed
hazards inside combat** (poison clouds, fire, falling debris, freezing pulses) — remains
**PLANNED**; nothing places one in an encounter yet.

**Statuses are fully data-driven** — one definition type, no bespoke class per ailment — in four
categories whose rules differ:

| Category | Ships today |
|---|---|
| **Ailments** (damage over time) — 5 | Bleed · Poison · Burn · Toxin · Latched |
| **Impairments** (debuff, no damage) — 6 | Chill · Shock · Corroded · Weaken · Dissonance · Illuminated |
| **Controls** (prevent or redirect action) — 4 | Stun · Freeze · Fear · Silence |
| **States** (tactical markers) — 14 | Vulnerable · Guarded · Barrier · Recalled Move · Phased · Rooted Growth · Planted Charge · Feint Ready · Filed Intent · Liability (×2) · Fault · Spreading · Stoneskin |

Six lanes map to a signature ailment at the encounter seam (physical → Bleed, heat → Burn,
toxin → Poison, cold → Chill, charge → Shock, corrosion → Corroded); magic and decay
deliberately have none.

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

**All of the above is BUILT**: **29 status definitions** across the four categories (the 14
core, the previously-dangling authored set — `status.recalled_move` went live with the Move
system — and later library additions), DoT ticks that cannot proc anything (proc-safety rule 4),
`while_active` modifiers that combat actually reads (Chill genuinely slows windups; Corroded
genuinely strips armour), Resolve with shared buildup, control immunity and per-encounter
escalation, and stagger folding into Stun buildup exactly as designed. The old remainder is
closed: **ailment application chances have their source** — R4b's affixes grant them (a rolled
"12% chance to Poison" is a modifier on gear), and moves may carry them as effect riders — so
Bleed/Burn/Poison fire from ordinary hits whenever the build or the weapon says so.

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

**M2′ is BUILT — the library and its acquisition — and M6 grew it. The 2026-08-19 spell-library
expansion grew it 12×:**

- **517 moves ship** (the 9 E4 moves, the M2′ library, M6's weapon-archetype moves, and the
  474-spell library expansion — see `docs/spell-library.md` for the full manifest and the
  parked-names backlog), soft-gated only: costs, `equippedTag`, cooldowns — never class (D25).
  Coverage: all four damage types, all seven aspects, controls through Resolve buildup, heals,
  lifesteal riders, resource pacts (health as a cost), attribute/resist/tempo buffs and curses,
  weapon imbues via `modifyMove`, and a `grantMove` conjuration. A validator rule rejects any
  form granting a move it cannot fire; `SpellLibraryTests` proves each mechanic family resolves.
- **Technique items** (`technique.*`, 493 shipped) teach moves into a **persisted learned list**
  (save v5, learn-order-preserving, once-per-move — a duplicate refuses without consuming).
  Learned moves join moveset composition as their own grant source with `learned` provenance.
  **Loot faucets are live (M6) for the original 19** — they drop from the shared martial/arcane
  technique tables (killing the Hexer is how a martial build finds a spell). **The 474 expansion
  techniques are deliberately in no loot table yet** (user decision, 2026-08-19): distribution
  lands with the balance pass; the debug grant button is their only faucet today.
- **One vocabulary extension:** `EffectSpec` takes an optional per-effect `target` override
  (rule payloads and move riders alike) — Drain lands decay on the enemy while its heal rider
  names `TriggerSource`. Lifesteal shapes are authorable everywhere now.
- Bases keep **0–1 starting moves** as kit (Wizard's Fireball, Bastion's Shield Bash — both
  also exist as findable grimoires for everyone else).

Move-count growth from here is ordinary content work; E5's affix pools author the move
*modifiers*.

---

# 7. Professions — **BUILT (all 20)**

Persistent, Melvor-inspired progression. Trainable at the Hideout's twenty stations and, where
appropriate, inside Realms — where time passing carries real risk. Full system doc:
`docs/professions.md`.

## 7.1 The shipped roster (20) — **BUILT (P4)**

| Category | Professions |
|---|---|
| **Gathering (7)** | Mining · Forestry · Fishing · Farming · Hunting · Beast Lore · Salvaging |
| **Processing (9)** | Smithing · Herblore · Alchemy · Cooking · Leatherworking · Tailoring · Fletching · Artifice · Runecrafting |
| **Utility (4)** | Thieving · Agility · Cartography · Assay |

**348 actions · 36 nested opportunities · 12 training obstacles**, one execute path behind all
twenty (active and passive literally call the same method), success chance below 1.0 only for
Hunting and Thieving, and everything gated by profession level on the ladder.

> **Superseded:** the original 19-name design roster (Enchanting, Medicine, Sleight of Hand,
> Campcraft, Wayfinding, Devotion, Summoning). The P4 pass replaced it — Sleight of Hand became
> **Thieving**, Wayfinding became **Cartography**, Enchanting's territory went to **Runecrafting**
> and **Artifice**, and Campcraft/Devotion/Summoning/Medicine did not survive. The intent
> (interconnection, active/passive, mastery) carried over intact.

Every interval and XP value is a relative placeholder for the balance pass — breadth, not
balance, by standing decision.

## 7.2 Interconnection is the point — enforced by test

Professions must not exist in an isolated fake economy:

```
Forestry → Oak Bark
Herblore → understands its properties
Smithing → infuses iron with treated bark
        → Barkbound Iron
```

`ProfessionEcosystemTests` makes the ecosystem a fence rather than an intent: every Processing
profession consumes another profession's output; **no profession is a dead end** (Cooking is the
one named exception, exempt until consumable forms land); Hunting produces carcasses and **only
Beast Lore opens them**; only Cartography teaches Realm Knowledge; every plantable seed has a
wild source; every opportunity out-pays its own action.

```
Mining → Smithing → ingots → Artifice → lenses → Assay reads deeper
Hunting → carcass → Beast Lore → hide → Leatherworking → fabrication
Cartography → survey chart → Salvaging finds the ruin worth digging
Assay → property dossier → the three deepest crafting actions require one
```

## 7.3 Mastery — **BUILT** (Phase 8, D40)

Per-*action* mastery, 0–99, granting interval reduction, output doubling, input preservation,
rare-find chance and better active interactions — plus **offers a novice never sees**.

**Mastery level is completions**, one per landed attempt, to a ceiling of 99. The curve is
deliberately linear: bending it would have repriced every action in the game while claiming to be
an integration pass, and the balance backlog is parked. It changes in one method.

Six benefits, all authored in `game/data/mastery/` and all consumed:

| Rung | Unlocks at | What it does |
|---|---|---|
| Interval reduction | 1 | The work goes quicker |
| Bonus-output chance | 1 | You turn up the uncommon finds more often |
| **Input preservation** | 20 | Sometimes the materials survive the work |
| **Output doubling** | 40 | Sometimes the work yields twice |
| Opportunity chance | 1 | You notice what a stranger to this work would walk past |
| Opportunity risk | 1 | You know which chances are worth taking |

Preservation and doubling carry an unlock level on purpose: they **start happening** rather than
creeping up from zero, and both are announced in the log when they fire.

**The mastery-gated opportunity is the action-specific unlock.** Five of the game's highest-risk
offers carry `required_mastery` — Thieving's Strongbox (25) and Reliquary (40), Hunting's
storm-charged bull (30), and two of Mining's deep finds (30). Below the gate they are **not
rolled at all**: "a novice cannot find this" is a fact about the code, not a very small
probability.

> **Still unbalanced.** Every magnitude is the placeholder it was before it became content.

## 7.4 Offline progress — **BUILT** (P4; auto-repeat + the return summary in Phase 10, D41)

Offline is a first-class path, not a courtesy, and it **cannot drift from live play by
construction**: `OfflineProgressCalculator` loops the same `ProfessionSystem.Execute` the
attended game calls, at performance 0, re-reading the effective interval every completion (so
mastery earned while away shortens the rest of the absence). Caps: **12 hours** of offline time
and 20,000 completions per absence, both stated honestly in the return summary when they bite.

- **The selection is standing (Phase 10).** Running out of materials makes the passive runner
  *wait* and resume by itself when inputs reappear; only Stop clears it. Temporary problems
  wait; permanent ones refuse.
- **The return is a read-model.** `AwayProgress` aggregates one absence — completions, crops
  lifted, items merged per id, XP, mastery, levels gained — and the presentation layer owns
  every word of the summary panel, in the player's units (items and hours, never ticks).
- **Autosave on quit**, guarded: it refuses when no save exists yet and inside a Realm, because
  offline time is measured from the save stamp.

## 7.5 The active layer — Discover → Pursue / Ignore — **BUILT** (P4, reshaped by D29.3)

Active play's structural advantage is the **opportunity**: an active attempt may surface an
offer (a rich vein, a shape under the boat, an unattended satchel, an unmarked side path) that
passive play *never rolls for at all*. Pursuing costs real time on the shared tick engine and
can be lost to risk; declining costs nothing; mastery raises the odds and talks the risk down.
**36 opportunities ship**, four of them gated by `required_mastery` — below the gate the offer
is not rolled, so "a novice cannot find this" is a fact about the code.

> **D29.3 (settled — do not reopen): profession essence is active-only.** Essence reaches a
> profession **only as an opportunity payload**. No action output, bonus output or profession
> drop table carries it — a structural fence, not an allowlist — so essence cannot be banked
> while idle. Essence is extraction's export; the Realms remain its faucet.

## 7.6 Synergies and global bonuses — **BUILT** (Phase 10, D41)

One benefit seam, three sources. `ProfessionBenefits` folds the mastery ladder and the synergy
table into the single question the execute path asks — *what is this benefit worth, right now,
for this action?* — so cross-profession and account-wide bonuses landed with **zero change** to
the execution code, and E6's worn tools become a third field on the same seam.

- **13 cross-profession rows**, each following a material chain the professions already have
  (Smithing helps Mining, Beast Lore helps Hunting…). Source and target must differ — validated.
- **2 global rows** that read the player's **total** profession level — the account-passive
  layer.
- Synergies **sum** (each capped individually); a profession-scoped mastery rung *replaces* the
  general one. Same three-field formula as the mastery ladder (unlock level, per-level rate,
  cap), same content file shape, deliberately.

## 7.7 The professions that are a system, not a list — **BUILT**

- **Farming** — the only profession that runs in parallel with itself: up to **six plots**
  (unlocked at Farming 1/5/15/30/50/70) growing on the world clock, so crops finish while the
  game is closed. Planting pays the seed; harvest is prepaid, so it can never charge twice.
- **Agility** — a five-slot **training course** (12 obstacles ship). The fitted configuration is
  a standing loadout of Realm utility bonuses (`course.*` keys: travel speed, gathering speed,
  extraction speed, hazard avoidance, opportunity safety) — declared, aggregated and displayed,
  but **consumed by nothing until E6**. Honest scaffolding, shown as readiness rather than a
  number that changes nothing.
- **Assay** — the legibility skill. A material reading is computed identically at every level;
  levelling only removes `???`: identity at 1, composition at 10, reactive behaviour at 25,
  traits at 45, essence at 65, potential at 85. **Redaction, never power.** Assay dossiers gate
  the three deepest crafting actions.
- **Cartography** — the one profession that teaches **Realm Knowledge** (its actions carry
  `realmKnowledgeGain`). All of its gains point at the Dark Forest today, because it is the only
  realm with content behind the map.

---

# 8. Gathering & Resources

## 8.1 The material library

**1,448 materials** on a 0–100 property scale — the original hand-authored core (~559 by M6)
grown by the D35 content expansion (582 plants, 307 ores and gems, plus creature anatomy,
salvage, reagents, runes, knowledge items and prepared goods), authored biome-by-biome as a
design lens — temperate forest, arctic, volcanic, desert, jungle, swamp, mountain, cavern,
necrotic, coastal. There is deliberately **no biome field**; biome was a brainstorming device
for producing varied property profiles, not a system. The anti-tiering rule is encoded in the
generator that produced the expansion and asserted by test, and **every raw material has a
gathering source** (D36 — `EveryRawMaterialHasASource` is exact: 348 gathering actions plus
anatomy on the enemy family tables cover all of it).

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
and no per-combination rule anywhere. Authored content is **eight processes**, a byproduct table
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
| Attune | Alchemy 10 | arcane | 0.35 | resonance, arcane — the vessel-raiser for the essence layer (**BUILT**, C1) |

> ⚠ **Temporary (2026-08-17):** Distill and Attune are **ungated in the shipped content** so the
> Alchemy Lab and the Runic Altar can be playtested — their designed gates above named a
> profession neither station trains. The override is marked in `processes.json` and pinned by
> `CraftingActionContentTests.OnlyGrindIsUngated`, which goes red the moment the exception list
> stops matching the content.

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

## 9.9 Crafting layers — P2/P3/P5 **BUILT**; P4 planned; P6 partial

| Layer | Description | Status |
|---|---|---|
| **P2 — Traits** | Named, discrete, capped qualitative states (Emberveined, Bound Opposition). Cap 3, weakest displaced, authored merge rules let pairs **supersede** into stronger traits. Every trait carries a drawback, consumes properties and charges integrity | **BUILT (C1)** — 16 traits ship |
| **P3 — Essence** | A rare *supernatural* layer (fire/frost/storm/nature/necrotic/radiant/abyssal) distinct from mundane reactive properties. Capacity governed by `resonance`; excess becomes **strain**, not a cap — *powerful magic needs a worthy vessel* | **BUILT (C1)** — seven essences; Attune is live; strain feeds effective instability |
| **P4 — Signature reactions** | 30–80 authored spikes matched against *abstract conditions*, never item ids. Plus ~10–20 **chain signatures** matching an ordered sequence | Planned |
| **P5 — Fabrication** | Materials → equipment (consumable forms are the P5c remainder) | **BUILT (C2a/C2b + M6)** — see §10.3; consumable forms Planned |
| **P6 — Codex & Assay** | Discovery journal, known-rules journal, proximity hints, player renaming | **PARTIAL** — the Assay reveal ladder ships as a profession (§7.7) and trait-proximity hints ship at the bench; journal, codex and renaming remain Planned |

**`arcane` is a property, never an essence** — it is not an element, it is the medium elements
travel through. It gates magical effects, amplifies essence expression, loads against resonance,
and types otherwise-untyped damage.

---

# 10. Items & Equipment

## 10.1 Two-tier model

- **Definitions** — what a kind of item *is*. Shared, never mutated.
- **Instances** — a specific owned item. **Equipment only.** Materials always stack.

## 10.2 Current equipment — **BUILT** (D32/D33: nine slots, worn loadout, rings)

**Nine worn slots** — Weapon · Offhand · Head · Body · Hands · Feet · Trinket · Ring I ·
Ring II — with the two ring positions interchangeable (one Ring form fills either; D33). The
`Armor` → `Body` slot rename was the project's first save migration (v9).

Four hand-authored pieces remain (Rusty Sword, Iron Sword, Tattered Armor, Iron Armor) as the
starter kit and the calibration references; everything else the player wears is **fabricated**
(§10.3) — a derived equipment definition persisted in the save, minted as an `ItemInstance`
with a Genome and rolled modifiers.

**Since E4 a weapon is its moves**: it authors the moves it grants (the Iron Sword grants Iron
Slash and Heavy Strike), and re-equipping reconfigures the moveset — working today. Worn
mitigation is the **sum of the loadout**: each armour-bearing piece contributes
hardness-derived armour (through the resolver) plus the per-lane resistances fabrication baked
into it from its response properties, with coverage authored in the form's stat map rather than
coded per slot.

The property → combat seam (`EquipmentResolver`) still reads only **mass → damage/windup** and
**hardness → armour** directly off an instance; fabrication lands its richer results (per-lane
resistances, derived stats) on the definition it mints, so combat consumes them without the
seam widening. Richer direct mappings remain the E-track intent.

## 10.2a Modifiers, genetics and the casino — **front half BUILT (R4b/R4c, 2026-08-16)**

> **BUILT:** the Genome (stat-map-weighted pressure, persisted — save v6), `AffixDefinition`
> with the three genetic levers + potency as roll-quality, §4 rolling on the seeded source,
> **innates as a deterministic affix class** (never rerollable, U-7), **44 representative
> affixes** across offence/character/defence/resource/ailment/retaliation/avoidance/
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

**Shipped: 23 forms across all nine slots (M6, D32–D34), sixteen of them weapons** — each form
existing to exercise a part of the material system no other form reads (the Longbow reads
flexibility where the Longsword reads hardness; the Maul wants mass the Dagger refuses; the Ring
is the only reader of conductivity/affinity, the Focus the only reader of resonance/arcane).
~180 weapon names ride ten weapon forms as `name_variants` — picked deterministically from the
item's signature, cosmetic by construction (nothing reads a variant, so it can never quietly
become a mechanical difference). A validator rule rejects a form granting a move it cannot fire.

**Unexpressed trait magnitude becomes *dormant*** — shown in the tooltip, counted in value, and
fully available if the material is used in a different form later. Dormant traits make one
material interesting in several directions rather than optimal in one.

> ✅ **The old scale blocker is resolved (C2b).** Material properties are 0–100; combat runs in
> its own units; the reconciliation happens in fabrication and **only** there
> (`EquipmentAssemblyTuning.CombatUnitScale`), pinned by an iron-sword parity test so the
> calibration cannot drift silently. Whether the resulting numbers *feel* right is part of the
> parked balance pass.

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
are escalating versions of a realm. *(Honesty note: tiers are carried in data and on the run
today and read by nothing — no loot entry or system gates on tier yet.)*

## 11.2 The realms that ship — one finished, 163 walkable shells

**The Dark Forest is the finished reference realm** (Phase 6, D37) — themes of fey, toxins,
giant trees, bogs, predators and hidden crossings. **34 locations across three depths**
(12 / 13 / 9), carrying every node kind the architecture has: entrances at each depth (the
deep-entry doors), travel, eight gathering workings, five combat nodes (including an **elite**,
Grask the Warlord, and the first **boss**, Thornheart the Old Growth — a plant-family boss in a
goblin realm, so everything learned on the way down is the wrong lesson), events, descents,
four extractions, two camps, a shrine, the Hedge Trader (a merchant), three hazards, and three
**hidden** nodes that do not exist for a party which has not learned the routes. Deeper pays
**rarer, not merely more** — average drop rarity 1.75 at depth 3 against 0.78 at depth 1, with
a test asserting the direction. The first balance pass (D38) made it coherent against a 59 HP
fresh character; **feel is still unplayed**.

**The other 163 realms are the roster** (D35): a name, a biome tag set, a tier band and a
walkable two-depth graph (entrance → fork → descent, a way out at each depth). They deliberately
carry **no combat or gather nodes** — wiring encounters is a later pass. The old named realm
list (Tiered Deserts, Tundra, Wastelands, Garden Maze, City of Infinite Alleys…) lives on inside
this roster as shells. What ships is the map, not the ambush.

**Deliberate direction: make the Dark Forest much richer before adding a second realm.** Once one
realm is genuinely good, adding realms is a content problem instead of repeatedly rebuilding
unfinished systems.

## 11.3 Location types — all eleven **BUILT**

Entrance · Travel · Gather · Combat · Event · Descent · Extraction · **Camp** (rest — restores a
fraction, once) · **Shrine** (a narrated blessing) · **Merchant** (spends gold from the run bag)
· **Hazard** (crossing costs health once, paid on arrival — dangerous ground, not an action).
*Hidden* is a flag any node may carry, revealed by the Hidden Routes insight. *Elite* and *Boss*
are **enemy ranks** (identity tags on the actor, D26), not location types — a combat node simply
hosts one.

## 11.4 Realm Knowledge

Persistent per realm, earned through exploration, encounters, resources, events, extraction and
bosses.

**Knowledge unlocks information and options, not raw damage.** ✅ **All seven items on this list
now ship** (D37, D39, D40), as a seven-rung ladder read both inside a run and — since Phase 7 —
on the preparation screen before one:

| Rung | Buys |
|---|---|
| Common resources | What this place is made of, walked out of its own loot tables |
| Enemy weaknesses | Resistances, exposed lanes and vulnerable damage types |
| Hazards | The dangerous ground, seen instead of stood in |
| Rich nodes | Which workings are worth the run-time |
| Hidden routes | The caches, shortcuts and side routes that were always in the graph |
| Extraction routes | Where the ways out are from where you stand |
| **Deep entry** | **Portal targeting: begin an expedition at a deeper door** |

Deep entry is the only rung that hands over an *option* rather than a fact, and it is last
because you cannot aim at a door you have not found. It is priced honestly: starting at depth 2
skips depth 1's fights, its loot **and** the knowledge they would have paid.

## 11.5 Affixes — **Not built**

Realm modifiers affecting both danger and opportunity: Undead Infested · Volatile · Toxic Bloom ·
Treasure Rich · Eternal Night · Predator's Domain · Shattered Paths · Arcane Storm.

## 11.6 Campsite — **PARTIAL**

**Camp nodes ship** (§11.3): a place in the graph that restores a fraction of the party once per
run. The richer designed sanctuary — cook, repair, craft emergency supplies, prepare ammunition
in the field — is not built, and its sponsoring profession (**Campcraft**) did not survive the
P4 roster replacement, so the full concept would need a new home if it returns. **The rest
mechanic exists; the field workshop does not.**

## 11.7 Preparation — **BUILT** (Phase 7, D39)

The portal screen communicates known information and lets the player choose their equipment and
supplies before committing. **Knowledgeable preparation materially improves survival** — the core
intended reward for Realm Knowledge and profession investment.

What ships: the loadout (all nine slots, equipped through the normal equip path — there is no
second copy of worn gear), a **pack** of consumables that transfers into the run at entry and is
unsecured from that moment, field readiness for the realm's gathering trades, and a
knowledge-redacted **briefing** — known threats with their weaknesses, hazards, rich workings,
routes and the insight ladder. Only what has been earned is shown.

Two things are deliberately absent. **Ammunition** has no system behind it. **Profession tools**
are E6 — tool slots, tool forms and the yield pipeline that would read them do not exist, so the
Tools panel shows profession readiness rather than a slot that changes nothing.

The screen never blocks entry on missing gear; a **starter kit** is one button away whenever the
player owns no weapon at all (§13.1).

---

# 12. Enemies & Encounters

## 12.1 Current state — framework **BUILT** (M2′c); roster **BUILT** (M6: 483 actors)

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

**The roster is complete (M6): 483 actors across 26 families and 7 roles, all pure data
(~8 lines each).** Every name in the design list ships; wave 2 needed no new families, which is
the layering paying for itself. Seven reusable AI brains (`ai.skirmisher/brute/caster/archer/
stalker/guardian/champion`) serve the whole roster by matching moves **by tag** — weighting
`delivery:ranged` is what makes "an archer must actually carry a ranged move" a load error
rather than a play-session discovery. Validation covers refs, layer conflicts, unusable moves,
tag rules matching nothing, and D-02 vulnerability ranges.

**Wired into play today: the Dark Forest's five encounters** — Raider (skirmisher pressure),
Brute (armoured, heavy telegraphs — still the proof of the core combat idea: *"I see something
dangerous coming. What do I do before it lands?"*), Hexer (caster, and the only Realm source of
arcane techniques), **Grask the Warlord (the first elite)** and **Thornheart the Old Growth
(the first boss)**. The other 478 actors are authored breadth waiting on realm encounter wiring
(§11.2) — resolvable, validated, fightable from the dev console, but placed nowhere.

## 12.2 The rank layer — **BUILT** ahead of its content

Elite and Boss are **identity tags on the actor** flowing through the same fold (D26), not
variant definitions. The rank tag joins the loot context, where `loot.shared.rank_spoils` —
nested by every family table — pays elite/boss spoils only when the tag is present. Grask and
Thornheart carry the tags today; authoring the next elite is one tag and one line of loot.

## 12.3 Roster targets — **superseded by M6**

The old slice target ("8–10 enemies, each exercising a distinct mechanic") is kept here for the
principle, which still governs *which* actors get wired into realm nodes: an encounter earns its
place by testing a distinct skill — melee pressure, ranged pressure, heavy telegraphs, statuses,
spellcasting, armour — never by variety alone. Unique bosses may break the composable rule where
necessary (none has needed to yet).

## 12.4 Ecology — **BUILT** (M6/D36)

**A creature does not merely drop "Enemy Loot".** Enemy loot composes family + role + actor
(§13), and the family half is anatomy mapped into the real material library — hide, bone, gland,
venom, blood. Hunting produces carcasses; **only Beast Lore opens them**; Leatherworking wants
the hides; fabrication wants what Leatherworking makes. The chain the section always promised is
now enforced by the ecosystem tests.

---

# 13. Death, Risk & Reward

## 13.1 The rules

- Death **ends the run**.
- **Unsecured Realm loot is lost** — materials, generated materials, drops, **and coin**.
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

## 13.4 Loot — **BUILT** (M6, D31). Full doc: `docs/loot.md`

**One data-driven table shape serves every payer in the game** — enemies, gathering nodes,
event chests, profession actions. **72 tables ship** over the existing material library.
Three drop rules as separate named lists (guaranteed drops · independent chance drops ·
weighted draws where `dropsNothing` is a real miss), quantity ranges, depth and context-tag
conditions, and nested shared tables instead of copied entries.

- **Enemy loot composes** exactly the way the enemy does (D26): a kill rolls its family's
  table (anatomy), its role's table (kit) and its actor's table (identity), merged into one
  haul — so a new creature becomes lootable in one line.
- **D28 — gear comes from the bench; realms drop inputs.** *Extraction converts risk into
  materials; fabrication converts materials into permanence.* A test fails any table yielding
  finished equipment. Relic materials are the designed chase (boss drops with impossible
  property profiles that feed the genome machinery rather than bypassing it); sealed authored
  uniques are the one fenced exception, and even they end at the bench (Fracture).
- **The active/passive seam is structural**: gathering tables gate their richer draw on the
  `active` context tag, so passive play cannot reach those entries at any rate.
- **Rarity is read from the material's own tag**, never restated on the entry — one source of
  truth, validated.
- **Elite/boss spoils fire on the rank tag** (§12.2). **Essence never appears in a profession
  drop table** (D29.3) and **no profession table pays coin** — both held by test.
- **Gold** drops from tables (save v8), lives on the inventory, and obeys the extraction model
  like everything else. Its only sink today is the Realm merchant (§15).

---

# 14. The Hideout — **BUILT** (stations); upgrades **NEEDS DESIGN**

The persistent home base is a real place now: **twenty stations, one per profession** — the
Forge, the Apothecary, the Alchemy Lab, the Kitchen, the Tannery, the Loom, the Fletcher's
Bench, the Workbench, the Runic Altar, the Assay Table, and one apiece for the gathering and
utility trades (Mineshaft, Timber Yard, Fishing Dock, Garden Plots, Hunting Lodge, Bone Table,
Salvage Yard, Thieves' Nook, Training Course, Cartographer's Desk).

```
Hideout → choose a station → train its ladder / transform at its bench / assemble at it
```

**A station is routing, never rules.** It says which profession ladders are trained there, which
crafting actions its bench offers and which blueprints it can assemble — and every one of those
resolves through the system that always owned it, under the same gate. Hosting decides *where
you stand*, never *whether you may*; an ungated action can have two homes (Grind: a mortar at
the Apothecary, a mill at the Workbench). Two reachability rules are enforced at load: every
profession is hosted by exactly one station, and every crafting action and blueprint is offered
somewhere — so new content cannot ship unreachable.

Farming's plots, Agility's course and Assay's reading table appear at their stations because of
*which profession is hosted*, never a flag. One fixed **activity strip** (the passive/waiting
status bar, the active timing sweep, the Discover → Pursue card, and the while-you-were-away
summary) sits above the station index, because only one activity is in flight at a time.

Still **NEEDS DESIGN**: Hideout upgrades · station unlock/upgrade costs · storage management.
(Farming as the renewable Hideout profession shipped in P4 — §7.7. The portal/preparation screen
is built — §11.7.)

---

# 15. Economy — faucet **BUILT**, sinks **NEEDS DESIGN**

What exists is a barter-of-materials economy (gather → process → craft → use) plus **gold**:
it drops from loot tables (save v8), accumulates on the inventory, is unsecured in a Realm like
everything else, and has exactly **one sink — the Realm merchant** (the Dark Forest's Hedge
Trader spends coin from the run bag). That ordering is deliberate: the loot pass's brief was
*"gold simply exists as the current currency; do not design the economy yet"* — a currency
nothing spends is harmless, while pricing things before knowing what drops is guesswork.
**Thieving deliberately yields no coin of its own** — it takes precious metal, gems, keys and
paperwork; coin is a Realm export, and a test holds the line alongside D29.3's essence rule.

Missing and needed:

- **Vendors beyond the one merchant** — stock design, pricing, restocking; the location type
  works, the economy behind it does not exist.
- **Item valuation** — emergent items have no author to price them, so value must be *computed*
  from potency, trait rarity, essence, generation and integrity.
- **Resource sinks** — Melvor's purchasable-upgrade axis (Hideout/station upgrades, §14) is the
  obvious candidate and is unconsidered so far.
- **Respec pricing** — designed as "costly but accessible, in Realm currency"; whether that
  currency is gold is undecided.

---

# 16. UI & Game Flow

## 16.1 Current state

One code-built developer console with a persistent header (tick/sim status, Play/Advance/Save/
Load), a tabbed body, and an always-visible event log beside it. Dark, code-only theme; no art
or audio.

**Tabs:** Character · Char Lab · Equipment · Hideout · Realm · Combat · Inventory.

- The **Hideout** tab is the station host: the while-you-were-away summary card, the fixed
  activity strip (passive/waiting status, active timing sweep, Discover → Pursue card), then a
  station index that swaps to one station's page — ladders, bench, assembly, plots, course or
  assay table as the station's own definition routes (§14).
- The **Realm** tab is **two screens that swap** (D39): the preparation panel out of a run
  (destination + deep-entry picker once earned, loadout with warnings and the starter kit,
  consumable packing, fieldwork readiness, the knowledge-redacted briefing, one large ENTER
  button), and the in-run panel inside one (report, one action button named for the node type,
  Go Deeper / Extract, travel buttons, and a combat row when a fight is on).
- The **Combat** tab shows live telegraph countdowns ("⚠ Goblin Brute: Overhead Smash — impact
  in 1.2s"), per-combatant HP/stamina with stance markers, move buttons with provenance
  tooltips, Attack / Block / Dodge / Parry (gear-gated) / Use Salve / Wait, a Hit-trace toggle,
  and the **auto-combat toggle with its brain picker** (Steady / Aggressive / Cautious) and an
  honest explanation of the reaction-latency handicap.
- The **Char Lab** is the build-selection surface today: Base / Prefix / Suffix pickers (plus
  Random) with a live *"What changed:"* diff — Species is not yet pickable (§3.9).

> ✅ **The D30 re-voicing landed (R0–R4).** Bench, projection, fabrication and item surfaces
> speak the semantic language by default (tiers, pips, trends, risk bands, slot-fit lines);
> the numeric voice sits behind the bench's single **Advanced** toggle, and Assay removes
> `???`s rather than adding numbers. Colour is the client's only contribution — the words come
> from `Dungeons.Presentation`.

> ⚠ **Unverified in the editor** (Godot is not on this machine's PATH; the user runs it): the
> Phase 10 surfaces — the away panel, the auto-combat row, the three-state passive label, the
> synergy "Helped by:" lines, autosave-on-quit — plus the older list: the whole Realm
> Preparation screen, mastery readouts, the deep-entry picker, the rebuilt Hideout tab, the
> R-track bench grammar and fabrication preview, the Techniques panel, the goblin fights.

> ⚠ **Known seams, filed:** the Combat tab's Parry button is evaluated once at startup (the
> in-run combat row re-checks correctly, so a fabricated Buckler enables Parry in a Realm but
> not on the Combat tab until restart); "Use Salve" is hard-wired to the Healing Salve rather
> than reading the pack; several dev faucets (Grant Test Mats, Grant Techniques, per-actor
> fight buttons, equipment grants, Reroll) sit on player tabs by design while the game has no
> production UI.

Two surfaces are genuinely designed rather than debug scaffolding:

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
| 3 | ~~**The 0–100 vs 0–5 scale mismatch**~~ | ✅ **Answered — C2b.** The reconciliation lives in fabrication and only there (`CombatUnitScale`), pinned by the iron-sword parity test. Whether the resulting numbers *feel* right belongs to the parked balance pass |
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

Built, tested, and runnable today. **1,191 passing tests, zero build warnings.** Single-slot
save at **schema v11** (v4 emergent archetypes → v5 learned moves → v6 genome + rolled
modifiers → v7 offline progress → v8 gold → v9 the `Armor`→`Body` slot migration → v10 the run
loadout → v11 character XP).

| System | What's real |
|---|---|
| **Tick simulation** | One deterministic shared clock (20/s) driving combat, professions, plots, statuses and the away payout |
| **Materials** | **1,448 definitions**, 21 typed properties, namespaced tags, load-time validation; every raw material has a gathering source (D36) |
| **Emergent crafting** | The complete engine: **8 processes**, the full algebra, potency, integrity, destruction → byproducts, signature registry, naming, Reaction Log, pre-commit projection — plus **traits (16, C1)** and **essence (7, C1)** with resonance capacity → strain |
| **Crafting bench UI** | Station-scoped action/substrate/ordered-reagent/catalyst selection with the semantic projection and the Advanced toggle |
| **Fabrication** | **23 forms across all 9 slots (16 weapons, ~180 deterministic name variants)**, apertures + dormancy, derived archetypes persisted, the 0–100 → combat-unit reconciliation pinned by iron-sword parity (C2b), per-piece armour + lane resistances, ring interchangeability (D33), per-slot component UI with slot-fit readings |
| **Genome + item modifiers** | Genome persisted (v6); eligibility/weight/tier + potency roll-quality; deterministic innates (U-7); **44 affixes** with live grants (scoped contributions, attached rules, move-modifier grants); seeded distribution tests; the pre-roll "Supports:" translation |
| **The semantic layer (D30, R0–R4)** | `Dungeons.Presentation` — tiers/pips/wear words, trends, risk bands, slot-fit, material/item readings — the only path from simulation state to player text; raw values behind Advanced; Assay as redaction |
| **Character identity** | 15 Bases, 25 Prefixes, 50 Suffixes (10 fully expressed), 3 Species (thin), 9 name formats, the 4.0 growth budget, gauges (8 Bases + 7 Prefixes carry one), channels — 18,750 resolvable builds with hooks live in combat |
| **Character Lab UI** | Base/Prefix/Suffix pickers + Random with a live "What changed:" diff — the build-selection surface today |
| **Modifier system** | **60 data-defined keys**, five stacking kinds (incl. diminishing), clamps and `danger` caps as data, scoped contributions with the wrong-context guard, one authoritative read path |
| **Events & rules** | 35 events; declarative trigger rules — 17 conditions (incl. stateful world reads), 16 effects, one-roll-`effects[]`, target selectors, cooldowns, seeded chance; full proc safety (chain ids, depth 2, once-per-chain, ICD, `CanTrigger`, 64-effect fuse) |
| **Effect handlers** | 11 combat handlers; unhandled kinds visibly recorded, never dropped |
| **The hit pipeline** | Packets × lanes, traced Hit Log, diminishing armour (K = 1.0), resistance cap/floor with penetration after the cap, enemy vulnerability, crit, Perfect Block, **Parry** (gear-granted), **Evade** + lane avoidance, Barrier absorption, thorns/retaliation as content, modifier-driven INCREASED/block/damage-taken stages |
| **Statuses** | **29 data-driven definitions** across all four categories; lane-mapped signature ailments; DoTs that cannot proc; `while_active` modifiers combat reads; **Resolve** (buildup, shared immunity, +25% escalation); stagger→Stun |
| **Moves** | One shape for both sides; weapon-first moveset composition with provenance; 11-op modification in fixed order; `grantMove`/`triggerMove`/`modifyMove`/`recallMove`; the Mnemonic loop; **517 shipped moves** (474 of them the 2026-08-19 spell library, `docs/spell-library.md`) |
| **Techniques** | **493 items** teaching moves into a persisted learned list (v5) with the Learn UI; the original 19 have live loot faucets, the 474 expansion techniques are debug-grant-only until the balance pass |
| **Enemies** | Family + Role + Actor composition (D26): **483 actors / 26 families / 7 roles / 7 AI brains**; the elite (Grask) and boss (Thornheart) live with rank-gated spoils and XP multipliers; five encounters wired into the Dark Forest, the rest authored breadth awaiting realm wiring |
| **Auto-combat (D41)** | Three brains on the *player* combatant through the enemy's own chooser; the only handicap is `reaction_ticks` (validator-floored at 5); live fights only |
| **Combat** | Tick-driven encounter, telegraph → windup → execute → recovery as real states, timed block/dodge/parry, queue-time costs/cooldowns/requirements, consumable use, gauges, death |
| **Professions** | **20 professions / 348 actions / 36 opportunities**, one execute path; mastery as content (6 rungs; preservation 20, doubling 40); **synergies 13 cross + 2 global** through one benefit seam; offline (12 h / 20 k caps) with auto-repeat, the away summary and guarded autosave-on-quit; Farming plots (6 at levels 1–70); the Agility course (12 obstacles; bonuses declared, unread until E6); the Assay reveal ladder; Cartography → Realm Knowledge |
| **Hideout** | 20 stations (routing, never rules) with load-time reachability both ways; the activity strip; station pages composed from the definition |
| **Realms** | The Dark Forest — **34 locations / 3 depths / all 11 node types / 3 hidden**, elite + boss, deeper-pays-rarer pinned; **163 walkable roster shells** (no encounters wired, deliberately); Realm Knowledge — **7 insights at 12/30/75/160/320/560/900**, including deep entry |
| **Realm preparation (D39)** | Destination + deep-entry picker, loadout with warnings (never a bar — anti-soft-lock is a test), consumable packing that transfers into the run bag, fieldwork readiness, the knowledge-redacted briefing, the starter kit |
| **Loot (D31)** | **72 tables**, three drop rules, composed enemy loot, the active/passive structural seam, depth gates, D28 inputs-only held by test, rarity from the material's own tag |
| **Gold** | Drops (v8), unsecured like everything else; one sink — the Hedge Trader (40 coin, unsecured run gold) |
| **Progression (D40)** | Nine consumed tracks with a roll-call test; character XP is Realm-only (0.25 × enemy max health, ×1.5 elite / ×2 boss, +25 per extraction; 50-step curve to 99); levelling raises ceilings and never heals |
| **Persistence** | Single-slot `user://save.json`, schema v11, ids-and-runtime-values only (emergent archetypes are the one definition-shaped exception, by necessity) |

## 19.3 PLANNED — designed, not built

Direction and specifics are settled; the build has not reached them.

- **Crafting operations + Overreach + Anomalous modifiers** (E7) — the endgame casino. The
  validator already reserves the Anomalous proc-depth exception; zero content exists. Exotic
  rare-rolls and **Signature affixes** (need crafting P4) sit beside it
- **Signature reactions (P4)** and **the codex/journal/renaming half of P6** — the Assay ladder
  and bench proximity hints ship; the book does not
- **Consumable forms (P5c)** — retires the legacy Healing-Salve interaction path when it lands
- **Profession tools + the yield pipeline (E6)** — the seam is ready (`ProfessionBenefits` is
  three-source by construction; the course's bonus keys and Artifice/Smithing tool components
  already ship, read by nothing)
- **Form/schematic acquisition (D29.2)** — schematic items drop from eight tables and bind to
  no form; **the one progression track nothing reads**, exempt from the roll-call test by name
- **Combat-side hazards** — ticking, telegraphed encounter hazards; realm hazard *nodes* ship
- **Exposure/inversion content, the max-resistance raise** (the 0.90 ceiling constant is read
  by nothing), **stored retaliation, ignore-fraction** — E7's defensive fringe
- **Realm affixes · the campsite workshop · encounter wiring for the 163 roster realms · realm
  tiers doing anything** (SupportedTiers and the run's tier are carried and read by nothing)
- **Fully unattended Realm runs** — auto-combat is live-only; travel, extraction decisions and
  the run bag were deliberately not started
- **Relic materials + sealed uniques** — the chase content; the boss table's `relic_shard`
  already drops as the placeholder shape
- **Chain content** — the `addChain` op and falloff machinery are live with zero users
- **The "gear at risk" difficulty toggle**

## 19.3a NEEDS CONTENT — the engine is ahead of the data

- **Remaining 40 suffix mechanics** (~120 expressions) — the ten expressed prove the model
- **The Species roster** (3 thin of a designed 10) — also a design gap, §19.4
- **Move modifiers** — the 11-op system ships with exactly one data user (Emberbrand); E5's
  affix pools remain its intended author at scale
- **The full 150–250 modifier catalog** — 44 representative affixes prove every family shape
- **Anomalous/Exotic affix content** — blocked on E7 by design

## 19.4 NEEDS DESIGN — little or no design exists

- **Economy** — one currency (gold) and one sink (the merchant) exist; vendors at scale, item
  valuation (emergent items must be *priced by computation*), resource sinks and respec pricing
  are all undesigned. Thieving deliberately ships no coin of its own
- **The Hideout's meta-layer** — upgrades, station unlock/upgrade costs, storage management
  (the stations themselves are built, §14)
- **Species' mechanical role** — the least-developed identity layer, still fixed at Human in
  play
- **The Fighter's identity hook** (§18 #15) and **casting-speed scaling** (§18 #16)
- **Build selection as a player surface** — the Char Lab picks Base/Prefix/Suffix with a live
  diff today, but it is a dev-styled surface; the real onboarding screen (and Species choice)
  is undesigned
- **Mastery pools/checkpoints and progression milestones** — the two Melvor layers still open
- **Positioning** — deferred without a decision on whether it's ever coming
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
| Fixed crafting interactions | **Retired** — only the Healing Salve shim remains until consumable forms (P5c) land |

## Appendix: the effect-foundation package

The design settled by the 27 decisions in `docs/effect-foundation.md` §12. **Slices E0–E4, C1,
C2 and E5's front half are BUILT** (events → hit pipeline → lifecycle split + statuses → rule
engine + scoped modifiers + handlers + stateful conditions → moves → traits/essence →
fabrication + the scale reconciliation → the Genome and 44 live modifiers). **E6 and E7
remain** (profession tools; operations + Overreach + Anomalous), plus E5's long tail (the full
modifier catalog).

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
