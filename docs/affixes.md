# Item Affixes, Material Genetics & the Crafting Casino

> ⚠ **SUPERSEDED AS DESIGN (2026-08-20).** The genome/pressure model and the property-keyed
> affix pools described here are being replaced by the **Identity + Signature system** —
> design of record: `docs/identity-foundation.md` (DECISIONS **D42–D44**). The grant/trigger
> vocabulary this document defines **survives** — it is the target machinery of the new
> Signature grammar; the material-genetics half is what goes. Accurate for the **code as
> shipped** until the migration lands.

> **DECIDED** — settled by the 27 decisions in `effect-foundation.md` §12. Not yet built. Part of the effect-foundation package (`effect-foundation.md`).
> **Extends `emergent-item-system.md` §16 (fabrication); does not replace it.** Amends GDD §10.
> Labels: **[EXISTING/PRESERVE]** · **[DECIDED]** · **[UNRESOLVED]**

---

# 0. The claim this system has to deliver

Every ARPG lets you find an item and gamble on it. **This game lets you invent the material
first.** The whole chain has to be one continuous system:

```
invent the material  →  fabricate the object  →  the object has a GENOME
   →  the genome decides what CAN roll, how LIKELY, and how STRONG
      →  gamble on the affixes  →  manipulate them  →  risk it all on Overreach
```

If the genome step is weak, this is just Path of Exile with a crafting minigame bolted on the
front. If it is strong, the sentence *"I made a material specifically to enable this affix"* is
true, and nothing else on the market does that.

**The design rule that follows:** *every* affix property — eligibility, weight, tier ceiling and
roll quality — must be a **pure function of the item's genome**, and the player must be able to
**see all four before rolling.**

---

# 1. Naming — keeping two Prefix systems apart **[DECIDED — D-17]**

The project already has **Character** Prefixes and Suffixes (`prefix.galvanic`,
`suffix.exploding_kneecaps`). Item affixes also want prefix/suffix slots.

**The compiler collision is illusory** — `AffixSlot.Prefix` (enum member) and `PrefixDefinition`
(type) are different names in different namespaces. The real risk is human: reading `prefix` in a
JSON file six months from now and not knowing which system owns it.

**Decided: namespace + qualification + player-facing wording.**

| | Character identity | Item modifiers |
|---|---|---|
| Namespace | `Dungeons.Characters.Composition` | **`Dungeons.Affixes`** |
| Types | `PrefixDefinition`, `SuffixDefinition` | `AffixDefinition`, `AffixSlot { Prefix, Suffix }` |
| Ids | `prefix.galvanic` | **`affix.*`** — never `prefix.*` |
| Player-facing word | "your **Prefix**" | "item **modifiers**" — the word *prefix* never appears in item UI |
| Rule | `AffixSlot.Prefix` is always written qualified in code | |

The bare word `Prefix` therefore only ever means the character layer, in code *and* in the UI.
A validator rule rejects an affix id starting `prefix.` or `suffix.`.

**Rejected (b):** renaming the item side to invented jargon (Etchings/Marks). It removes the
collision at the cost of teaching players two new words for a concept every ARPG already named,
and every wiki habit and community term stops transferring.

**Rejected (c):** renaming the *character* layer instead — even though it is the non-standard
usage, and in every other ARPG "prefix/suffix" means item affixes. It was the intellectually
tidier option and it loses on cost: a save break (`prefix.*`/`suffix.*` ids are persisted), 75
authored entries re-ided, GDD §3 / `classes.md` / DECISIONS D22 rewritten, `ClassNameFormatter`
and 9 name formats touched — and new terms would still need designing. **Noted for the record
that this only gets more expensive**; if the confusion ever bites in practice, this is the fix.

---

# 2. The Genome **[DECIDED — the centrepiece]**

## 2.1 Definition

When fabrication (`emergent-item-system.md` §16.3) resolves a form's slots into an
`ItemInstance`, it also computes the **Genome** — the item's genetic profile, stored on the
instance and never recomputed.

