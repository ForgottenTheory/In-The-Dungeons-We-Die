# Transformation Verbs — the crafting actions of the identity system

> **Status: APPROVED DESIGN — pre-implementation.** Approved 2026-08-20 (DECISIONS **D47–D48**).
> Companion to `docs/identity-foundation.md` (D42–D46) — read that first; this document assumes
> its vocabulary: identities, ranks, capacity, stability, condition, quality, profiles,
> carriers, base stats.
>
> §8 holds the profession assignment (Step 4, D48). Open details are collected in §7; every
> number is provisional until play.

---

# 1. Verbs are code; actions are data

The same bargain the effect kinds and behaviors strike (D16): the **verb set is a closed code
vocabulary** — one executor per verb — and everything the player actually clicks is a
**crafting-action content entry**:

```
crafting action = verb + parameters + gates (profession, level, station) + costs + fiction name
```

Smithing's "Smelt" and the mortar's "Grind" are both the **Process** verb with different data.
The forge's "Alloy" is the **Fuse** verb wearing its fiction. A new station's worth of actions
is authoring, never engine work — and the old system's fictions return as data:

| Old process | Becomes |
|---|---|
| Smelt, Grind | authored **Process** actions |
| Alloy | an authored **Fuse** action |
| Distill | authored **Extract** + **Refine** actions |
| Quench, Temper, Forge Infusion | authored **Develop** / **Refine** / **Transfer** actions in smithing fiction |
| Attune | dies with the essence layer — **Expand** takes its niche |

**Preview parity is mandatory for every verb** — preview and commit run the same computation,
as everywhere else in the design.

---

# 2. The ten verbs

| Verb | Does | Condition cost |
|---|---|---|
| **Process** | mundane conversion (ore→ingot) | none |
| **Fuse** | merge substances — the one verb that moves base stats | ambitious |
| **Reveal** | latent → active | 1 step |
| **Transfer** | move an identity from a source into the substrate | 1 step |
| **Develop** | raise an identity's rank by feeding it | 1 step (r3→r4 ambitious) |
| **Extract** | pull an identity out onto a carrier | 1 step on a surviving source |
| **Displace** | swap an identity in, deliberately ejecting one | 1 step |
| **Refine** | improve quality | gentle |
| **Restore** | climb the condition ladder | gentle |
| **Expand** | +1 stable capacity | ambitious |

**Process** — raw → workable: changes `state:`/`form:` tags, swaps the base block to the
processed form's, sets quality from the craft. Identities, latents, profile, and condition all
carry through untouched. The birth of the material's working life, so it costs no condition.

**Fuse** — two or more structural materials become a new substance. Base stats blend by
contribution weight; identities and latents union (on a shared identity, the higher rank is
kept); profiles merge (§11.4 of the foundation); quality from the craft. Capacity of the output
and its condition are derived — provisional rules in §7. If the union exceeds capacity, the
crafter chooses what to keep or accepts overfill. Ambitious.

**Reveal** — activates a latent identity at rank 1 (authored exceptions possible). Requires a
free slot. Works blind — revealing what Assay never told you is discovery gameplay, not an
error.

