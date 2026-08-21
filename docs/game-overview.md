# Game Overview — the whole game, plainly

> **What this is.** The game explained for someone who did not build it: what you do, why you
> care, how the systems feed each other, and honestly how far each one actually got. Read this
> first; follow the links at the bottom when you need rules and formulas.
>
> **What this is not.** Not a specification — `docs/GDD.md` is the authoritative detailed
> reference, and the per-system docs hold the math. Nothing here overrides them.
>
> Last synced with the repo: **2026-08-21** (1,011 passing tests, 0 build warnings, save v14).
>
> The crafting sections describe the **identity system** (DECISIONS D42–D54) — the property
> model that shipped first was replaced across migration Phases 1–7 and deleted whole.
> Design of record: `docs/identity-foundation.md`; the stack map: `docs/crafting-overview.md`.

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

1. **Crafting with no recipes, anywhere.** Ten universal verbs work any material's identities —
   reveal what sleeps in it, transfer it, deepen it, overfill it and gamble. Every result is a
   real, named, stackable material — including ones nobody has seen.
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

At the Forge bench you **Transfer** the stormglass's Storm identity into an iron ingot, the
preview stating the whole deal before you click — what settles in, what the work costs the
metal, and the odds only if you chose them. You mint a material that did not exist five minutes
ago: **Storm Iron**. First discovery. It stacks like anything else.

At the forge you feed it into a **Longsword** — the edge bites, the whole blade swings by its
heft, and the preview shows the projection the mint will draw from: the guaranteed Storm floor,
the scored table of what might join it ("Likely — On Hit: Jolt"), the Signature odds. You forge
it and the sentences land: *"While Worn: +5% Charge Resistance"*, *"On Critical: inflict Shock
(2.4)"*. You engineered the odds, then you played them.

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
survives. The levels, the mastery, the knowledge, the discovery of Storm Iron: all safe.
You know exactly what to do differently, and it starts at the bench.

---

# 3. The core loop ✅

```
                ┌────────────────── HIDEOUT ──────────────────┐
                │  Train professions — active, passive,        │
                │    or offline while the game is closed   ✅  │
                │  Gather and process raw materials        ✅  │
                │  Work identities at the verb bench       ✅  │
                │  Forge them into equipment               ✅  │
                │  Sentences mint from the scored table    ✅  │
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

# 7. Materials and the bench ✅ — the identity system

## 7.1 The ingredient library

**1,448 materials**, deliberately **mundane-majority** (oak, iron, salt, spring water) so the
strange things stand out by what they *are*, not by tier. A material is: **capacity** (how many
identities it safely holds, 1–4) · **active identities** (ranked doors into effect families —
only **53** materials ship any: motes, essences, hearts, runes, cores) · **latents** (present
but asleep until revealed — ~413 carriers, discovery gameplay) · **base stats** (Heft, Bite,
Toughness, Give on 0–10 — the mundane physical floor, structural stock only) · a **signature
profile** (its crafting personality — 47 curated). **There is no "Iron < Better Iron"**
anywhere, and every raw material has a gathering source — enforced exactly, by test.

The **identity roster is 24 and closed** (Dense, Vital, Ember, Frost, Storm, Warded, Arcane…) —
a new identity is a design decision with a pinned test, never a content addition.

## 7.2 The identity bench — ten verbs, no recipes

Everything the bench does is one of **ten verbs**, shipped as **53 content actions across 11
professions** at their own stations: **Process** (ore→ingot; the output's innate identities
activate — preparation *is* activation) · **Fuse** · **Reveal** (wake a latent) · **Transfer** ·
**Develop** (deepen a rank) · **Extract** (pull an identity onto a carrier) · **Displace** ·
**Refine** (workmanship) · **Restore** · **Expand** (capacity, rare).

**Risk only lives where you chose it.** Refusals are deterministic and previewable; the only
dice are fracture (working an **overfilled** material — Unstable/Volatile, the §10.3 ladder)
and destruction (deep work at **Fragile** condition), both shown as odds before the click, both
shaved by per-action mastery (capped — skill narrows variance, never deletes it). **The bench
trains**: every gated action pays profession XP and mastery.

Worked materials are **fingerprinted and registered** — the same state always mints the same
stackable material with the same name ("Vital Oakbound Iron Ingot"), and plain smelted ore
lands on authored `material.iron_ingot`, never an emergent twin.

## 7.3 The language you see it in ✅

The player reads identities, rung words (*improved / advanced / build-changing* — never
numerals), Condition and Stability ladder words, workmanship words, and effect **sentences** —
never engine ids or weights. The draw table reads as likelihood words (Likely / Possible / A
long shot); exact scores live behind one **Advanced** toggle (D30/D53,
`docs/presentation-architecture.md`).

---

# 8. Assay — buying comprehension, never power ✅

The reading is computed identically at every level; levelling only removes `???`. Always
visible: a material's active identities and its overfill word (chosen risk is never hidden).
The ladder then opens **Vessel** (slots, condition, workmanship) at 10 → **Latency**
("something sleeps in this") at 25 → **Latent names** at 45 → **Leanings** (the profile as
words — "leans toward blocking work · favors Warding") at 65 → **Potential** ("on gear,
promises Vitality") at 85. Themes are never visible at any level.

---

# 9. The identity forge — materials become gear ✅

The terminal boundary, with the same fairness guarantee as the bench: the **pre-commit preview
is the projection the mint draws from** — the scored candidate table the player sees IS the
draw distribution.

```
Form + a material in each named slot
  → compose: identity union, form cap, dormancy (D51) + base delivery (D46)
  → the item-effect pipeline (D50)
  → a minted item carrying its sentences
