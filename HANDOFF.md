# HANDOFF.md

For the next Claude session. Read `CLAUDE.md`, then this.

**Entry-point docs — read these before anything else:**
- **`docs/game-overview.md`** — the whole game on one map (player's side), with
  BUILT / PARTIAL / DESIGNED / UNRESOLVED marks.
- **`docs/code-map.md`** — the whole repo on one map: layers, entry points, a card per
  subsystem, **"Where do I change X?"**, and the do-not-rename persistent-identifier list.
- **`docs/crafting-overview.md`** — the crafting stack end to end. **§15 is the
  design-word ↔ code-name bridge and you will need it** (see the ⚠ below).
- **`docs/professions.md`** — rewritten this context. The 20-profession system as it actually
  ships, including the active layer and offline progression.

`docs/GDD.md` is still the deepest single design document. For the D30 presentation rule see
`docs/presentation-architecture.md`; decisions are in `DECISIONS.md`.

## Repo / build state
- Branch `main`. Last commit **`bc2e267`** — the 20-profession expansion pass (85 files,
  +6858/−487). **Not pushed** — the user has not asked.
- Green at handoff: **`dotnet build InTheDungeonsWeDie.slnx` clean (0 warnings) ·
  `dotnet test` → 847 passing** (765 at the start of this context).
- ⚠ **Save schema is now v7.** `SaveData` gained `SavedAtUnixSeconds`, `PassiveActionId`,
  `FarmingPlots`, `TrainingCourse`. **A v6 save still loads** — it arrives with no passive
  action, nothing planted and an empty course, which is the state a new game starts in, so no
  migration step was needed. A test pins that.
- **`GDD/` is the user's personal folder and is in `.gitignore`.** Leave it alone.
- Godot is **not** on PATH — verify with `dotnet build`/`dotnet test`. The user runs the game
  from their Godot 4.7.1 editor and checks UI visually.
- **Workflow (user-set):** concise reports, plan → approve → build → report. Commit only when
  asked. Do not update this HANDOFF mid-session — only when the user says the context is ending.

---

## ⚠️ READ THIS FIRST — the crafting vocabulary

The crafting code was renamed for readability in `8824f66`. **If you grep for `ReactionEngine`,
`Genome`, `Potency` or `Integrity` in the C# you will find nothing.** The design docs, the GDD,
the player UI and the Reaction Log still use the old words on purpose.

```
Integrity → Workability            Potency  → MaterialStrength
Process   → CraftingAction         Channel  → AffectedQualities
Form      → EquipmentBlueprint     Aperture → TraitExpression
Genome    → ItemPotential          Pressure → MaterialInfluence
ReactionEngine   → MaterialTransformationEngine   (Resolve/Project → RunCraft/PreviewCraft)
ReactionAlgebra  → MaterialTransformationRules
FabricationEngine→ EquipmentAssemblyEngine        (Fabricate/Project → Assemble/Preview)
AffixRoller      → ModifierGenerator
MaterialProfile  → MaterialState                  (Resolve → StateOf)
```

Full table, including what deliberately did **not** move, in `docs/crafting-overview.md` §15.
Player-facing text, save keys and content ids stayed put on purpose — **offer to change displayed
wording, don't just do it.**

---

## ⭐ START HERE — two things, in this order

### 1. One open question the user has not answered

**The D29.3 essence allowlist.** The 20-profession pass added nine essence-bearing profession
faucets and the audit test caught them. They were allowlisted **with the argument stated, not
with the user's agreement** — the user said "commit this" without responding to the flag. The
argument on file: a level-45+ rung is not competing with Realm extraction for the same player at
the same time. The nine, with gates:

```
static_charge   Fishing 34        cinder_shard / rime_shard / ember_core   Mining 45
emberwood_log / emberbark          Forestry 50
livingbark_log / spiritwood_log    Forestry 62
soul_gem                           Thieving 58
```

`C2cAuditTests.ProfessionFaucetsYieldEssenceOnlyFromTheAuditedAllowlist` carries them with
comments; `NewEssenceFaucetsSitBehindDeepLevelGates` forces every new one to level 30+. **Ask
whether this is the call the user wants** — pulling them back out is a content edit, not a
refactor. Same audit now also covers opportunity payloads, which it could not see before.

### 2. C2c's playtest half — still the user's, still unchanged

Play the full loop (mine → smelt → infuse → attune → fabricate → roll → equip → fight with
thorns/ailments/parry live), then land the whole balance backlog in one pass. **Do not retune
anything before the user reports play feel** — standing decision. The backlog now also carries
everything this context added: profession intervals/XP across all twenty, opportunity
discovery/risk/cost, offline caps, plot grow times, course bonus magnitudes.

**Editor verification pending (written, never user-verified):**
- **NEW: the whole rebuilt Professions tab** — profession picker + per-profession ladder with
  level gates, the Discover/Pursue card, the Farming plots panel, the training-course fitter,
  the pursuit bar sharing the passive bar. None of this has been seen running.
- The older list: the entire R-track UI (bench semantic grammar + Advanced toggle, glyph
  rendering — any tofu box is one JSON edit in `game/data/properties/properties.json` —
  fabrication slot-fit lines + preview card, the Latest-work reveal + debug Reroll, the Parry
  button), Techniques panel, the three goblin fights, combat card height floor, Char Lab hooks.

---

## This context, in one commit: `bc2e267`

**8 professions → 20; 26 actions → 194.** Full design in `docs/professions.md`. What matters
architecturally:

1. **The active layer is one mechanism, not twenty minigames.** `opportunities[]` nested on an
   action; an active attempt returns an *offer*, and `ProfessionSystem.PursueOpportunity`
   resolves the gamble. **Core resolves instantly; the client owns the clock** —
   `GameRoot.PursuePendingOpportunity` schedules `extraIntervalTicks` on the TickEngine. Keep
   that seam; putting scheduling in Core would drag `TickEngine` into every profession test.
2. **Passive cannot roll for opportunities — structurally.** The discovery roll only exists on
   the active path in `ActionResolver`. That is what makes "fewer rare outcomes" a fact about
   the code rather than a tuning number. Do not "fix" it into a probability.
3. **Offline runs the same `Execute`.** `OfflineProgressCalculator` loops it at performance 0,
   so offline can never drift from live passive. Capped at 12h / 20k completions.
4. **Two bespoke systems, and only two.** `FarmingPlots` (parallel; seed paid at planting,
   harvest **prepaid** via `CompletePrepaidAction`; `GameRoot.RebasePlantedCrops` moves
   remaining grow time onto the new session's clock on load) and `TrainingCourse` (Agility;
   `ActiveBonuses()` is what the rest of the game would read).
5. **`ProfessionCategory.Crafting` → `Processing`** — it collided with `Dungeons.Crafting`, the
   bench. Definition data, not a save key.
6. **`ProfessionEcosystemTests` is the guard.** It fails a profession that consumes nothing or
   feeds nothing. A new profession must cross-feed or the suite goes red — that is deliberate.

### Fences that fired, and what was done

- **`state:attuned` was a valid state tag with no `WorkabilityByState` entry**, silently reading
  as untouched (100). Runecrafting's runes are the first *authored* material to carry it; the
  entry exists now at 75. Pre-existing gap, exposed by new content.
- **Seven materials were consumed by actions and produced by none** — `springwater`, `wood_ash`,
  `lye`, `glass`, `parchment`, `mortar`, `tallow`. All pre-existing. All have sources now
  (Farming draws water, Artifice melts glass and presses parchment, Herblore renders tallow).
- **Alchemy lost its level-1 rung** when herb powder moved to Herblore; Leach Lye replaced it.
- **A real bug in `FarmingPlots.Plant`**: it validated against `ProfessionSystem`'s inventory
  (the *active* bag — the unsecured run inventory inside a Realm) and then took the seed out of
  the Stash. Now checks the bag it spends. Pinned by test.

---

## ⚠️ Deferred by explicit decision — do not relitigate, do not silently fix

- **Balance, wholesale** (see START HERE). Every profession number is breadth-not-balance.
- **Player-facing crafting wording** (Integrity/Potency/process on screen) — offer, don't do.
- **Casting-speed scaling** (GDD §18 #16) — at the balance pass.
- **The Fighter identity hook** (§18 #15) — NEEDS DESIGN, parked.
- **Stored retaliation, inversion, ignore-fraction, Exotic rare-roll** — E7 (Exotic tier).
- **Operations + Overreach + Anomalous** — E7. **Signature affixes** — need crafting P4.
- **Sealed uniques + relic materials** — post-slice content (D28 fencing recorded).
- **No currency for Thieving.** There is no economy (NEEDS DESIGN), and a coin nothing spends
  would be the invented profession-specific resource the design forbids. Thieves take precious
  metal, gems, keys and paperwork — all existing materials.
- **`MainMvpUI` → a better name** — the `.tscn`/`.uid` reference the script and Godot is not on
  PATH here. An editor-side job.
- **`AffixDefinition`/`RolledAffix`/`Dungeons.Affixes`** keep their names (D-17).

## Known debt and filed items

- **NEW: `CourseBonusKeys` are declared, aggregated and displayed — and nothing reads them.**
  Realm travel, hazards, extraction and opportunity risk all ignore the Agility course today.
  E6 is where they get consumed. Same for the tool components (Smithing's `tool_head`,
  Artifice's haft/mechanisms/lenses): the parts exist, the two worn slots do not.
- **NEW: Cooking is the one documented dead end.** Its consumer is the player via consumable
  forms, which have not shipped. `ProfessionEcosystemTests.NoProfessionIsADeadEnd` names it
  explicitly rather than weakening the rule — **when consumables land, delete the exception and
  the test should still pass.**
- **NEW: Cartography's knowledge gains are all `realm.dark_forest`** — the only realm there is.
  Realm Knowledge counts up and still unlocks nothing.
- **NEW: Fletching makes parts for bows and projectiles that have no forms yet** (form
  acquisition, M6). The components carry both their source form and their worked form
  (`form:wood` + `form:shaft`) so fabrication can already accept them.
- Zeroing every affix's `chance_weight.base` still passes the suite — that lever is unguarded.
  Pre-existing; worth a test.
- Full `capped/raw` on a character sheet + preparation screen (D-05a's other two surfaces); the
  §2.3 numeric ItemPotential Readout panel.
- Two provisional crafting constants; `PropertyDefinition.transferable` unconsumed; response
  properties drop on transformation; `InappropriateOptimismRule` orphaned;
  `StatusController.ModifierTotal` display-only; **`GameRoot` is now ~1,900 lines** —
  Application-layer extraction deferred again (this context added ~240 lines of thin forwards
  and application glue to it, which makes the case stronger, not weaker).
- `PROJECT_STATE.md` / `SYSTEM_INDEX.md` predate the R-track, the crafting rename **and now the
  profession pass**. (GDD / ROADMAP / DECISIONS / the four entry-point docs / this file are
  current.)
- **`HANDOFF.md` was uncommitted for a whole context** before this one — the previous session
  wrote it and it never made it into a commit. Worth checking `git status` at handoff time.

## The rules that keep this tractable — do not erode

- **Code optimizes for human comprehension (CLAUDE.md rule 8).** Expressive names, one name per
  concept project-wide, no magic numbers, no behaviour-selecting booleans, and **code-symbol
  renaming is not persistent-identifier renaming** — `docs/code-map.md` §12. That list grew this
  context: `TrainingSlot` member names, **action ids** (mastery is keyed by them in every save),
  and `CourseBonusKeys` values.
- **Three languages (D30, rule 7):** raw simulation values never on normal play surfaces;
  `Dungeons.Presentation` is the one path to player-facing text, one-way, unit-tested; **a
  player-facing modifier ships only when its mechanic resolves.** `AssayLens` joined that
  namespace — it redacts a reading, it never changes one.
- **No recipe, ever.** **A Prefix may never reference a Base.** **Every Base distributes the
  same growth budget.** **No class-check condition kind (D25).** **Enemy identity composes
  (D26).** **Innates never reroll (U-7).** One affix per family per item (§3.5).
- **Professions are an ecosystem, not twenty XP bars** — enforced by `ProfessionEcosystemTests`.
- **D-12** never default a `ModifierContext` · **D-08** Resolve, not per-control chances ·
  **D-06** on-block hooks listen to `Blocked` (both outcomes) · **D-01** lane movement is
  always `convert`/`addAsExtra` with a fraction · ailment ticks never proc.
- Keep `dotnet test` green (**847**) and the build at **0 warnings**. Content is data; every new
  content type ships with validator rules + failing-content tests. Commit only when asked;
  `main`; Co-Authored-By trailer.

## The method note that keeps paying

Previous contexts: rendering a worked example and reading the output, and perturbation testing
when touching data keys. **This context it was the audit tests themselves.** Every real defect
found — the essence faucets, the missing `attuned` workability, the seven unsourced materials,
Alchemy's missing level-1 rung, the wrong-inventory bug in `Plant` — came from a test written to
express a *design rule* rather than to cover a code path. Two of them (`NoProfessionIsADeadEnd`,
`EveryProfessionCanBeStartedAtLevelOne`) failed on their very first run against content that
looked finished.

Corollary worth keeping: **when a rule has one honest exception, name it in the test rather than
weakening the assertion.** Cooking is exempt from the dead-end rule by name, with the milestone
that removes the exemption written next to it. A weakened rule stops catching the next mistake;
a named exception is a to-do list.
