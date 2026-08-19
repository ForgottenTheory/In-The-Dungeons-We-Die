# Game Overview — the top-down map

> **What this is.** The whole game on one map: what it is, how the systems connect, and how far
> each one actually got. Read this first; follow the links when you need the detail.
>
> **What this is not.** Not a specification. Every rule, formula and roster lives in the deep
> docs (`docs/GDD.md` and the per-system docs). Nothing here overrides them. Not an
> implementation document either — for code, see `docs/code-map.md`.
>
> Last synced with the repo: **2026-08-17** (854 tests, 0 build warnings), after the
> Hideout station split.

## Status marks used throughout

| Mark | Meaning |
|---|---|
| ✅ **BUILT** | In the game, running, covered by tests |
| 🟡 **PARTIAL** | Real but incomplete — works, but a load-bearing piece is missing |
| 📐 **DESIGNED** | Decided and specified, **no code yet** |
| ❓ **UNRESOLVED** | An open question. Do not assume an answer exists |

Nothing below is described as existing unless it does.

---

# 1. What the game is

**In The Dungeons We Die** is a progression-heavy **extraction RPG**.

You assemble a strange character out of parts, train persistent professions in a home base,
invent materials nobody designed by combining things in a crafting bench, forge those materials
into equipment, and carry it into a hostile Realm. Loot you find there is **not yours until you
walk out with it**. The defining question is always:

> *"I have valuable loot and I'm still alive. Do I leave, or push deeper?"*

Tone is grim-funny — a lethal dungeon run by something with a sense of humour and a filing
system. That is why character modifiers read like citations and workplace incidents.

### The five pillars

1. **Emergent crafting, not recipes.** A universal reaction algebra computes what any
   combination produces. There is no recipe table anywhere.
2. **Builds you assemble, not classes you pick.** Base + Prefix + Suffix = 18,750 characters,
   none hand-authored.
3. **Tick-based tactical combat.** Continuous simulation, not turns. The clock runs while you
   think. Skill is reading a telegraph and answering it in time.
4. **Layered, persistent progression.** Melvor Idle is the reference: many overlapping reasons
   you got better, not one number.
5. **Extraction risk.** Realm loot is unsecured. Death forfeits it. Progression survives.

### The guiding question, applied to every feature

> *"How does this make preparing for, exploring, surviving, mastering, or extracting from a
> Realm more interesting?"*

---

# 2. The core loop

```
                          ┌──────────────── HIDEOUT ────────────────┐
                          │                                          │
                          │  Train professions (active or passive)   │  ✅
                          │  Gather / process raw materials          │  ✅
                          │  Transform materials at the bench        │  ✅
                          │  Fabricate materials into equipment      │  ✅
                          │  Modifiers roll onto that equipment      │  ✅
                          │  Assemble your build (Base+Prefix+Suffix)│  ✅
                          │  Prepare the run: loadout, pack, briefing│  ✅
                          └──────────────────┬───────────────────────┘
                                             │  enter
                                             ▼
                          ┌──────────────── REALM RUN ───────────────┐
                          │                                          │
                          │  Explore a spatial location graph        │  ✅
                          │  Gather biome resources (time = risk)    │  ✅
                          │  Fight with your moveset, on the clock   │  ✅
                          │  Harvest anatomy → crafting inputs       │  📐
                          │  Reach a depth checkpoint                │  ✅
                          └───────────┬──────────────────┬───────────┘
                                      │                  │
                        ┌── EXTRACT ──┘                  └── GO DEEPER ──┐
                        │  loot secured to Stash  ✅       better rewards,│
                        │                                  more danger  ✅ │
                        ▼                                                 │
                  back to the HIDEOUT ◄───────────────────────────────────┘
                        ▲
                        │  (death: unsecured loot lost, progression survives) ✅
```

**In one line:**

```
Realm → Gather → Transform Materials → Fabricate → Generate Equipment
      → Build Character → Fight → Extract → Improve → Push Deeper
```

**The loop is closed and runs end to end today**, preparation included: the Realm tab opens on a
preparation screen where you set your loadout, pack supplies and read what Realm Knowledge has
taught you, then enter (D39). What is still thin there is **profession tools** — the trades are
shown, the worn tools are E6.

### The risk rhyme — one decision at three scales

The same shape is deliberately repeated. Preserve it.

| Scale | The decision |
|---|---|
| **Realm** | Extract now, or go deeper? |
| **Crafting** | Refine once more, or commit this material before it's destroyed? |
| **Combat** | Spend the resource now, or hold it for the telegraph you can see coming? |

---

# 3. Character identity ✅ BUILT

```
Species   +   Base   +   Prefix   +   Suffix
   🟡           ✅          ✅          🟡
```

- **Species** — physiology. 🟡 3 exist as thin stat packages against a designed roster of 10.
- **Base** (15) — the progression chassis: growth weights, an optional gauge, a starting kit.
  **Never a license** — anything a Base starts with, another layer may grant later.
- **Prefix** (25) — one recognisable mechanic (Feint, Charge, Toxin stacks, Recall…).
- **Suffix** (50, 10 fully expressed) — a rule-breaker allowed to reach outside combat, with one
  expression per channel and a stated drawback.

**Three load-bearing rules, each enforced by validation and tests:**

1. **Every Base distributes the same growth budget** (4.0 attribute points/level). Only the
   shape differs — so Base choice is a trade, never a menu with bigger options.
2. **A Prefix may never name a Base.** Prefixes hook *events*, so a Bastion galvanises by
   blocking and a Wizard by releasing a hold. Break this and 25 mechanics become 375.
3. **Formatting never touches mechanics.** Nine name grammars (citation / warning / medical /
   liability…) change how a build reads and can never change how it plays.

**Expression channels** — Suffixes express through **Strike** (you landed a hit), **Guard** (you
avoided/absorbed/protected) or **Surge** (you spent/accumulated/sustained a resource). Keyed to
events every build produces, so no Suffix is unusable by any Base.

**Attributes** (7): Strength · Dexterity · Intelligence · Constitution · Wisdom · Endurance ·
Luck. **Resources**: Health (no combat regen) · Mana · Stamina, plus at most **two gauges**
(one from the Base, one from the Prefix).

❓ **Unresolved:** Species' mechanical role · the Fighter's identity hook (its old engine,
"moveset comes from the weapon", was universalised for everyone) · the remaining 40 Suffix
mechanics · respec pricing (designed as "costly but accessible", but there is no currency).

---

# 4. Progression ✅ / 🟡

Progression runs on **multiple independent tracks**. There is deliberately no single
"Character Level" that represents everything.

