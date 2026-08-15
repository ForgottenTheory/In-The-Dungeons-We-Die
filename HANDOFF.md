# HANDOFF.md

For the next Claude session. Read `CLAUDE.md`, then this, then `PROJECT_STATE.md` / `SYSTEM_INDEX.md` / `DECISIONS.md` / `ROADMAP.md`. Then read **`docs/emergent-item-system.md`** in full — it is the accepted spec for the task below and supersedes `docs/crafting.md §17`.

## Repo / build state
- Branch `main`, latest commit **`c84cf83`** (pre-expansion cleanup pass). Working tree clean.
- `dotnet build InTheDungeonsWeDie.slnx` clean (0 warnings); `dotnet test` → **193 passing**. Godot is **not** on PATH — verify via `dotnet build`/`dotnet test` only; the user runs the game from their Godot 4.7.1 editor and verifies UI visually.
- Recent commits: `c84cf83` cleanup · `a15df87` emergent P0 + tabbed UI · `05a9f29` base-resource ecosystem (~470 materials) · `77ae8df` handoff docs.

## Where we are
MVP vertical slice complete; equipment/item-instance system done; **~470-material library** authored; **emergent-item-system P0 done** (tag `family:value` namespace, `PropertyDefinition` registry as the single source of truth for property names, `ResistanceCalculator`, tag/property validation); and a **cleanup/audit pass** just landed (`ContentBundle` + `ContentLoader.LoadAll`, bundle-based `ContentValidator`, id conventions, typed `CharacterBuild` ids, gameplay pulled out of `GameRoot`/UI into Core). See `DECISIONS.md` D16–D19.

---

## THE TASK: Emergent crafting — **P1** (the universal reaction engine)

Build P1 from `docs/emergent-item-system.md §20`. **This is large — plan-first with the user and build in tested increments.** Read these spec sections closely: **§6** (potency/integrity/generation), **§7** (processes/channels/medium), **§8** (the algebra — the core), **§12** (identity/quantization/registry/variance), **§13** (naming v1), **§15.3** (Reaction Log), **§18** (data model), **§20/§21** (phasing + open decisions).

### In scope (P1)
`ProcessDefinition` (data) + the universal reaction algebra (acceptance/release → convergence → off-channel drift+prune → opposition/annihilation) + **potency** (weighted mean) + **integrity** (transformation budget, incl. **destruction at 0 + byproducts + the pre-commit integrity-projection UI**, §6.2c — this UI is P1 scope, not later) + **quantization→signature→archetype registry** + **naming v1** + **Reaction Log** + a minimal first-discovery flag. **A playable emergent material system with zero authored content beyond processes.**

### Explicitly OUT of P1 (do NOT build)
- **Traits** (P2), **Essence + resonance/strain + `arcane` amplification + `Attune` process** (P3), **Signature/Chain reactions** (P4), **Fabrication → equipment/consumables** (P5), **Codex/known-rules journal/Assay/renaming** (P6). §8.4 essence transfer is P3 — the P1 algebra runs channel convergence, off-channel, opposition, potency, integrity **only**. Ship the 7 mundane starter processes; **`Attune` is P3** (it raises `resonance` for essence).

### ⚠️ Critical architectural reconciliation to settle FIRST
The spec's **§0 Decision 3** says emergent materials are **stackable runtime `ItemDefinition`s registered by signature**, NOT per-unit `ItemInstance`s. This **contradicts** `docs/itemization.md` D6 ("any generated material becomes a unique `ItemInstance`") and the current `CraftingExperimentSystem` (which mints Barkbound Iron as an `ItemInstance` via `resultIsInstance`). **The emergent doc wins** (per its status line). So P1 introduces an **emergent archetype registry**: a craft result is quantized → hashed → looked up/registered as a stackable `MaterialDefinition`, and identical results **stack**. `ItemInstance` stays for **equipment only** (P5). Update `itemization.md` D6 and `DECISIONS.md` when you do this. This is the single most important shift in P1 — get it right before writing the algebra.

