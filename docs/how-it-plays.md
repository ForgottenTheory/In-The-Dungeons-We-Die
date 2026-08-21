# How It Plays

> The experience arc, system by system. This document describes the game as a **played
> experience** — what the player is doing, wanting, and learning at each stage — the layer the
> mechanics docs deliberately don't cover. Nothing here redefines a mechanic; every rule lives
> in the linked doc. Status marks (**BUILT** / **PLANNED** / **NEEDS DESIGN**) refer to the
> machinery; the arc itself is design intent, validated at playtest checkpoints (C2c first).
>
> Chapters land one at a time as each system is fleshed out. **Ch. 1: Crafting** (2026-08-16 —
> ⚠ written for the property system the identity redesign replaced and deleted, D42–D54; the
> arc's intent stands, its mechanics references are history — current: `identity-foundation.md`).
> Planned: Combat · Realms & extraction · Professions · Character identity · Economy.

---

# Chapter 1 — Crafting: from the first blade to Overreach

## 1.1 The spine

**Extraction converts risk into materials; fabrication converts materials into permanence**
(D28). Unsecured loot is inputs; the forged answer to them is safe by default (D10). The bench
is where realm risk becomes durable power — which is why gear comes from the bench, why the
realm drops inputs rather than outputs, and why this arc is the game's progression spine rather
than a side system.

The three risk decisions (GDD §13.2) all run through it: *extract or go deeper* decides what
reaches the bench; *refine once more or commit* is the bench's own gamble; and Overreach
eventually adds the fourth verse — *push the finished item once more, or stop while it's good.*

## 1.2 What the realm gives — the drop taxonomy (D28)

| Drop class | What it is | Feeds | Frequency |
|---|---|---|---|
| **Anatomy** | hide, bone, gland, venom — creature parts in the real material library (GDD §12.4) | everything | common |
| **Salvage** | enemy-wielded gear arriving as *materials* (the Brute's crude blade → scrap metal + rawhide) | smithing chains | common |
| **Rare profiles** | unusual property-combination materials (Storm Core, Glacial Heart) | trait & essence hunting | uncommon |
| **Essence-bearing parts** | the supernatural tier's raw input; realm sources, plus profession opportunities only (D29.3) | Attune → infusion | rare |
| **Techniques** | items that teach moves (**BUILT**, M2′) | the moveset | uncommon |
| **Schematics** | items that teach **forms** (D29.2) — techniques' sibling | fabrication breadth | rare |
| **Catalysts** | not consumed; bend process rates | craft planning | uncommon |
| **Relic materials** | impossible profiles, pre-attuned essence, traits nothing else can birth — the chase tier (D28.1) | endgame genomes | very rare *(post-slice)* |
| **Sealed uniques** | authored rule-breaker gear; no genome, no rolls; Fractures into a relic-grade material (D28.2) | the one exception | rarest *(post-slice)* |

Consumables follow the same logic: crafted-primary, found rarely and situationally (D28.4).
**No finished, rollable equipment ever drops.** Every drop class above is either an input or
knowledge — the sealed unique is the lone fenced exception, and even it terminates at the bench.

## 1.3 The seven stages

| # | Stage | Arrives | Gated by | The pull to the bench |
|---|---|---|---|---|
| 0 | **The starter floor** | minute 0 | — | none; it exists to be obsoleted (D10) |
| 1 | **The first blade** | first session | Smithing 1 | beat the floor with something that has your name on it |
| 2 | **Steering** | hours ~1–5 | Quench 5 · Alloy 10 · Steep 1 | stop accepting outcomes; aim at one |
| 3 | **The realm feeds the bench** | first extractions | Attune (Alchemy 10) + realm inputs | the supernatural tier |
| 4 | **The casino opens** | mid-game (E5) | Assay | engineer luck before spending it |
| 5 | **Operations** | mid-late (E7) | the byproduct economy | failure has been funding this all along |
| 6 | **Overreach** | late (E7) | repeatable, escalating Ruin | the fourth risk verse |

The gates are all soft — profession levels, materials, knowledge — never mode switches. That is
the same philosophy as D25 for classes: the question is always *"can I make this work yet?"*,
never *"has the game turned this feature on?"*

### Stage 0 — the starter floor

Authored gear exists so death can never brick a character (D10). It is deliberately the thing
crafting obsoletes within the first session; the four hand-authored items are a floor, not a
loot class (D28). If a player is still wearing the starter kit in hour three, Stage 1 has
failed.

### Stage 1 — the first blade

Mine → Smelt (Smithing 1) → fabricate. The first Longsword from plain refined iron, rawhide on
the binding, done inside the first session. It only needs to beat the floor slightly — the point
is not power, it's authorship: the Reaction Log narrated every step, the projection warned
before commitment, and the item that came out has a name the player caused.

Affixes roll even here (D29.1) — usually nothing, occasionally one **unreadable mark**. The mark
is deliberate: the player learns *items carry more than stats* long before the system explains
itself, and the mark is the standing advertisement for the knowledge layer.

**What it teaches:** the log is the tutor; the bench beats the floor.

> **C2c checklist item:** a fresh character must be able to reach a fabricated Longsword inside
> the first session using professions only — no debug grants. Longsword slots require metal
> (edge), metal/wood (core), hide/fiber (binding); if the early profession ladders don't supply
> a binding-legal material, that is a content gap to close, not a pacing redesign.

### Stage 2 — steering

Quench (5), Alloy (10), Steep (Herblore 1). The player stops accepting outcomes and starts
aiming at one: order matters (six outcomes from three reagents), integrity is a budget, and
elegance is mechanically cheaper than force. Destruction stops being a disaster — byproducts
are the consolation prize, and, quietly, the currency of Stage 5.

The stage's landmark is the **first trait birth** — a named, capped, drawback-bearing quality
(*Emberveined*) that turns "high heat number" into an identity. Dormancy starts a habit here
too: a trait this form can't express is a reason to keep the material for a different form, not
scrap it.

**Pacing target:** the first trait birth should be reachable within Stage 2 — tuned at C2c.

### Stage 3 — the realm feeds the bench

The first extractions land anatomy, salvage, maybe one rare profile worth planning around.
Essence arrives from realms, and from professions only through the active Discover → Pursue
layer (D29.3, settled 2026-08-18), so the first meaningful essence craft is a
post-extraction milestone *by construction* — the supernatural tier is extraction's reward, and
"why do I keep running realms" now has a permanent crafting answer. Strain teaches the lesson
in the engine's own voice: **attune first, then infuse** (BUILT, C1b) — powerful magic needs a
worthy vessel. Schematic drops open exotic forms (D29.2).

From here the loop is self-reinforcing: extract to craft, craft to survive deeper.

### Stage 4 — the casino opens (E5)

Assay's two surfaces come online: **material proximity hints**
(`emergent-item-system.md` §15.4 — *"within reach of a Resilient state; needs flexibility"*)
and the **Genome Readout** (`affixes.md` §2.3). The unreadable marks become modifiers;
eligibility, weight and tier ceilings are visible *before* rolling; innates guarantee that a
well-engineered item is never a total loss. The player's question shifts from *"what did I
get?"* to *"what odds did I build?"*

### Stage 5 — operations (E7)

Anneal · Etch · Scour · Reforge · Bind · Temper · Fracture — paid chiefly in destruction
byproducts. The economy loop closes retroactively: every blown craft since Stage 2 was funding
this stage. Gambling stays bounded by engineering because every operation respects the genome.

### Stage 6 — Overreach (E7)

The repeatable, escalating-Ruin casino, drawn only from the item's own genetic families. Above
it sits the chase: **relic materials** (D28.1) — the drop that makes an endgame genome possible.
And the rarest surprise in the game: a **sealed unique** (D28.2) — a rule-breaker with a
drawback, never a stat-stick — which, when outgrown, Fractures into the relic-grade material at
its heart. Even the exception ends at the bench.

## 1.4 The knowledge arc

Legibility is layered. The game never hides *what happened*; it prices *what will happen* and
*what it means*:

| Layer | Surface | When |
|---|---|---|
| **Reaction Log** | why each craft did what it did | always, from craft #1 (**BUILT**) |
| **Pre-commit projection** | result, cost, destruction odds before committing | always (**BUILT**) — the §9.5 fairness guarantee |
| **Codex** | what you have discovered — never what you haven't | P6 |
| **Assay — material hints** | trait proximity, receptiveness, saturation | P6, depth by profession level |
| **Assay — Genome Readout** | pressure, eligibility, tier ceilings, slots | E5, required scope |
| **Known-rules journal** | rules you've proven, stated in words | P6 — "the main progression currency of the crafting game" |

Principle (D29.1): **Assay gates legibility, never capability.** Nothing in the system waits
for the player to be able to read it.

## 1.5 Session texture

Early (Stages 1–2), five minutes at the bench is **one decision**: which reagent, which order,
commit or push. The projection says what will happen, the log explains what did, and the
session ends with one material better than yesterday's.

Late (Stages 5–6), five minutes is a **portfolio review**: which of three candidate materials
deserves the schematic'd form, whether this genome justifies rolling now or steering one more
step, whether the sword that is already good gets Overreached anyway. Deliberately the same
shape as standing at the extraction portal with full pockets.

## 1.6 Left open, deliberately

| Item | Where it lands |
|---|---|
| First-session sufficiency audit (fresh character → fabricated Longsword, professions only) | C2c checklist |
| Trait-birth pacing numbers | C2c balance backlog |
| ~~Essence source audit (38 essence-authored materials vs D29.3's noncompete rule)~~ | ✅ Settled 2026-08-18: profession essence is active-only |
| `forms.json` acquisition field + persisted known-forms list + validator rule | M6 (learned-list precedent) |
| Schematic content list (which forms drop where) | M6 |
| P4 signature-reaction exemplars | when P4 schedules |
| Consumable forms + arc placement (likely Stage 2 — meals/tinctures already exist as prepared materials) | P5c |
| Relic material + sealed unique exemplars | post-slice |

**Decisions this chapter rests on:** D28 (gear from the bench; the drop taxonomy; relic
materials; sealed uniques) · D29 (always-rolling affixes; acquired forms; essence as the
realm's export). Mechanics: `emergent-item-system.md` · `affixes.md` · GDD §9–§10, §13.2.
