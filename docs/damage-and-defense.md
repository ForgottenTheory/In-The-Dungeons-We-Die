# Damage & Defence

> **DECIDED** — settled by the 27 decisions in `effect-foundation.md` §12. Not yet built. Part of the effect-foundation package (`effect-foundation.md`).
> **Replaces the retired `combat-spec.md` §15–16, §22–24.** Amends GDD §5.5.
> Labels: **[EXISTING/PRESERVE]** · **[DECIDED]** · **[UNRESOLVED]**

---

# 1. What a hit is

**[DECIDED]** The unit of combat resolution is a **Hit**, not a number.

```csharp
sealed record Hit(
    Combatant Source,
    Combatant Target,
    string    MoveId,
    IReadOnlyList<Packet> Packets,
    IReadOnlySet<string>  Tags,        // action:attack, delivery:melee, form:sword, mech:critical…
    double    StaggerPower,            // control buildup toward Stun
    EffectContext Context);            // chain id, depth, origin tags

sealed record Packet(
    DamageType Type,                   // Slashing | Crushing | Piercing | Magic   [code enum, D16]
    string?    Aspect,                 // heat | cold | charge | toxin | corrosion | decay | kinetic | null
    double     Amount);
```

A move produces **one or more packets**. `Overhead Smash` is one Crushing packet. A storm spell
is one Magic/charge packet. A flaming sword is two packets:

```
Flaming sword hit
  Packet 1   Slashing        80    lane: physical   armour: YES
  Packet 2   Slashing/Heat   20    lane: heat       armour: YES
```

## 1.1 The splitting rule **[DECIDED — D-01]**

> **A hit is never forced into one hybrid packet.** Damage splits into as many packets as it has
> distinct lanes, and **each packet is defended by exactly one resistance lane**. Armour applies
> whenever the packet's *delivery type* is physical, whatever its aspect.

Three consequences, and they are the point of the rule:

1. **No double-resistance punishment.** The 20 heat above is checked against heat resistance and
   nothing else. It is never taxed by the physical lane as well.
2. **Added aspect damage is genuinely *added*.** `Adds 20 Heat damage to attacks` takes an
   80-damage sword to **100**, in two packets. It does not relabel the existing 80 as heat.
3. **Defences stay readable.** A player looking at a hybrid hit can point at each number and name
   the one stat that reduced it.

**Why packets and not a single number:** conversion, added-as-extra damage, per-lane resistance,
per-lane penetration, per-lane avoidance and aspect-gated ailments all need the damage to be
*divisible*. A single `(type, amount)` pair — what `CombatCalculator.Resolve` takes today —
cannot express any of them without lying about one half of the hit.

**The three ways a second packet is born**, and they are deliberately different in power and tier:

| Mechanism | Total damage | Example | Tier |
|---|---|---|---|
| **Flat added** | **increases** | `Adds 20 Heat damage` → 80 + 20 | Standard — the common case |
| **Added as extra** | **increases**, scaled off the source | `Gain 20% of Physical as extra Charge` → 80 + 16 | Exotic |
| **Conversion** | **unchanged**, moved between lanes | `Convert 30% of Physical to Heat` → 56 + 24 | Standard→Exotic |

Flat-added being the *common* case is what makes aspects feel like added damage rather than
bookkeeping. Conversion is the rarer **lane-shifting** tool — you use it to hit something where
it is weak, not to make a bigger number (see §3.2 and Example G).

---

# 2. Damage types × Aspects **[DECIDED — D-01]**

## 2.1 The model

| Axis | Cardinality | Purpose | Authority |
|---|---|---|---|
| **Damage type** | exactly 1 per packet | *delivery* — what kind of violence this is. Drives armour applicability, weapon identity, enemy vulnerability, `increased Slashing damage`. | **code enum** (D16) — Slashing, Crushing, Piercing, Magic |
| **Aspect** | 0 or 1 per packet | *energy* — what rides along. Drives the resistance lane, ailment eligibility, aspect-specific penetration/avoidance/inversion. | **data**, from the reactive property vocabulary |

