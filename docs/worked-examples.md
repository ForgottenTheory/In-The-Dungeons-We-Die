# Worked Examples — Builds, Tools, and Resolution Traces

> ⚠ **PARTLY SUPERSEDED (2026-08-20).** The material → genome → affix chains in these examples
> describe the outgoing property model, replaced by the **Identity + Signature system**
> (`docs/identity-foundation.md`, DECISIONS **D42–D44**). The combat-side halves — moves,
> statuses, triggers, resolution traces — still stand.

> **DECIDED** — settled by the 27 decisions in `effect-foundation.md` §12. Not yet built. Part of the effect-foundation package (`effect-foundation.md`).
> Proves the architecture composes. All materials are **real entries from `game/data/materials/`**;
> all property values are illustrative but plausible. Affix numbers are placeholders.
> Catalog references `[n]` point at `effect-catalog.md`.

---

# Part 1 — Ten combat builds

Each shows the full chain the design claims is continuous:
**material characteristics → fabrication → genome → eligible affixes → rolled affixes → moves &
statuses affected → the actual gameplay loop.**

---

## Build 1 — The Thorn Tank *(passive retaliation)*

**Base + Prefix + Suffix:** Bastion · Crystalline · Personal Liability

| Step | |
|---|---|
| **Materials** | **Drake Scale** (hardness 82, mass 55, heat_res 70) · **Bog Iron** (hardness 58, mass 70, corrosion 40) · **Boiled Leather** (flexibility 62, mass 20) |
| **Fabrication** | `form.heavy_armour` — plate: Drake Scale · core: Bog Iron · binding: Boiled Leather. Smelt → Quench → Forge Infusion |
| **Genome** | hardness **79** · mass **62** · corrosion **34** · insulation 41 · growth 6 · arcane 0. Trait *Resilient (71)* expressed |
| **Eligible** | Physical/Thorns up to **T1** (hardness 79) · Corrosion thorns **T3** · Life/Recovery **locked** (growth 6) · Storm **locked** |
| **Rolled** | `+18 Physical Thorns` **[87]** · `62% increased Thorns damage` **[93]** · `Retaliate for 24 when you Block` **[95]** · `+9% Physical resistance` **[51]** · `+41 Armour` **[49]** · `Thorns can apply Bleed` **[99]** |
| **Innate** | `+14% armour penetration` (hardness 79) — earned by material choice, not rolled |

**Moves & statuses:** Block gains a retaliation rider; Bleed applies from thorns; Crystalline's
Lattice shatter is a second thorns-shaped payload.

**The loop:** Hold ground. Every incoming hit — *including blocked ones*, because thorns hooks
`HitLanded` — returns damage. Blocked hits return *more*. Bleed stacks on anything that keeps
swinging. Weakness: nothing happens if you are not being attacked, and a single huge telegraph
still hurts because armour's diminishing formula is weak against big packets.

**Counterplay:** ranged enemies, Corroded (strips your armour), unblockable moves.

---

## Build 2 — Storm Critical Fighter

**Base + Prefix + Suffix:** Fighter · Galvanic · Terminal Curiosity

| Step | |
|---|---|
| **Materials** | **Stormglass** (charge 88, conductivity 79, hardness 61, instability 55) · **Electrum Ingot** (conductivity 84, hardness 52) · **Deer Sinew** (flexibility 71) |
| **Fabrication** | `form.longsword` — edge: Stormglass · core: Electrum Ingot · binding: Deer Sinew. Forge Infusion ×2 |
| **Genome** | charge **83** · conductivity **80** · hardness **58** · instability **51** · essence.storm **34**. Trait *Stormlaced (66)* |
| **Eligible** | Charge **T1** · Crit **T2** (instability 51) · Storm Exotic **unlocked** (essence.storm 34 ≥ 30) · Heat **locked** (heat 3) |
| **Rolled** | `+11% critical chance` **[19]** · `+14% crit chance against Shocked` **[194]** · `Adds 22 Charge damage` **[4]** · `18% chance to Shock on hit` **[107]** · `+16 Charge penetration` **[30]** · **Exotic:** `Critical hits treat positive Charge resistance as zero` **[37]** |

**Moves & statuses:** every attack rolls Shock; Shocked targets raise your crit chance, which
raises your Shock rate — a **self-reinforcing loop that terminates**, because Shock is
refresh-highest and crit chance is capped at 0.75.

**The loop:** Open with fast attacks to land Shock. Once Shocked, crit chance spikes; crits
nullify charge resistance entirely. Galvanic's Charge gauge fills from every Stamina spend and
discharges as an arc. Weakness: nothing works before the first Shock lands, so the opening
seconds are flat.

**Counterplay:** charge-immune enemies (Shock cannot apply), high Resolve (Shock's
interrupt rarely fires), fights too short to ramp.

---

## Build 3 — Freeze Control Caster

**Base + Prefix + Suffix:** Wizard · Chrono · The Emergency Exit

