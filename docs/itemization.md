# Itemization — Items, Properties, Instances, Equipment

> ⚠ **PARTLY SUPERSEDED.** Still accurate: §1's two-tier definition/instance model, §2's property
> model, §3's `EquipmentResolver` seam, §4 inventory, §5 gear loss.
> **Reversed:** §1's rule that any material whose properties diverge becomes a per-unit instance —
> materials **stack** and `ItemInstance` is equipment-only (DECISIONS **D20**).
> **Extended by** `docs/affixes.md` (genome, affixes, crafting operations, Overreach) and
> `docs/damage-and-defense.md` (what equipment properties actually drive).
>
> Status legend: **[impl]** implemented in code · **[arch]** architecture/seam exists, behavior deferred · **[planned]** design only.
>
> This document defines the item data model that supports emergent, recursive, property-based crafting (see `docs/emergent-item-system.md`).

---

## 1. Two-tier item model

There are two kinds of "item" in the game, and the distinction is mandatory (see `docs/architecture.md §8`).

### Item Definitions — static, shared, canonical **[impl]**

A *definition* describes what a kind of item **is**. It is loaded from JSON, shared by every copy, and never mutated at runtime.

- Raw materials (Iron Ore, Bloodmoss, Emberleaf, …) — `MaterialDefinition`.
- Equipment blueprints (Iron Sword, Leather Armor, …) — `EquipmentDefinition`.
- Consumables (Healing Salve, …) — `ConsumableDefinition`.

All item definitions expose the `IItemDefinition` contract: `Id`, `Name`, `ItemType`, `Stackable`, and `BaseProperties` (a `PropertySet`).

### Item Instances — unique, generated, per-item **[impl]**

An *instance* is a specific owned item whose properties may differ from its definition. Instances are used for:

- **All equipment** (every crafted/looted weapon or armor is an instance).
- ~~Any generated/processed material whose properties differ from its raw definition.~~ **Superseded — see the box below.**

> ### ⚠️ Emergent materials are stackable definitions, not instances
>
> **This reverses the original rule of this section.** `docs/emergent-item-system.md` §0
> Decision 3 supersedes it, and the emergent P1 implementation follows the new rule.
>
> A crafted material's state is quantized and hashed into a canonical **signature**, and a
> runtime `MaterialDefinition` is registered under it (`emergent.7f3a91c4`). Identical results
> therefore **stack**, like any other material.
>
> **Why the reversal.** Under the old rule, 40 units of the same emergent alloy were 40 unique
> objects — inventory, save file, UI and the future codex all break at that scale. Worse,
> variance would have produced *random stats on the same material* rather than a genuinely
> different one, which is a much poorer fit for a discovery game. Under the new rule two
> players (or the same player twice) who reach the same state get the same material with the
> same name, so discovery is shareable and worth talking about.
>
> **Consequence for code.** Emergent archetypes are registered into the *same*
> `DataStore<MaterialDefinition>` the authored library lives in, so they flow through every
> existing path — `Inventory` stacks, lookups, crafting inputs, loot — with no special-casing.
> Nothing needs to know whether an input was authored or generated.
>
> **`ItemInstance` remains, and remains correct, for equipment**, which is genuinely unique per
> copy. It is no longer used for materials.

`ItemInstance` carries:

| Field | Meaning |
|---|---|
| `InstanceId` | unique id (deterministic counter, `InstanceIdSource`) |
| `BaseDefinitionId` | the definition it derives from (identity/render fallback) |
| `ItemType` | Material / Weapon / Armor / Consumable |
| `DisplayName` | generated name, e.g. "Bloodmoss Iron Ingot" |
| `Quality` | Poor / Normal / Fine / Exceptional / Masterwork |
| `Properties` | the **derived** `PropertySet` (this is what makes it different) |
| `Provenance` | definition ids of the materials it was made from |
| `Traits` | generated named effects/traits (e.g. "blight", reserved for the reaction sim) |

**Rule of thumb (revised):** *materials* are always quantity-based stacks — authored ones under their authored id, emergent ones under their signature. *Equipment* is always an instance. The dividing line is no longer "do the properties differ from the definition" but "is this kind of thing unique per copy".

---

## 2. Property model — data-driven **[impl]**

Properties are stored in a `PropertySet`: a **string-keyed, case-insensitive map of `name → value`** on a **0–100 scale**. In JSON a material's properties are a **flat object** (`"properties": { "hardness": 40, "conductivity": 85 }`), matching `EquipmentDefinition`. Storage is string-keyed on purpose — adding a new property never requires touching code, only data/rules. Only the properties a material actually has are listed; **anything absent reads as 0** (`PropertySet` drops zero-valued entries, so "absent" and "zero" are identical). Known property names are provided as constants in `ItemProperties` (grouped below) and are what load-time validation checks against; unknown names or values outside 0–100 fail validation (`ContentValidator`).

**Physical** — `hardness` (resist cutting/crushing/deformation), `mass` (density/heaviness), `flexibility` (bend without breaking), `affinity` (willingness to form stable bonds with foreign materials — pivotal for crafting), `conductivity` (how readily electrical energy moves through it), `insulation` (how strongly it resists/contains electrical energy — **not** simply `100 − conductivity`).

**Processing** — `harvest_resistance` (difficulty to gather/extract intact — a fragile thing can still score high; replaces the old "Vitality" concept), `solubility` (how readily its useful properties transfer into solution), `instability` (unpredictability during crafting — high is *not* simply "bad"; it drives failure/partial/mutation/rare transformation).

