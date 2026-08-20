# Emergent Item System — Design Proposal

> ⚠ **SUPERSEDED AS DESIGN (2026-08-20).** The property/trait/essence model specified here is
> being replaced by the **Identity + Signature system** — design of record:
> `docs/identity-foundation.md` (DECISIONS **D42–D44**). Until that migration lands, this
> document remains the accurate reference for the **code as shipped**; do not design new
> systems or author new content against it.

> **Status: ACCEPTED. P0 implemented (§20); P1–P6 not started.** This document supersedes
> `docs/crafting.md §17`. **P0 shipped:** tag namespacing (`family:value`) across the ~470
> materials, `PropertyDefinition` registry with roles (`game/data/properties/`), the
> `resonance` property, derived resistances (`ResistanceCalculator`), and tag-family +
> property validation in `ContentValidator`. No reaction engine yet.
>
> Grounding: written against the code that exists today — `PropertySet` (string-keyed,
> 0–100), `IItemDefinition` / `MaterialDefinition` / `EquipmentDefinition`, `ItemInstance`
> (quality / derived props / provenance / traits), `DataStore<T>`, `ContentValidator`,
> `EquipmentResolver`, `CraftingExperimentSystem`, and the ~470-material library in
> `game/data/materials/`.

---

## 0. Verdict, and the three decisions everything hangs on

**Is the concept sound? Yes — but only if three specific structural decisions are made
correctly. Get any one of them wrong and the system collapses into either a hardcoded
recipe table or unreadable noise.**

The original pitch has one dangerous idea buried in it. "Ordered property reactions where
`Toxicity → Stability` and `Stability → Toxicity` mean different things" is, if implemented
literally, **a lookup table wearing a costume**. With 19 properties that's 342 ordered pairs
and 5,814 ordered triples of authored content. You would have replaced "50,000 hardcoded
recipes" with "6,000 hardcoded reactions" and called it emergence. It would also be
unlearnable: an arbitrary table can only be memorised, never reasoned about, which directly
contradicts the stated goal.

The fix is three decisions:

### Decision 1 — The engine is a **total function**, not a lookup

Every combination of materials and process produces *a* result. Always. The result is
computed by a **universal algebra** that runs on any inputs. Authored content sits *on top*
of that as rare **Signature Reactions** — spikes that make specific situations special.

- Universal algebra: ~15 rules, covers 100% of inputs, produces the "mundane but real"
  results (blends, transfers, dilutions, annihilations, failures).
- Signature reactions: 30–80 authored rules over the life of the game, each written against
  *abstract conditions* (tags, thresholds, channels), never against item ids.

This is what makes the space genuinely open. The algebra is what a player learns; the
signatures are what a player *discovers*.

### Decision 2 — Order matters because crafting is a **sequence of state transitions**, not a set

Don't model order as an abstract permutation of property names. Model it as what it
physically is:

```
Substrate (the thing being changed)
  ← step 1: apply Reagent A  ⇒ intermediate state S1
  ← step 2: apply Reagent B  ⇒ intermediate state S2
  ← step 3: apply Reagent C  ⇒ final state
```

`A→B→C` differs from `B→A→C` **automatically**, because step 2 acts on a different
intermediate state. You get all six permutations of a triple producing six different
outcomes *for free*, from composed binary steps, with zero authored triples. And it is
*reasonable*: a player can inspect S1, see what it became, and predict step 2. That is the
difference between a system you master and a system you memorise.

Three-property "chains" are therefore not a separate mechanic. They are three steps. If you
later want hand-crafted magic at depth 3, add **Chain Signatures** (§9.2) — a small number
of authored rules that fire when a specific *ordered sequence* occurred. That is the correct
place for authored order-dependence, and it costs a dozen rules, not thousands.

### Decision 3 — A material's identity is a **hash of its resulting state**, and emergent materials are **stackable runtime definitions**, not per-unit instances

This is the one place the current codebase is wrong for this system. `docs/itemization.md`
says "the moment an item's properties diverge from its definition, it becomes an instance."
That means 40 units of the same emergent alloy are 40 unique objects. Inventory, save file,
UI, trade and the codex all break.

Instead: quantize the result state, hash it into a canonical **signature**, and register a
**runtime-generated `ItemDefinition`** under that signature. Consequences:

- Identical results **stack**, like any other material. Save data stays small.
- Two players (or the same player twice) who reach the same state get **the same material,
  with the same name**. Discovery is shareable and meaningful.
- Emergent materials flow through *every existing code path* — `Inventory` stacks,
  `DataStore`, crafting inputs, loot — with no special-casing. This directly satisfies
  "the system should not care whether an input was authored or generated."
- Crafting **variance produces different materials, not random stats on the same material.**
  That is a much better fit for a discovery game (§12.3).

`ItemInstance` remains, and remains correct, for **equipment** — which is genuinely unique
per copy.

---

## 1. The six layers, and what keeps them distinct

A recurring failure mode in systems like this is layers bleeding into each other until
nobody can say what a "tag" is versus a "property". Hard definitions:

| Layer | Answers | Cardinality | Authored or derived | Consumed by |
|---|---|---|---|---|
| **ItemType** | *Which code path owns this?* | exactly 1, closed enum | authored / assigned | engine dispatch, UI, slots |
| **Tags** | *Which rules apply to this?* | many, family-scoped | authored on bases, **derived** on emergent | rule matching, filters, search |
| **Properties** | *How does it behave, numerically?* | 0–100 scalars | authored on bases, **computed** on emergent | reaction algebra, stat translation |
| **Essence** | *What kind of power does it carry?* | small vector, mostly zero | authored (rare), transferred | trait gating, damage typing, magical effects |
| **Traits** | *What did it qualitatively become?* | ≤ 3 (soft 4) | **emergent only** | special behaviours, naming, value |
| **Meta** | *How refined / how strong / how far gone?* | 3 scalars | computed | gating, expression strength, economy |

The load-bearing distinctions:

- **Type vs Tag.** `Ore`, `Metal`, `Plant`, `Crystal`, `Liquid` from the original list are
  **not types — they are tags.** If `Ore` is a type you will write `if (type == Ore)` and
  the system calcifies. Type is a tiny closed set that decides *which subsystem owns the
  object*: `Material`, `Weapon`, `Armor`, `Tool`, `Consumable`, `Component`. That's it.
  Everything else classifies, and classification is tags.
- **Property vs Tag.** A property is a *quantity that participates in arithmetic*. A tag is
  a *predicate used for matching*. `heat: 55` is a property. `form:metal` is a tag. Never
  encode a quantity as a tag ("hot") and never encode a category as a 0/100 property.
- **Property vs Essence.** See §5 — this is the sharpest disagreement I have with the
  original pitch, because your proposed Essences (Fire, Frost, Storm, Nature, Necrotic) are
  **near-duplicates of properties you already authored** (`heat`, `cold`, `charge`,
  `growth`, `decay`).
- **Property vs Trait.** Properties are continuous and always present (implicitly zero).
  Traits are discrete, rare, named, capped, and *cost* properties to exist.

---

## 2. Property system

### 2.1 The honest assessment of your proposed list

Your brainstorm list (Thermal, Conductivity, Solubility, Hardness, Flexibility, Density,
Toxicity, Reactivity, Instability, Affinity, Growth, Essence, Potency, Refinement Integrity)
has four problems:

1. **`Potency`, `Refinement Integrity` and `Essence` are not properties.** They are meta
   fields and a separate vector. Mixing them into the property bag means the reaction
   algebra will accidentally treat "how refined is this" as something that can be alloyed.
   Keep them off `PropertySet`.
2. **`Reactivity` and `Instability` overlap** and, worse, `Instability` should be *partly
   derived* — a material with contradictory internals should become unstable **because** of
   that contradiction, not because someone typed a number. (§6.3)
3. **`Affinity` was undefined** in your list. Your existing codebase already defines it well
   ("willingness to form stable bonds with foreign materials"). Keep that definition — it's
   the single most important gate in the whole engine.
4. **`Thermal` is ambiguous** (emits heat? conducts heat? resists heat?). Your shipped data
   already solved this correctly by splitting `heat` / `heat_resistance` / `insulation`.

### 2.2 Recommendation: keep your shipped vocabulary, add two things, and add metadata

I strongly recommend **against** replacing the property set. You have ~470 materials
authored against it, the distinctions in `itemization.md §2` are well-reasoned
(`charge` vs `conductivity`, `toxicity` vs `corrosion`, `affinity` vs `instability`), and
the split-pair model (`heat`/`cold` as separate 0–100 values rather than one signed axis) is
actually *better* than signed axes for this system: a material can carry both, and that
contradiction becomes a source of strain and rare traits (§8.6). Signed axes would silently
delete that.

**Keep all 19 existing properties.** Add:

| Add | Meaning | Why |
|---|---|---|
| `resonance` | capacity to *hold and channel* supernatural energy | Stands to `arcane`/Essence exactly as `conductivity` stands to `charge`. Without it, essence has no carrying capacity and nothing stops magical stat-stacking. |
| `cohesion` *(optional, see below)* | how tightly the material holds itself together | Only add if playtesting shows `flexibility` can't carry brittleness. Default: **don't add it**; brittle = high `hardness` + low `flexibility` already works. |

