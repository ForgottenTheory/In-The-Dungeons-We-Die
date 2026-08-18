# Crafting — the whole stack, as it stands

> **What this is.** One place that explains crafting end to end: what it does, how the six layers
> fit together, what actually ships, and what does not. Written against the repository, with every
> count and constant read out of the content and code rather than carried over from older docs.
>
> **What this is not.** Not the specification — `docs/emergent-item-system.md` (§1–§20) holds the
> mathematics and is authoritative wherever this summarises. Not a code map either; for files and
> call flow see `docs/code-map.md` §10.4–10.6.
>
> Verified against the repo on **2026-08-16** — build clean, 765 tests passing.

---

# 1. The one claim

**Crafting is a total function, not a lookup.**

Every combination of substrate, reagents and process produces a real, named, stackable result —
always, computed by one universal algebra. There is **no recipe table and no per-combination rule
anywhere in the codebase.** Authored content is eight processes, sixteen traits, seven essences,
three forms, four byproducts and a name grammar. Everything else is computed.

The consequence that matters for design: **unusual experimentation yields legitimate results
rather than "nothing happens".** That is the entire reason the system exists.

---

# 2. The stack at a glance

Six layers. Each is a separate mechanism; each hands its output to the next.

```
  ┌─ 1. MATERIALS ────────────────────────────────────────────────────── ✅ BUILT
  │    480 definitions · 21 properties on a 0–100 scale · family:value tags
  │    Gathered by professions, dropped by realms, or produced by layer 2.
  ▼
  ┌─ 2. THE BENCH — the reaction engine ──────────────────────────────── ✅ BUILT
  │    substrate ← reagent₁ ← reagent₂ … (+ catalyst) under one PROCESS
  │    converge → drift → oppose → prune · potency · integrity · destruction
  │    → quantize → hash → register a new stackable MATERIAL
  │    ↺ the output is a material, so it feeds straight back into layer 1
  ▼
  ┌─ 3. TRAITS ───────────────────────────────────────────────────────── ✅ BUILT
  │    Named discrete qualitative states born from the settled property state.
  │    Cap 3 · weakest displaced · pairs supersede · every trait has a drawback.
  ▼
  ┌─ 4. ESSENCE ──────────────────────────────────────────────────────── ✅ BUILT
  │    The supernatural tier. 7 typed essences · capacity governed by resonance ·
  │    excess becomes STRAIN, not a cap. Attune builds the vessel first.
  ▼
  ┌─ 5. FABRICATION — the terminal boundary ──────────────────────────── ✅ BUILT
  │    FORM + one material per named SLOT → an ItemInstance.
  │    stat maps · apertures · dormancy · the 0–100 → combat-unit reconciliation.
  │    Materials stop here. Nothing goes back.
  ▼
  ┌─ 6. THE GENOME AND ITS MODIFIERS ─────────────────────────────────── ✅ BUILT
  │    Genome = stat-map-weighted property PRESSURE + essence + traits + tags
  │             + potency + generation.
  │    → eligibility · weight · tier ceiling · roll position
  │    → 1–3 deterministic INNATES + up to 3+3 rolled modifiers.
  ▼
       EQUIPMENT the player wears, in combat units, speaking gameplay language.
```

**Two boundaries are worth internalising:**

- **Layer 2 is a loop; layer 5 is a door.** A crafted material is *a material*, so it re-enters
  the bench with no special-casing. Fabrication is terminal and irreversible.
- **Layers 1–4 speak the simulation language; layers 5–6 speak gameplay.** The translation seam
  is fabrication, and it happens in exactly one place (§7).

---

# 3. Layer 1 — materials, the input ✅

**480 definitions** across nine category files in `game/data/materials/`:

| File | Count | |
|---|---:|---|
| `fauna.json` | 114 | creature parts — hide, fur, meat, bone, fang, blood |
| `flora.json` | 108 | |
| `minerals.json` | 84 | |
| `environmental.json` | 54 | |
| `fungal.json` | 45 | |
| `elemental.json` | 36 | the rare property-profile spikes |
| `processed.json` | 31 | ingots, leather, planks |
| `byproducts.json` | 4 | Slag · Cinders · Dross · Residue |
| `prepared.json` | 4 | `form:meal` / `form:tincture` |

Authored **biome-by-biome as a design lens** — there is deliberately no biome field. Deliberately
**mundane-majority**, so the rare things stand out by their property *combination*, not by tier.
**Never MMO tiering:** Oak / Ironwood / Emberwood / Frostpine all fill the "wood" role and behave
differently.

### The 21 properties, by role

| Role | Properties | Behaviour in a reaction |
|---|---|---|
| **Structural** (9) | hardness, mass, flexibility, affinity, conductivity, insulation, solubility, resonance, instability | Blend *slowly* toward a mass-weighted mixture. Define what the material **is** |
| **Reactive** (8) | heat, cold, charge, toxicity, growth, decay, corrosion, arcane | Transfer along the process channel; subject to opposition. Define what it **does to things** |
| **Response** (3) | heat_resistance, cold_resistance, toxin_resistance | Derived. **Never a reaction input** |
| **Sourcing** (1) | harvest_resistance | Inert in crafting; read only by gathering |

