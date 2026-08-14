# Architecture

# In The Dungeons We Die

## 1. Purpose

This document defines the technical architecture of the game.

The primary architectural goal is to separate authoritative gameplay simulation from presentation while still using Godot extensively for everything the engine does well.

The architecture must support:

- Godot 4 .NET
- 2D MVP UI
- Future hybrid 2D/3D presentation
- Tick-based simulation
- Idle progression
- Active gameplay
- Data-driven content
- Automated testing
- Future multiplayer without requiring it during MVP

---

# 2. Layer Overview

The application is divided conceptually into:

Godot Presentation
        ↓
Application
        ↓
Domain
        ↑
Infrastructure

The dependency direction matters more than the physical folder structure.

---

# 3. Domain Layer

The Domain contains authoritative gameplay rules.

It must not reference Godot.

Suggested systems include:

## Simulation

- TickEngine
- ActionScheduler
- GameClock

## Characters

- Character
- Attributes
- Resources
- CharacterComposition
- StatusEffects

## Combat

- CombatEncounter
- CombatSystem
- CombatCalculator
- CombatAction
- TelegraphSystem

## Professions

- ProfessionProgress
- ProfessionAction
- GatheringSystem
- ProfessionMasterySystem

## Inventory

- Inventory
- Stash
- Equipment
- ItemInstance

## Crafting

- CraftingSystem
- RecipeResolver
- MaterialInteractionResolver
- CraftingDiscoverySystem
- CraftingQualityResolver

## Realms

- RealmRun
- RealmState
- RealmLocation
- RealmEncounter
- RealmKnowledge
- ExtractionSystem

## Loot

- LootGenerator
- LootTableResolver
- ItemGenerationSystem

## Classes

- SpeciesDefinition
- BaseClassDefinition
- PrefixDefinition
- SuffixDefinition
- CharacterBuild

---

# 4. Application Layer

The Application layer coordinates Domain operations.

It represents use cases rather than game rules.

Examples:

- CreateCharacter
- StartProfessionAction
- StopProfessionAction
- LaunchRealm
- MoveParty
- StartEncounter
- QueueCombatAction
- ExtractFromRealm
- CraftItem
- CollectIdleRewards

Application services may coordinate multiple Domain systems.

Example:

`ExtractFromRealm`

may coordinate:

RealmRun
Inventory
Stash
RealmKnowledge
Progression

The extraction rules themselves remain Domain behavior.

---

# 5. Infrastructure Layer

Infrastructure handles external concerns.

Examples:

- JSON file loading
- Save serialization
- Save file storage
- Logging
- Configuration
- Random seed persistence

Infrastructure implementations may satisfy Domain/Application abstractions.

---

# 6. Godot Layer

Godot is the interactive client.

Responsibilities include:

- Control scenes
- 2D presentation
- 3D presentation
- Input
- Animations
- Audio
- Camera
- Scene transitions
- Tooltips
- Visual feedback
- Multiplayer API integration later

Godot scripts may call Application services and subscribe to Domain/Application events.

They must not become the authoritative source of gameplay calculations.

---

# 7. Recommended Project Structure

Game/
    Domain/
        Characters/
        Classes/
        Combat/
        Crafting/
        Inventory/
        Items/
        Loot/
        Professions/
        Realms/
        Simulation/

    Application/
        Commands/
        Queries/
        Services/

    Infrastructure/
        Data/
        Persistence/
        Logging/

    Godot/
        Autoloads/
        Screens/
        Controls/
        Presenters/
        ViewModels/

    Data/
        Actors/
        Classes/
        Items/
        Professions/
        Realms/
        Recipes/
        LootTables/

Tests/
    Domain/
    Application/

docs/

---

# 8. Definitions vs Runtime State

This distinction is mandatory.

Definition data represents shared content.

Example:

WeaponData:
- Id
- Name
- BaseDamage
- DamageType

Runtime state represents a particular object.

Example:

ItemInstance:
- InstanceId
- DefinitionId
- Quality
- Durability
- GeneratedAffixes

Do not mutate `WeaponData` because one player's sword gained an affix.

---

# 9. IDs

Data-driven entities should use stable string IDs.

