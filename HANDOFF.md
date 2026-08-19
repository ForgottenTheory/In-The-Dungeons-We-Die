# HANDOFF.md

For the next Claude session. Read `CLAUDE.md`, then this.

**Entry-point docs — read these before anything else:**
- **`docs/game-overview.md`** — the whole game on one map (player's side), with
  BUILT / PARTIAL / DESIGNED / UNRESOLVED marks.
- **`docs/code-map.md`** — the whole repo on one map: layers, entry points, a card per
  subsystem, **"Where do I change X?"**, and the do-not-rename persistent-identifier list.
  **§10.16b** is Realm preparation, **§10.16c** is the seven progression tracks.
- **`docs/crafting-overview.md`** — the crafting stack end to end. **§15 is the
  design-word ↔ code-name bridge and you will need it** (see the ⚠ below).
- **`docs/professions.md`** — the 20-profession system as it actually ships.
- **`docs/loot.md`** — the reward layer, one table shape for every source.

`docs/GDD.md` is still the deepest single design document. For the D30 presentation rule see
`docs/presentation-architecture.md`; decisions are in `DECISIONS.md` (now through **D40**).

## Repo / build state
- Branch `main`. Last commit **`a1602ac`** — Phases 7 and 8 together (66 files, +4944/−222).
  **Not pushed** — pushing is the user's job.
- Green at handoff: **`dotnet build InTheDungeonsWeDie.slnx` clean (0 warnings) ·
  `dotnet test` → 1121 passing** (1058 at the start of this context).
- ⚠ **Save schema is now v11.** v10 added `Loadout` (Phase 7), v11 added `CharacterXp`
  (Phase 8). **Both load forward with no migration** — an older save arrives with no
  destination, an empty pack and level 1, which is the state a new game starts in. Tests pin
  both. The one real migration in the project's history is still v9's `Armor` → `Body`.
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

---

## ⭐ START HERE — three things, in this order

### 1. The playtest half is the user's, and it is now the bottleneck

Everything the loop needs is built. **Nobody has played it.** Play the full loop — prepare a
loadout → enter → fight → extract → mine/smelt/infuse/attune/fabricate/roll → equip → go again —
then land the whole balance backlog in one pass. **Do not retune anything before the user reports
play feel** — standing decision, and it now guards a large backlog: profession intervals/XP
across all twenty, opportunity odds/risk/cost, offline caps, plot grow times, course bonuses,
Fireball, Bastion, casting-speed (GDD §18 #16), fabrication constants, affix-roll odds, the whole
Dark Forest, **and everything Phase 8 added** (the mastery ladder, the character XP curve, the
two new knowledge thresholds).

### 2. Editor verification pending — the list is long and none of it has been seen running

- **NEW (Phase 7): the whole Realm Preparation screen.** `game/ui/RealmPreparationPanel.cs` —
  realm picker, nine loadout slots, the consumable pack, the Tools readiness panel, Known
  Threats / Known Resources / Realm Information, and `[ ENTER REALM ]`. The Realm tab is now two
  screens that swap.
- **NEW (Phase 8):** the mastery readout on every profession ladder row, character level on the
  character sheet and the preparation header, the deep-entry depth picker (only appears at 900
  Dark Forest knowledge), and the `[mastery: materials saved, and doubled]` log line.
- The older list, still unverified: the whole rebuilt Professions/Hideout tab (station picker,
  ladders, Discover/Pursue card, Farming plots, training-course fitter), the entire R-track UI
  (bench semantic grammar + Advanced toggle, glyph rendering — any tofu box is one JSON edit in
  `game/data/properties/properties.json` — fabrication slot-fit lines + preview card, the
  Latest-work reveal + debug Reroll, the Parry button), Techniques panel, the goblin fights,
  combat card height floor, Char Lab hooks.

### 3. One open question the user has still not answered

**The D29.3 essence allowlist.** Eleven profession faucets yield essence-bearing materials. Two
(the shock-eel rung) were audited; nine were added by the 20-profession pass and allowlisted
**with the argument stated, not with the user's agreement**. The argument on file: a level-45+
rung is not competing with Realm extraction for the same player at the same time. All eleven
carry their level gate in
`C2cAuditTests.ProfessionFaucetsYieldEssenceOnlyFromTheAuditedAllowlist`. **This has now been
carried unanswered for three contexts — ask.** Pulling them back out is a content edit.

---

## This context, in one commit: `a1602ac`

Two phases. They share a commit because Phase 8 edited files Phase 7 created, so the tree only
ever held the final state — splitting would have produced an intermediate commit that never built.

### Phase 7 — Realm Preparation (D39)

**The bridge the loop diagram carried as `[PLANNED]` since the first commit.** Entering a Realm
was 164 `Enter <Realm>` buttons with no way to see what you were walking into.

1. **`RunLoadout` holds the destination and the pack, and nothing else.** Worn `Equipment`
   already *is* the gear half of a loadout; a second copy means two answers to "what is the
   player wearing". The screen edits the real equipment through the normal equip path.
2. **Packing closed a real hole rather than adding a feature.** `CombatUseConsumable` reads the
   *active* bag, which inside a Realm is the run inventory, and `EnterRealm` created it empty —
   **a Healing Salve in the Stash was unreachable during a run.** Supplies transfer at entry and
   are unsecured from that moment, obeying the extraction model with no second code path.
3. **The door is never locked.** Every gear problem is a `LoadoutIssue` warning; only "no realm
   selected" blocks entry. `LoadoutCheckTests.NoAmountOfMissingGearEverStopsThePlayerEntering` is
   the anti-soft-lock fence (GDD §13.1). The starter kit reads the **Stash** too — an unequipped
   sword is not stuck.
4. **`RealmBriefing` is a redaction, not a second knowledge system.** It lives in
   `Dungeons.Presentation` for the reason `AssayLens` does. Every gate goes through
   `RealmKnowledgeLevels.Reveals`. The hidden-node rule moved onto
   `RealmLocationDefinition.IsVisibleAt` so travel, the map and the briefing cannot diverge.

### Phase 8 — the progression pass (D40)

**Three tracks were tracked and not read.** Mastery incremented behind four hardcoded constants.
Realm Knowledge was missing the one GDD §11.4 item that grants an *option*. Character XP had the
entire growth system — `AttributeGrowth`, the 4.0 budget, `ResolvedBuild.GrowthAt` — and **no
source**, so the character was permanently level 1.

1. **Mastery's numbers are content** (`game/data/mastery/`): one shared six-rung ladder, not a
   block on each of 284 actions. **Mastery level is completions, linear, ceiling 99** — a bending
   curve would reprice every action while claiming to be an integration pass. Preservation (20)
   and doubling (40) ship as **unlocks**, not creeping percentages, and the log says so when they
   fire.
2. **`RequiredMasteryLevel` gates four high-risk opportunities.** Below the gate they are **not
   rolled at all** — the same structural trick the active/passive seam uses.
3. **Realm Knowledge was bracketed, never rescaled.** D38's five thresholds are untouched;
   `CommonResources` (12) and `DeepEntry` (900) sit below and above. Deep entry is GDD §11.4's
   portal targeting, priced honestly — starting at depth 2 skips depth 1's fights, loot **and**
   knowledge.
4. **Character XP comes from Realm activity only.** Awarding it in the Hideout is the universal
   power level GDD §4 exists to prevent.
   `ProgressionEcosystemTests.ProfessionWorkAwardsNoCharacterXp` fails at *compile* time if
   `ActionOutcome` ever grows the field.
5. **Levelling raises the ceiling and never refills what is under it.** `RebuildCharacter`
   composes a fresh `Character`, which starts full — pools now carry across, clamped. Loading a
   save is the one deliberate exception, because that is a rest.

### Fences that fired, and what was done

- **A validator rule caught the shipped mastery table on its first run.** Capping mastery at 99
  means the old per-point rates no longer reach their old ceilings, so `mastery.interval`
  promising 0.5 while delivering 0.495 was a promise the ladder could not keep. Caps are now
  exactly what mastery 99 buys.
- **One number genuinely moved:** opportunity risk reduction. Its old cap of 0.5 needed 250
  completions of one action — reachable for an uncapped counter, impossible for a 99-level track.
  Capped at what 99 levels buy (0.198) rather than raising the rate, because raising it would be
  a mid-track buff.
- **A hidden extraction node is revealed by HiddenRoutes, not ExtractionRoutes.** The Split Trunk
  is both. Finding a node reveals what it is — the rule the run already uses. The first version
  of that test asserted otherwise and was wrong.
- **`RealmBriefingTests` was silently not testing the new yield section** — its fixture bundle
  omitted `LootTables` and `Materials`, so the list was always empty. Caught on review, fixed.
- **The in-run intel drops the most actionable fact about an enemy.** `KnowledgeIntel` lists only
  lanes above zero, so "this thing burns" never reached the player. The briefing splits on the
  sign: *hit it with* / *burns to* / *shrugs off*. **The in-run intel still has the omission** —
  worth unifying, deliberately not done in a preparation pass.

---

## ⚠️ Deferred by explicit decision — do not relitigate, do not silently fix

- **Balance, wholesale** (see START HERE). Every profession, mastery, character-XP and Realm
  number is breadth-not-balance.
- **Form/schematic acquisition (D29.2)** — `material.schematic_fragment` drops from eight tables
  and binds to no form. **The one progression track nothing reads**, and
  `ProgressionEcosystemTests` exempts it **by name**. Building it means authoring acquisition
  onto 23 forms and deciding which a fresh player already knows — a balance and soft-lock
  decision, not an integration. **When it ships, delete the exemption and the roll-call should
  still pass.**
- **Profession tools** — E6. Tool slots, tool forms and the yield pipeline. The Preparation
  screen's Tools panel shows profession *readiness* instead, because a tool slot with no mechanic
  behind it breaks rule 7. `MasteryBenefitKind` deliberately uses the same six names as the
  unread `profession.*` modifier keys so E6 can merge the two sources without a rename.
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

- **NEW: three of the five Dark Forest enemies have no readable weakness at all.** Raider, Hexer
  and Grask all read "hit with nothing in particular; burns to nothing; shrugs off toxin". So the
  *first* rung of the knowledge ladder buys an almost-empty Known Threats panel for 3 of 5
  fights. Content balance — flagged, not touched.
- **NEW: `GameRoot` is now ~2,680 lines.** Application-layer extraction deferred again; this
  context added ~330 lines of thin forwards and application glue, which makes the case stronger.
- **NEW: the in-run `KnowledgeIntel` and the pre-run `RealmBriefing` are two implementations of
  "what does knowledge reveal".** They share the thresholds (`RealmKnowledgeLevels`) but not the
  reading. Unifying them is the obvious next tidy.
- **Cartography's knowledge gains are all `realm.dark_forest`** — the only realm with content.
- **Cooking is the one documented profession dead end.** `ProfessionEcosystemTests` names it
  explicitly; when consumable forms land, delete the exception and it should still pass.
- **Fletching makes parts for bows and projectiles that have no forms yet** (form acquisition).
- `CourseBonusKeys` are declared, aggregated, displayed — and read by nothing. E6.
- Zeroing every affix's `chance_weight.base` still passes the suite — that lever is unguarded.
- Two provisional crafting constants; `PropertyDefinition.transferable` unconsumed; response
  properties drop on transformation; `InappropriateOptimismRule` orphaned;
  `StatusController.ModifierTotal` display-only.
- **`PROJECT_STATE.md` / `SYSTEM_INDEX.md` are badly stale** — they predate the R-track, the
  crafting rename, the profession pass, M6, and both phases in this commit. (GDD / ROADMAP /
  DECISIONS / the entry-point docs / this file are current.) **Either refresh them or delete
  them; a stale map is worse than none.**

## The rules that keep this tractable — do not erode

- **Code optimizes for human comprehension (CLAUDE.md rule 8).** Expressive names, one name per
  concept project-wide, no magic numbers, no behaviour-selecting booleans, and **code-symbol
  renaming is not persistent-identifier renaming** — `docs/code-map.md` §12. That list now also
  carries: **action ids** (mastery is keyed by them in every save), `CourseBonusKeys` values, the
  `elite`/`boss` actor tags (`EnemyRanks`), and `MasteryBenefitKind` member names.
- **Three languages (D30, rule 7):** raw simulation values never on normal play surfaces;
  `Dungeons.Presentation` is the one path to player-facing text, one-way, unit-tested; **a
  player-facing surface ships only when its mechanic resolves.** That last clause is why the
  Tools panel shows readiness rather than empty tool slots.
- **Progression stays layered (D40).** Character XP is Realm-only. Nothing in the Hideout may
  feed it, or every track collapses into one power number.
- **No recipe, ever.** **A Prefix may never reference a Base.** **Every Base distributes the same
  growth budget** — now actually exercised, and pinned by `CharacterProgressionTests`. **No
  class-check condition kind (D25).** **Enemy identity composes (D26).** **Innates never reroll
  (U-7).** One affix per family per item (§3.5).
- **Professions are an ecosystem, not twenty XP bars** — `ProfessionEcosystemTests`.
- **D-12** never default a `ModifierContext` · **D-08** Resolve, not per-control chances ·
  **D-06** on-block hooks listen to `Blocked` (both outcomes) · **D-01** lane movement is always
  `convert`/`addAsExtra` with a fraction · ailment ticks never proc.
- Keep `dotnet test` green (**1121**) and the build at **0 warnings**. Content is data; every new
  content type ships with validator rules + failing-content tests per rule. Commit only when
  asked; `main`; Co-Authored-By trailer.

## The method note that keeps paying

Previous contexts: rendering a worked example and reading the output; perturbation testing when
touching data keys; **writing tests that express a design rule rather than cover a code path.**

**This context added a fourth: write the validation rule before the data it validates.** The
mastery ladder's caps were wrong on their first run and the validator said so immediately —
`mastery.interval caps at 0.5 but reaches only 0.495 at mastery 99`. Nobody would have noticed
that by reading. Same shape as the previous context's audit tests, one step earlier.

Corollary, restated because it keeps earning its place: **when a rule has one honest exception,
name it in the test rather than weakening the assertion.** Cooking is exempt from the dead-end
rule by name. Form acquisition is exempt from the progression roll-call by name, with the
milestone that removes it written next to it. A weakened rule stops catching the next mistake; a
named exception is a to-do list.