**Reclassify** (no data change, metadata only):

- `harvest_resistance` → role **Sourcing**. It describes how hard the material is to *obtain*,
  not how it behaves in a crucible. It must be excluded from the reaction algebra entirely,
  or every craft will "alloy the difficulty of mining."
- `toxin_resistance` → fold into the derived-resistance model below; keep the constant for
  save compatibility, stop authoring it.

**Stop authoring Response properties; derive them.** Authoring `heat_resistance`,
`cold_resistance`, `corrosion_resistance`, `charge_resistance`, `toxin_resistance`… across
470 materials is a content treadmill and a source of inconsistency. Instead, each *Reactive*
property definition declares what resists it, and resistance is computed:

```
resist(X) = clamp( authored_override
                 ?? Σ over contributors c of (value(c.property) × c.weight) )
```

e.g. `heat` is resisted by `insulation` (0.5) + `heat_resistance` (1.0) + `mass` (0.15).
Existing authored values become overrides and keep working.

### 2.3 Property **roles** — the thing that makes 20 properties survivable

Twenty properties in a reaction engine sounds unmanageable. It isn't, because **only 2–4 are
live in any given process** (§7.2). But the engine needs to know *how* each property
behaves. Promote properties to first-class data (`game/data/properties/*.json`), each with:

```jsonc
{
  "id": "heat",
  "name": "Heat",
  "role": "reactive",            // structural | reactive | response | sourcing
  "family": "thermal",
  "opposes": "cold",             // mutual-annihilation partner, optional
  "resisted_by": [ { "property": "heat_resistance", "weight": 1.0 },
                   { "property": "insulation",      "weight": 0.5 } ],
  "dilutes": true,               // off-channel: drifts toward 0 rather than blending
  "transferable": true,          // can be moved by a reagent at all
  "floor": 5                     // below this it is pruned to 0
}
```

Role semantics in the algebra:

| Role | Properties | Behaviour |
|---|---|---|
| **Structural** | `hardness`, `mass`, `flexibility`, `affinity`, `conductivity`, `insulation`, `solubility` | Blend toward a mass-weighted mixture. Change slowly. Resist transformation. These define *what the material is*. |
| **Reactive** | `heat`, `cold`, `charge`, `toxicity`, `growth`, `decay`, `corrosion`, `arcane` | Transfer readily along a process channel. Subject to opposition/annihilation. These define *what the material does to things*. |
| **Response** | `*_resistance` | Derived; never a reaction input, always a reaction output. |
| **Sourcing** | `harvest_resistance` | Inert in crafting. Read only by harvest/gathering systems. |

This also satisfies the project invariant that "new material properties are data, not code."

### 2.4 What was cut from your list, and where it went

| Proposed | Disposition |
|---|---|
| Thermal | → `heat` + `cold` + `heat_resistance` (already shipped, better) |
| Density | → `mass` (already shipped) |
| Reactivity | → merged into `affinity` (willingness to bond) + `instability` (unpredictability) |
| Solubility | kept, but reinterpreted as one of several **transfer media** (§7.3) |
| Growth | kept; its opposite `decay` already exists as a separate property |
| Essence | promoted out of properties into its own vector (§5) |
| Potency / Refinement Integrity | promoted out of properties into Meta (§6) |
| Affinity | kept, with your existing definition, and given the central gating role |

---

## 3. Item Types

Keep the enum tiny and behavioural. Proposed final set:

`Material` · `Component` · `Weapon` · `Armor` · `Tool` · `Consumable`

- **Material** — participates in transmutation; stackable; has a `MaterialProfile`.
- **Component** — a fabricated part (blade, haft, binding) that slots into equipment but
  isn't equippable itself. Optional; add only when multi-part fabrication lands (§16.3).
- **Weapon / Armor / Tool** — equippable, instance-based, has a form template.
- **Consumable** — has an effect payload, consumed on use.

Everything else you listed (Ore, Metal, Plant, Wood, Fungus, Bone, Hide, Liquid, Crystal,
Gem, Food, Potion, Catalyst) becomes tags. "Catalyst" in particular is a **role in a
process**, not a kind of object — any material can be a catalyst if the process accepts it
in the catalyst slot.

---

## 4. Tags

### 4.1 Namespace them by family

Your shipped tags are a flat bag: `["raw", "elemental", "fire", "mote", "rare", "magical"]`.
That works today and will not survive rule-matching, because `fire` could mean an origin, an
essence, a hazard, or a biome. Migrate to `family:value`:

| Family | Cardinality | Examples |
|---|---|---|
| `origin:` | 1–2 | `flora` `fauna` `fungal` `mineral` `elemental` `arcane` `synthetic` |
| `comp:` | exactly 1 | `organic` `inorganic` |
| `form:` | exactly 1 primary (+ modifiers) | `ore` `metal` `wood` `bark` `fiber` `hide` `bone` `crystal` `glass` `powder` `liquid` `gas` `sludge` `salt` `ash` `resin` |
| `state:` | exactly 1 | `raw` `refined` `processed` `alloy` `extract` `distillate` `composite` `spent` |
| `rarity:` | exactly 1 | `common` … `exceptional` |
| `class:` | many | `magical` `fuel` `venomous` `edible` `monster` |
| `part:` | many | `mote` `core` `gland` `horn` `sap` |

Family cardinality rules are enforceable by `ContentValidator` and by the tag-derivation step.
`form:` exclusivity is what stops emergent items being simultaneously a liquid and an ingot.

The migration is mechanical — a one-time scripted rewrite of the seven material files plus a
validator rule.

### 4.2 Tags on emergent items are **derived, never inherited wholesale**

If tags were carried forward, generation 5 materials would have forty tags. Instead, run a
`TagDerivation` pass after every transformation, from three sources in priority order:

1. **Process assertion** — the process declares tag edits: Smelt sets `state:refined` and
   clears `state:raw`; Grind sets `form:powder`; Distill sets `form:liquid`,
   `state:distillate`; Alloy sets `state:alloy`.
2. **State thresholds** — declarative rules over the resulting property/essence vector:
   `growth ≥ 50 → comp:organic`; `toxicity ≥ 55 → class:venomous`;
   `arcane ≥ 30 or any essence ≥ 25 → class:magical`. Destruction byproducts (§6.2c) are
   tagged `state:spent`.
3. **Lineage carry** — a small whitelist of tags that survive ancestry: `origin:*` carries
   from the dominant lineage root at reduced weight; `part:*` never carries.

Result: tag count on emergent items is naturally bounded at roughly 6–9, and tags always
describe *what the thing is now*.

---

## 5. Essence

### 5.1 The problem with Essence as pitched

Your proposed Essences — Fire, Frost, Storm, Nature, Necrotic, Arcane — map almost
one-to-one onto reactive properties you already shipped: `heat`, `cold`, `charge`, `growth`,
`decay`, `arcane`. If both exist with no sharp distinction, every material needs two numbers
meaning nearly the same thing, every reaction rule doubles, and players will reasonably ask
why Fire Essence and Heat are different fields.

Two defensible resolutions. **I recommend B.**

**A. Cut Essence.** Reactive properties do the job. `arcane` becomes the "is this
supernatural" scalar. Simplest; loses a flavour axis.

**B. Essence is the rare *supernatural* layer; reactive properties are the mundane one.**
A burning coal has `heat: 70` and no essence. An Ember Core has `heat: 100` *and*
`essence.fire: 60`. Heat burns things; Fire essence **ignites without fuel, empowers fire
magic, is required to unlock magical traits, and types damage as magical rather than
physical.** This fits your existing design intent exactly: the library is deliberately
*mundane-majority*, so essence being present on only ~40 of 470 materials makes those
materials genuinely special rather than "the same but bigger."

### 5.2 Model (Recommendation B)

- A small vector, stored separately from `PropertySet`: `essence: { "fire": 60 }`.
- **No `Mundane` essence.** Mundane is the absence of essence — don't store 430 zeros.
- **The set is purely *typed*:** `fire` `frost` `storm` `nature` `necrotic` `radiant`
  `abyssal`. **There is deliberately no `arcane` essence** — see 5.2.1.
- Each essence definition declares an **anchor property** it amplifies and is amplified by:

| Essence | Anchor | Opposes |
|---|---|---|
| fire | `heat` | frost |
| frost | `cold` | fire |
| storm | `charge` | — |
| nature | `growth` | necrotic |
| necrotic | `decay` | radiant / nature |
| radiant | — (anchors on `resonance`) | necrotic / abyssal |
| abyssal | `corrosion` | radiant |

The anchor link is what lets essence participate in the property engine **without a second
subsystem**: a process that moves `heat` also moves `essence.fire`, at a lower rate.

### 5.2.1 `arcane` is a property, never an essence  **[DECIDED]**

An "Arcane essence" meaning *generic magic* would be the untyped default masquerading as a
type — a weak member of a typed list, and a straight duplicate of the `arcane` property you
already authored on ~40 materials. So: **the property `arcane` keeps its name and all its
existing authored values (zero migration), and there is no arcane essence.** Arcane is not an
element; it is the medium elements travel through.