Examples:

`weapon.rusty_sword`

`enemy.goblin_raider`

`realm.dark_forest`

`profession.forestry`

`species.undead`

`suffix.exploding_kneecaps`

IDs are persistence and lookup keys.

Display names may change without breaking saves.

---

# 10. DataStore<T>

A generic DataStore should provide:

- Loading
- Validation
- Caching
- ID lookup
- Enumerating definitions
- Development reload support where practical

Do not force every content system to write its own JSON loader.

However, DataStore should remain a data access utility rather than becoming a god object containing gameplay logic.

---

# 11. Tick Engine

The TickEngine provides deterministic simulation timing.

Conceptually:

TickEngine
    CurrentTick
    Schedule(action)
    Advance()
    Cancel(action)

Scheduled actions have:

- ID
- Actor
- Start Tick
- Resolve Tick
- Action Type
- Payload / command data

The exact implementation may evolve.

---

# 12. Action Timing

Actions should be modeled independently from rendering time.

Example:

Sword attack:

Start: Tick 100
Telegraph: Tick 100
Execute: Tick 130
Recovery End: Tick 150

Godot may visualize that as seconds.

The Domain remains authoritative about resolution.

---

# 13. Combat Event Flow

Example:

Player presses Attack.

Godot:
AttackButton

Application:
QueueAttackCommand

Domain:
CombatSystem validates action.

TickEngine schedules attack.

Combat event:
ActionTelegraphed

Godot:
Displays action bar.

Tick 130 arrives.

CombatCalculator calculates result.

Combat event:
DamageDealt

Godot:
Updates HP and visual feedback.

---

# 14. Domain Events

Domain events communicate completed or meaningful state transitions.

Examples:

- TickAdvanced
- ActionQueued
- ActionCancelled
- ActionTelegraphed
- ActionResolved
- DamageDealt
- CharacterDefeated
- ItemReceived
- ItemLost
- ProfessionXpGained
- ProfessionLevelIncreased
- CraftingDiscoveryMade
- RealmEntered
- RealmLocationDiscovered
- RealmDepthChanged
- ExtractionCompleted

Avoid events for every trivial property assignment.

---

# 15. Commands

Commands represent intent.

Examples:

- AttackTarget
- Block
- Move
- UseItem
- BeginGathering
- CraftRecipe
- EnterRealm
- TravelToLocation
- Extract

Commands may fail validation.

The UI must not assume a command succeeded until authoritative state confirms it.

---

# 16. Queries / State Snapshots

UI needs safe ways to read game state.

Prefer:

- Query services
- Read-only DTOs
- Immutable snapshots

over giving UI unrestricted mutable access to Domain objects.

---

# 17. Character Composition

Character identity is composed from:

Species
Base Class
Prefix
Suffix

Conceptually:

CharacterBuild
{
    SpeciesId
    BaseClassId
    PrefixId
    SuffixId
}

Systems resolve these definitions into:

- Stats
- Abilities
- Passives
- Rule modifiers
- Resource behavior

Avoid hardcoding:

`if character.Class == "Bastion"`

throughout the codebase.

Use tags, abilities, modifiers, policies, and well-defined mechanics.

---

# 18. Modifier Pipeline

Many systems will modify calculations.

Examples:

- Species
- Class
- Prefix
- Suffix
- Equipment
- Status
- Realm effect
- Profession bonus

Use a consistent modifier strategy.

Conceptually:

Base Value
→ Additive Modifiers
→ Multiplicative Modifiers
→ Rule Overrides
→ Clamp / Validation
→ Final Value

Rule-breaking suffixes may require explicit hooks rather than merely numeric modifiers.

Do not force every mechanic into `+5%`.

---

# 19. Profession Architecture

Professions share common concepts:

- Level
- XP
- Mastery
- Action interval
- Output
- Active modifiers
- Passive modifiers

But profession-specific behavior should remain extensible.

Do not build one gigantic ProfessionSystem switch statement.

---

# 20. Active and Passive Actions

The same underlying recipe/activity should be reusable across active and passive execution where possible.

Example:

Smith Iron Bar

Base definition:
- Inputs
- Outputs
- Interval
- XP