| Step | |
|---|---|
| **Materials** | **Glacial Heart** (cold 94, resonance 61, arcane 38) · **Frostpine Log** (cold 52, flexibility 58) · **Froststag Antler** (cold 44, hardness 63) |
| **Fabrication** | `form.staff` — head: Glacial Heart · shaft: Frostpine Log · cap: Froststag Antler. Attune → Quench |
| **Genome** | cold **81** · resonance **64** · arcane **35** · essence.frost **41**. Trait *Rimebound (58)* |
| **Eligible** | Cold **T1** · Control **T1** · Spell damage **T2** (resonance 64) · Frost Exotic **unlocked** · Physical **locked** |
| **Rolled** | `31% chance to Chill on hit` **[106]** · `+44% Freeze buildup` **[121]** · `+21% increased control duration` **[124]** · `+18 Cold penetration` **[29]** · `Reduce target Resolve by 20% while Chilled` **[125]** · **Exotic:** `52% more damage against Frozen enemies` **[193]** |

**Moves & statuses:** Chill on nearly every hit; Freeze buildup accrues only on Chilled targets,
and `[125]` lowers the very Resolve threshold that buildup must cross — so the two affixes are
multiplicative in a way the player can watch happen on the Resolve bar.

**The loop:** Chill first, always. Then hold the Wizard's charged spell until Freeze lands and
release into the frozen window at +52% damage. Against a boss, Freeze lands roughly once per
Resolve cycle and each cycle costs 25% more — so the fight becomes *"earn the window, spend it
perfectly, wait"*, which is exactly the Wizard's stated engine.

**Counterplay:** Resolve escalation, the Control Immunity window, cold-resistant enemies, and
Shatter breaking your own Freeze early if you also carry heavy physical damage.

**Note this build carries no other control.** That is not thematic tidiness — Stun and Fear draw
on the same Resolve pool, so a second control source would compete with its own Freezes. The
system taught the build to specialise.

---

## Build 4 — Poison / Decay attrition

**Base + Prefix + Suffix:** Operative · Venomous · Absolutely No Refunds

| Step | |
|---|---|
| **Materials** | **Scorpion Queen Venom** (toxicity 91, solubility 66) · **Obsidian** (hardness 88, mass 34) · **Frog Hide** (toxicity 48, flexibility 66) |
| **Fabrication** | `form.dagger` — edge: Obsidian · coating: Scorpion Queen Venom · grip: Frog Hide. Distill → Steep |
| **Genome** | toxicity **77** · hardness **69** · solubility 51 · decay **28**. Trait *Envenomed (73)* |
| **Eligible** | Toxin **T1** · Physical **T2** · Decay **T4** (28) · Heat/Cold **locked** |
| **Rolled** | `34% chance to Poison on hit` **[104]** · `+58% increased Poison damage` **[110]** · `+4 maximum Poison stacks` **[117]** · `Convert 25% of Physical to Toxin` **[184]** · `+22% increased ailment duration` **[112]** · **Signature:** `Poison does not expire below 25% Health` **[116]** |

**Moves & statuses:** Poison stacks freely (max 20, +4 here); the Venomous prefix's toxin-burst
mechanic consumes the whole stack on a heavy action.

**The loop:** Apply, apply, apply, then decide — burst the stacks now with a heavy hit, or let
them ride and reapply? Below 25% Health the poison becomes permanent and the target is already
dead, it just doesn't know yet. **This build ignores armour entirely** (conversion to a lane
armour does not touch) and is the answer to heavily armoured enemies.

**Counterplay:** toxin resistance, short fights, enemies that cleanse, high healing.

---

## Build 5 — Heavy Stagger

**Base + Prefix + Suffix:** Juggernaut · Seismic · Mandatory Overtime

| Step | |
|---|---|
| **Materials** | **Adamantine Ore → Ingot** (hardness 94, mass 88) · **Mammoth Tusk** (mass 74, hardness 66) · **Rope** (flexibility 54) |
| **Fabrication** | `form.warhammer` — head: Adamantine · haft: Mammoth Tusk · binding: Rope. Smelt → Alloy |
| **Genome** | mass **84** · hardness **86** · flexibility 21. Trait *Unyielding (79)* |
| **Eligible** | Stagger **T1** · Crushing **T1** · Physical **T1** · every reactive family **locked** (heat/cold/charge/toxin all < 10) |
| **Rolled** | `+38 stagger power` **[22]** · `+46% increased stagger with Crushing` **[23]** · `+27% increased Crushing damage` **[10]** · `+29% Stun buildup` **[120]** · `52% more damage against Frozen` **[193]** · `Controls you apply also apply Vulnerable` **[126]** |

**Moves & statuses:** stagger power *is* control buildup toward **Stun**, spending the same
Resolve pool Freeze and Fear would — so this build competes with itself if it also tries to
Freeze. It doesn't; it has no cold at all.

**The loop:** Slow, committed swings. Each one dumps buildup into Resolve, and the enemy frame
shows it filling. When Resolve breaks, Stun lands, `[126]` adds Vulnerable, and the whole window
is free damage. Juggernaut's Momentum builds from damage dealt *and taken*, so standing in the
open is correct.

