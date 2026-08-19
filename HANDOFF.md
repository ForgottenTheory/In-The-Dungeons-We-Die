# HANDOFF.md

For the next Claude session. Read `CLAUDE.md`, then this.

**Entry-point docs — read these before anything else:**
- **`docs/game-overview.md`** — the whole game on one map (player's side), with
  BUILT / PARTIAL / DESIGNED / UNRESOLVED marks.
- **`docs/code-map.md`** — the whole repo on one map: layers, entry points, a card per
  subsystem, **"Where do I change X?"**, and the do-not-rename persistent-identifier list.
  **§10.13b** is auto-combat, **§10.14** is professions (and the benefit seam), **§10.16b** is
  Realm preparation, **§10.16c** is the seven progression tracks.
- **`docs/crafting-overview.md`** — the crafting stack end to end. **§15 is the
  design-word ↔ code-name bridge and you will need it** (see the ⚠ below).
- **`docs/professions.md`** — the 20-profession system as it actually ships.
- **`docs/loot.md`** — the reward layer, one table shape for every source.

`docs/GDD.md` is still the deepest single design document. For the D30 presentation rule see
`docs/presentation-architecture.md`; decisions are in `DECISIONS.md` (now through **D41**, plus
**D29.3 resolved** at the end).

## Repo / build state
- Branch `main`. Two commits this context, **not pushed** — pushing is the user's job.
  - **`ce5430c`** — Phase 10, offline + automation (51 files, +3874/−364).
  - **`27e6dcd`** — D29.3 settled (12 files, content + one test; **no code changed**).
- Green at handoff: **`dotnet build InTheDungeonsWeDie.slnx` clean (0 warnings) ·
  `dotnet test` → 1191 passing** (1121 at the start of this context).
- ⚠ **Save schema is still v11 — Phase 10 added no field.** `PassiveActionId` now carries the
  *standing selection* rather than the running action, which is the same key meaning the same
  thing slightly more honestly. The one real migration in the project's history is still v9's
  `Armor` → `Body`.
- **`GDD/` is the user's personal folder and is in `.gitignore`.** Leave it alone.
- Godot is **not** on PATH — verify with `dotnet build`/`dotnet test`. The user runs the game
  from their Godot 4.7.1 editor and checks UI visually.
- **Workflow (user-set):** concise reports, plan → approve → build → report. Commit only when
  asked. Do not update this HANDOFF mid-session — only when the user says the context is ending.
- **Do not spawn subagents** unless the user asks for them (standing instruction).

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

**One more rename landed this context:** `MasteryBenefitKind` → **`ProfessionBenefitKind`**. The
six quantities are no longer mastery's alone — synergies pay into them too, and E6's tools will.
**Member names did not move**; they are the JSON `kind` values in `mastery/` and `synergies/`.

---

## ⭐ START HERE — two things, in this order

*(The third item — the D29.3 essence allowlist — was answered this context. It is settled, and
the argument is in `DECISIONS.md`. Do not reopen it.)*

### 1. The playtest half is the user's, and it is now the whole bottleneck

Everything the loop needs is built, **including the idle half**. Nobody has played it. Play the
full loop — prepare a loadout → enter → fight → extract → mine/smelt/infuse/attune/fabricate/roll
→ equip → go again, and leave something training overnight — then land the whole balance backlog
in one pass. **Do not retune anything before the user reports play feel** — standing decision,
guarding a large backlog: profession intervals/XP across all twenty, opportunity odds/risk/cost,
offline caps, plot grow times, course bonuses, Fireball, Bastion, casting-speed (GDD §18 #16),
fabrication constants, affix-roll odds, the whole Dark Forest, the mastery ladder, the character
XP curve, the two knowledge thresholds, **and everything Phase 10 added** (13 synergy rates, both
global unlock thresholds, three auto-combat brains, and the seven re-shaped D29.3 opportunities).

### 2. Editor verification pending — the list is long and none of it has been seen running

- **NEW (Phase 10):** the **While-you-were-away panel** at the top of the Hideout tab
  (`game/ui/AwaySummaryPanel.cs`, appears only after a Load that earned something, dismissible);
  the **auto-combat toggle + brain picker** on the Combat tab with its explanation line; the
  three-state passive label (`Passive:` / `Waiting: … (no materials)` / `(idle)`); the
  **"Helped by: …" synergy line** on each profession ladder; and **autosave on quit** — close the
  window with a save file present and confirm the next Load reports the absence.
- **NEW (Phase 10), the one worth watching closely:** turn auto-combat on and watch a Brute
  fight. It should block reliably, never negate a hit outright, and never parry even with a
  Buckler equipped. If it *does* perfect-block, something is wrong with the reaction arithmetic
  and `AutoCombatTests.ItNeverLandsAPerfectBlock` is lying.
- The older list, still unverified: the whole Realm Preparation screen (Phase 7), the mastery
  readout on every ladder row, character level on the sheet and the preparation header, the
  deep-entry depth picker (only at 900 Dark Forest knowledge), the rebuilt Professions/Hideout
  tab (station picker, ladders, Discover/Pursue card, Farming plots, course fitter), the entire
  R-track UI (bench semantic grammar + Advanced toggle, glyph rendering — any tofu box is one
  JSON edit in `game/data/properties/properties.json` — fabrication slot-fit lines + preview
  card, the Latest-work reveal + debug Reroll, the Parry button), Techniques panel, the goblin
  fights, combat card height floor, Char Lab hooks.

---

## This context, in two commits

### `ce5430c` — Phase 10: offline + automation (D41)

**The offline payout already worked.** `OfflineProgressCalculator` has looped the shared
`ProfessionSystem.Execute` since P4. What was missing was everything that makes idle play a
*loop*.

1. **One benefit seam, now three sources.** `ProfessionBenefits` folds the mastery ladder and a
   new synergy table into the single question the execution path asks. Because `ActionResolver`
   and `ProfessionSystem` already asked exactly once per benefit, **cross-profession and global
   bonuses arrived with no line of change in either file** — and **E6's worn tools are a third
   field on it and nothing downstream.**
2. **One content type covers both hooks**, because they are one statement. `synergies/` has 13
   cross-profession rows (each following a chain the professions already have) and 2 global rows
   that read **total** profession level. Same formula as the mastery ladder; source and target
   must differ, validated.
3. **Auto-repeat.** `PassiveProfessionRunner` holds `SelectedActionId` through a stall
   (Idle/Working/Waiting) and resumes by itself. **Temporary problems wait; permanent ones
   refuse.** Only `Stop()` clears it.
4. **The return is a read-model.** `AwayProgress` aggregates one absence — completions, crops,
   items merged per id, XP, mastery, **levels gained** — and `Presentation/AwayReadout` owns every
   word, so the console line and the panel cannot disagree. Autosave on quit, guarded: it refuses
   when no save file exists (a fresh game must not overwrite a real save) and inside a Realm.
5. **Auto-combat is the player on the enemy AI machinery, literally.** `AutoCombatPilot.Engage`
   puts the brain's `AiRuleSpec` rules on `Combatant.Ai` and asks
   `CombatEncounter.ChooseMoveFor` — the enemy method. **No second resolver, no damage
   multiplier.** Its whole handicap is `reaction_ticks`: the stance commits at
   `max(noticed + R, impact − R)`, so committing R early puts every tight window out of reach,
   and anything arriving within 2R is unanswerable. The reaction floor is a **validator rule**
   derived from the windows themselves.

### `27e6dcd` — D29.3 settled: profession essence is active-only

The eleven-id allowlist is **deleted**. Essence may reach a profession **only as an opportunity
payload**, and only the active path rolls opportunities — so "you cannot bank essence while idle"
is a fact about the code. Seven rungs moved; two of them (Emberwood 50, Livingbark 62) had been
handing over essence on **every** completion, which Phase 10's auto-repeat turned into thousands
of unattended units per absence. **32 → 36 opportunities.** Full argument in `DECISIONS.md`.

### Fences that fired, and what was done

- **`PassiveProfessionRunnerTests.CannotStartWithoutInputs` failed, correctly.** It expressed the
  old rule. Rewritten to express the new one rather than weakened — that is the difference
  between a test that documents a design and a test that documents an implementation.
- **The first auto-combat model was wrong and a test caught it.** Guarding only against acting
  *too early* meant a 5-tick jab was blocked, because `impact − R` was already in the past. The
  `noticed + R` half is what makes fast attacks unanswerable, and it fell out of fixing the bug.
- **`TheRosterMeetsItsStatedScale` caught the opportunity count twice** — once for Phase 10's
  synergy count, once for D29.3's four new opportunities. The pinned-scale test is doing exactly
  its job; update it *and* the four docs that quote the number.
- **A stale allowlist entry was invisible by construction.** `material.eel_skin` carries no
  essence and never did, but a `found ⊆ allowed` assertion can never notice a dead entry. The
  structural rule that replaced it cannot hold one.

---

## ⚠️ Deferred by explicit decision — do not relitigate, do not silently fix

- **Balance, wholesale** (see START HERE). Every profession, mastery, character-XP, Realm,
  synergy and auto-combat number is breadth-not-balance.
- **Form/schematic acquisition (D29.2)** — `material.schematic_fragment` drops from eight tables
  and binds to no form. **The one progression track nothing reads**, and
  `ProgressionEcosystemTests` exempts it **by name**. Building it means authoring acquisition
  onto 23 forms and deciding which a fresh player already knows — a balance and soft-lock
  decision, not an integration. **When it ships, delete the exemption and the roll-call should
  still pass.**
- **Profession tools** — E6. Tool slots, tool forms and the yield pipeline. **The seam is now
  built:** `ProfessionBenefits` folds every source of the six quantities into one answer, so
  tools are a third field on it and no change downstream. `ProfessionBenefitKind` still uses the
  same six names as the unread `profession.*` modifier keys, so the merge needs no rename.
- **Fully unattended Realm runs.** Auto-combat is **live-only** — it plays fights you are
  watching. Travel, extraction decisions and the run bag are a separate problem and were
  deliberately not started.
- **Player-facing crafting wording** (Integrity/Potency/process on screen) — offer, don't do.
- **Casting-speed scaling** (GDD §18 #16) · **the Fighter identity hook** (§18 #15, NEEDS DESIGN).
- **Stored retaliation, inversion, ignore-fraction, Exotic rare-roll, Operations + Overreach +
  Anomalous** — E7. **Signature affixes** — need crafting P4.
- **Sealed uniques + relic materials** — post-slice content (D28 fencing recorded).
- **No currency for Thieving.** There is no economy (NEEDS DESIGN).
- **`MainMvpUI` → a better name** — the `.tscn`/`.uid` reference the script and Godot is not on
  PATH here. An editor-side job.
- **`AffixDefinition`/`RolledAffix`/`Dungeons.Affixes`** keep their names (D-17).

## Known debt and filed items

- **`GameRoot` is now ~2,870 lines** (was ~2,680). This context added the auto-combat commands,
  the away queries and the synergy readout — all thin forwards and application glue, which makes
  the extraction case stronger every time it is deferred.
- **The in-run `KnowledgeIntel` and the pre-run `RealmBriefing` are two implementations of "what
  does knowledge reveal".** They share the thresholds (`RealmKnowledgeLevels`) but not the
  reading, and the in-run one still drops negative resistances (a real weakness the player never
  sees). Unifying them is the obvious next tidy.
- **Three of the five Dark Forest enemies have no readable weakness at all.** Raider, Hexer and
  Grask all read "hit with nothing in particular; burns to nothing; shrugs off toxin", so the
  first rung of the knowledge ladder buys an almost-empty Known Threats panel for 3 of 5 fights.
- **NEW: auto-combat never uses consumables.** It chooses moves and stances only; a healing salve
  in the pack is not something the pilot knows about. Deliberate for this pass (the item path
  goes through the client, not the encounter), and a real gap for unattended play.
- **NEW: `AutoCombatPilot` polls every tick.** One scheduled callback per tick while engaged.
  Fine today; if a fight ever holds several automated actors, the poll is the thing to revisit.
- **Cartography's knowledge gains are all `realm.dark_forest`** — the only realm with content.
- **Cooking is the one documented profession dead end.** `ProfessionEcosystemTests` names it
  explicitly; when consumable forms land, delete the exception and it should still pass.
- **Fletching makes parts for bows and projectiles that have no forms yet** (form acquisition).
- `CourseBonusKeys` are declared, aggregated, displayed — and read by nothing. E6. **Not the same
  thing as a synergy:** a synergy pays into the six profession-benefit quantities, a course bonus
  is standing utility inside a Realm.
- Zeroing every affix's `chance_weight.base` still passes the suite — that lever is unguarded.
- Two provisional crafting constants; `PropertyDefinition.transferable` unconsumed; response
  properties drop on transformation; `InappropriateOptimismRule` orphaned;
  `StatusController.ModifierTotal` display-only.
- **`PROJECT_STATE.md` / `SYSTEM_INDEX.md` are badly stale** — they predate the R-track, the
  crafting rename, the profession pass, M6, and the last three phases. (GDD / ROADMAP /
  DECISIONS / the entry-point docs / this file are current.) **Either refresh them or delete
  them; a stale map is worse than none.**

## The rules that keep this tractable — do not erode

- **Code optimizes for human comprehension (CLAUDE.md rule 8).** Expressive names, one name per
  concept project-wide, no magic numbers, no behaviour-selecting booleans, and **code-symbol
  renaming is not persistent-identifier renaming** — `docs/code-map.md` §12. That list now also
  carries: **action ids** (mastery is keyed by them in every save), `CourseBonusKeys` values, the
  `elite`/`boss` actor tags (`EnemyRanks`), **`ProfessionBenefitKind` member names** (the `kind`
  field of `mastery/` and `synergies/`), and **`DefensiveStance` member names** (the `stance`
  field of every `auto_combat/` defence rule).
- **Three languages (D30, rule 7):** raw simulation values never on normal play surfaces;
  `Dungeons.Presentation` is the one path to player-facing text, one-way, unit-tested; **a
  player-facing surface ships only when its mechanic resolves.** That clause is why the Tools
  panel shows readiness rather than empty tool slots, and why `AwayReadout` speaks in completions
  and items rather than ticks.
- **Progression stays layered (D40).** Character XP is Realm-only. Nothing in the Hideout may
  feed it, or every track collapses into one power number.
- **Automation is disadvantaged by latency, never by damage (D-07, D41).** There is **no second
  combat resolver** and there must never be one; the moment automated play has its own maths,
  passive and active are two balance models wearing one name. A profile quick enough to parry is
  a **load error**, not a comment.
- **Essence is extraction's export (D29.3).** Professions reach it only through opportunity
  payloads; drop tables not at all.
- **No recipe, ever.** **A Prefix may never reference a Base.** **Every Base distributes the same
  growth budget.** **No class-check condition kind (D25).** **Enemy identity composes (D26).**
  **Innates never reroll (U-7).** One affix per family per item (§3.5).
- **Professions are an ecosystem, not twenty XP bars** — `ProfessionEcosystemTests`.
- **D-12** never default a `ModifierContext` · **D-08** Resolve, not per-control chances ·
  **D-06** on-block hooks listen to `Blocked` (both outcomes) · **D-01** lane movement is always
  `convert`/`addAsExtra` with a fraction · ailment ticks never proc.
- Keep `dotnet test` green (**1191**) and the build at **0 warnings**. Content is data; every new
  content type ships with validator rules + failing-content tests per rule. Commit only when
  asked; `main`; Co-Authored-By trailer.

## The method note that keeps paying

Previous contexts: rendering a worked example and reading the output; perturbation testing when
touching data keys; **writing tests that express a design rule rather than cover a code path**;
**writing the validation rule before the data it validates.**

**This context added a fifth: prefer a structural fence to a list of exceptions.** D29.3 spent
three contexts as an eleven-id allowlist that had to be re-argued every time somebody looked at
it, and it could not even catch its own dead entry. Replaced by *"essence is reachable only
through the active path"* — one sentence, no exceptions, and a new faucet cannot be added because
there is no list to add it to. The same shape as the active/passive opportunity seam, and as
`MinimumReactionTicks` deriving itself from the combat windows: **when a rule can be made true by
construction, that is cheaper forever than making it true by agreement.**

Corollary, restated because it keeps earning its place: **when a rule has one honest exception,
name it in the test rather than weakening the assertion.** Cooking is exempt from the dead-end
rule by name. Form acquisition is exempt from the progression roll-call by name, with the
milestone that removes it written next to it. A weakened rule stops catching the next mistake; a
named exception is a to-do list. **And when the exceptions outnumber the rule, that is the signal
to look for the structural version instead** — which is exactly what happened to D29.3.
