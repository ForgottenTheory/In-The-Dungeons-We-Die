# Loot and drops — the reward layer

*The system that turns a Realm run into a pile of inputs. Written after M6's loot pass;
current with the shipped content.*

The rule the rest of this document serves, from **DECISIONS D28**:

> **Extraction converts risk into materials; fabrication converts materials into permanence.**

Realms drop **inputs** — anatomy, salvage, reagents, catalysts, techniques, schematics, coin.
They do not drop swords. What the Brute was swinging comes back as scrap iron and rawhide, and
the sword you make out of it is the reason to visit the bench.

---

## 1. One table shape, every source

There is exactly one loot type in the game, and everything that can pay a player out points at
it: enemies, gathering nodes, event chests, and profession actions.

```jsonc
{
  "id": "loot.actor.goblin_raider",
  "name": "Raider's Take",
  "tags": ["source:goblinoid"],          // joins the context for everything nested below

  "alwaysDrops":  [ { "itemId": "material.goblin_hide", "minQuantity": 1, "maxQuantity": 2 } ],
  "chanceDrops":  [ { "itemId": "material.goblin_scrap", "chance": 0.55 } ],
  "weightedDraws":[ { "picks": 1, "entries": [
                       { "tableId": "loot.shared.salvage_light", "weight": 48 },
                       { "itemId":  "material.iron_ore", "weight": 16 },
                       { "dropsNothing": true, "weight": 36 } ] } ],

  "gold": { "minAmount": 2, "maxAmount": 8, "chance": 0.75 }
}
```

**The three drop rules are separate named lists rather than one list with a kind field.** They
are genuinely different mechanics, and naming them in the JSON is what lets a table be read at a
glance instead of decoded:

| List | Rule |
|---|---|
| `alwaysDrops` | every entry drops (quantity still rolls its range) |
| `chanceDrops` | each entry rolls its own `chance`, independently of the others |
| `weightedDraws` | `picks` selections from a weighted set; `dropsNothing` is a real miss |

An entry sets **exactly one** of `itemId` / `tableId` / `dropsNothing`. Validation enforces it.

### Quantity, conditions, rarity

- `minQuantity` / `maxQuantity` — both default to 1, so a plain entry drops one.
- `when` — `{ minDepth, maxDepth, requiresTags, excludesTags }`. See §3.
- `rarity` — **only** for items that carry no `rarity:` tag of their own (techniques,
  schematics, consumables). Declaring one for a material is a validation error: the material's
  tag is the single source of truth, and the resolver reads it.

---

## 2. Composition, not duplication

An entry may point at another table. That is the whole extensibility story:

```
loot.actor.goblin_brute
  ├── loot.shared.salvage_heavy      ← shared by everything armoured
  ├── loot.shared.knowledge_rare     ← shared by everything that carries paperwork
  └── …
```

**A creature that does not exist yet is made lootable by one line of JSON.** Point its
`loot_table` at the shared tables that already ship; no code, no re-authored drop lists. That is
why `loot.template.beast_anatomy` exists ahead of any beast — it is the Dire Wolf shape from the
design brief, complete, validated and rolled in real play through the Ravaged Kill event, so it
is content the suite tests rather than a comment that has drifted.

### Enemies compose through three layers (D26)

A kill rolls **family + role + actor**, in that order, and the player sees one merged haul:

| Layer | Answers | Example |
|---|---|---|
| `family.goblin` → `loot.family.goblin` | what the body is made of | goblin hide, bone, blood |
| `role.brute` → `loot.role.brute` | what that archetype carries | heavy salvage, rawhide |
| `actor.goblin_brute` → `loot.actor.goblin_brute` | what makes *this one* worth seeking | chitin plate, a tool head |

Loot **accumulates** across the layers rather than overriding, unlike armour or Resolve: what a
body is made of and what it happens to be carrying are different claims, and both are true.

`loot.role.brute` is family-agnostic. An undead brute inherits it without inheriting goblin
biology — the same rule the rest of the enemy framework already follows.

---

## 3. Circumstances: `LootContext`

Rather than growing a field per question a designer might ask, the caller states the
circumstances and entries gate on them.