**Why the shared pool matters here:** against trash (Resolve 20) two swings are enough; against a
boss (Resolve 300, escalating) it takes a committed sequence and then a lockout. The same weapon
reads as a crowd-clearer and as a patient boss tool without a single conditional affix — and the
Resolve bar makes "one more hit and it breaks" legible while it happens.

**Note what the genome forbids:** this weapon **cannot roll a single elemental affix**, at any
price, from any crafting operation. It is a purely physical object and the material made it so.
That is the anti-universal-best-item rule working.

---

## Build 6 — High-risk, low Health

**Base + Prefix + Suffix:** Warlock · Masochistic · Questionable Ethics

| Step | |
|---|---|
| **Materials** | **Grave Soil** (decay 78, arcane 44) · **Deathcap** (toxicity 84, decay 61) · **Tainted Water** (decay 55, solubility 72) |
| **Fabrication** | `form.focus` — core: Grave Soil · reagent: Deathcap · binding: Tainted Water. Distill → Attune |
| **Genome** | decay **74** · arcane **49** · toxicity **58** · resonance **52** · essence.necrotic **36**. Traits *Wasting (64)*, *Bound Opposition (41)* |
| **Eligible** | Decay **T1** · Necrotic Exotic **unlocked** · Recovery **locked** (growth 2) |
| **Rolled** | `47% more damage while below 35% Health` **[191]** · `+16% resistances while below 35% Health` **[192]** · `55% of damage taken is dealt to Mana instead` **[187]** · `Gain 12 Mana on kill` **[133]** · `18% chance to Fear on critical hit` **[122]** · **Signature:** `Killing a Feared enemy resets your cooldowns` **[246 pattern]** |

**The loop:** Deliberately stay low. `[187]` turns Mana into a second health bar, and Warlock's
Debt makes power cheap in Mana — so the build spends itself into a corner and profits from being
there. Masochistic converts damage taken into spendable currency, but **healing destroys it**,
which is why there is not a single recovery affix here.

**Why this build is only possible in this game:** it requires a genome with high decay, high
arcane and **near-zero growth** — a material profile the reaction algebra produces naturally when
you focus decay, because off-channel dilution washes growth out. You cannot buy this item; you
have to have made a genuinely rotten material.

---

## Build 7 — Block / Stored Retaliation *(active retaliation)*

**Base + Prefix + Suffix:** Bastion · Clockwork · The Last Laugh

| Step | |
|---|---|
| **Materials** | **Mithril Ingot** (hardness 76, mass 41, resonance 55) · **Bear Hide** (mass 34, flexibility 58) · **Ironwood Log** (hardness 64, flexibility 44) |
| **Fabrication** | `form.shield` — face: Mithril · rim: Ironwood · strap: Bear Hide. Alloy → Quench |
| **Genome** | hardness **72** · mass **48** · resonance **51** · flexibility 47. Trait *Truesteel (61)* |
| **Rolled** | `+6 ticks to Perfect Block window` **[67]** **(Exotic)** · `0.28× block damage multiplier` **[65]** · `Gain 14 Stamina on Block` **[132]** · `Return 34% of damage mitigated` **[98]** · `On Perfect Block, refund full Stamina and gain 12 Gauge` **[164]** · **Signature:** `Stored Retaliation` **[102]** |

**How it differs from Build 1:** Build 1 retaliates *passively and constantly*. This one **banks**
mitigated damage and releases it on your next attack. That converts defence into an offensive
rhythm — block, block, block, **swing** — and it is completely proc-safe, because the release is
a normal attack at depth 0.

**The loop:** Read telegraphs. Perfect Block refunds everything and fills Guard. Bad blocks still
bank damage. When the pool is large, swing. Clockwork's Cadence rewards the metronomic
block-block-block-swing rhythm the affixes already create.

---

## Build 8 — Resource engine

**Base + Prefix + Suffix:** Invoker · Galvanic · Unlicensed Surgery

| Step | |
|---|---|
| **Materials** | **Storm Core** (charge 96, arcane 52, resonance 68) · **Copper Ingot** (conductivity 88) · **Canvas** (flexibility 61) |
| **Fabrication** | `form.focus` — core: Storm Core · winding: Copper Ingot · wrap: Canvas. Attune → Forge Infusion |
| **Genome** | charge **86** · conductivity **83** · resonance **71** · arcane **49** · essence.storm **44** |
| **Rolled** | `+34% increased Gauge generation` **[139]** · `Gain 6 Mana on Block` **[131]** · `Gain 9 Stamina on crit` **[130]** · `0.78× Mana costs` **[137]** · `Gain Gauge when you apply a status` **[135]** · **Exotic:** `Spending 25+ Stamina Empowers your next attack` **[140]** |

**The loop:** Every action pays for the next one. Invoker's Intensity ramps while channelling and
drains continuously; this item makes the drain affordable, so channels run longer, which ramps
harder. Galvanic converts every one of those spends into Charge.