| Track | Persistent? | Lost on death? | Status |
|---|---|---|---|
| Profession levels & XP | Yes | No | ✅ |
| Per-action Mastery | Yes | No | ✅ shortens intervals, raises bonus + opportunity chance, talks risk down |
| Realm Knowledge (per realm) | Yes | No | ✅ Five insights unlock at 6/12/20/30/42: enemy weaknesses, hazards, rich nodes, **hidden routes**, extraction routes. Options only — never damage |
| Crafting discoveries | Yes | No | ✅ |
| Equipment owned | Yes | Gear is safe by default | ✅ |
| Character level & attributes | Yes | No | 📐 growth weights exist; **nothing awards XP** |
| **Unsecured Realm loot** | **No** | **Yes** | ✅ |

**Horizontal over vertical**: progress should unlock options, routes and material combinations —
not merely bigger numbers.

**The circle:** profession progress → better preparation → better Realm performance → better
extraction → better materials → better crafting → deeper Realms → new profession opportunities.

**Active vs passive is a standing rule everywhere:** passive is automatic, reliable, lower yield,
lower ceiling, fewer rare outcomes. Active rewards real performance, never merely "clicking
Active Mode" — which is why active play carries the Discover → Pursue decision (§5) and passive
structurally cannot roll for one.

📐 Designed, not built: preservation · doubling · mastery unlocks · cross-skill bonuses ·
global passives.

---

# 5. Professions & gathering ✅ BUILT (all 20)

**Full detail: `docs/professions.md`.** The short version:

**Gathering (7)** Mining · Forestry · Fishing · Farming · Hunting · Beast Lore · Salvaging
**Processing (9)** Smithing · Herblore · Alchemy · Cooking · Leatherworking · Tailoring ·
Fletching · Artifice · Runecrafting
**Utility (4)** Thieving · Agility · Cartography · Assay

**348 actions · 32 opportunities · 12 course obstacles.** XP/levels, per-action mastery,
level-gated ladders, and one execute path behind all twenty.

**Three modes, and the difference between them is structural, not a number:**

- **Passive** — automatic, reliable, lower yield, and it *never rolls for opportunities*.
- **Offline** — a first-class path, not a courtesy. Whatever passive action is running when you
  close the game keeps running, at the same rate, through the same execute path. **Levelling
  never requires being at the keyboard.** Capped at 12h.
- **Active** — a timing score, plus **Discover → Pursue / Ignore**: an active attempt can
  surface an offer (a rich vein, a shape under the boat, an unattended satchel, an unmarked side
  path). Pursuing costs real time on the shared tick engine and can be lost to risk. Declining
  costs nothing. One mechanism, twenty flavours, all of it content.

Two professions earned a system of their own: **Farming** (parallel Hideout plots that grow
while the game is closed) and **Agility** (a five-slot training course whose configuration *is*
your standing travel/hazard/extraction loadout).

Interconnection is enforced by test, not intent: every Processing profession consumes another's
output; no profession is a dead end; Hunting produces carcasses and only Beast Lore opens them;
only Cartography teaches Realm Knowledge; every seed has a wild source.

```
Mining → Smithing → ingots → Artifice → lenses → Assay reads deeper
Hunting → carcass → Beast Lore → hide → Leatherworking → fabrication
Cartography → survey chart → Salvaging finds the ruin worth digging
Assay → property dossier → the three deepest crafting actions require one
```

📐 Not built: profession **tools** (two worn slots — Artifice and Smithing make the components
now, the slots are E6) · bow/projectile forms · Cooking's consumers (consumable forms) · course
bonuses are declared and displayed but nothing reads them yet.

---

# 6. Materials — the ingredient set ✅ BUILT

**1448 material definitions** on a **0–100 property scale**.

- Authored biome-by-biome as a *design lens* — there is deliberately **no biome field**.
- **Mundane-majority** (oak, iron, salt, spring water), so the rare things stand out by their
  property *profile* rather than by tier.
- **Never MMO tiering.** Oak / Ironwood / Emberwood / Frostpine all fill the "wood" role and
  behave differently. There is no "Iron < Better Iron".
- Creatures and plants yield **multiple parts** — a wolf gives hide, fur, meat, bone, fang, blood.

### The 21 properties, by role

| Role | Properties | Behaviour in crafting |
|---|---|---|
| **Structural** | hardness, mass, flexibility, affinity, conductivity, insulation, solubility, resonance, instability | Blend slowly toward a mass-weighted mixture. Define *what the material is* |
| **Reactive** | heat, cold, charge, toxicity, growth, decay, corrosion, arcane | Transfer along a process channel; subject to opposition. Define *what it does to things* |
| **Response** | heat_resistance, cold_resistance, toxin_resistance | Derived; never a reaction input |
| **Sourcing** | harvest_resistance | Inert in crafting; read only by gathering |

Distinctions that must stay intact: `heat`/`cold`/`charge` (influence introduced) vs resistances
(influence resisted) · `charge` (energy) vs `conductivity` (transmission) · `toxicity` (attacks
life) vs `corrosion` (attacks material) · `affinity` (willingness to bond) vs `instability`
(unpredictability). **Affinity is the single most important gate in the crafting engine.**

### Tags

Namespaced `family:value`: `origin:` · `comp:` · `form:` · `state:` · `rarity:` · `class:` ·
`part:`. **Rarity means availability, not power.**

---

# 7. Material transformation — the crafting bench ✅ BUILT

The signature system, and the most complete one in the game.

### The core claim

**The engine is a total function, not a lookup.** Every combination of substrate, reagents and
process produces a real, named, stackable result — always. Authored content is **eight
processes**, a byproduct table and a name grammar. There is **no recipe table and no
per-combination rule anywhere**.

### The shape of a craft

```
Substrate (the thing being transformed)
  ← Step 1: Reagent A   ⇒ intermediate state
  ← Step 2: Reagent B   ⇒ intermediate state
  ← Step 3: Reagent C   ⇒ final state
  + optional Catalyst   (not consumed; modifies rates, transfers nothing)
  + a Process           (decides which properties react at all)
```

**Order matters automatically**, because step 2 acts on a different intermediate state. Six
outcomes from three reagents, with zero authored triples — and the player can inspect the
intermediate and predict the next step.

### The eight processes

| Process | Profession | Medium | Severity | Opens |
|---|---|---|---|---|
| Grind | *ungated* | mechanical | 0.30 | solubility, mass, hardness |
| Steep | Herblore 1 | solvent | 0.20 | heat, toxicity, growth, cold, decay |
| Distill | Herblore 12 ⚠ | solvent | 0.50 | toxicity, arcane, decay, corrosion, solubility |
| Smelt | Smithing 1 | thermal | 0.60 | hardness, mass, conductivity, heat |
| Quench | Smithing 5 | thermal | 0.35 | cold, hardness, flexibility |
| Alloy | Smithing 10 | thermal | 0.45 | hardness, mass, conductivity, flexibility, affinity |
| Forge Infusion | Smithing 15 | thermal | 0.55 | heat, charge, hardness, affinity |
| Attune | Alchemy 10 ⚠ | arcane | 0.35 | resonance, arcane — the vessel for essence |

