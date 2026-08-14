# CLAUDE.md

# In The Dungeons We Die

## Purpose

This file is the primary instruction document for Claude when working on this repository.

It defines the project's architectural rules, development philosophy, scope rules, and documentation hierarchy.

Do not treat this file as the complete game design document.

Detailed mechanics belong in the documents under `/docs`.

---

# 1. Project Vision

In The Dungeons We Die is a progression-heavy extraction RPG inspired by:

- Melvor Idle's interconnected skill and progression systems.
- For The King 2's visual realm exploration and tactical presentation.
- Extraction games' preparation, risk, loss, extraction, and persistent progression loops.
- Dungeon Crawler Carl's absurd and mechanically meaningful class identities.

The game combines idle progression with active skill expression.

Players gather resources, train professions, craft equipment, develop characters, prepare for dangerous Realm Runs, enter increasingly dangerous realms, acquire valuable resources and equipment, and decide whether to continue deeper or extract safely.

The central loop is:

Prepare
→ Enter Realm
→ Explore
→ Fight / Gather / Discover
→ Decide Whether To Go Deeper
→ Extract
→ Improve
→ Repeat

THE DELVE / REALM RUN IS THE CENTER OF THE GAME.

Every major progression system should eventually provide meaningful advantages, options, knowledge, or preparation for Realm Runs.

---

# 2. Current Development Goal

DO NOT attempt to build the complete game.

The current development goal is a TESTABLE MVP VERTICAL SLICE.

The vertical slice must be playable through functional Godot 4 Control-based 2D UI.

The developer must be able to test the complete gameplay loop without requiring:

- Production artwork
- 3D character models
- Final animations
- Final shaders
- Multiplayer
- Large content libraries

The temporary 2D UI is a development client for the real game systems.

It is NOT throwaway gameplay logic.

The underlying domain should eventually support the production 3D/2D hybrid presentation without being rewritten.

---

# 3. Core Design Pillars

## 3.1 The Realm Run Is The Game

All major systems should connect back to Realm Runs.

Examples:

Forestry provides materials for:
- Equipment
- Ammunition
- Camp supplies
- Crafting
- Realm preparation

Alchemy provides:
- Healing
- Resistance
- Buffs
- Utility
- Realm-specific preparation

Smithing provides:
- Weapons
- Armor
- Tools
- Specialized equipment

Wayfinding provides:
- Realm information
- Better targeting
- Affix manipulation
- Improved route knowledge

Do not create disconnected progression systems merely because other RPGs contain them.

---

## 3.2 Active vs Passive

Most major progression activities should eventually support two approaches.

PASSIVE:
- Convenient
- Consistent
- Lower efficiency
- Lower maximum quality
- Lower risk
- Appropriate for idle progression

ACTIVE:
- Requires player interaction
- Greater efficiency
- Better rewards
- Better crafting outcomes
- Better survival
- Additional discoveries
- Greater skill expression

Passive gameplay must remain worthwhile.

Active gameplay must NOT simply invalidate passive gameplay.

The player is choosing convenience versus optimization and skill expression.

---

## 3.3 Preparation Matters

A successful Realm Run begins before entering the portal.

Preparation includes:

- Equipment
- Consumables
- Food
- Ammunition
- Class build
- Profession products
- Realm knowledge
- Resistance preparation
- Inventory capacity
- Campsite supplies

A player who understands a Realm should be able to prepare intelligently for it.

---

## 3.4 Risk vs Reward

Realm Runs continuously ask:

- Extract now?
- Continue deeper?
- Spend this consumable?
- Save it?
- Fight this enemy?
- Avoid it?
- Investigate this event?
- Take the dangerous route?
- Carry more loot and risk losing it?

Going deeper increases danger and potential reward.

Extraction converts temporary run gains into persistent ownership.

---

## 3.5 Discovery Matters

The game should reward experimentation and accumulated knowledge.

Examples:

- Crafting discoveries
- Material interactions
- Realm discoveries
- Enemy weaknesses
- Hidden locations
- Rare events
- Class interactions
- Recipes
- Affix combinations

Do not expose every interaction immediately.

The player should gradually build knowledge of the game.

---

## 3.6 Depth Before Breadth

Do not implement dozens of shallow systems for the MVP.

Implement a small number of systems that interact meaningfully.

Prefer:

3 professions with meaningful interactions

over:

18 professions that are isolated progress bars.

---

# 4. Character Identity

Characters are composed from multiple independent systems.

Core identity:

Species
+ Base Class
+ Prefix
+ Suffix

Example:

Undead
+ Bastion
+ Pyromaniac
+ Of The Exploding Kneecaps

Each component must provide meaningful mechanics.

Species defines biological or fundamental traits.