```csharp
sealed record Genome(
    string FormId,
    IReadOnlyDictionary<string,double> Pressure,   // 0–100 per material property, slot-weighted
    IReadOnlyDictionary<string,double> Essence,    // fire/frost/storm/… , usually empty
    IReadOnlyList<TraitRef> Expressed,             // traits that made the aperture cut
    IReadOnlyList<TraitRef> Dormant,               // traits that didn't — still count for value
    IReadOnlySet<string> Tags,                     // form:, class:, origin:, state:
    int Potency,
    int GenerationDepth,
    IReadOnlyList<string> Signatures);             // fabrication signatures that fired
```

## 2.2 Pressure — the one new computation

**Pressure** is *not* the raw property average. It is the **stat-map-weighted** value: how much
this property actually reaches the parts of the item that matter.

```
pressure(p) = Σ over slots s of ( material(s).property(p) × relevance(form, s, p) )

relevance = the form's stat_map weight for that (slot, property), renormalised per property,
            falling back to mass_share when the stat_map does not mention it
```

This means the **same materials in different forms produce different genomes**, which is the
whole point of `emergent-item-system.md` §16.2's *"stats read from named slots, never from a
blend"* rule — extended from stats to affixes.

> Stormglass in a **longsword edge** (edge relevance 0.60 for charge) → `charge` pressure 79
> Stormglass in a **plate binding** (binding relevance 0.15) → `charge` pressure 22
>
> Same material. The sword can roll Tier-1 storm affixes. The plate cannot roll them at all.

**This single rule is what stops one globally-best material existing** (GDD §17.7 / brief §36.7),
and it does it without any authored combination.

## 2.3 What the player sees — the Genome Readout (Assay) **[DECIDED — required scope]**

The third legibility artefact (`effect-foundation.md` §2.3). Without it, "engineer the casino"
is a lie — the player would be gambling blind, which is exactly what this design is supposed to
replace.

```
Emberlit Iron Longsword                    [Fabricated · Gen 3 · Potency 68]

GENETIC PRESSURE                           ELIGIBLE MODIFIER FAMILIES
  hardness      78  ██████████████▍         Heat            ▸ up to Tier 2   (heat 71)
  mass          64  ███████████▌            Physical        ▸ up to Tier 1   (hardness 78)
  heat          71  ████████████▊           Stagger         ▸ up to Tier 2   (mass 64)
  conductivity  41  ███████▍                Charge          ▸ up to Tier 4   (charge 41 — need 60 for T3)
  charge        41  ███████▍                Life/Recovery   ✕ locked         (growth 4 — need 35)
  growth         4  ▊                       Storm (Exotic)  ✕ locked         (needs essence.storm ≥ 30)
  arcane         0

  TRAITS  Emberveined (61) expressed · Resilient (44) dormant
  SLOTS   ▪▪▫ prefix 2/3    ▫▫▫ suffix 0/3
```

Everything in that panel is a pure function. Everything is testable. And it turns the
"gamble on the affixes" step from a slot machine into an **engineering decision followed by** a
slot machine — the brief's principle 4 exactly.

---

# 3. Affix architecture **[DECIDED — D-21]**

## 3.1 Item structure

PoE's bones, because they are proven and everybody already knows how to read them:

| Layer | Count | Source | Rerollable? |
|---|---|---|---|
| **Innate** | 1–3 | computed from the Genome at fabrication — *not rolled* | no |
| **Affix-Prefix** | ≤ 3 | rolled from the weighted pool | yes |
| **Affix-Suffix** | ≤ 3 | rolled from the weighted pool | yes |
| **Exotic** | 0–1 | rare roll, or Overreach | Overreach only |
| **Signature** | 0–1 | requires a fabrication signature to have fired | no |
| **Anomalous** | 0–1 | **Overreach only** — the only affixes that may bend proc rules | no |

**Innate modifiers are the genome speaking directly [DECIDED — D-21].** A sword whose edge
material has `hardness 78` gets `+12% Armour Penetration` as an innate — no roll, no luck, purely
earned by material choice.

This is the layer that is **ours rather than PoE's**, and it is load-bearing for the whole
premise:

- **Good crafting pays off before the gambling starts.** Material invention *guarantees* a
  result, rather than only shifting a probability distribution.
- **A well-engineered item is never a total loss.** A terrible affix roll still leaves the thing
  you built, so a player who spent thirty crafts reaching a genome is never handed nothing.