> ⚠ These two are the **designed** gates. Both are ungated in the shipped content right now, for
> playtesting — see §14.

**Media** explain *why* an ingredient suits a process: solvent releases by `solubility`, thermal
by `instability`, mechanical by inverse `hardness`, arcane by `resonance`. This is why Ember Sap
is an alchemy reagent and Ember Core is a forge reagent.

### The algebra, per reagent step

1. **Acceptance / release** — how willingly the substrate bonds (`affinity`), how readily the
   reagent gives up what it carries (the medium's property).
2. **Channel convergence** — on-channel properties move a *fraction of the remaining gap* toward
   the reagent's value. **They never add, and can never exceed the strongest input.** This one
   rule kills unbounded stat escalation permanently, with no caps or fudge factors.
3. **Off-channel handling** — structural properties blend slightly toward a mass-weighted
   mixture; reactive properties **dilute toward zero and receive nothing**. Each transformation
   *focuses* a material and washes the rest out, which is what stops deep materials carrying 25
   nonzero properties.
4. **Opposition** — opposed pairs (heat/cold, growth/decay) annihilate, releasing strain.
   **You cannot stockpile opposites.**
5. **Floor pruning** — trace values are pruned to zero.

### The meta fields

- **Potency (1–100)** — how strongly the material expresses. A **weighted mean, never a sum**,
  so adding junk *lowers* it. Capped at `best input + 8 × skill`. Consequence: a high-potency
  mundane material beats a low-potency exotic one, so base resources stay relevant forever.
- **Integrity (0–100)** — a **transformation budget**, not durability. Cost scales with how
  violent the change was, so gentle well-chosen steps cost little and brute force costs a lot.
  **Elegant crafting is mechanically rewarded** — this is the main skill axis.
- **Integrity 0 destroys the material.** Fair only because three things are guaranteed: the
  projection shows cost and result **before** commitment; below ~25 integrity it shows a
  destruction *chance* rather than false certainty; and destruction yields **byproducts** (Slag,
  Cinders, Dross, Residue) that are useful reagents. A blown craft is a setback with a
  consolation prize, never a zero.
- **Generation** — a depth counter for naming and valuation. Not a gate; integrity is the gate.

### Identity and naming

A result is **quantized → hashed into a canonical signature → registered as a stackable runtime
material**. Identical results stack; two players reaching the same state get the same material
with the same name, so discovery is shareable and saves stay small.

**Variance produces different materials, not random stats.** A bad roll gives you a *different,
weaker material with its own name* — possibly one nobody has seen — never "Emberveined Iron (bad
roll)". High skill narrows variance to zero; low skill scatters you across neighbouring buckets,
and you find things by accident.

Names are a **pure function of final state, never of history**. Max 3 words, at most one
intensity adjective, no "of X", no tier words, and numbers never appear. Intensity comes from
vocabulary ladders: `heat → Warmed · Emberlit · Cindered · Searing`.

### The Reaction Log — required scope, not polish

*"A system this deep is only playable if it explains itself."* Every craft emits a structured,
human-readable trace where every line states **why**.

```
Forge Infusion — Iron Ingot ← Ember Core
  Acceptance 0.48 — Iron Ingot resists bonding (affinity 30)
  Release 0.93 — Ember Core gives freely under thermal (instability 90)
  hardness        65 → 62  (channel, rate 0.25)
  heat             0 → 36  (channel, rate 0.80)
  Integrity 90 → 87  (cost 2.6: Δstate 0.50 × severity 0.55)
✦ First discovery: Emberlit Iron ×1
```

### Traits ✅ and Essence ✅

- **Traits** (16) — named, discrete, capped qualitative states (Emberveined, Bound Opposition).
  Cap 3, weakest displaced, authored merge rules let pairs **supersede** into stronger traits.
  Every trait carries a drawback, consumes properties, and charges integrity.
- **Essence** (7: fire/frost/storm/nature/necrotic/radiant/abyssal) — a rare *supernatural* layer
  distinct from mundane reactive properties. Capacity is governed by `resonance`; excess becomes
  **strain**, not a cap — *powerful magic needs a worthy vessel*. `Attune` is the process that
  raises resonance first.
- **`arcane` is a property, never an essence** — it is not an element, it is the medium elements
  travel through.

📐 Not built: signature reactions (30–80 authored spikes against *abstract conditions*) ·
consumable forms · the Codex & Assay layer.

---

# 8. The player-facing crafting language — "icon algebra" ✅ BUILT

> The single most important presentation rule in the project. Full spec:
> `docs/presentation-architecture.md`. Decision **D30**.

**Three languages, one direction:**

```
SIMULATION LANGUAGE          PLAYER CRAFTING LANGUAGE          GAMEPLAY LANGUAGE
0–100 properties,      →     glyph + qualitative tier    →     damage, Armour, Crit,
rates, coefficients,         + intensity + direction           Thorns, Shock, triggers,
severities, weights          + context                         Move modification
```

- **Raw simulation values never lead a normal play surface.** They live behind an *Advanced*
  toggle, the Assay skill, and developer labs.
- **The semantic layer is the only path** from simulation state to player-facing text. It is
  one-way (it may translate, never recompute), deterministic, and unit-tested.
- **Items speak gameplay language.** "Charge 72" is a *cause*, not a reward. An item says
  "+12% Shock chance"; the material property that produced it is shown as influence, not as the
  payoff.
- **A player-facing modifier ships only when its mechanic resolves in play.** Content that
  references unbuilt systems may be visibly inert internally — it may never be *offered* to the
  player.
- **Display metadata is data** (glyphs and glosses on the property definitions), never code
  switches. Display tiers never touch identity quantization.

What the player actually sees at the bench: qualitative tiers (Trace → Extreme) with pips, wear
words, trend arrows derived from the algebra's own typed change kinds, risk bands (SAFE →
DESTROYS), trait proximity hints ("Within reach: Emberveined — needs more Heat"), slot-fit
readings, and item cards/strips.

> Rejected, deliberately: **icons-as-numbers** — *"⚡⚡⚡⚡ is the same problem wearing a hat."*

---

# 9. Fabrication — materials become gear ✅ BUILT

The terminal boundary. Materials stop here; equipment begins.

```
Equipment Form  +  Material(s) in named slots  →  Equipment Instance
```

`Sword` is authored once. Iron Sword, Emberveined Iron Sword and Necrotic Storm Sword are
**never authored at all**.

- **Multi-component from v1.** A form declares named slots (edge / core / binding), each with
  required tags, a **mass share**, and an **aperture** governing how much of each trait category
  that slot may express.