```
LootContext { Depth, Tier, Tags }
```

| Tag | Set by | Used for |
|---|---|---|
| `active` / `passive` | the profession path | the active-play gate (§4) |
| `in_realm`, the realm id, the realm's own tags | `GameRoot.LootCircumstances` | realm-specific entries |
| the enemy's identity tags | the resolved actor | `elite` / `boss` spoils (§5) |
| a rolled table's own `tags` | the resolver, as it descends | source identity (§2) |

**Depth 0 is the Hideout.** Nothing has to ask "am I in a Realm?" — a `minDepth` gate answers it.

---

## 4. Active play beats passive play, structurally

Every gathering table has the same shape:

```jsonc
"weightedDraws": [
  { "picks": 1, "entries": [ /* what any attempt might turn up */ ] },
  { "picks": 1, "when": { "requiresTags": ["active"] },
    "entries": [ /* what only somebody actually standing there would notice */ ] }
]
```

The second draw is not better odds — it is **unreachable** from the passive path at any rate.
This is the same structural trick the profession pass used for opportunities: a fact about the
content rather than a tuning number, and `LootEcosystemTests` fails a gathering table that does
not have it.

Inside a Realm, a gather node stacks three payouts: the action's own outputs, the action's drop
table (rolled with `active`), and the **node's** table on top. A node's table rolls only when the
attempt actually lands, so standing on a node is never a free faucet.

### Drop tables vs `bonusOutputs`

Both exist and the distinction is load-bearing:

| | `bonusOutputs` | `loot_table` |
|---|---|---|
| Means | *more of the same work* | *something else entirely* |
| Scales with mastery/performance | **yes** — a progression lever | no |
| Can express | flat chance, fixed quantity | weights, ranges, nesting, conditions |
| Shared across actions | no, copied per action | yes, one table per profession |

---

## 5. Elites and bosses, before there is an elite

Every family table nests one line:

```jsonc
{ "tableId": "loot.shared.rank_spoils" }
```

`loot.shared.rank_spoils` contains two draws, gated `requiresTags: ["elite"]` and
`["boss"]`. Nothing carries those tags today, so it pays nothing — and **the day an elite is
authored, giving it the tag is the entire change.** The context tags come from the resolved
actor's own identity tags, so combat never has to learn what a rank is.

`LootEcosystemTests.AnEliteTagUnlocksSpoilsAnOrdinaryEnemyCannotReach` proves the seam works
before there is anything to use it, which is the only way to stop the first elite ever authored
from shipping with no spoils.

Boss spoils lead with `material.relic_shard` — D28 sub-ruling 1: the chase item is a **material**
with an impossible profile, not a sword. The real relic materials are post-slice content; the
shard is the placeholder that keeps the shape honest.

---

## 6. Gold

Gold lives on `Inventory`, not in a separate purse. That one decision makes coin obey the
extraction risk model for free: gold picked up inside a Realm sits in the **unsecured** run
inventory and is lost on death; gold in the Stash is safe. Same bag, same rule, no second code
path to keep in step.

**Nothing spends it yet**, by design — there is no economy (NEEDS DESIGN). Save schema **v8**; a
v7 save loads with none, which is the state a character who has never been paid is already in.

Coin is a **Realm export**, the same way essence is: enemy tables, node tables and chests pay it,
profession drop tables do not. `LootEcosystemTests.NoProfessionDropTablePaysCoin` enforces it.

---

## 7. The shipped library

`game/data/loot_tables/` — 34 tables in four files.

| File | Contains |
|---|---|
| `shared.json` | the library everything nests: creature remains, the beast-anatomy template, light/heavy salvage, forest reagents, catalysts, fey essence, common/rare knowledge, martial/arcane techniques, rank/elite/boss spoils |
| `enemies.json` | `loot.family.goblin`, the three role tables, the three actor tables |
| `gathering.json` | one per gathering profession: forestry, mining, fishing, hunting, foraging, salvaging |
| `realm_dark_forest.json` | the five node tables and the two event tables |

**Zero new materials.** The 559-material library already had everything: hides, blood, bone,
glands, organs, carcasses, ores, salvage, reagents, catalysts, essence-bearing parts, and the
nine knowledge materials that serve as schematics.