- **It is the answer to "why invent materials at all?"** Without it, two players with wildly
  different materials can land functionally identical items on lucky and unlucky rolls, and the
  invention chain becomes decoration on a slot machine.

**The tension it costs, acknowledged:** innates are power that bypasses the casino, and the
casino is where the drama lives. The counterweight is that innates are *modest and predictable*
by design — they are the floor, never the ceiling. Everything exciting still comes out of the
rolled pool, Exotics, Signatures and Overreach.

**Innates are never rerollable** (U-7). An operation that rerolled them would sever material
choice from outcome, which is the one thing this layer exists to prevent.

Prefixes are conventionally offensive/character, suffixes defensive/utility, but the split is
enforced by the affix's declared `slot`, not by a rule about families.

## 3.2 `AffixDefinition`

```jsonc
{
  "id": "affix.storm_conduit",
  "name": "Storm Conduit",
  "slot": "suffix",
  "family": "charge_avoidance",         // ONE affix per family per item — the anti-stacking rule
  "class": "exotic",                    // standard | trigger | exotic | signature | anomalous
  "tags": ["lane:charge", "mech:negate"],

  "eligibility": {
    "forms_any":  ["light_armour","heavy_armour","shield","focus"],
    "requires":   [ { "property": "conductivity", "min": 45 },
                    { "property": "charge",       "min": 30 } ],
    "requires_any_essence": ["storm"],
    "requires_signature":   null,
    "excludes_family":      ["charge_resistance_max"]
  },

  "weight": {
    "base": 40,
    "scale": [ { "property": "conductivity", "per10": 14 },
               { "property": "charge",       "per10": 9  },
               { "essence":  "storm",        "per10": 30 } ]
  },

  "tiers": [
    { "tier": 4, "requires": { "conductivity": 45 }, "range": [0.03, 0.05] },
    { "tier": 3, "requires": { "conductivity": 60 }, "range": [0.05, 0.08] },
    { "tier": 2, "requires": { "conductivity": 75 }, "range": [0.08, 0.11] },
    { "tier": 1, "requires": { "conductivity": 88, "essence.storm": 40 }, "range": [0.11, 0.15] }
  ],

  "grants": [
    { "type": "stat", "key": "combat.avoid.lane", "scope": "lane:charge", "value": "$roll" }
  ],

  "drawback": null,
  "description": "$roll% chance to negate Charge hits."
}
```

**`grants` is the same `Grant[]` atom** as a status's `while_active`, a class prefix's rules, and
a tool's bonuses (`effect-foundation.md` §2.1). An affix that grants a **`RuleGrant`** instead of
a **`StatGrant`** is a triggered affix; nothing else about it differs.

## 3.3 The three genetic levers, exactly as the brief names them

| Lever | Field | Meaning |
|---|---|---|
| **Eligibility** | `eligibility` | *Can this roll at all?* Hard gate. All conditions must pass. |
| **Weight** | `weight` | *How likely?* `base + Σ (pressure/10 × per10)`, floored at 0. |
| **Tier ceiling** | `tiers[].requires` | *How strong?* The highest tier whose requirements the genome meets. |

Plus a fourth, quieter one:

| **Roll quality** | `potency` | Where in the tier's range the value lands: `roll = lerp(range, 0.35 + 0.65 × potency/100 ± variance)` |

That gives `potency` a real second job (`emergent-item-system.md` §6.1 defined it as an
expression coefficient) and closes the loop the crafting spec wanted: *a high-potency mundane
material beats a low-potency exotic one*, now true for affixes as well as for stats.

## 3.4 The brief's genetics examples, made concrete

| Brief's example | Mechanism |
|---|---|
| High Charge unlocks Charge affixes | `eligibility.requires: charge ≥ 30` |
| High Charge **+ Conductivity** weights them more | `weight.scale` on both properties |
| Extreme Charge + Conductivity + **ThunderGlass Signature** unlocks exotic Storm affixes | `class: exotic` + `requires_signature: sig.thunderglass` + T1 tier requirement |
| High Growth unlocks life/recovery | `growth ≥ 35` gates the **Barrier** family (`damage-and-defense.md` §5.7) |
| High Toxicity unlocks Poison | `toxicity ≥ 40` gates ailment-application affixes in the toxin lane |
| High Corrosion unlocks armour stripping | `corrosion ≥ 40` gates Corroded-application and armour-reduction affixes |
| High Cold unlocks Chill/Freeze | `cold ≥ 40` for Chill; Freeze application needs `cold ≥ 65` **and** the form to be a weapon |
| High Heat unlocks Burn | `heat ≥ 35` |
| High Resonance + Essence unlocks supernatural/spell affixes | `resonance ≥ 50` **and** `requires_any_essence` |

