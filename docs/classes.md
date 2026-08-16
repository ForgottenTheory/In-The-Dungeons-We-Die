# Character Classes & Identity

> **Status: IMPLEMENTED (data + composition + naming). Movesets are BUILT (E4); the universal
> move library + acquisition (M2′) is the remaining content pass. Base framing follows D25: a
> Base is a growth archetype plus a starting kit — never a license.**
>
> This document was rewritten when the roster was replaced. The previous roster (Hexslinger,
> Wayfarer, Pitfighter, Gravetender, Haruspex, Warden, Wretch; the Pyromaniac/Bloodbound prefix
> pool; the "Of The …" suffix pool) is **superseded**. Only Bastion, Exploding Kneecaps,
> The Last Laugh and Unreasonable Confidence survived.

---

## 1. Structure

```
Base  +  Prefix  +  Suffix
```

- **Base** — the progression chassis: what grows, plus a starting kit — never a license (D25).
- **Prefix** — one mechanic that mutates that playstyle.
- **Suffix** — a bizarre rule you get to abuse, with three expressions so any build can use it.

**15 × 25 × 50 = 18,750 characters, none of them authored.** Every combination is derived.

Species still exists as a separate layer and is out of scope for this pass.

---

## 2. Bases

A Base is distinguished by its **engine** — how resource flows and how its loop feels against a
ticking clock — not by its flavour. Two Bases with the same engine and different themes are the
same Base.

**An engine is the Base's *starting kit and affinity*, never an exclusive license (D25).** Gauges,
moves and mechanics are universal definitions any layer may grant later (equipment, Prefix,
Species, learned specialization); attributes, resources and scoped modifiers do the specializing.
Move requirements are physical and conditional only — a class-check condition kind may never be
added.