**The failure mode this build is testing:** four independent resource-generation sources plus a
cost reduction is exactly the kind of engine that trivialises resource management. The caps do
the work — `resource.cost.mult` floors at 0.40, and the on-event grants are flat and
ICD-limited, so the engine can *sustain* but cannot *accelerate without bound*.

---

## Build 9 — Corrosion armour-strip *(the anti-tank)*

**Base + Prefix + Suffix:** Vanguard · Dissonant · Improper Safety Procedures

| Step | |
|---|---|
| **Materials** | **Sulfurcap** (corrosion 81, toxicity 44) · **Witchbog Brine** (corrosion 69, solubility 84) · **Flint** (hardness 79) |
| **Fabrication** | `form.axe` — edge: Flint · etch: Sulfurcap · quench bath: Witchbog Brine. Steep → Quench |
| **Genome** | corrosion **76** · hardness **66** · solubility 58 · toxicity **38** |
| **Rolled** | `31% chance to Corrode on hit` **[108]** · `Adds 17 Corrosion damage` **[6]** · `Hits apply Physical Exposure` **[35]** · `+19 armour penetration` **[32]** · `Ailments you apply spread to a nearby enemy` **[114]** · `+22% increased ailment duration` **[112]** |

**The loop:** Corroded stacks strip Armour and physical resistance; Physical Exposure eats the
target's *overcapped* physical resistance (it applies **before** the cap, unlike penetration);
armour penetration handles the rest. Against an unarmoured target this build is mediocre. Against
a plated elite it is the only thing that works.

**Why it matters:** it proves defences have counters. A Build-1 Thorn Tank meeting a Build-9
player is a genuine rock-paper-scissors moment rather than a stat check.

---

## Build 10 — Arcane, unresistable

**Base + Prefix + Suffix:** Kineticist · Psionic · Terminal Curiosity

| Step | |
|---|---|
| **Materials** | **Mana Prism** (arcane 88, resonance 79) · **Silver Ingot** (conductivity 71, resonance 48) · **Linen Thread** (flexibility 66) |
| **Fabrication** | `form.focus` — prism: Mana Prism · frame: Silver Ingot · binding: Linen Thread. Attune ×2 |
| **Genome** | arcane **82** · resonance **76** · conductivity 55 · **no essence** |
| **Rolled** | `Adds 19 Arcane damage` **[7]** · `Convert 35% of Magic to Arcane` **[185]** · `+14% increased Spell damage` **[12]** · `+8% crit chance with Spells` **[20]** · `Reveals enemy resistances on encounter start` **[233]** · `+22 max Mana` **[47]** |

**The loop:** Arcane damage has **no resistance lane** (`damage-and-defense.md` §2.5). Nothing
this build does can be resisted, exposed, inverted or immune-walled. In exchange, nothing can be
*amplified* either: no arcane ailment, no arcane penetration, no essence anchor, no
lane-conditional damage bonus.

**This is the "reliable floor" build** — the one you take into a Realm whose resistances you
cannot predict, and the reason arcane is a property and not an element. It also carries `[233]`,
which is thematically perfect: the build that ignores resistances is the one that tells you what
they were.

---

# Part 2 — Four profession tools

## Tool 1 — The Fishing Specialist Rod

| Step | |
|---|---|
| **Materials** | **Bogwillow Log** (flexibility 84, mass 22, growth 51) · **Linen Thread** (flexibility 71, hardness 30) · **Wolf Fang** (hardness 78) |
| **Fabrication** | `form.rod` — shaft: Bogwillow · line: Linen Thread · hook: Wolf Fang |
| **Genome** | flexibility **79** · growth **44** · hardness **41** (hook slot only) · mass 24 · resonance 8 |
| **Eligible** | Fishing interval **T1** (flexibility 79) · Doubling **T2** (growth 44) · Bait preservation **T3** (hardness 41) · Supernatural catch **locked** (resonance 8) |
| **Rolled** | `0.87× Fishing interval` **[203]** · `+15% bait preservation` **[205]** · `+11% chance to double the catch` **[206]** · `2.2× rare fish weighting` **[208]** · `1.25× Mastery XP` **[212]** · **Trigger:** `18% chance to recover bait after a failed cast` **[215]** |

**Why bogwillow.** It is a *bad* material for a sword — floppy, light, no hardness. In a rod
shaft, where `stat_map.action_interval` reads `flexibility` at weight 0.6 and `mass` at −0.4, it
is close to ideal. **Same library, different form, opposite verdict** — the design goal, achieved
with no tool-specific content.

**Resolution:** see Example F.

---

## Tool 2 — The Rare-Ore Pickaxe

| Step | |
|---|---|
| **Materials** | **Adamantine Ingot** (hardness 94, mass 88) · **Ironwood Log** (hardness 64, flexibility 44) · **Stormglass** shard (charge 88, resonance 62) |
| **Fabrication** | `form.pick` — head: Adamantine · haft: Ironwood · inlay: Stormglass |
| **Genome** | hardness **86** · mass **71** · resonance **34** · charge **29** |
| **Rolled** | `+31 Harvest penetration` **[210]** · `+14% chance to double ore` **[207]** · `2.6× rare ore weighting` **[209]** **(resonance 34 gates T2)** · `0.93× Mining interval` **[204]** *(a weak roll — mass 71 fights it)* · `1.15× Mastery XP` **[212]** |

