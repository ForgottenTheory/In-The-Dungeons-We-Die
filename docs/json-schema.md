# JSON Data Specification

## 1. Purpose

Game definitions should be externalized into JSON wherever practical.

The goal is:

- Easy balancing
- Easy content creation
- Reduced loader boilerplate
- Mod-friendly foundations
- AI-assisted content generation
- Clear separation between definitions and runtime state

Use System.Text.Json.

---

# 2. ID Convention

Use stable namespaced string IDs.

Examples:

actor.goblin_raider
weapon.rusty_sword
material.oak_bark
realm.dark_forest
profession.forestry
species.undead
class.bastion
prefix.pyromaniac
suffix.exploding_kneecaps

Never use display names as persistent IDs.

---

# 3. Base Definition

Definitions should generally expose:

Id
Name

Optional:

Description
Tags

Example:

{
  "id": "material.oak_bark",
  "name": "Oak Bark",
  "description": "Bark harvested from mature oak trees.",
  "tags": ["material", "plant", "wood", "bark"]
}

---

# 4. ActorData

Example:

{
  "id": "actor.goblin_raider",
  "name": "Goblin Raider",

  "attributes": {
    "strength": 7,
    "dexterity": 10,
    "intelligence": 4,
    "constitution": 6,
    "wisdom": 4,
    "endurance": 7,
    "luck": 5
  },

  "resources": {
    "health": 30,
    "mana": 0,
    "stamina": 40
  },

  "abilities": [
    "ability.goblin_slash"
  ],

  "lootTableId": "loot.goblin_basic"
}

---

# 5. ItemData

Common fields:

{
  "id": "item.example",
  "name": "Example Item",
  "itemType": "Material",
  "rarity": "Normal",
  "stackSize": 100,
  "tags": []
}

Runtime item state does not belong here.

---

# 6. WeaponData

{
  "id": "weapon.rusty_sword",
  "name": "Rusty Sword",
  "itemType": "Weapon",
  "weaponType": "Sword",
  "damageType": "Slashing",
  "baseDamage": 6,
  "baseActionTicks": 30,
  "attributeScaling": {
    "strength": 0.8,
    "dexterity": 0.2
  },
  "tags": [
    "weapon",
    "sword",
    "metal"
  ]
}

---

# 7. MaterialData

`properties` is a **flat name→value object** on a 0–100 scale (matching EquipmentData); list only the properties the material actually has (absent = 0). Files under `game/data/materials/` are grouped into category **arrays** (`flora.json`, `fauna.json`, …), auto-detected by the loader.

{
  "id": "material.copper_ore",
  "name": "Copper Ore",
  "tags": ["raw", "metal", "ore", "mineral"],
  "properties": {
    "hardness": 40,
    "mass": 50,
    "conductivity": 85,
    "insulation": 5,
    "heat_resistance": 55,
    "harvest_resistance": 45
  }
}

Property names are validated at load against `ItemProperties`; values must be 0–100 (`ContentValidator`). See `docs/itemization.md §2` for the property meanings. Material properties are the intrinsic starting point crafting derives from.

---

# 8. ProfessionData

{
  "id": "profession.forestry",
  "name": "Forestry",
  "category": "Gathering",
  "primaryAttributes": [
    "strength",
    "dexterity"
  ]
}

Profession progression itself belongs in save/runtime state.

---

# 9. ProfessionActionData

{
  "id": "profession_action.chop_oak",
  "professionId": "profession.forestry",
  "name": "Chop Oak",
  "requiredLevel": 1,
  "baseIntervalTicks": 100,
  "outputs": [
    {
      "itemId": "material.oak_log",
      "quantity": 1
    }
  ],
  "bonusOutputs": [
    {
      "itemId": "material.oak_bark",
      "chance": 0.2,
      "quantity": 1
    }
  ],
  "experience": 10
}

---

# 10. RecipeData

