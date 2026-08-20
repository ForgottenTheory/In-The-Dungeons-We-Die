# Game Overview — the whole game, plainly

> **What this is.** The game explained for someone who did not build it: what you do, why you
> care, how the systems feed each other, and honestly how far each one actually got. Read this
> first; follow the links at the bottom when you need rules and formulas.
>
> **What this is not.** Not a specification — `docs/GDD.md` is the authoritative detailed
> reference, and the per-system docs hold the math. Nothing here overrides them.
>
> Last synced with the repo: **2026-08-19** (1,191 passing tests, 0 build warnings, save v11).
>
> ⚠ **Crafting redesign in progress (2026-08-20, DECISIONS D42–D44):** the material/crafting
> model described here is being replaced by the **Identity + Signature system** — design of
> record: `docs/identity-foundation.md`. Crafting sections describe the code as shipped until
> the migration lands.

## How to read the status marks

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

You assemble a strange character out of parts. You train twenty persistent professions in a home
base — attended or while you sleep. You invent materials nobody designed by combining things at
a crafting bench, forge those materials into equipment whose magic is decided by what you made
it from, and carry it all into a hostile Realm. Everything you find there is **not yours until
you walk out with it**. The defining question, asked at every scale the game has:

> *"I'm holding something valuable and I'm still alive. Do I leave, or do I push my luck?"*

The tone is grim-funny — a lethal dungeon administered by something with a sense of humour and a
filing system. That is why your character might be *The Explosive Vanguard [Cited for Improper
Safety Procedures]*, and why every power states its drawback like a liability waiver.

### What makes it different

1. **Crafting with no recipes, anywhere.** A universal reaction algebra computes what *any*
   combination produces. Every experiment yields a real, named, stackable material — including
   ones nobody has seen.
2. **Builds you assemble, not classes you pick.** Base + Prefix + Suffix = **18,750 characters**,
   none hand-authored.
3. **Combat on a clock, not in turns.** The simulation keeps ticking while you think. Skill is
   reading a telegraph and answering it in time.
4. **Many overlapping reasons you got better.** Melvor Idle is the reference: levels, per-action
   mastery, cross-profession synergies, knowledge, techniques — never one power number.
5. **Extraction risk.** Realm loot (and coin) is unsecured until you leave. Death forfeits it.
   Your progression survives.

Every proposed feature answers one question or gets cut: *how does this make preparing for,
exploring, surviving, mastering, or extracting from a Realm more interesting?*

---

# 2. One run, end to end ✅

Everything in this walkthrough works in the game today.

You start in the **Hideout**, at your stations. Overnight, the Forge kept smelting — the away
report says you gained two Smithing levels and a chest of iron ingots, and that it stopped
early when the ore ran out. You work the Mineshaft **actively** for a while, aiming for the
middle of the timing sweep, and the extra attention pays off: *you notice something a stranger
to this work would walk past* — an unusually rich seam. Pursue it (it costs time, it might
collapse) or let it go? You pursue, and come away with more ore than a day of idle picking —
including a few lumps of stormglass.