```

**23 forms across all nine equipment slots (16 weapons), every one identity-forgeable**, each
authored to disagree with the others: the longsword's edge **bites**, the maul's head hits by
**heft** (no edge, no Bite — D52's rule), the bow's limb springs by **Give** (iron limbs are
still a bad bow), armour reads **Toughness**, gauntlets add the glove's **Give** (supple beats
plated), and the Focus and Ring read nothing at all — pure identity vessels. Identities beyond
the form's cap go **Dormant**: recorded, inert, never deleted. ~120 weapon names (Falchion,
Scimitar, Spatha…) ride the forms as cosmetic variants picked deterministically from the
derived definition id — the preview promises the noun the forge mints.

Weapons carry their own moves (re-equipping literally reconfigures your moveset); the worn
armour total is the sum of the loadout.

📐 Not built: consumable forms · **form acquisition** — schematic items already drop, but today
every form is available from the first minute. The one progression track nothing reads, and the
roll-call test names it. 🟡

---

# 10. The item-effect pipeline — one generator, three categories ✅

Every minted item's effects come from one pipeline (D50), kept apart on the item so the promise,
the roll and the rarity never blur:

```
  FLOOR       guaranteed — each expressed identity's promised expression, deepened by rank,
              deterministic to the last digit ("Guaranteed: While Worn: +9 Max Health")
+ GENERATED   1–3 sentences drawn from the scored table the preview shows — trigger →
              behavior → payload over the shared vocabulary (22 × 11 × 29), biased by the
              materials' profiles and the form's lean, never a recipe
+ SIGNATURE   the earned special layer: 1–2 coherent sentences, odds from theme resonance,
              quality and overfill — earned, never owed (ceiling 90%)