**The genetic tension, visible in the roll.** Adamantine gives a T1 harvest-penetration ceiling
and a **bad** interval ceiling, because interval reads `−mass`. You cannot have both from one
material. A Mithril head would swap those. **That is a real crafting decision with no correct
answer**, which is what the design is for.

`harvest_resistance` is an authored property on every material with role `Sourcing` that
**currently nothing reads**. This tool is what gives it a job.

---

## Tool 3 — The High-Quality Smithing Hammer

| Step | |
|---|---|
| **Materials** | **Mithril Ingot** (hardness 76, mass 41, resonance 55) · **Deer Antler** (hardness 61, flexibility 38) · **Ember Core** (heat 100, instability 90, conductivity 62) |
| **Fabrication** | `form.hammer_tool` — head: Mithril · haft: Deer Antler · core inlay: Ember Core |
| **Genome** | hardness **71** · resonance **49** · heat **38** · mass 44 · instability **31** |
| **Rolled** | `+22 Craftsmanship` **[219]** · `0.79× Integrity cost` **[220]** · `+12% chance of an exceptional fabrication` **[221]** · `+9% input preservation` **[218]** · **Exotic:** `Biases the reaction channel toward thermal properties` **[224]** · `0.72× outcome variance` **[223]** |

**The two that change the game, not the numbers:**
- **`[224]` channel bias** — the same reagents in the same order now land in a *different region
  of state space*. This hammer changes **what you can invent**, not how fast. It is the single
  most valuable tool affix in the design and it costs one coefficient in `ReactionCoefficients`.
- **`[223]` narrowed variance** — `emergent-item-system.md` §12.3 already says high skill narrows
  variance to zero and low skill scatters you into neighbouring buckets. A precision tool does the
  same thing. Note the *deliberate* tradeoff: narrow variance makes you reliable and makes you
  **discover fewer accidents**. A sloppy hammer is a better exploration tool.

**Invariant guard:** these move rates and costs. They never touch bounds — convergence still
cannot exceed the strongest input, potency is still a weighted mean, integrity is still
monotonically non-increasing (`profession-tools.md` §5).

---

## Tool 4 — The Alchemy Apparatus

| Step | |
|---|---|
| **Materials** | **Glass** (solubility 8, hardness 44, insulation 61) · **Silver Ingot** (conductivity 71, resonance 48, affinity 66) · **Wax** (insulation 74, solubility 41) |
| **Fabrication** | `form.apparatus` — vessel: Glass · condenser: Silver Ingot · seal: Wax |
| **Genome** | affinity **58** · insulation **62** · resonance **41** · solubility 29 |
| **Rolled** | `1.6× catalyst effectiveness` **[222]** · `+14% potency retention` **[226]** · `+11% ingredient preservation` **[218]** · `1.4× chance of an unusual reaction result` **[228]** · **Exotic:** `Biases the reaction channel toward biological properties` **[225]** |

**`[226]` potency retention is the interesting one.** `emergent-item-system.md` §6.1 makes potency
a **weighted mean**, so adding a junk input always lowers it — the anti-God-Ingot rule. This affix
does not break that rule; it *softens the penalty*, which lets an alchemist afford one
experimental reagent in a chain without wrecking the batch. **A tool that buys you permission to
experiment** is a better tool affix than a tool that buys you a bigger number.

`[228]` widens signature-reaction matching tolerance — the apparatus finds more accidents.
Paired with the *sloppy* hammer above, that is a coherent "explorer's kit" versus the
"production kit" of `[223]` + `[226]`.

---

# Part 3 — Worked resolution traces

All traces use the pipeline in `damage-and-defense.md` §3 and the resistance order in §4.2.
These are the **golden tests** — the assertions are the whole trace, not the final number.

---

## Example A — Penetration + resistance inversion

> **Setup.** Enemy: `60% Heat resistance`. Player: `20 Heat penetration`, `25% chance to invert
> Heat resistance`. Incoming packet: **Magic/heat 50**.

### A1 — inversion does not proc (75% of the time)

```
Resistance (heat)
  1 sum          0.60
  2 exposure     0.60   (none)
  3 cap          0.60   (max 0.75 — not binding)
  4 invert       0.60   ✕ did not proc (roll 0.61 ≥ 0.25)
  5 ignore       0.60   (none)
  6 penetrate    0.40   (−0.20)
  7 floor        0.40   (≥ −1.00)
  8 multiplier   ×0.60
Damage        50 → 30
```

### A2 — inversion procs (25%)

```
Resistance (heat)
  1 sum          0.60
  2 exposure     0.60
  3 cap          0.60
  4 invert      −0.50   ✔ PROC — max(−0.60, INVERSION_FLOOR −0.50)
  5 ignore      −0.50   (skipped: only applies to positive resistance)
  6 penetrate   −0.70   (−0.20)
  7 floor       −0.70
  8 multiplier   ×1.70
Damage        50 → 85
```