**Reactive** — influences a material *introduces* into another: `heat`, `cold`, `charge` (electrical energy — kept separate from `conductivity`/`insulation`, which only move/contain it), `toxicity` (attacks biological systems), `growth`, `decay`, `corrosion` (attacks inorganic structure — kept separate from `toxicity`), `arcane` (raw supernatural influence — uncommon).

**Response** — how strongly a material *resists* an introduced influence: `heat_resistance`, `cold_resistance`. **[impl]** (Also `toxin_resistance`, a legacy/derived resistance carried by the Barkbound Iron demo — kept as a known property; its naming will be revisited when the reaction simulation fixes the final resistance vocabulary.)

Key distinctions that must stay intact: `heat`/`cold`/`charge` (influence introduced) vs `heat_resistance`/`cold_resistance`/`insulation` (resistance); `charge` (energy) vs `conductivity` (transmission); `toxicity` (attacks life) vs `corrosion` (attacks material); `affinity` (bonding) vs `instability` (unpredictability of the result).

A definition's `BaseProperties` are its intrinsic values; an instance's `Properties` are the derived result of crafting. `PropertySet` supports `Get`, `Has`, `With`, and `Combine(other, fn)` for derivation.

**Content organization:** the raw-material library (~470 definitions) lives in `game/data/materials/` grouped into category **array files** — `flora.json`, `fauna.json`, `fungal.json`, `minerals.json`, `environmental.json`, `elemental.json`, `processed.json` — each a JSON array. `DataStore.LoadDocuments` auto-detects array vs single-object files, so other content types stay one-per-file. The library is deliberately **mundane-majority** (oak, iron, salt, springwater) so the rare/elemental materials (Storm Core, Glacial Heart, Mana Prism) stand out by their property profiles. Note: material properties are on 0–100, while equipment base properties are still on a small legacy scale (~0–5); they don't interact yet (crafting produces material instances, not gear), and the `EquipmentResolver` seam will be recalibrated when crafted materials begin driving combat.

**Biome + rarity as tags (not schema).** Materials are authored with **biome variety in mind** (temperate forest, arctic, volcanic, desert, jungle/rainforest, swamp, mountain, cavern, necrotic, coastal) purely as a design lens for diverse property profiles — there is deliberately **no biome type or biome field**; biome is a brainstorming device, not a system. **Rarity** is likewise a free-form **tag** (`common` / `uncommon` / `rare` / `very_rare` / `exceptional`) — availability, not power (a `rare` material is unusual for its property *combination*, not for having every stat at 90). Every material carries exactly one rarity tag (enforced by test). Creatures/plants yield **multiple parts** as separate materials (Wolf → hide/fur/meat/bone/fang/blood; Thunderhorn → hide/horn/charged blood/storm gland/meat) only where the parts differ meaningfully. Property families recur across biomes with different profiles (Oak / Ironwood / Emberwood / Frostpine / Bogwillow fill the same "wood" role but behave differently) — never MMO tiering (Iron < Better Iron).

---

## 3. Equipment **[impl (data) / arch (combat)]**

`EquipmentDefinition` (`IItemDefinition`) adds:

- `Slot` — `Weapon` or `Armor` (more slots later).
- optional `Weapon` block — `BaseDamage`, `DamageType`, `BaseIntervalTicks`, `StaminaCost` (attribute scaling later).
- optional `Armor` block — `Armor` value + typed `Resistances` map.
- `BaseProperties` — intrinsic material-style properties.

At runtime a character holds an `Equipment` container (slot → equipped `ItemInstance`).

**Definition + instance → combat values** is resolved by `EquipmentResolver`:
- `ResolveWeapon(def, instance, unarmedFallback)` → `AttackProfile` (base damage/type/interval/stamina, adjusted by instance properties).
- `ResolveArmor(def, instance)` → `ArmorProfile` (flat armor + resistances, adjusted by instance properties).

The **property → stat effects** (e.g. high `mass` raises damage but slows the swing; `hardness` improves armor; `toxicity`/`charge`/`heat` eventually add on-hit effects and resistances) are intentionally a **small, extensible seam** right now — a couple of illustrative effects are implemented and clearly marked as the expansion point. Combat reads only the neutral `AttackProfile`/`ArmorProfile`, never equipment types directly, so the material→combat rules can grow without touching the encounter. **[arch]**

Two Iron Swords share the base weapon definition, but a *Bloodmoss Iron Sword* instance and a *Storm-Infused Iron Sword* instance resolve to different `AttackProfile`s because their derived properties differ.

---

## 4. Inventory **[impl]**

One `Inventory` (used for both the Stash and a run's unsecured bag) holds **both**:
- **Stacks** — `definitionId → quantity` for stackable raw materials/consumables (unchanged from before).
- **Instances** — a list of `ItemInstance` for equipment and generated materials.

Extraction (`RealmExtraction`) and, later, save both move stacks **and** instances.

---

## 5. Gear loss & recovery **[impl (rule) / arch (toggle)]**

- Death forfeits the **unsecured run inventory** (stacks + instances); equipped gear and the Stash are safe.
- A **starter loadout** (weak weapon + armor) is always available so a fresh or broke character can never be bricked. **[planned]**
- "Gear at risk on death" is designed as a switchable option but defaults **off**. **[arch]**

---

## 6. What is deferred

- The crafting **reaction simulation** that generates instance properties (see `docs/crafting.md`) — architecture only for now.
- Full material→combat formulas, on-hit status effects, resistances-in-combat.
- Additional equipment slots, durability, sockets, affix rolls, rarity-from-loot.
- Save persistence of instances/equipment (next phase).