### Open decisions to settle with the user before/while building
1. **Base-material potency/integrity source.** The ~470 authored materials have no `potency`/`integrity`/`generation`. P1's potency math (§6.1) needs a base potency per material. Options: flat default (e.g. 50), **derive from the `rarity:` tag** (common→low … exceptional→high) [recommended cheap start], or author per-material. Integrity defaults to 100, generation to 1. Decide with the user.
2. **Quantization granularity (§12.2).** Start at 5-point buckets; **highest-risk tuning number** — too coarse collapses the space, too fine floods the registry. Must be tuned empirically once it runs.
3. **`MaterialProfile` placement (§18).** Recommend extending `MaterialDefinition` with potency/integrity/generation/lineage/signature (authored bases get the defaults from #1; emergent archetypes get computed values). Keep authored material JSON unchanged (defaults fill in).
4. **Fate of the 2 existing interactions.** `interaction.barkbound_iron` (becomes an emergent result — likely retire the fixed recipe) and `interaction.healing_salve` (a consumable recipe — fabrication is P5, so keep a minimal consumable path until then, or shim it). Decide what the old `CraftingExperimentSystem`/`CraftingDerivation`/`CraftingInteractionDefinition` become — they are the prototype P1 replaces; don't 10x them.
5. **Registry persistence.** The archetype registry lives in the save (§12.4) behind `IEmergentRegistry`; bump `SaveData` schema (currently v3 → v4). Codex stays separate/per-save (P6).

### Suggested build order (each slice: `dotnet build` + `dotnet test` green)
1. **`MaterialProfile` + base defaults** (decision #1/#3) + tests. No behaviour change to existing content.
2. **`ProcessDefinition`** (data type + `game/data/processes/*.json`, 7 processes) + load via `ContentBundle`/`ContentLoader.LoadAll` + validate (channel props known, medium valid, tags resolve). This is why the cleanup added the content registry — adding this type is one store + one load line.
3. **The algebra** as pure Core functions (`ReactionEngine` / `IReactionEngine.Resolve`): acceptance/release (8.1) → convergence (8.2) → off-channel drift+prune (8.3) → opposition/annihilation (8.5). Heavily unit-tested with the §19 worked examples (Warmed Iron, Emberveined-minus-traits) as fixtures — but note traits are P2, so the §19 examples' trait/signature/essence steps are stubbed.
4. **Potency + integrity + effective-instability + generation** (§6) + tests (weighted-mean can't inflate; integrity cost ∝ Δstate; destruction at 0 yields byproducts).
5. **Quantization → signature → `IEmergentRegistry`** (§12) + save-backed store + **seeded variance perturbation** (§12.3). Determinism tests: same inputs+process+seed → same archetype id.
6. **Naming v1** (§13) — state-based; without traits it's `[intensity] [root noun] [form noun]` from lineage+form; keep it ≤3 words, no numbers.
7. **Reaction Log** (§15.3) — structured, human-readable step trace (required scope; it's the tutorial + debugger + future codex content).
8. **`CraftRequest → CraftOutcome` orchestration** replacing the fixed matcher; wire into `GameRoot` + a **Crafting tab UI** to pick substrate/reagents/process and show the **pre-commit integrity projection + destruction warning** (§6.2c). Godot-side; user verifies visually.

Determinism invariant (§12.5): everything pure **except** the execution-quality roll and the variance perturbation, both through the seeded `IRandomSource`. This keeps it unit-testable.

## Guardrails
- Keep `dotnet test` green (193 now) in tested increments. Add Core tests for the algebra/registry/naming (pure → easy).
- Core stays Godot-free; nothing authoritative in `GameRoot`/UI (see the cleanup — leaked gameplay was just pulled out; don't re-introduce it).
- Do **not** hardcode per-combination recipes or reaction rules; the algebra is the source of truth. No traits/essence/signatures yet.
- Commit only when the user asks; on `main`, end messages with the Co-Authored-By trailer.
- `GameRoot` is ~960 lines; the **application-layer extraction** is the other deferred cleanup (separate from P1) — don't let P1 command handlers pile into `GameRoot` without a plan.