**Read-out.** A proc is **2.83× the non-proc damage**. Expected damage across both branches is
`0.75×30 + 0.25×85 = 43.75`, i.e. **1.46× the penetration-only build**. Strong, spiky, bounded.

**The floor is doing real work.** Without `INVERSION_FLOOR`, step 4 would be −0.60 and step 6
−0.80 → ×1.80, and against a 90%-max-res boss it would be ×2.00 — inversion would scale with the
*enemy's* defensive investment, which is backwards.

**Penetration and inversion stack; exposure and inversion do not.** If the player had
`−25 Heat Exposure` instead of penetration: step 2 gives 0.35, step 4 inverts to −0.35, final
×1.35 — *worse* than the penetration version. Correct: they are competing answers to the same
problem and stacking both should not be strictly best.

---

## Example B — Avoidance + resistance + retaliation on the same hit

> **Setup.** Player: `10% chance to negate Charge hits`, `45% Charge resistance`, `On Block:
> retaliate with 20 Charge`. Player is **blocking**. Enemy attack: **Magic/charge 30**.

### B1 — negation fails (90%)

```
 4 Dodge          no
 5 Parry          no capability
 6 Perfect Block  no   (stance began tick 300; impact 314; perfect window 300–304)
 7 Evade          n/a  (telegraphed hit)
 8 Negate charge  ✕    roll 0.44 ≥ 0.10
 9 ⚑ HitLanded  + Blocked   [mech:block, lane:charge]
10 Crit           no
11–15 offence     30 (unchanged)
16 Armour         n/a — packet type is Magic
17 Resistance     charge 0.45 → cap 0.45 → ×0.55  →  16.5
18 Damage taken   ×1.00
20 Block          ×0.40  →  6.6
21 Floor          6.6 → 7
22 Barrier        none
23 Apply          Health −7            ⚑ DamageDealt / DamageTaken
24 Mitigated      prevented 23         ⚑ DamageMitigated
26 Ailments       Shock: enemy has no Shock chance
28 Procs (d1)     On Block: retaliate 20 Charge → enemy   [can_trigger = false]
```

### B2 — negation succeeds (10%)

```
 8 Negate charge  ✔ roll 0.06 < 0.10 — the only packet is removed
   ⚑ HitAvoided [via:negate]
   — resolution ends. No HitLanded. No Blocked. No damage. No ailment.
   — the On-Block retaliation DOES NOT FIRE.
```

### B3 — the same hit, perfectly blocked

```
 6 Perfect Block  ✔  (stance began tick 312; impact 314; perfect window 312–316)
   ⚑ HitAvoided [via:perfect_block]  +  ⚑ Blocked [mech:perfect_block]
   — no damage. No HitLanded. No DamageTaken.
28 Procs (d1)     On Block: retaliate 20 Charge → enemy       ✔ FIRES
                  Perfect Block: refund block stamina + 12 Guard   ✔ FIRES
```

**Why the on-block retaliation fires here but not in B2.** `Blocked` is raised by *both* block
outcomes (D-06 §6.2), so timing the block well is rewarded twice — full negation **and** the
retaliation. Negation in B2 is not a block at all, so nothing on-block hooks it.

**The teaching point.** Avoidance is *better* than mitigation for survival and *worse* for a
retaliation build, because avoided hits produce no `HitLanded` and therefore no generic thorns. A
player stacking lane negation **and** *retaliate when hit* is working against themselves. But
*retaliate when you Block* is immune to that trap, because it hooks `Blocked`. Two similarly
worded affixes with genuinely different build implications — discoverable from the rules, not
authored as a special case.

---

## Example C — Can retaliation trigger resource effects?

> **Setup.** Player: `Thorns can apply Shock` **[100]** and `Gain 8 Stamina when you apply a
> status` **[135]**. An enemy hits the player.

```
d0  Enemy attack effect                                     depth 0
d0  ⚑ HitLanded (on player)                                 depth 0, can_trigger ✔
      └─ rule "thorns" matches (0 < MAX_PROC_DEPTH 2) → fires at depth 1
d1     effect 1: damage 14 Physical → enemy
              ⚑ DamageDealt  depth 1, can_trigger ✕   [mech:retaliation — rule 4]
              └─ nothing matches. Thorns damage cannot proc on-hit effects.
d1     effect 2: applyStatus status.shock → enemy
              ⚑ StatusApplied  depth 1, can_trigger ✔
              └─ rule "stamina on status" matches (1 < 2) → fires at depth 2
d2        effect: grantResource Stamina +8
              ⚑ ResourceGenerated  depth 2, can_trigger ✕
              └─ nothing may match. CHAIN TERMINATES.
```

**Answer: yes — exactly one level deep, and it always terminates.**

**The distinction that makes this safe:** retaliation *damage* is flagged `can_trigger = false`,
but the retaliation rule's **own declared effects** (the Shock rider) run normally. So the affix
does what its tooltip says, and the *damage* it produces cannot become a second hit. If both were
blocked, `[100]` would be a lie; if neither were, the fusion loop the brief warned about would be
live.

