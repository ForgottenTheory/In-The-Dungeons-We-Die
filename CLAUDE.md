# CLAUDE.md — In The Dungeons We Die

Permanent project instructions and development rules. **Keep this file concise — it is rules, not documentation.** For current state, systems, decisions, and next steps read the handoff docs (below) and `/docs`.

## What this is
A progression-heavy extraction RPG (Melvor-style professions + For-The-King-2 spatial realms + extraction risk/loss + tick-based tactical combat + emergent crafting). Godot 4.7 (.NET) client over an engine-independent C# domain. The playable MVP vertical slice is complete; the **identity crafting system** (D42–D54) is the crafting stack — the original property system was deleted whole in migration Phase 7.

## Read these first (handoff docs, kept current)
- `docs/game-overview.md` — the top-down map of the game: every system, how they connect, and how far each one got. **Start here.**
- `docs/code-map.md` — the developer's technical map: layers, entry points, every subsystem, and **"Where do I change X?"**. Includes the do-not-rename persistent-identifier list.
- `docs/crafting-overview.md` — the identity crafting stack (materials → verb bench → forge → equip seam) in one place, with real content counts and every tuning constant located.
- `docs/identity-foundation.md` — the crafting foundation: roster, grammar, capacity/condition, the item-effect pipeline, the migration record.
- `docs/loot.md` — the reward layer: the one table shape every source shares, how enemy loot composes, the active/passive and depth gates, the elite/boss seam, gold, and the fences the tests hold.
- `DECISIONS.md` — architectural/gameplay decisions **and why** (+ rejected options).
- `ROADMAP.md` — remaining work and order.
- `HANDOFF.md` — where we stopped and exact next steps.
- `docs/effect-foundation.md` — the settled effect/damage/status/move architecture (26 decisions, §12).

## Architecture rules (hard invariants)
1. **Domain-first, enforced by the assembly split.** Gameplay logic lives in `core/` (`InTheDungeonsWeDie.Core`, namespace `Dungeons.*`, `net8.0`) and MUST NOT reference Godot. The Godot project `game/` references Core; never the reverse. Tests reference Core only.
2. **Godot is the client.** Use it for UI, input, scenes, file access (`res://`/`user://`), presentation. `GameRoot` (autoload) is the composition root + application glue; it wires systems and exposes commands/queries/events to the UI. Do not put authoritative gameplay rules in UI or `GameRoot`.
3. **Data-driven content.** Definitions are JSON under `game/data/<type>/`, loaded via `ContentLoader` (Godot) into `DataStore<T>` (Core, path-agnostic — it takes JSON text, never file paths). Use stable namespaced ids (`material.oak_bark`, `equip.iron_sword`).
4. **Definitions vs runtime state are separate.** `IItemDefinition`/`MaterialDefinition`/`EquipmentDefinition` describe kinds; `ItemInstance` is a specific owned item with derived properties. Raw stackables stay quantity-based; anything with derived/unique properties is an instance. Never mutate definitions.
5. **Deterministic tick simulation.** One shared `TickEngine` drives combat + passive gathering; `GameRoot._Process` advances it. Inject `IRandomSource` (seeded) — no scattered global RNG.
6. **Crafting speaks identities, not numbers.** A material is capacity + identities + latents + base stats + profile (`docs/identity-foundation.md`); effects are trigger→behavior→payload sentences over vocabulary registries. New payloads/actions/forms are data, not code — and every vocabulary entry must bind to machinery that already resolves (the D30 fence). Combat reads neutral `ResolvedMove`s and an `ArmorProfile` (via `EquipmentResolver`), never equipment types.
7. **Three languages (D30).** Raw simulation values (0–100 properties, rates, coefficients) never appear on normal play surfaces — Advanced/Assay/labs only. The only path from simulation state to player-facing crafting/item text is the semantic layer (`Dungeons.Presentation`): one-way, deterministic, unit-tested. Items speak gameplay language (damage, crit, Thorns…), never property language. A player-facing modifier ships only when its mechanic resolves. See `docs/presentation-architecture.md`.
8. **Code optimizes for human comprehension.** See the next section — it is a hard rule, not a style preference.

## Readability (permanent rule)

**CODE MUST OPTIMIZE FOR HUMAN COMPREHENSION.** A developer unfamiliar with the implementation should usually be able to infer the purpose of a class, method, parameter, or important variable **from its name alone**, without opening another file.

Prefer: explicit names · clear single responsibility · straightforward control flow · small understandable methods · meaningful domain terminology.
Over: shorthand · cleverness · dense abstractions · unnecessary indirection · premature generalization.

- **Expressive names.** `remainingMaterialIntegrity`, `finalDamageAfterResistance`, `eligibleModifierDefinitions`, `ResolveIncomingDamage()`, `EvaluateCraftingReaction()` — never `x`, `tmp`, `res`, `calc`, `mgr`, `val`, `mat`, `gen`, `DoThing()`, `Handle()`, `Process()`. Not absurdly long either: name for the reader, not for the word count.
- **No shorthand** unless it is a universal term (`ID`, `UI`, `AI`, `HP`, `XP`) — and prefer clarity even then. A short lambda parameter in a one-line LINQ chain is fine; a name that lives for 30 lines is not.
- **One name per concept, project-wide.** *Suffix* is a character component. *Modifier* is the player-facing word for an affix. *Pool* is a `ResourcePool`. *Lane* is a resistance lane. Reusing a domain word for something else is the most expensive naming mistake in this codebase.
- **No magic numbers.** Every tuned value goes in the relevant `*Tuning` class with a doc comment saying what it means. Twelve `*Tuning` classes is not too many.
- **No behaviour-selecting boolean parameters.** Use a small enum (see `DefensiveStance`).
- **Comment the *why*, especially the rejected alternative.** Mechanics are readable from the code; the reasoning is not.
- **Delete a doc comment the moment the code moves out from under it.** A stale comment is worse than none.
- **Separate code-symbol renaming from persistent-identifier renaming.** Save-file keys (`SaveData` / `*Save` properties), content ids, property names, modifier keys, lane/tag strings and anything hashed into a signature are **data**. Renaming them breaks saves or content. Document the desired rename instead; see `docs/code-map.md §12` for the full list.

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

## Crafting vocabulary (identity system, D42–D54)
One name per concept, project-wide: an **identity** is a material identity (never character or enemy identity) · a **sentence** is trigger→behavior→payload · a **Signature** is the *earned* special layer, never the blanket word for generated effects (D50) · **capacity** is material-side slots, a form's **identity_cap** is item-side expression — neither implies the other (D51) · **rank/rung** words are basic→improved→advanced→build-changing, never player-facing numerals (D44) · **Condition** (Pristine→Fragile) and **Stability** (Stable→Volatile) enum words are the player words. The pre-redesign C# vocabulary (Workability, MaterialStrength, ItemPotential, the reaction/assembly engines) was deleted with its system in Phase 7.
