# Identity Foundation — materials, signatures, and the crafting redesign

> **Status: APPROVED DESIGN — pre-implementation.** Approved 2026-08-20 (DECISIONS **D42–D49**).
> This is the design of record for the material / crafting / itemization overhaul. It **replaces**
> the hidden-property simulation (21 numeric properties, the reaction algebra, the trait layer,
> the essence layer, the genome) described in `docs/crafting-overview.md`,
> `docs/emergent-item-system.md`, `docs/itemization.md` and `docs/affixes.md`. Those documents
> remain accurate about the **code as shipped** until the migration lands; they carry superseded
> banners and no new design may be built against them.
>
> **"Identity" in this document always means *material* identity** — not character identity
> (D22) and not enemy identity (D26).
>
> Open questions are marked **[OPEN]** and collected in §14. Nothing marked [OPEN] is settled by
> appearing here.

---

# 0. Five words, kept apart

The vocabulary discipline for the whole redesign. These are five different things; conflating
them is the most expensive mistake available.

| Word | Means | Status |
|---|---|---|
| **Identity** | A named mechanical door on a material — Dense, Vital, Ember. Deterministic, player-legible, and the thing crafting moves around. An identity opens an **effect family**; it is never itself one effect | Approved (roster in §3) |
| **Signature Profile** | Authored tendency data on a material — themes, favored triggers/behaviors/payloads, biases, exclusions. Weights, never recipes. The reason Oak-derived gear feels different from Willow-derived gear even when both carry Vital | Approved (§6) |
| **Signature** (generated) | The **special layer** on a crafted thing: 1–N coherent, material/process-derived effect sentences, produced by the Signature stage of the item-effect pipeline. One of three effect categories an item can carry (§8, D50) — never a blanket name for everything generated | Approved (§7–§8, D50) |
| **Effect-family progression** | How deep into a family a material/item can reach: basic → improved → advanced → build-changing. An *access* ladder — advancing an identity expands what it can DO | Approved as a concept (§4–§5) |
| **Named identity evolution** | A possible future player-facing ladder of identity *names* (e.g. Vital → Regenerative → Lifebound) | **[OPEN — deliberately unresolved.]** Examples are illustrations, never commitments. Internal ranks may exist meanwhile; player-facing roman numerals (Vital I/II/III) are rejected |

---

# 1. Why this redesign exists

The shipped system made materials 21 hidden 0–100 numbers and crafted with an algebra
(converge → drift → oppose → prune). It produced genuine emergence and nobody — player or
developer — could reason about it without reading equations. The replacement philosophy:

> **Simple, understandable rules interacting to create deep emergent results.** A player should
> be able to say "I did this to this material, which is why the item behaves this way."

Crafting rewards knowledge, experimentation, process order, material selection, preparation and
risk management — never the decoding of hidden equations. **Emergence stays. Recipes stay dead.**
There is no recipe table for `Dense Oakbound Iron Sword`; the system understands how it emerged.

**What deliberately survives from the old system** — lessons already paid for:

- **Preview parity** — preview and commit run the same computation (the old Project/Resolve
  pattern). The pre-commit projection is incapable of lying.
- **Identical results stack** (D20). Materials never become per-unit instances; only equipment
  gets an `ItemInstance`.
- **Names derive from final state, never from history** — history-based names grow without bound.
- **Destruction pays byproducts** — a blown craft is a setback with a consolation prize, never zero.
- **Skill narrows variance** — mastery means control, not bigger numbers.
- **Capacity-with-overflow precedent** — the essence capacity/strain idea, generalised into §10.
- **No recipes, ever** — D7's goal, kept; only its mechanism is replaced.

---

# 2. The two layers

**Identities are the floor. Signature Profiles are the personality on top of it.**

| Layer | Question it answers | Determinism |
|---|---|---|
| **Identities** | What is this material, and which effect families are *open* to it? | Guaranteed. Transfer Vital into Iron and the result carries Vital, every time |
| **Signature Profile** | Within (and occasionally beyond) that open space, which expressions does this material lean toward? | Weighted. This is the "RNG above the floor," and the player engineers it |

The load-bearing consequence (approved rule): **exact source materials and process must matter,
not just identity totals.** Dense + Vital as abstract inputs is not enough information to
generate from. Oak-derived Vital, Willow-derived Vital and Bloodroot-derived Vital should be
*capable* of producing different signature tendencies, because each material's profile — not
just its identities — enters generation. Two materials sharing every identity still keep their
own personalities.

A material with no profile is **neutral**: it works purely on its identities. Profiles are
flavor a material *has*, never homework every material owes.

---

# 3. The identity roster — 24 identities in 7 clusters

> ⚠ **How to read the tables.** The Floor / High-end columns are **possibility-space sketches**
> of each effect family at its access rungs (basic → improved | advanced → build-changing).
> They are **not** finalized effects, **not** balanced numbers, and **not** player-facing
> identity-evolution trees (that concept is [OPEN], §0/§5). They exist so every identity's
> mechanical footprint is checked against machinery that actually resolves (D30).

## Physical core

| Identity | It is… | Floor (basic → improved) | High end (advanced → build-changing) |
|---|---|---|---|
| **Dense** | Weight as force: impact, block, stagger | Impact damage, block strength → stagger power | Bonus vs staggered/stunned, uninterruptible windups → blocks that stagger the attacker; heavy hits ripple to nearby enemies |
| **Hardened** | Surface toughness: armor and mitigation | Armor, physical resist → durability, resistance to armor-break (Corroded) | Armor while blocking → damage scaling from armor; first hit each fight glances at full armor |
| **Earthen** | Immovability: refusing to be moved or controlled | Resolve / control resistance → stagger resistance, flat damage reduction (stoneskin) | Shorter controls, immunity windows → control buildup converts to armor; stances trading speed for steadiness |

*The three-way boundary: Dense hits, Earthen refuses to move, Hardened shrugs it off.*

## Precision & tempo

| Identity | It is… | Floor | High end |
|---|---|---|---|
| **Keen** | Precision offense | Crit chance → crit damage, armor penetration | Bonus vs Vulnerable, crits vs ailing enemies → the on-crit trigger economy; earned guaranteed crits (after a parry) |
| **Balanced** | Consistency and control | Tighter damage rolls, reduced move penalties → wider parry windows | Perfect-block/parry rewards, timing forgiveness → the parry economy (refunds, riposte sentences); flow bonuses for alternating moves |
| **Swift** | Action economy: act sooner, spend less | Windup/recovery reduction → stamina cost and cooldown reduction | Burst of speed after avoiding → acceleration streaks; speed-to-damage conversion |

