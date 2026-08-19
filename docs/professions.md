# Professions

The 20-profession pass. This document describes what **ships**, not what is planned; where
something is designed but not built it says so.

**Where things live:** definitions in `game/data/professions/` (one file each) · action ladders
in `game/data/profession_actions/` (one file each) · the Agility course in
`game/data/training_obstacles/` · the code in `core/Professions/`.

---

## 1. The shape of a profession

Every profession has the same anatomy, and there is exactly one code path behind all twenty:

| Piece | Where |
|---|---|
| XP → level (1–99) | `ProfessionLeveling` — 100·L per level, shared by all |
| Level, XP, per-action mastery | `ProfessionProgress` (runtime, persisted) |
| Mastery points → level (0–99) | `MasteryLeveling` — linear on purpose; see D40 |
| What mastery buys | `game/data/mastery/` → `MasteryBenefits` — six rungs, all content |
| What OTHER progress buys | `game/data/synergies/` → `ProfessionSynergies` — cross-profession and global, same six |
| The two folded into one answer | `ProfessionBenefits` — the single question the execute path asks |
| An action ladder | `ProfessionActionDefinition` — level gate, interval, inputs, outputs, bonus outputs, XP |
| Passive + active execution | `ProfessionSystem.Execute` — **one** path, so the two can never drift |
| Offline payout | `OfflineProgressCalculator` — the *same* execute path |
| The standing selection | `PassiveProfessionRunner` — Idle / Working / Waiting; survives running dry |
| What an absence earned | `AwayProgress` → `AwayReport`, worded by `Presentation/AwayReadout` |
| Balance constants | `ProfessionTuning` |

Two professions add a system of their own, because their loop genuinely is not a repeating
action: **Farming** (`FarmingPlots` — parallel, asynchronous) and **Agility** (`TrainingCourse`
— a configuration, not an activity). Nothing else needed one.

---

## 2. Passive, active, and offline

The rule the design turns on: **active mode must never mean clicking the same button more
often.**

**Passive** is automatic, reliable, lower-yield, and structurally free of rare outcomes — it
never rolls for opportunities at all. That is enforced by construction, not by a tuning number:
the discovery roll only happens on the active path (`ActionResolver.Resolve`, `isActive`).

**Offline is a first-class parallel path, not a courtesy.** Whatever action is selected when the
game closes keeps running while it is closed, at exactly the same rate as live passive play,
because it runs through the same `Execute`. Levelling never requires being at the keyboard.
Bounded at both ends: `MaxOfflineTicks` (12h) and `MaxOfflineCompletions`. Mastery earned partway
through an absence shortens the completions still to come, exactly as a live passive runner does
when it re-reads the interval each cycle.

**The selection is standing, and it waits (Phase 10).** An action that runs out of materials used
to stop and be forgotten — so "leave it running and come back" ended the first time a chest of ore
ran dry, silently, on exactly the sessions idle progress exists for. `PassiveProfessionRunner` now
keeps `SelectedActionId` through a stall, sits in `Waiting`, and resumes by itself when the
materials return. **Temporary problems wait; permanent ones refuse** — selecting an action you
cannot afford *yet* is a legal standing choice, while a level gate is still a refusal. Only Stop
clears it.

**Coming back is a summary, not a scroll.** `AwayProgress.Resolve` aggregates one absence —
completions, crops lifted, items merged per id, XP, mastery, and **which professions levelled** —
and `Presentation/AwayReadout` owns every word of it, so the console line and the panel cannot
describe the same absence differently. It aggregates and never resolves: every number came out of
`Execute`. It is also honest about the two different ways a payout gets cut short, in two
different sentences, because "you were gone too long" and "you ran out of oak" are different
problems and only one is worth acting on.

**What the absence cannot do.** No opportunities (structural — only the active path rolls), no
materials it did not have, and nothing past the cap. Offline's lower ceiling is a property of the
code, not a number someone can retune away.

**And therefore: no essence (D29.3, settled 2026-08-18).** Essence-bearing materials reach a
profession *only* as opportunity payloads, so the supernatural tier is unreachable while idle by
construction. See §5 for what that changed and why.

**Active** adds a timing score *and* the layer that actually makes it a different activity:

### Discover → Pursue / Ignore

An action may carry `opportunities[]`. An active attempt rolls for one (chance scaled by mastery
and performance); on a hit the attempt returns an **offer**, not a payout. The player reads the
prompt and decides.

```json
{ "id": "opportunity.rich_iron_vein", "name": "Rich Vein",
  "prompt": "The seam widens into denser ore. Following it means standing here a good deal longer.",
  "discoveryChance": 0.12, "extraIntervalTicks": 260, "riskWeight": 0.10,
  "outputs": [ { "itemId": "material.iron_ore", "quantity": 4 } ], "experience": 45 }
```

Pursuing costs real time on the shared `TickEngine` — inside a Realm that is time not spent
heading for the portal — and `riskWeight` can lose it. Declining is simply never pursuing;
the attempt's own yield already landed.

This is **one mechanism, twenty flavours**. Twenty minigames would have been twenty balance
surfaces and twenty UIs; the same three fields read completely differently because the prompt,
the cost and the payoff are content — the rich vein, the shape under the boat, the unattended
satchel, the unmarked side path, the ledge you have to commit to.

Core resolves the gamble instantly and deterministically; *when* the result arrives is the
client's business (`GameRoot.PursuePendingOpportunity`). That is why the time cost lives in the
client and the odds live in Core.

### Success chance

Two professions can miss outright — `successChance` below 1: **Hunting** (prey bolts) and
**Thieving** (the mark looks up). A miss still consumes inputs and still pays
`MissedAttemptXpFraction` of the XP, so a bad streak is slow rather than punishing, and it grants
no mastery. Everywhere else a swung pickaxe always produces ore; rolling for that would be
noise, not tension. A test pins that only those two can miss.

---

## 3. The roster

**Gathering (7)**

| Profession | What it does | Feeds |
|---|---|---|
| **Mining** | Ore, stone, crystal, salts, fuel | Smithing, Runecrafting, Artifice |
| **Forestry** | Logs, bark, sap, resin, fungi | Fletching, Artifice, Smithing, Leatherworking |
| **Fishing** | Location catch tables: food, oils, scales, skins | Cooking, Alchemy |
| **Farming** | Plants and fungi, wild *and* Hideout plots | Herblore, Cooking, Alchemy |
| **Hunting** | Finds and takes creatures → **a carcass** | Beast Lore |
| **Beast Lore** | Reads the carcass → meat, hide, bone, glands | Cooking, Leatherworking, Herblore |
| **Salvaging** | Wrecks, scrap heaps, ruins, ancient machinery | Smithing, Artifice, Tailoring |

**Processing (9)**

| Profession | What it does | Feeds |
|---|---|---|
| **Smithing** | Ore and scrap → ingots; the forged tool head | Artifice, Fletching, fabrication |
| **Herblore** | *Prepares* organics — dry, press, grind, steep, render | Alchemy, Cooking, Runecrafting |
| **Alchemy** | *Transforms* — leach, distil, concentrate, catalyse, transmute | Assay, Artifice, the bench |
| **Cooking** | Meals as configurations, not recipes | *(the player — awaiting consumables)* |
| **Leatherworking** | Hide → leather, **per creature** | fabrication, Fletching |
| **Tailoring** | Fibre → thread → cloth | fabrication, Fletching |
| **Fletching** | Shafts, staves, strings, heads, vanes | fabrication |
| **Artifice** | Glass, parchment, mortar, lenses, mechanisms | Cartography, Assay, fabrication |
| **Runecrafting** | Blanks → one rune per essence, plus the resonance catalyst | Artifice, the bench, Attune |

**Utility (4)**

| Profession | What it does | Feeds |
|---|---|---|
| **Thieving** | Marks with awareness, difficulty and unique tables | metals, keys, schematics |
| **Agility** | A configurable course; reach-gated gathering | standing utility bonuses |
| **Cartography** | Surveys and charts → **Realm Knowledge** | Salvaging, Realm preparation |
| **Assay** | Understanding, never power | gates the deepest crafts |

### The distinctions that must stay clear

- **Hunting** finds and takes the creature · **Beast Lore** reads what can be recovered from it.
- **Herblore** prepares organic material without changing what it is · **Alchemy** changes it.
- **Cartography** understands Realms · **Assay** understands materials and items.