Base Class defines the core combat chassis and gameplay role.

Prefix modifies the way the class operates.

Suffix is a rule-breaking mechanic capable of significantly changing gameplay.

Do not implement these as cosmetic names attached to generic stat bonuses.

Detailed rules belong in:

`docs/classes.md`

---

# 5. Professions

The game's Melvor-inspired progression systems are called PROFESSIONS.

Examples include:

- Mining
- Forestry
- Fishing
- Herblore
- Farming
- Smithing
- Alchemy
- Cooking
- Enchanting
- Fletching
- Tailoring
- Medicine
- Beast Lore
- Sleight of Hand
- Agility
- Campcraft
- Wayfinding
- Devotion
- Summoning

Profession progression is persistent.

Profession systems should interact with:

- Crafting
- Realm preparation
- Realm gathering
- Character builds
- Equipment
- Other professions

Avoid isolated progression systems.

Detailed rules belong in:

`docs/professions.md`

---

# 6. Domain-First Architecture

Gameplay rules must be implemented primarily as engine-independent C#.

The Domain layer must not reference Godot.

Forbidden Domain dependencies include:

- Godot.Node
- Godot.Node2D
- Godot.Node3D
- Godot.Control
- Godot.Resource
- Godot.SceneTree
- Godot signals
- Godot UI classes

Examples of Domain systems:

- TickEngine
- CombatSystem
- CombatCalculator
- CharacterSystem
- ProfessionSystem
- InventorySystem
- EquipmentSystem
- CraftingSystem
- RealmSystem
- RealmKnowledgeSystem
- LootSystem
- ExtractionSystem
- ClassCompositionSystem

These should be normal C# classes.

---

# 7. Godot Is The Client / Presentation Layer

Godot may be used extensively where Godot is actually useful.

Use Godot for:

- Control UI
- Scene composition
- Input
- Rendering
- Animation
- Audio
- Navigation
- Camera systems
- Visual effects
- 2D and 3D presentation
- Multiplayer integration
- Engine lifecycle integration

Do NOT interpret "Domain-first" as "avoid Godot."

Use Godot's strengths.

The restriction is specifically that GAME RULES should not become tightly coupled to Nodes.

---

# 8. Communication Between Godot And Domain

Preferred mechanisms:

- Application services
- Commands
- Queries
- DTOs
- C# events
- Read-only state snapshots

Example:

AttackButton
→ QueueAttackCommand
→ CombatSystem
→ TickEngine
→ Attack Resolves
→ DamageDealtEvent
→ Godot UI Updates

UI must not calculate authoritative combat results.

---

# 9. Data-Driven Content

Game content should be data-driven wherever practical.

Use external JSON for definitions such as:

- Actors
- Enemies
- Items
- Weapons
- Armor
- Materials
- Species
- Base Classes
- Prefixes
- Suffixes
- Professions
- Recipes
- Realms
- Realm Affixes
- Loot Tables
- Abilities
- Status Effects

Use `System.Text.Json`.

Prefer a reusable generic:

`DataStore<T>`

Definitions and runtime state must remain separate.

Example:

`ItemData`

describes what an item IS.

`ItemInstance`

describes a specific owned item.

Do not mutate shared definition objects to represent runtime state.

---

# 10. Tick-Based Simulation

The game uses a deterministic tick-driven simulation for gameplay systems where timing matters.

Examples:

- Combat actions
- Movement
- Resource regeneration
- Hazards
- Gathering
- Crafting
- Idle progression

Actions use intervals / tick costs.

Combat is NOT traditional turn-based combat.

Combat should feel real-time while remaining readable enough for tactical decisions.

---

# 11. Combat Philosophy

Combat prioritizes decision-making over twitch reflexes.

Enemy attacks should generally expose readable intent.

Conceptual attack lifecycle:

Telegraph
→ Windup
→ Execution
→ Recovery

The player should have enough time to make meaningful decisions such as:

- Attack
- Block
- Dodge
- Move
- Interrupt
- Use ability
- Consume item
- Change target

Different enemies may manipulate timing, hide information, accelerate attacks, or otherwise challenge this model.

Do not turn combat into a turn-based system disguised with timers.

---

# 12. Health Rule

Health does NOT naturally regenerate during normal Realm combat.

Recovery requires intentional systems such as:

- Healing abilities
- Medicine
- Potions
- Food where appropriate
- Campsite systems
- Class mechanics
- Special effects

Mana and Stamina may regenerate according to their respective systems.

Attrition is important to Realm Runs.

---

# 13. Realm Philosophy

Realm exploration should ultimately resemble For The King 2 more than Slay The Spire.

The production game should support spatial exploration rather than merely selecting nodes from a card-like map.

Realm content may include:

- Traversable locations
- Branching routes
- Combat
- Gathering
- Events
- Hazards
- Hidden areas
- Campsites
- Extraction portals
- Boss encounters
- Realm-specific resources

The MVP may represent these systems through simplified 2D UI while the Domain model remains compatible with future visual exploration.

---

# 14. Extraction

Loot acquired during a Realm Run is considered unsecured until extraction unless explicitly specified otherwise.

Successful extraction transfers secured rewards into persistent storage.

Death should create meaningful loss.

The exact loss model belongs in `docs/realms.md`.

Do not casually bypass extraction risk by automatically banking Realm loot.

---

# 15. Realm Knowledge

Realm Knowledge is persistent progression associated with individual Realms.

Repeated exploration can reveal:

- Enemy information
- Resource information
- Routes
- Events
- Extraction opportunities
- Environmental threats
- Rare encounters
- Boss information
- Hidden discoveries

Realm Knowledge should make experienced players meaningfully better at preparing for and navigating a Realm without simply becoming a raw damage multiplier.

---

# 16. Crafting Philosophy

Crafting is interconnected.

Materials may influence other materials and recipes.

Example concept:

Iron Ingot
+ Oak Bark
+ sufficient Herblore knowledge
→ Barkbound Iron

The resulting material might gain properties that affect equipment created from it.

Crafting should eventually support:

- Passive production
- Active crafting
- Material experimentation
- Infusions
- Quality
- Masterwork outcomes
- Recipe discovery
- Profession interactions

Detailed rules belong in:

`docs/crafting.md`

---

# 17. Testing

Domain systems should be testable without running a Godot scene whenever practical.

Prioritize tests for:

- Combat formulas
- Tick scheduling
- Inventory transactions
- Crafting resolution
- Loot generation
- Extraction
- Realm progression
- Class modifiers
- Profession calculations

Do not move gameplay logic into UI scripts merely because it is easier to prototype.

---

# 18. Current MVP Rule

When choosing between:

A technically impressive framework for a future feature

and

A working implementation of the vertical slice

prefer the vertical slice.

The MVP must prove:

Gather
→ Prepare
→ Enter Realm
→ Explore
→ Fight
→ Loot
→ Decide Continue/Extract
→ Extract
→ Craft/Upgrade
→ Repeat

---

# 19. Scope Control

Unless explicitly requested, DO NOT prioritize:

- Multiplayer
- Production 3D graphics
- Cross-hatched shaders
- Massive procedural generation
- Hundreds of items
- Full profession roster
- Complete class roster
- Live services
- Trading
- PvP
- Complex networking

Design architecture so these are possible where reasonable.

Do not build them before the vertical slice proves the game.

---

# 20. Coding Standards

- Use modern C# supported by the project's .NET/Godot version.
- Enable nullable reference types.
- Prefer composition over deep inheritance.
- Prefer explicit domain types over primitive obsession where useful.
- Keep classes focused.
- Avoid global mutable state.
- Avoid unnecessary static systems.
- Prefer dependency injection through constructors for Domain services.
- Keep authoritative calculations deterministic where practical.
- Avoid premature abstractions.
- Avoid speculative generic frameworks without an actual use case.
- Optimize for readability and testability.
- Use interfaces where multiple implementations or boundaries genuinely exist.
- Do not create an interface for every class merely for ceremony.

---

# 21. Documentation Rules

Always consult:

- `docs/architecture.md`
- `docs/vertical-slice.md`

Read feature-specific documents when working on those systems:

- `docs/combat-spec.md`
- `docs/godot-ui-mvp.md`
- `docs/json-schema.md`
- `docs/classes.md`
- `docs/professions.md`
- `docs/realms.md`
- `docs/progression.md`
- `docs/crafting.md`
- `docs/itemization.md`

If documentation conflicts:

1. CLAUDE.md architectural rules win.
2. The most feature-specific document wins for game mechanics.
3. The MVP document wins for current implementation scope.
4. Ask before making a major irreversible assumption.

---

# 22. Claude Implementation Behavior

Before implementing a significant feature:

1. Identify the relevant design document.
2. Identify which layer owns the behavior.
3. Identify whether the feature is required for the current vertical slice.
4. Reuse existing systems before creating new architecture.
5. Keep domain rules outside Godot UI.
6. Add or update tests for deterministic Domain behavior.
7. Keep the project runnable after meaningful increments.

Do not silently redesign major game systems.

If implementation reveals a design contradiction, explain the contradiction before introducing a large architectural workaround.

---

# 23. Guiding Question

When uncertain about a gameplay feature, ask:

"How does this make preparing for, surviving, exploring, or extracting from a Realm more interesting?"

If there is no convincing answer, the feature may not belong in the game.