To stop it being vague, `arcane` gets four concrete jobs:

1. **Gate** — magical traits and signatures require `arcane ≥ threshold` **or** any essence
   present. It is the "is this supernatural at all" switch.
2. **Amplifier** — essence effect strength scales with `arcane`:
   `essence_expression = essence_value × (0.6 + 0.4 × arcane/100)`. Fire essence in a mundane
   host burns weakly; in an arcane-charged host it burns properly. This is why Sunflare Shard
   (`arcane: 30`) and Phoenix Ash (`arcane: 50`) differ from ordinary fire sources.
3. **Load** — `arcane` counts toward strain against `resonance` capacity (§5.3), so raw
   magical charge also demands a worthy vessel.
4. **Damage typing** — supplies untyped force/magic damage when no essence types the effect.

`arcane` is unipolar (no opposition partner) and role `reactive`.

### 5.3 The capacity rule — essence's real job

```
essence_capacity = resonance × 1.5          (resonance 0–100 → capacity 0–150)
total_essence    = Σ essence values
strain           = max(0, total_essence − essence_capacity)
```

Essence beyond capacity does **not** simply cap. It becomes **strain**, which feeds
effective instability (§6.3) — the material bleeds, is unpredictable in further crafting,
and may spontaneously discharge. This gives the classic and very readable fantasy: *powerful
magic needs a worthy vessel.* It is also a hard brake on essence stacking that isn't an
arbitrary cap.

Improving `resonance` (via a dedicated process) is therefore a real crafting goal, and
"attune the vessel first, then infuse" becomes learnable player technique.

---

## 6. Meta fields: Potency, Refinement Integrity, Volatility, Generation

These live on the material archetype, **not** in `PropertySet`.

### 6.1 Potency — an *expression coefficient*, computed as a weighted mean

**Definition: Potency (0–100) scales how strongly this material's properties, essence and
traits express when it is used** — in combat stats, consumable effects, and as a reagent.

```
base    = Σ (input.potency × role_weight)      // role_weight sums to 1.0
result  = base × quality_mult                   // 0.85 .. 1.12
ceiling = max(input.potency) + 8 × quality_norm // hard cap, quality_norm 0..1
potency = min(result, ceiling, 100)
```

Why this shape:

- **Weighted mean, never a sum.** Adding a junk input *lowers* potency. The "God Ingot #847"
  loop — feed everything into one item until arithmetic gives up — is dead on arrival. You
  can only raise potency by using *better* inputs.
- **The `max(input) + 8` ceiling** encodes a plain, teachable rule: *refinement can improve a
  material a little; it cannot conjure quality from nothing.* Climbing from 40 to 90 requires
  many generations, and the Integrity budget won't allow it (§6.2). That's the intersection
  that closes the loop.
- Role weights come from the process (e.g. substrate 0.65 / reagent 0.30 / catalyst 0.05), so
  potency can't be gamed by dumping quantity into a slot.

Consequence worth stating: a **high-potency mundane material beats a low-potency exotic one**
in practice. Base resources stay economically relevant forever, which is what you want in a
game with professions and gathering.

### 6.2 Refinement Integrity — a *transformation budget*, not a use-counter

**Definition: Integrity (0–100) is how much further structural transformation the material
can tolerate.** It is explicitly *not* durability, hardness, condition, or stability.

Three rules make it interesting rather than a wall:

**(a) Cost scales with how violent the change was, not with how many times you crafted.**

```
Δstate       = Σ over properties |after − before| / 100
integrity_cost = (Δstate × severity_of_process × 12)
               + (traits_created × 4)
               + (strain_released × 0.3)
               − (skill_mitigation)
```

A gentle, well-chosen step that achieves its goal precisely costs little. A brute-force
process that thrashes half the vector costs a lot. **Elegant crafting paths are mechanically
rewarded** — this is the main skill-expression axis of the whole system.

**(b) Low integrity is the *frontier*, not a wall.**

```
effective_instability = instability + strain + (100 − integrity) × 0.4
```

Low integrity → high effective instability → **wider outcome variance**. Deep-generation
materials are a gamble, not a dead end. Crucially, **some of the best traits are only
reachable in high-variance states** — you cannot reach them safely. That is exactly the
risk/reward loop your extraction game already runs on, applied to the workbench.

**(c) Integrity 0 = the material is destroyed.**  **[DECIDED]**

A transformation that would take integrity to 0 or below **destroys the units submitted to
that craft** (not the rest of the stack). Integrity 0 is therefore a *terminal event*, never
a state you hold in inventory — there are no inert 0-integrity items sitting around.

This is a harder rule than "the material is finished but still usable", and it is only
acceptable if three things are true. All three are **required scope**, not polish:

1. **Destruction is never a surprise.** The craft UI must show the **projected integrity
   cost and the resulting integrity** *before* the player commits, and must show an explicit
   destruction warning when the projection reaches zero. A system that silently eats
   eight hours of refinement is a system players stop experimenting with — which defeats the
   entire design goal.
2. **Destruction is never total loss.** A destroyed material yields **byproducts** —
   `Slag`, `Residue`, `Ash`, `Spent <root>` — determined by its dominant form tag and
   surviving properties, at reduced potency. This is already the intent of
   `docs/crafting.md §14` ("failure, partial recovery, strange byproduct, discovery clue").
   Some byproducts should be genuinely useful reagents in their own right, so a blown craft
   is a setback and a consolation prize, not a zero.
3. **The edge is a visible risk band, not a hidden cliff.** As integrity falls, variance
   rises (6.2b), so the *projected* cost has a spread. Below ~25 integrity the UI shows a
   **destruction chance percentage** rather than a certainty. Pushing a
   generation-5 material is then a legible gamble the player chooses to take.

**The tension this creates is the best thing about the rule, and should be leaned into.**
Because a destroyed material cannot be fabricated, every refinement step is a live
question: *push one more transmutation, or commit and forge it now?* Integrity stops being a
counter and becomes a **commit-or-lose resource-management decision**, made once per
material, under uncertainty. That is a strong core loop and it rhymes with the extract-or-go-
deeper decision the realm layer already runs on.

Corollaries:

- **Fabrication requires only that the material exists** (integrity ≥ 1). Do *not* add a
  separate minimum-integrity floor for forging — that would be a second cliff stacked on the
  first, and would make the last transmutation punishing rather than tense.
- Fabrication itself is terminal and consumes the material regardless of remaining integrity.
- There is **no `state:inert` tag** and no "Set" state. Remove both from the tag families.
- Optional later: an `Anneal` process restoring integrity at the cost of potency or a trait.
  Given destruction is now permanent, this becomes a more valuable economy hook than it was —
  but still not v1.

### 6.3 Volatility — derived, not authored

Keep authored `instability` (you have 470 materials using it) as the *base*, but the value
the engine uses is `effective_instability` from 6.2(b). This means contradiction produces
instability: a material carrying both `heat` and `cold`, or essence beyond its resonance, or
deep generation depth, is unstable *because of what it is* — and the player can see exactly
why in the inspector.

### 6.4 Generation

An integer depth counter. Used for naming ladders, value, codex sorting, and lineage pruning.
Not a gate — Integrity is the gate.

---

## 7. Crafting processes — where "order" actually lives

### 7.1 Slots and roles

A **Process** is authored data (`game/data/processes/*.json`) and defines typed slots:

| Slot | Count | Meaning |
|---|---|---|
| **Substrate** | exactly 1 | The thing being transformed. Its identity, lineage and structural properties carry forward. |
| **Reagent** | 1–3, **ordered** | Applied in sequence. Consumed. |
| **Catalyst** | 0–1 | Not consumed (or partly). Modifies rates, does not transfer its own properties. |
| **Medium / Environment** | 0–1 | Water, oil, brine, ash bed. Sets transfer medium and can bleed properties. |

**This is where order lives, and it is now legible in the UI.** The player literally sees
"Base: Iron Ingot → Step 1: Ember Sap → Step 2: Stormglass" and can reorder the steps. No
abstract property-dragging. Permuting the reagents permutes the outcome automatically,
because each step acts on a different intermediate state.

### 7.2 Channels — how the engine picks which of 20 properties reacts

**A process declares its channel.** This is the answer to "which properties participate when
materials have many." Not "the highest one" (degenerate), not "all pairs" (explosion), not
random (unlearnable) — *the process decides*.

```jsonc
{
  "id": "process.forge_infusion",
  "name": "Forge Infusion",
  "profession": "smithing",
  "severity": 0.55,
  "medium": "thermal",                    // see 7.3
  "role_weights": { "substrate": 0.65, "reagent": 0.30, "catalyst": 0.05 },
  "channel": [
    { "property": "heat",       "rate": 0.80 },
    { "property": "hardness",   "rate": 0.25 },
    { "property": "affinity",   "rate": 0.15 }
  ],
  "essence_rate": 0.45,
  "requires": { "substrate_tags": ["form:metal"], "profession_level": 15 },
  "tag_effects": { "set": ["state:alloy"], "clear": ["state:raw"] }
}
```