{
  "id": "recipe.iron_ingot",
  "name": "Iron Ingot",
  "professionId": "profession.smithing",
  "requiredLevel": 1,
  "baseIntervalTicks": 120,

  "inputs": [
    {
      "itemId": "material.iron_ore",
      "quantity": 2
    }
  ],

  "outputs": [
    {
      "itemId": "material.iron_ingot",
      "quantity": 1
    }
  ]
}

---

# 11. Crafting Interaction Data

Some interactions should be data-driven.

Example:

{
  "id": "interaction.barkbound_iron",
  "requiredTags": [
    "metal:iron",
    "plant:oak"
  ],
  "professionRequirements": [
    {
      "professionId": "profession.herblore",
      "level": 5
    }
  ],
  "resultProperties": [
    {
      "property": "toxin_resistance",
      "value": 0.05
    }
  ],
  "discoveryId": "discovery.barkbound_iron"
}

Not every complex crafting rule must be forced into JSON.

Use code for genuine behavior.

Use data for content/configuration.

---

# 12. SpeciesData

{
  "id": "species.undead",
  "name": "Undead",
  "description": "A creature that has inconveniently refused to remain dead.",

  "tags": [
    "undead"
  ],

  "modifiers": [
    {
      "type": "Immunity",
      "target": "Poison"
    }
  ],

  "abilities": []
}

---

# 13. BaseClassData

{
  "id": "class.bastion",
  "name": "Bastion",
  "resourceType": "Stamina",

  "startingAbilities": [
    "ability.guard"
  ],

  "tags": [
    "martial",
    "defensive",
    "heavy_armor"
  ]
}

Exact final class roster belongs in classes.md.

---

# 14. PrefixData

{
  "id": "prefix.pyromaniac",
  "name": "Pyromaniac",

  "ruleIds": [
    "rule.physical_fire_conversion"
  ],

  "tags": [
    "fire",
    "conversion"
  ]
}

---

# 15. SuffixData

{
  "id": "suffix.exploding_kneecaps",
  "name": "Of The Exploding Kneecaps",

  "ruleIds": [
    "rule.exploding_kneecaps"
  ],

  "tags": [
    "critical",
    "explosion"
  ]
}

Complex suffix behavior should be implemented by reusable rule handlers.

Do not attempt to express arbitrary game logic through giant JSON expression languages.

---

# 16. AbilityData

{
  "id": "ability.goblin_heavy_swing",
  "name": "Heavy Swing",

  "targeting": "SingleEnemy",

  "timing": {
    "telegraphTicks": 20,
    "windupTicks": 30,
    "recoveryTicks": 30
  },

  "costs": {
    "stamina": 10
  },

  "effects": [
    {
      "type": "Damage",
      "damageType": "Crushing",
      "baseValue": 15
    }
  ]
}

---

# 17. RealmData

{
  "id": "realm.dark_forest",
  "name": "The Dark Forest",

  "description": "An ancient forest containing hostile creatures and stranger things.",

  "supportedTiers": [
    1,
    2,
    3
  ],

  "tags": [
    "forest",
    "fey",
    "toxin"
  ],

  "resourcePools": [
    "resource_pool.dark_forest_basic"
  ],

  "encounterPools": [
    "encounter_pool.dark_forest_tier1"
  ]
}

---

# 18. RealmAffixData

{
  "id": "realm_affix.undead_infested",
  "name": "Undead Infested",

  "description": "Undead encounters are substantially more common.",

  "tags": [
    "undead"
  ],

  "modifiers": []
}

---

# 19. LootTableData

**BUILT** (M6). `game/data/loot_tables/*.json`. Full system doc: `docs/loot.md`.

One shape for every loot source in the game. Three drop rules as separate named lists, because
they are separate mechanics; an entry sets **exactly one** of `itemId` / `tableId` /
`dropsNothing`.