- **Stats read from named slots, never from a blend.** A hard brittle edge on a flexible core is
  a genuinely different weapon from the reverse, computed with zero authored combinations.
- **A stat map** is what makes the same material excellent in one form and useless in another —
  a robe reads flexibility and insulation, plate reads hardness and mass, a staff reads resonance
  and arcane. This is what stops a single "best material" existing.
- **Unexpressed trait magnitude goes dormant** — shown, counted in value, and fully available if
  the material is used in a different form later. Dormancy makes one material interesting in
  several directions rather than optimal in one.
- **The 0–100 ↔ combat-unit reconciliation happens here and only here**, pinned by a parity test
  against the authored Iron Sword.
- **Fabrication is terminal and irreversible**, so it gets the same fairness guarantee the
  reaction bench has: a **pre-commit projection** computed by the same code path that mints the
  real item.

**Built forms: 23 across all 9 slots** ✅, each existing to exercise a different part of the
material system rather than to add variety:

| Form | Slot | What it tests |
|---|---|---|
| **Longsword** (3 slots) | Weapon | the calibration reference — hardness off the edge |
| **Warspear** (3 slots) | Weapon | the counter-example — flexibility off the *haft*, which is 60% of it, so the iron that makes a great sword makes a mediocre spear |
| **Dagger** | Weapon | almost no mass read anywhere — the one weapon where a rare, light, hard material is not wasted on a big lump of steel |
| **Greatsword** | Weapon | reads mass at **1.20** across the whole item — the first form that *wants* to be heavy |
| **Battle Axe** | Weapon | head-heavy: mass is read off the **head alone**, so a dense head on a light haft beats the same metal spread evenly |
| **Maul** | Weapon | mass off the head at **1.40**, the highest weight in the file. Lead is a bad sword and a fine maul |
| **Longbow** | Weapon | **the sharpest counter-example in the game** — flexibility off the limb at 1.10, hardness at 0.20. A bow limbed in iron is a bad bow |
| **Crossbow** | Weapon | the bow's opposite answer: hardness off the *mechanism*, the smallest slot in the file |
| **Quarterstaff** | Weapon | one slot, three reads — the teaching form, where a single material decides everything |
| **Flail** | Weapon | the only form that wants a component to be **floppy**: flexibility off the chain |
| **Halberd** | Weapon | the Warspear's opposite — a spear flexes on the thrust and reads its haft for flex; a halberd is a weight on a lever and wants the haft **stiff** |
| **Shortsword** | Weapon | reads hardness *and* flexibility off one blade — short blades bend rather than break, and a brittle one snaps |
| **Javelin** | Weapon | thrown, so mass is **entirely a cost** (0.20, the second lowest in the file) and the point still has to be hard |
| **Sling** | Weapon | reads **no hardness at all** — it is cord and a pouch, and the stone is not part of the weapon |
| **Whip** | Weapon | flexibility off the lash at **1.35**, the hardest any form reads flexibility off one component. Hardness is read only off the handle, which does no damage |
| **Knuckles** | Weapon | the smallest weapon there is: mass at **0.10**. The form for a material you have almost none of |
| **Buckler** | Offhand | the simplest possible form; declares `parry` |
| **Helm** | Head | the only armour that reads **insulation** hard — where a fur lining beats a better metal |
| **Vest** | Body | the hardness/flexibility trade-off |
| **Gauntlets** | Hands | where being **too hard** is a cost |
| **Treads** | Feet | where being **too heavy** is a cost |
| **Focus** | Trinket | the only form that reads **resonance/arcane**, which is what gives resonant materials anywhere to be excellent. Grants no armour and no moves |
| **Ring** | Ring1 / Ring2 | the only form that reads **conductivity/affinity** — nothing read either before it, so the most conductive metals were strictly worse swords and nothing else. Deliberately *not* resonance, or it would just be a small focus |

A worn loadout's mitigation is the **sum of its pieces**, and how much each contributes is
authored (a helm reads hardness at a lower weight than a vest) rather than coded as a per-slot
multiplier.

**~180 weapon names live on ten weapon forms, as `name_variants`.** A Falchion, a Scimitar and a
Sabre are one blueprint — same slots, same reads, same moves — so they are *names*, not forms.
Which one an item gets is derived from its **signature**, so it is deterministic (the preview
promises the noun the bench mints) and identical for identical materials. A variant is cosmetic
by construction: nothing reads it, so it can never quietly become a mechanical difference. If a
weapon needs to *behave* differently, it needs a form, and the "no two forms are the same form"
rule decides whether it has earned one.

**The two ring positions are the one place the slot is not decided by the item.** Every ring
definition names `Ring1` — a definition must name one slot — so the second ring you put on would
displace the first, and you would own a ring slot you could never fill. `EquipmentSlots.`
`InterchangeablePositions` states that rings fill either position, and `Equipment.`
`EquipInFirstFreePosition` is what every equip path goes through. **Do not author a second
near-identical ring form to fill `Ring2`.**

📐 Not built: consumable forms · form **acquisition** (starter set + profession ladders +
schematics as a knowledge loot class — the schematic *items* drop already) · balance of any of
the above. There is no arcane-category trait yet, so the Focus's arcane aperture gates a category
with no content in it.

---

# 10. Equipment, item stats and the Genome

## 10.1 The two-tier model ✅

- **Definitions** — what a *kind* of item is. Shared, never mutated.
- **Instances** — a specific owned item. **Equipment only.** Materials always stack.

## 10.2 Material Genetics — the Genome ✅ BUILT

Every fabricated item carries a **Genome**, computed once at fabrication and never recomputed:

```
Genome = stat-map-weighted property PRESSURE
       + essence
       + expressed traits + dormant traits
       + tags
       + potency
       + generation depth
       + signatures  (📐 P4)
```

**Pressure** is the key idea: not "how much hardness is in this thing", but *how much hardness
actually reaches the parts of the item that matter*, weighted by the form's own stat map. Same
materials, different form ⇒ different genome. This is the mechanism that stops one globally-best
material existing.

The genome decides **three things** about every possible modifier, all pure functions:

| Lever | Question |
|---|---|
| **Eligibility** | *Can* this modifier roll on this item at all? (hard gate) |
| **Weight** | How *likely* is it? (scales with pressure and essence) |
| **Tier ceiling** | How *strong* can it be? (the best tier the genome qualifies for) |

and **potency** decides *where inside that tier* the value lands — roll quality.

The player sees all of this **before rolling**, because *"engineer the casino" is a lie if you
gamble blind*.

## 10.3 Innates and modifiers (affixes) ✅ BUILT

