# Itemization — Items, Properties, Instances, Equipment

> Status legend: **[impl]** implemented in code · **[arch]** architecture/seam exists, behavior deferred · **[planned]** design only.
>
> This document supersedes the earlier stray content in this file. It defines the item data model that supports emergent, recursive, property-based crafting (see `docs/crafting.md`).

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
- **Any generated/processed material** whose properties differ from its raw definition (e.g. *Bloodmoss Iron Ingot* produced by crafting).

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

**Rule of thumb:** identical raw materials stay lightweight quantity-based stacks; the moment an item's properties diverge from its definition, it becomes an instance. Iron Ore never becomes a unique object; *Charred Bloodmoss Iron* always does.

---

## 2. Property model — data-driven **[impl]**

Properties are stored in a `PropertySet`: a **string-keyed, case-insensitive map of `name → value`**. Storage is string-keyed on purpose — adding a new property never requires touching code, only data/rules. Known property names are provided as constants in `ItemProperties` for convenience and consistency, grouped:

**Physical** — `hardness` (resist physical deformation/damage), `mass` (weight/density), `flexibility` (deform without breaking), `affinity` (willingness to bond), `conductivity` (transmit electricity), `insulation` (resist/contain electricity).

**Processing** — `harvest_resistance` (difficulty to extract), `solubility` (ease properties transfer into solution), `instability` (likelihood of craft failure/mutation).

**Reactive** — `heat`, `cold`, `charge`, `toxicity`, `growth`, `decay`, `corrosion`, `arcane`.

**Response/resistance** properties (e.g. `heat_resistance`, `cold_resistance`) will be added as the model is finalized. **[planned]**

A definition's `BaseProperties` are its intrinsic values; an instance's `Properties` are the derived result of crafting. `PropertySet` supports `Get`, `Has`, `With`, and `Combine(other, fn)` for derivation.

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