**Aspects: `heat cold charge toxin corrosion decay kinetic`** (7). *(`kinetic` was named
`arcane` until 2026-08-20 — renamed by D44 so "arcane" belongs solely to the magic-economy
identity; the aspect's semantics are unchanged.)*

Deliberately **not** aspects:
- **`growth`** — there is no "nature damage". Growth is the *recovery/vital* property; it gates
  Barrier and recovery affixes and drives consumable healing. Making it a damage aspect would
  produce the mush the material system exists to avoid.
- **Essences** (`fire frost storm nature necrotic radiant abyssal`) — see §2.4.

## 2.2 The lane rule — the one that makes it coherent

> **Lane(packet) = packet.Aspect ?? LaneOf(packet.Type)**
> **A packet is reduced by exactly one resistance. Never two.**

```
LaneOf(Slashing | Crushing | Piercing) = physical
LaneOf(Magic)                          = magic
Lane(aspect = kinetic)                 = (none — unresistable)
```

This is the whole trick, and it is why aspects are safe to add liberally.

| Packet | Lane | Armour applies? |
|---|---|---|
| Slashing 40 | `physical` | ✅ |
| Slashing/heat 12 | `heat` | ✅ — it is still a sword cut |
| Magic 20 | `magic` | ❌ |
| Magic/charge 20 | `charge` | ❌ |
| Piercing/arcane 15 | **none** | ✅ |

**Armour applies to packets whose *type* is physical, regardless of aspect.** This closes the
obvious exploit: you cannot bypass armour by slapping a heat aspect onto a sword. Armour
resists the *delivery*; resistance resists the *energy*.

## 2.3 Why this is better than the alternatives

| Alternative | Failure |
|---|---|
| Aspects multiply an extra resistance layer (`×(1−typeR)×(1−aspectR)`) | Hybrid damage is strictly worse than pure damage against any defended target, so nobody ever wants an aspect. Kills the whole idea. |
| Aspects are their own damage types (11 types) | Grim Dawn. Eleven defensive stats on every item, unreadable tooltips, and "which of my nine resistances is low?" as a UI problem. |
| Aspects are cosmetic tags with no defensive meaning | Then "Charge Resistance" and "chance to negate a Charge hit" — explicitly requested — have nothing to attach to. |

**One lane per packet** gives aspects a real defensive identity, keeps the defensive stat count
at 8, and makes conversion a genuine tactical lever (§2.6) rather than a damage bonus.

## 2.4 Essences tag, empower and gate — but never mitigate **[DECIDED — D-04]**

Per GDD §9 / `emergent-item-system.md` §5, essence is the rare supernatural layer with an
**anchor property**. In combat it does four jobs:

1. **Empowers its anchor aspect.** `essence.fire` increases heat-aspect damage and Burn
   magnitude on items that carry it. Scales expression, not lanes.
2. **Gates supernatural affixes.** Storm-family Exotic and Signature affixes require
   `essence.storm ≥ n`. *"This affix requires an item capable of expressing Necrotic Essence"*
   is affix eligibility (`affixes.md` §3.2).
3. **Modifies ailments.** Fire-essenced Burn is stronger and lasts longer than mundane Burn.
4. **Tags packets, moves and effects as metadata** that conditions can read.

### The invariant

> **Essence tags are identity and metadata. They are never an additional mitigation
> calculation.**
>
> Damage always resolves through the packet's normal **lane** — heat, cold, charge, toxin,
> corrosion, physical, magic. There is no Fire Resistance, no Storm Resistance, no Necrotic
> Resistance, and no essence-keyed avoidance.

**Where essence tags may and may not be read**, mapped onto the pipeline in §3.1:

| Pipeline stages | Essence readable? | |
|---|---|---|
| 4–8 avoidance (dodge/parry/block/evade/negate) | ❌ | avoidance is lane-keyed only — otherwise essence becomes a defensive axis you must gear against |
| 16–21 mitigation (armour, resistance, damage taken, block) | ❌ | the invariant |
| 26 ailment application | ✅ | essence modifies ailment magnitude and duration |
| 28 triggered effects | ✅ | any condition may test an essence tag |
| Affix eligibility / weight / tier | ✅ | `affixes.md` §2 |

**Validator rule:** no `combat.resist.*`, `combat.avoid.*` or `combat.damage_taken.*` contribution
may carry an `essence:` scope. Triggers are unrestricted.

### What this buys, with no new machinery

`essence:` becomes a tag namespace (`effect-foundation.md` §5), so the **existing** `hasTag`
condition covers every case:

| Effect you want | How it is authored |
|---|---|
| *Storm-essenced hits can chain* | `MoveModifier` `addChain`, match `hasTag essence:storm` |
| *Radiant effects deal increased damage to Undead* | `when: [hasTag essence:radiant, targetHasTag origin:undead]` |
| *Abyssal spells have a chance to invert resistance* | `when: [hasTag essence:abyssal, actionHasTag action:spell]`, effect `invertResistance` |
| *When struck by a Frost-essenced effect, gain X* | `event: HitLanded`, `when: [targetIsSelf, hasTag essence:frost]` — a **trigger**, not mitigation, so it is permitted |
| *Requires an item capable of expressing Necrotic Essence* | `eligibility.requires_any_essence: ["necrotic"]` |

**Seven more lanes would double the defensive surface** for a layer present on ~40 of 470
materials. Tags cost one namespace and zero defensive stats — and because scoped contributions
(D-12) accept any tag as a scope, `+30% increased Storm damage` falls out for free as a rarer,
stronger sibling of `+30% increased Charge damage` without ever becoming a resistance.

## 2.5 `kinetic` — the unresistable aspect **[DECIDED — D-03a; renamed from `arcane` 2026-08-20, D44]**

The aspect was named `arcane` until the identity redesign claimed that word for the
magic-economy identity (D44); *kinetic* — freed by cutting Kinetic from the identity roster —
names what this aspect always was: raw force. GDD §9's original grounding stands: it is not an
element, and its job is *"supplies untyped force/magic damage."*

**So `kinetic` is the aspect with no resistance lane.** Consequences, all intentional:

- Kinetic damage is **reliable** — no resistance, no exposure, no immunity ever applies.
- Kinetic damage is **unamplifiable** — no kinetic ailment, no kinetic penetration, no kinetic
  inversion, no essence anchors it.
- It is mitigated only by **armour** (if the packet's type is physical), **avoidance**, and
  global `damage taken` modifiers.
- It is the correct lane for Kineticist collision damage, Warlock Debt payments, and any effect
  that must simply *work*.

This gives kinetic a sharp, teachable identity and stops it being "generic magic".

### 2.5.1 The scaling guard — why unresistable damage does not become best-in-slot

Unresistable damage is the classic stat that quietly wins, because "cannot be reduced" compounds
with every offensive multiplier. Leaving that to tuning would be a promise, not a design. So the
guard is **structural and validator-enforced**:

> **Kinetic damage accepts flat additions and *global* increases. It never accepts a
> lane-specific `increased` modifier, and it never accepts a `more`/`less` multiplier at all.**

Consequences:

| | Every other lane | Kinetic |
|---|---|---|
| Flat added | ✅ | ✅ |
| Global `increased damage` | ✅ | ✅ |
| Lane-specific `increased` (`+40% increased Heat damage`) | ✅ | ❌ |
| `more` / `less` multipliers | ✅ | ❌ |
| Ailment amplification | ✅ | ❌ (no kinetic ailment) |
| Penetration / exposure / inversion | ✅ | ❌ (nothing to reduce) |
| Conditional damage (`vs Frozen`, `while Barrier active`) | ✅ | ❌ |

**The resulting shape is the intended one.** Kinetic damage scales roughly *linearly* while every
resistable lane can go multiplicative. So kinetic is **strongest at low investment** — the
reliable floor you take into an unknown Realm — and **weakest at high investment**, because a
specialist heat build eventually leaves it far behind. A build cannot "just stack unresistable
damage" because there is nothing to stack.

**Validator rules:** no affix may target a `more`/`less` key scoped to `lane:kinetic`; no affix may
declare a lane-specific `increased` in the kinetic lane; kinetic tier ranges on flat-add affixes may
not exceed the corresponding resistable-lane ranges (kinetic trades ceiling for reliability, it
does not get both).

> **Note on `properties.json`:** the `arcane` **property** (which keeps its name until the
> identity migration retires the property layer) declares `resisted_by: [{ resonance, 0.5 }]`.
> That stays — it is **crafting-side only**, describing how a material resists arcane *influence
> during a reaction* (`ResistanceCalculator`). It has no combat expression under D-03a, and the
> validator should not treat it as defining a combat lane.

## 2.6 The offensive modifier stack **[DECIDED]**

Borrowed from PoE because it is proven, readable and resistant to multiplication:

| Layer | Mode | Example |
|---|---|---|
| **Base** | — | the move's authored packet amounts |
| **Flat added** | additive | `+8 Slashing damage`, `adds 4–9 heat damage to attacks` |
| **Increased / reduced** | **additive with each other**, then applied once | `40% increased Slashing damage` + `25% increased damage with Swords` = `×1.65` |
| **More / less** | **multiplicative** | `30% more damage while at full Stamina` = `×1.30`, separately |
| **Conversion** | moves amount between packets | `30% of Slashing converted to Heat` |
| **Added as extra** | creates a new packet without removing | `Gain 20% of Slashing as extra Heat` |

**Why `increased` and `more` are different words:** it is the single best readability decision in
PoE. Additive-with-each-other bonuses are common and safe; multiplicative bonuses are rare and
build-defining, and the player can tell which they are holding from one word. **Adopt the
distinction; adopt the vocabulary.** Naming: use "increased/reduced" and "more/less" exactly as
PoE does — inventing synonyms here helps nobody.

**Conversion rules** (the anti-degeneracy set):
1. Conversion is applied to the **base packet before increases**, so it cannot double-dip.
2. **Total conversion out of a lane is capped at 100%.** Excess is proportionally scaled down.
3. Conversion is **single-hop**: `physical → heat → cold` does not chain. Validator enforces the
   conversion graph is acyclic *and* depth 1.
4. A converted packet keeps its **original `DamageType`** (so armour still applies) and takes
   the **new aspect's lane**. This is the rule that makes conversion a *lane-shifting* tool
   rather than an armour-bypass tool.
5. **Added-as-extra** creates a packet that does **not** reduce the source. It is therefore
   strictly stronger than conversion and lives at a higher affix tier.

---

# 3. The damage resolution pipeline **[DECIDED]**

## 3.1 The stages, in order

Every stage is a discrete, individually testable step that reads and writes a mutable
`HitContext` and appends to the `HitLog`. **The order is the specification.**

```
── PRE-HIT ─────────────────────────────────────────────────────────────────
 1  BUILD          move + moveset modifiers → packets, tags, stagger power
 2  COST           pay resources; abort if unaffordable            → ResourceSpent
 3  ON-EXECUTE     trigger rules on ActionCompleted (depth 0)      → ActionCompleted

── AVOIDANCE (binary; any success ends resolution) ──────────────────────────
 4  DODGE          target in dodge stance at this tick?            → HitAvoided via:dodge
 5  PARRY          target in parry window? (gear-granted)          → HitAvoided via:parry, Parried
 6  PERFECT BLOCK  target in the tight block window?               → HitAvoided via:perfect_block, Blocked
 7  EVADE          passive, untelegraphed hits only, capped        → HitAvoided via:evade
 8  NEGATE         per-lane avoidance affixes, rolled per packet   → HitAvoided via:negate
     ── if every packet is negated, the hit is Avoided; else continue with survivors

── THE HIT LANDS ───────────────────────────────────────────────────────────
 9  HIT LANDED     the hit connected                               → HitLanded  ⚑ thorns hook
10  FLAT ADDED     add flat damage (scope-filtered by tags/lane); attribute scaling is one
11  CRIT           roll once for the hit; multiplies base+flat, and stops there
12  INCREASED      Σ increased/reduced, applied once per packet
13  MORE/LESS      Π more/less multipliers per packet
14  CONVERSION     move amounts between packets (cap 100% out, depth 1)
15  ADDED-AS-EXTRA create additional packets

── PER-PACKET MITIGATION (each packet resolved independently) ──────────────
16  ARMOUR         physical-type packets only; diminishing formula (§5.4)
17  RESISTANCE     the lane pipeline (§4.2): sum → exposure → cap → invert → penetrate → floor
18  DAMAGE TAKEN   Π damage-taken multipliers (Vulnerable, Guarded, per-lane)
19  CRIT TAKEN     critical damage taken reduction, if the hit crit
20  BLOCK          normal block multiplier applied to the whole hit
21  FLOOR          each packet ≥ 0; total ≥ MinimumDamage if any packet survived

── APPLICATION ─────────────────────────────────────────────────────────────
22  BARRIER        absorb from Barrier first, then Health          → BarrierBroken
23  APPLY          reduce Health                                    → DamageDealt / DamageTaken
24  MITIGATION     report prevented total                           → DamageMitigated
25  STAGGER        stagger power → control buildup toward Stun     → (see statuses.md §4)
26  AILMENTS       roll per-lane ailment application on final per-lane damage → StatusApplied
27  DEATH          if Health ≤ 0                                    → Killed / Defeated
28  ON-HIT PROCS   trigger rules at depth 1 (thorns, on-crit, on-kill, resource-on-hit)
```

## 3.2 The seven ordering decisions that matter, and why

**Avoidance before everything (4–8).** Avoidance is binary; resolving damage you then throw away
wastes work and makes the log lie. It also makes the semantic clean: an avoided hit produced no
packets, so no ailment, no thorns, no on-hit.

**Crit after flat, before increases (11, between 10 and 12).** Crit multiplies *base + flat* and
stops there. If it came last it would multiply every "more" multiplier as well and crit builds
would scale quadratically with everything else — the difference between crit being *a* build and
crit being *the* build.

> **Corrected while building E1.** This rationale always said "base+flat", but the stage list had
> CRIT at 10 and FLAT ADDED at 11 — so *as specified*, crit would have multiplied the base weapon
> damage only and ignored every flat addition, attribute scaling included. Implementing it against
> the existing `Crit_MultipliesBeforeArmor` test surfaced the contradiction immediately. The
> reasoning was right; the ordering contradicted it.

**Conversion after increases (14 after 12–13).** Deliberately **the opposite of PoE**, which
converts first and lets both the source and destination lanes' increases apply. PoE's ordering
is the single most confusing thing in its damage system and it produced a decade of "does my
physical increase apply to my converted cold damage?" confusion. **Converting after the
multipliers means: increases apply to the lane the damage *started* in.** One sentence, always
true, no wiki required.
> **Cost of this choice, stated honestly:** "convert to heat then stack heat increases" no longer
> works, so conversion is a *defensive-lane* tool (hit them where they're weak) rather than an
> *offensive-scaling* tool. That is the intended job. **[UNRESOLVED — U-6]** if playtesting says
> conversion feels weak, the fix is a small `more` multiplier on converted damage, not reordering.

**Armour before resistance (16 before 17).** Armour is flat-shaped; applying it first means
resistance scales the post-armour number and the two layers are genuinely multiplicative rather
than one swamping the other. It also means **penetration cannot be used to make armour worse**,
which keeps the two defences independent.

**Block last among the multipliers (20).** Block is a *player decision made in time*, and it must
always feel like it did something regardless of what else is on. Applying it after all
percentage mitigation guarantees a fixed proportional benefit: a well-timed block always cuts
the damage that would otherwise have landed by the same fraction.

**Barrier before Health (22).** Barrier is the only recovery mechanic in this design (§5.7), so
it must absorb first or it is decorative.

**Ailments after final damage (26).** Ailment magnitude derives from the damage that *actually
landed* in that lane. So resistance reduces both the hit and its ailment with one number, and
"why is my Burn weak against this enemy?" has an obvious answer.

**On-hit procs last (28).** Everything the proc might read — final damage, crit, kill, ailments
applied — is settled. No ordering surprises inside a rule.

## 3.3 The Hit Log **[DECIDED — required scope]**

The combat analogue of the Reaction Log. Every stage that changes a number appends one line
stating **what changed and why**.

```
Overhead Smash — Goblin Brute → You
  Dodge          no    (stance ended tick 412, impact tick 419)
  Block          yes   (stance active 408–424)
  Packets        Crushing 34
  Crit           no    (12% chance)
  Increased      +25%  → 42.5   [Brute: Enraged +25%]
  Armour         −11.3 → 31.2   (armour 38 vs packet 42.5 → 26.6% reduction)
  Resistance     physical 45% capped 45% → ×0.55 → 17.2
  Damage taken   ×1.00
  Block          ×0.40 → 6.9
  Barrier        absorbed 6 of 6.9        [Barrier 6 → 0]  ⚑ BarrierBroken
  Health         −1   [You 44/60]
  Stagger        18 → Resolve 62/80
  Procs (d1)     Thorns 4 Crushing → Goblin Brute   [equip.bramble_plate]
                 Stamina +6                          [affix.guarded_reserve]
```

This is the artefact that answers "why did this happen?" for every deep interaction in the
game, it is what the Combat Lab renders, and it is what golden tests assert. **It is required
scope, not polish** — the same argument the Reaction Log won.

---

# 4. Resistance

## 4.1 Lanes **[DECIDED — D-03]**

| Lane | Source of damage | Signature ailment | Ships |
|---|---|---|---|
| `physical` | Slashing / Crushing / Piercing | Bleed | E1 |
| `magic` | aspectless Magic | — | E1 |
| `heat` | heat aspect | Burn | E2 |
| `cold` | cold aspect | Chill → Freeze | E2 |
| `charge` | charge aspect | Shock | E2 |
| `toxin` | toxin aspect | Poison | E2 |
| `corrosion` | corrosion aspect | Corroded | E2 |
| `decay` | decay aspect | Wither | **lane reserved E1, content later** |
| *(none)* | kinetic aspect | — | E1 |

**Eight lanes.** Each has a distinct ailment identity, which is the test for whether a lane
earns its place.

**`decay` is reserved, not shipped [DECIDED — D-03b].** The lane is registered in the closed
vocabulary and enforced by the validator from E1, so nothing needs retrofitting later — but no
Wither status, no decay affixes and no decay enemies are authored until necrotic content exists.
Cost now: one enum entry. Cost of adding a lane after the fact: the closed lane vocabulary, the
resistance table, save data, and every validator rule that enumerates lanes.

## 4.1a Physical is one lane; per-type weakness lives on the enemy **[DECIDED — D-02]**

Slashing, Crushing and Piercing remain distinct **damage types** — they drive weapon identity,
`increased Crushing damage`, move authoring and enemy counters. They do **not** get separate
resistances. Gear rolls one **Physical Resistance**.

**The counter-play moves to the enemy**, as a two-way `vulnerability` multiplier:

```jsonc
"actor.skeleton": { "vulnerable": { "Crushing": 1.25, "Piercing": 0.80 } }
"actor.drake":    { "vulnerable": { "Piercing":  1.20 } }
```

A skeleton takes 25% more crushing and 20% *less* piercing. The multiplier runs both ways, so an
enemy can be genuinely armoured against one physical type and soft against another — which is a
richer counter than a resistance number and costs the player no stat slots.

| Why this is the right trade | |
|---|---|
| **Defensive stat count** | 8 instead of 10, and the affix pool loses 3 resistance + 3 max-resistance + 3 penetration families |
| **Fighter identity** | *"Swaps to the weapon that counters it"* (GDD §3.4) is preserved exactly — it now reads the enemy rather than reading its own gear |
| **Realm Knowledge** | GDD §11.4 promises "reveal enemy resistances". This is the table it reveals. Preparation finally has a payload |
| **Avoids Grim Dawn's failure** | Nine resistances turned its endgame into "which of my numbers is low?" |

Vulnerability applies at **stage 18** (damage taken), is clamped to **[0.50, 1.50]**, and is
always shown in the Hit Log with the type named.

> **Migration (small, and only cheap now):** `combat.resist.slashing/crushing/piercing` → one
> `combat.resist.physical` key; `equip.iron_armor`'s `{ "Slashing": 0.15 }` → `{ "physical": 0.15 }`;
> `ActorDefinition` gains `vulnerable`. **Amends GDD §5.5.** Doing this after 200 items are
> authored would not be small.

## 4.2 The resistance pipeline **[DECIDED — D-05a]**

For one packet in lane *L*:

```
 1  total     = base(L) + Σ gear/passive/status resistance in L      // uncapped, can exceed cap
 2  total    += Σ exposure and resistance-reduction in L             // flat, applied BEFORE the cap
 3  capped    = min(total, maxRes(L))                                // maxRes = 0.75 + Σ max-res mods, hard ≤ 0.90
 4  inverted  = capped > 0 && invertRolled                           // see §4.4
                  ? max(−capped, INVERSION_FLOOR)                    // INVERSION_FLOOR = −0.50
                  : capped
 5  ignored   = inverted > 0 ? inverted × (1 − ignoreFraction) : inverted
 6  effective = ignored − penetration(L)                             // flat, can go negative
 7  effective = max(effective, RESIST_FLOOR)                         // RESIST_FLOOR = −1.00 → 2× damage
 8  multiplier = 1 − effective
```

**Overcapping is meaningful and is the point of step 2 being before step 3.** Resistance above
the cap does nothing on its own but absorbs exposure and resistance-reduction — so "overcap for
the Arcane Storm realm affix" becomes a real gearing goal. This is PoE's model and it is one of
the best-designed parts of that game's defensive layer.

**Penetration is after the cap** and therefore eats through overcapping. Exposure is before it
and does not. That is what makes them non-redundant.

### 4.2.1 Two-number display is required scope **[DECIDED — D-05a]**

Overcapping only works as a design if the player can *see* the number they are buying. A sheet
that reads `Heat Resistance 75%` while the truth is 88% is a hidden stat, and hidden stats are
how "why did I die?" becomes unanswerable.

> **Every resistance is displayed as `capped / raw` wherever it appears.**
> `Heat Resistance 75% / 88%` — and when raw ≤ cap, the second number is omitted entirely, so
> the complexity only appears for players who have engaged with it.

Three places this must land:

| Surface | Shows |
|---|---|
| **Character sheet** | `75% / 88%` per lane; the overcap margin highlighted when a realm affix is active |
| **Hit Log** | the full chain, so the debuff that ate the margin is visible: `Resistance heat 88 raw → −25 exposure → 63 → cap 63 → ×0.37` |
| **Realm preparation screen** | *effective* resistances **after** the target realm's affixes are applied — this is exactly the GDD §11.7 promise that "knowledgeable preparation should materially improve survival", and it is the screen where overcapping stops being trivia and becomes a decision |

Without the third one, overcapping is a stat players learn about from a wiki. With it, it is the
reason the portal screen exists.

## 4.3 The resistance-manipulation family, differentiated

Every one of these does something the others cannot. Redundant candidates were cut.

| Effect | Where | Scales with target resistance? | Tier | Distinct because |
|---|---|---|---|---|
| **Penetration** (flat, `−20 heat`) | step 6 | no | Standard | The workhorse. Eats overcap. Flat, so best against *low*-resistance targets. |
| **Exposure / Resistance reduction** (flat, debuff) | step 2 | no | Trigger | Applied *to the target*, benefits **all attackers**, has a duration, and is absorbed by overcap. A party/DoT tool, not a personal one. |
| **Ignore X% of resistance** | step 5 | yes | Exotic | Multiplicative, so it is best against *high*-resistance targets — the exact inverse of penetration's curve. |
| **Treat positive resistance as zero** | step 5, `ignoreFraction = 1` | — | Signature | Binary and unaffected by magnitude. The answer to a 90% wall. Cannot stack with itself. |
| **Invert resistance** | step 4 | yes, up to the floor | Exotic / Signature | The only effect that makes resistance *negative*. Chance-based, spiky, memorable. |
| **Steal resistance** | on-hit effect | — | Exotic | **Defensive**, not offensive: transfers N resistance in that lane from target to self for a duration. Different axis entirely. |
| **Damage scales from target resistance** (`+X% per 10 resistance`) | stage 13 (a `more` multiplier) | yes | Exotic | The *anti*-penetration affix — you now *want* resistant targets. Creates a genuinely inverted build. Capped at +100%. |

**Cut as redundant:** *"convert target resistance into another vulnerability"* — it is inversion
with extra words and a worse tooltip. Say no to it once, here.

## 4.4 Resistance inversion — exact semantics **[DECIDED — D-05b]**

The brief asks for this specifically, so it gets a full specification.

```
INVERSION_FLOOR = −0.50        // tunable single constant
inverted = (capped > 0 && roll succeeds) ? max(−capped, INVERSION_FLOOR) : capped
```

**Answers to the brief's exact questions:**

| Question | Answer | Why |
|---|---|---|
| Enemy has +60% heat res. Inverted → ? | **−50%** (floored) | Reflecting to −60% then adding penetration produced ~5–8× damage swings on a proc. The floor bounds the spike while keeping the effect strongest against the most resistant targets — which is the fantasy. |
| What if the enemy is already negative? | **Inversion does nothing.** | An effect that sometimes helps your enemy is an effect nobody equips. Inversion is a **one-way operation**: it can only ever benefit the attacker. |
| Order vs *resistance reduction / exposure*? | Exposure first (step 2), inversion after the cap (step 4). | Exposure lowers the number that then gets inverted — so exposure and inversion *anti-synergise* slightly. Correct: they are competing solutions to the same problem, and stacking both should not be strictly best. |
| Order vs *penetration*? | Inversion first (4), penetration after (6). | They **stack**: −50% inverted − 20 pen = −70% → ×1.70. Strong, bounded by the −100% floor, and it means a penetration build gets a genuine payoff spike rather than an anti-synergy. |
| Order vs *caps*? | **After the cap.** | So a 90%-max-res boss inverts to −50% just like a 75% one. Inversion's value should not scale with the *enemy's* investment in max resistance. |
| Order vs *immunity*? | **Immunity is checked first and inversion cannot break it** (§4.5). | Immunity is a design statement ("this cannot be hurt this way"), not a large number. |
| Order vs *temporary resistance buffs*? | They are ordinary contributions at step 1, so a buff raises the value that gets inverted — **buffing resistance while an inversion build is attacking you is actively bad for you.** | A genuinely fun interaction and worth surfacing in the Hit Log. |

**Constraints that keep it healthy:**
- **Chance-based** (5–25%), **one lane**, `unique` stacking — a second inversion affix in the
  same lane is rejected at equip time.
- **Exotic tier minimum**; the strongest version (guaranteed on crit) is Signature.
- The Hit Log states loudly when it procs: `Resistance heat 68% → INVERTED −50%`.

## 4.5 Immunity **[DECIDED]**

Two distinct things, and conflating them is a classic bug:

| | **Lane immunity** | **Status immunity** |
|---|---|---|
| Means | takes 0 damage in this lane | cannot receive this status/category |
| Source | rare enemy trait, realm affix, brief self-buff | Control Immunity window (`statuses.md` §4), boss traits |
| Bypassed by | **nothing** — not penetration, not inversion, not exposure | nothing |
| Partial bypass | **[DECIDED]** a Signature-tier `pierce immunity` affix converts lane immunity into **90% resistance** for that hit — it does not remove it | — |

Immunity is a **flag**, never a very large resistance number, precisely so that no arithmetic
can accidentally overcome it. It is checked at stage 17 before anything else in the lane.

**Use it sparingly.** A lane-immune enemy with no other counterplay is a wall; the intended use
is 1–2 boss mechanics and a Realm affix, always with a stated alternative lane.

---

# 5. Defence layers

Each layer is justified against the brief's seven questions. Layers that failed the test are in
§5.9.

## 5.1 Evasion / Dodge **[EXISTING/PRESERVE + DECIDED demotion — D-07]**

| | |
|---|---|
| **Problem solved** | The telegraph skill test. "I see something dangerous coming — what do I do?" |
| **Player experience** | A timed stance. Press dodge in the window, take nothing. |
| **Counter** | Fast or untelegraphed attacks; stamina pressure; multi-hit moves that outlast the window |
| **Capped?** | The *window* is capped at base + 50%; stamina cost has a floor |
| **Immunity?** | Yes, within the window — that is the point |
| **Gear** | Buys **window length**, **stamina cost**, and **recovery**. ⚠ **Not a passive roll.** |
| **Enemies** | Enemies may dodge on the same rules; some declare `undodgeable` |

**The demotion, argued.** `combat.dodge.chance` exists as a modifier key. GDD §5.5 says the
timed stance "is the core skill test and it works today". A passive dodge roll makes that test
optional: at 40% passive dodge the correct play is to stop reading telegraphs and attack more.

**[DECIDED]** Keep **one** small passive: `evade`, applying **only to untelegraphed hits**
(ailment ticks excluded), `diminishing`, capped at **15%**. It exists so that light armour and
Dexterity have a defensive identity without touching the skill test.

**Modifier registry changes:**

| Key | Change |
|---|---|
| `combat.dodge.chance` | **retired as an affix target** → renamed `combat.evade.chance`, kind `diminishing`, max **0.15**, scoped to untelegraphed hits |
| `combat.dodge.window` | **new** — additive ticks, max +50% of base |
| `combat.dodge.cost.mult` | **new** — multiplicative, floor 0.5 |
| `combat.block.window` | **new** — additive ticks, max +50% of base |
| `combat.perfect_block.window` | **new** — additive ticks, max +6 |

### 5.1.1 How auto-combat dodges **[DECIDED — D-07 consequence · BUILT Phase 10, D41]**

The obvious reading of this decision is *"auto-combat has no reflexes, so it can never avoid
anything."* That is wrong, and the correct answer makes the decision stronger.

GDD §5.7 requires that **auto-combat uses the same rules** — "automation chooses actions, the
domain resolves them normally", with deliberately no separate combat calculator. So auto-combat
*does* block and dodge: its AI profile watches telegraphs and issues the same commands a player
would. It is simply **slower**.

> **Automation's disadvantage is expressed as reaction latency, never as a damage penalty.**

```
AI profile:  reaction_ticks: 8
Player:      whatever their actual reaction is, typically 2–5
```

| | Skilled player | Auto-combat (`reaction_ticks: 8`) |
|---|---|---|
| Normal block (16-tick window) | reliably | reliably |
| Dodge (10-tick window) | reliably | often |
| **Perfect Block** (4-tick window) | with practice | **almost never** |
| Parry (3-tick window) | the top of the skill ladder | **never** |

This is exactly the property GDD §5.3 demands — *"active play must earn its advantage through
better decisions, never a hidden +50% active damage"*. Active play's advantage is that it can hit
the tight windows. Passive play still functions, just without the highest-value outcomes. And it
falls out of one number on the AI profile rather than a parallel balance model.

**Consequence for gear:** window-widening affixes are worth *more* to an auto-combat build than to
a skilled player, because they pull the tight windows into reach of an 8-tick reaction. That is a
genuine, discoverable gearing difference between playstyles, and it cost nothing to create.

> **As built (Phase 10, `core/Combat/AutoCombatPilot.cs`).** The mechanism that produces this
> table is one line of arithmetic: the stance is committed at `max(noticed + R, impact − R)`.
> Committing `R` early is what puts every tight window out of reach — they are all measured from
> the moment the stance went up. The `noticed + R` half adds a second consequence for free: an
> attack arriving sooner than `2R` after it appears **cannot be answered at all**, which is
> exactly why the small untelegraphed-only `evade` passive survived this decision.
>
> `AutoCombatTuning.MinimumReactionTicks` is derived from the Perfect Block and Parry windows, so
> a profile fast enough to reach them is a **load error**. Retuning a window retunes the floor.

## 5.2 Block **[EXISTING/PRESERVE + DECIDED split — D-06]**

| | |
|---|---|
| **Problem solved** | A *mitigation* answer to attacks you cannot avoid — sustained pressure, multi-hit, area |
| **Player experience** | Timed stance; damage drops sharply. Precise timing negates entirely. |
| **Counter** | Stamina cost; unblockable moves; stagger damage still lands |
| **Capped?** | Block multiplier floored at 0.25 |
| **Immunity?** | Only via **Perfect Block** |
| **Gear** | Shields and heavy forms buy window, multiplier, stamina cost, and Perfect-Block window |
| **Enemies** | Enemies block; blocking enemies are the reason stagger/`unblockable` exist |

**[DECIDED — D-06] Two outcomes, not one:**

| Outcome | Window | Result | Semantics | Events |
|---|---|---|---|---|
| **Block** | the stance window | ×0.40 damage | **Mitigation** | `HitLanded` + `Blocked` |
| **Perfect Block** | the first ~4 ticks of the stance | negated, refunds Guard/stamina | **Avoidance** | `HitAvoided(via:perfect_block)` + `Blocked` |

This gives the **Bastion** its stated identity ("precise blocks refund Guard") for free, gives
the Guard channel two distinct events to hook, and makes "chance to retaliate when blocking"
meaningfully different from "chance to retaliate when hit".

## 5.3 Parry **[DECIDED — D-26: gear-granted]**

| | |
|---|---|
| **Problem solved** | The *offensive* defence. Block survives; parry creates an opening. |
| **Player experience** | A very tight window that negates **and** applies heavy stagger to the attacker, opening a counter-window |
| **Counter** | Punishing window (~3 ticks); failing it leaves you in recovery; unparryable moves |
| **Capped?** | Window capped tightly; stagger applied is capped |
| **Immunity?** | Within the window, yes |
| **Gear** | **Gear-granted capability, not universal.** Only forms with `parry` provide it |
| **Enemies** | Elite/boss enemies parry — the reason `unparryable` and feints exist |

**Why it earns a slot:** dodge avoids, block mitigates, parry *converts defence into offence*.
That is a third distinct job and it is the top of the skill ladder. It is also the natural home
for the retaliation archetype and for the Trickster's feint mechanic to punish.

**[DECIDED — D-26] Parry is gear-granted, not universal.** A form declares `grants: parry`;
without a parrying weapon or shield equipped, the command does not exist.

Three reasons it earns the extra complexity:

1. **It makes forms genuinely different.** A rapier that parries and a greataxe that does not are
   distinct *defensively*, not just in damage numbers — which is the strongest argument for
   having many weapon forms at all.
2. **It is the cleanest instance of "an item grants a capability"**, which is a stated design
   goal (`moves.md` §3.4). Parry proves the mechanism before affixes need it.
3. **It gives the top of the skill ladder a gearing prerequisite.** Parry is a 3-tick window; a
   player who wants that expression has to build for it, which makes it a *choice* rather than a
   free option everyone ignores.

**Consequence for auto-combat:** an 8-tick reaction cannot hit a 3-tick window, so automated play
never parries (§5.1.1). Correct — parry is the reward for being present.

**Consequence for enemies:** elite and boss actors may declare `grants: parry`, which is what
makes `unparryable` moves and the Trickster's feint mechanic meaningful.

## 5.4 Armour **[EXISTING, DECIDED reshape — D-25]**

| | |
|---|---|
| **Problem solved** | Sustained physical attrition — the many small hits of a long Realm run |
| **Player experience** | Chip damage becomes negligible; big hits still hurt |
| **Counter** | Big single hits; **Corroded**; magic and aspect damage bypass it entirely |
| **Capped?** | By the formula's shape, not a hard cap |
| **Immunity?** | **No.** Armour asymptotes; it never reaches 100% |
| **Gear** | The primary heavy-armour stat; scales from material `hardness` and `mass` |
| **Enemies** | Armoured enemies are the reason `corrosion` and armour penetration exist |

**[DECIDED — D-25] Change the formula.**

```
reduction = armour / (armour + K × packetAmount)      K = 1      [D-27]
final     = packet × (1 − reduction)
```

> **`K = 1`, not PoE's 10 — and the reason matters [D-27].** The formula *shape* is borrowed;
> the constant must not be. PoE's armour values are in the **thousands**, ours are single digits
> (iron armour + CON 5 ≈ **10**), so a PoE-scaled K gutted armour rather than reshaping it:
>
> | Incoming, vs 10 armour | Today (flat) | K = 5 | **K = 1** |
> |---|---|---|---|
> | Rusty Slash (9) | 89% ← the cliff | 18% | **53%** |
> | Overhead Smash (26) | 38% | 7% | **28%** |
>
> K = 1 keeps the intended shape — strong against attrition, weaker against spikes — while
> removing the `max(1, …)` cliff that currently makes iron armour near-immune to chip damage.
> **Recalibrate in C2**, when material properties start driving equipment stats and the whole
> armour scale moves.

Current code is `max(1, damage − armour)`. That makes armour *total* against chip damage (a
5-damage hit becomes 1) and *irrelevant* against the Goblin Brute's Overhead Smash — exactly
backwards for a defensive investment in a telegraph game, and it interacts terribly with a
`MinimumDamage` floor.

The diminishing formula gives armour and resistance **genuinely different jobs**:

| Incoming packet | Armour 40 | |
|---|---|---|
| 5 | −80% | armour is for attrition |
| 20 | −29% | |
| 60 | −12% | resistance is for spikes |

That is a real gearing decision ("do I fear the swarm or the smash?") rather than two stats
doing the same arithmetic.

**Armour penetration** is a flat reduction of the defender's armour value before the formula,
capped at 100% of armour.

## 5.5 Resistance **[EXISTING/PRESERVE, extended]**

| | |
|---|---|
| **Problem solved** | Spike damage in a specific lane; preparing for a *known* Realm |
| **Player experience** | A percentage that scales with hit size — the answer to "this realm is full of fire" |
| **Counter** | Penetration, exposure, inversion; and having 8 lanes and 6 slots |
| **Capped?** | 75%, raisable to 90% by rare max-res affixes |
| **Immunity?** | Never from resistance — only from the immunity flag |
| **Gear** | The main defensive affix family; strongly driven by material genetics |
| **Enemies** | Enemies have per-lane resistance; Realm Knowledge reveals it |

The **preparation loop** is the point: Realm Knowledge tells you the Dark Forest is toxic; you
fabricate toxin-resistant gear from materials with high `toxin_resistance` genetics; the run
goes better. That is GDD §11.7 ("knowledgeable preparation should materially improve survival")
finally having a mechanism.

## 5.6 Hit avoidance (negation) **[DECIDED]**

| | |
|---|---|
| **Problem solved** | A *qualitatively* different answer to one lane — not "less damage", but "sometimes nothing" |
| **Player experience** | Rare, spiky, memorable: "that Charge blast just… didn't" |
| **Counter** | Hard caps; it cannot be built into reliability |
| **Capped?** | **Yes, hard: 25% per lane, 15% global, `diminishing`** |
| **Immunity?** | **Never.** By construction it cannot reach 100% |
| **Gear** | Exotic tier; requires strong genetic pressure in that lane's property |
| **Enemies** | Rare enemy trait; always telegraphed as a mechanic, never a silent roll |

Distinct from resistance, exactly as the brief says: `+40% Charge Resistance` means charge hits
land for less; `8% chance to negate Charge hits` means roughly 8% do nothing at all. Avoidance
is strictly stronger per point than mitigation (it removes ailments and on-hit effects too),
which is why it is capped low and priced high.

**Rolled per packet at stage 8.** If every packet is negated the hit is Avoided; if some
survive, the hit lands with the negated packets removed. That makes partial negation of a hybrid
hit coherent.

## 5.7 Barrier **[DECIDED — D-15, the recovery answer]**

| | |
|---|---|
| **Problem solved** | Sustain that does **not** trivialise Realm attrition |
| **Player experience** | A temporary shield that absorbs before Health and decays. Spend well, or lose it. |
| **Counter** | Decays on its own; **Wither** reduces gain; big hits blow through it |
| **Capped?** | Max Barrier capped as a fraction of max Health (~40%) |
| **Immunity?** | No |
| **Gear** | The entire recovery affix family targets Barrier |
| **Enemies** | Enemies gain Barrier from shield-type abilities; breaking it is a visible goal |

**This is the most important design call in the defence model.** GDD §4/§5.4/§13 make
non-regenerating Health load-bearing: damage is Realm attrition, healing costs resources, and the
extract-or-deeper decision is driven by your remaining Health. An affix pool containing
`+3 Health on Hit` quietly deletes that pillar, and it does so *invisibly*, one small affix at a
time.

**[DECIDED]** Affixes never grant passive Health regeneration. The recovery family grants:
- **Barrier on kill / on block / on crit / on status applied** — resets between fights, never
  banks
- **Barrier per second while [condition]** — conditional uptime
- **Increased Barrier effectiveness / capacity / decay rate**
- **Conditional, capped, on-event healing** — allowed, but rare, always capped per hit *and* per
  second, and never unconditional

The player still gets the ARPG "sustain build" fantasy; the Realm still grinds them down.
`BarrierBroken` is a trigger, which makes "when your Barrier breaks…" a real affix family.

### 5.7.1 Barrier does not spend the meter budget

GDD §3.6 sets a hard readability rule: **a build runs at most two meters** — one from the Base,
one from the Prefix — because *"three would stop being readable."*

**Barrier is not a third meter.** It renders as an **overlay on the Health bar** (the WoW
absorb-shield / PoE energy-shield presentation), not as its own gauge:

```
Health  ████████████████░░░░  44/60
                    ▓▓▓▓▓▓    +18 Barrier
```

This matters more than it looks. A Barrier presented as a separate bar would compete for
attention with the Base and Prefix gauges — the exact readability collapse §3.6 exists to
prevent — and would also read as a *resource you manage* rather than what it is: **a buffer that
is either there or isn't.** The overlay says "you have a bit more Health than usual, briefly",
which is the correct mental model and needs no explanation.

**Consequence for the class combinator:** a Bastion (Guard gauge) running a Galvanic prefix
(Charge gauge) can still carry Barrier gear without hitting the readability ceiling.

## 5.8 Damage-taken modifiers **[EXISTING key, DECIDED discipline]**

| | |
|---|---|
| **Problem solved** | The lever statuses need — Vulnerable, Guarded, enemy vulnerability |
| **Player experience** | Mostly invisible as gear; visible as statuses |
| **Counter** | Floor of 0.50; it is a *status* lane, not a gearing lane |
| **Capped?** | Reduction floored at ×0.50 total |
| **Gear** | **Deliberately rare as an affix.** Global damage reduction is the least interesting defensive stat in any ARPG — it has no counter-play and no build identity |

Keep the key, keep it out of the common affix pool. It exists so statuses have something to move.

## 5.9 Layers deliberately **not** added

| Rejected | Because |
|---|---|
| **Physical Damage Reduction** | It *is* the `physical` resistance lane. Two stats, one arithmetic. |
| **Fortify / Guard stacks** | The Bastion's Guard gauge already occupies this space, and Barrier covers the rest. A third stacking-mitigation mechanic is mush. |
| **Critical avoidance** (chance for crits to become normal hits) | Binary swings on an already-random event. **Critical damage taken reduction** solves the same problem smoothly. Keep that; cut this. |
| **Stagger resistance** as its own stat | **Resolve** already does it (`statuses.md` §4.4) — stagger power is buildup toward Stun, and Resolve is what it has to cross. Two poise systems is one too many. |
| **DoT-specific mitigation** | Ailments are damage in a lane. Their lane resistance already mitigates them. A separate "damage over time reduction" stat would double-dip and confuse. |
| **Spell avoidance** as a distinct family | It is lane avoidance on `magic`. Same machinery, no new concept. |
| **Deflection / glancing blows / etc.** | No distinct job left after the eight above. |

---

# 6. Avoidance vs mitigation — the semantics **[DECIDED — D-06]**

The brief is right that triggers must be able to tell these apart. Three events, precisely
defined, and every trigger in the game hangs off one of them.

| Event | Fires when | Carries |
|---|---|---|
| **`HitLanded`** | a hit was **not** avoided — regardless of how much was mitigated, including a normal Block and including a hit reduced to 0 | packets, lanes, tags, `blocked` flag |
| **`DamageDealt` / `DamageTaken`** | final applied damage **> 0**, whether it landed on Barrier or Health | `amount`, `health_lost`, `barrier_absorbed`, per-lane values |
| **`HitAvoided`** | dodge, parry, perfect block, evade, or full negation | `via:` tag naming which |

| Defence | Category | Raises |
|---|---|---|
| Dodge | avoidance | `HitAvoided(via:dodge)` |
| Parry | avoidance | `HitAvoided(via:parry)`, `Parried` |
| **Perfect** Block | avoidance | `HitAvoided(via:perfect_block)`, `Blocked` |
| Evade | avoidance | `HitAvoided(via:evade)` |
| Negate (lane avoidance) | avoidance | `HitAvoided(via:negate)` |
| **Normal Block** | **mitigation** | `HitLanded`, `Blocked`, `DamageMitigated` |
| Armour · Resistance · Damage taken · Barrier | mitigation | `HitLanded`, `DamageMitigated` |

## 6.1 The full event matrix

|  | `HitLanded` | `Blocked` | `HitAvoided` | `DamageTaken` |
|---|---|---|---|---|
| Dodge | — | — | `via:dodge` | — |
| Parry | — | — | `via:parry` | — |
| **Perfect Block** | — | **✅** | `via:perfect_block` | — |
| Normal Block | ✅ | ✅ | — | ✅ |
| Armour / resistance only | ✅ | — | — | ✅ |
| Lane negation | — | — | `via:negate` | — |
| Mitigated to zero | ✅ | — | — | — |
| Ailment tick | — | — | — | ✅ |

## 6.2 `Blocked` fires on both block outcomes — and why that matters

> **On-block affixes hook `Blocked`, not `HitLanded`.**

The alternative — hooking thorns to `HitLanded` and letting an optional condition pick out the
block case — looks tidier but is a **design bug**: a *perfect* block is avoidance, so it raises no
`HitLanded`, so the better-timed block would produce **no retaliation**. Skill would be punished.

Firing `Blocked` for both outcomes fixes it and keeps the two affix families genuinely distinct:

| Affix wording | Hooks | Fires on normal block? | On perfect block? |
|---|---|---|---|
| *Retaliate when you Block* | `Blocked` | ✅ | ✅ |
| *Retaliate when hit* | `HitLanded` | ✅ | ❌ — nothing hit you |

So a Bastion who perfect-blocks refunds Guard **and** still retaliates, while a generic
thorns-when-hit build correctly gets nothing from a hit that never landed.

## 6.3 The other consequences, all falling out for free

- `DamageMitigated` carries `amount_prevented` — the basis for "return 20% of mitigated damage"
  and for the Bastion's stored-retaliation pool (§7.2).
- A hit reduced to 0 still raises `HitLanded` but **not** `DamageTaken`. "On taking damage, gain
  Rage" won't tick from a fully-mitigated hit; "on being hit, retaliate" will. Both are the
  intuitive reading.
- **Ailment ticks raise `DamageTaken` but never `HitLanded`.** A Poison tick is not a hit, so it
  cannot proc thorns. That single rule kills an entire class of DoT-driven proc loops.
- **Avoidance and retaliation anti-synergise**, and it is discoverable rather than authored: a
  build stacking lane negation removes the very `HitLanded` events its thorns need (Example B).

---

# 7. Retaliation / Thorns **[DECIDED]**

The brief asks for retaliation as a full archetype. It is, and it is the family most likely to
explode, so its rules are strict.

## 7.1 Model

Thorns is a **damage source with its own packets**, not a damage-taken multiplier:

```
thornsPackets = flat thorns (per lane) × (1 + Σ increased thorns) × Π more thorns
               + reflectFraction × damageMitigated       [capped at 0.60 of mitigated]
```

It fires at **depth 1**, targeting `triggerSource`, with **`can_trigger = false`** — thorns
damage cannot itself proc anything. **Which event it hooks depends on the affix's wording
(D-06 §6.2):** *retaliate when hit* hooks `HitLanded`; *retaliate when you Block* hooks
`Blocked`, so it fires on normal **and** perfect blocks; *retaliate when you Parry* hooks
`Parried`.

## 7.2 The three tiers

| Tier | Affixes |
|---|---|
| **Standard** | flat thorns per lane · % increased thorns · chance to retaliate · thorns only when blocking / parrying / after dodging · % of mitigated damage returned |
| **Trigger/Exotic** | thorns apply Bleed / Poison / Shock · thorns can crit · thorns scale with armour · **stored retaliation** (Block banks mitigated damage; your next attack releases it) · thorns Fear |
| **Anomalous only** | **thorns trigger on-hit effects** (raises `max_depth` by 1) · retaliation chains to a second target · thorns inherit a fraction of the weapon's aspects |

**Stored retaliation is the safest of the strong ones** and should be the archetype's headline:
Block banks the mitigated amount into a pool, and your **next attack** releases it. The release
is a normal attack at depth 0, so it procs normally without any recursion risk — and it converts
a defensive build into an offensive rhythm, which is a far better feel than passive reflection.
It is also a perfect Bastion payoff.

## 7.3 Why the restrictions

Reflection is the classic proc-loop engine: A hits B, B reflects, the reflection counts as a hit,
A's reflection triggers, forever. The three rules that close it:

1. Thorns damage carries `can_trigger = false` by default.
2. Ailment ticks never raise `HitLanded`.
3. `MAX_PROC_DEPTH = 2` and once-per-chain per rule.

Even the Anomalous version, which raises depth to 3, terminates — it just gets one more bounce.

---

# 8. Constants **[UNRESOLVED — U-4, all tunable]**

| Constant | Proposed | Note |
|---|---|---|
| `MaxResistance` | 0.75 | existing `CombatTuning.MaxResistance` is 0.75 ✅ |
| `MaxResistanceCeiling` | 0.90 | with max-res affixes |
| `RESIST_FLOOR` | −1.00 | 2× damage |
| `INVERSION_FLOOR` | −0.50 | |
| `ArmourK` | **1** | `armour/(armour + 1 × packet)` — scaled to this game's single-digit armour, **not** PoE's thousands (D-27, §5.4) |
| `BlockDamageMultiplier` | 0.40 | existing ✅ |
| `PerfectBlockWindowTicks` | 4 | of a 16-tick block stance |
| `ParryWindowTicks` | 3 | gear-granted |
| `AvoidCapPerLane` | 0.25 | diminishing |
| `AvoidCapGlobal` | 0.15 | diminishing |
| `EvadeCap` | 0.15 | untelegraphed hits only |
| `BarrierCapFraction` | 0.40 | of max Health |
| `ThornsReflectCap` | 0.60 | of mitigated damage |
| `VulnerabilityRange` | [0.50, 1.50] | enemy per-type multiplier, two-way (D-02) |
| `MAX_PROC_DEPTH` | 2 | 3 for Anomalous |
| `MAX_EFFECTS_PER_CHAIN` | 64 | the fuse |
| `MinimumDamage` | 1 | existing ✅ — **applies to the hit total, not per packet** |
