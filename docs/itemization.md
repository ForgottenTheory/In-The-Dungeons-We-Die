# CLAUDE.md

# In The Dungeons We Die

## Purpose

This is the primary project instruction file for Claude Code. It defines the project vision, architecture rules, MVP priorities, and implementation behavior. Detailed mechanics live in `/docs` and should be read only when relevant to the task.

## 1. Project Vision

**In The Dungeons We Die** is a progression-heavy extraction RPG combining:

* Melvor Idle-style interconnected profession progression.
* For The King 2-style spatial Realm exploration.
* Extraction-game preparation, risk, loss, and recovery.
* Real-time tick-based tactical combat.
* Dungeon Crawler Carl-inspired character identity through Species, Prefix, Base Class, and Suffix.
* Deep crafting, experimentation, and material interactions.

The central loop is:
**Prepare → Enter Realm → Explore → Fight / Gather / Discover → Extract or Go Deeper → Improve → Repeat**

**The Realm Run is the center of the game.** Major systems should meaningfully support Realm preparation, survival, exploration, extraction, or mastery.

## 2. Current Goal

Build a **playable MVP vertical slice** before expanding the full game.
The MVP must prove:
**Gather → Craft → Prepare → Enter Realm → Explore → Fight → Loot → Extract or Go Deeper → Improve → Repeat**

Use functional Godot 4 Control-based 2D UI. Do not wait for production art, final 3D presentation, shaders, multiplayer, or large content libraries.
The temporary UI is a real client for real game systems. Do not put throwaway gameplay rules inside UI code.

## 3. Core Design Pillars

### Realm-Centered Design

Every major system should connect back to Realm Runs. Avoid disconnected progression systems that exist only because other RPGs have them.

### Active vs Passive

Most progression systems should eventually support both.

* Passive: convenient, consistent, offline-friendly, lower optimization ceiling.
* Active: requires decisions or execution, better efficiency or quality, more discovery opportunities, better survival potential.

Active play should reward actual performance, not a hidden flat bonus. Passive play must remain worthwhile.

### Preparation Matters

Realm success should be influenced by equipment, consumables, food, profession products, build choices, Realm Knowledge, resistances, and supplies.

### Risk vs Reward

Realm Runs should repeatedly create meaningful decisions about continuing, extracting, spending resources, avoiding danger, and risking unsecured loot.

### Discovery Matters

Players should gradually discover recipes, material interactions, enemy weaknesses, Realm secrets, hidden routes, class interactions, and rare events.

### Depth Before Breadth

Prefer a few deeply interconnected systems over many shallow ones.

## 4. Character Identity

Character combat identity is:
**Species + Prefix + Base Class + Suffix**

* Species defines fundamental biological or metaphysical rules.
* Base Class defines the combat chassis.
* Prefix changes how the chassis operates.
* Suffix acts as a rule breaker.

Use established custom class names when available. Do not replace them with generic RPG names without explicit direction.
See `docs/classes.md`.

## 5. Architecture Invariant

Authoritative gameplay rules belong primarily in engine-independent C#.
The Domain layer must not depend on Godot scene-tree types or inherit from `Node`, `Node2D`, `Node3D`, `Control`, or `Resource`.

Examples of Domain systems:
`TickEngine`, `CombatSystem`, `CharacterSystem`, `ProfessionSystem`, `InventorySystem`, `CraftingSystem`, `RealmSystem`, `RealmKnowledgeSystem`, `ExtractionSystem`, `ClassCompositionSystem`.

Godot is **not forbidden**. Use Godot for UI, input, rendering, scenes, animation, audio, camera, visual effects, and engine integration.
The rule is: **Gameplay rules should not become coupled to the Godot scene tree.**

## 6. Layering

Preferred flow:
**Godot Presentation → Application → Domain**

Infrastructure handles JSON, saves, logging, and other external concerns.
Godot should communicate with gameplay through application services, commands, queries, DTOs, events, and read-only state snapshots.
The UI must not calculate authoritative combat or progression results.
See `docs/architecture.md`.

## 7. Data-Driven Content

Use external JSON for game definitions where practical and `System.Text.Json` for serialization.
Prefer reusable loading through `DataStore<T>`.
Keep definitions separate from runtime state. Example: `WeaponData` defines a weapon type; `ItemInstance` represents a specific owned weapon.

Use stable namespaced IDs such as:

* `weapon.rusty_sword`
* `enemy.goblin_raider`
* `realm.dark_forest`
* `profession.forestry`
* `species.undead`

See `docs/json-schema.md`.

## 8. Simulation And Combat

Gameplay timing should use the shared tick simulation where timing matters.
Combat is continuous and real-time, but readable rather than twitch-heavy.

Core combat timing:
**Telegraph → Windup → Execution → Recovery**

Players should have time to make meaningful decisions such as attacking, blocking, dodging, moving, interrupting, using abilities, or consuming items.
Health does **not naturally regenerate during normal Realm combat**. Healing requires intentional resources or mechanics.
See `docs/combat-spec.md`.

## 9. Realm And Extraction Rules

Production Realm exploration should resemble For The King 2-style spatial exploration rather than a simple card/node progression screen.
Realm loot is generally unsecured until successful extraction.
Death should create meaningful loss without erasing long-term progression such as profession levels, Realm Knowledge, discoveries, or safe Stash contents.
The player must always retain a recovery path through weak starter equipment.
See `docs/realms.md`.

## 10. Crafting And Professions

Professions are persistent progression systems and should interact with one another and with Realm preparation.
Crafting should eventually support passive crafting, active crafting, experimentation, material infusion, quality, discovery, and cross-profession interactions.
Avoid isolated skill bars and recipe systems with no meaningful connection to the Realm loop.

See:

* `docs/professions.md`
* `docs/crafting.md`
* `docs/itemization.md`
* `docs/progression.md`

## 11. Testing And Coding Rules

Domain systems should be testable without launching Godot whenever practical.
Prioritize tests for tick scheduling, combat calculations/timing, inventory transactions, crafting, loot, extraction, profession progression, offline calculations, Realm progression, and class modifiers.

Coding rules:

* Use the C# version supported by the current Godot .NET project.
* Enable nullable reference types.
* Prefer composition over deep inheritance.
* Keep classes focused and avoid global mutable state.
* Avoid unnecessary static systems.
* Prefer constructor dependency injection in Domain code.
* Keep authoritative calculations deterministic where practical.
* Prefer readability over cleverness.
* Use interfaces where they represent real boundaries, not ceremony.
* Do not create giant manager classes or giant switches based on content IDs.
* Do not generalize systems before real use cases require it.

## 12. MVP Scope Control

Prefer a working vertical-slice feature over a sophisticated framework for a future feature.

Unless explicitly requested, do not prioritize:

* Multiplayer or PvP.
* Production 3D graphics or final shaders.
* Huge procedural generation systems.
* Hundreds of items.
* Full profession content or full class roster.
* Trading or live-service infrastructure.

Design reasonable extension points without building speculative systems.
See `docs/vertical-slice.md` and `docs/godot-ui-mvp.md`.

## 13. Documentation Map

Always read before major implementation:

* `docs/architecture.md`
* `docs/vertical-slice.md`

Read when relevant:

* Combat: `docs/combat-spec.md`
* Godot MVP UI: `docs/godot-ui-mvp.md`
* Data: `docs/json-schema.md`
* Classes: `docs/classes.md`
* Professions: `docs/professions.md`
* Realms: `docs/realms.md`
* Progression: `docs/progression.md`
* Crafting: `docs/crafting.md`
* Items: `docs/itemization.md`

Do not load every document unnecessarily.

If documentation conflicts:

1. `CLAUDE.md` wins for architecture and development rules.
2. The most feature-specific document wins for mechanics.
3. `vertical-slice.md` wins for current MVP scope.
4. Do not silently redesign major systems to resolve contradictions.

## 14. Implementation Workflow

Before implementing a significant feature:

1. Read the relevant design document.
2. Determine which architecture layer owns the behavior.
3. Check whether the feature belongs in the current vertical slice.
4. Inspect existing code before creating new abstractions.
5. Implement the smallest coherent version that advances the playable loop.
6. Add tests for deterministic Domain behavior.
7. Keep gameplay rules outside Godot presentation code.
8. Keep the project runnable after meaningful increments.
9. Update documentation when an established design decision changes.

Do not silently redesign major game systems during implementation.

## 15. Guiding Question

When evaluating a gameplay feature, ask:
**How does this make preparing for, entering, exploring, surviving, mastering, or extracting from a Realm more interesting?**
If there is no convincing answer, reconsider whether the feature belongs in the game.