At the Forge bench you run a **Forge Infusion** — iron ingot, then the stormglass, step by
step — watching the projection as you go ("Risk: COSTLY · within reach: Stormlaced — needs more
Charge"). You push one more step, and mint a material that did not exist five minutes ago:
**Stormlaced Iron**. First discovery. It stacks like anything else.

At the Fletcher's Bench you feed it into a **Longbow** form — limb, grip, string. The preview
tells you the truth before you commit: the limb wants flexibility and your iron is stiff, the
bow will be mediocre — *but* the same material in the **Longsword** would express its storm
trait fully. You forge the sword instead. Its **Genome** — how much hardness and charge actually
reached the parts that matter — decides what it can roll. It comes out with a guaranteed innate,
plus *"+12% Shock chance"*. You made the casino, then you played it.

On the **Realm tab** you pick the Dark Forest. The briefing shows only what your Realm Knowledge
has earned: the Brute's weakness to Crushing, the bog that costs health to cross, which workings
are rich. You pack two salves — *packed supplies are unsecured from the moment you enter* — and
go in at depth 2, through the deeper door you unlocked at 900 knowledge.

Inside, the clock runs. A goblin Brute telegraphs **OVERHEAD SMASH — impact in 1.2s**. Block it,
dodge it, parry it if your buckler grants parry, or interrupt it mid-windup. You win, and the
kill pays in **inputs** — hide, scrap, blood — never finished gear; gear comes from your bench.
Your bag fills. The elite behind the hazard would pay better. The boss below him would pay best.
Every step deeper is the same bet: extract now and bank it, or push and maybe lose the lot.

You die to Thornheart. The bag is gone — the ore, the salves, forty gold. The sword on your back
survives. The levels, the mastery, the knowledge, the discovery of Stormlaced Iron: all safe.
You know exactly what to do differently, and it starts at the bench.

---

# 3. The core loop ✅

```
                ┌────────────────── HIDEOUT ──────────────────┐
                │  Train professions — active, passive,        │
                │    or offline while the game is closed   ✅  │
                │  Gather and process raw materials        ✅  │
                │  Invent materials at the crafting bench  ✅  │
                │  Fabricate them into equipment           ✅  │
                │  Modifiers roll from the item's Genome   ✅  │
                │  Assemble your build (Base+Prefix+Suffix)✅  │
                │  Prepare: loadout, pack, briefing        ✅  │
                └───────────────────┬──────────────────────────┘
                                    │ enter (deeper doors unlock later)
                                    ▼
                ┌────────────────── REALM RUN ─────────────────┐
                │  Explore a spatial location graph        ✅  │
                │  Gather biome resources (time = risk)    ✅  │
                │  Fight on the clock — incl. elite & boss ✅  │
                │  Kills drop anatomy → crafting inputs    ✅  │
                │  Camps, shrines, a merchant, hazards     ✅  │
                └──────────┬──────────────────────┬────────────┘
                           │                      │
                 ┌─ EXTRACT┘                      └─ GO DEEPER ─┐
                 │  loot + gold secured            rarer loot,  │
                 ▼  to the Stash  ✅                more danger ✅│
           back to the HIDEOUT ◄────────────────────────────────┘
                 ▲
                 │ (death: unsecured loot lost, progression survives) ✅
```

**The loop is closed and runs end to end today — both halves of it.** The attended half
(prepare → fight → extract → craft → improve) and the idle half (a standing selection that keeps
training offline, waits out shortages, and reports honestly when you return).

### The risk rhyme — one decision at three scales

| Scale | The decision |
|---|---|
| **Realm** | Extract now, or go deeper? |
| **Crafting** | Refine once more, or commit this material before it's destroyed? |
| **Combat** | Spend the resource now, or hold it for the telegraph you can see coming? |

The endgame **Overreach** system (§10) is designed to be the fourth verse of the same rhyme.

---

# 4. Your character ✅

```
Species    +    Base    +    Prefix    +    Suffix
  🟡             ✅            ✅             ✅ (10 of 50 fully expressed)
```

- **Base** (15) — the *engine*: what grows each level, what resource drives you, how your loop
  feels against a ticking clock. A **Juggernaut** builds Momentum by taking hits; a **Wizard**
  charges one big spell and *holds* it for the right tick; a **Warlock** buys power cheap in
  mana and expensive in Debt, which grows, empowers — then collects.
- **Prefix** (25) — one recognisable mechanic bolted on: Feint-cancels, planted charges, toxin
  stacks you can burst early, a parasite that leaps hosts.
- **Suffix** (50) — the rule-breaker, and where the tone lives: *Exploding Kneecaps*, *Mandatory
  Overtime*, *Absolutely No Refunds*. Ten are fully mechanised; forty are named roster entries
  awaiting design.
- **Species** (3, thin) — physiology. The least-developed layer; today you are effectively
  Human. 🟡

**Three rules keep 18,750 builds sane, all enforced by validation and tests:**

1. **Every Base gets exactly the same growth budget** (4.0 attribute points per level). Only the
   *shape* differs — a Base choice is a trade, never a bigger menu item.
2. **A Prefix may never reference a Base.** Prefixes hook *events* ("a resource was spent"), so
   the Galvanic Bastion charges by blocking and the Galvanic Wizard by releasing a hold. One
   design, fifteen feels.
3. **A Suffix works on every build.** Each carries one expression per channel — **Strike** (you
   landed a hit), **Guard** (you avoided or absorbed), **Surge** (you spent or sustained) — and
   your Base's channel picks which one fires. Every expression states its drawback.

Nine **name grammars** (citation, warning, medical, liability…) change how a build reads and can
never change how it plays.

You pick Base, Prefix and Suffix in the **Char Lab** with a live *"What changed:"* diff. It is a
dev-styled surface — the real onboarding screen is future work — but the choice itself is real.

❓ Open: Species' role · the Fighter's identity hook (its old engine — "moveset comes from the
weapon" — became true for *everyone*) · the remaining 40 suffix mechanics · respec pricing.

---

# 5. The Hideout: twenty stations ✅

The home base is a real place: **twenty stations, one per profession** — the Forge, the
Apothecary, the Alchemy Lab, the Kitchen, the Tannery, the Loom, the Fletcher's Bench, the
Workbench, the Runic Altar, the Assay Table, the Mineshaft, the Timber Yard, the Fishing Dock,
the Garden Plots, the Hunting Lodge, the Bone Table, the Salvage Yard, the Thieves' Nook, the
Training Course, the Cartographer's Desk.

```
Hideout → choose a station → train its ladder / transform at its bench / assemble at it
```

A station is **where you stand, never whether you may** — it routes to ladders, bench actions
and blueprints, and every one of those keeps the gate it always had. Content cannot ship
unreachable: load-time rules guarantee every profession has exactly one home and every crafting
action and blueprint is offered somewhere.

Pinned above the stations: the **while-you-were-away report**, the passive status bar, the
active **timing sweep**, and the **Discover → Pursue** card when an opportunity fires.

🟡 The stations exist; the meta-layer (upgrades, unlock costs, storage management) is undesigned.

---

# 6. Twenty professions, three ways to train ✅

**Gathering (7)** Mining · Forestry · Fishing · Farming · Hunting · Beast Lore · Salvaging
**Processing (9)** Smithing · Herblore · Alchemy · Cooking · Leatherworking · Tailoring ·
Fletching · Artifice · Runecrafting
**Utility (4)** Thieving · Agility · Cartography · Assay

**348 actions · 36 opportunities**, one execute path behind all of it — active, passive and
offline literally call the same method, so they can never drift apart.

- **Passive** — automatic and reliable, and it *never rolls for opportunities*. That is a fact
  about the code, not a smaller percentage.
- **Offline** — a first-class path. Whatever is selected when you quit keeps running (capped at
  12 hours per absence). The selection is *standing*: run out of materials and it waits, then
  resumes by itself; only Stop stops it. You return to a summary — completions, items, XP,
  levels gained — that says plainly when a cap or an empty chest cut it short. The game
  autosaves on quit so the clock is honest.
- **Active** — a timing score, plus the **Discover → Pursue / Ignore** layer: a rich vein, a
  shape under the boat, an unattended satchel. Pursuing costs real time and can fail; declining
  costs nothing; mastery raises the odds and talks the risk down. Five of the best offers are
  **mastery-gated** — below the gate they are not rolled at all.

**Mastery** is per-action (0–99, one point per completion): the work gets quicker, bonus finds
get likelier — and at mastery 20 materials sometimes survive the work (**preservation**), at 40
the work sometimes pays double (**doubling**). Unlocks, not creep.

**Synergies (Phase 10):** 13 cross-profession bonuses that follow chains the trades already have
(Smithing helps Mining; Beast Lore helps Hunting), plus 2 account-wide bonuses that read your
**total** level. Everything pays into the same six quantities through one seam — worn tools
(E6) will be the third payer into it.

The professions are **an ecosystem, enforced by test**: every Processing trade eats another
trade's output; Hunting produces carcasses and *only Beast Lore opens them*; every seed has a
wild source; no profession is a dead end (Cooking is the one named exception until consumable
crafting lands).

```
Mining → Smithing → ingots → Artifice → lenses → Assay reads deeper
Hunting → carcass → Beast Lore → hide → Leatherworking → fabrication
Cartography → survey chart → Salvaging finds the ruin worth digging
Assay → property dossier → the three deepest crafting actions require one
```

Three trades are systems of their own: **Farming** (up to six plots, unlocked at levels
1/5/15/30/50/70, growing on the world clock — crops finish while the game is closed, and always
land safely in the Stash) · **Agility** (a five-slot training course of 12 obstacles whose
configuration is your standing Realm-utility loadout — declared and displayed, consumed by
nothing until profession tools land 🟡) · **Assay** (§8).

📐 Not built: worn profession tools (two slots; Artifice and Smithing already make the
components) · bow/projectile consumers for Fletching · Cooking's consumable outlet.

---

# 7. Materials and the bench ✅

## 7.1 The ingredient library

**1,448 materials** on a 0–100 property scale — deliberately **mundane-majority** (oak, iron,
salt, spring water) so the rare things stand out by their property *profile*, not by tier.
**There is no "Iron < Better Iron"** anywhere: Oak, Ironwood, Emberwood and Frostpine all fill
the "wood" role and behave differently. A wolf gives hide, fur, meat, bone, fang and blood as
separate materials. Every raw material has a gathering source — enforced exactly, by test.

**21 properties** in four roles: **structural** (hardness, mass, flexibility, affinity,
conductivity, insulation, solubility, resonance, instability — *what it is*), **reactive**
(heat, cold, charge, toxicity, growth, decay, corrosion, arcane — *what it does to things*),
**response** (the three resistances — derived, never inputs), **sourcing** (harvest_resistance —
read only by gathering). **Affinity — the willingness to bond — is the single most important
gate in the crafting engine.**

## 7.2 The crafting bench — no recipes, ever

**The engine is a total function, not a lookup.** Substrate + ordered reagents + a process +
an optional catalyst → a real result, always. Authored content is just **eight processes**
(Grind, Steep, Distill, Smelt, Quench, Alloy, Forge Infusion, Attune), four byproducts and a
name grammar.

```
Substrate  ← Step 1: Reagent A  ⇒ intermediate state
           ← Step 2: Reagent B  ⇒ intermediate state
           ← Step 3: Reagent C  ⇒ final state
           + Catalyst (not consumed — lends its affinity)
           + Process  (decides which properties react at all)
```

**Order matters automatically** — step 2 acts on what step 1 made. The algebra's one
load-bearing rule: on-channel properties **converge toward the reagent's value and can never
exceed the strongest input**. That single sentence kills stat inflation forever, with no caps.
Off-channel values wash out, opposites (heat/cold, growth/decay) annihilate — you cannot
stockpile both.

Two meta-numbers drive the drama:

- **Potency** — how strongly the result expresses. A **weighted mean, never a sum** — junk
  inputs *lower* it, so a high-potency mundane material beats a low-potency exotic one and base
  resources stay relevant forever.
- **Integrity** — a **transformation budget**. Gentle, well-chosen steps cost little; brute
  force costs a lot. **Zero destroys the material** — fair only because the projection shows
  cost and risk *before* you commit, and destruction pays out byproducts (Slag, Cinders, Dross,
  Residue) that are real reagents. *Refine once more, or commit?* — the crafting verse of the
  risk rhyme.

**Identity:** results are quantized, hashed, and registered as **stackable materials with
deterministic names** (max 3 words, no "of X", no tier words — `heat` climbs Warmed → Emberlit →
Cindered → Searing). Two players who reach the same state mint the same material with the same
name. **Variance produces different neighbouring materials, not random stats** — a bad roll is a
*different, weaker invention*, possibly one nobody has seen. High skill narrows variance to
zero.

**Traits (16)** are the qualitative layer — Emberveined, Frostbound, Stormlaced — born at
thresholds, capped at 3, with authored merges (Emberveined + Stormlaced supersede into
**Tempestforged**). Every trait carries a drawback. **Essence (7)** — fire, frost, storm,
nature, necrotic, radiant, abyssal — is the rare supernatural layer on top: capacity is governed
by `resonance` (Attune raises it), and excess becomes **strain** rather than a cap. *Powerful
magic needs a worthy vessel.* Essence is scarce on purpose: it is **extraction's export** — you
can only bank it through attentive play and Realm drops, never idle farming.

And because a system this deep is only playable if it explains itself, **every craft emits the
Reaction Log** — a human-readable trace where every line states *why*.

📐 Not built: signature reactions (authored spikes over the universal rule) · consumable forms ·
the codex/journal.

## 7.3 The language you see it in ✅

The player never reads "hardness 68" on a normal surface. **Three languages, one direction:**
simulation numbers → a semantic crafting voice (glyphs, tiers Trace→Extreme, trend arrows, risk
bands SAFE→DESTROYS, "within reach: Emberveined — needs more Heat") → gameplay payoffs (damage,
Crit, Thorns, Shock). Raw numbers live behind a single **Advanced** toggle and the labs. Items
speak gameplay language: *"+12% Shock chance"*, with the material influence shown as the cause,
never the reward.

---

# 8. Assay — buying comprehension, never power ✅

The reading of a material is computed identically at every Assay level; levelling only removes
`???`. Identity at level 1, composition at 10, reactive behaviour at 25, traits at 45, essence
at 65, full potential at 85. Modifiers on items you cannot read yet still work — the unreadable
mark *is* the advertisement for the knowledge layer. Assay also produces **property dossiers**,
which the three deepest crafting actions require as inputs.

---

# 9. Fabrication — materials become gear ✅

The terminal boundary: materials stop here, equipment begins, and it is irreversible — which is
why it gets the same fairness guarantee as the bench: a **pre-commit preview computed by the
same code that mints the item**.

```
Equipment Form + material in each named slot → a unique item with a Genome
```

`Longsword` is authored once — edge, core, binding, each slot with tag gates, a **mass share**,
and an **aperture** deciding how much of each trait category that slot can express (the rest
goes **dormant**: visible, valued, and fully available if you re-use the material in a different
form later).

**23 forms across all nine equipment slots (16 weapons), each one authored to disagree with the
others** about what matters — that is the whole trick that prevents a single best material:

| Form | What it reads |
|---|---|
| **Longsword** | hardness off the edge — the calibration reference |
| **Warspear** | flexibility off the *haft*, which is 60% of it — great sword-iron makes a mediocre spear |
| **Longbow** | flexibility at 1.10, hardness at 0.20 — a bow limbed in iron is a bad bow |
| **Maul** | mass off the head at 1.40 — lead is a bad sword and a fine maul |
| **Dagger / Javelin / Knuckles** | almost no mass read anywhere — the forms for rare, light materials |
| **Whip** | flexibility off the lash at 1.35; hardness only off the handle, which does no damage |
| **Focus** | the only reader of resonance/arcane — the caster's slot |
| **Ring** | the only reader of conductivity/affinity — one form fills either ring position |

~180 weapon names (Falchion, Scimitar, Sabre…) ride ten of the weapon forms as **cosmetic
variants picked deterministically from the item's signature** — the preview promises the noun
the bench mints, and a variant can never quietly become a mechanical difference.

Weapons carry their own moves (re-equipping literally reconfigures your moveset); armour pieces
carry armour and lane resistances derived from what you made them of; the worn total is the sum
of the loadout, with coverage authored per form.

📐 Not built: consumable forms · **form acquisition** — schematic items already drop, but today
every form is available from the first minute and the schematics bind to nothing. The one
progression track nothing reads, and the roll-call test names it. 🟡

---

# 10. The Genome, modifiers, and the designed endgame

## 10.1 The Genome ✅

Every fabricated item carries a **Genome**: not "how much hardness went in", but *how much
hardness actually reached the parts that matter*, weighted by the form's own stat map — plus
essence, traits (expressed and dormant), tags and potency. Same materials, different form ⇒
different genome.

The genome answers three questions about every possible modifier, all as pure functions the
player can see **before rolling**: *can* it roll (eligibility), how *likely* (weight), how
*strong* (tier ceiling). Potency decides where in the tier the value lands. **"Engineer the
casino" would be a lie if you gambled blind — so you don't.**

## 10.2 Innates and modifiers ✅

```
  1–3 INNATES        deterministic, zero variance, never rerollable —
                     a well-engineered material is never a total loss
+ ≤3 modifier-prefixes ┐  rolled from weighted, tiered pools;
+ ≤3 modifier-suffixes ┘  one modifier per family per item
+ Exotic / Signature / Anomalous     📐 (endgame, not built)
```

**44 modifiers ship** and every one resolves in real play — ailment chances, thorns and
retaliation, parry and avoidance, penetration, barrier, resource and trigger effects, and the
first move-rewriting modifier (Emberbrand, which adds Heat to your heavy strikes). A modifier is
always called a *modifier* on screen — the bare word "Prefix" belongs to characters.

## 10.3 Operations and Overreach 📐 DESIGNED — the crafting endgame

Not built yet, and worth knowing where it is going:

- **Operations** (Anneal · Etch · Scour · Reforge · Bind · Temper · Fracture) — targeted
  gambling on an existing item, paid for chiefly with the **byproducts of failed crafts**, every
  operation bounded by the genome.
- **Overreach** — the final casino: Ruin · Brick · Mutation · Elevation · Exotic Mutation ·
  Transcendence, drawn **only from the item's own genetic families** — a poison dagger can never
  Overreach into a lightning effect, at any odds. Repeatable with **escalating Ruin odds**: the
  extract-or-go-deeper decision, played at a workbench. **Anomalous** modifiers — the only
  content allowed to bend the proc-safety rules — exist only here. (The engine already reserves
  their exception; zero Overreach content exists today.)

---

# 11. Combat ✅

## 11.1 What a fight feels like

Tick-based, **not turn-based** — 20 ticks a second, and the clock runs while you decide.

```
QUEUE → TELEGRAPH → WINDUP → EXECUTION → RECOVERY → READY
```

The Brute announces **OVERHEAD SMASH** and you watch the impact countdown. Blocking is a timed
stance (0.8s window) that costs stamina and eats most of the hit; the first 0.2s of it is a
**Perfect Block** that negates entirely. Dodging (0.5s) avoids everything if you read it right.
**Parry** (0.15s) is gear-granted — a form must declare it (the Buckler does) — and staggers the
attacker on success. Holding block forever is never correct; the windows only matter near the
impact tick. **That timing is the core skill test, and gear buys windows and costs — never a
passive dodge roll — so the test can never be priced out.**

Interrupts can cut a telegraph or a windup (the game knows which). Recovery after your own swing
is the enemy's window. Health does not regenerate in combat — damage is Realm attrition, healing
costs resources, and the one designed recovery surface is **Barrier**, which soaks before
Health.

## 11.2 Damage, honestly traced

A hit is a list of **packets** — each with one damage type (Slashing/Crushing/Piercing/Magic)
and at most one aspect (heat, cold, charge, toxin, corrosion, decay, arcane). A flaming sword is
80 Slashing + 20 Slashing/Heat, never one relabelled hybrid. One resistance lane per packet;
armour applies to physical *delivery* whatever the aspect, so an aspect can never sneak past
armour and hybrids are never taxed twice. Armour is diminishing (strong against attrition, weak
against spikes); resistance caps at 75% with penetration applied after the cap; per-type
weaknesses live on the **enemy** — which is exactly what Realm Knowledge reveals. `arcane` has
no lane at all: unresistable, and structurally unamplifiable in exchange.

Every hit emits a stage-by-stage **Hit Log**, because a pipeline with this many sources is
unplayable if it cannot answer *"why did that hit for 17?"*

## 11.3 Statuses and Resolve ✅

**29 data-driven statuses** — no C# class per ailment. Ailments (Bleed, Burn, Poison, Toxin,
Latched), impairments that genuinely do what they say (Chill slows windups; Corroded strips
armour), controls, and tactical states. The load-bearing contrast is **Burn vs Poison** — short
and hot versus long and stacking — and **Freeze requires Chill**, making cold a two-step plan.

**Crowd control is gated by Resolve.** Controls build up; crossing the bar lands the control,
opens an immunity window, and permanently raises Resolve **+25% for the encounter**. The first
Freeze lands in seconds, the third takes fifteen — a boss is **never locked and never immune**.
Stagger is the same currency (buildup toward Stun), so you cannot stun-lock *and* freeze-lock.
Your tooltip still says "12% chance to Freeze"; the Resolve bar shows the truth.

Damage-over-time ticks can never trigger anything — one rule that kills an entire genre of proc
loops.

## 11.4 Auto-combat ✅ — automation pays in reflexes, never in damage

Toggle it on, pick a brain (Steady / Aggressive / Cautious), and the pilot plays your character
**through the same encounter, choosing from your real moveset by tag and pressing the same
buttons a hand would**. There is no separate "idle combat calculator" — deliberately, forever.

Its entire handicap is **reaction latency**: it commits stances 8 ticks (0.4s) early, so it
blocks and dodges reliably and can *never* land a Perfect Block or a Parry — and anything
arriving faster than it can react to simply lands. A brain fast enough to parry is rejected at
load. Window-widening gear is therefore worth *more* to an automated build than to a present
player — a real, discoverable difference between playstyles.

Live fights only: travel, extraction decisions and the run bag are yours. Fully unattended runs
are deliberately not built. 📐

---

# 12. Enemies ✅

**Enemy identity composes — never a class per monster:**

```
Family (what the body is)  +  Role (what it does)  +  Actor (who it is)
        26 families               7 roles                 483 actors
```

A Brute is armoured, slow, weak to Crushing and shrugs off Slashing — whether the body is
goblin, undead or construct, because `role.brute` is one definition. Seven reusable **AI
brains** choose intent by weighted rules that match moves **by tag** ("the big stagger threat,
whatever it is on this body"); the tick engine resolves the timing. **Elite and Boss are tags
through the same fold** — Grask the Warlord and Thornheart the Old Growth carry them today, and
the rank pays extra spoils and extra XP through seams that need no code for the next elite.

The full design roster ships as data. **Five encounters are wired into the world so far** — the
Dark Forest's Raider, Brute, Hexer (killing the caster is how a martial build finds a spell),
Grask, and Thornheart. The other 478 are authored breadth waiting on realm encounter wiring. 🟡

Kills pay **anatomy into the real material library** — hide, bone, gland, venom — so Beast Lore
feeds Leatherworking feeds fabrication feeds the next run.

---

# 13. Realms: preparing, exploring, knowing ✅

## 13.1 Preparation — the bridge ✅

The Realm tab opens on the **preparation screen**: pick the destination, see your loadout with
plain warnings (never a locked door — a starter kit is one button away if you own no weapon at
all), pack consumables (they transfer into the run bag at entry and are **unsecured from that
moment**), check your trades' field readiness, and read the **briefing** — which shows only what
your Realm Knowledge has earned. Enough knowledge, and you can start the run at a **deeper
door**, honestly priced: skipping depth 1 skips its loot and the knowledge it would have paid.

## 13.2 The Dark Forest — the finished reference realm ✅

**34 locations across three depths**, carrying every node kind the game has: gathering workings,
five combats, events, **camps** (rest once), a **shrine**, the **Hedge Trader** (the game's one
gold sink — and he trades in your *unsecured* run gold, so shopping is the extraction decision
in miniature), **hazards** (dangerous ground that costs health to cross — there is no "decline
to be in the bog"), four ways out, and **three hidden nodes that do not exist** for a party that
has not learned the routes.

Depth 1 teaches the place. Depth 2 is the wall — the elite behind a hazard, the trader, an
extraction deliberately far from the descent. Depth 3 is the payoff: a plant-family boss in a
goblin realm, *so everything you learned on the way down is the wrong lesson*, the two richest
workings in the game, and a hidden back door out of the boss room. **Deeper pays rarer, not
merely more** — pinned by test.

**163 more realms ship as walkable shells** — names, biomes, tier bands, small graphs — with no
encounters wired, deliberately: one realm is being made genuinely good before breadth begins.
(Realm tiers exist in data and currently change nothing. 🟡)

## 13.3 Realm Knowledge — seven insights ✅

Knowledge is earned by doing — entering, travelling, clearing, descending, extracting, shrines —
and it buys **information and options, never damage**:

```
  12  Common resources    what this place yields
  30  Enemy weaknesses    lanes and damage types that work
  75  Hazards             dangerous ground, seen instead of stood in
 160  Rich nodes          which workings repay the run-time
 320  Hidden routes       caches and shortcuts that were always there
 560  Extraction routes   the ways out, from wherever you stand
 900  Deep entry          begin the expedition at a deeper door
```

The same thresholds gate the pre-run briefing and the in-run intel, so the map and the plan can
never disagree. Roughly thirteen thorough runs see everything — the ladder is a campaign, not a
checklist. 📐 Not built: realm affixes, the full campsite workshop, procedural realms.

---

# 14. Loot, gold, extraction ✅

**One table shape pays for everything** — enemies, gathering, events, profession actions — with
guaranteed drops, independent chances, and weighted draws where "nothing" is a real outcome.
**72 tables ship.** Enemy loot **composes** exactly like the enemy does (family + role + actor,
merged), so a new creature becomes lootable in one line.

The rule that shapes the whole economy — **D28: realms drop inputs; gear comes from your
bench**:

> *Extraction converts risk into materials; fabrication converts materials into permanence.*

A slain Brute's crude blade arrives as scrap iron and rawhide, never as an equippable sword. A
test fails any table that yields finished equipment. The designed chase items are **relic
materials** — boss drops with impossible property profiles that feed the genome machinery
instead of bypassing it (the boss already drops the placeholder shards 📐).

Structural, not numeric: **active gathering reaches entries passive cannot** (a condition, not
better odds) · elite/boss spoils fire on the rank tag · a material's rarity comes from its own
tag, stated once · essence never sits in a profession drop table, and no profession table pays
coin — **coin and essence are Realm exports**, both held by test.

**Gold** drops, rides in your bag *unsecured like everything else*, and today spends in exactly
one place: the Hedge Trader. That ordering is deliberate — the faucet shipped before the economy
is designed, because a currency nothing spends is harmless and pricing before knowing what drops
is guesswork. ❓

**Death** ends the run: the unsecured bag — materials, drops, coin — is gone; worn gear is safe
by default; professions, mastery, knowledge, discoveries and the Stash survive, always. A
"gear at risk" toggle is designed and off. 📐

---

# 15. Progression — many reasons you got better ✅

There is deliberately **no single power number**. Nine persistent tracks, every one consumed by
a named system (a roll-call test fails any track nothing reads):

| Track | What it buys |
|---|---|
| Profession levels (×20) | New actions up each ladder; Assay reading depth |
| Per-action mastery | Speed, bonus finds, preservation (20), doubling (40), better offers |
| Realm Knowledge (per realm) | The seven insights, ending in deep entry |
| **Character level** | Attribute growth on the Base's shape — **earned in Realms only** (kills ×1.5 elite / ×2 boss, +25 per extraction). The Hideout awards none, ever — enforced by test |
| Crafting discoveries | The record of what you have invented |
| Techniques | 493 items that teach moves permanently — the original 19 loot-fed, the 474-spell library debug-only until the balance pass (`docs/spell-library.md`) |
| Synergies + global bonuses | Trades helping trades; total level pays account-wide |
| Gold | The Realm export waiting for its economy |
| Equipment owned | The Stash and what you wear — safe by default |

Levelling **raises ceilings and never heals** — pools carry across, clamped. Horizontal beats
vertical everywhere: progression unlocks options, routes, offers and combinations, not just
bigger numbers.

🟡 The one dead track, named and fenced: **schematics drop and bind to no form yet** (D29.2).

---

# 16. What is real, in one glance

**Content, all data-driven, all load-time validated** (a bad reference fails at startup, never
mid-play):

| Authored as data | Count |
|---|---|
| Materials / Properties | 1,448 / 21 |
| Processes / Byproducts / Traits / Essences / Forms | 8 / 4 / 16 / 7 / 23 |
| Item modifiers (affixes) | 44 |
| Moves / Move modifiers / Techniques | 517 / 11 / 493 |
| Statuses / Modifier keys | 29 / 60 |
| Bases / Prefixes / Suffixes / Species / Name formats | 15 / 25 / 50 / 3 / 9 |
| Professions / Actions / Opportunities / Obstacles | 20 / 348 / 36 / 12 |
| Mastery rungs / Synergies / Stations | 6 / 15 / 20 |
| Enemy families / roles / brains / actors | 26 / 7 / 7 / 483 |
| Auto-combat brains | 3 |
| Realms / Loot tables / Equipment / Consumables | 164 / 72 / 4 / 1 |

✅ **BUILT** — the full loop: tick simulation · the reaction engine with traits and essence ·
fabrication with the Genome and 44 live modifiers · the semantic presentation language · the
class combinator · the hit pipeline, 29 statuses, Resolve · 517 moves and 493 techniques · the
composed enemy roster with a live elite and boss · auto-combat · 20 professions with mastery,
synergies, opportunities, offline + the away report · Farming plots and the Agility course ·
20 stations · the Dark Forest with all eleven node kinds · Realm preparation and deep entry ·
composed loot, gold, extraction and death · nine consumed progression tracks · single-slot
save at schema v11.

🟡 **PARTIAL** — Species (3 thin) · Suffixes (10 of 50 expressed) · form acquisition (schematics
inert) · the Hideout meta-layer · course bonuses (displayed, unread) · realm tiers (carried,
unread) · 478 actors awaiting encounter wiring · the debug-console UI itself.

📐 **DESIGNED, NOT BUILT** — operations + **Overreach** + Anomalous/Exotic/Signature modifiers ·
signature reactions · consumable forms · the codex · profession tools (E6) · realm affixes and
the campsite workshop · combat-side hazards · relic materials and sealed uniques · unattended
Realm runs · the "gear at risk" toggle.

❓ **UNRESOLVED** — the economy (valuation, sinks, vendors at scale, respec pricing) · Species'
mechanical role · the Fighter hook · positioning · durability · the combat triangle (leaning
no) · casting-speed scaling · quantization bucket size · integrity budget strength · **and
balance, wholesale: every shipped number is breadth-not-balance until the playtest pass.**

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
| The 20-profession system | `docs/professions.md` |
| Damage, defence, lanes, thorns | `docs/damage-and-defense.md` |
| Statuses and Resolve | `docs/statuses.md` |
| The Move model | `docs/moves.md` |
| The Genome, modifiers, operations, Overreach | `docs/affixes.md` |
| The reward layer, table by table | `docs/loot.md` |
| Why a decision was made (and what was rejected) | `DECISIONS.md` |
| What's next, and where the last session stopped | `ROADMAP.md`, `HANDOFF.md` |