## Sustain & exchange

| Identity | It is… | Floor | High end |
|---|---|---|---|
| **Vital** | The health pool itself | Max health, regeneration → healing received, heal on kill | Regen while wounded, low-health defenses → overheal becomes Barrier; blocking heals; health-as-cost pacts |
| **Leeching** | Taking from the enemy | Lifesteal → mana/stamina steal | Leech scales with the target's ailments, drain on block → kills grant stacking regeneration; excess leech charges gauges |
| **Thorned** | Answering harm | Thorns → block-reflection, thorns scaling with armor | Retaliation carries statuses → stored retaliation (bank damage taken, release on next hit — absorbs the parked E7 concept); the when-struck trigger economy |

## The elemental six — one per damage lane

| Identity | Lane / status | Floor | High end |
|---|---|---|---|
| **Ember** | heat / Burn | Heat damage or conversion → Burn | Bonus vs Burning, Burn detonation → spread-on-kill, burst economies |
| **Frost** | cold / Chill→Freeze | Cold damage → Chill | Freeze buildup, Shatter interplay → freeze-lock and shatter-burst builds |
| **Storm** | charge / Shock | Charge damage → Shock | Chaining/bouncing (machinery ships today, content empty) → charge batteries: crits build charges, max releases Chain Lightning |
| **Venomous** | toxin / Poison | Toxin damage → Poison stacking | Stack depth, duration, amplification → poison spreads on kill; stack-count payoffs |
| **Corrosive** | corrosion / Corroded | Corrosion damage → armor shred | Resistance shred, shred depth → shred converts to Vulnerable; anti-armor execution plays |
| **Serrated** | physical / Bleed | Bleed chance → Bleed magnitude | Bonus vs Bleeding, Bleed's ticks-harder-while-acting hook → hemorrhage bursts; wound cross-plays with Leeching/Vital |

## Magical

| Identity | It is… | Floor | High end |
|---|---|---|---|
| **Warded** | Magical defense | Lane resists → status duration reduction, Barrier access | Barrier efficiency, on-break triggers → the Barrier economy: recharging shields, damage-while-shielded conversions |
| **Arcane** | The magic economy | Max mana, spell damage → mana efficiency | Cast-triggered sentences, spell echoes → spend-mana-to-empower economies; spellblade imbues |
| **Resonant** | Stored power | A small charge gauge with a band bonus → faster charging, more feeds | Max-charge releases → the cross-identity battery: other identities' triggers feed it, its release fires their payloads. Natural home of the `store` behavior |

## Living & occult

| Identity | It is… | Floor | High end |
|---|---|---|---|
| **Verdant** | Ramping renewal — stronger the longer it runs | Regeneration → effects that strengthen while active | Blooming (delayed, larger payoffs) → cultivation economies: compounding growth that resets when spent |
| **Radiant** | Light: cleansing and protection | Healing output → cleansing, Guarded | Anti-decay/undead (via enemy tags), Illuminated → cleanse-to-power conversions; protection economies |
| **Umbral** | Shadow: evasion and openings | Evade → Fear/Silence access | Bonus vs controlled enemies, post-avoidance windows → the opening-exploitation engine (pairs with Keen) |
| **Blighted** | Decay: curses and anti-healing | Decay damage — the first content the empty decay lane has ever had → healing reduction | Wither ships here, spreading curses → epidemic economies: afflicted enemies detonate or spread on death |

## Fortune & meta

| Identity | It is… | Floor | High end |
|---|---|---|---|
| **Charmed** | Fortune: yield, rarity, opportunity | Gold find, bonus gathering outputs → rarity weighting | Opportunity odds and risk twisting (both benefit kinds exist) → luck economies: second-chance procs, rerolls. Deliberately light in combat; the identity that makes crafted tools (E6) matter |
| **Pure** *(meta-identity)* | Crafting fidelity — it modifies crafting itself, not combat | Cleaner transfer, compatibility → refinement ease | Capacity headroom, preservation on failure → the capacity-gambit enabler (§10) |

## Boundary rulings baked into the roster

- **Accuracy is deleted** from Keen and Balanced — no attacker-accuracy stat exists (misses are
  defender-side). Adding one would be a combat-mechanic decision on its own merits first (D30).
