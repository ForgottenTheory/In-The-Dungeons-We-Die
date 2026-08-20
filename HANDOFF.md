# HANDOFF.md

For the next Claude session. Read `CLAUDE.md`, then this.

**Entry-point docs — read these before anything else:**
- **`docs/game-overview.md`** — the whole game on one map (player's side), with
  BUILT / PARTIAL / DESIGNED / UNRESOLVED marks. **Rewritten and re-verified this context** —
  it now opens with a one-run walkthrough and every number in it was measured, not quoted.
- **`docs/code-map.md`** — the whole repo on one map: layers, entry points, a card per
  subsystem, **"Where do I change X?"**, and the do-not-rename persistent-identifier list.
  **§10.13b** is auto-combat, **§10.14** is professions (and the benefit seam), **§10.16b** is
  Realm preparation, **§10.16c** is the seven progression tracks.
- **`docs/GDD.md`** — the deepest single design document, and **current again as of this
  context**: statuses, counts and §19 were re-grounded against the code at HEAD. Where an older
  design number and the shipped constant disagree, the GDD now records both (see the honesty
  notes in §5.5, §5.6, §11.1, §19.3).
- **`docs/crafting-overview.md`** — the crafting stack end to end. **§15 is the
  design-word ↔ code-name bridge and you will need it** (see the ⚠ below).
- **`docs/professions.md`** — the 20-profession system as it actually ships.
- **`docs/loot.md`** — the reward layer, one table shape for every source.

Decisions are in `DECISIONS.md` (through **D41**, plus **D29.3 resolved** at the end). For the
D30 presentation rule see `docs/presentation-architecture.md`.

## Repo / build state

- Branch `main`. This context: **`89e00bc`** (the documentation sync) is committed; **the two
  content fixes below and this handoff are in the working tree awaiting the user's commit
  call** — commit only when asked. Pushing is the user's job.
- Green at handoff: **`dotnet build InTheDungeonsWeDie.slnx` clean (0 warnings) ·
  `dotnet test` → 1,191 passing.**
- **Save schema is v11.** Nothing this context touched code or the save. The one real
  migration in the project's history is still v9's `Armor` → `Body`.
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
Player-facing text, save keys and content ids stayed put on purpose — **offer to change
displayed wording, don't just do it.** (`MasteryBenefitKind` → `ProfessionBenefitKind` also
landed in Phase 10; member names did not move — they are the JSON `kind` values.)

---

## ⭐ START HERE — the priority has not changed

### 1. The playtest half is the user's, and it is still the whole bottleneck

