# CLAUDE.md — In The Dungeons We Die

Permanent project instructions and development rules. **Keep this file concise — it is rules, not documentation.** For current state, systems, decisions, and next steps read the handoff docs (below) and `/docs`.

## What this is
A progression-heavy extraction RPG (Melvor-style professions + For-The-King-2 spatial realms + extraction risk/loss + tick-based tactical combat + emergent crafting). Godot 4.7 (.NET) client over an engine-independent C# domain. The playable MVP vertical slice is complete; current work is the **equipment + emergent-crafting system**.

## Read these first (handoff docs, kept current)
- `PROJECT_STATE.md` — what's implemented / partial / scaffolded / planned.
- `SYSTEM_INDEX.md` — systems, key files, how they connect.
- `DECISIONS.md` — architectural/gameplay decisions **and why** (+ rejected options).
- `ROADMAP.md` — remaining work and order.
- `HANDOFF.md` — where we stopped and exact next steps.
- `docs/current-state.md` — deep audit (verify against code; may lag by a phase).
- `docs/itemization.md`, `docs/crafting.md §17` — the item-instance + emergent-crafting model.

## Architecture rules (hard invariants)
1. **Domain-first, enforced by the assembly split.** Gameplay logic lives in `core/` (`InTheDungeonsWeDie.Core`, namespace `Dungeons.*`, `net8.0`) and MUST NOT reference Godot. The Godot project `game/` references Core; never the reverse. Tests reference Core only.
2. **Godot is the client.** Use it for UI, input, scenes, file access (`res://`/`user://`), presentation. `GameRoot` (autoload) is the composition root + application glue; it wires systems and exposes commands/queries/events to the UI. Do not put authoritative gameplay rules in UI or `GameRoot`.
3. **Data-driven content.** Definitions are JSON under `game/data/<type>/`, loaded via `ContentLoader` (Godot) into `DataStore<T>` (Core, path-agnostic — it takes JSON text, never file paths). Use stable namespaced ids (`material.oak_bark`, `equip.iron_sword`).
4. **Definitions vs runtime state are separate.** `IItemDefinition`/`MaterialDefinition`/`EquipmentDefinition` describe kinds; `ItemInstance` is a specific owned item with derived properties. Raw stackables stay quantity-based; anything with derived/unique properties is an instance. Never mutate definitions.
5. **Deterministic tick simulation.** One shared `TickEngine` drives combat + passive gathering; `GameRoot._Process` advances it. Inject `IRandomSource` (seeded) — no scattered global RNG.
6. **Properties are string-keyed** (`PropertySet`); new material/item properties are data, not code. Crafting derives instance properties through the single `CraftingDerivation` seam; combat reads neutral `AttackProfile`/`ArmorProfile` (via `EquipmentResolver`), never equipment types.

## Coding standards
- Modern C#, nullable enabled, `ImplicitUsings` on (Core). Composition over inheritance. Constructor DI for domain services. Keep authoritative calcs deterministic. Small focused classes; no god objects; no giant id switches.
- **Namespace ≠ type-name traps:** the `Inventory` and `Equipment` classes live in namespace `Dungeons.Items` on purpose (a class named the same as its namespace breaks callers). Watch for this.
- Add/keep Core unit tests for deterministic behavior. Content should be validated by tests (runtime load-time validation is a known TODO).

## Workflow rules
- Work in tested increments; keep `dotnet build` + `dotnet test` green after each. Verify with `dotnet build InTheDungeonsWeDie.slnx` and `dotnet test` (Godot is not on PATH here — the game window runs from the user's editor).
- Only .NET 10 SDK is installed; everything targets `net8.0` (Godot 4.7 baseline) and relies on roll-forward. Don't change TFMs casually.
- Commit only when the user asks. Solo project — commits go on `main`. End commit messages with the Co-Authored-By trailer.
- Milestones/phases are done one at a time, plan-first, smallest coherent slice. Don't silently redesign major systems; surface contradictions.
- Persist important cross-session knowledge into the handoff docs above.

## Guiding question
"How does this make preparing for, exploring, surviving, mastering, or extracting from a Realm more interesting?" If there's no convincing answer, reconsider the feature.