**Transfer** — moves an identity the substrate does **not** already have from a consumed source
into it (feeding an identity it *does* have is Develop's job — one verb per question). The
floor is deterministic: the identity arrives, every time. Delivered rank follows the fidelity
rule (§3). Beyond capacity → the overfill ladder, with the preview showing the odds.

**Develop** — raises an existing identity's rank by consuming same-identity sources. Progress
per source scales with the source's rank and quality; the cost curve grows per rank, and
r3→r4 is ambitious work. No sources, no development (§3).

**Extract** — pulls an identity out of a material onto a fresh **carrier** (a capacity-1
vessel — a content pattern: extracts, tinctures, concentrates), **preserving its rank**. This
is Herblore's oak→extract move, and also the caring way to free a slot on a crowded material
(the brief's *isolate*). The source is consumed by default; authored actions on precious
substrates may degrade condition instead (§7).

**Displace** — pushes a new identity in while deliberately ejecting a chosen existing one. No
overfill; the ejected identity is lost with no refund — Extract is the version that keeps it.
The incoming identity follows Transfer's fidelity rules.

**Refine** — quality up. Gentle: no condition step. Skill caps how high (§7).

**Restore** — condition up one step, at a material/time cost. Proposal (provisional):
**Pristine is virgin-only** — Restore climbs to Worked at best; a deeply-worked material never
fakes freshness.

**Expand** — raises stable capacity by one. Rare and expensive: Pure's family turf plus
authored catalysts. Ambitious, and the legitimate exit from overfill besides removing an
identity.

---

# 3. The rank economy

The answer to the foundation's last open math item, and the rule that makes the profession
ecosystem load-bearing:

1. **Transfer from a raw source delivers rank 1.** Raw oak at the forge moves Vital — in its
   shallowest form.
2. **Transfer from a prepared carrier delivers the carrier's rank** (up to a skill ceiling,
   provisional). Extract preserves rank onto the carrier; Develop and Refine deepen it there.
   *Distilled Oak Tincture* (Vital r2) exists because Herblore extracted and Alchemy developed —
   and the smith using it gets r2 where raw oak gave r1. **Preparation = fidelity.** The
   Forestry → Herblore → Alchemy → Smithing chain is economically real with zero mandatory
   recipes: working raw always works, working prepared works better.
3. **Develop feeds.** Ranks rise only by consuming same-identity sources — which keeps
   professions interlocked instead of self-sufficient, and gives every identity-bearing
   material in the world a sink.
4. **Ranks never decay passively.** Loss is always an event the crafter chose to risk:
   fracture on the overfill ladder, or destruction at Fragile — which still pays byproducts.

---

# 4. Where variance lives — the complete map

Verbs are **deterministic at the floor**. Randomness exists in exactly three places, all
previewed, all opted into:

| Where | What varies |
|---|---|
| Signature generation (item time) | which sentences, within the scored space |
| The overfill ladder | wilder candidate distributions, fracture rolls, raised anomaly odds |
| Fragile + ambitious work | destruction rolls (byproducts on loss) |

**Quality** is the control stat, not a power stat: it tightens signature scoring, scales base
delivery at fabrication, and improves Develop efficiency (provisional). Active crafting earns
quality through the existing timing-performance mechanic; verbs award profession XP and
mastery — closing the shipped system's known gap where bench work trained nothing (wired in
migration Phase 5).

---

# 5. Deliberately not verbs

- **Profile or theme manipulation** — personality is shaped only by *choosing sources*; themes
  stay hidden scoring metadata (foundation §6.1).
- **Stabilize-in-place** — the only exits from overfill are Extract, Displace, or Expand.
  "Stable at 3/2" cannot exist; capacity is a count, not a mood.
- **Un-fabricating** — the door stays terminal (foundation §8).
- **Assay and gathering** — analysis and acquisition are profession actions, not bench verbs.

---

# 6. The worked chain, with real math

```
Oak (latent Vital, renewal/endurance profile)
  → Reveal   (Herblore)              Oak — Vital r1 active            condition −1
  → Extract  (Herblore)              Oak Extract carrier — Vital r1   source consumed
  → Develop  (Alchemy, feeds herbs)  carrier — Vital r2               condition −1 (carrier's)
  → Refine   (Alchemy)               Distilled Oak Tincture — r2, high quality
Iron Ore
  → Process  (Smelt, Smithing)       Iron Ingot — clean slate, Pristine
  → Transfer (Dense source)          Dense Iron — Dense r1, 1/2 slots  condition → Worked
  → Transfer (the tincture)          Dense Oakbound Iron — Vital r2!   condition → Strained
  → fabricate now, or Restore and push — the extraction rhyme, at the bench
```

Every step is one verb; every rank is accounted for; the r2 exists **because** two other
professions touched the chain.

---

# 7. Open details (provisional)