Everything the loop needs is built, **including the idle half**. Nobody has played it. Play the
full loop — prepare a loadout → enter → fight → extract → mine/smelt/infuse/attune/fabricate/roll
→ equip → go again, and leave something training overnight — then land the whole balance backlog
in one pass. **Do not retune anything before the user reports play feel** — standing decision.
The backlog: profession intervals/XP across all twenty, opportunity odds/risk/cost, offline
caps, plot grow times, course bonuses, Fireball, Bastion, casting-speed (GDD §18 #16),
fabrication constants, affix-roll odds, the whole Dark Forest, the mastery ladder, the character
XP curve, the knowledge thresholds, everything Phase 10 added (13 synergy rates, both global
unlock thresholds, three auto-combat brains, the seven re-shaped D29.3 opportunities) — **plus
the two content fixes below, which change live behaviour** (Emberbrand got narrower, Reflection
got real).

### 2. Editor verification pending — unchanged from last handoff; nothing new rendered

This context was documentation and two data lines; the whole list from the previous handoff
stands: the Phase 10 surfaces (away panel, auto-combat toggle + brain picker, three-state
passive label, "Helped by:" synergy lines, autosave-on-quit — and watch a Brute fight with
auto-combat on: it should block reliably and never perfect-block or parry), and the older list
(the Realm Preparation screen, mastery readouts, deep-entry picker at 900 knowledge, the rebuilt
Hideout tab, the R-track bench grammar + fabrication preview + Reroll, the Parry button, the
Techniques panel, the goblin fights, Char Lab hooks).

---

## This context, in two pieces

### `89e00bc` — the documentation sync (docs only; no code, no data)

`docs/GDD.md` and `docs/game-overview.md` were re-grounded **against the repository, not
against each other** — every count measured, every status mark checked against code and tests.

- **GDD**: the loop diagram no longer claims fabrication/combat/anatomy are PLANNED; §7 is the
  real 20/348/36 profession system (the old designed-19 roster moved to a superseded note);
  offline is BUILT with its caps; materials 1,448; processes 8 (Attune live; the temporary
  Distill/Attune ungating documented with its pinning test); statuses 29 with true category
  rosters; moves 43; affixes 44; keys 60; Dark Forest 34/3 with all eleven node types; enemies
  483/26/7/7 with the rank layer; Hideout = 20 stations; Economy = gold + 72 tables + the one
  Hedge Trader sink; a new §13.4 Loot section; §19 fully rebuilt (it still said auto-combat and
  offline were unbuilt, 8 professions, save v4).
- **Recorded doc/code honesty notes** (documented, deliberately not "fixed" in code): armour
  ships **ArmourK = 1.0** (the 5× formula in `docs/damage-and-defense.md` is older intent);
  **`MaxResistanceCeiling` (0.90) is declared and read by nothing**; of the timing keys only
  `combat.windup.mult` is consumed (interval/telegraph/recovery are declared with clamps,
  unread — and the D-20 floor 0.55 **is** applied in the registry now); **realm tiers are
  carried and read by nothing**; chains (`addChain`) are live machinery with zero content.
- **game-overview.md** was rewritten as the plain-language overview (a narrative run, player
  experience per system, ~40% shorter), keeping the status marks and the reading map.

### The two content fixes (working tree) — silent-key bugs, now behaviour changes

`DataStore<T>` matches JSON keys case-insensitively against the C# property name **or** its
exact `[JsonPropertyName]` — and silently ignores everything else. Two shipped records paid
for that:

1. **`game/data/move_modifiers/move_modifiers.json`** — `movemod.emberbrand` authored
   `"moveId"` where `MoveMatch` declares `"move_id"`. The id was ignored, the match was empty,
   and **Emberbrand added heat to every move** instead of only Heavy Strike. Fixed. An
   Emberbrand item is now strictly narrower — exactly the "Heavy Strike gains additional Heat
   damage" exemplar it was written to be.
2. **`game/data/affixes/affixes.json`** — `affix.reflection` authored `"scalesWith"` where
   `EffectSpec` declares `"scales_with"`. With it ignored, `Magnitude()` returned the flat
   rolled amount — **Reflection dealt ~0.1–0.3 flat damage**. Fixed: it now returns
   roll% × the mitigated amount, which is what "Return $roll% of damage your block prevents"
   always claimed. Reflection is now *much* stronger than it ever was in testing — flag for
   the balance pass.

No test pinned either bug (nothing in `tests/` references emberbrand or reflection); the suite
is green after both fixes.

**The structural fence this suggests** (method note, not started): the game has no
unknown-JSON-field detection — `UnmappedMemberHandling.Disallow` on the definition types, or a
validator arm, would make this whole bug class a load error instead of a silent shrug. Same
shape as every other "true by construction" win in this file. Worth proposing when content work
resumes.

---

## ⚠️ Deferred by explicit decision — do not relitigate, do not silently fix

- **Balance, wholesale** (see START HERE).
- **Form/schematic acquisition (D29.2)** — schematics drop from eight tables and bind to no
  form. The one progression track nothing reads; `ProgressionEcosystemTests` exempts it **by
  name**. When it ships, delete the exemption and the roll-call should still pass.
- **Profession tools (E6)** — the seam is built (`ProfessionBenefits` is three-source by
  construction; `CourseBonusKeys` and the tool components already ship, read by nothing).
- **Fully unattended Realm runs** — auto-combat is live-only by design.
- **Player-facing crafting wording** (Integrity/Potency on screen) — offer, don't do.
- **Casting-speed scaling** (§18 #16) · **the Fighter identity hook** (§18 #15, NEEDS DESIGN).
- **Operations + Overreach + Anomalous, Exotic rare-roll, stored retaliation, inversion,
  ignore-fraction** — E7. **Signature affixes** — need crafting P4.
- **Sealed uniques + relic materials** — post-slice content (D28 fencing recorded).
- **No currency for Thieving; no economy** (NEEDS DESIGN — the Hedge Trader is the only sink).
- **`MainMvpUI` → a better name** — editor-side job.
- **`AffixDefinition`/`RolledAffix`/`Dungeons.Affixes`** keep their names (D-17).

## Known debt and filed items

- **`PROJECT_STATE.md` / `SYSTEM_INDEX.md` are badly stale** (they predate the R-track, the
  crafting rename, the profession pass, M6 and Phases 6–10). GDD/overview/ROADMAP/DECISIONS and
  this file are current. **Either refresh them or delete them; a stale map is worse than none.**
- **Two sibling docs still carry pre-sync numbers:** `docs/damage-and-defense.md` (the 5×
  armour constant) and `docs/statuses.md` ("~27 definitions"; it is 29). Small, mechanical.
- **Stale code comments found during verification** (cheap sweep, all one-liners):
  `game/data/statuses/core.json` header says 14, the file holds 15 · `StatusController.cs`
  says "fourteen statuses" · `CharacterLeveling.cs` says "481 actors" (483) ·
  `mastery_benefits.json` + `MasteryBenefitDefinition.cs` say "659 actions" (348) ·
  `MaterialStateTuning.cs`/`TagFamilies.cs` say "~470 library" (1,448) · `DiscoverySystem.cs`
  claims persistence is future (it persists, `SaveMapper.cs:90/:196`) ·
  `CombatTuning.ResolveDecayPerTick` is applied per 5-tick sweep, so the name overstates the
  rate 5×.
- **Declared-but-unread constants**, now recorded in the GDD rather than only in heads:
  `MaxResistanceCeiling`, `combat.interval/telegraph/recovery.mult`, `combat.damage.flat`,
  the per-type damage keys, `combat.stagger.vulnerable`, realm `SupportedTiers`/run tier.
- **`GameRoot` is ~2,870 lines** — application-layer extraction still deferred (D2).
- **The in-run `KnowledgeIntel` and pre-run `RealmBriefing`** are two implementations of "what
  does knowledge reveal" sharing thresholds but not the reading; the in-run one still drops
  negative resistances. Unifying them is the obvious next tidy.
- **Three of the five Dark Forest enemies have no readable weakness** (Raider, Hexer, Grask) —
  the first knowledge rung buys a nearly empty Known Threats panel for 3 of 5 fights.
- **Auto-combat never uses consumables**, and `AutoCombatPilot` polls every tick — both filed.
- **Combat-tab Parry button** is evaluated once at startup (`BuildCombatSection`); the in-run
  row re-checks correctly, so a fabricated Buckler enables Parry in a Realm but not on the
  Combat tab until restart. **"Use Salve" is hard-wired** to `consumable.healing_salve`;
  `GameRoot.UsableConsumables` exists and no UI calls it. Both are one-screen fixes when UI
  work resumes.
- **Cooking is the one documented profession dead end** (named exception; dies with consumable
  forms). **Fletching's bow/projectile parts have no forms yet** (form acquisition).
- Zeroing every affix's `chance_weight.base` still passes the suite — that lever is unguarded.
- Two provisional crafting constants; `PropertyDefinition.transferable` unconsumed; response
  properties drop on transformation; `InappropriateOptimismRule` orphaned;
  `StatusController.ModifierTotal` display-only.

## The rules that keep this tractable — do not erode

- **Code optimizes for human comprehension (CLAUDE.md rule 8).** Expressive names, one name per
  concept project-wide, no magic numbers, no behaviour-selecting booleans, and **code-symbol
  renaming is not persistent-identifier renaming** — `docs/code-map.md` §12 (action ids,
  `CourseBonusKeys` values, `elite`/`boss` tags, `ProfessionBenefitKind` members,
  `DefensiveStance` members, and every `[JsonPropertyName]` value — the two fixes this context
  are what ignoring that list costs).
- **Three languages (D30, rule 7):** raw simulation values never on normal play surfaces;
  `Dungeons.Presentation` is the one path to player-facing text; a player-facing surface ships
  only when its mechanic resolves.
- **Progression stays layered (D40).** Character XP is Realm-only. Nothing in the Hideout may
  feed it.
- **Automation is disadvantaged by latency, never by damage (D-07, D41).** No second combat
  resolver, ever. A profile quick enough to parry is a load error.
- **Essence is extraction's export (D29.3).** Professions reach it only through opportunity
  payloads; drop tables not at all.
- **No recipe, ever.** **A Prefix may never reference a Base.** **Every Base distributes the
  same growth budget.** **No class-check condition kind (D25).** **Enemy identity composes
  (D26).** **Innates never reroll (U-7).** One affix per family per item (§3.5).
- **Professions are an ecosystem, not twenty XP bars** — `ProfessionEcosystemTests`.
- **D-12** never default a `ModifierContext` · **D-08** Resolve, not per-control chances ·
  **D-06** on-block hooks listen to `Blocked` (both outcomes) · **D-01** lane movement is
  always `convert`/`addAsExtra` with a fraction · ailment ticks never proc.
- Keep `dotnet test` green (**1,191**) and the build at **0 warnings**. Content is data; every
  new content type ships with validator rules + failing-content tests per rule. Commit only
  when asked; `main`; Co-Authored-By trailer.

## The method notes that keep paying

Rendering a worked example and reading the output · perturbation testing when touching data
keys · tests that express a design rule rather than cover a code path · writing the validation
rule before the data it validates · **prefer a structural fence to a list of exceptions**
(D29.3's allowlist → one sentence; `MinimumReactionTicks` deriving itself from the windows) ·
when a rule has one honest exception, **name it in the test** (Cooking; form acquisition).

**This context added a sixth: documentation is a claim, so measure it.** The docs pass found
its corrections by counting records, greping constants and running the suite — not by
reconciling documents against each other. Two of those measurements turned out to be live
content bugs (`moveId`, `scalesWith`) that the whole 1,191-test suite was structurally unable
to notice, because unknown JSON fields are silently ignored at load. The unknown-field fence
(above) is the structural version of that lesson.