```jsonc
{
  "id": "loot.actor.goblin_raider",
  "name": "Raider's Take",              // player-facing; the log prints it
  "tags": ["source:goblinoid"],         // joins the roll context for everything nested below

  "alwaysDrops": [                      // every entry drops
    { "itemId": "material.goblin_hide", "minQuantity": 1, "maxQuantity": 2 }
  ],

  "chanceDrops": [                      // each entry rolls its own chance, independently
    { "itemId": "material.goblin_scrap", "chance": 0.55, "maxQuantity": 3 }
  ],

  "weightedDraws": [                    // `picks` selections from a weighted set
    {
      "picks": 1,
      "when": { "requiresTags": ["active"] },     // optional, same shape as an entry's
      "entries": [
        { "tableId": "loot.shared.salvage_light", "weight": 48 },   // nested/shared table
        { "itemId": "material.iron_ore", "weight": 16 },
        { "itemId": "technique.manual_feint", "weight": 3, "rarity": "Rare" },
        { "dropsNothing": true, "weight": 33 }                      // a real miss
      ]
    }
  ],

  "gold": { "minAmount": 2, "maxAmount": 8, "chance": 0.75 }
}
```

**Entry fields.** `itemId` (material / consumable / technique) · `tableId` (nest a table) ·
`dropsNothing` · `weight` (draws only) · `chance` (chanceDrops only) · `minQuantity` /
`maxQuantity` (both default 1) · `when` · `rarity`.

**`when`** — `{ "minDepth": 2, "maxDepth": 3, "requiresTags": [...], "excludesTags": [...] }`.
Depth 0 is the Hideout. Guaranteed context tags: `active`, `passive`, `in_realm`, the realm id
and its tags, the enemy's identity tags (`elite`, `boss`, `family:*`), and each rolled table's
own `tags`.

**`rarity`** — `Common` · `Uncommon` · `Rare` · `VeryRare` · `Exceptional`. **Only** for items
with no `rarity:` tag of their own; declaring one for a material is a validation error, because
the material's tag is the single source of truth and the resolver reads it.

**Who points at a table** — `actors/`, `enemy_families/` and `enemy_roles/` via `loot_table`
(all three roll and merge); `realms/` locations via `loot_table` (Gather: on top of the action,
only when it lands — Event: the node itself); `profession_actions/` via `loot_table`.

**Validated** — unknown items/tables, cycles, empty tables, inverted ranges, out-of-range
chances, zero weights inside a draw, draws that are nothing but misses, self-contradicting
conditions, and restated rarity.

**Note:** there is no `currency.coin` item. Gold is not an item — it is `gold` on the table and
`Inventory.Gold` at runtime, so it obeys the extraction risk model with the rest of the bag.

---

# 20. DataStore<T>

Expected capabilities:

DataStore<T>
- Load
- Reload
- GetById
- TryGetById
- GetAll
- Validate duplicate IDs

Definitions should implement or expose an ID contract.

Example:

IDataDefinition
{
    string Id { get; }
}

---

# 21. Validation

Development startup should identify:

- Duplicate IDs
- Missing referenced IDs
- Invalid ranges
- Invalid required values

Fail loudly in development for broken content.

A goblin referencing `weapon.sowrd` should not survive quietly until three hours into a Realm Run.

---

# 22. Save Data

Save files are separate from definition files.

Save data stores:

- IDs
- Quantities
- Runtime values
- Progression
- Discoveries
- Generated item state

Do not serialize the entire definition database into every save.

---

# 23. Future Definition Types

Expected:

- StatusEffectData
- ArmorData
- ConsumableData
- EnemyData
- GatheringNodeData
- RecipeData
- MaterialData
- LootTableData
- SpeciesData
- BaseClassData
- PrefixData
- SuffixData
- ProfessionData
- RealmData
- RealmAffixData
- EventData
- EncounterData
- AbilityData
- ItemAffixData

Add them when required.

Do not implement empty frameworks merely because they appear on this list.