**Note what falls out:** you cannot roll poison affixes on a storm sword, because you cannot
*fabricate* a storm sword with high toxicity — the reaction algebra's off-channel dilution
(§8.3) washes out the properties you didn't focus. **The crafting engine's anti-accumulation rule
is what makes affix pools naturally specialised.** That is the two systems being genuinely one
system, not two systems politely referencing each other.

## 3.5 Affix families and anti-stacking

`family` is the unit of exclusion: **one affix per family per item.** Families are declared in
data and validated. This prevents "three sources of charge avoidance on one glove" without
needing per-affix exclusion lists.

Cross-family safety is handled by the modifier caps (`effect-foundation.md` §4.4) — families
control *item* composition, caps control *build* totals. Both are needed; neither substitutes for
the other.

---

# 4. Rolling **[DECIDED]**

```
Roll(genome, slot, seed):
  1  pool     = every affix where slot matches, eligibility passes,
                family not already present, excludes_family not present
  2  weights  = base + Σ (pressure/10 × per10)          [drop weight ≤ 0]
  3  pick     = weighted choice via IRandomSource
  4  tier     = the highest tier whose requires{} the genome satisfies
  5  value    = lerp(tier.range, 0.35 + 0.65 × potency/100 + variance)
  6  attach   = instantiate grants[] with $roll substituted
```

Deterministic given the seed. Pure apart from step 3. Fully inspectable in the Item Lab, which
shows the entire weighted pool with each term of each weight broken out — the same provenance
discipline `ModifierContribution.Source` already established.

---

# 5. Crafting operations **[DECIDED]**

The brief's requirement: *"no arbitrary magic-currency pile disconnected from crafting."*

**Every operation is paid for with materials the game already produces** — and the most common
currency is **destruction byproducts** (Slag · Cinders · Dross · Residue), which the crafting
engine already generates when integrity hits zero (`emergent-item-system.md` §6.2c). Failed
crafts fund the affix casino. That is a genuinely elegant loop and it costs nothing to build.

| Operation | Cost | Effect | PoE analogue |
|---|---|---|---|
| **Anneal** | Cinders ×N | Reroll the numeric values of all affixes; affixes and tiers unchanged | Divine |
| **Etch** | a material with pressure in the target family | Add one affix to a free slot, **weighted toward the reagent's genetic families** | Exalt + Essence |
| **Scour** | Slag ×N | Remove one random affix | Annul |
| **Reforge** | Dross + a substrate material | Reroll **all** affixes. **Tier ceilings come from the reagent's genome, not the item's** — so a superb reagent lifts a mediocre item | Chaos |
| **Bind** | a rare stabiliser material | Protect one affix from the **next** operation. Consumed on use | Crafting bench "lock" |
| **Temper** | a high-potency material in the affix's family | Attempt +1 tier on one affix. **On failure, −1 tier.** | Harvest reforge-more |
| **Fracture** | a Signature-bearing material | **Permanently lock** one affix — never removable, never rerollable. Enables safe rerolling of everything else | Fractured/Synth |
| **Overreach** | see §6 | the final casino | Corruption / Vaal |

**Every operation respects the Genome.** You cannot Etch a storm affix onto a low-conductivity
item at any price. **The gambling is bounded by the engineering** — brief principle 4, enforced
by construction rather than by tuning.

**Etch and Reforge are where material invention pays off twice:** the material you invented does
not only make the item, it also *steers what the item can become afterwards*.

---

# 6. Overreach **[DECIDED — D-22]**

## 6.1 The rule

> **The outcome pool is drawn only from the item's own genetic families.**
> A poison dagger can never Overreach into a lightning effect.
>
> **Players engineer which casino they enter. They do not control whether it pays out.**