The four load-bearing distinctions: `heat`/`cold`/`charge` (influence *introduced*) vs resistances
(influence *resisted*) · `charge` (energy) vs `conductivity` (transmission) · `toxicity` (attacks
life) vs `corrosion` (attacks material) · `affinity` (willingness to bond) vs `instability`
(unpredictability).

> **Affinity is the single most important gate in the engine.** It decides how willingly a
> substrate accepts anything at all.

### Definition → profile

An authored `MaterialDefinition` is a flat property map. `MaterialProfileResolver` turns it into
the runtime `MaterialProfile` the engine actually operates on:

```
MaterialProfile = properties + potency + integrity + traits + essence + lineage + signature
```

Authored materials get derived defaults (potency from their two strongest properties — floor 20,
slope 0.30; integrity 100). Emergent materials carry the profile they were born with.

---

# 4. Layer 2 — the bench ✅

## 4.1 The shape of a craft

```
Substrate            the thing being transformed
  ← Step 1: Reagent A   ⇒ intermediate state
  ← Step 2: Reagent B   ⇒ intermediate state
  ← Step 3: Reagent C   ⇒ final state
  + Catalyst (optional) not consumed; modifies rates, transfers nothing of its own
  + a Process           decides which properties participate at all
```

**Order matters automatically**, because step 2 acts on a different intermediate state. Six
outcomes from three reagents with zero authored triples — and it is *reasonable*: the player can
inspect the intermediate and predict the next step.

## 4.2 The eight processes

Read from `game/data/processes/processes.json`.

| Process | Profession gate | Substrate gate | Medium | Severity | Essence rate | Channel (property → rate) |
|---|---|---|---|---:|---:|---|
| **Grind** | *ungated* | — | mechanical | 0.30 | 0.10 | solubility .45 · mass .35 · hardness .30 |
| **Steep** | Herblore 1 | — | solvent | 0.20 | 0.20 | heat .55 · toxicity .55 · growth .55 · cold .50 · decay .45 |
| **Quench** | Smithing 5 | `form:metal` | thermal | 0.35 | 0.25 | cold · hardness · flexibility |
| **Attune** | *ungated* ⚠ | — | arcane | 0.35 | 0.30 | resonance .60 · arcane .35 |
| **Alloy** | Smithing 10 | `form:metal` | thermal | 0.45 | 0.20 | hardness · mass · conductivity · flexibility · affinity |
| **Distill** | *ungated* ⚠ | — | solvent | 0.50 | 0.55 | toxicity · arcane · decay · corrosion · solubility |
| **Forge Infusion** | Smithing 15 | `form:metal` | thermal | 0.55 | 0.45 | heat · charge · hardness · affinity |
| **Smelt** | Smithing 1 | `form:ore` | thermal | 0.60 | 0.15 | hardness · mass · conductivity · heat |

