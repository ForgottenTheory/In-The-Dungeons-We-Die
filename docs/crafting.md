# Crafting

> ⚠ **SUPERSEDED AS DESIGN (2026-08-20).** This document describes earlier crafting designs
> (including the recipe-flavored vision that `docs/emergent-item-system.md` already replaced).
> The design of record for materials and crafting is now the **Identity + Signature system** —
> `docs/identity-foundation.md` (DECISIONS **D42–D44**). Kept for history; do not design or
> author against it.

## 1. Vision

Crafting is a major expression of player knowledge.

It combines:

Materials
+
Profession Skill
+
Recipe
+
Experimentation
+
Active Performance

Crafting should be capable of producing items the player feels personally responsible for creating.

---

# 2. Passive Crafting

Passive crafting provides:

- Reliable production
- Standard quality
- Offline capability
- Lower rare-outcome frequency

Examples:

Smelt iron.

Cook rations.

Brew standard potion.

---

# 3. Active Crafting

Active crafting provides opportunities for:

- Increased quality
- Better yield
- Material conservation
- Masterwork results
- Rare traits

Performance matters.

---

# 4. Material Properties

Materials are more than recipe tokens.

Examples:

Oak Bark:
- Organic
- Tannin
- Toxin resistance potential

Iron:
- Durable
- Heavy
- Conductive

Combining material properties can create new outcomes.

---

# 5. Cross-Profession Crafting

Professions interact.

Example:

Forestry:
Obtains Oak Bark.

Herblore:
Understands/treats Oak Bark.

Smithing:
Infuses Iron.

Result:

Barkbound Iron.

Potential property:

Toxin Resistance.

---

# 6. Discovery

Some recipes/interactions are hidden.

Players discover them by:

- Experimentation
- Realm discoveries
- Profession mastery
- Books/lore
- Events

Once discovered, the interaction is recorded.

---

# 7. Crafting Journal

The game should eventually maintain a journal containing:

- Known recipes
- Known materials
- Known interactions
- Discovered properties
- Experimental notes

This transforms game knowledge into visible progression.

---

# 8. Smithing Active Gameplay

Potential stages:

Heat
→ Strike
→ Temper

Player timing affects quality.

Future systems may include:

- Alloy balancing
- Socket carving
- Material infusion

---

# 9. Alchemy Active Gameplay

Potential interactions:

- Potency vs Quantity
- Temperature
- Ingredient timing
- Distillation

---

# 10. Cooking Active Gameplay

Potential interactions:

- Timing
- Ingredient balance
- Heat
- Preparation

Food should primarily support preparation and Realm endurance.

---

# 11. Enchanting Active Gameplay

Potential interactions:

- Rune tracing
- Attunement
- Frequency matching
- Affix stabilization

---

# 12. Crafting Quality

Potential tiers:

Poor
Normal
Fine
Exceptional
Masterwork

Names remain subject to iteration.

Quality can affect:

- Base effectiveness
- Durability
- Affix potential
- Value

---

# 13. Masterwork

Masterwork outcomes should feel earned.

Sources:

- High Profession skill
- High Mastery
- Rare materials
- Active performance
- Specialized tools
- Class/suffix interactions

Do not make Masterwork merely a flat random 1% roll detached from player behavior.

---

# 14. Experiment Failure

Experimentation should consume something meaningful but not always produce nothing.

Possible outcomes:

- Failure
- Partial recovery
- Strange byproduct
- Discovery clue
- Unexpected valid interaction

This encourages experimentation without making it free.

---

# 15. Field Crafting

Most crafting occurs in the Hideout.

Limited field crafting may occur through:

- Campsites
- Specific class mechanics
- Equipment
- Suffixes

Field crafting should remain constrained because preparation matters.

---

# 16. Crafting Goal

The ideal player thought is:

"I made this specifically for the Dark Forest because I know what lives there."

That is the target.

---

# 17. Revised Direction — Property-Based Emergent Crafting  [PLANNED]

> This section records the intended long-term crafting model. As of now only the **item/property/instance architecture** exists (see `docs/itemization.md`); the reaction simulation below is **not** implemented. The current shipped crafting is a small interaction/discovery step that is being generalized toward this.

## 17.1 Not a recipe table

Crafting is **not** a hand-authored recipe list (`Iron + Bloodmoss + Emberleaf = Item #427`). Instead it is a small **simulation**: ingredients carry properties, and **universal interaction rules** decide what happens when those properties meet. Successful combinations produce new **item instances** whose properties are derived from their parents.

Discovered recipes may eventually exist as **records of combinations the player has found**, but they are not the source of truth — the rules are.

## 17.2 Recursive materials

Any generated material is a real instance and can be crafted again:

```
Iron Ingot + Bloodmoss        → Bloodmoss Iron Ingot        (instance, derived props)
Bloodmoss Iron Ingot + Fire Mote → Charred Bloodmoss Iron    (instance, evaluated again)
Charred Bloodmoss Iron + …    → … same universal system again
```

This recursion is a fundamental requirement, and it is what the two-tier item model (`docs/itemization.md`) exists to support.

## 17.3 Universal interaction pipeline (conceptual)

A craft attempt evaluates roughly:

1. Can the materials bond? (compatibility + `affinity`)
2. Which properties can transfer into the receiving material? (`solubility`, thresholds)
3. Resolve opposing properties (`heat` vs `cold`, `growth` vs `decay`).
4. Detect property **reactions** (e.g. `growth` + `toxicity` → a derived *blight* trait/effect).
5. Evaluate how much foreign influence the material can contain (capacity/thresholds).
6. Apply catalysts/modifiers from additional ingredients (a 3rd ingredient may catalyze, stabilize, or destabilize — not merely "+ another stat").
7. Apply `instability` → resolve outcome: success / partial / failure / **mutation**.
8. Generate a new `ItemInstance` with the resulting derived `Properties` and any generated `Traits`, recording provenance.
9. The generated instance can participate in crafting again.

## 17.4 Outcomes

Experimentation should consume something meaningful but not always yield nothing (failure, partial recovery, strange byproduct, discovery clue, or an unexpected valid interaction). Quality tiers (Poor→Masterwork) and mutation are properties of the generated instance.

## 17.5 Implementation seam

- Item **instances** and the **`PropertySet`** already exist and carry derived properties.
- The current `CraftingExperimentSystem` (fixed inputs → fixed result id) is the placeholder that will be generalized into the rule evaluator above; it will emit `ItemInstance`s with derived properties instead of stackable definition ids.
- The universal ruleset will live behind a single evaluator so new properties/reactions are data + rules, not scattered `if` statements.