## 6.2 Outcomes

| Outcome | Weighted up by | Result |
|---|---|---|
| **Ruin** | high `instability`, high strain, low fabrication integrity | Item destroyed → byproducts (the same table crafting already uses) |
| **Brick** | strain, generation depth | Affixes scrambled to their lowest tiers; the item becomes permanently un-Overreachable |
| **Mutation** | *(baseline — the most common)* | One affix replaced by another **in a related family** at equal tier |
| **Elevation** | potency, expressed trait count | One affix raised **one tier beyond its normal genetic ceiling** |
| **Exotic Mutation** | essence total, `resonance` | Gains an **Exotic** affix drawn from the item's own genetic families |
| **Transcendence** | superseded traits + potency ≥ 90 + a fabrication signature | Gains an **Anomalous** affix, and a **proper name** from the epithet grammar (`emergent-item-system.md` §16.5) |

Weights are a pure function of the genome, so **the Item Lab can show the player their exact odds
before they commit.** Overreach is a knowing risk, not a mystery — the same fairness argument
that made the pre-commit integrity projection required scope in the crafting spec.

## 6.2.1 Overreach is repeatable, and gets worse every time **[DECIDED — D-22b]**

An item may be Overreached again — **unless it Bricked**, which ends it permanently. Each attempt
raises the Ruin and Brick weights:

| Attempt | Ruin | Brick | … | Transcendence |
|---|---|---|---|---|
| 1st | 8% | 5% | | 2% |
| 2nd | 20% | 11% | | 4% |
| 3rd | 38% | 18% | | 7% |
| 4th | 60% | 24% | | 11% |

*(Illustrative — the escalation curve is a tuning constant, not a design commitment.)*

**Why repeatable-with-escalation rather than one-shot.** GDD §13.2 identifies a deliberate rhyme:
the same decision shape at three scales — *extract now or go deeper* · *refine once more or commit
this material* · *spend the resource now or hold it*. A one-shot Overreach breaks that rhyme at
exactly the moment the stakes are highest. Escalating odds make it the fourth verse: **push once
more, or stop?**

**Why not flat odds.** With no escalation, Transcendence becomes a function of how many reagents
you farmed rather than how much nerve you had — which removes the gamble from the gambling
system.

**Consequence worth noting:** the interesting decision is rarely the first Overreach (8% Ruin is
cheap). It is the third, holding an item that is already better than anything you own.

## 6.3 Anomalous affixes — where proc safety is allowed to bend

**Anomalous affixes exist only here**, and they are the **only** content permitted to raise
`MAX_PROC_DEPTH` — by exactly 1, never more (`effect-foundation.md` §6.3).

That ties the whole package together: **the safety valve on recursion is also the top-end reward
of the crafting casino.** Examples: *thorns trigger your on-hit effects* · *your retaliation can
chain* · *Burn you apply can itself Ignite a second target*. Each is a rule that is deliberately
unsafe, deliberately bounded, and deliberately only obtainable by risking a masterpiece.

This is the brief's principle 9 — "weird effects belong at the top" — implemented as an
architectural rule rather than as a tuning convention.

## 6.4 Consumables and tools Overreach too

Consumables are fabricated by the same system (`emergent-item-system.md` §16.6) and are the
natural home for *negative* emergent outcomes. A bricked draught that heals and corrodes is a
memorable story; a bricked sword is a bad afternoon. **Recommend allowing Overreach on
consumables at a much lower cost**, as the cheap way for players to learn the mechanic before
risking a weapon.

---

# 7. Affix families — the taxonomy **[DECIDED]**

Full concepts are in `effect-catalog.md`. The families and their genetic drivers:

| Group | Families | Genetic driver |
|---|---|---|
| **Offence** | flat damage · increased damage · lane damage · action interval · penetration · crit chance · crit damage · stagger · conditional damage | `hardness` `mass` `instability` + lane properties |
| **Character** | attributes · max Health · max Mana · max Stamina | `mass` (health) `resonance` (mana) `flexibility` (stamina) |
| **Defence** | armour · resistance (per lane) · max resistance · block strength/window · parry · evade · Resolve | `hardness` `insulation` `mass` + `*_resistance` |
| **Avoidance** | lane negation · status avoidance | high lane property **+** `insulation` or `resonance` |
| **Retaliation** | flat thorns · increased thorns · reflect % · conditional thorns · thorns riders | `hardness` `corrosion` + lane properties |
| **Ailment** | application chance · duration · magnitude · spread · consume | the matching lane property |
| **Control** | control buildup · control duration · Resolve reduction | `mass` (stagger) `cold` (freeze) `decay` (fear) |
| **Resource** | on hit/crit/block/kill/status · reduced costs · gauge generation | `resonance` `charge` `growth` |
| **Recovery** | **Barrier** gain/capacity/decay · conditional capped healing · cleanse | `growth` `resonance` |
| **Triggered** | on hit · crit · block · parry · dodge · kill · take hit · barrier break · control resisted | any — gated by `arcane`/`instability` |
| **Move mod** | damage · aspect · conversion · targets · chains · costs · timing · added effects · **granted moves** | `arcane` `resonance` + lane |
| **Conversion** | damage conversion · added-as-extra · resource conversion | `conductivity` `affinity` |
| **Conditional** | low/high Health · vs status · after defence · at high resource · vs tag | any |
| **Profession** | interval · yield · preservation · doubling · rare weighting · quality · mastery XP · craftsmanship · integrity cost · catalyst | see `profession-tools.md` |
| **Realm** | hazard resistance · Knowledge gain · extraction · assay | `insulation` `resonance` |

---

# 8. Validation **[DECIDED]**

| Rule | Catches |
|---|---|
| Every `grants[]` key/effect/status/lane/tag resolves | typos |
| Every affix is eligible for ≥1 form | dead affix |
| **Every affix is reachable** — some real material combination in some form can satisfy its eligibility **and** its T1 requirements | an affix nobody can ever roll |
| Tier requirements are **monotonic** (T1 ≥ T2 ≥ T3 ≥ T4) and ranges do not overlap | malformed tiers |
| `family` is declared for every affix; `excludes_family` names real families | broken exclusion |
| `class: anomalous` ⟹ obtainable only via Overreach | leaked Anomalous into the normal pool |
| Only `class: anomalous` may set `proc.max_depth > 2` | proc-safety bypass |
| Affix id does not start `prefix.`/`suffix.` | the naming collision (D-17) |
| `$roll` appears in `grants` **and** `description` | silent tooltip drift |
| Every `danger: true` modifier key targeted by an affix has a cap | uncapped avoidance |
| Every declared family has ≥2 affixes | a family of one is not a family |

**Distribution tests (seeded, N ≥ 100k):** rolled affix frequencies match declared weights within
tolerance · tier ceilings are never exceeded by any genome · potency correlates with roll
position · no affix ever appears twice from one family on one item.

---

# 9. Dependencies and open questions

**⚠ This system cannot be built until fabrication (`emergent-item-system.md` P5a) exists.** There
is no path from a material to a piece of equipment today, and affixes on four hand-authored items
would be a normal ARPG rather than this one. Build order: `effect-foundation.md` §10 (C1 → C2 →
E5).

**⚠ The 0–100 vs ~0–5 scale reconciliation is a combat rebalance** (GDD §18 Q3) and must be
budgeted as its own piece of work inside C2, not absorbed into E5.

**[UNRESOLVED]**

- **U-3 Durability.** `stat_map.durability` is authored in the fabrication spec and the game has
  none. Several proposed affixes ("reduced Integrity damage", tool preservation) assume an item
  can degrade. **Recommend: still defer** — the extraction loop supplies the risk pressure, and
  Overreach supplies the item-loss drama.
- **U-7 Can Innate modifiers be rerolled?** Currently no, by design (they are the genome
  speaking). An operation that rerolls innates would let players separate material choice from
  outcome — which weakens the central claim. **Recommend: never.**
- **U-8 Affix slot counts.** 3+3 is PoE's number and it is proven. **[UNRESOLVED]** whether
  forms should differ (a two-handed weapon getting 4+3, say). Recommend uniform 3+3 for v1.
- **U-9 Trade/sharing.** Emergent materials are shareable by signature (`emergent-item-system.md`
  §12.2). Whether *items* are, and what that does to the casino's value, is undesigned.