```
   1–3 INNATES        computed from the genome, deterministic, zero variance,
                      never rerollable — the guarantee that engineering a good
                      material can never produce a total loss
 + ≤3 modifier-PREFIXES  ┐ rolled from weighted, tiered pools;
 + ≤3 modifier-SUFFIXES  ┘ one modifier per family per item
 + Exotic / Signature / Anomalous     📐 (E7 / P4)
```

**44 representative modifiers ship**, across offence, character, defence, resource, ailment,
retaliation, avoidance, penetration, trigger, status-depth and move-modification families.
Every fabricated item rolls its modifiers **from the very first craft** — there is no
"modifiers unlock later" switch. Pacing is emergent: weak early genomes roll 0–1 minor
modifiers.

> **Terminology, enforced.** In code these are `affix.*` in `Dungeons.Affixes`. In
> **player-facing text they are always "modifiers"** — the bare word *Prefix* means only the
> character-identity layer.

A modifier grants one of three things, all reusing vocabulary that already existed:

| Grant | Becomes |
|---|---|
| `stat` | a scoped modifier contribution (with per-modifier provenance) |
| `rule` | a trigger rule attached while the item is worn |
| `moveModifier` | one of the 11 move-rewrite operations |

## 10.4 Crafting operations and Overreach 📐 DESIGNED

- **Operations** (Anneal · Etch · Scour · Reforge · Bind · Temper · Fracture) — paid for with
  materials the game already produces, chiefly the **destruction byproducts of failed crafts**.
  Every operation respects the genome, so the gambling is bounded by the engineering.
- **Overreach** — the final casino: Ruin · Brick · Mutation · Elevation · Exotic Mutation ·
  Transcendence, drawn **only from the item's own genetic families**. A poison dagger can never
  Overreach into a lightning effect, at any odds. **Repeatable with escalating Ruin odds** — the
  fourth verse of the risk rhyme. Anomalous modifiers exist *only* here.

❓ **Unresolved:** durability (the design assumes it; the game has none; recommended deferred).

---

# 11. Combat ✅ BUILT

## 11.1 The model — tick-based, not turn-based (hard constraint)

Combat is a continuous, interval-driven simulation on a shared deterministic tick engine
(20 ticks/second). The clock keeps running while the player deliberates. **There are no turns,
no initiative rounds, no alternation.** *For The King 2* is a reference for tactical readability
and presentation — **not** for its turn structure.

## 11.2 The action lifecycle ✅

```
QUEUE → TELEGRAPH → WINDUP → EXECUTION → RECOVERY → READY
```

Telegraph communicates intent ("Goblin Brute: OVERHEAD SMASH"). Windup is the window where an
action can be interrupted. Recovery creates the counterattack window. Telegraph and windup are
**separate scheduler states**, which is what makes "interrupted mid-swing" expressible at all —
an interrupt records *which phase it cut*.

## 11.3 Moves — one shape for everything ✅

A **Move** represents melee attacks, ranged attacks, spells, defensive actions, utility,
reactions, channels, summons, class abilities and enemy abilities. One data shape.

```
Move = tags + timing + costs + requirements + targeting + packets + effect riders
```

An attack and a spell differ **only in their data**. `MoveKind` exists for dispatch and
filtering; behaviour never switches on it.

- **Movesets compose**, weapon-first: weapon → Species → Base → Prefix → Suffix → learned, every
  grant carrying provenance. Species grant Bare Fists, so nobody is ever moveless.
- **Move modification** is 11 declarative operations, matched by tag or move id, applied in a
  **fixed order** proved independent of source order. *"Heavy Strike gains additional Heat
  damage"* is data, never `if item == ThunderSword`.
- **27 moves ship**, universally available. Gates are physical and conditional — costs,
  `equippedTag`, cooldowns, statuses — **never class**.
- **Techniques** (19 items) teach moves into a persisted learn-order-preserving list.

> **Standing rule, enforce forever (D25):** a class-check condition kind may never be added to
> the rule vocabulary. The interesting question is *"how well can this build make Fireball
> work?"*, never *"is this Base allowed to cast Fireball?"*

## 11.4 Damage and defence ✅

**A hit is a list of packets.** Each packet carries exactly one **damage type** (Slashing ·
Crushing · Piercing · Magic) and zero-or-one **aspect** (heat · cold · charge · toxin ·
corrosion · decay · arcane). A flaming sword resolves as 80 Slashing plus 20 Slashing/Heat —
never as one relabelled hybrid.

**One resistance per packet:** `Lane = aspect ?? type`. Armour applies wherever the packet's
*delivery type* is physical, whatever its aspect — so an aspect can never bypass armour, and
hybrid damage is never taxed twice.

**Eight lanes:** physical · magic · heat · cold · charge · toxin · corrosion · decay.
Slashing/Crushing/Piercing share **one** physical lane; per-type weakness lives on the **enemy**
as a two-way vulnerability multiplier — which is what Realm Knowledge is meant to reveal.
**`arcane` has no lane at all** — unresistable, and structurally unamplifiable in exchange.

**Essence never becomes a lane.** It empowers its anchor aspect, gates supernatural modifiers and
tags effects — *identity and metadata, never a mitigation calculation.*

**Defence layers, in resolution order:**

```
evasion (timed)  →  perfect block (tight window, avoidance, refunds Guard)
                 →  parry (gear-granted; avoidance + counter-window)
                 →  evade (untelegraphed hits only)
                 →  lane avoidance (rare, hard-capped)
                 →  armour   ( armour / (armour + 5 × packet) )
                 →  resistance (sum → cap 75% → penetration → floor −100%)
                 →  block (timed, mitigation)
                 →  damage-taken modifiers
                 →  Barrier absorption
                 →  Resolve (gates controls)
```

Armour is strong against attrition and weak against spikes; resistance is the reverse. Because
overcapping absorbs debuffs but not penetration, resistances display as `capped / raw`.

**Blocking and dodging are timed decisions that cost stamina** and only matter near an incoming
attack's execution tick. **This is the core skill test.** Gear buys the *window* and the *cost*,
never a passive dodge roll, so the skill test can never be priced out.

**Recovery is Barrier, not healing.** No modifier grants passive Health regeneration; Health
remains Realm attrition.

**Every hit emits a stage-by-stage trace** (the Hit Log). A pipeline with this many
multiplicative sources is unplayable if it cannot answer *"why did that hit for 17?"*

## 11.5 Statuses ✅ and hazards 📐

Fully data-driven — **one definition type, no C# class per ailment**. 28 definitions ship in
four categories whose rules differ:

| Category | Ships |
|---|---|
| **Ailments** (damage over time) | Bleed · Poison · Burn |
| **Impairments** (debuff, no damage) | Chill · Shock · Corroded · Weaken |
| **Controls** (prevent or redirect action) | Stun · Freeze · Fear · Silence |
| **States** (tactical markers) | Vulnerable · Guarded · Barrier |