---

## 4. The four professions worth reading twice

### Farming — the only parallel profession

Plots are the reason it needs `FarmingPlots` rather than a row in the passive runner. Plot count
unlocks with level (`FarmingTuning.PlotUnlockLevels`). A planting is an ordinary action whose
inputs are the seed and whose interval is the grow time; **the seed is taken at planting and the
harvest is prepaid** (`ProfessionSystem.CompletePrepaidAction`), so XP, mastery and bonus outputs
behave exactly as everywhere else. Every bed returns its own seed, so an established plot
sustains itself — the scarce resource is plot-time, not seeds. Growth runs on the world clock,
so crops finish while the game is closed; on load, remaining grow time is rebased onto the new
session's clock (`GameRoot.RebasePlantedCrops`).

### Beast Lore — the quick/full decision

Each carcass has a fast dressing and a long full harvest. Boar: dress at L8 for 100 ticks and the
meat, or full-harvest at L20 for 340 ticks and the meat, the hide, the tusk, the blood and the
bone. Inside a Realm that difference is time not spent extracting. Level is what makes the deeper
anatomy legible at all — the gland and the marrow are gated, not merely rarer. A test pins that
the thorough option always costs more time, returns more, and sits at a higher level.

### Agility — the course *is* the decision

Five slots (Balance · Climbing · Endurance · Recovery · Advanced), one obstacle each. Running a
lap grants XP; the obstacles you fitted grant `CourseBonusKeys` — travel speed, gathering speed,
extraction speed, hazard avoidance, opportunity safety — for as long as they stay fitted.
Choosing the climbing wall over the endurance run is choosing gathering speed over travel speed
and living with it. A different *shape* of decision from Discover → Pursue: made once, not
moment to moment. Agility's four actions are reach-gated gathering — material nobody else can
stand next to.

### Assay — comprehension, never power

Assay level drives `AssayLens`, which decides how much of a material's reading is legible:

| Level | Depth | What opens |
|---|---|---|
| 1 | Superficial | name and descriptor — "Hot Metal", then `???` |
| 10 | Composition | the leading-property strip |
| 25 | Reactive | bonding, receptiveness, wear |
| 45 | Traits | traits and their drawbacks |
| 65 | Essence | essence load, resonance, vessel strain |
| 85 | Potential | potential pressure, slot fit, modifier eligibility |

The underlying reading is computed identically at every level — a test pins that. A high-Assay
player is not holding a better material, they are finally reading the one they had. At full depth
the lens defers to `SemanticFormat.Material` outright so the two voices cannot drift.

Its material output is the **property dossier**, and it is not a trophy: Alchemy's transmutation,
Runecrafting's resonance catalyst and Artifice's clockwork core all require one. You cannot do
the hard work on a compound you have not bothered to understand.

---

## 5. Interconnection

`Gather → Process → Manufacture → Prepare → Realm → Extract → Progress`

The ecosystem is enforced by test, not by intent (`ProfessionEcosystemTests`):

- every **Processing** profession consumes something another profession makes;
- no profession is a **dead end** — its output is wanted by another action, the crafting bench,
  or a fabrication slot;
- Hunting produces carcasses and **only** Beast Lore opens them;
- **only** Cartography teaches Realm Knowledge;
- every plantable seed has a wild source, and every bed reseeds itself;
- every opportunity out-pays the action that surfaced it.

Named chains that exist today:

```
Mining → Smithing → ingots → Artifice/Fletching → fabrication
Forestry → Fletching (staves, shafts) · Artifice (hafts) · Leatherworking (bark tanning)
Hunting → Beast Lore → Cooking · Leatherworking · Herblore
Farming → Herblore → Alchemy → Assay
Salvaging → Smithing (scrap) · Artifice (broken mechanisms come back working)
Cartography → survey chart → Salvaging finds the ruin worth digging
Assay → property dossier → Alchemy · Runecrafting · Artifice
Runecrafting → rune → Artifice's clockwork core, and the bench as a reagent
Artifice → glass, parchment, lenses → Cartography and Assay
```

### Synergies — the chains pay twice (Phase 10, D41)