The same two materials in `Forge Infusion` versus `Distillation` versus `Grafting` produce
completely different materials, because different channels are open. **Process choice is a
first-class player decision**, which is exactly what a game with professions wants.

Starter process set (8 is plenty for v1):
`Smelt` · `Forge Infusion` · `Quench` · `Alloy` · `Grind` · `Steep/Decoct` · `Distill` ·
`Attune` *(the resonance-raising process)*.

### 7.3 Transfer medium — why you'd dissolve something instead of forging it

Each process declares a **medium**, which names the property governing how readily a reagent
*releases* what it carries:

| Medium | Release governed by | Flavour |
|---|---|---|
| `solvent` | `solubility` | steeping, decoction, alchemy |
| `thermal` | `instability` (and low `heat_resistance`) | forge, smelt, quench |
| `mechanical` | inverse `hardness` | grinding, milling, pressing |
| `arcane` | `resonance` | attunement, enchanting |

This is small but does a lot of work: it explains why Ember Sap (solubility 55) is an
alchemy reagent while Ember Core (instability 90, no solubility) is a forge reagent — and it
means "which process suits this ingredient" is a readable property of the ingredient.

### 7.4 Execution quality

```
quality_norm = clamp01( (profession_level_factor
                        + mastery_factor
                        + tool_factor
                        + active_performance_factor      // your timing minigame
                        - effective_instability/150) )
quality_mult = 0.85 + 0.27 × quality_norm                // 0.85 .. 1.12
```

Skill's most important effect is **narrowing variance** (§12.3), not adding a flat bonus.
Mastery means *control*, which is a far better progression fantasy for this system than
"+5% stats."

---

## 8. The universal reaction algebra

Executed once per reagent step. All constants are first-pass and expected to be tuned.

### 8.1 Acceptance and release

```
A (substrate acceptance) = 0.25 + 0.75 × (substrate.affinity / 100)
S (reagent release)      = 0.25 + 0.75 × (medium_property(reagent) / 100)
I (integrity factor)     = 0.50 + 0.50 × (substrate.integrity / 100)
C (catalyst factor)      = catalyst-provided, default 1.0
```

### 8.2 Convergence — the anti-inflation core

For each channel property `p`:

```
k        = rate_p × A × S × I × C × quality_mult
target   = reagent.p
before   = substrate.p
after    = before + (target − before) × clamp(k, 0, 0.85)
```

**Properties move a fraction of the remaining gap toward the reagent's value. They never
add, and they can never exceed the strongest input.** This single rule kills unbounded stat
escalation everywhere in the system, permanently, without caps or diminishing-return fudge
factors. It also means "how are a new material's numbers determined" has one clean answer.

The *only* way to exceed an input value is an authored Signature Reaction with an explicit
`overshoot` (§9), which is rare, costed, and deliberately the exciting case.

### 8.3 Off-channel handling — the anti-accumulation rule

**Only channel properties transfer.** Everything else:

- **Structural** off-channel properties: blend slightly toward a mass-weighted mixture
  (rate ≈ 0.10) — an alloy does get a bit heavier if you add heavy things.
- **Reactive** off-channel properties: **dilute toward zero** (rate ≈ 0.08) and receive
  *nothing* from the reagent.
- Any property below its `floor` (default 5) is pruned to 0.

This is what stops generation-5 materials having twenty-five nonzero properties. Each
transformation *focuses* the material along the process channel and washes out the rest.
Materials get more distinctive with refinement, not muddier — which is also the right
*feel*.

### 8.4 Essence transfer

```
essence_gain(e) = reagent.essence(e) × essence_rate × A × S × quality_mult
```
plus a bonus if the process channel includes `e`'s anchor property. Then recompute strain
(§5.3). Essence never converges toward zero on its own — it must be diluted deliberately or
annihilated by opposition.

### 8.5 Opposition and annihilation

For each opposed pair (`heat`/`cold`, `growth`/`decay`, and opposed essences):

```
overlap  = min(a, b)
a, b    -= overlap × 0.9
released = overlap × 0.9
integrity -= released × 0.25
```

The released energy is `strain_released` and feeds integrity cost — and is a Signature
trigger. Only the asymmetry survives, which means **you cannot stockpile opposites**. If you
want a material that genuinely holds both, you must earn a **Bound Opposition** trait — very
strong, very unstable, and one of the best discovery moments available.

### 8.6 Property disposition summary (answers "consume, transform, suppress, or preserve?")

All four, assigned deterministically by role — never per-recipe:

| Thing | Disposition |
|---|---|
| Reagent item | **Consumed** entirely (it's an ingredient) |
| Substrate channel properties | **Transformed** (converge toward reagent) |
| Substrate off-channel reactive | **Suppressed** (dilute toward 0) |
| Substrate off-channel structural | **Preserved**, minor blend |
| Properties that fed a Signature trait | **Partially consumed** — this is the trait's price |
| Opposed pairs | **Mutually annihilated**, releasing strain |
| Catalyst | **Preserved** (or partially consumed); transfers nothing |

### 8.7 Full step pipeline

```
for each reagent step:
   1. gate      — process requirements, substrate tags, profession level, integrity > 0
   2. acceptance/release coefficients          (8.1)
   3. converge channel properties              (8.2)
   4. transfer essence                         (8.4)
   5. off-channel drift + prune                (8.3)
   6. resolve opposition + strain              (8.5)
   7. evaluate Signature Reactions             (§9)
   8. evaluate State Traits                    (§10.2)
   9. enforce trait budget / supersession      (§10.4)
  10. recompute derived resistances            (2.2)
  11. recompute effective instability          (6.3)
  12. charge integrity                         (6.2a)
  13. append step to the Reaction Log          (§15.3)

finalize:
  14. derive tags                              (§4.2)
  15. compute potency                          (6.1)
  16. quantize → signature → registry lookup   (§12)
  17. generate name (if new)                   (§13)
  18. record discovery                         (§15)
```

---

## 9. Signature Reactions — the authored spikes

### 9.1 Shape

A Signature is a **conditional rule matched against abstract state**, never against item ids.

```jsonc
{
  "id": "sig.emberveined",
  "when": {
    "process_family": "forge",
    "substrate_tags_all": ["form:metal"],
    "substrate_before": { "conductivity": { "min": 50 }, "hardness": { "min": 55 } },
    "result_after":     { "heat": { "min": 30 } },
    "driving_channel":  "heat"
  },
  "then": {
    "grant_trait": { "id": "trait.emberveined",
                     "magnitude": "0.5*heat + 0.3*conductivity" },
    "overshoot":   { "heat": 10 },
    "consume":     { "flexibility": 12, "conductivity": 5 },
    "integrity_extra_cost": 6
  }
}
```

Three things to hold to:

- **Never reference item ids** in `when`. If a signature says `iron_ingot`, you've started
  building the recipe table again.
- **Always `consume` something.** A trait must cost properties. This is the anti-power-creep
  valve and it makes traits feel like tradeoffs rather than free upgrades.
- Budget: **30–80 signatures for the entire game.** If you need more, the algebra isn't
  doing enough work.

### 9.2 Chain Signatures (the "order really matters" spice)

A small number of signatures may match on the **ordered sequence of prior steps**:

```jsonc
"when": {
  "chain": [ { "driving_channel": "heat" },
             { "driving_channel": "charge" } ],   // heat THEN charge
  "within_steps": 3
}
```

`heat → charge` producing something different from `charge → heat` is now an authored,
deliberate, *rare* statement — not the default behaviour of 6,000 table entries. Budget:
**10–20 chain signatures, ever.** Everything else gets its order-dependence for free from
state composition.

---

## 10. Traits — what an "Emergent Property" actually is

### 10.1 Definition

**A Trait is a named, discrete, capped qualitative state that a material entered.** It has an
id, a magnitude (0–100), a display vocabulary, gameplay effects, and usually a drawback.
Traits are **authored as rules** (the trait library) but **never authored onto items** —
which item has which trait is always emergent.

Trait library budget: **40–80 traits over the life of the game.** This is a manageable
authoring load and produces a combinatorially enormous item space when crossed with
continuous property vectors, essence, lineage roots and forms.

### 10.2 Two ways a trait is born

**State Traits — declarative, inevitable, learnable.**

```jsonc
{ "id": "trait.resilient",
  "condition": { "hardness": {"min":70}, "flexibility": {"min":60} },
  "magnitude": "min(hardness, flexibility)",
  "consumes": { "hardness": 5, "flexibility": 5 } }
```

Anyone who reaches that region of state-space gets it, every time. These are the traits
players *learn to aim at*. They make the system masterable.

**Signature Traits — history-conditioned, discovery-flavoured.** Born from §9. These require
knowing *how*, not just *where*. These are what players *stumble into and then chase*.

Ship both. The mix is what makes the system simultaneously learnable and surprising.

### 10.3 Every trait carries a drawback or an opportunity cost

Non-negotiable design rule. `Emberveined` grants on-hit burn but reduces `flexibility`
(brittle blade). `Parasitic` drains the enemy but slowly damages the wielder's stamina.
`Bound Opposition` is enormously strong and permanently raises instability.

Without this rule, discovery collapses into a tier list and you get MMO progression by the
back door. With it, discovery is about **fit** — the right material for the right realm and
the right form — which is exactly the stated crafting goal ("I made this specifically for the
Dark Forest because I know what lives there").

### 10.4 The trait budget and supersession — how accumulation is prevented

**Materials cap at 3 traits. Equipment caps at 4.**  **[DECIDED]**

What the cap does and does not govern:

- It caps **named traits only**. It does *not* cap properties, essence, or tags — a material
  may carry twelve nonzero properties, three essences and eight tags alongside its three
  traits. Different layer, different rules.
- A material at 3 traits **does not fail to react**. Reactions proceed normally; only trait
  *retention* is bounded.

**Displacement.** When a 4th trait would be born, traits are sorted by magnitude and the
**weakest is dropped**. The drop is always reported explicitly:

```
✦ Trait gained: Stormlaced (31)
⚠ Trait lost:   Warmed (22) — displaced, trait cap 3/3
```

The displaced trait's `consumes` costs are **not** refunded — the properties it ate are gone.
That makes late-chain crafting a genuine "which three?" decision rather than accumulation,
and it is the main reason two players starting from Iron Ingot end up with meaningfully
different materials depending on the order they worked in.

**Supersession is the escape valve, and the reason to go deep.** Authored pairs (rarely
triples) merge into a single stronger trait, freeing a slot:

```
Emberveined + Stormlaced → Tempestforged
Resilient   + Parasitic  → Regenerative
```

So the deep-generation fantasy is **not "collect more traits"** — it is *combine traits into
better ones*. Prestige materials are the ones whose three slots hold superseded traits. This
is where the top of the crafting game lives, and it costs only a table of merge rules.

**Why cap at all.** Without it, a generation-8 material carries fifteen traits and every deep
item is strictly better than every shallow one — which is precisely the MMO tiering the
design rejects. The cap plus per-trait drawbacks (§10.3) means depth buys *specialisation*,
not raw superiority.

**Equipment cap (4).** With multi-component fabrication (§16.3) a three-component item could
inherit up to nine traits. Equipment selects its 4 by **expressed magnitude** — that is,
after each component slot's aperture is applied (§16.2). An `Emberveined` binding therefore
loses to an `Emberveined` edge, which is the correct outcome. Traits that don't make the cut
are listed as **dormant** on the item, contributing to value and flavour only.

Traits also **consume properties** when born (§10.2), so they cannot accrete for free even
below the cap.

### 10.5 Can traits participate in later reactions? Yes — as *conditions*, not as properties

A trait must **never** become a pseudo-property with its own arithmetic; that's how you end
up with two parallel numeric systems. Instead:

- Signature `when` clauses may require `has_trait` / `lacks_trait`.
- Traits may modify coefficients (a `Porous` trait raises acceptance `A`).
- Traits may supersede (10.4).

This gives you the recursion you wanted — `Emergent Property X → Arcane → new state` — with
zero new machinery.

---

## 11. What lives where (Item vs the rest of the game)

Rule of thumb: **an item describes what it IS. The world describes where it COMES FROM.
Systems describe what it DOES.**

| Data | Home | Not on Item because |
|---|---|---|
| id, name, description, type, tags, stackable, flags | `ItemDefinition` | — |
| properties, essence, potency, integrity, traits, lineage, signature | `MaterialProfile` (component on the definition) | keeps non-materials clean |
| **drop rate / drop chance** | `LootTable` entries (actor/container → weighted item refs, with conditions) | the same ore drops at different rates from different sources — a universal `DropRate` is incoherent |
| **harvest yields, node difficulty, tool/skill requirements** | `HarvestNode` / `ResourceSource` definitions | one material comes from many node types |
| **biome / realm availability** | realm & generation tables | availability is a world fact, not an item fact |
| **weight** | *computed* from `mass` × form volume | avoids desync with properties |
| **value** | *computed* by `ValuationRules` from potency, trait rarity, essence, generation, integrity | emergent items have no author to price them |
| **equipment stats** | `FormTemplate` + material profile → resolved (§16) | the same material makes a good sword and bad armour |
| **consumable effects** | `EffectTemplate` + material profile | same reason |
| **recipes** | there are none — `ProcessDefinition` replaces them | the rules are the source of truth |
| **use requirements** | `RequirementSet` component | |

`harvest_resistance` is the borderline case: it stays *on* the material (it's an intrinsic
fact about the substance), but it's role-tagged `sourcing` so the reaction engine ignores it,
and the *node* decides yield and difficulty using it.

---

## 12. Identity, determinism, and the archetype registry

### 12.1 Canonical signature

After finalization, quantize the result and hash it:

```
signature = hash(
   root_lineage_ids (sorted, weight-bucketed to 10%),
   form tag + state tag,
   traits (id + tier, sorted),          // tier = magnitude bucketed to 5 levels
   properties (each bucketed to nearest 5),
   essence (each bucketed to nearest 5),
   potency bucketed to nearest 5,
   generation
)
→ "emergent.7f3a91c4"
```

Look it up in the **Emergent Archetype Registry**. If present, you produced an existing
material — stack it. If absent, register a new runtime `MaterialDefinition`, generate a name,
and flag it as a **first discovery**.

### 12.2 Why this is the right call

- **Q28 answered: yes, identical transformations always produce the same material.** Same
  state ⇒ same signature ⇒ same id ⇒ same name, for every player, forever.
- Emergent materials stack, so inventory and save data stay small (an archetype is ~200–400
  bytes; a save realistically holds hundreds, not tens of thousands).
- Lineage becomes a link through the registry instead of a nested copy (§14).
- Trading, codex sharing, and "did anyone else find this?" all become possible later.
- Registry growth is bounded in practice because integrity caps chain depth, the trait budget
  caps trait combinations, and quantization collapses near-identical results.

Tune quantization empirically: too coarse and everything collapses into a handful of
materials; too fine and the registry fills with meaningless neighbours. Start at 5-point
buckets and measure.

### 12.3 Variance produces **different materials**, not random stats

This is a subtle but important consequence. A bad roll doesn't give you "Emberveined Iron
(bad roll)". It gives you a *different, weaker material* with its own name and signature —
possibly one nobody has seen. That is a strictly better outcome for a discovery game:

```
variance_magnitude = effective_instability × (1 − quality_norm) × process.severity
```

applied as a seeded perturbation of the channel properties *before* quantization. High skill
⇒ near-zero variance ⇒ you reliably hit the material you were aiming for. Low skill or low
integrity ⇒ you scatter across neighbouring buckets and find things by accident.

### 12.4 Where the registry lives  **[DECIDED]**

Two separable things that are easy to conflate:

- **The registry** — `signature → generated definition`. This is a **deterministic cache, not
  progress.** Because signature is a pure function of state, regenerating an entry produces a
  byte-identical result. Where it is stored is purely an engineering choice and affects no
  gameplay.
- **The codex** — what *this character* has actually discovered. This **is** progress, and is
  **always per-save**, regardless of the above.

**Decision: the registry lives in the save file** (extends `SaveData`, matching today's
single-slot `user://save.json`), behind an `IEmergentRegistry` interface so it can move to a
global install-level store later without touching the engine. Keeping the codex separate
means that move would never leak knowledge between characters.

### 12.5 Deterministic vs probabilistic

**Deterministic:** the entire algebra, opposition, signature matching, trait birth,
supersession, tag derivation, potency, integrity cost, quantization, naming, valuation.

**Probabilistic (seeded `IRandomSource` only):** the execution-quality roll, and the variance
perturbation in 12.3. That's it.

No random trait tables, no random stat rolls, no "5% chance of Masterwork." Given
(inputs, process, character stats, seed), the outcome is fully reproducible — which is
required by your deterministic-simulation invariant, makes the system unit-testable, and
means mastery genuinely converges on control.

---

## 13. Emergent naming

### 13.1 Grammar

A name is a **pure function of the final state** — never of the history. History-based names
grow without bound; state-based names stay short.

```
[Intensity-inflected Trait Adjective] [Essence Qualifier?] [Root Noun] [Form Noun?]
```

with hard constraints:

- **Maximum 3 words.**
- **At most one trait adjective**, drawn from the dominant trait only.
- **No "of X" constructions. Ever.**
- **No generic tier words** (Greater, Superior, Lesser, Grand). Intensity is expressed
  through vocabulary, not adjectives-of-adjectives.
- Numbers never appear.

### 13.2 The ladder trick

Every trait ships an **ordered adjective ladder** keyed to magnitude tiers. That's how you
get intensity without "Greater":

```
trait.emberveined : [ "Warmed", "Emberveined", "Cindered", "Searing" ]
trait.stormlaced  : [ "Charged", "Stormlaced", "Levinstruck", "Tempestbound" ]
trait.parasitic   : [ "Clinging", "Grasping", "Parasitic", "Devouring" ]
```

Same for essence qualifiers and for root nouns.

### 13.3 Root nouns

The root comes from the **dominant lineage root** (§14) crossed with the current `form:` tag:

| Dominant root | form:metal | form:powder | form:liquid | form:glass |
|---|---|---|---|---|
| iron | Iron | Iron Dust | Ferrous Draught | Ironglass |
| oak | — | Oak Ash | Oak Tincture | — |

If the dominant lineage root's weight falls below ~40%, or the form changes category
entirely, the root **shifts** to the new dominant contributor. That's how "Iron" eventually
stops being Iron and becomes something else — a rare, satisfying moment worth flagging in
the codex.

### 13.4 Collision handling

Because names derive from state and state is quantized, two *different* signatures can
produce the same string. Resolve deterministically: append the second-strongest trait's
adjective, then the essence qualifier, then (last resort) a stable two-syllable coinage
derived from the signature hash via a syllable table. Never append a number.

### 13.5 The player naming escape hatch

**Let the discoverer rename it in their codex.** The canonical signature stays; the display
name becomes the player's. This is cheap to build and it completely defuses the
"procedurally generated names feel like loot spam" risk — because the names the player cares
about most become *their* names. Worth doing in v1.

---

## 14. Lineage

Store a **fixed-size, lossy, weighted ancestry** plus **one level of links**:

```csharp
sealed record Lineage(
    IReadOnlyList<RootShare> Roots,   // ≤ 3 entries, weights sum to 1.0
    int Generation,
    string ProcessId,                 // the process that produced this
    IReadOnlyList<string> ParentSignatures);  // ≤ 4 archetype ids, ONE level only
```

- **Roots** carry forward by weighted merge, renormalized each generation. Anything under 5%
  is dropped into an implicit "trace" remainder. This is what naming, flavour, valuation and
  NPC interest read.
- **Parent links** are one level deep. Because every emergent archetype is registered, the
  **full ancestry tree is reconstructible by walking the registry** — you never embed a
  recursive copy. This is the entire answer to "lineage without becoming enormous."
- **Provenance of the specific craft** (exact reagents, order, catalyst, who made it, when,
  quality roll) lives in the **codex entry**, not on the item. That's a per-save journal
  record, written once at first discovery.

`ItemInstance.Provenance` already exists and can hold the equipment-level version of this.

---

## 15. Discovery, the codex, and legibility

### 15.1 Codex

Per-save, records only what the player has actually produced or been told about:

- archetype id, generated name, player's name, first-discovered timestamp
- the exact craft that produced it (substrate, ordered reagents, process, catalyst)
- observed properties, essence, traits, potency, integrity
- one-level lineage links, navigable (walk up to the base resources)

**The codex must never list undiscovered emergent materials**, because they don't exist until
generated. This is not a limitation — it's the core of the fantasy.

### 15.2 Known-rules journal

Separate from the codex: as players trigger signatures and state traits, the *rule* is
recorded in readable form — `Resilient: any material with hardness ≥ 70 and flexibility ≥ 60`.
This converts play into permanent, transferable knowledge and is the main progression
currency of the crafting game.

### 15.3 The Reaction Log — a hard requirement, not a nice-to-have

Every craft emits a structured, human-readable step-by-step trace:

```
Forge Infusion — Iron Ingot ← Ember Core
  Acceptance 0.48 (iron resists bonding: affinity 30)
  Release    0.93 (ember core is volatile: instability 90)
  heat        0 → 35   (channel, rate 0.80)
  hardness   65 → 62   (channel, rate 0.25)
  conductivity 55 → 57 (structural blend)
  ✦ Signature: Emberveined — thermal charge driven into a conductive metal
      heat +10 (overshoot), flexibility −12, conductivity −5
  Integrity 90 → 72  (Δstate 0.41 × severity 0.55, +6 signature cost)
  Potency 40, 70 → 53
```

A system this deep is only playable if it explains itself. This log is also your debugging
tool, your tutorial, and your codex content. Budget real time for it.

### 15.4 Assay — the legibility unlock

An **Assay/Analysis** action (a Herblore/Smithing skill use) inspects a material and reports
**proximity hints**: *"This substance is within reach of a Resilient state — it needs more
flexibility."* / *"Highly receptive to thermal work."* / *"Saturated: further infusion will
be unstable."*

This is the single most important usability feature in the design. It converts blind
flailing into directed experimentation, without ever handing the player a recipe. Reveal
depth of hints by profession level.

---

## 16. Materials → equipment (the boundary)

### 16.1 Two distinct operations

| | **Transmutation** | **Fabrication** |
|---|---|---|
| Produces | a material archetype | an `ItemInstance` (weapon/armor/tool) |
| Uses | reaction algebra | form template |
| Costs integrity | yes (0 ⇒ destroyed, §6.2c) | terminal — materials consumed, integrity irrelevant |
| Recursive | yes | no |

Clean, and it means "where do materials stop" has a one-word answer: **at fabrication.**
Combined with destruction-at-zero, it also means every material carries a standing question —
*refine once more, or commit it to a form now?*

### 16.2 Form templates — multi-component from v1  **[DECIDED]**

```jsonc
{
  "id": "form.longsword",
  "type": "Weapon",
  "slots": {
    "edge":    { "requires_tags": ["form:metal","form:crystal"], "mass_share": 0.60,
                 "aperture": { "thermal":1.0, "charge":0.9, "toxic":1.0,
                               "vital":0.2, "arcane":0.5, "structural":1.0 } },
    "core":    { "requires_tags": ["form:metal","form:wood"],    "mass_share": 0.25,
                 "aperture": { "thermal":0.3, "charge":0.5, "toxic":0.1,
                               "vital":0.3, "arcane":0.7, "structural":0.8 } },
    "binding": { "requires_tags": ["form:hide","form:fiber"],    "mass_share": 0.15,
                 "aperture": { "thermal":0.2, "charge":0.2, "toxic":0.4,
                               "vital":0.6, "arcane":0.3, "structural":0.3 } }
  },
  "stat_map": {
    "base_damage":    [ {"slot":"edge","property":"hardness","w":0.50},
                        {"slot":"edge","property":"mass",    "w":0.30},
                        {"slot":"core","property":"mass",    "w":0.20} ],
    "interval_ticks": [ {"slot":"*",   "property":"mass",    "w":1.00} ],
    "durability":     [ {"slot":"edge","property":"hardness","w":0.35},
                        {"slot":"core","property":"flexibility","w":0.45},
                        {"slot":"binding","property":"flexibility","w":0.20} ],
    "stamina_cost":   [ {"slot":"*",   "property":"mass",    "w":0.7} ]
  },
  "trait_cap": 4
}
```

Three rules do the work:

- **Stats read from *named slots*, not from a blend.** This is the crucial choice. If a form
  averaged its components into one property vector, "which material goes where" would be
  meaningless mush. Reading `base_damage` from the **edge** and `durability` mostly from the
  **core** makes component placement a real decision: a hard brittle edge on a flexible core
  is a genuinely different weapon from the reverse, and the system computes that without any
  authored combination. `"slot": "*"` means the mass-share-weighted total across all slots
  (used for weight-like stats).
- **`stat_map` is the answer to "high Hardness means something different for an edge than for
  cloth."** A robe's map reads `flexibility` and `insulation`; plate reads `hardness` and
  `mass`; a staff reads `resonance` and `arcane`. The same material is excellent in one form
  and useless in another — which is what stops a single "best material" existing.
- **`aperture` is per-slot, not per-form**, and gates how much of each trait *category* that
  slot can express. An `Emberveined` edge expresses at 1.0; the same material as a *binding*
  expresses at 0.2. Unexpressed magnitude becomes **dormant** — shown in the tooltip,
  counted in value and flavour, and **fully available if the material is used in a different
  form later**. Dormant traits are a feature: they make one material interesting in several
  directions rather than optimal in one.

### 16.3 Resolving a multi-component item

```
1. validate    each slot's material against requires_tags
2. properties  evaluate stat_map entries against the named slots
3. traits      for every component trait: expressed = magnitude × slot.aperture[category]
4. select      keep the top `trait_cap` (4) by expressed magnitude; rest → dormant
5. essence     mass_share-weighted sum, then × (0.6 + 0.4 × arcane/100)   (§5.2.1)
6. potency     mass_share-weighted mean of component potencies × craft quality
7. signature   hash the resolved item → name → ItemInstance
```

Step 3/4 is where the equipment trait cap of 4 (§10.4) applies, and why it's applied to
*expressed* rather than raw magnitude.

**Scope warning, stated plainly:** multi-component fabrication roughly doubles the
fabrication phase versus single-material. It needs slot validation, a slot-aware stat
resolver, per-slot apertures, dormancy tracking, a component-selection UI, and naming that
handles more than one contributing material (§16.5). It is worth it — this is where a huge
amount of depth comes from for almost no *content* cost — but it should not be estimated as a
small addition.

### 16.4 Can finished equipment be emergent? Yes

Same signature approach, but the result is an `ItemInstance` with a **derived
`EquipmentDefinition`** built from the form template + component material profiles.

This slots directly into your existing `EquipmentResolver` seam: it keeps producing neutral
`AttackProfile` / `ArmorProfile`, just fed by richer inputs. Note the calibration TODO in
`itemization.md §2` (materials 0–100 vs equipment ~0–5) has to be resolved as part of this.

### 16.5 Naming fabricated items

The name comes from the **primary slot** (the one with the largest `mass_share`), never from
all components — otherwise multi-component items produce exactly the loot-generator garbage
the design forbids.

```
[Dominant expressed trait adjective] [Primary slot root] [Form noun]
    → "Emberveined Iron Longsword"
```

Secondary components appear **only** in the tooltip/inspector, never in the name. The one
exception: if a non-primary component contributes the single strongest *expressed* trait, its
adjective is used instead of the primary slot's — so a mundane iron blade with a
`Tempestbound` binding reads "Tempestbound Iron Longsword", which is both shorter and more
informative than naming both materials.

If the composite crosses an authored **artifact signature** (e.g. two superseded traits plus
potency ≥ 90), it earns a **proper name** from a separate epithet grammar
(`[Epithet] the [Noun]` / single coined name) — rare enough to feel like a genuine artifact,
and the one place a name may exceed three words.

### 16.6 Emergent consumables  **[DECIDED — in scope]**

Consumables are **the same fabrication system with a different map.** No new machinery:

| | Equipment | Consumable |
|---|---|---|
| Template | `FormTemplate` + `stat_map` | `FormTemplate` + `effect_map` |
| Slots | edge / core / binding | base / active / stabiliser |
| Output | `ItemInstance` | **stackable emergent definition** (like materials) |
| Traits | expressed via aperture, cap 4 | expressed via aperture, cap 3 |

Forms: `form.draught` · `form.salve` · `form.flask` (thrown) · `form.incense` · `form.ration`.

```jsonc
"effect_map": {
  "heal":          [ {"slot":"base","property":"growth","w":1.0} ],
  "poison_damage": [ {"slot":"active","property":"toxicity","w":1.0} ],
  "burn_damage":   [ {"slot":"active","property":"heat","w":0.7},
                     {"slot":"active","essence":"fire","w":0.6} ],
  "duration_ticks":[ {"slot":"stabiliser","property":"insulation","w":0.6},
                     {"slot":"stabiliser","property":"solubility","w":-0.4} ],
  "side_effect":   [ {"slot":"*","property":"corrosion","w":1.0} ]
}
```

Two consumable-specific rules:

- **Potency drives magnitude; the stabiliser slot drives duration and side-effect
  suppression.** That makes "what did you stabilise it with" a real decision and gives
  mundane materials (chalk, beeswax, spring water) a permanent job.
- **Consumables stack**, so they use the material-style archetype registry (§12), not
  `ItemInstance`. A batch of identical draughts is one stack, which is essential given how
  many the player will brew.

Unlike equipment, consumables are the natural home for **negative** emergent outcomes —
a botched draught that is toxic, or that heals *and* corrodes. Lean into that; it's the
cheapest source of memorable results in the whole system.

---

## 17. Failure modes, exploits, and their counters

| Risk | Counter |
|---|---|
| **Recipe table in disguise** (thousands of ordered rules) | Total-function algebra + ≤80 signatures + composed steps (§0) |
| **Infinite stat escalation** | Convergence math can never exceed inputs (§8.2) |
| **God Ingot: feed everything in** | Potency is a weighted mean; junk lowers it (§6.1) |
| **Infinite refinement chains** | Integrity budget with cost ∝ magnitude of change (§6.2) |
| **Ratcheting A→B→A loops** | Integrity is monotonically non-increasing; no free restoration |
| **Property accumulation** (25 nonzero props) | Only channel properties transfer; off-channel dilutes; floor pruning (§8.3) |
| **Trait accumulation** | Cap 3, supersession, traits consume properties (§10.4) |
| **Registry / save bloat** | Quantized signatures, archetypes stack, ~300 bytes each (§12) |
| **Essence stacking** | Resonance capacity → strain, not a cap (§5.3) |
| **Opposite-stacking** (heat AND cold on everything) | Mutual annihilation; only a rare trait binds both (§8.5) |
| **One globally optimal material** | Form apertures + stat maps make optimum form-dependent; every trait has a drawback (§10.3) |
| **Power creep via traits** | Traits consume properties and carry drawbacks |
| **Unreadable results** | Reaction Log (§15.3) + Assay hints (§15.4) — treat as required scope |
| **Boring middle game** (most combos are mush) | The algebra always produces a *real, named, stackable* material; state traits are aimable; Assay gives direction |
| **Everything trends to grey** (essence dilutes away) | Distillation/extraction *concentrate* essence at the cost of yield |
| **Name spam** | ≤3 words, one trait adjective, ladders not tier words, player rename (§13) |
| **Content authoring underestimated** | Real v1 load: ~20 property defs, ~8 processes, ~15 traits, ~10 signatures, ~4 forms. Budget it explicitly. |
| **UI complexity** | Progressive disclosure; the material inspector is a *major* UI deliverable, not a tooltip |
| **Determinism broken** | Everything except two seeded rolls is pure; seeded `IRandomSource` only (§12.5) |
| **Silently losing hours of refinement** (integrity 0 destroys) | Projected integrity shown *before* commit; explicit destruction warning; destruction-chance % below 25 integrity; byproducts on destruction (§6.2c) |
| **Multi-component mush** (components averaged into one vector) | `stat_map` reads from *named slots*, never a blend (§16.2) |
| **Consumable spam** (thousands of unique brews) | Consumables use the stackable archetype registry, not instances (§16.6) |
| **Migration cost of tag namespacing** | One scripted rewrite of 7 JSON files + a validator rule; do it before anything else |

---

## 18. Conceptual data model

```csharp
// ---- Definitions (authored OR runtime-registered) -------------------------
interface IItemDefinition {
    string Id; string Name; ItemType ItemType; bool Stackable;
    TagSet Tags; PropertySet BaseProperties;
}

sealed record MaterialProfile(
    PropertySet Properties,       // existing 0-100 string-keyed set
    EssenceSet  Essence,          // small vector, usually empty
    IReadOnlyList<TraitRef> Traits,
    int Potency, int Integrity, int Generation,
    Lineage Lineage,
    string Signature);            // canonical hash; == definition id for emergent

sealed class MaterialDefinition : IItemDefinition { MaterialProfile Profile; ... }

// ---- Authored rules (data) -----------------------------------------------
PropertyDefinition   // id, role, family, opposes, resisted_by, dilutes, floor
EssenceDefinition    // id, anchor property, opposes
TagFamily            // id, cardinality, allowed values
ProcessDefinition    // slots, channel, medium, severity, role weights, tag effects, gates
TraitDefinition      // id, birth condition | signature-only, magnitude expr,
                     //   consumes, effects, drawback, adjective ladder, category
SignatureReaction    // when{} / then{}  — abstract conditions only
FormTemplate         // slots (requires_tags, mass_share, per-slot aperture),
                     //   stat_map | effect_map (slot-scoped entries), trait_cap
ValuationRules
NameGrammar          // root nouns, form nouns, ladders, essence qualifiers, syllables

// ---- Runtime services -----------------------------------------------------
IReactionEngine       Resolve(CraftRequest) -> CraftOutcome
IEmergentRegistry     Lookup/Register by signature; persisted per save
INameGenerator        MaterialProfile -> string  (pure)
ITagDeriver           state + process + lineage -> TagSet  (pure)
IValuation            MaterialProfile -> int  (pure)
IFabricator           FormTemplate + slotted components
                        -> ItemInstance (equipment) | archetype id (consumables)
ICodex                discoveries + known rules (per save)

sealed record CraftOutcome(
    string ResultDefinitionId, int Quantity,
    bool IsFirstDiscovery,
    IReadOnlyList<ReactionLogEntry> Log,
    IReadOnlyList<ItemStack> Byproducts);
```

Everything except `IEmergentRegistry` (which needs a save-backed store) and `IFabricator` is
a pure function — trivially unit-testable, which matters given the invariant that Core stays
deterministic and Godot-free.

---

## 19. Worked example (real values from `game/data/materials/`)

### Attempt 1 — the naive craft (and why it's still a good outcome)

```
Substrate: Iron Ingot   hardness 65, mass 62, affinity 30, conductivity 55,
                        insulation 10, solubility 2, heat_res 60, cold_res 60
                        potency 40, integrity 90, generation 1
Reagent:   Ember Sap    mass 11, affinity 58, solubility 55, instability 40,
                        heat 45, heat_res 35    potency 45, integrity 100
Process:   Steep (medium: solvent, severity 0.20, channel heat@0.55)
```

```
A = 0.25 + 0.75×0.30 = 0.475        (iron barely wants to bond)
S = 0.25 + 0.75×0.55 = 0.663        (sap dissolves willingly)
I = 0.50 + 0.50×0.90 = 0.950
k = 0.55 × 0.475 × 0.663 × 0.95 × 1.00 = 0.164
heat: 0 + (45 − 0) × 0.164 = 7
```

Result: heat 7 — barely anything. Iron's low `affinity` is the whole story, and the Reaction
Log says so in plain language. Integrity 90 → 87. The player nonetheless gets a real,
stackable, named material (**"Warmed Iron"**) and a first discovery. **The lesson is
legible: solvents don't open metal. Use heat.**

### Attempt 2 — the informed craft

```
Substrate: Iron Ingot (as above)
Reagent:   Ember Core   hardness 40, mass 30, affinity 30, instability 90,
                        heat 100, heat_res 80, arcane 30, essence.fire 60
                        potency 70, integrity 100
Process:   Forge Infusion (medium: thermal, severity 0.55,
                           channel heat@0.80 / hardness@0.25, essence_rate 0.45)
Quality:   skilled + good timing → quality_mult 1.05
```

```
A = 0.475
S = 0.25 + 0.75×(instability 90/100) = 0.925    ← volatile reagents give freely under heat
I = 0.95
k(heat) = 0.80 × 0.475 × 0.925 × 0.95 × 1.05 = 0.351
heat:      0 + (100 − 0) × 0.351 = 35
hardness: 65 + (40 − 65) × 0.110 = 62
essence.fire: 60 × 0.45 × 0.475 × 0.925 × 1.05 = 12
  → strain: resonance 0 ⇒ capacity 0 ⇒ strain 12   (iron is a poor vessel — visible warning)

✦ sig.emberveined fires: form:metal ✓, conductivity 55 ≥ 50 ✓, hardness 65 ≥ 55 ✓,
                          heat 35 ≥ 30 ✓, driving channel = heat ✓
   → trait.emberveined, magnitude = 0.5×45 + 0.3×57 = 40  (tier 2 → "Emberveined")
   → overshoot heat +10 → 45 ; consume flexibility −12 (already 0), conductivity −5 → 52
   → +6 integrity cost

Δstate ≈ 0.41 → integrity cost = 0.41×0.55×12 + 4 + 12×0.3 + 6 ≈ 18
Integrity 90 → 72
Potency = (40×0.65 + 70×0.30 + 0) × 1.05 = 49 ;  ceiling = 70 + 8 = 78 → potency 49
Tags: state:raw cleared, state:alloy set, class:magical set (essence.fire 12 < 25? no) …
Name: ladder tier 2 of trait.emberveined → "Emberveined" + root "Iron"
```

**→ `Emberveined Iron` — potency 49, integrity 72, generation 2, 1 trait, strained.**

The strain warning teaches the next lesson: `Attune` the iron first (raise `resonance`) and
the fire essence would have *held* instead of bleeding.

### Attempt 3 — pushing it

```
Emberveined Iron ← Stormglass (conductivity 80, charge 60, hardness 54, instability 50)
   Process: Forge Infusion, channel charge@0.75 / conductivity@0.40
   → charge 0 → 21, conductivity 52 → 63
   ✦ sig.stormlaced fires → trait.stormlaced (magnitude 31, tier 1 → "Charged")
   Traits now: emberveined(40), stormlaced(31)
   ✦ supersession: emberveined + stormlaced → trait.tempestforged (magnitude 45)
   Integrity 72 → 51.  Generation 3.
   Name: ladder tier 2 of trait.tempestforged → "Tempestforged Iron"
```

### Fabrication

```
form.longsword — edge: Tempestforged Iron (0.60), core: Ironwood (0.25),
                 binding: Wolf Hide (0.15)
   base_damage = (0.5×hardness 62 + 0.4×mass 61) × potency/100 …
   aperture thermal 1.0, charge 0.9 → Tempestforged expresses at ~0.95
   → "Tempestforged Iron Longsword"  (instance, unique, with dormant portions noted)
```

Nothing in that chain was authored as an item. The authored content was: 20 property
definitions, 1 process, 3 signatures, 3 traits, 1 form, and a name grammar.

---

## 20. Build phasing

Each phase must leave `dotnet build` + `dotnet test` green, per project workflow rules.

| Phase | Content | Ships |
|---|---|---|
| **P0** | Tag namespacing migration; `PropertyDefinition` registry + roles; add `resonance`; derived resistances; `arcane` role wiring; validator rules | no gameplay change, unblocks everything |
| **P1** | `ProcessDefinition` + universal algebra + convergence + off-channel + opposition + potency + integrity (incl. destruction + byproducts + **pre-commit projection UI**) + signature/quantization + archetype registry + naming v1 + Reaction Log. **Zero authored signatures, zero traits.** | **This alone is a playable, fun, genuinely emergent system.** Prove it before adding anything. |
| **P2** | State traits (~15) + trait cap 3 + displacement + supersession | qualitative results |
| **P3** | Essence layer + resonance capacity/strain + `arcane` amplification + `Attune` process | the magical tier |
| **P4** | Signature reactions (~10) + chain signatures (~4) | authored spikes |
| **P5a** | Fabrication core: single-slot form templates, `stat_map`, apertures, dormancy, `EquipmentResolver` recalibration | materials → gear |
| **P5b** | Multi-component: slot validation, slot-aware stat resolution, per-slot apertures, equipment trait cap 4, component-selection UI, naming §16.5 | the depth payoff |
| **P5c** | Consumable forms + `effect_map` (reuses P5a/P5b wholesale) | brewing |
| **P6** | Codex, known-rules journal, Assay, player renaming | the discovery metagame |

**P1 shipping alone is the key claim.** If pure convergence + integrity + naming + stacking
isn't already interesting to play with, adding traits and signatures will not save it — and
you'll have found that out cheaply.

**P5 is split deliberately.** Multi-component and consumables are both in scope, but P5a is
the risky part (property→stat calibration against the 0–100 vs 0–5 scale mismatch flagged in
`itemization.md §2`). Get single-slot swords feeling right before adding slots; P5b and P5c
are then mostly additive and cheap.

Note the **pre-commit integrity projection UI is P1 scope, not P6 scope.** With
destruction-at-zero it is not a usability nicety — it is the thing that makes the rule fair.

---

## 21. Decisions taken, and what is still open

### Settled

| # | Decision | Section |
|---|---|---|
| 1 | **Essence is kept** as the rare supernatural layer (option B). Reactive properties remain the mundane layer. | §5 |
| 2 | **`arcane` stays a property; there is no arcane essence.** Essences are purely typed. `arcane` gains four defined jobs (gate / amplifier / load / damage typing). Zero data migration. | §5.2.1 |
| 3 | **Material trait cap 3** with magnitude displacement and supersession; **equipment cap 4** on *expressed* magnitude. | §10.4 |
| 4 | **Integrity 0 destroys the material.** Requires pre-commit projection, destruction-chance %, and byproducts — all P1 scope. | §6.2c |
| 5 | **Multi-component fabrication in v1**, with slot-scoped `stat_map` and per-slot apertures. | §16.2–16.3 |
| 6 | **Emergent consumables in scope**, reusing fabrication with an `effect_map`; stackable, not instances. | §16.6 |
| 7 | **Registry lives in the save file** behind `IEmergentRegistry`; the **codex stays per-save** regardless. | §12.4 |

### Still open

1. **Quantization granularity** (§12.1) — start at 5-point buckets, but this must be tuned
   empirically once P1 runs. Too coarse collapses the space; too fine floods the registry.
   This is the single highest-risk tuning number in the design.
2. **Does `cohesion` need to exist?** (§2.2) Deferred until playtesting shows whether
   `hardness` + `flexibility` adequately expresses brittleness. Default: no.
3. **Byproduct table on destruction** (§6.2c) — which byproducts, from which form tags, and
   how useful should they be? Needs to be generous enough that destruction stings without
   discouraging experimentation.
4. **Should `Anneal` (integrity restoration) exist at all?** Now that destruction is
   permanent it is a more attractive hook, but it also weakens the commit-or-lose tension
   that makes §6.2c good. Recommend deferring past v1 and deciding from play.
5. **Equipment durability** — the design assumes it exists (`stat_map.durability`) but the
   current game has none. Either add it or drop those map entries.
6. **Active-crafting performance** (`docs/crafting.md §8–11`) feeds `quality_norm` (§7.4).
   How much should timing minigames move the number relative to profession level? This
   determines whether crafting skill or crafting *play* is the dominant lever.

---

## 22. Question index

| Your Q | Section |
|---|---|
| 1 sound? | §0 |
| 2, 3 property set | §2 |
| 4 types | §3 |
| 5 tags | §4 |
| 6 layer distinctness | §1 |
| 7 item vs elsewhere | §11 |
| 8, 9 2- and 3-property reactions | §0 D2, §7.1, §8, §9.2 |
| 10 which properties participate | §7.2 |
| 11 material order → property order | §7.1 |
| 12 process influence | §7.2, §7.3 |
| 13 consume/transform/suppress/preserve | §8.6 |
| 14, 15 emergent properties | §10 |
| 16 accumulation | §8.3, §10.4 |
| 17 new numeric values | §8.2 |
| 18, 19 essence | §5 |
| 20 potency | §6.1 |
| 21, 22 integrity / infinite scaling | §6.2, §17 |
| 23 tag transformation | §4.2 |
| 24 lineage | §14 |
| 25, 26 naming | §13 |
| 27 discovery record | §15 |
| 28 determinism of identity | §12.1, §12.2 |
| 29 skill variance | §7.4, §12.3 |
| 30 material → gameplay stats | §16.2, §16.6 |
| 31 emergent equipment | §16.4, §16.5 |
| 32 exploits & traps | §17 |
| 33 data model | §18 |
| 34 deterministic vs probabilistic | §12.5 |
| 35 deep but learnable | §0 D2, §10.2, §12.3, §15.3, §15.4 |