- **Movement speed and knockback are trimmed everywhere** — no positioning system exists.
- **Stamina economy → Swift, mana economy → Arcane** (the `Supple` innate's successor home).
- **One primary home per mechanic; crossovers on purpose.** Barriers live in Warded; Vital's
  overheal-to-Barrier is a deliberate build-changing crossover.
- **Vs-controlled → Umbral; vs-afflicted → Blighted.**
- **Proc safety honored:** ailment ticks can never trigger anything (existing rule), so no
  family contains an "on DoT tick" sentence; effects scale *with ailment presence* instead.
- **Casting speed** (Swift floor) is the parked GDD §18 #16 mechanic — flagged, not resolved.
- **Kinetic (identity) is cut** — impact folded into Dense, repeated-action into Resonant/Swift.
  Momentum stays on file as a future identity if its mechanics ever earn it.

---

# 4. Effect families

**Identity → effect family, never identity → effect.** Vital does not mean "+2 regeneration";
Vital means the health/healing/sustain family is open, and form, history, development, quality,
other identities and the Signature layer decide which member appears.

Families are organized in four **access rungs** — basic, improved, advanced, build-changing.
Deeper rungs unlock more interesting *kinds* of effects, not bigger numbers. Build-changing
effects (blocking restores health; crits build Storm Charges; overhealing becomes Barrier) are
the reason the rung ladder exists.

Rung membership per family is authored **content** (the payload registry, §7), not code.

---

# 5. Progression — two concepts, kept separate

1. **Effect-family progression (approved):** a material or item's development in an identity
   controls how deep into that family generation may reach. Development is the per-identity
   **rank** on the material state (§11); how ranks accrue is settled in
   `docs/transformation-verbs.md` §3 (D47).
2. **Named identity evolution ([OPEN]):** identities may eventually *evolve by name*
   (Vital → Regenerative → …) instead of showing ranks. Nothing about it is finalized — not the
   names, not the shape, not whether it ships. Internal ranks are acceptable machinery in the
   meantime. Player-facing tier numerals are rejected permanently.

**The Floor/High-end tables in §3 are neither of these.** They sketch each family's possibility
space so the roster could be sanity-checked against real machinery. Do not read them as trees.

---

# 6. Signature Profiles

An **optional authored block on a material** describing its personality as generation weights:

```jsonc
{
  "id": "material.oak",
  "signature_profile": {
    "themes": ["renewal", "endurance", "growth"],       // hidden scoring metadata — §6.1
    "favored_triggers": ["on_block", "on_heal"],
    "favored_behaviors": ["amplify", "store"],
    "favored_payloads": ["regeneration", "barrier"],
    "interaction_biases": [ /* weight adjustments against other themes/identities */ ],
    "hidden_tendencies": [ /* invisible authored hooks; feed the future anomaly system */ ],
    "exclusions": [ /* combinations this material resists or refuses */ ]
  }
}
```

**Rules:**

- **Weights and tendencies, never recipes.** Oak + Iron must not resolve to one predetermined
  signature. Profiles bias the scored space; they do not enumerate outcomes.
- **Profiles are how materials keep personality when identities match** (§2). The resolver
  reads the *actual* source list and process history, never just identity totals.
- **Absent profile = neutral material.** Fully functional on identities alone.
- **Profiles travel.** Crafted/emergent materials inherit a merged profile from their sources
  (merge rules in §11). The merged profile is part of what the crafted material *is*, and
  enters its fingerprint through the root composition (§11).

## 6.1 Themes are hidden scoring metadata — nothing more

**Approved rule:** themes exist only inside generation scoring (cross-material resonance and
bias arithmetic). They are **never player-facing**, never a second identity system, and never a
new simulation layer. The theme list is small, closed-ish data; a theme does nothing except
adjust scores. Distinct from `hidden_tendencies`, which are per-material authored hooks
reserved for the future Emergent Phenomena system — also invisible, but pointed at §8's seam.

---

# 7. The Signature grammar

A signature effect is a **sentence**: *trigger* (+ optional conditions) → *behavior* →
*payload*, with magnitude basis, chance, cooldown and target as knobs. Sentences compile to the
existing runtime vocabulary (`TriggerRule`s, effect kinds, modifier keys, gauges, move ops) —
the grammar is a **generator over machinery that already resolves**, not a new execution engine.

## 7.1 A Signature is 1–N sentences

**Approved rule:** a generated Signature may hold one sentence or several.

- **Simple signature:** one sentence — `on_block → store → barrier`.
- **Advanced/rare signature:** multiple related sentences that read as one idea. The canonical
  example: a cursed axe that both poisons its wielder (`while_worn → afflict(self) →
  status.poison`) **and** improves Fishing (`while_worn → sustain → profession.fishing bonus`).

Sentence-count drivers are partly settled: overfilled (Unstable/Volatile) materials widen the
distribution and raise rare-outcome odds (§10); the remaining drivers (quality, generation
context) are [OPEN — §14]. Multi-sentence signatures should cohere — shared source, trigger, or
theme — scored toward coherence rather than enforced by rule.

## 7.2 Triggers — bound to shipped events

| Group | Draft triggers | Binds to (`GameEvents`) |
|---|---|---|
| Offense | `on_hit` · `on_crit` · `on_kill` | `DamageDealt` · `CriticalLanded` · `Killed` |
| Defense | `on_being_struck` · `on_block` (both outcomes, D-06; perfect-block via event values) · `on_parry` · `on_dodge` · `on_barrier_break` · `on_control_resisted` | `DamageTaken` · `Blocked` · `Parried` · `Dodged` · `BarrierBroken` · `ControlResisted` |
| Sustain | `on_heal` (given/received via source conditions) | `Healed` |
| Statuses | `on_status_applied` · `on_status_expired` (scoped by id/category) | `StatusApplied` · `StatusExpired` |
| Action economy | `on_move` (scoped by move tags) · `on_resource_spent` · `on_interrupted` | `MoveExecuted` · `ResourceSpent` · `ActionInterrupted` |
| Encounter | `encounter_start` / `encounter_end` | `EncounterStarted` / `EncounterEnded` |
| World & profession | `on_craft_completed` · `on_chest_opened` · `on_item_received` · `on_extraction` | `CraftCompleted` · `ChestOpened` · `ItemReceived` · `ExtractionCompleted` |
| Standing | `while_worn` — not an event; compiles to standing modifier grants | equip/unequip pipeline |

Sentence qualifiers reuse the existing 17 condition kinds (`targetHasStatus`,
`selfHealthBelow`, `gaugeAtLeast`, `hitHasLane`, `equippedTag`, …) unchanged.

## 7.3 Behaviors — the semi-closed verb set

Each behavior is one small registered **assembler** that composes trigger + payload into
concrete grants — the same bargain effect kinds strike with `IEffectHandler` (D16). New
*combinations* are data; a genuinely new *verb* is one assembler, never an engine rewrite.

| Verb | Composes | Status |
|---|---|---|
| `direct` | the payload, plainly | live |
| `sustain` | continuous while-worn/while-active modifier | live |
| `amplify` | scale a quantity via `grantModifier` (scoped) | live |
| `afflict` | `applyStatus`, target selectable — `targetIsSelf` is how gear curses its wielder | live |
| `retaliate` | payload aimed back at the attacker | live |
| `drain` | `drainResource` + `grantResource` / lifesteal riders | live |
| `convert` | lane movement via existing move ops (D-01: fraction-based, always) | live |
| `echo` | `triggerMove` | live |
| `imbue` | `modifyMove` (the 11 rewrite ops) | live |
| `exchange` | pay a cost (health-as-cost ships in pacts) → payload | live |
| `store` | feed a gauge + release rule (`gaugeAtLeast` → payload + gauge drain) | live (composed) |
| `detonate` | consume a status/stacks → payload | **gap:** needs `consumeStatus` effect kind |
| `spread` | copy/jump statuses to nearby enemies | **gap:** needs area status application |
| `bloom` | schedule a delayed, larger payload | **gap:** needs a delayed-effect wrapper |

A fourth small gap: a `cleanse` effect kind (cleanse groups exist on statuses; no rule effect
invokes them yet) — Radiant wants it. Each gap is one effect kind + one handler through the
sanctioned extension path.

**Two compile shapes the D30 fence forced at Phase 3** (`SentenceAssemblers`): `drainResource`
is authorable but has no registered handler, so **drain** compiles as damage-plus-restore (a
faithful "take from the enemy" over machinery that resolves) and **store** compiles as
feed-plus-band — the trigger feeds a gauge whose band grants the payload's modifier while
high — rather than release-on-full. The release shape joins when a gauge-spend effect kind
exists; until then an assembler that compiled to a silent no-op would be the exact lie this
architecture exists to prevent.

## 7.4 Payloads — binding categories

A payload registry entry = binding + target + magnitude basis + **which identity families own
it** + rung + default weights. Binding kinds, all live: damage packets (typed/laned/aspected) ·
status ids · modifier keys (including the declared-but-unread `profession.*` keys) ·
resource/gauge grants · move grants and triggered moves · move rewrites · `grantItem` ·
`revealInfo`. One genuinely new seam: loot-luck payloads need `LootResolver` to read a
player-side modifier (small; Charmed's consumer).

---

# 8. Generation — the item-effect pipeline

**One unified pipeline generates everything a finished item does (approved 2026-08-20, D50)** —
working name `ItemEffectResolver`, succeeding the old genome → `ModifierGenerator` path. Its
output is **three distinct categories, kept apart on the item**:

| Category | Determinism | What it is |
|---|---|---|
| **Identity floor expressions** | Guaranteed | The deterministic grants each active identity promises at its rank — §2's floor, inheriting the innate layer's determinism |
| **Ordinary generated effects** | Weighted | The workaday layer: sentences drawn from the shared trigger/behavior/payload vocabulary under family/rung gates and profile/form bias |
| **Signatures** | Weighted, optional | The special material/process-derived layer (§7): 1–N coherent sentences, present only when sources, process, profile or overfill earn one. Not a rename of affixes — D50 |

The 44 old affixes re-ground as content in the shared vocabulary where appropriate — one
mechanical language, one balancing pipeline. `SignatureResolver` names the Signature-specific
stage inside the pipeline; `ModifierGenerator` and the innate/prefix/suffix shape retire in
Phase 7. How the two weighted categories divide mechanically at generation time is Phase 3
implementation design.

**Inputs (all of them, by rule):** the exact source materials, current identities, crafting
process/history, equipment form, quality, catalysts, and every contributing Signature Profile.

Ordered stages (the weighted categories; floor expressions are deterministic reads beside them):

1. **Collect influence** — each contributing material's profile enters, weighted by its share
   and role; the crafting actions used, the form, and catalysts contribute their own biases (a
   shield form leans `on_block` the way its stat map leans defense today).
2. **Generate candidates** — enumerate trigger × behavior × payload sentences from the space
   the identities open, plus any authored extensions (§9), minus exclusions.
3. **Score** — sum weights; shared themes across sources resonate; quality tightens and raises
   outcomes.
4. **⟨Emergent Phenomena seam⟩** — a reserved, currently-empty stage between scoring and
   selection where rare anomaly rules may later inject or bend candidates. It gets a name and a
   stable interface now, and nothing else. **Explicitly not designed and not to be built yet.**
   The one designed input it already has: overfilled materials raise the odds this stage will
   eventually act (§10).
5. **Select** — seeded draws above the guaranteed identity floor; sentence count per §7.1.
6. **Emit** — behavior assemblers compile winners into ordinary grants
   (`stat` / `rule` / `moveModifier` / gauge configs) flowing down the existing equip pipeline.

**Where it runs:** profiles and identity state *accumulate* on materials through crafting;
signatures *crystallize* at item generation — the same accumulate-then-crystallize split as the
old genome → modifiers, which is architecturally proven.

**Determinism:** the identity floor is guaranteed; randomness lives above it, is seeded, and is
shown before commitment — the scored candidate table appears in the preview (Project/Resolve
parity). *"I am engineering the odds," never "I pulled a lever."*

## 8.1 Item-side identity expression (approved 2026-08-20, D51)

Which identities a finished item carries — settled before any generation runs:

- **Union:** the item inherits every active identity its components bring.
- **Form cap:** each form authors a small active-identity cap; up to that many express.
- **Dormancy:** identities beyond the cap are recorded **Dormant** on the item — no floor
  expressions, no generation weight, never deleted. They wait for future deliberate mechanics
  (reforging, awakening, unusual Signatures, Emergent Phenomena).
- **Readable selection:** which identities express is decided by simple authored slot
  roles/priorities and clear rules — never an opaque per-slot percentage scheme (the old
  `trait_expression` apertures do not return). Placement matters through base reads (§11.5)
  and slot-role bias in scoring (stage 1 above), never through floor eligibility — reality
  test 5's identical-floor rule holds.
- **Two caps, two concepts:** material capacity (§10.1) is the crafting-side risk axis; the
  form's identity cap is the item-side expression axis. Neither implies the other.

---

# 9. Compatibility — sensible defaults, not creative law

**Approved rule:** identity-family compatibility is the **default weighting regime**, not an
unbreakable gate.

- **The floor stays hard:** what identities deterministically grant is guaranteed (§2, §8).
- **Procedural generation strongly favors sensible combinations** — candidates inside the
  identities' open families dominate the scored space by weight.
- **Authored data may explicitly breach the defaults.** A Signature Profile or authored
  signature/weighting rule can extend eligibility beyond the open families — deliberately, in
  data, with no special-case engine code. The cursed axe (self-poison + Fishing) is the
  canonical proof; nothing in the engine knows payloads are "supposed" to be combat-flavored.
- **The one fence that never bends is D30:** a trigger/behavior/payload may only be
  *registered* by binding to machinery that resolves in play — validator-enforced. Weirdness is
  unrestricted; lying to the player is impossible.

---

# 10. Capacity, stability, and condition (approved 2026-08-20, D45)

Three small, visible facts replace the old hidden budgets. The design rule for all three:
**within the limits, crafting is predictable and controllable; beyond them, risk is chosen, not
ambient.**

## 10.1 Capacity — stable slots

A material has a small authored **stable capacity** for distinct identities — usually **1–4**.
Most raw materials hold 1–2, good materials 3, exceptional stock 4. **Breadth and depth are
separate axes by rule:** identity **rank does not consume capacity**. The crafting question is
always "*which* identities does this carry, and how far have I taken each one" — never a points
budget.

Capacity **expansion** exists but is rare and expensive: Pure's family turf, plus occasional
authored catalysts. Deliberate **displacement** (overwrite an identity) and **isolation**
(extract one out, potentially onto a carrier) are planned crafting verbs, assigned to
professions in the Step 4 pass.

## 10.2 Latent identities

A material may carry identities that are **present but inactive** — Oak might hold latent
Vital. Latents do not occupy capacity; **revealing** one (a crafting/analysis verb) promotes it
to active and requires a free slot, or it stays latent. Assay's new job is exactly this:
detecting and reading potential rather than unredacting numbers. Latents give gathering and
analysis professions discovery gameplay with no extra machinery.

## 10.3 Stability — the overfill ladder

Exceeding stable capacity is **advanced crafting, not an error.** The transfer succeeds and the
material steps onto a visible ladder:

| State | Meaning |
|---|---|
| **Stable** | within capacity — clean, deterministic |
| **Unstable** (capacity +1) | reduced control: signature generation widens — variance up, unusual candidates score higher, **and Emergent Phenomena odds rise** (§8 stage 4). Further ambitious work risks *fracture* (losing an identity) |
| **Volatile** (capacity +2) | the deep end: fracture likely, destruction possible on further work, and items minted from it may carry a drawback sentence |

Overfill is where deliberately weird outcomes live. The preview shows the odds either way —
stepping onto the ladder is always a choice made with open eyes.

## 10.4 Condition — the work budget

The old integrity float is replaced by a four-step visible ladder:
**Pristine → Worked → Strained → Fragile.**

- Identity-changing actions (transfers, reveals, rank-ups, overfills) step it down — default
  one step each; gentle work (refinement, preparation) doesn't step it. *(Provisional.)*
- At **Fragile**, ambitious work risks **destruction — which still pays byproducts.**
- A **restoration** verb can climb the ladder back up, at a cost.

Same "push one more or commit now" tension as the old system — the rhyme with the extraction
decision is deliberate — with zero hidden arithmetic. All specific step rules and odds are
provisional until play.

---

# 11. Representation — the material state (approved 2026-08-20, D45)

## 11.1 Authored shape

New fields on `MaterialDefinition`; ids and tags untouched, so loot tables and profession
actions survive the migration:

```jsonc
{
  "id": "material.oak",
  "name": "Oak",
  "tags": ["state:raw", "form:wood", "origin:flora", "rarity:common"],
  "base": { "heft": 4, "toughness": 5, "give": 6 },   // §11.5 — omit stats at zero
  "capacity": 2,
  "identities": [],                              // innate active identities (often empty)
  "latent": ["identity.vital"],                  // present, inactive until revealed
  "signature_profile": { /* §6 — optional */ }
}
```

## 11.2 Runtime state — eight facets, each readable in a sentence

The successor to `MaterialState`. Against the old model's 21 floats plus five derived layers:

| Facet | Shape | Answers |
|---|---|---|
| Identities | list of `{id, rank 1–4}` | what it is |
| Latent | list of ids | what it could become |
| Capacity | small int | how much it holds |
| Stability | Stable / Unstable / Volatile | is it overfilled |
| Condition | Pristine / Worked / Strained / Fragile | how much more work it survives |
| Quality | one workmanship number | how well it was made |
| Profile | merged `SignatureProfile` | its personality |
| Provenance | bounded roots (lossy, like today's lineage — deliberate) | where it came from |

Ranks are internal integers 1–4 mapping to the family rungs; presentation renders qualitative
language, never numerals (D44), pending the named-evolution question.

## 11.3 The fingerprint

The stacking hash (successor to `MaterialSignature`) covers: **sorted identities+ranks ·
capacity · stability · condition · quality bucket · quantized root composition ·
`form:`/`state:` tags.**

Deliberately excluded: **full crafting history** (state is canonical — history shapes state,
and two paths to the same state stack together) and **profile content** (the merged profile
derives deterministically from root composition, so hashing roots covers it). Same-state-stacks
survives intact; bucket sizes are provisional.

## 11.4 Profile merge

Merged profile = weighted union of source profiles by contribution share: weights renormalized,
trace entries pruned, list lengths bounded, exclusions unioned, themes deduped at max weight.
Deterministic; order matters *across* crafts because intermediate states are real inputs —
which is exactly why process order matters at all.

## 11.5 The base-stat channel (approved 2026-08-20, D46)

The mundane physical floor — what answers "how hard does the plain iron sword hit" now that
properties are gone. **Four visible physical stats, 0–10 integers**, in an optional `base`
block (absent = zeros; tag gates keep non-structural materials out of structural slots anyway).
These are player-facing gameplay numbers like item damage, not hidden sim values:

| Stat | The physical fact | Drives on items |
|---|---|---|
| **Heft** | how heavy | impact damage and block up, action speed down, stagger — the heavy/light axis |
| **Bite** | how keen an edge/point it takes | weapon damage from cutting/piercing slots |
| **Toughness** | how much punishment it absorbs | armor, block strength, durability-flavored reads |
| **Give** | flexibility, spring | hafts, bow staves, grips, cloth — handling and the flexibility-weapon axis |

**The dividing line, stated as a rule:** *if a physical quality only matters for special or
magical behavior, it is an identity, not a base stat.* Elemental resistance is Warded/elemental
turf; crit is Keen; mana is Arcane. This line is what keeps the channel at four stats forever —
the old conductivity/resonance/insulation axes all live on the identity side now.

**Behavior:**

- **Identity work never moves base stats** — transferring Vital into iron doesn't change its
  Bite. Substance-changing transformations (alloying-style verbs) may; that belongs to the
  transformation-verbs design. Refining workmanship is **quality**, which modestly scales base
  delivery at fabrication.
- **Emergent materials inherit base from their physical roots**, weighted by contribution.
  Roots are already in the fingerprint, so base needs no separate fingerprint entry.
- **Forms keep the mechanism that worked:** slots, mass shares and tag gates survive; the old
  `stat_map` becomes **base reads** — each form declares which slot's base stat feeds which
  item stat (Longsword reads Bite off the edge; Warspear reads Give off the haft; a bow reads
  Give off the stave). The mechanism behind "no best material, only best placement" was never
  the problem — the 21 hidden inputs were. Same mechanism, four visible inputs.
- **One scale constant** maps base units to combat units, parity-pinned to the authored Iron
  Sword — the same proven calibration trick.

**Consequences:** plain gear is viable on base alone (the mundane floor carries the early game;
identities are the growth axis, not the entry fee) · Focus and Ring become almost pure identity
items — a plain oak focus is a stick, which is correct fiction · content migration simplifies
(author four visible integers instead of guessing 21 hidden floats).

## 11.6 Deferred on purpose

- **Item-side identity expression** — resolved (D51, §8.1): union + form cap + dormancy, with
  readable slot roles. Ships in Phase 3 together with the per-form base reads.
- **Every specific number above** — provisional until play.

Rank accrual and transfer math are settled in **`docs/transformation-verbs.md`** (D47): raw
transfers deliver rank 1, prepared carriers deliver their full rank, Develop feeds on
same-identity sources, and ranks never decay passively.

---

# 12. Reality tests — the worked path (Step 3)

The foundation, run against the brief's canonical examples. Each test asks: *does the model
produce this without new rules, and can the player explain the result?*

**1 · Iron Ore → Iron Ingot → Iron Sword (the mundane floor).** Iron Ore: capacity 2, no
identities, neutral profile. Smelting changes `state:`/`form:` tags and sets quality — still no
identities. A plain-iron Longsword gets base stats from the base channel [OPEN] and, with no
identities, no floor grants and little or nothing from the resolver — its damage and speed
compute from base reads (Bite off the edge, Heft across the whole; §11.5). **Plain gear stays
plain — no christmas trees.** ✓

**2 · Iron → Dense Iron.** Transfer Dense r1 from a Dense-bearing source into the ingot (or
reveal an authored latent — both paths exist; which materials get which is a content decision).
Deterministic floor: **Dense Iron Ingot**, 1/2 slots, Stable, Pristine→Worked. The name derives
from state. ✓

**3 · Oak → a prepared crafting product.** Oak (latent Vital; renewal/endurance profile) →
Herblore reveals and extracts → **Oak Extract**, a *carrier* material (capacity 1, Vital r1,
oak's profile) → Alchemy refines → **Distilled Oak Tincture** (Vital r1 at higher quality,
profile sharpened). A cross-profession chain with no recipe anywhere; carriers are a content
pattern (small capacity-1 vessels), not a mechanic. ✓

**4 · Dense Iron + tincture → Dense Oakbound Iron.** Transfer Vital from the tincture:
2/2 slots, Stable; merged profile leans oak (iron is neutral); condition Worked→Strained — the
material now poses the real question: *develop further, or forge now?* The fingerprint
({Dense 1, Vital 1} + roots {iron, oak} + state) stacks with any identically-reached batch. One
finding: the name generator needs **root-derived adjectives** ("Oakbound" from the oak root) —
a naming-grammar work item, not a foundation problem. ✓

**5 · The same material, two forms.** Longsword vs Buckler from Dense Oakbound Iron: identical
floor grants (Dense basic + Vital basic), opposite signature leans. The sword form biases
`on_hit`/`on_crit`, so oak's `on_block`/`store` preferences score lower and on-hit sustain
candidates rise; the buckler biases `on_block`, so *"blocking charges a barrier"* dominates.
**Same material, two personalities, zero authored combinations** — placement matters. ✓

**6 · The gamble.** Push Ember into the full material: **Unstable** — the preview shows
fracture odds, a wilder candidate table, and raised anomaly odds. Expert play is reachable,
legible, and optional. ✓

**Findings carried forward:** root-derived naming adjectives (naming grammar); the carrier
archetype (content pattern). The base-stat channel (§11.5) closed the last blocked computation.
The foundation survived all six tests without inventing a new rule.

---

# 13. Naming decisions (approved 2026-08-20)

| Decision | Ruling |
|---|---|
| The concept word | **Identity** stays, as both design and player word. The old "identity = hash" usage dies with the old system |
| The stacking hash | Renamed **fingerprint** (was `MaterialSignature` / "signature" in old docs). "Signature" now means only the generated effect layer |
| Old "P4 Signature reactions" | **Absorbed** by this system — authored spikes become authored profile/weighting data |
| The `arcane` damage aspect | Renamed **`kinetic`** (semantics unchanged: raw unresistable force, no lane, structurally unamplifiable — D-03a). The word is freed by cutting Kinetic-the-identity. Sweep: `DamageAspects.Arcane` + every packet authored with aspect `arcane` (incl. `spells_arcane.json`), same-commit with content |
| The fortune identity | **Charmed** |
| Essences | **Absorbed into identities:** fire→Ember · frost→Frost · storm→Storm · nature→Verdant · necrotic→Blighted · radiant→Radiant · abyssal→Umbral (corrosion *lane* stays Corrosive's mechanical home). `essences.json` retires; the `essence` scoped-modifier dimension and `essence:*` move tags become the identity dimension in the same sweep. **D29.3 survives translated:** the richest identity-bearing reagents remain opportunity-gated, active-only |
| Transitional collisions | Trait ids Keen/Venomous/Verdant/Blighted, the Resonant innate, and the `resonance` property all die with the layers they live in — no permanent renames needed, only migration care |

---

# 14. Open questions — deliberately unresolved

| # | Question | Belongs to |
|---|---|---|
| 1 | ~~Signatures vs rolled modifiers~~ — **RESOLVED (D50, §8):** one unified pipeline (`ItemEffectResolver`) emitting three categories — identity floor expressions, ordinary generated effects, optional Signatures; the 44 affixes re-ground as shared-vocabulary content | ✅ 2026-08-20 |
| 2 | ~~Item-side identity expression~~ — **RESOLVED (D51, §8.1):** union + form cap + dormancy; readable slot roles, never percentage apertures; material capacity and the form cap stay separate | ✅ 2026-08-20 |
| 3 | ~~Profile visibility~~ — **RESOLVED (D53):** Assay reads the profile as plain leanings at its high rung — the strongest few favored triggers/behaviors/payloads in vocabulary words, weights as words, exact weights Advanced-only. Themes never visible (§6.1). The forge's candidate table speaks likelihood words derived from score share, scores behind Advanced | ✅ 2026-08-20 |
| 4 | **Named identity evolution** (§5) | Later, explicitly |
| 5 | **Residual sentence-count drivers** (quality, generation context — §7.1) | Balance |
| 6 | **Emergent Phenomena** — the anomaly system behind §8's seam; overfill raising its odds (§10.3) is its first designed input | Much later; seam only |

The save stance is **decided** (D49): progression survives, items reset — see §16.

Verb-level open details (Fuse derivations, Restore's ceiling, fracture targeting, cost curves)
live in `docs/transformation-verbs.md` §7.

---

# 15. Migration plan

Ordered so the game stays green and nothing is authored twice. Position marker kept current.

| Phase | Work | Status |
|---|---|---|
| **0 — Foundation on paper** | Identity roster ✅ · grammar vocabularies ✅ (draft) · Signature Profiles ✅ · capacity + condition + representation ✅ · reality tests ✅ (§12) · base-stat channel ✅ (§11.5) · transformation verbs ✅ · profession assignment ✅ (`docs/transformation-verbs.md` §8) | **✅ complete (2026-08-20)** |
| 0.5 — Prep fences | Unknown-JSON-field rejection ✅ (`DataStore` rejects unknown members — shipped with the spell-library commit) · `arcane`→`kinetic` aspect sweep ✅ (code, content, tests, combat docs) · fingerprint needs no pre-work (the new name arrives with Phase 2's code; `MaterialSignature` dies on schedule) · save stance decided (D49) | **✅ complete (2026-08-20)** |
| 1 — Core model, coexisting | Identity + profile + grammar registries as content types (bundle → loader → validator → failing-content tests → ContentStudio registry); `MaterialDefinition` grows capacity/identities/latent/base/profile. Old system untouched, suite green. Shipped: 24 identities · 22 triggers · 11 behaviors (detonate/spread/bloom parked behind the D30 fence, named in the pin test) · 16 themes. The payload registry and `favored_payloads` arrive with Phase 3, so no reference ships unvalidated | **✅ complete (2026-08-20)** |
| 2 — Transformation engine | The ten verbs (`docs/transformation-verbs.md`) behind the same bench surface; profile carry/merge; condition + stability enforcement; preview parity + reaction-log equivalent from day one. **Slice 1 ✅ (2026-08-20):** `core/Crafting/Identity/` — the eight-facet state, the fingerprint, root-derived profile/base, and `IdentityCraftingEngine` with all ten verbs, preview parity, and the §6 worked chain passing as a test. **Slice 2b ✅ (2026-08-20):** `VerbActionDefinition` (verb + gates + identity scope + Process output + extra costs) with full validation and station routing. **Slice 2c-core ✅ (2026-08-20):** emergent registration reusing `EmergentRegistry` under fingerprint ids (`MaterialDefinition.IdentityState`), the naming generator (identity adjectives + "-bound" root adjectives, four-word budget — "Vital Oakbound Iron Ingot" ships), the **authored-equivalence rule** (plain smelted ore deposits as `material.iron_ingot`, never an emergent twin), `VerbActionRunner` (gates → verb → consume → register → deposit, testable in Core), GameRoot commands (`PreviewVerbAction`/`RunVerbAction`/`VerbActionsAt`), and a **starter content set**: 5 migrated materials (iron ore/ingot, granite, oak, sageleaf), 9 verb actions, 5 stations routing them — the full worked chain runs over real shipped data. Transfer/Displace/Develop now merge source provenance (share 0.15 — 0.25 flipped the substrate's primary root under repeated infusion; caught by test). **Slice 2c-finish ✅ (2026-08-20):** save schema **v12** — `IdentityArchetypeSave` persists the eight facets beside the old-model archetypes (capture used to *throw* on a new-model registry entry; the split is pinned by test, and a v11 save loads with none) · `VerbBenchPanel` at every station routing verb actions (action/material/source/identity pickers shaped by the verb, preview-before-commit, engine step text pending the Phase 6 semantic pass). **Phase 2 complete** — editor verification of the panel pending, per standing practice | **✅ complete (2026-08-20)** |
| **3 — Item generation** | The item-effect pipeline (`ItemEffectResolver`, D50 — identity floor expressions + ordinary generated effects + optional Signatures, succeeding genome → `ModifierGenerator`); the payload registry (effect families as content); per-form base reads (§11.5); item-side expression (D51, §8.1) | **✅ complete (2026-08-20)** — shipped: the payload registry (bare-key entries binding to live machinery, families+rungs, the one-floor-per-identity discipline validator-enforced; 8 starter payloads over Dense/Vital/Ember/Warded; `favored_payloads` joins profiles and §9's breach path works) · form identity fields (`identity_cap`, `base_reads`, per-slot `identity_priority`, `generation_profile`) authored on Longsword + Buckler, leather migrated to complete the starter set · `IdentityEquipmentComposer` (D51 union/cap/dormancy; base delivery parity-pinned to the authored Iron Sword through the live resolver seam) · `ItemEffectResolver`/`SignatureResolver` + 11 behavior assemblers with preview-parity projections (the scored table IS the draw distribution; per-payload diversity cap) · `IdentityFabricationEngine` minting `ItemInstance`s that carry the three categories, dormant identities and the base delivery · save **v13** (sentences + delivery + identity split persist; derived definitions ride the existing emergent-equipment list) · `IdentityForgePanel` beside the old assembly · the equip seam (stat grants, rules, gauges, move modifiers) attaching sentences like affixes. Editor verification pending, per standing practice |
| 4 — Material database | Re-author the library to capacity/identities/latents/profiles; `material.*` ids stay stable so loot tables and profession actions survive. **The expected cull was consciously declined (D52)**: the library measured 1,448 materials with 1,446 referenced by shipped profession/loot content, so everything migrates | **✅ complete (2026-08-20)** — all 1,448 materials migrated: derivation-drafted (capacity by rarity +magical, base from properties on structural forms — `TagFamilies.StructuralForms`/`EdgeCapableForms` now validator rules — latents from D44's essence map + property signals, ~413 carriers) and hand-tiered: **53 active-identity materials** (elemental motes/shards r1, essences/hearts/runes r2, cores r3; Earthen found its home in elemental earth, Resonant in the resonance catalyst, Pure in the alchemical salts) · **every one of the 24 identities has a shipped source and an authored floor payload** (28 payloads total; the fence caught `craft.quality`/`loot.*` as declared-but-unread, so Pure floors on `profession.preserve.chance` and Charmed on `attr.luck`) · **the acquisition fence** (D29.3 translated: gathering faucets never passively pay active-identity stock — caught the arcane core as a guaranteed idle yield; three new opportunities took the evicted payouts, 36→39) · **46 curated signature profiles** · seeds carry their plant's latent (D48). Fences: `MaterialLibraryMigrationTests` |
| 5 — Professions | Identity verbs per profession (gather/reveal/isolate/transfer/develop); bench awards XP/mastery (fixing the existing gap); cross-profession prepared products | **✅ complete (2026-08-20)** — **the bench trains**: `VerbActionDefinition.experience` (validated: gated ⇒ pays, ungated ⇒ cannot), awards through the shared `ProfessionProgress` ledger on any run where work happened (success/fracture/destruction; refusals pay nothing), level-ups surface in the result and at the bench, and the preview names the pay · **mastery steadies the hand** (the D47 §4 consumer): per-action mastery shaves both risk chances via `VerbRequest.RiskReduction`, engine-clamped at the ceiling — built in the shared gate path so preview and commit read one practiced hand · **the D48 matrix is content**: 53 actions across 11 professions at their own stations, pinned by test incl. Runecrafting-is-the-only-identity-scoped-profession; domain Restores eat salvage stock; Expand costs its catalysts; the worked chain now trains all four of its professions · **preparation = activation**: raw→prepared Process pairs (drying emberleaf/frostfern, curing, spinning/weaving, hewing, glass-melting) land on the authored prepared ids with the output's innate identities active, pinned by `DryingActivatesThePreparedForm` |
| 6 — Presentation + UI | Identity/signature readings replace property readings; Assay re-aimed at detecting latents and reading potential | **✅ complete (2026-08-21)** — D53 (profile leanings in words · likelihood-word draw table). Shipped, all in `Dungeons.Presentation` (docs/presentation-architecture.md §5.2): `SentenceReadings` (one sentence → one player line, truthful to the assemblers; modifier units derived from the key registry) · the item card/strip identity layer (`Identities:` with §4 rung words — numerals stay banned; `Guaranteed:`/`Signature:`/`Drawback:` labels keep D50 legible; dormant identities on the dormant line) · `MintReadings` (forge preview: likelihood words vs the uniform share, "beyond its families" for breaches, exact scores behind Advanced) · `VerbReadings` (refusals in words; previews/outcomes diffed from engine states; engine step text = the Advanced voice) · `IdentityMaterialReadings` (bench inspector: §11.2 in sentences) · `AssayLens.IdentityMaterial` (Vessel → Latency → Latent names → Leanings → Potential on the same five rungs; stakes + overfill never gated; themes never shown). Workmanship words rough→masterwork; `bulwark` fixed from delta to factor range behind a new multiplicative-range validator fence. Editor verification pending, per standing practice |
| 7 — Deletion + docs | Property algebra, genome pressure, trait/essence layers, stale tests and superseded docs removed; save schema settles | **✅ complete (2026-08-21)** — D54 executed. The migration finished first: all **23 forms** author identity fields (caps, base reads, priorities, generation profiles; every-form-forgeable pinned by test) and the D34 name-variant pick carried across (`IdentityFabricationEngine.FormNoun`, deterministic from the derived id). Then the deletion, whole: the reaction engine/algebra/quantization, genome + `ModifierGenerator` + the affix layer, traits, essences, `MaterialState(+Resolver)`, old fabrication, the property presentation stack (readings/tiers/trends/glossary/risk bands/`AdvancedFormat`), `CraftingBenchPanel` + `EquipmentAssemblyPanel`, the `processes/`/`traits/`/`essences/`/`affixes/`/`name_grammar/`/`properties/` content folders, and every test that pinned them (suite 1,378 → **1,011**, all green, 0 warnings). Material JSON stripped of `properties`/`essence` (1,448 entries), forms of `stat_map`/`trait_expression`/`trait_cap`, stations route `verb_actions` + `has_assembly` only. `ItemInstance` and the save settled at **v14**: identity fields only; pre-v14 loads keep every progression section (gold included) and drop every item section — the v9 slot-rename shim retired as unreachable. The live-fire re-grounding of the thorns e2e caught a real Phase 3 bug (retaliate aimed at `TriggerSource`; the attacker is the defensive events' *target* — fixed in `SentenceAssemblers`). ContentStudio registry/schema/balance views follow (14/14 green). Docs: the six superseded docs + `PROJECT_STATE`/`SYSTEM_INDEX` deleted; `crafting-overview` rewritten as the identity-stack map; CLAUDE/code-map/game-overview/GDD/ROADMAP/loot/json-schema refreshed |

---

# 16. System impact ledger

All consequences landed with Phase 7 (2026-08-21); kept as the record of what the migration
touched:

- **Saves — executed (D49/D54, v14): progression survives, items reset.** Pre-v14 loads keep
  every progression section exactly (gold included) and drop every item section; the
  starter-kit rule re-equips. The old-model DTOs are gone; the serializer ignores their keys
  in old files.
- **Combat:** no pipeline change, as promised. The retired affixes' machinery (retaliation,
  parry, barrier, potency/duration keys, move-mod grants) lives on, fed by sentences. Open
  content later: Wither and the decay lane, charge gauges, chain content; four effect-kind
  gaps (§7.3) + the `LootResolver` luck seam (§7.4).
- **Content types:** identities, profiles (on materials), triggers, behaviors, payloads,
  themes and verb actions all ship the full chain (bundle → loader → validator → failing-
  content tests → ContentStudio registry + `SchemaOverrides`). The property/process/trait/
  essence/affix/name-grammar types are deleted end to end.
- **Content Studio:** registry and schema overrides track the identity types only; the 0–100
  material histogram warnings retired with the numbers they histogrammed.
- **Presentation:** the property stack (tiers/pips/trends/glossary/risk bands) is gone; the
  semantic layer shrank exactly as predicted because the sim itself became legible. D30
  discipline unchanged; D53 added the profile-leanings and likelihood-word voices.
- **Docs:** the six superseded docs and `PROJECT_STATE`/`SYSTEM_INDEX` are deleted (git is
  the archive); `crafting-overview.md` is the identity-stack map; CLAUDE.md, code-map,
  game-overview, GDD, ROADMAP, loot and json-schema are refreshed.
- **Tests:** 367 old-system tests retired with their systems (1,378 → 1,011 green); the
  validator-before-content method continues — Phase 7 itself added the multiplicative-range
  payload fence and the every-form-forgeable pin.
- **Standing decisions:** the crafting-half balance backlog is superseded into the identity
  playtest checkpoint (ROADMAP #4); D7's *goal* survives with a new mechanism; D20, D30,
  D-01, D-06, D-07, D29.3 (identity edition), D40 all carry forward.

---

## See also

| For | Read |
|---|---|
| Why each decision, and what was rejected | `DECISIONS.md` D42–D49 |
| The crafting verbs, the rank economy, and the profession assignment | `docs/transformation-verbs.md` |
| The outgoing system (code as shipped, superseded as design) | `docs/crafting-overview.md`, `docs/emergent-item-system.md`, `docs/affixes.md`, `docs/itemization.md` |
| The combat machinery the grammar binds to | `docs/effect-foundation.md`, `core/Rules/TriggerRule.cs`, `core/Events/GameEvent.cs` |
| The presentation rule this system must still obey | `docs/presentation-architecture.md` (D30) |