### The Dark Forest, widened

Five new nodes so the tables have somewhere to live:

| Node | Depth | What |
|---|---|---|
| Iron Vein | 1 | Gather (`action.mine_iron`, level 1) — the design brief's worked example |
| Overturned Wagon | 1 | Gather (`action.search_wagon_wreck`, Salvaging 5) — a deliberate profession gate |
| Abandoned Hunting Blind | 2 | Gather (`action.snare_rabbit`, level 1) — carcasses, opened by Beast Lore |
| Hexer's Hollow | 2 | Combat — the Hexer finally has somewhere to live |
| A Ravaged Kill | 2 | Event — beast anatomy and fey essence |

---

## 8. The fences the tests hold

Every rule below is one somebody could break with a plausible JSON edit and never notice in
play, because bad loot looks exactly like bad luck. They live in
`tests/Loot/LootEcosystemTests.cs`.

| Rule | Why |
|---|---|
| **No table yields finished equipment** (D28) | an enemy that drops a sword makes the bench optional |
| **No profession drop table reaches essence** (D29.3) | essence is the Realm's export; extraction keeps the monopoly |
| **No profession drop table pays coin** | same reasoning, same rule |
| **Realm sources *do* reach essence** | the positive half — a monopoly on nothing is not a monopoly |
| **Every gathering table rewards active play with things passive cannot reach** | the §4 claim, made true rather than intended |
| **No hunting drop table recovers anatomy** | Hunting brings back the creature; Beast Lore opens it |
| **Everything behind a depth gate is uncommon or better** | gating a common material costs a trip and pays nothing |
| **An `elite` tag unlocks spoils an ordinary enemy cannot reach** | §5, before there is an elite |
| **Every loot source hands back something a system names** | the loot half of "no profession is a dead end" |
| **Every shipped source pays out** | a source that has quietly gone dry raises no error at all |
| **An enemy's loot composes from family + role + actor** | if it collapses to one table, every new enemy re-authors its drop list |

`tests/Loot/DarkForestHaulTests.cs` renders a full run source-by-source (`RenderAFullRun`) and
pins the Phase 3 bar: **≥12 different things per run, coin, most sources paying, and depth 2
returning more rare-or-better than depth 1.**

---

## 9. Where the code is

| File | Owns |
|---|---|
| `core/Loot/LootTableDefinition.cs` | the table, entry, draw, gold and condition shapes |
| `core/Loot/LootResolver.cs` | **every roll in the game** — pure, seeded, one readable method per rule |
| `core/Loot/LootContext.cs` | circumstances + the tags the code guarantees |
| `core/Loot/LootResult.cs` | the merged haul, and `DepositInto(bag)` |
| `core/Loot/LootReachability.cs` | walks the graph without rolling it — validation and audits |
| `core/Loot/LootRarity.cs` | the five steps, shared with the `rarity:` tag family |
| `core/Loot/LootTuning.cs` | two safety rails (nesting depth, picks per draw) — no balance knobs |
| `game/GameRoot.cs` | `LootCircumstances` / `GrantLoot` — application glue only |

Rolls draw from their own seeded stream (`0x1007ab1e`), so changing a combat or crafting roll
does not silently reshuffle what drops.

---

## 10. What is deliberately not here

- **Merchants, prices, valuation, sinks, multiple currencies.** Out of scope; gold simply exists.
- **Balance.** Every number is breadth-not-balance, the same standing decision the professions
  are under. `RealmTuning.RealmGatherPerformance` (0.5) is provisional and belongs to the same
  backlog.
- **Loot modifiers.** Beast Lore / Hunting influencing what anatomy is recovered enters as a
  **tag on the context** that unlocks a richer nested table — not as a quantity multiplier. That
  keeps every number in content and avoids a second scaling model. Nothing sets such a tag yet.
- **Relic materials and sealed uniques** (D28 sub-rulings 1–2) — post-slice content.
- **Form acquisition from schematics** (D29.2). The knowledge materials drop today; the
  `forms.json` acquisition field and the persisted known-forms list are still M6 work.