**The fusion chain, explicitly:** *thorns → counts as hit → triggers Shock → Shock triggers
retaliation → retaliation triggers thorns* is broken at step 2. Thorns damage does not raise
`HitLanded`, so "Shock triggers retaliation" never has a hit to hook.

---

## Example D — Chill into Freeze

> **Setup.** Enemy: Resolve 120 (elite), currently **Chilled**. Player lands a cold hit carrying
> 55 Freeze buildup, with a `+44% Freeze buildup` affix.

```
Hit resolves → 34 Cold damage applied
26 Ailments / controls
     Chill      refresh_highest — existing 22% ≥ new 19% → existing kept, duration refreshed
     Freeze     gate: requires status.chill  ✔ present  (unchilled → no buildup at all)
                buildup +55 × 1.44 = +79
                Freeze buildup 46 → 125   ≥ Resolve 120   ⚑ FREEZE LANDS
                  · buildup(freeze) → 0
                  · Control Immunity 60 ticks — blocks ALL controls, not just Freeze
                  · Resolve 120 → 150  (escalation +25%, rest of encounter)
                  ⚑ StatusApplied status.freeze
```

**The relationship, stated:** Chill is the **gate**, Freeze is the **payoff**. Freeze buildup does
not accumulate at all on an unchilled target — so cold is a genuinely two-step aspect, which is
what distinguishes it from heat (immediate DoT) and charge (immediate disruption).

**Note what landing it just cost.** The immunity window blocks the player's next Stun and Fear
too, and Resolve is now permanently 25% higher for this fight. Investing in one control type is
strictly better than spreading across three (`statuses.md` §4.4).

**Shatter.** While Frozen, the target takes increased physical damage, and a large physical hit
**breaks the Freeze early**. So a cold/physical hybrid build must choose: hold the lock, or cash
it. That choice is the reason Freeze and Stun are different statuses rather than one.

---

## Example E — A boss under repeated Fear, Stun and Freeze

> **Setup.** Boss: **Resolve 300**, immunity **60 ticks**, escalation **+25%**.
> Player attempts Stun (stagger 120/hit), Freeze (140/application), Fear (90/application).

```
t=000  Stagger  120 → buildup(stun)   120 / 300              ⚑ ControlResisted
t=020  Freeze   140 → buildup(freeze) 140 / 300              ⚑ ControlResisted
t=040  Stagger  120 → buildup(stun)   232 / 300  (decay −8)  ⚑ ControlResisted
t=060  Stagger  120 → buildup(stun)   344 ≥ 300   ⚑ STUN LANDS
                       buildup(stun) → 0 · Immunity 60t · Resolve 300 → 375
t=065  Fear     — IMMUNE. No buildup added.                  ⚑ ControlResisted
t=080  Freeze   — IMMUNE.                                    ⚑ ControlResisted
t=120  immunity ends. buildup(freeze) has decayed 140 → 62
t=140  Freeze   140 → 202 / 375
t=180  Freeze   140 → 334 / 375                              ⚑ ControlResisted
t=220  Freeze   140 → 458 ≥ 375   ⚑ FREEZE LANDS
                       Immunity 60t · Resolve 375 → 469
                       3rd control needs 469; 4th needs 586.
```

**What the boss fight feels like.** The first control lands in ~3 seconds. The second takes ~8.
The third takes ~15. **Crowd control is never useless and never a lock** — it is a window you
earn, then earn again more expensively, until you stop building around it mid-fight and start
using it to punctuate.

**That in-fight arc is the thing Resolve buys** that a flat diminishing-returns ladder cannot: the
same build feels different at t=60 and at t=600 of the same fight, without any authored phase
change.

**Three properties, all from one mechanism:**
1. **Immunity blocks *all* controls**, so Fear → Stun → Freeze rotation cannot keep a target
   locked.
2. **Buildup is per-type but the threshold is shared**, so investing in two control types is worse
   than investing in one.
3. **Escalation is per-encounter and uncapped**, so there is no number of stacks that beats a boss.

`ControlResisted` fires on every attempt that fails to cross and every attempt into immunity —
which is why `[76]` and `[160]` (gain Resolve / deal damage when you resist control) are worth
carrying on a boss-hunting build.

---

## Example F — Fishing resolution with the specialist rod

> **Setup.** Tool 1. Fishing 34, Mastery 41. Action: Deep Pool Cast (base interval 120,
> input Riverworm ×1, output Silverfin ×1, rare table: Glass Eel w4 / Mirrorcarp w2).