*(Listed gentlest-first, which is also the order the bench's picker uses.)*

> ⚠ **Temporary (2026-08-17).** Distill and Attune are ungated **for playtesting only**. Their
> designed gates are **Herblore 12** and **Alchemy 10**; the Hideout split gave each its own
> station (Alchemy Lab, Runic Altar) and those gates named a profession neither station trains,
> so the stations could not be exercised. The override is marked at both entries in
> `processes.json` and named in `CraftingActionContentTests.OnlyGrindIsUngated` — deleting the two
> ids from that test's exception list is what "restored" looks like, whether by putting the old
> gate back or by re-homing it to the station's own profession.

**A process also declares:** `role_weights` (how much of the result comes from substrate vs
reagents vs catalyst — Grind is 0.80/0.20/0.00, Steep 0.60/0.35/0.05) and `tag_effects`
(what it sets and clears — Grind sets `form:powder` + `state:processed` and clears any prior
`form:*`/`state:*`).

### Media — why an ingredient suits a process

The **medium** decides which property governs how readily a reagent gives up what it carries:

| Medium | Releases by | So the good reagents are… |
|---|---|---|
| solvent | `solubility` | saps, extracts, soluble powders |
| thermal | `instability` | volatile cores, unstable minerals |
| mechanical | inverse `hardness` | soft, crushable things |
| arcane | `resonance` | crystals, ley-touched matter |

This is the whole reason **Ember Sap is an alchemy reagent and Ember Core is a forge reagent**,
without either being authored as such.

## 4.3 The algebra, per reagent step

`ReactionAlgebra.ApplyReagent` — five stages, in order:

1. **Acceptance / release.** How willingly the substrate bonds (`affinity`), how readily the
   reagent releases (the medium's property). Both map `0–100 → 0.25 + 0.75 × fraction`, so
   nothing is ever fully inert or fully compliant.
2. **Channel convergence.** On-channel properties move a *fraction of the remaining gap* toward
   the reagent's value, capped at 0.85 of that gap. **They never add, and can never exceed the
   strongest input.** This one rule kills unbounded stat escalation permanently, with no caps and
   no fudge factors.
3. **Off-channel handling.** Structural properties blend gently toward a mass-weighted mixture
   (rate 0.10); reactive properties **dilute toward zero and receive nothing** (rate 0.08). Each
   transformation therefore *focuses* the material and washes the rest out — which is what stops
   a deep material accumulating twenty-five nonzero properties.
4. **Opposition.** Opposed pairs (heat/cold, growth/decay) mutually annihilate at rate 0.9,
   releasing **strain**. **You cannot stockpile opposites.**
5. **Floor pruning.** Trace values below the floor (default 5) are pruned to zero.

Every movement is emitted as a typed `PropertyChange` carrying **why** it moved (channel /
structural blend / dilution / opposition) — which is what the Reaction Log and the whole player
crafting language are built on.

## 4.4 Potency — how strongly it expresses

A **weighted mean, never a sum**, so adding a junk input *lowers* it. Ceiling is
`best input + 8 × craft quality`.

**The consequence is a design pillar:** a high-potency mundane material beats a low-potency
exotic one, so base resources stay relevant forever and there is no "stop mining copper at
level 20" cliff.

## 4.5 Integrity — the transformation budget

**Not durability.** Integrity is how much more transformation a material can survive.

```
cost ≈ Δstate × 12.0 × severity  +  strain × 0.3       (minimum 1.0)
       − up to 25% mitigated by craft quality
```

Cost scales with **how violent the change was**, so gentle well-chosen steps cost little and
brute force costs a lot. **Elegant crafting is mechanically rewarded — this is the main skill
axis of the system.**

**Integrity 0 destroys the material.** That is only fair because three things are guaranteed:

1. The bench shows the projected cost and result **before** commitment.
2. Below **integrity 25** it shows a destruction *chance* rather than a false certainty.
3. Destruction yields **byproducts** — Slag · Cinders · Dross · Residue, chosen by the material's
   `form:` tag — which are useful reagents in their own right.

> A blown craft is a setback with a consolation prize, never a zero.

The tension is the point, and it rhymes deliberately with the extraction decision: *push one more
transmutation, or commit and forge it now?*

## 4.6 Craft quality and variance

```
craftQuality = 0.10 baseline
             + 0.40 × (profession level / max)
             + 0.35 × timing performance        (0.5 for a passive craft)
             − effectiveInstability / 150
```

`effectiveInstability` rises as integrity falls **and** as essence strain grows — so the same
crafter has less control over step three than step one, and an overloaded vessel is a wilder
vessel. That is the whole *"attune first, then infuse"* lesson, expressed as a formula rather
than a tutorial.

Quality's most important effect is **narrowing variance**, not adding a bonus. Mastery means
*control*.

> **Variance produces different materials, not random stats.** A bad roll gives you a
> *different, weaker material with its own name* — possibly one nobody has seen — never
> "Emberveined Iron (bad roll)". High skill narrows variance to zero; low skill scatters you
> across neighbouring buckets and you find things by accident.

## 4.7 Identity — signatures and stacking

The settled state is **quantized** (properties and potency to 5-point buckets, lineage weights to
0.10), hashed with SHA-256, and truncated to `emergent.7f3a91c4`. That id is registered as a
runtime `MaterialDefinition` **into the same store the authored library lives in**, so emergent
materials flow through inventory, lookups, crafting inputs and loot **with no special-casing**.

Identical results **stack**. Two players who reach the same state get the same material with the
same name, so discovery is shareable and the save stays small.

> **`ItemInstance` is equipment-only.** An earlier design made any divergent material a unique
> per-unit instance; under that rule forty units of one alloy were forty objects and the
> inventory, save file and UI all broke. Reversed deliberately (D20).

**Traits and essence join the signature** (`id:tier` for traits). **Integrity does not** — a
recorded tension: an archetype keeps the integrity of its *first* discovery, so reaching the same
state by a cheaper path inherits the wrong remaining budget. Judged self-balancing in practice;
filed, not fixed.

## 4.8 Naming

A **pure function of final state, never of history** — history-based names grow without bound.

Hard constraints: **max 3 words** · at most one intensity adjective · **no "of X"** · **no tier
words** (Greater/Superior/Lesser) · **numbers never appear**.

Intensity comes from vocabulary ladders, not adjectives-of-adjectives:
`heat → Warmed · Emberlit · Cindered · Searing`.

Real output: *Emberlit Iron · Warmed Iron Tincture · Chilled Iron · Tainted Oak Tincture ·
Lightning-Veined Copper · Hardened Granite Dust*. On a collision it coins a stable syllable
(*Lunith Iron*) — never a number.

## 4.9 The Reaction Log — required scope, not polish

> *"A system this deep is only playable if it explains itself."*

Every craft emits a structured, human-readable trace where **every line states why**. It is
simultaneously the tutorial, the debugger and future codex content.

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

## 4.10 Project vs Resolve — why the preview cannot lie

`ReactionEngine` has exactly two public methods, and they run **the same pipeline**:

| | `Project(request)` | `Resolve(request)` |
|---|---|---|
| Variance | **off** | on |
| Inventory | untouched | inputs consumed, result deposited |
| Registry | untouched | archetype registered |
| Returns | `CraftProjection` — integrity projection, potency, the name it would get, typed steps, preview log | `CraftOutcome` — the real thing, or byproducts |

The only difference is a boolean and what happens afterwards. That is *why* the pre-commit
projection is trustworthy: it is not a second model of the craft, it **is** the craft.

---

# 5. Layer 3 — traits ✅

**16 traits** in `game/data/traits/traits.json`, in five categories:

| Category | Count | Traits |
|---|---:|---|
| structural | 5 | Resilient · Unyielding · Porous · Keen · Adamant |
| charge | 4 | Stormlaced · Conductive · Galvanic Rime · Cycled |
| thermal | 3 | Emberveined · Frostbound · Tempestforged |
| vital | 3 | Verdant · Blighted · Volatile |
| toxic | 1 | Venomous |

A trait is a **named, discrete, capped qualitative state** born from the *settled* property state
after variance — so a lucky or unlucky roll can cross a threshold.

**The pass runs birth → supersede → cap:**

- **Birth** — property conditions met ⇒ the trait appears, **consuming the properties that made
  it**. Every trait carries a drawback.
- **Supersede** — authored merge rules let pairs collapse into something stronger (Emberveined +
  Frostbound → Tempestforged).
- **Cap 3** — the weakest is displaced, without refund.

Births charge integrity at **4.0 each**, which can itself destroy the material. That is
deliberate: the best traits live in high-variance states, and reaching for them is a genuine
gamble.

Traits join the signature as `id:tier`, so a trait genuinely changes *what the material is*.

---

# 6. Layer 4 — essence ✅

The **supernatural** tier, kept strictly distinct from mundane reactive properties.

| Essence | Anchors on |
|---|---|
| fire | heat |
| frost | cold |
| storm | charge |
| nature | growth |
| necrotic | decay |
| radiant | resonance |
| abyssal | corrosion |

**How it moves:** additively, at the process's `essence_rate`, with a +0.5 bonus when the
process's channel includes the essence's anchor property. Opposed essences annihilate on contact
and the overlap becomes **strain**.

**Capacity is governed by `resonance`** — `capacity = resonance × 1.5`. Excess is **not** clipped;
it becomes strain, which feeds `effectiveInstability` and makes everything downstream wilder.

> *Powerful magic needs a worthy vessel.* This is why **Attune** exists as its own process: you
> raise resonance first, then infuse. A player who skips that step does not get blocked — they
> get an unpredictable result, which is a far better lesson.

**`arcane` is a property, never an essence.** It is not an element; it is the medium elements
travel through. It gates magical effects, amplifies essence expression at fabrication, loads
against resonance, and types otherwise-untyped damage.

---

# 7. Layer 5 — fabrication ✅

**The terminal boundary. Materials stop here.**

```
Equipment FORM  +  one material per named SLOT  →  ItemInstance
```

`Sword` is authored once. Iron Sword, Emberveined Iron Sword and Necrotic Storm Sword are **never
authored at all**.

## 7.1 What a form declares

**Nine ship** (`game/data/forms/forms.json`), one per equipment slot plus a second weapon:
**Longsword** (edge / core / binding), **Warspear** (point / haft / grip), **Buckler** (face —
and it declares the `parry` tag that grants the parry command), **Helm** (crown / lining),
**Vest** (shell), **Gauntlets** (glove / plating), **Treads** (sole / upper), **Focus** (stone /
setting), **Ring** (band / inset). The table of what each one exists to test is in
`docs/game-overview.md` §9.

The Ring is the one form that does **not** map one-to-one onto a slot: it declares `Ring1` and
fills either ring position (D33). Do not author a second ring form for `Ring2`.

Each **slot** declares:

| Field | Meaning |
|---|---|
| `requires_tags` | any-of tag gate — `["form:metal","form:crystal"]` means either works. Validated: a gate no material satisfies is a form nobody can build |
| `mass_share` | how much of the item this component is. Validated: they must sum to 1 |
| `trait_expression` | per **trait category**, 0–1: how much of that kind of trait this slot may express (the design word is *aperture*) |

And the form declares a **`stat_map`**: each stat is a list of weighted reads —
`{ slot, property, weight }`, where slot `"*"` means the mass-share-weighted total across all
slots. The Longsword's hardness reads `edge × 0.8 + core × 0.2`.

## 7.2 Why this makes placement a real decision

**Stats read from named slots, never from a blend.** A hard brittle edge on a flexible core is a
genuinely different weapon from the reverse, and the system computes that with zero authored
combinations.

**The stat map is what stops a single "best material" existing** — and since the breadth pass it
is no longer only an argument, it is a shipped pair. The **Longsword** reads hardness off its
edge, which is 60% of it. The **Warspear** reads flexibility off its *haft*, which is 60% of it,
and hardness off a point that is a quarter. So the same iron ingot makes an excellent sword and a
heavy, stiff, worse spear, while a yew log does the reverse — same library, opposite verdict, no
authored combinations. `FormBreadthTests.TheSameMetalIsExcellentInOneFormAndWastedInAnother`
fails the day that stops being true.

The extreme case is the **Focus**, the only form whose stat map reads `resonance` at all. Delete
that one read and every resonant material in the game — ley crystal, runes, mana prisms — becomes
decoration with nowhere to be excellent. The **Ring** is the same argument run forwards: it reads
`conductivity` and `affinity`, which *no* form read before it existed, so until it shipped the
most conductive metals in the library were strictly worse swords and nothing else.

**The stat map also decides what the item can *roll*.** `ItemPotentialCalculator.MaterialInfluence`
weights each property by where the map reads it, and that influence is the sole input to modifier
eligibility, weight and tier. A form that reads flexibility hard is a flexibility item that rolls
flexibility modifiers — the Gauntlets reach Ghoststep and the Helm reaches Grounding for exactly
this reason, with no per-form affix content.

## 7.3 Apertures and dormancy

A trait's expressed magnitude is `magnitude × the slot's aperture for that trait's category`. The
top `trait_cap` by expressed magnitude are **expressed**; the rest go **dormant**.

**Dormant traits are shown, counted in value, and fully available if the material is used in a
different form later.** That is what makes one material interesting in several directions rather
than optimal in one.

## 7.4 The scale reconciliation

Material properties are 0–100. Equipment stats are ~0–5 combat units. **The conversion happens
here and only here:**

```
stat = Σ (property_value / 100 × contribution.weight) × FabricationTuning.CombatUnitScale   // 5.0
```

Calibrated so a plain iron-ingot longsword matches the authored Iron Sword, **pinned by a parity
test**. Everything downstream — `EquipmentResolver`, the hit pipeline — consumes fabricated gear
unchanged.

## 7.5 What comes out

A derived `EquipmentDefinition` registered under `equip.emergent.<hash>` (form + component ids +
stats), plus an `ItemInstance` carrying the stats, expressed traits, dormant traits, essence, the
genome and its rolled modifiers. Both are **persisted in the save** — without them, a fabricated
item in the stash would point at a definition that no longer exists after load.

## 7.6 Project vs Fabricate

Same pattern as the bench, same reason. `Compose()` is **one side-effect-free computation used by
both**, so the preview cannot drift from the mint. Fabrication is irreversible, which makes this
fairness guarantee more important here than anywhere else.

The preview additionally **promises the innates**, because innates are deterministic. Rolled
modifiers stay behind the commit, by design.

---

# 8. Layer 6 — the genome and its modifiers ✅

## 8.1 The Genome

Computed once at fabrication, stored on the instance, **never recomputed**.

```
Genome = pressure          stat-map-weighted property values
       + essence
       + expressed traits + dormant traits
       + tags
       + potency           mass-share-weighted mean of the components
       + generation depth  the deepest component's
       + signatures        📐 P4
```

**Pressure is the key idea.** Not "how much hardness is in this thing", but *how much hardness
actually reaches the parts of the item that matter*, weighted by the form's own stat map, with
mass share as the fallback for properties the stat map never reads. Same materials, different
form ⇒ different genome. Pressure below 0.5 is dropped as trace.

## 8.2 The three levers, plus roll quality

| Lever | Question | How |
|---|---|---|
| **Eligibility** | *Can* it roll at all? | Hard gate: form/tag match, property-pressure minimums, required essence, excluded families |
| **Weight** | How *likely*? | `base + Σ (pressure or essence)/10 × per10` |
| **Tier ceiling** | How *strong*? | The best (lowest-numbered) tier whose genome requirements are met |
| **Potency** | *Where in that tier?* | `0.35 + 0.65 × potency/100`, ± 0.10 variance |

All four are pure functions of the genome, and **the player sees them before rolling** — because
*"engineer the casino" is a lie if you gamble blind*.

## 8.3 What ships

**44 modifiers** in `game/data/affixes/affixes.json`:

| | Count |
|---|---:|
| innates (deterministic, never rerollable) | 5 |
| prefixes | 13 |
| suffixes | 26 |
| by class: standard / trigger / innate | 23 / 16 / 5 |

The five innates: **Keen Edge** (crit) · **Massive** (force) · **Resonant** (mana) · **Supple**
(stamina) · **Insulated** (charge resistance).

```
   1–3 INNATES        top-3 eligible by weight above the floor (25), potency-positioned,
                      ZERO variance, never rerollable — the guarantee that engineering a
                      good material can never produce a total loss
 + ≤3 rolled PREFIXES ┐ count drawn 1/2/3 at 0.35/0.40/0.25, then weighted picks,
 + ≤3 rolled SUFFIXES ┘ one modifier per family per item
 + Exotic / Signature / Anomalous    📐 not built (E7 / P4)
```

**Every fabricated item rolls its modifiers from the very first craft.** There is no "modifiers
unlock later" switch — pacing is emergent, because weak early genomes simply roll 0–1 minor ones.

## 8.4 How a roll becomes real

A modifier's `grants` reuse vocabulary that already existed — nothing bespoke:

| Grant type | Becomes | Attached at |
|---|---|---|
| `stat` | a scoped `ModifierContribution` with per-modifier provenance | equip/unequip |
| `rule` | a `TriggerRule` live while the item is worn | equip/unequip |
| `moveModifier` | one of the 11 move-rewrite operations | moveset composition |

**`$roll` is substituted in exactly one place** (`AffixGrants`), so the tooltip and the mechanics
can never drift. A validator rule enforces the parity — it caught seven wrong descriptions when
it was written.

> **Terminology, enforced.** In code these are `affix.*` in `Dungeons.Affixes`. In player-facing
> text they are **always "modifiers"** — the bare word *Prefix* means only the character layer.

⚠ **Content note:** all 44 modifiers currently sit in 44 *distinct* families, so §3.5's
one-per-family anti-stacking rule is live but has nothing to bite on yet. It starts mattering the
moment a family gains a second member — which is exactly what the catalog's growth to 150–250
will do.

---

# 9. What the player actually sees ✅

Crafting is where the **three languages** rule (D30) does most of its work, because crafting is
where the simulation is at its most naked.

```
SIMULATION                    PLAYER CRAFTING LANGUAGE            GAMEPLAY
0–100 properties,      →      glyph + qualitative tier      →     damage, Armour, Crit,
rates, severities,            + intensity + direction             Thorns, Shock, triggers
coefficients                  + context
```

- **Raw values never lead a normal surface.** They live behind the *Advanced* toggle, Assay, and
  the labs.
- **`Dungeons.Presentation` is the only path** from simulation state to player-facing text —
  one-way, deterministic, unit-tested. It may **translate, never recompute**.
- **Display tiers never touch identity quantization.** `QuantizationTuning` is unread by
  presentation, forever.
- **Glyphs and glosses are data** on `PropertyDefinition`, never code switches.

At the bench that means: qualitative tiers (Trace → Extreme) with pips, wear words, trend arrows
derived from the algebra's own typed change kinds, **risk bands** (SAFE → DESTROYS), trait
proximity hints (*"Within reach: Emberveined — needs more Heat"*), slot-fit readings, and item
cards.

> Rejected deliberately: **icons-as-numbers** — *"⚡⚡⚡⚡ is the same problem wearing a hat."*

---

# 10. A worked path, end to end

What a player actually does, and which layer answers:

```
 Mining          →  Iron Ore                                       layer 1
 Smelt           →  Iron Ingot            (form:ore gate, sev 0.60) layer 2
 Attune          →  resonance up          (Alchemy 10, arcane)      layer 4 prep
 Forge Infusion  ←  Ember Core            (heat 0 → 36, integ −3)   layer 2
                 →  ✦ Emberlit Iron       first discovery, stacks   layer 2
                 →  trait Emberveined born, integrity −4            layer 3
                 →  fire essence carried at rate 0.45               layer 4
 Fabricate       →  Longsword: edge = Emberlit Iron
                                core = Iron Ingot
                                binding = Leather                   layer 5
                 →  "Emberveined Iron Longsword"
                 →  stats in combat units; 1 dormant trait
                 →  Genome: high hardness+heat pressure, fire essence
                 →  innate Keen Edge (deterministic)
                    + rolled Emberbrand (Heavy Strike +25% as heat)  layer 6
 Equip           →  the modifier's move-modifier grant rewrites Heavy Strike
                 →  the hit pipeline resolves a heat packet in the heat lane
                 →  the heat lane's ailment chance can now apply Burn
```

Every arrow above is machinery that exists and is tested. Nothing in that path is authored as a
combination.

---

# 11. Where every number lives

Crafting has no magic numbers. Twelve `*Tuning` classes hold them all; these seven are crafting's:

| Class | Holds | Notable |
|---|---|---|
| `ReactionTuning` | acceptance/release curves, convergence cap, blend and dilution rates, annihilation, catalyst bonus | `MaxConvergence = 0.85` · `StructuralBlendRate = 0.10` · `ReactiveDilutionRate = 0.08` |
| `RefinementTuning` | integrity cost, potency ceiling, quality multipliers, trait cost | `StateDeltaCost = 12.0` · `TraitCost = 4.0` · `PotencyCeilingBonus = 8.0` · `DestructionRiskBandIntegrity = 25` |
| `QuantizationTuning` | identity bucket sizes | `PropertyBucket = 5.0` — **the highest-risk tuning number in the design** |
| `EssenceTuning` | capacity, anchor bonus, arcane amplification | `CapacityPerResonance = 1.5` · `AnchorChannelBonus = 0.5` |
| `FabricationTuning` | the 0–100 ↔ combat-unit scale, response→lane map, trait categories | `CombatUnitScale = 5.0` (parity-pinned) |
| `AffixTuning` | roll counts, variance, innate floor, potency curve | `CountWeights = .35/.40/.25` · `InnateWeightFloor = 25` — **all provisional** |
| `MaterialProfileTuning` | derived potency/integrity for authored materials | `PotencyFloor = 20` · `PotencySlope = 0.30` |

Plus `CraftQuality`'s own weights (baseline 0.10, level 0.40, performance 0.35).

---

# 12. What is NOT built 📐

| Layer | What it is | Where it sits |
|---|---|---|
| **P4 — Signature reactions** | 30–80 authored spikes matched against **abstract conditions**, never item ids; plus ~10–20 **chain** signatures matching an ordered sequence. The "authored spikes on top of a universal rule" layer | after the playtest |
| **P5c — Consumable forms** | The same fabrication system with an effect map: slots become base / active / stabiliser, output stacks like a material. **The natural home for negative outcomes** — a botched draught that heals *and* corrodes is the cheapest source of memorable results in the whole design | with P4 |
| **P6 — Codex & Assay** | The knowledge layer: discovery journal, known-rules journal, proximity hints, player renaming. **Assay gates legibility, never capability** — an unassayed modifier renders as an unreadable mark, the standing advertisement for the knowledge layer | after P4 |
| **E7 — Operations** | Anneal · Etch · Scour · Reforge · Bind · Temper · Fracture, paid for with **destruction byproducts of failed crafts**. Every operation respects the genome, so the gambling is bounded by the engineering | last |
| **E7 — Overreach** | The final casino: Ruin · Brick · Mutation · Elevation · Exotic Mutation · Transcendence, drawn **only from the item's own genetic families** — a poison dagger can never Overreach into lightning, at any odds. **Repeatable with escalating Ruin odds** — the fourth verse of the risk rhyme. **Anomalous modifiers exist only here** | last |
| **Exotic / Signature modifier classes** | Parse today, deliberately excluded from the v1 rolling pools | E7 / P4 |
| **Form acquisition** | Forms are ungated today. Designed: a starter set always known, most on profession ladders, a few as **schematics** — a knowledge loot class symmetric with techniques | M6 |
| **Profession tools** | Two worn slots, fabricated from the same forms system with the same genome, affixes and operations. Zero tool-specific content needed | E6 |

**Legacy, scheduled for deletion:** `CraftingExperimentSystem` + `CraftingInteractionDefinition` +
`DiscoverySystem` + `ExperimentOutcome` + `CraftingDerivation` — the old fixed-recipe path, alive
only because the Healing Salve has no emergent route yet. **Delete the whole path when P5c
lands** (D21).

---

# 13. Known debt and open questions

| # | Item | Status |
|---|---|---|
| 1 | **Quantization bucket size** (`PropertyBucket = 5.0`) | *The single highest-risk tuning number in the design.* Too coarse collapses the space; too fine floods the registry with indistinguishable neighbours. Measured at 67% collapse over 2,800 crafts — provisional, needs play |
| 2 | **Integrity budget strength** | Currently allows ~20–40 meaningful refinements — looser than the "commit-or-lose" fantasy implies, because the expensive cost terms (traits, signature reactions) barely exist yet. Accept and wait, or tighten now? |
| 3 | **Integrity excluded from identity** | An archetype keeps the integrity of its *first* discovery, so a cheaper path to the same state inherits the wrong budget. Judged self-balancing; filed, not fixed. Including it in the hash is the fix, at the cost of many near-duplicate stacks |
| 4 | **Response properties drop on transformation** | Iron's authored heat resistance of 60 becomes a derived ~14 after any craft. Arguably the more honest number, but it is a visible discontinuity |
| 5 | **`PropertyDefinition.transferable` is unconsumed** | Structural properties are marked non-transferable, yet processes move them on-channel. Give it a job or drop it |
| 6 | **All 44 modifiers are in distinct families** | The one-per-family rule is live but toothless until families gain second members |
| 7 | **Every affix number is provisional** | `AffixTuning` roll counts, variance and the innate floor are breadth-not-balance, deliberately parked for the balance pass |
| 8 | **Essence source overlap** | Two storm-trace faucets exist in Fishing (`eel_skin`, `shock_eel_gland`). Allowed as a rare outcome, but D29.3 says *"trace profession essence must never compete economically with Realm extraction"* — a playtest call, pinned by test |
| 9 | **Fabrication calibration** | `CombatUnitScale = 5.0` is parity-pinned to the authored Iron Sword, which is itself an unbalanced placeholder |

---

# 14. Where do I change crafting?

| I want to… | Do this |
|---|---|
| **Add a material** | One entry in `game/data/materials/<category>.json` |
| **Add a property** | One entry in `game/data/properties/properties.json` (name, role, glyph, gloss, opposite, `resisted_by`, `grants_tags`) |
| **Add a process** | One entry in `processes.json`: channel, medium, severity, role weights, gates, tag effects, essence rate |
| **Add a trait** | One entry in `traits.json`: property conditions, magnitude, category, drawback, optional merge rule |
| **Add an essence** | One entry in `essences.json`: anchor aspect, opposites |
| **Add a form** | One entry in `forms.json`: slots (`requires_tags`, `mass_share`, `aperture`), `stat_map`, `trait_cap`, moves, tags |
| **Add a modifier** | One entry in `affixes.json` — and **only when its mechanic already resolves in play** (D30) |
| **Change the algebra** | `core/Crafting/ReactionAlgebra.cs`. Worked examples are pinned in `tests/Crafting/ReactionAlgebraTests.cs` |
| **Change the pipeline order** | `ReactionEngine.RunReaction` |
| **Change fabrication** | `FabricationEngine.Compose` — one method, used by both preview and mint |
| **Change what rolls** | `AffixRoller` (eligibility/weight/tier/position) and `AffixTuning`. `GenomeCalculator.Pressure` if the *inputs* to those decisions should change |
| **Change a number** | Find the `*Tuning` class in §11. Never inline a constant |
| **Change player-facing wording** | `core/Presentation/SemanticFormat.cs`. New *facts* go on the relevant `XReading` first. **Never format in the UI** |

---

## See also

| For | Read |
|---|---|
| The mathematics, in full | `docs/emergent-item-system.md` |
| The Genome, modifiers, operations, Overreach | `docs/affixes.md` |
| The presentation rule and its enforcement | `docs/presentation-architecture.md` |
| The crafting arc as a played experience | `docs/how-it-plays.md` ch. 1 |
| Files, classes and call flow | `docs/code-map.md` §10.4–10.6 |
| The rest of the game | `docs/game-overview.md` |
| Why a decision was made, and what was rejected | `DECISIONS.md` — D7, D20, D21, D28, D29, D30 |

---

# 15. Design words vs code names

**The design vocabulary did not change. The code vocabulary did.**

Everything above — and everything in the GDD, the player-facing UI and the Reaction Log — uses
the *design* words. The C# uses plainer, more literal names, because a developer opening
`core/Crafting/` should not have to learn the fiction first. When the two differ, this table is
the bridge.

| Design / player word | C# name | Where |
|---|---|---|
| a material's crafting state | `MaterialState` | `core/Content/MaterialState.cs` |
| the reaction engine | `MaterialTransformationEngine` | `core/Crafting/` |
| the algebra | `MaterialTransformationRules` | `core/Crafting/` |
| Process | `CraftingActionDefinition` | `core/Content/` |
| Channel | `AffectedQualities` | on `CraftingActionDefinition` |
| Acceptance (from `affinity`) | `Compatibility` | `TransferCoefficients` |
| Release | `TransferStrength` | `TransferCoefficients` |
| Opposition | `QualityConflict` | `PropertyChangeKind` |
| Strain | `ReactionStress` / `Stress(...)` | `EssenceTuning`, step results |
| **Integrity** | **`Workability`** | `MaterialState`, `WorkabilityCalculator` |
| **Potency** | **`MaterialStrength`** | `MaterialState`, `MaterialStrengthCalculator` |
| Fabrication | `EquipmentAssemblyEngine` | `core/Crafting/` |
| Form | `EquipmentBlueprintDefinition` | `core/Crafting/` |
| Aperture | `TraitExpression` | on `BlueprintSlot` |
| **Genome** | **`ItemPotential`** | `core/Crafting/ItemPotential.cs` |
| Pressure | `MaterialInfluence` | on `ItemPotential` |
| eligibility | `Availability` / `IsAvailableFor` | `ModifierGenerator` |
| weight | `ChanceWeight` / `ChanceWeightFor` | `ModifierGenerator` |
| tier ceiling | `MaximumModifierTier` | `ModifierGenerator` |
| the affix roller | `ModifierGenerator` | `core/Affixes/` |

**Three things deliberately did *not* move:**

1. **Player-facing text still says Potency, Integrity and process.** The Reaction Log renders
   `Integrity 90 → 87` and `Potency 40, 70 → 49` exactly as before — changing displayed wording
   would be a UX change, not a readability one.
2. **Save keys still say `Potency`, `Integrity`, `Genome`, `Pressure`, `FormId`, `ProcessId`.**
   `core/Persistence/SaveData.cs` was not touched at all, so every existing save still loads.
   `SaveMapper` is where old key meets new name, and each such line carries a comment saying so.
3. **Content ids still say `process.*` and `form.*`**, and the `form:` tag family is untouched —
   they are referenced across JSON *and* by save data.

Four content **JSON keys** were renamed alongside their C# property, in the same commit:
`channel` → `affected_qualities`, `aperture` → `trait_expression`, `eligibility` →
`availability`, `weight` → `chance_weight` (affixes only), `per10` → `per_ten_influence`.
These live in shipped content files, never in a save.

## The reading path

```
MaterialState              what a material currently is
      ↓
MaterialTransformationEngine   turn it into a different material   (loop — output is a material)
      ↓
EquipmentAssemblyEngine        turn materials into an item         (door — terminal)
      ↓
ItemPotential                  what that item is capable of
      ↓
ModifierGenerator              which modifiers it actually gets
```