+ DRAWBACK    the price of minting from Volatile stock: an ailment aimed at the wearer
```

Worn, the sentences recompile deterministically into stat grants, trigger rules, gauges and
move modifiers — the same seams character components use, so they swap with the gear.

📐 The designed endgame (E7) still waits: **Operations** (targeted gambling on an existing
item) and **Overreach** (the escalating-Ruin casino, Anomalous effects) — now to be built over
the sentence vocabulary rather than the retired affix layer.

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
materials** — boss drops with impossible identity profiles that feed the crafting machinery
instead of bypassing it (the boss already drops the placeholder shards 📐).

Structural, not numeric: **active gathering reaches entries passive cannot** (a condition, not
better odds) · elite/boss spoils fire on the rank tag · a material's rarity comes from its own
tag, stated once · active-identity stock never sits in a profession drop table, and no
profession table pays coin — **coin and the supernatural tier are Realm exports**, both held
by test (D29.3, identity edition).

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
| Materials (53 active-identity) / Identities | 1,448 / 24 |
| Triggers / Behaviors / Payloads / Themes / Profiles | 22 / 11 / 29 / 16 / 47 |
| Verb actions / Byproducts / Forms | 53 / 4 / 23 |
| Moves / Move modifiers / Techniques | 517 / 11 / 493 |
| Statuses / Modifier keys | 29 / 60 |
| Bases / Prefixes / Suffixes / Species / Name formats | 15 / 25 / 50 / 3 / 9 |
| Professions / Actions / Opportunities / Obstacles | 20 / 348 / 36 / 12 |
| Mastery rungs / Synergies / Stations | 6 / 15 / 20 |
| Enemy families / roles / brains / actors | 26 / 7 / 7 / 483 |
| Auto-combat brains | 3 |
| Realms / Loot tables / Equipment / Consumables | 164 / 72 / 4 / 1 |

✅ **BUILT** — the full loop: tick simulation · the identity bench (ten verbs, trained) · the
identity forge and the item-effect pipeline · the player-language presentation layer · the
class combinator · the hit pipeline, 29 statuses, Resolve · 517 moves and 493 techniques · the
composed enemy roster with a live elite and boss · auto-combat · 20 professions with mastery,
synergies, opportunities, offline + the away report · Farming plots and the Agility course ·
20 stations · the Dark Forest with all eleven node kinds · Realm preparation and deep entry ·
composed loot, gold, extraction and death · nine consumed progression tracks · single-slot
save at schema v14.

🟡 **PARTIAL** — Species (3 thin) · Suffixes (10 of 50 expressed) · form acquisition (schematics
inert) · the Hideout meta-layer · course bonuses (displayed, unread) · realm tiers (carried,
unread) · 478 actors awaiting encounter wiring · the debug-console UI itself.

📐 **DESIGNED, NOT BUILT** — operations + **Overreach** + Anomalous effects (E7, over the
sentence vocabulary) · detonate/spread/bloom behaviors · named identity evolution · consumable
forms · the codex · profession tools (E6) · realm affixes and the campsite workshop ·
combat-side hazards · relic materials and sealed uniques · unattended Realm runs · the "gear
at risk" toggle.

❓ **UNRESOLVED** — the economy (valuation, sinks, vendors at scale, respec pricing) · Species'
mechanical role · the Fighter hook · positioning · durability · the combat triangle (leaning
no) · casting-speed scaling · **and balance, wholesale: every shipped number — the identity
identity system's especially — is breadth-not-balance until the playtest pass.**

---

## Where to go next

| You want | Read |
|---|---|
| Full design detail on anything above | `docs/GDD.md` |
| How the code is laid out, and where to change things | `docs/code-map.md` |
| The experience arc, stage by stage | `docs/how-it-plays.md` |
| The presentation rule and its enforcement | `docs/presentation-architecture.md` |
| The whole crafting stack, in one place | `docs/crafting-overview.md` |
| The crafting foundation and the migration record | `docs/identity-foundation.md` |
| The 20-profession system | `docs/professions.md` |
| Damage, defence, lanes, thorns | `docs/damage-and-defense.md` |
| Statuses and Resolve | `docs/statuses.md` |
| The Move model | `docs/moves.md` |
| The reward layer, table by table | `docs/loot.md` |
| Why a decision was made (and what was rejected) | `DECISIONS.md` |
| What's next, and where the last session stopped | `ROADMAP.md`, `HANDOFF.md` |