The load-bearing contrast is **Burn vs Poison**: high damage / short / no stacking against low /
very long / stacks to 20. That single pairing is what makes heat and toxin play differently
instead of being reskins. **Freeze requires Chill**, making cold a two-step aspect.
**Burn supersedes Ignite; Chill supersedes Slow** — shipping both of either pair means one is
strictly worse.

**Crowd control is gated by Resolve** — a pool every combatant has. Controls apply *buildup*;
crossing Resolve lands the control, opens a **Control Immunity** window, and raises Resolve
**+25% for the rest of the encounter**. So the first Freeze lands in seconds, the third takes
fifteen, and a boss is **never locked and never immune**. **Stagger folds in** as buildup toward
Stun, so a build cannot Stun-lock *and* Freeze-lock. Player text still reads "12% chance to
Freeze"; the Resolve bar shows the truth.

**Ailments resolve last, from the damage that actually landed in each lane** — so one resistance
number reduces both the hit and the ailment. No second calculation, no second stat.

📐 **Hazards** (poison clouds, fire, falling debris, trap tiles) are designed on the same
tick/telegraph model. Nothing places one yet.

## 11.6 Thorns / retaliation ✅ BUILT

Retaliation is **pure content over the trigger-rule engine — it needed zero new combat
machinery.** The shipped family covers when-hit, on-block, after-dodge, poison barbs, and
reflect-% of mitigated damage.

Two rules keep it from exploding:

- **Ailment ticks can never proc anything.** A Poison tick is not a hit. That single rule kills
  an entire class of damage-over-time proc loops.
- **`Blocked` fires for both block outcomes** (ordinary and perfect), so a *perfect* block still
  retaliates — otherwise the better play would be punished.

📐 Stored retaliation and damage inversion stay with the Exotic tier.

## 11.7 Effects, triggers and proc safety ✅

The spine everything else is built on:

- **31 game events**, synchronous and ordered (a determinism requirement — the sim must replay
  from a seed).
- **Declarative trigger rules**: `event + conditions + effect(s) + cooldown + chance`. Prefixes,
  Suffix expressions, gauge feeds, status hooks and item modifiers are all *just lists of these*.
- **17 condition kinds** (including stateful world reads) and **16 effect kinds**, 11 of which
  combat owns.
- **Proc safety with teeth**: chain identity, a depth budget of 2, once-per-chain by default,
  per-target internal cooldown, and a 64-effect fuse.
- **Effects with no registered handler are recorded, not dropped** — content that references
  unbuilt systems must be **visibly inert rather than silently missing**.

## 11.8 Enemies ✅ framework / 🟡 roster

**Enemy identity composes** — never a bespoke class per monster:

```
Family (physiology)  +  Role (combat archetype)  +  Actor (identity + overrides)
```

- **Family** — what a creature *is*: baseline attributes, resource silhouette, biological lane
  resistances, Resolve. Never behaviour.
- **Role** — what a creature *does*, as **deltas over any family**. `role.brute` is one
  definition whether the body is goblin, undead or construct.
