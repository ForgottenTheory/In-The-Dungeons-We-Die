# SYSTEM_INDEX.md

Map of systems → key files → how they connect. Paths are relative to repo root. Core namespaces are `Dungeons.*`; Godot is `Dungeons.Game.*`.

## Layout
```
core/    InTheDungeonsWeDie.Core.csproj   (net8.0, RootNamespace "Dungeons", NO Godot ref)
game/    InTheDungeonsWeDie.csproj         (Godot.NET.Sdk/4.7.1, references Core); project.godot here
tests/   InTheDungeonsWeDie.Core.Tests.csproj (xUnit, references Core only)
docs/    design docs + docs/current-state.md audit
game/data/<type>/*.json   all content
InTheDungeonsWeDie.slnx   root solution
```

## Composition root (how everything connects)
- **`game/GameRoot.cs`** — Godot autoload (`[autoload] GameRoot`). Constructs every Core service in `_Ready()`, loads JSON via `ContentLoader`, owns run/combat/equipment state, and exposes commands + query strings + C# events to the UI. This is the single wiring point and the application layer. (~850 lines; flagged to split later.)
- **`game/ui/MainMvpUI.cs` + `MainMvpUI.tscn`** — the one debug UI page (main scene). Builds all controls in code, calls `GameRoot` methods, subscribes to its events (`LogEmitted`, `CharacterChanged`, `InventoryChanged`, `RunningChanged`, `DiscoveryChanged`, `CombatChanged`, `RealmChanged`).
- **`game/Infrastructure/ContentLoader.cs`** — reads `res://data/**.json`, feeds text to `DataStore<T>`. **`SaveStore.cs`** — `user://save.json` read/write, delegates (de)serialization to Core.

## Simulation — `core/Simulation/`
`TickEngine`, `ScheduledAction`. Deterministic clock; `Schedule(delay, cb)`/`Advance(n)`/`Cancel`/`TickAdvanced`. One instance shared by combat + `PassiveProfessionRunner`; advanced by `GameRoot._Process` at `TicksPerSecond=20`.

## Content — `core/Content/`
`IDefinition`, `DataStore<T>` (load/lookup/dup-id validation, enum+case-insensitive JSON), `MaterialDefinition` (implements `IItemDefinition`), `DuplicateDefinitionException`.

## Items / Inventory / Equipment — `core/Items/`, `core/Inventory/`, `core/Equipment/` (all namespace `Dungeons.Items`)
- `PropertySet` (string-keyed, immutable, `Combine`), `ItemProperties` (property-name constants), `ItemType`/`ItemQuality`, `IItemDefinition`, `ItemInstance`, `InstanceIdSource`, `ItemStack`.
- `Inventory` — stacks (`Add`/`TryRemove`/`Snapshot`) + instances (`AddInstance`/`RemoveInstance`/`Instances`).
- `EquipmentDefinition` (Weapon/Armor stats + base properties), `Equipment` (slot→instance container), `EquipmentResolver` → `Combat.AttackProfile`/`ArmorProfile` (the material→combat seam, currently Mass/Hardness only).
- **Connects:** professions/crafting/combat deposit into `Inventory`; `EquipmentResolver` output is passed by `GameRoot` into `Combatant.FromCharacter`.

## Characters — `core/Characters/`
- `AttributeSet`, `AttributeType`, `ResourcePool`, `ResourceType`, `ResourceCalculator`.
- `Modifiers/` — `StatId`, `ModifierOperation`, `StatModifier`, `ModifierData`, `ModifierPipeline`.
- `Composition/` — `CharacterBuild` (4 ids), `CharacterComponentDefinition` (+ Species/Prefix/Suffix/BaseClass), `CharacterComposer` → `CharacterBlueprint`; runtime `Character`.
- `Rules/` — `ICharacterRule`, `RuleRegistry`, `CharacterSnapshot`, `UnreasonableConfidenceRule`, `InappropriateOptimismRule`. **Connects:** `Character.EffectiveAttributes` = base + active rule bonuses; read by the player `Combatant`.

## Professions — `core/Professions/`
`ProfessionDefinition`, `ProfessionActionDefinition` (ItemAmount/ItemChance sub-shapes), `ProfessionProgress` (xp/level/mastery), `ProfessionLeveling`, `ProfessionTuning`, `ActionResolver`, `ActionOutcome`, `ProfessionSystem` (single `Execute` path for passive+active; provider-supplied `Inventory`), `PassiveProfessionRunner` (on TickEngine). **Connects:** `GameRoot` gives it `() => CurrentBag` so gathering lands in the Stash or the run inventory.

## Crafting — `core/Crafting/`
`CraftingInteractionDefinition` (inputs + profession reqs + result + `ResultIsInstance`), `DiscoverySystem`, `CraftingExperimentSystem`, `ExperimentOutcome`, `CraftingDerivation` (property-merge seam → future reaction sim). **Connects:** uses the Stash + `_materials` + `InstanceIdSource`; produces stacks or derived `ItemInstance`s.

## Combat — `core/Combat/`
`DamageType`, `AbilityDefinition`, `ActorDefinition`, `ConsumableDefinition`, `Combatant` (player shares `Character` pools + effective attrs + weapon `AttackProfile` + `ArmorProfile`; enemy from actor), `CombatCalculator`, `CombatTuning`, `AttackProfile`/`ArmorProfile`, `CombatEncounter` (tick-driven lifecycle, AI loop, player commands, events `Logged`/`StateChanged`/`Ended`). **Connects:** runs on the shared TickEngine; `GameRoot` bridges realm combat nodes ↔ encounter, routes loot + clears/ends the run on `Ended`.

## Realms — `core/Realms/`
`RealmLocationDefinition` (type/depth/connections/content refs), `RealmDefinition` (location graph), `RealmRun` (travel/depth/clear + `RunInventory`), `RealmExtraction` (`Secure`/`Forfeit`, moves stacks+instances). **Connects:** `GameRoot` orchestrates node actions via existing combat/gather systems.

## Persistence — `core/Persistence/`
`SaveData` (v3: build, stash stacks+instances, equipment, next-instance-id, professions, knowledge, discoveries) + `ItemInstanceSave`/`ProfessionSave` DTOs, `SaveSerializer` (System.Text.Json), `SaveMapper` (Capture/Apply between live systems ↔ SaveData). **Connects:** `GameRoot.SaveGame/LoadGame` via `SaveStore` (Godot `user://`).

## Data content — `game/data/`
`species/`(3) `classes/`(2) `prefixes/`(3) `suffixes/`(5) `professions/`(3) `profession_actions/`(3) `materials/`(8) `crafting_interactions/`(2) `abilities/`(3) `actors/`(2) `consumables/`(1) `equipment/`(4) `realms/`(1). Each folder auto-loads into a `DataStore<T>` in `GameRoot._Ready`.

## Tests — `tests/`
Mirror the Core namespaces: `Simulation/`, `Content/`, `Characters/`, `Items/` (item model, equipment, equipment content validation), `Professions/`, `Crafting/`, `Combat/`, `Realms/`, `Persistence/`, `Integration/` (`FullLoopTests` — the whole loop headless). Content-validation tests (Characters/Professions/Combat/Realms/Items) load real `game/data` JSON via `TestPaths.DataDir`.