A **synergy** is progress in one place paying off in another: `game/data/synergies/` holds 13
cross-profession rows and 2 global ones, and they feed the *same six quantities* the mastery
ladder does (`ProfessionBenefitKind`), through the same `ProfessionBenefits` seam. Adding them
changed no line of `ActionResolver` or `ProfessionSystem`.

**A synergy must follow a chain that already exists above.** Mining pays Smithing preservation
because ore actually walks from one to the other; a synergy between two professions that never
touch would be a number pretending to be a relationship, and the player would learn to read the
table instead of the game. Source and target must differ — a profession paying for its own level
is a mastery rung with extra steps, and a self-amplifying one, so the validator refuses it.

A row with **no source** reads the player's **total** profession level across the roster: that is
the global/account passive, earned by breadth rather than granted as a constant. Both global rows
unlock late (totals of 200 and 400) and pay little per level, because breadth should not out-earn
mastery of the action actually being performed.

### Essence is extraction's export (D29.3, settled 2026-08-18)

**A profession may reach an essence-bearing material only through an opportunity payload.** Not
through its outputs, not through its bonus outputs, and not through its drop table — and since
only the active path rolls opportunities, *an absence can never bank essence*. That is a fact
about the code rather than a probability someone can retune, which is what makes "the
supernatural tier is what you go to a Realm for" hold under idle progression.

This replaced an eleven-id allowlist of grandfathered faucets. Two of them were guaranteed
outputs on passive rungs, which meant a 12-hour absence banked thousands of essence-bearing logs
with no Realm exposure at all — and Phase 10's auto-repeat made that unattended too. The seven
rungs involved kept their content and gained a decision:

| Rung | The passive yield | The essence, now |
|---|---|---|
| Hunt Eels (Fishing 20) | eel skin | `opportunity.live_eel` — the gland |
| Harvest Storm Kelp (34) | storm kelp | `opportunity.charged_frond` |
| Mine Emberite (45) | emberite ore | `opportunity.unstable_ember_pocket` — shard + core |
| Mine Frostiron (45) | frostiron ore | `opportunity.rimed_seam` |
| Harvest Emberwood (50) | ember sap, cinderroot | `opportunity.emberwood_heartwood` — log + bark |
| Harvest Livingbark (62) | spirit bark | `opportunity.heartwood_seam` — both logs |
| Cut a Cultist's Purse (58) | native gold | the Reliquary |

The fiction improved rather than survived: a tree gives up sap and bark to anyone who turns up,
and its burning heartwood only to somebody who stayed and cut for it.

**No fake resources.** Profession outputs use the existing material library wherever one fits.
Thieving deliberately produces no currency: there is no economy yet, and a coin nothing spends
would be exactly the invented resource the design forbids — so a thief walks off with precious
metal, gems, a key, or somebody's paperwork.

---

## 6. Content counts

**20 professions · 348 actions · 36 opportunities · 12 obstacles · 15 synergies · 1448 materials**
(79 added by the P4 pass). Save schema **v11** — Phase 10 added no field: `PassiveActionId` now
carries the standing selection rather than the running action, which is the same key meaning the
same thing slightly more honestly. `ProfessionEcosystemTests.TheRosterMeetsItsStatedScale` pins
these, so the numbers in this table cannot quietly drift.

---

## 7. Known gaps

- **Cooking is the one documented dead end.** A meal's consumer is the player, through consumable
  forms that have not shipped. Named explicitly in `ProfessionEcosystemTests` rather than hidden.
- **Profession tools** (two worn slots) are E6. Artifice and Smithing make the *components*
  now — deliberately ahead of the slots.
- **Bow and projectile forms** land with form acquisition; Fletching makes the parts today.
- **Course bonuses are declared, not consumed.** `CourseBonusKeys` values are aggregated and
  displayed, but Realm travel, hazards and extraction do not read them yet. They are **not** the
  same thing as synergies: a synergy pays into the six profession-benefit quantities, while a
  course bonus is standing utility inside a Realm.
- **Cartography's knowledge gains are all `realm.dark_forest`** — the only realm that exists.
- **Synergy rates and both global unlock thresholds are placeholders**, like every other number
  in this document.
- **Everything here is breadth, not balance.** Intervals, XP, chances and level gates are
  provisional and belong to the balance pass.