Passive executor:
- Resolves normal production.

Active executor:
- Uses same base action.
- Adds interaction/performance result.
- May improve quality/yield.

This prevents two unrelated games from emerging.

---

# 21. Offline Progress

Offline progress should not replay millions of individual ticks.

Use aggregate calculations where behavior permits.

Conceptually:

ElapsedTime / EffectiveInterval
= CompletedActions

Then resolve:

- Inputs consumed
- Outputs generated
- XP
- Mastery

Complex systems may require capped or staged simulation.

Offline results must remain deterministic enough to test.

---

# 22. Crafting Architecture

Crafting should support:

Recipe
+
Materials
+
Character Professions
+
Optional Infusions
+
Active Performance
+
Modifiers
=
Craft Result

The recipe defines the base transformation.

Materials contribute properties.

Professions unlock capabilities and improve outcomes.

Active crafting may improve results.

Discovery records newly found combinations.

---

# 23. Inventory Architecture

Separate:

## Stash

Persistent safe storage.

## Loadout

Items brought into a Realm.

## Realm Inventory

Unsecured items acquired during a run.

Extraction moves eligible Realm inventory into persistent ownership.

Death processes unsecured items according to Realm rules.

---

# 24. RealmRun Aggregate

A RealmRun should own the authoritative state for a current expedition.

Possible state:

- Realm ID
- Tier
- Depth
- Seed
- Party
- Current location
- Visited locations
- Active modifiers
- Run inventory
- Campsite availability
- Extraction state
- Encounter state

This allows save/resume and future host-authoritative multiplayer.

---

# 25. Realm Knowledge

RealmKnowledge is persistent and separate from RealmRun.

Example:

RealmKnowledge
{
    RealmId
    KnowledgeLevel
    Experience
    DiscoveredEnemies
    DiscoveredResources
    DiscoveredLocations
    DiscoveredEvents
}

Dying does not erase Realm Knowledge unless a future mechanic explicitly says otherwise.

---

# 26. Randomness

Random systems should support seeded randomness where practical.

Important for:

- Testing
- Realm generation
- Loot reproduction
- Future multiplayer authority
- Bug reproduction

Avoid scattered calls to unrelated global random generators inside Domain logic.

---

# 27. Persistence

Save data eventually includes:

- Characters
- Stash
- Equipment
- Profession progression
- Crafting discoveries
- Realm knowledge
- Unlocks
- Settings
- Active Realm Run where supported

Definitions are NOT duplicated into save files unless required for migration.

Save IDs and runtime values.

---

# 28. Save Versioning

Save data must include a version.

Future migration should be possible.

Do not assume today's schema will remain permanent.

---

# 29. Testing Strategy

Unit test deterministic Domain behavior.

High priority:

TickEngine:
- ordering
- cancellation
- simultaneous actions

Combat:
- damage
- telegraphs
- mitigation
- death

Inventory:
- add/remove
- capacity
- extraction transfer

Crafting:
- inputs
- outputs
- discovery
- modifiers

Professions:
- XP
- intervals
- offline calculations

Realms:
- extraction
- loss
- knowledge

---

# 30. Future Multiplayer

MVP is single-player.

Do not build multiplayer infrastructure prematurely.

However, Domain authority should make future networking possible.

Future model:

Host owns authoritative Domain simulation.

Clients send intent.

Host resolves.

Clients receive authoritative state/events.

Godot's multiplayer APIs can provide transport/synchronization later.

---

# 31. Architectural Anti-Patterns

Avoid:

- Gameplay formulas inside Button callbacks.
- Godot Nodes acting as databases.
- Static global service locators everywhere.
- One GameManager containing the entire game.
- Huge switch statements based on item/class IDs.
- Duplicated active/passive implementations.
- Runtime state stored inside JSON definitions.
- UI directly mutating Domain collections.
- Premature multiplayer architecture.
- Premature ECS conversion.
- Generic abstractions without multiple real use cases.

---

# 32. Architectural Goal

The same Domain should ultimately be capable of driving:

- Automated tests
- Developer simulations
- 2D MVP UI
- Production Godot presentation
- Future host-authoritative multiplayer

without rewriting the core game rules.