```
Fishing — Deep Pool Cast                        [Fishing 34 · Mastery 41 · Tidecaller Rod]
 1 Eligibility   ✔ level 34 ≥ 20 · rod equipped · Riverworm ×1 in bag
 2 Interval      120 × 0.87 (rod [203]) × 0.90 (mastery 41)  →  94 ticks
 3 ⚑ ActionStarted  [domain:profession, profession:fishing]
 4 Consume       Riverworm: preserve roll 0.11 < 0.18  →  PRESERVED
                   (0.15 rod [205] + 0.03 mastery, diminishing)
 5 Success       n/a — this action has no failure mode
 6 Primary       Silverfin ×1
 7 Doubling      roll 0.07 < 0.11  →  Silverfin ×2         [206]
 8 Bonus         none declared
 9 Rare weight   Glass Eel 4 → 8.8 · Mirrorcarp 2 → 4.4    [208] ×2.2
                 rare roll 0.86 → no rare this cast
10 Quality       Silverfin potency 42 → 46  (rod [211] +9%)
11 Bias          none
12 ⚑ OutputProduced ×2   [class:edible]
13 XP            +48
14 Mastery       +12 × 1.25 = +15                          [212]
15 ⚑ ActionCompleted  [domain:profession, profession:fishing]
16 Procs (d1)    "second bite" [216-pattern] — not on this rod
 ⇒ Silverfin ×2 (potency 46) · Riverworm preserved · 48 XP · 15 Mastery
```

**Note stage 12 raising one event per output.** That is the single hook `duplicateOutput` needs,
and it is shared by gathering, crafting **and** loot — so `[206]` "chance to double the catch"
and a hypothetical "chance to double loot drops" are the same effect handler with a different
condition.

**Note stage 16 exists at all.** A profession action runs the *same* trigger engine as combat,
at the same depth, with the same proc rules. That is the payoff of the shared `ActionCompleted`
vocabulary (`moves.md` §4.2).

---

## Example G — Conversion ordering *(the rule that differs from PoE)*

> **Setup.** Weapon: 40 Slashing base. `+60% increased Physical damage`, `+40% increased Heat
> damage`, `Convert 30% of Physical to Heat`. Enemy: 0% physical resistance, 50% heat resistance,
> Armour 0.

```
11 Flat added     Slashing 40
12 Increased      Physical +60%  →  Slashing 64
                  (heat increases have nothing to apply to yet)
14 Conversion     30% of 64 = 19.2 moves to the heat lane
                  → Slashing 44.8   ·   Slashing/heat 19.2
17 Resistance     Slashing 44.8 × (1 − 0.00) = 44.8
                  Slashing/heat 19.2 × (1 − 0.50) = 9.6
                                                    ⇒ total 54.4
```

**The rule, in one sentence:** *increases apply to the lane the damage started in.* The `+40%
increased Heat damage` did **nothing** here, because no damage started as heat.

**Why this is right, and what it costs.** PoE converts *before* increases and lets both lanes'
multipliers apply, which is the single most-asked-about interaction in that game. Our ordering is
one sentence with no exceptions.

The cost, stated honestly: "convert to heat then stack heat increases" is not a scaling strategy
here. **Conversion is a defensive-lane tool** — hit them where they're weak — and against this
enemy it was actively *bad* (54.4 vs 64 unconverted). Against a 50%-physical / 0%-heat enemy it
would be the opposite. That is a real, legible decision.

**[UNRESOLVED — U-6]** if conversion feels too weak in play, the fix is a small `more` multiplier
on converted damage, **not** reordering the pipeline.

---

## Example H — Armour vs resistance against two different hits

> **Setup.** Player: Armour 40, Physical resistance 30%. Two incoming Crushing packets.

```
A swarm hit — Crushing 8
 16 Armour       reduction = 40/(40 + 5×8) = 0.50   →  4.0
 17 Resistance   ×0.70                              →  2.8
 ⇒ 2.8   (65% total reduction)

A telegraphed smash — Crushing 60
 16 Armour       reduction = 40/(40 + 5×60) = 0.118 →  52.9
 17 Resistance   ×0.70                              →  37.1
 ⇒ 37.1  (38% total reduction)
```

**The two layers have genuinely different jobs**, which is the whole reason to have both:

| | Armour | Resistance |
|---|---|---|
| Against 8 damage | −50% | −30% |
| Against 60 damage | −11.8% | −30% |
| Job | **attrition** — the long Realm run | **spikes** — the telegraphed smash |

Under the *current* code (`max(1, damage − armour)`), the swarm hit becomes **1** and the smash
becomes **20 → ×0.70 = 14** — armour is total against chip and enormously strong against the
smash too, with a cliff in between. The diminishing formula removes the cliff and creates a real
gearing question: **do you fear the swarm or the smash?**

---

# What these examples establish

| Claim | Shown by |
|---|---|
| Material choice determines what an item *can* be, not just how big its numbers are | Builds 5, 6, 10 (families **locked** by genome) |
| The same material is excellent in one form and useless in another | Tool 1 (bogwillow), Tool 2 (adamantine's tension) |
| Defences have counters; no build is a stat check | Build 9 vs Build 1 |
| Deep interactions are explainable from ordered rules | Every trace in Part 3 |
| Proc chains terminate by construction | Example C |
| Crowd control is a window, never a lock | Example E |
| Combat and professions share one vocabulary | Example F stages 3, 12, 15, 16 |
| Strong effects are bounded without being neutered | Example A (`INVERSION_FLOOR`) |
| Avoidance and retaliation anti-synergise — a discoverable, un-authored interaction | Example B |