| Base | Growth | Resource | Ch. | Engine | Weakness |
|---|---|---|---|---|---|
| Fighter | STR·DEX / END | Stamina, *no gauge* | S | ⚠ *Stale* — "moveset from the weapon" was universalized in E4; needs a new hook (NEEDS DESIGN, GDD §18 #15) | Only as good as their gear |
| Juggernaut | STR·CON / END | **Momentum** | S | Builds from damage dealt *and taken*; shortens windups, ignores interrupts | Lulls kill the engine |
| Operative | DEX·LUK / INT | *on the target* | S | Creates openings, then dumps into the window | Nothing banked between fights |
| Outlander | DEX·WIS / CON | consumables | S | Finite prepared resources spent at range | Collapses once closed on |
| Kineticist | INT·STR / DEX | **Force** | S | Damage from collision and geometry, not the spell | Needs terrain and bodies |
| Vitalist | WIS·CON / END | *Health itself* | G | Banks and moves life between targets | Every cast is a real risk |
| Wizard | INT·WIS / DEX | **Held Spell** | S | Charge one big spell and *hold* it; the skill is the wait | A wasted hold is a wasted fight |
| Invoker | INT·END / CON | **Intensity** | U | Channels that ramp the longer they're held | Fragile mid-channel |
| Druid | WIS·STR / CON | **Form** | G | Forms swap the entire moveset at a transformation cost | Wrong form is fatal |
| Bastion | CON·END / STR | **Guard** | G | Block pool spent absorbing; precise blocks refund it | Very low damage |
| Bard | LUK·WIS / DEX | songs | U | Manipulates **the clock** — intervals, telegraphs, recovery | Needs uptime |
| Necromancer | WIS·INT / CON | **Thralls** | U | Kills make bodies that act on their own intervals | Long ramp |
| Artificer | INT·DEX / END | **Charges** | U | Deployables on independent timers | Setup time |
| Warlock | INT·LUK / CON | **Debt** | U | Cheap in mana, expensive in Debt, which collects | Self-destructive |
| Vanguard | STR·END / DEX | **Threat** | G | Forces targeting; dictates where the fight happens | Fragile once it stops dictating |

**Growth budget.** Every Base distributes the same **4.0 points per level**; only the shape
differs. Authored weights name the notable attributes and the remainder trickles evenly. This is
the rule that makes Base choice a trade rather than a menu where some options are strictly
larger — enforced by `ContentValidator` and by test.

**Gauges are optional.** Seven Bases have none. Giving everyone a bar would flatten exactly the
distinctions the roster exists to create. Gauge feeds are ordinary trigger rules, so a gauge
needs no bespoke plumbing.

---

## 3. Expression channels

Suffixes express through one of three channels, keyed to **events every build produces** rather
than to attribute archetypes — so no Suffix is ever unusable by a given Base.

| Channel | Fires when you… |
|---|---|
| **Strike** | land a discrete damaging hit |
| **Guard** | avoid, absorb, mitigate, or protect |
| **Surge** | spend, accumulate, or sustain a resource |

The Base declares a default. Prefixes or equipment may shift it later; that hook belongs in
`BuildResolver` and nothing downstream needs to change when it lands.

---

## 4. Prefixes

Each adds **one recognizable mechanic**, authored once against events.

**Hard rule: a Prefix may never reference a Base.** Galvanic hooks `ResourceSpent` — so a
Juggernaut charges by swinging, a Wizard by releasing a hold, a Bastion by absorbing, a Warlock
fastest of all. One implementation, fifteen feels. Without this rule the roster costs
15 × 25 = 375 hand-authored combinations. `ContentValidator` enforces it.

| Prefix | Mechanic |
|---|---|
| Trickster | **Feint** — cancel a telegraph into something else and punish the reaction |
| Galvanic | **Charge** from any resource spend, discharging as a chain arc |
| Explosive | **Planted charges** that detonate on a timer, or instantly on death |
| Venomous | **Toxin stacks** burstable early by a heavy action |
| Gravitic | **Wells** — actions leave pull/slow zones |
| Vampiric | **Siphon** — banked life that must be *claimed*, never auto-healed |
| Clockwork | **Cadence** — consistent intervals build a bonus |
| Spectral | **Phase** — intangible, unable to affect anything either |
| Sylvan | **Rooting** — holding position grows stacks; moving resets |
| Abyssal | **Devour** — kills steal a trait for the encounter |
| Radiant | **Illumination** — damaged enemies are lit and cannot hide |
| Seismic | **Aftershock** on any long-windup action |
| Chrono | **Rewind** — snapshots you can spend to restore |
| Psionic | **Overload** built by *observing* telegraphs, spent to interrupt |
| Crystalline | **Lattice** — huge mitigation that shatters into damage |
| Bureaucratic | **Filed intent** — declare, then comply or be penalized |
| Recursive | **Echo** — moves re-fire at reduced power, chaining |
| Dissonant | **Interference** — nearby statuses corrupt |
| Parasitic | **Latch** — a parasite that drains and leaps on death |
| Infested | **Brood** — damage in either direction spawns swarmlings |
| Masochistic | **Suffering** — damage taken becomes currency; healing destroys it |
| Mnemonic | **Recall** — replay a stored move with no windup |
| Biomechanical | **Grafts** — implanted materials contribute to *you* |
| Glitched | **Fault** — actions misfire into wrong, sometimes better, outcomes |
| Quantum | **Superposition** — two outcomes, collapsing on a steerable condition |

Seven bring a gauge. A build runs **at most two meters**: one Base, one Prefix.

---

## 5. Suffixes

Rule-breakers. Explicitly allowed to reach outside combat — harvesting, extraction, crafting,
Realm danger. A Suffix should make the player ask *"wait, my character can do WHAT?"*

**Every expressed Suffix carries one expression per channel**, so no build looks at it and sees
a mechanic meant for someone else. Each expression states a drawback. Both enforced.

**Ten are fully expressed:** Exploding Kneecaps · Improper Safety Procedures · The Last Laugh ·
Questionable Ethics · Mandatory Overtime · Unlicensed Surgery · The Emergency Exit ·
Personal Liability · Terminal Curiosity · Absolutely No Refunds.

The other forty are roster entries — named, formatted, given a one-line fantasy, awaiting design.
That is deliberate: the naming system and the roster ship ahead of 150 authored mechanics.

Example — Exploding Kneecaps:

| Channel | Trigger | Effect |
|---|---|---|
| Strike | heavy hit lands | detonates at the target |
| Guard | a block lands | detonates against the attacker |
| Surge | a large resource dump | detonates around you |

> Drawback: the blast does not discriminate.

**A deliberate anti-synergy worth preserving:** Absolutely No Refunds makes actions
uncancellable; The Trickster's entire mechanic is cancelling. There is a test asserting the
conflict still exists.

---

## 6. Dynamic naming

Not every character reads as "Prefix Base of the Suffix". Each Suffix carries **format
metadata** that changes the grammar of the sentence.

Only the trailing clause is templated; the lead `The {prefix} {archetype}` is universal, so a
name degrades cleanly with no prefix, no suffix, or neither.

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

A `custom_phrase` replaces the whole clause for phrasings too good to generalise.

**Formatting never touches mechanics.** A Suffix's `format` is read by `ClassNameFormatter` and
nowhere else — changing how a name reads can never change how a character plays. Pinned by test.

---

## 7. Composition

`BuildResolver.Resolve` is the whole model, and it is short on purpose:

1. The **Base** contributes growth weights, an optional gauge, and the channel.
2. The **Prefix** contributes its mechanic and an optional second gauge.
3. The **Suffix** contributes **exactly one expression** — the one matching the channel.
4. Static modifiers from all three accumulate with **provenance**, so the Lab can answer
   "why is this number what it is?"

No per-combination logic exists anywhere. `BuildResolver.Diff` reports what changed between two
builds, which is what the Character Lab renders.

---

## 8. Architecture

Everything above is data over three Core mechanisms:

- **`ModifierKeyDefinition`** — an open, validated vocabulary of modifier targets. Adding a
  mechanic is a JSON entry, not an enum change.
- **`GameEventBus`** — synchronous, ordered, deterministic. 30 events spanning combat, crafting,
  loot and extraction.
- **`TriggerRule`** — `event + conditions + effect + cooldown`, interpreted generically. Prefixes
  and Suffix expressions are *both* just lists of these.

Effects referencing systems that don't exist yet (statuses, summons, repositioning) are
authorable now and land in the engine's `Unhandled` list — **visibly inert rather than silently
missing** — until the owning system ships.

---

## 9. Open

- **Respec:** costly but reasonably accessible. Not implemented.
- **Movesets:** BUILT (E4) — movesets compose weapon-first from eight sources, and a Base may
  grant 0–1 *starting* moves from the universal library (Wizard/Bastion carry the exemplars).
  What remains is M2′: the library itself and technique-item acquisition.
- **The remaining 40 suffix mechanics**, once the three-expression model proves out in play.
- **Unlocking** Prefixes and Suffixes through Realm discoveries, bosses, profession milestones.