- **AI profile** — a named, reusable brain: weighted rules over the shared condition vocabulary,
  matching moves **by id or by tag** (`moveTag: "mech:stagger"` = "the big hit, whatever it is on
  this body"). AI chooses intent only; the tick engine resolves timing.
- A future **Elite/Boss variant is one more delta through the same fold**, never a duplicated
  definition.

🟡 **Three shipped enemies, ~8 data lines each**: Raider (skirmisher), Brute (armoured, heavy
telegraphs), Hexer (caster, entirely from the universal move library). Target is 8–10, each
chosen to **exercise a distinct combat mechanic** rather than for variety's own sake.

**Ecology rule:** a creature must not merely drop "Enemy Loot". Anatomy maps into the real
material library — hide, bone, gland, venom — so Beast Lore harvesting feeds crafting, which
feeds equipment, which feeds the next run. 📐

## 11.9 Auto-combat 📐 and positioning ❓

- **Auto-combat uses the same rules.** Automation chooses actions; the domain resolves them
  normally. There is deliberately no separate "fake" combat calculator. It is disadvantaged by
  *reaction latency*, never by a damage penalty. The profile machinery exists (enemies use it);
  pointing it at the player does not.
- **Positioning** is explicitly deferred without a decision on whether it is ever coming. Adding
  it later multiplies the design space of every move and every enemy.

---

# 12. Realms & exploration 🟡

Realms are **spatial location graphs**, closer to For The King 2 than to Slay the Spire node
selection. Movement is adjacency-gated; **Depth** measures progression within a run; **Tiers**
are escalating versions of a realm.

**164 realms ship; one is finished.** The Dark Forest is the reference Realm — **31 locations
across 3 depths**, carrying every node kind the architecture has: entrance, travel, gather,
combat, event, descent, extraction, **camp, shrine, merchant, hazard**, plus three **hidden**
nodes that do not exist for a party which has not learned the routes.

| Depth | What it is |
|---|---|
| **1** | Learn the place. **Two** ways out, so leaving is a repeated decision rather than the end of a run. A camp, a hazard, and the hidden Poacher's Cache behind it |
| **2** | The wall. An **elite** (Grask, the Warlord) behind a hazard, the **Hedge Trader** — the first gold sink in the game — a shrine, and an extraction that is deliberately not next to the descent |
| **3** | The payoff. **Thornheart, the Old Growth** — a plant-family boss in a goblin realm, so everything learned on the way down is the wrong lesson — the two richest gathering nodes in the game, and a hidden back door out of the boss room |

Deeper pays **rarer, not merely more**: the average rarity of what depth 3 can hand you is 1.75
against depth 1's 0.78, and a test asserts the direction.

The other 163 are the **roster**: a name, a biome tag set, a tier band and a walkable two-depth
graph (entrance → fork → descent, with a way out at each depth). They deliberately carry **no
combat or gather nodes** — those need actors and profession actions, and wiring encounters is a
later pass. What ships is the map, not the ambush.

**Deliberate direction: make the Dark Forest much richer before adding a second realm.** Once one
realm is genuinely good, adding realms is a content problem instead of repeatedly rebuilding
unfinished systems.

| Location types | Status |
|---|---|
| Entrance · Travel · Gather · Combat · Event · Descent · Extraction | ✅ |
| Camp · Shrine · Merchant · Elite · Boss · Hidden · Hazard | 📐 |

**Realm Knowledge** is persistent per realm and unlocks **information and options, not raw
damage** — enemy resistances, likely hazards, resource-rich areas, hidden routes, extraction
routes. ✅ Five insights on a five-rung ladder (D37/D38). Read **inside** a run as intel, and
**before** one on the preparation screen, where the same thresholds decide what the briefing is
allowed to show (D39).

📐 Not built: realm affixes (Undead Infested, Volatile, Toxic Bloom, Arcane Storm…) · the
campsite · tiers beyond 1 · procedural generation.

---

# 13. Death, extraction, risk ✅ BUILT

- Death **ends the run**.
- **Unsecured Realm loot is lost** — materials, generated materials, drops.
- **Equipped gear is safe** by default. (A "gear at risk" difficulty toggle is designed and off.)
- **Persistent progression always survives**: professions, Realm Knowledge, discoveries, the
  Stash.
- A **starter loadout is always available**, so a fresh or broke character can never be bricked.

**Extraction opportunities should be valuable decisions, not ubiquitous escape buttons.** Some
mechanics deliberately reward *refusing* extraction (the "Of No Return" pattern).

**Loot** ✅ BUILT — full doc: **`docs/loot.md`**. One data-driven table shape serves every source
in the game (enemies, gathering nodes, chests, profession actions): guaranteed drops, independent
chance drops, weighted draws with real misses, quantity ranges, depth/tag conditions, and nested
shared tables. 34 tables ship over the existing 559-material library — **no new materials were
needed**, which is itself the evidence that the ecology was already there.

It follows **D28 — gear comes from the bench; realms drop inputs**:

> *Extraction converts risk into materials; fabrication converts materials into permanence.*

Realm loot is **inputs**: anatomy, salvage (an enemy's crude blade arrives as scrap metal and
rawhide, never as equippable gear), rare property-profile materials, essence-bearing parts,
technique items, catalysts, schematics, coin. A test fails any table that yields finished
equipment. **Relic materials are the chase design** — boss drops with impossible property
profiles that feed the genome machinery instead of bypassing it (📐 content post-slice; the boss
table's `relic_shard` holds the shape). Rare authored **unique gear** is the one sanctioned
exception, and it is fenced: a rule-breaker with a drawback, **sealed** (no genome, no rolls, no
operations), rarer than relic materials, and its end-of-life is Fracture back into components.
Even the exception terminates at the bench.

Three things the loot layer makes structural rather than numeric:

- **Enemy loot composes** family + role + actor, so a new creature is lootable in one line.
- **Active gathering reaches what passive cannot** — a condition, not better odds.
- **Elite/boss spoils are already wired** and fire on a context tag, so authoring the first elite
  needs no code. Nothing carries the tag yet; a test proves the seam anyway.

**Gold** ✅ exists as the sole currency. It lives on the inventory, so coin is unsecured in a Realm
and lost on death exactly like everything else. Nothing spends it — see §15.

---

# 14. Base systems — the Hideout 🟡 PARTIAL

The persistent home, and now a real place rather than an implication. **Twenty stations, one per
profession** — the Forge, the Apothecary, the Alchemy Lab, the Runic Altar, the Workbench, the
Kitchen, the Tannery, the Loom, the Fletcher's Bench, the Assay Table, and one apiece for the
gathering and utility professions (Mineshaft, Timber Yard, Fishing Dock, Garden Plots, Hunting
Lodge, Bone Table, Salvage Yard, Thieves' Nook, Training Course, Cartographer's Desk).

```
Hideout  →  choose a station  →  train it / transform at its bench / assemble at it
```

A station is **routing, not rules**. It says which profession ladders are trained there, which
crafting actions its bench offers, and which blueprints it can assemble — and every one of those
still resolves through the system that always owned it, under the same gate. Hosting decides
*where you stand*, never *whether you may*, and an ungated action can have two homes (Grind: a
mortar at the Apothecary, a mill at the Workbench).

> ⚠ **Temporary:** Distill and Attune are **ungated for playtesting** (2026-08-17) so the Alchemy
> Lab and the Runic Altar can be exercised — their designed gates (Herblore 12, Alchemy 10, listed
> in §7) named a profession neither station trains. Marked in the content and named in the test.

Two rules are enforced at load: **every profession is hosted by exactly one station**, and every
station hosts at least one profession — so a new profession cannot ship unreachable, and no
ladder is ever drawn on two screens that then drift apart.

Farming's plots, Agility's course and Assay's reading table appear at their stations because of
*which profession is hosted*, not because of a flag.

Still undesigned: Hideout upgrades · station upgrades or unlock costs · storage management.

---

# 15. Economy ❓ LARGELY UNDESIGNED

What exists is a barter-of-materials economy: gather → process → craft → use — plus **gold**,
which now drops (🟡) and accumulates and does absolutely nothing. That is deliberate: the brief
for the loot pass was *"gold simply exists as the current currency; do not design the economy
yet."* Having the faucet before the sink is the right order — a currency nothing spends is
harmless, whereas pricing things before knowing what drops is guesswork.

Missing and needed: **merchants/vendors** · **item valuation** (emergent items have no author to
price them, so value must be *computed* from potency, trait rarity, essence, generation and
integrity) · **resource sinks** · whether respec's "Realm currency" is gold or something else.

**Thieving deliberately still yields no coin of its own** — it takes precious metal, gems, keys
and paperwork. Coin is a Realm export, alongside essence (D29.3), and a test holds that line.

---

# 16. Codex & Assay 📐 DESIGNED, NOT BUILT

The knowledge layer. Two surfaces, one skill action:

- **Material proximity hints** — "this is close to something".
- **The fabricated-item Genome Readout** — what the item *could* have rolled and why.

**Assay gates legibility, never capability.** Before the player can Assay, a rolled modifier
renders as an unreadable mark — the standing advertisement for the knowledge layer. Hint depth
scales with profession level. Modifiers still roll and still work; the player just cannot read
them yet.

Also planned here: the discovery journal, the known-rules journal, and player renaming.

---

# 17. Developer content systems ✅ / 📐

**Data-driven content is a pillar, not a convenience.** Everything in `game/data/<type>/*.json`
loads at startup, is **cross-reference validated**, and **fails loudly** — a bad reference breaks
at startup, never mid-play.

| Authored as data | Count |
|---|---|
| Materials | 1448 |
| Properties (with glyph + gloss) | 21 |
| Processes / Byproducts / Traits / Essences / Forms | 8 / 4 / 16 / 7 / 23 |
| Modifiers (affixes) | 44 |
| Moves / Move modifiers / Techniques | 43 / 1 / 19 |
| Statuses | 28 |
| Modifier keys | 51 |
| Bases / Prefixes / Suffixes / Species / Name formats | 15 / 25 / 50 / 3 / 9 |
| Professions / Profession actions / Opportunities | 20 / 348 / 32 |
| Training obstacles (the Agility course) | 12 |
| Hideout stations | 20 |
| Enemy families / roles / AI profiles / actors | 26 / 7 / 7 / 481 |
| Realms / Equipment / Consumables | 164 / 4 / 1 |

**The dividing line:** *code owns structure and closed vocabularies; data owns content
instances.* Damage types, item types, roles and tag families are code. Materials, moves, statuses,
modifiers, enemies and realms are data.

**Existing developer surfaces** ✅: one code-built debug console with tabs (Character · Char Lab ·
Equipment · Hideout · Realm · Combat · Inventory), an always-visible event log, the
**Character Lab** (component swapping with a live diff), the **Crafting Bench** (ordered reagent
chain + live pre-commit projection + Advanced toggle) at the stations that offer it, the Hit Log
toggle, and debug grants.

📐 **Planned labs** — treated as real deliverables, because the combination count makes balancing
impossible without them: Item Lab · Crafting Lab (property-override sandbox, A→B vs B→A
comparison, lineage walker) · Equipment Lab · Profession Lab · Combat Lab · Enemy Lab ·
Move Viewer.

---

# 18. UI 🟡

One code-built developer console. Dark, code-only theme. **No art, audio, animation or telegraph
visuals.** The path from one debug page to real screens is unplanned.

Three surfaces are genuinely designed rather than debug scaffolding: the **Crafting Bench** (the
reagent chain is deliberately literal — "Step 1: Ember Sap, Step 2: Stormglass" — because order
*is* the mechanic and it has to be visible), the **Character Lab**, and the **Hideout** — station
index → station page, with the passive bar, timing sweep and Discover → Pursue card pinned above
both because only one activity can be in flight at a time.

---

# 19. Design principles worth preserving

Each of these has been argued for explicitly and each recurs across systems.

1. **Total functions over lookup tables.** Every input produces a result. Authored content is
   spikes on top of a universal rule, never the rule itself.
2. **Data owns content; code owns structure and closed vocabularies.**
3. **Determinism.** Same inputs + same seed = same outcome. Randomness is confined to a seeded
   source and to as few points as possible — in crafting, exactly two.
4. **Every power has a cost.** Traits consume properties and carry drawbacks; suffix expressions
   state their risk; potency is a mean, so junk dilutes it.
5. **Legibility is required scope.** The Reaction Log and the pre-commit projection are not
   polish — a system that silently eats eight hours of refinement is one players stop
   experimenting with.
6. **Fail loudly at load.** Bad content references break at startup, never mid-play.
7. **Never author a combination.** If a feature needs N × M hand-written entries, the design is
   wrong.
8. **Move requirements are physical and conditional, never identity checks.**
9. **Three languages, one direction.** Complexity belongs underneath; clarity belongs in the
   player's hands.

---

# 20. Status summary

## ✅ BUILT — runnable and tested today

Tick simulation · materials + properties + tags · the full reaction engine (algebra, potency,
integrity, destruction, byproducts, signatures, naming, Reaction Log, projection) · traits ·
essence · fabrication with forms/apertures/dormancy and the scale reconciliation · the semantic
presentation layer · the Genome + 44 modifiers with live grants · the class combinator (18,750
builds) · the modifier vocabulary with scoped contributions · the event bus + trigger rules +
proc safety · 11 effect handlers · the hit pipeline · 28 statuses with Resolve · the Move system
and 27 moves · techniques · the enemy composition framework and 3 enemies · thorns/parry/evade/
barrier · tick combat · 20 professions / 348 actions / 32 opportunities · Farming plots and the
Agility course · offline progress · the Hideout's 20 stations · the Dark Forest · extraction and
death · save/load (schema v7).

## 🟡 PARTIAL — real but a load-bearing piece is missing

Species (3 thin of 10) · Suffixes (10 of 50 expressed) · the Hideout (stations exist; upgrades,
unlocks and storage do not) · **form acquisition** (schematics drop and bind to no form — the one
progression track nothing reads, D29.2) · profession tools (E6) · UI (debug console only, plus
the Realm Preparation screen).

*Mastery, Realm Knowledge and character XP left this list in Phase 8 (D40); loot and the enemy
roster left it in M6.*

## 📐 DESIGNED — decided and specified, not built

Crafting operations + Overreach + Anomalous modifiers · Exotic and Signature modifiers ·
signature reactions · consumable forms · the Codex & Assay layer · profession tools + the yield
pipeline · form acquisition (ladders + schematics) · loot tables with the D28 input-only rule ·
relic materials and sealed uniques · realm affixes and tiers · elite
and boss variants · auto-combat · offline progress · character XP and levels.

## ❓ UNRESOLVED — do not assume an answer exists

| # | Question |
|---|---|
| 1 | **Quantization bucket size** for emergent identity — *the single highest-risk tuning number in the design*. Measured at 67% collapse over 2,800 crafts; provisional |
| 2 | **Integrity budget strength** — currently allows ~20–40 refinements, looser than the "commit-or-lose" fantasy implies. Accept and wait, or tighten now? |
| 3 | **Currency and resource sinks** — nothing designed; respec pricing depends on it |
| 4 | **Species' mechanical role** — the least-developed identity layer |
| 5 | **The Fighter's identity hook** — its engine was universalised for everyone |
| 6 | **Positioning** — in or out? Deferred without a decision |
| 7 | **Durability** — the design assumes it; recommended deferred |
| 8 | **Combat triangle** (melee/ranged/magic advantage) — leaning *no*; enemy vulnerability already provides the counter-play |
| 9 | **Casting-speed attribute scaling** — a low-INT caster is weak and mana-poor but not *slow* |
| 10 | **Integrity excluded from material identity** — reaching the same state by a cheaper path inherits the wrong budget. Judged self-balancing; filed, not fixed |
| 11 | **`transferable` property flag is unused** — give it a job or drop it |
| 12 | **Response properties drop on transformation** — a visible discontinuity, arguably the more honest number |
| 13 | **Balance, wholesale** — every shipped number is breadth-not-balance, deliberately parked until a playtest pass |

---

## Where to go next

| You want | Read |
|---|---|
| Full design detail on anything above | `docs/GDD.md` |
| How the code is laid out, and where to change things | `docs/code-map.md` |
| The experience arc, stage by stage | `docs/how-it-plays.md` |
| The presentation rule and its enforcement | `docs/presentation-architecture.md` |
| The whole crafting stack, in one place | `docs/crafting-overview.md` |
| The crafting engine's mathematics | `docs/emergent-item-system.md` |
| Damage, defence, lanes, thorns | `docs/damage-and-defense.md` |
| Statuses and Resolve | `docs/statuses.md` |
| The Move model | `docs/moves.md` |
| The Genome, modifiers, operations, Overreach | `docs/affixes.md` |
| Why a decision was made (and what was rejected) | `DECISIONS.md` |
| What's implemented, what's next, where we stopped | `PROJECT_STATE.md`, `ROADMAP.md`, `HANDOFF.md` |
