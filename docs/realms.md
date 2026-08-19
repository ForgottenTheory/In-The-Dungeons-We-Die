# Realms

## 1. Vision

Realm Runs are the central active gameplay experience.

Production exploration should resemble For The King 2:

- Spatial movement
- Locations
- Routes
- Encounters
- Exploration
- Decisions

rather than a simple Slay-the-Spire-style node selection interface.

---

# 2. Realm Run Loop

Open Portal
→ Select Realm
→ Prepare Loadout
→ Enter
→ Explore
→ Gather
→ Fight
→ Discover
→ Reach Depth Decision
→ Extract OR Go Deeper

---

# 3. Realm Tiers

Realms contain escalating tiers.

Example:

Dark Forest Tier I

Dark Forest Tier II

Dark Forest Tier III

Higher tiers introduce:

- Stronger enemies
- New enemies
- New materials
- New events
- New affixes
- Better loot
- New bosses

---

# 4. Depth

Depth represents progression within the current run.

Depth increases danger.

Potential scaling:

- Enemy strength
- Elite frequency
- Hazard frequency
- Affix intensity
- Reward quality
- Rare material availability

---

# 5. Extraction

Extraction secures Realm gains.

Before extraction:

Loot is unsecured.

After extraction:

Eligible loot moves to persistent Stash.

Extraction opportunities should be valuable decisions rather than ubiquitous escape buttons.

---

# 6. Death

Death ends the Realm Run.

Default philosophy:

- Unsecured Realm loot is lost.
- Gear brought into the Realm may be at risk.
- Persistent progression remains.
- Safe Stash remains.

Players always retain access to weak starter equipment so failure cannot permanently brick progression.

Exact equipment-loss tuning is subject to testing.

---

# 7. Going Deeper

At major checkpoints:

EXTRACT

or

GO DEEPER

Going deeper provides:

- Better loot
- Better materials
- Increased Realm Knowledge opportunities
- Increased danger

Some mechanics may reward deliberately refusing extraction.

---

# 8. Realm Knowledge

Each Realm has persistent Knowledge.

Knowledge increases through:

- Exploration
- Enemy encounters
- Resources
- Events
- Successful extraction
- Boss encounters

Knowledge unlocks INFORMATION and OPTIONS.

Prefer this over raw universal damage bonuses.

Examples:

- Reveal enemy resistances
- Identify likely hazards
- Show resource-rich areas
- Reveal extraction routes
- Discover hidden locations
- Unlock portal targeting

---

# 9. Realm Affixes

Realms may contain modifiers.

Examples:

Undead Infested

Volatile

Toxic Bloom

Treasure Rich

Eternal Night

Predator's Domain

Shattered Paths

Arcane Storm

Affixes affect both danger and opportunity.

---

# 10. Initial Realm Concepts

## The Dark Forest

Themes:

- Fey
- Toxins
- Giant trees
- Bogs
- Predators
- Hidden crossings

**As shipped** (`game/data/realms/dark_forest.json`) — 15 locations across 2 depths:

| Depth | Nodes |
|---|---|
| 1 | Camp Entrance · Forest Path · Old Grove (Forestry) · **Iron Vein** (Mining) · Goblin Camp (Raider) · **Overturned Wagon** (Salvaging 5) · Crumbling Ruins (chest) · The Descent |
| 2 | Deep Path · **Abandoned Hunting Blind** (Hunting) · Dark Grove (Farming) · **Hexer's Hollow** (Hexer) · Brute Warren (Brute) · **A Ravaged Kill** (event) · Extraction Portal |

Every Gather and Event node carries a `loot_table`, which is the Realm's own layer *on top of*
whatever the profession action already pays — see `docs/loot.md`. A Gather node's table rolls
only when the attempt lands, so standing on a node is never a free faucet.

## Tiered Deserts

Progression:

Swept Desert
→ Sweltering Desert
→ Deep Desert

Threats:

- Heat
- Quicksand
- Scarcity

## Tundra

Progression:

Frozen Expanse
→ Ice Caves
→ Abyssal Caverns

## Wastelands

Progression:

Wasteland
→ Ashlands
→ Volcano

## Garden Maze

Themes:

- Puzzles
- Disorientation
- Hidden routes
- Strange flora

## City Of Infinite Alleys

Themes:

- Claustrophobia
- Ambushes
- Vertical routes
- Urban anomalies

---

# 11. Location Types

Possible locations:

- Combat
- Gathering
- Event
- Camp
- Shrine
- Merchant
- Elite
- Boss
- Extraction
- Hidden
- Hazard

Locations exist spatially in the Realm.

---

# 12. Campsite

Players carry limited access to a sanctuary/campsite system.

Potential functions:

- Cook
- Recover
- Repair
- Craft emergency supplies
- Prepare ammunition

Campcraft modifies effectiveness.

Campsite use is limited and strategically important.

---

# 13. Realm Preparation — BUILT (Phase 7, D39)

The Portal screen communicates known information and is where the run is decided.
See `docs/code-map.md` §10.16b for the implementation and GDD §11.7 for the design.

Player chooses:

- **Equipment** ✅ — all nine slots, equipped through the normal equip path. The screen does not
  keep its own copy of what is worn; `Equipment` is the loadout's gear half.
- **Consumables** ✅ — packed as a standing plan, transferred into the run bag at entry and
  unsecured from that moment. This is what makes supplies reachable inside a Realm at all.
- **Tools** 🟡 — profession *readiness* (which trades the realm asks for, and your levels).
  Worn tools need E6's tool slots, tool forms and yield pipeline.
- **Food** 📐 — no separate system; food is a consumable when consumable forms ship.
- **Ammunition** 📐 — no system behind it yet.

Knowledgeable preparation materially improves survival: the briefing is redacted by Realm
Knowledge, so the same screen shows a first-time visitor almost nothing and a veteran the
threats' weaknesses, the hazards, the rich workings and the ways out.

---

# 14. Passive Realm Runs

Long-term design supports passive Realm Runs.

Passive runs:

- Use automated behavior
- Carry real risk
- Produce lower expected rewards
- Should not outperform skilled active players

The same Domain rules should drive both modes.

Automation chooses actions rather than receiving fake calculated rewards.

Implementation may use aggregate simulation where mathematically safe.

---

# 15. Realm Design Goal

The defining Realm question is:

"I have valuable loot and I'm still alive. Do I leave, or do I push my luck?"

If Realm design stops producing that question, something has gone wrong.