| # | Detail | Note |
|---|---|---|
| 1 | Fuse's derived capacity and output condition | proposal: highest input capacity; condition one step below best input |
| 2 | Restore's ceiling | proposal: Pristine is virgin-only |
| 3 | Fracture target selection | proposal: the newest identity |
| 4 | Develop cost curves; carrier-fidelity skill ceilings | balance pass |
| 5 | Extract on precious substrates | consumed by default; degrade-instead as an authored action parameter |
| 6 | Expand catalysts and any cap on expansions | content + balance |
| 7 | Latent reveal rank | r1 default; authored exceptions allowed |

---

# 8. Profession assignment (approved 2026-08-20, D48)

## 8.1 The two structural rules

1. **Professions own domains, not verbs.** Working professions share the same core verb kit,
   scoped to their material domain by substrate tags (the gating machinery that already
   exists). No profession is "the Transfer profession" — a smith never visits the tailor to
   infuse a sword.
2. **Solo-complete, chain-enhanced.** Every verb a profession offers works with raw inputs
   alone; other professions' products only raise fidelity, efficiency and odds (§3). No craft
   requires a second profession to be *possible*. Deliberate exceptions: **Expand** (rare by
   design) and **Cooking's** named dead-end until consumable forms.

## 8.2 The matrix

| Profession | Role in the identity economy | Verbs (scope) |
|---|---|---|
| **Smithing** | the metal domain | Process, Transfer, Develop, Refine, Restore, Fuse (metal) |
| **Leatherworking** | hide/leather domain | Process, Transfer, Develop, Refine, Restore (leather) |
| **Tailoring** | cloth/fiber domain | Process, Transfer, Develop, Refine, Restore (cloth) |
| **Fletching** | wood-working domain (bow forms still pending — existing note) | Process, Transfer, Develop, Refine, Restore (wood) |
| **Artifice** | glass/crystal/gem domain; composites | Process, Transfer, Develop, Refine, Restore, Fuse (crystal/glass) |
| **Runecrafting** | **the identity-domain specialist**: magical identities (Arcane, Resonant, Warded, Radiant, Umbral) on *any* substrate — the one profession scoped by identity rather than material | Transfer, Develop (magical identities); Expand (late, with Alchemy) |
| **Herblore** | flora specialist; the carrier-maker | Process, Reveal, Extract (flora) |
| **Alchemy** | **depth**: universal extraction, carrier development, capacity science | Extract (universal, late), Develop + Refine (carriers), Fuse (solutions), Expand (late) |
| **Beast Lore** | fauna specialist (already the carcass-opener) | Reveal, Extract (fauna) |
| **Mining** | gather minerals; **prospecting** — active Reveal at the source | Reveal (mineral) |
| **Forestry** | gather timber; reading the grain | Reveal (timber) |
| **Fishing / Hunting / Salvaging / Thieving** | suppliers: aquatic · carcasses · reclaimed scrap (**Salvaging's output is the Restore feedstock** — it feeds the repair economy) · exotics and rare catalysts | — |
| **Farming** | renewable identity sources: seeds carrying latents; cultivation quality *(niche flagged, provisional — growing is supply-side, not the Develop verb)* | — |
| **Cooking** | named dead-end until consumable forms (P5c), unchanged | (Process, then) |
| **Assay** | detection: latents, profile hints, precise capacity/condition readouts — information only, **gating legibility never capability** (identity preserved) | — |
| **Agility / Cartography** | not crafting (course bonuses / realm knowledge, unchanged) | — |

**Displace** follows a rule rather than a row: every Transfer-capable profession gains it late,
in its own scope — the surgical swap is a mastery move, not a separate profession's toll.

## 8.3 Gate philosophy and continuity

Verbs unlock in waves per profession: Process/Refine early · Reveal/Transfer mid ·
Develop/Extract/Restore mid-late · Fuse/Displace late · Expand last. All numbers belong to the
balance pass. Verb actions are authored at each profession's own station (the station model and
its validators carry forward unchanged), and verbs award profession XP and mastery (§4).

The ecosystem tests stay true with a *reason* behind them: every processing profession still
eats another profession's output — no longer because a list says so, but because prepared
inputs are mechanically better (§3's fidelity rule).
