# Vertical Slice Expansion — Implementation Plan

> ⚠ **LARGELY SUPERSEDED — kept for its audit and its rationale, not its plan.**
> Its **P2** (modifier vocabulary + effect bus + trigger rules) **shipped** — see DECISIONS D23.
> Its **P3–P10** are replaced by the slice plan in `docs/effect-foundation.md` §10 (E0–E7 + C1/C2),
> settled by the decisions in §12 of that document.
> §1's audit and §2's documentation-vs-implementation disagreements remain useful history.
>
> **Original status: PROPOSED — awaiting approval. No implementation started.**
>
> Goal: widen the existing vertical slice until it resembles the intended game rather than a
> technical demonstration, and until the loop in §22 of the brief can actually be played.
>
> This plan is written against the **real current repo** (commit `9917d68`, 364 tests green).
> Where documentation and implementation disagree, that is called out in §2 rather than
> silently resolved.

---

## 1. Audit — what actually exists

Legend: ✅ works · 🟡 partial · 🧱 architecture only · ⬜ missing

### Foundations (solid, reuse as-is)

| System | State | Notes |
|---|---|---|
| `TickEngine` | ✅ | Deterministic schedule/advance/cancel. One instance drives combat + passive gathering. **This is the correct spine for the tick-based direction and needs no change.** |
| `DataStore<T>` + `ContentBundle` + `ContentLoader.LoadAll` | ✅ | Adding a content type is one store + one load line. Proven four times this session. |
| `ContentValidator` | ✅ | ~25 rules, each with a failing test. Extends cleanly. |
| Save/load | ✅ | `SaveData` v4, `SaveMapper`, versioned. No migration path yet (accepted, D14). |
| `ModifierPipeline` | 🟡 | Correct shape (base → add → multiply → clamp) but **addresses only 10 stats** — see §3.1. |
| `IRandomSource` | ✅ | Seeded, injected. Determinism tests rely on it. |
| Emergent crafting P1 | ✅ | Shipped this session — full reaction engine, no recipes. See `SYSTEM_INDEX.md`. |

### Gameplay systems

| System | State | Reality |
|---|---|---|
| Character composition | 🟡 | Species+Prefix+Class+Suffix composes correctly. But every component is **stat modifiers only**, except two suffix rules. |
| `ICharacterRule` | 🧱 | Hook exists, but returns *only* `AttributeBonus`. Cannot express any documented suffix. See §3.2. |
| Combat | 🟡 | Real tick loop, telegraph→execute→recovery, block/dodge stances, 2 enemies, 3 abilities. Single target in practice. |
| Abilities | 🧱 | `AbilityDefinition` = damage type + value + stamina + timing. No tags, targeting, effects, costs, requirements, or conditions. |
| Professions | 🟡 | 3 professions, 3 actions, passive+active, XP/level, per-action mastery **stored but unused**. |
| Equipment | 🟡 | 2 slots, 4 hand-authored definitions. `EquipmentResolver` maps Mass→damage/speed and Hardness→armor only. |
| Realm | 🟡 | Dark Forest, 10 locations, depths 1–2, travel/descend/extract, knowledge counter. |
| Emergent materials → gameplay | ⬜ | Materials exist and stack, but **nothing consumes them**. This is the biggest hole. |

### Content inventory vs documented rosters

| Content | Docs | Shipped | Gap |
|---|---|---|---|
| Species | 10 | **3** | 7 |
| Base Classes | 9 | **2** | 7 |
| Prefixes | ~50 | **3** | ~47 |
| Suffixes | ~100 | **5** (2 live) | ~95 |
| Professions | 19 | **3** | 16 |
| Profession actions | — | **3** | — |
| Abilities / moves | — | **3** | — |
| Enemies | — | **2** | — |
| Equipment | — | **4** | — |
| Materials | — | **474** | — (this one is done) |

The material library is the *only* content axis that is genuinely wide. Everything else is a
placeholder set sized for a technical demo.

---

## 2. Documentation vs implementation disagreements

You asked for these to be called out rather than quietly fixed.

1. **`docs/classes.md` §3 lists 9 Base Classes; only Bastion and Hexslinger exist.** The doc also
   flags that Spellblade-type and Summoner-type chassis "require a final project-specific name".
   **That naming decision is still open and is yours to make** — I will not invent them.

2. **Class abilities are dead ids.** `class.bastion` declares `ability.guard`, `class.hexslinger`
   declares `ability.hex_bolt`. Neither ability definition exists. `ContentValidator` has an
   explicit `KnownUnimplementedAbilities` allowlist tolerating them. This is honest placeholder
   handling, but it means **no class currently has a class ability**.

3. **`docs/classes.md` §5 describes Pyromaniac as "converts part of qualifying damage into
   Fire".** The shipped prefix is `+1 Intelligence`. Same for every other prefix: documented
   behaviour, numeric implementation. `Frenzied` ("repeated attacks build offensive momentum")
   is `+2 STR / +1 DEX`.

4. **`docs/classes.md` §7 suffixes are explicitly rule-breakers.** Three of the five shipped
   suffixes (`Exploding Kneecaps`, `The Last Laugh`, `The Bigger Hammer`) have **no rule at all** —
   tags and stat modifiers only. The two that do have rules only grant conditional attribute
   bonuses, because that is the only thing `ICharacterRule` can return.

5. **`docs/professions.md` §9 specifies Mastery 1–99 granting interval reduction, yield,
   cost reduction, rare-material chance.** `ProfessionProgress.Masteries` stores per-action
   mastery integers and **nothing reads them**. Mastery is currently a number that goes up and
   does nothing.

6. **`docs/professions.md` §10 specifies offline progress** with a stated formula. Not
   implemented. `PROJECT_STATE.md` correctly records this as missing.

7. **`docs/combat-spec.md` §4 specifies `QUEUE → TELEGRAPH → WINDUP → EXECUTION → RECOVERY →
   READY`.** The implementation collapses telegraph and windup into a single
   `TimeToImpactTicks` and has no distinct QUEUE or interrupt window. The *shape* is right and
   the tick architecture supports the full lifecycle — but the stages are not currently
   separable, so **nothing can interrupt "during windup" because windup is not addressable**.

8. **`docs/combat-spec.md` §20: "Enemy AI should primarily choose intent."** Current AI is
   `_rng.NextInt(0, abilityIds.Count)` — uniform random selection. There is no intent, threat,
   or state consideration.

9. **`docs/combat-spec.md` §25 statuses, §21 interrupts, §19 hazards, §17 positioning, §28 auto
   combat** — none implemented. §17 explicitly permits MVP simplification; the others do not.

10. **Mana exists and nothing spends it.** `class.hexslinger` is a Mana class with no Mana
    abilities.

11. **Iron ore is seeded into the stash at startup** (`GameRoot._Ready`) because Mining does not
    exist. A hardcoded workaround standing in for a profession.

12. **`docs/itemization.md` §2 flags the scale mismatch**: materials are 0–100, equipment base
    properties are ~0–5. They do not interact yet. **They must be reconciled before materials
    can drive equipment**, and that is a combat rebalance, not a mapping change.

13. **`docs/current-state.md` is stale** (written before the equipment system). `PROJECT_STATE.md`
    supersedes it. Recommend deleting `current-state.md` rather than maintaining two audits.

---

## 3. Architectural blockers

Three things will cause large rework if not addressed early. Everything in §4 depends on them.

### 3.1 The modifier vocabulary is a closed 10-value enum

`StatId` is `{7 attributes} + {MaxHealth, MaxMana, MaxStamina}`. That is all any modifier can
target today.

Melvor-style progression is *fundamentally a modifier-stacking game*. The layers you listed —
interval reduction, resource preservation, doubling, yield, efficiency, cross-skill bonuses,
equipment-affecting-skills, global passives — are **all modifiers against things `StatId`
cannot name**: action interval, profession yield, preservation chance, damage, crit chance,
typed resistance, block effectiveness, extraction bonus.

If Equipment, Professions and Combat each invent their own bonus mechanism, we get three
incompatible systems and a painful merge. **This must be widened once, early, and used by
everything after.**

Recommended: keep the pipeline, replace the closed enum with a **namespaced open modifier key**
(`combat.damage.slashing`, `profession.interval.mining`, `profession.preserve.smithing`,
`equip.armor`), mirroring how `PropertyDefinition` already made property names data (D17). A
registry defines valid keys, so typos still fail validation. The 10 existing `StatId` values
become the first 10 registered keys — **no save-format change, no behaviour change**.

### 3.2 `ICharacterRule` cannot express a single documented suffix

It returns `IEnumerable<AttributeBonus>`. "Certain critical hits cause a secondary area
explosion" is not an attribute bonus. Neither is "extracting before severe danger grants a
persistent efficiency bonus", or "failed crafting experiments can occasionally produce useful
discoveries".

What is needed is a **game-event hook bus**: systems raise typed events (`DamageDealt`,
`CriticalLanded`, `EnemyDefeated`, `ExtractionCompleted`, `CraftFailed`, `LocationDiscovered`),
and rules subscribe and may *modify or react*. `docs/architecture.md` §14 already lists exactly
this event vocabulary — it is designed, just not built.

Without it, Phase 4 (Prefixes/Suffixes), Phase 5 (Combat) and Phase 7 (Enemies) each hardcode
their own hooks, and the "Dungeon Crawler Carl weirdness" the design is built around is
unreachable.

### 3.3 `GameRoot` is 1,059 lines and this plan roughly triples the command surface

The Application-layer extraction has now been deferred three times (D2, D18, and again in P1).
Every phase below adds commands. `docs/architecture.md` §4 already specifies the layer and names
the use cases.

This is not cosmetic: **the event bus in 3.2 has to live somewhere**, and `GameRoot` is a Godot
node — putting it there would violate the Core-stays-engine-free invariant.

**Recommendation: do 3.1, 3.2 and 3.3 together as one focused architectural phase, before any
content work.** They are the same refactor viewed from three angles, and doing them together is
substantially cheaper than three separate passes.

---

## 4. Recommended ordering, and where I disagree with yours

Your ordering is sound in intent. Four changes, in descending importance:

### 4.1 Moves must come before Classes ⚠️ **biggest issue**

Your Phase 3 says "wire actual abilities/modifiers rather than placeholder IDs". But abilities
*are* moves. `ability.guard` is a dead id **precisely because there is no move architecture to
express it in**. Building Classes first means either inventing a throwaway ability format and
rewriting it in Phase 5, or leaving classes as stat blocks again.

**Move the universal Move architecture before Classes.** Classes then become "which moves,
which modifiers" — data, not code.

### 4.2 The architectural unblock must precede Equipment

Your Phase 2 (Equipment) needs modifiers for equipment bonuses; Phase 4 (Professions) needs them
for mastery/interval/yield; Phase 5 (Combat) needs them for damage and resistance. Doing
Equipment first means inventing the modifier system inside Equipment and generalising later.

### 4.3 Phase 1 is largely already done — but its scope has drifted

Emergent P1 shipped this session. What remains of your Phase 1 is **the Crafting Lab**.

However, your Lab spec asks to inspect **Essence**, **Emergent Properties** and **Quality**, none
of which exist yet (Essence is emergent-P3, traits are emergent-P2, Quality is an unused
`ItemQuality` enum). Two options:

- **(a) Lab against P1 only.** Cheap. But every emergent material is a number blend with a
  generated name — the qualitative payoff is missing, and "is this fun?" is hard to answer.
- **(b) Lab + pull emergent P2 (traits) forward.** Traits are what make a result *qualitatively*
  different ("Emberveined", "Bound Opposition") rather than "iron but 7 hotter".

**I recommend (b)**, with a caveat: `docs/emergent-item-system.md` §20 explicitly argues for (a)
— *"if pure convergence + integrity + naming + stacking isn't already interesting, adding traits
will not save it."* That is a real argument and it is your call. My reasoning for overriding it:
the spec was written before P1 existed, and now that it does, **the honest answer is that P1
alone reads as chemistry, not as discovery**. Traits also restore the integrity tension P1
currently lacks (the expensive cost terms are traits and signatures — see `HANDOFF.md`).

Either way: **build the Lab's inspector with trait/essence sections from the start**, showing
"none (P2)" until they land, so it is not rebuilt.

### 4.4 Build each Lab with its phase, not all up front

Your §17 lists 8 labs. Six of them inspect systems that do not exist yet. Build each alongside
the phase that creates its subject — the Lab is then also the phase's acceptance test.

### Resulting order

| # | Phase | Your § | Rationale for position |
|---|---|---|---|
| **P0** | Audit | 21/Phase 0 | This document |
| **P1** | Crafting Lab + emergent traits | 1, 17 | Answers "is the core fun?" before content |
| **P2** | **Modifier vocabulary + Effect bus + Application layer** | *(new)* | Unblocks everything after |
| **P3** | Moves & movesets | 11, 12 | Must precede Classes |
| **P4** | Equipment forms + fabrication | 2, 3 | First consumer of emergent materials |
| **P5** | Character identity + Character Lab | 4–8 | Now expressible via P2 + P3 |
| **P6** | Combat depth + Combat Lab | 10 | Statuses, interrupts, lifecycle, AI |
| **P7** | Professions breadth + Profession Lab | 9 | Needs P2's modifiers |
| **P8** | Enemy ecosystem + Enemy Lab | 13, 14 | Needs P3 + P6 |
| **P9** | Realm integration | 15, 16 | Ties it together |
| **P10** | Balance & content pass | 8 | Only once the loop is fun |

---

## 5. Phase detail

Every phase must leave `dotnet build` (0 warnings) and `dotnet test` green, in tested
increments, per `CLAUDE.md`.

---

### P1 — Crafting Lab + Emergent Traits

**Current status.** Emergent P1 complete (reaction engine, registry, naming, log, projection,
basic Crafting tab). Traits, essence, signature reactions not started. The Crafting tab exists
but has never been run.

**Reuse.** Everything in `core/Crafting/`. `ReactionLog` already records structured
`PropertyChange` entries with causes — the Lab's "why did this react" view is largely a
rendering job. `CraftProjection` already supplies pre-commit data.

**Missing.**
- Verification that the Crafting tab works at all (blocking; see `HANDOFF.md`).
- State traits (emergent P2): `TraitDefinition`, birth conditions, magnitude expressions,
  `consumes` costs, cap 3 + displacement + supersession, adjective ladders already authored in
  the name grammar.
- Lab-specific: property override sandbox, A→B vs B→A comparison, item duplication, lineage
  walker, save/load round-trip verification, experiment reset.

**Architecture / data.**
- `TraitDefinition` content type (`game/data/traits/`), ~15 traits. Slots into `ContentBundle`.
- Signature already reserves `|traits=` — **existing archetypes keep their ids**.
- Trait birth/displacement/supersession in `ReactionAlgebra` step 8–9 (already stubbed in §8.7).
- Lab override sandbox must **not** leak into real crafting: a `ScratchInventory` +
  synthetic `MaterialDefinition`s built in-memory, never registered to the shared store.

**UI.** A dedicated **Crafting Lab** tab, separate from the player-facing Crafting tab:
material picker with search (474 materials — a dropdown is unusable), editable property grid,
ordered chain builder, side-by-side A→B / B→A comparison panes, full inspector (name, tags,
all properties, potency, integrity, generation, lineage tree, traits, reaction log), and
buttons for duplicate / feed-forward / save / reload / reset.

**Debug tooling.** The "participating properties → resolved reaction → result" explanation you
described. `ReactionLogBuilder` produces most of it; the Lab adds a *pre*-reaction view showing
which channel properties are live and what the coefficients will be.

**Tests.** Trait birth conditions, cap/displacement (weakest dropped, costs not refunded),
supersession, trait participation in signature, naming with traits, determinism with traits,
serialization of traits.

**Docs.** `docs/emergent-item-system.md` P2 → shipped. `PROJECT_STATE.md`. New
`docs/dev-tools.md` describing the Lab (this becomes the home for all later labs).

**Depends on.** Nothing. **Blocks.** P4 (equipment wants trait expression).

**Order within phase.** (1) verify the existing tab → (2) Lab shell + inspector → (3) override
sandbox + comparison → (4) traits → (5) Lab trait display.

**Acceptance.**
- Take Fire Moss + Iron Ingot → emergent material → feed into Stormglass → another material →
  save → reload → still correct, still stacks, still named the same.
- A→B and B→A shown side by side with the differing steps visible.
- Force `Toxicity 80 / Conductivity 70 / Stability 60` by hand and see the engine's response.
- At least one craft produces a **named trait**, and the log explains why it was born.

---

### P2 — Modifier Vocabulary + Effect Bus + Application Layer ⭐ *(new phase)*

**Current status.** `ModifierPipeline` correct but 10-key. `ICharacterRule` attribute-only.
`GameRoot` 1,059 lines. `docs/architecture.md` §4/§14/§15/§18 already specify all three.

**Reuse.** The pipeline's math and ordering are right and stay. `RuleRegistry`'s
id→implementation resolution pattern stays. The existing `GameRoot` events
(`LogEmitted`, `InventoryChanged`, …) become the UI-facing projection of the new bus.

**Missing.**
- `ModifierKey` registry (data-defined, validated), replacing the closed `StatId` enum as the
  modifier target vocabulary. `StatId` survives as the attribute enum.
- `ModifierSource` provenance so the Character Lab can answer "*why* is my interval 87?".
- A typed game-event bus in Core with the `architecture.md` §14 vocabulary.
- `IGameRule` superseding `ICharacterRule`: subscribes to events, may contribute modifiers,
  react, or veto.
- Application layer: `core/Application/` use-case services (`CraftItem`, `EnterRealm`,
  `QueueCombatAction`, `ExtractFromRealm`, …) that `GameRoot` forwards to.

**Architecture / data.** This is the one phase that is *only* refactor. Success is measured by
behaviour being **unchanged** — 364 tests still green, no save-format change — with the surface
widened.

**UI.** None, except `GameRoot` shrinking to wiring + forwarding.

**Tests.** Modifier resolution across sources with provenance; unknown modifier key fails
validation; event ordering determinism; a rule can observe and modify an event; existing
character/profession/combat tests unchanged.

**Docs.** `docs/architecture.md` §4/§14/§18 updated from "planned" to "implemented".
`DECISIONS.md` — supersede D2 (GameRoot as application layer).

**Depends on.** Nothing. **Blocks.** P4, P5, P6, P7 — all of them.

**Order within phase.** (1) modifier keys + registry + validation → (2) migrate the 10 existing
stats, prove no behaviour change → (3) event bus → (4) `IGameRule` + port the 2 existing rules →
(5) Application layer extraction.

**Acceptance.** All 364 tests green with no test changes except the two rule ports. A throwaway
rule can be written that reacts to `EnemyDefeated` and grants a modifier — proving the surface
is real — then deleted.

**Cost note.** This phase produces **no visible gameplay**. It is the least satisfying phase and
the one most tempting to skip. Skipping it means paying for it three times later.

---

### P3 — Moves & Movesets

**Current status.** `AbilityDefinition` = damage type + base value + stamina + timing. 3 exist.
`AttackProfile` is the neutral combat view. `CombatEncounter` consumes both.

**Reuse.** `AbilityTiming` (telegraph/windup/recovery) is the correct core and stays.
`AttackProfile`/`ArmorProfile` neutrality (D8) stays — combat must keep reading neutral profiles.

**Missing.** Everything else about moves.

**Architecture / data.** `MoveDefinition` with a **composed effect list**, not a wide nullable
record — you explicitly warned against the latter, and it is the right call:

```
MoveDefinition: id, name, moveType, tags, timing, costs[], targeting,
                requirements[], effects[], interruptible, tags
Effect (polymorphic, small set): Damage | Heal | ApplyStatus | Move | Interrupt | Resource | Summon
```

Serialization uses a discriminated `type` field per effect. New effect kinds are new classes,
not new nullable fields.

**Moveset composition** — the rules for combining sources are the load-bearing design decision:
Base Class grants core moves; weapon grants weapon moves; Species/Prefix/Suffix may add, replace
or modify; equipment may gate. Resolution needs a deterministic precedence order and a way to
express *replacement* (`Of The Wrong Weapon` needs this).

**UI.** **Move Viewer** lab: every move, its source, timing bars, costs, effects, requirements.

**Tests.** Move resolution from multi-source sets; precedence and replacement; requirement
gating; cost validation; timing lifecycle; a move's effects apply in order; enemy movesets
resolve identically to player ones.

**Docs.** New `docs/moves.md`. Update `docs/combat-spec.md` §10 to reference it.

**Depends on.** P2 (costs/requirements are modifier-aware). **Blocks.** P5, P6, P8.

**Order within phase.** (1) `MoveDefinition` + effects → (2) port the 3 existing abilities, prove
combat unchanged → (3) moveset composition → (4) Move Viewer → (5) a handful of representative
moves across categories.

**Acceptance.** The existing encounter plays identically through the move system. A weapon-granted
move and a class-granted move coexist on one character with visible sources.

---

### P4 — Equipment Forms + Fabrication

**Current status.** 2 slots, 4 authored definitions, `EquipmentResolver` maps 2 properties.
This is emergent-item-system **P5a/P5b**, and the two roadmaps merge here.

**Reuse.** `ItemInstance` (correct for equipment, per D20), `Equipment` container,
`EquipmentResolver` as the seam, `AttackProfile`/`ArmorProfile`.

**Missing.** Form templates, `stat_map`, per-slot apertures, multi-component fabrication,
generated equipment naming, durability decision, the scale reconciliation.

**⚠️ The scale reconciliation is the real work here.** Materials are 0–100; equipment base
properties are ~0–5 and drive current combat tuning. Mapping 0–100 material properties into
combat means **re-tuning every existing combat number**. This is flagged in `itemization.md` §2
and emergent §16.4 and should be budgeted as its own step, not assumed away.

**Architecture / data.** `FormTemplate` content type: slots with `requires_tags` + `mass_share`
+ per-slot `aperture`, and a slot-scoped `stat_map`. **Stats read from named slots, never a
blend** (emergent §16.2) — a hard edge on a flexible core must differ from the reverse.

Your property→stat questions, as design proposals to review rather than decisions:
- **Hardness** → weapon armour-penetration and edge retention; armour mitigation. Brittle if
  paired with low flexibility.
- **Mass** → damage and stagger up, action interval up, stamina cost up. On armour: mitigation
  up, dodge effectiveness down.
- **Flexibility** → bows and light armour good, rigid weapons bad. Interacts with hardness for
  breakage.
- **Conductivity** → boon for charge-channelling gear, liability against lightning damage.
- **Resonance** → the caster stat; gates essence expression (P3 of emergent).
- **Instability** → higher variance in output; a gambler's material.

Slots: expand from Weapon/Armor to at least Weapon / Offhand / Head / Body / Hands / Feet /
Trinket. Forms: ~6–8 (longsword, dagger, bow, staff, plate body, robe, light body, shield) —
enough that material choice visibly matters, not more.

**Durability:** `docs/emergent-item-system.md` §21 lists this as open and the game has none.
**Recommend deferring** — it is a whole attrition subsystem and the extraction loop already
provides the risk pressure.

**UI.** **Equipment Lab**: pick form + materials per slot → see resolved stats, expressed vs
dormant traits, generated name → equip → see the character sheet change.

**Tests.** Slot validation against `requires_tags`; stat resolution from named slots; aperture
expression and dormancy; equipment trait cap 4 on *expressed* magnitude; generated naming
(§16.5); serialization of fabricated instances; equip/unequip; requirements.

**Docs.** `docs/itemization.md` §3, `docs/emergent-item-system.md` P5a/b → shipped. New
`docs/equipment.md` for forms and the property→stat mappings.

**Depends on.** P1 (traits), P2 (modifiers). **Blocks.** P5 (class/equipment interaction), P9.

**Order within phase.** (1) slots widening → (2) **scale reconciliation + combat re-tune** →
(3) single-slot forms + `stat_map` → (4) Equipment Lab → (5) multi-component + apertures →
(6) naming.

**Acceptance.** `Iron Ore → Iron Ingot → [emergent craft] → Emberveined Iron → longsword edge →
equipped → character sheet changes → the weapon behaves differently in combat than a mundane
iron one.` That chain is the single most important acceptance test in this plan.

---

### P5 — Character Identity + Character Lab

**Current status.** Composition works; content is 3/2/3/5 against documented rosters of
10/9/~50/~100; behaviour is stat modifiers plus two conditional-attribute rules.

**Reuse.** `CharacterComposer`, `CharacterBlueprint`, typed component ids, the composition tests.

**Missing.** Mechanical identity for every layer, and the roster breadth.

**Architecture / data.** Almost entirely **data plus rules**, given P2 and P3 exist. Each layer
gets a defined mechanical role — proposed, for your review:

- **Species** = *physiology*. Resistances, vulnerabilities, resource behaviour, one innate
  move, environmental/profession interaction. One coherent identity each, not fifteen bonuses.
  (Undead: poison immunity, reduced conventional healing, necrotic affinity — already in the doc.)
- **Base Class** = *combat chassis*. Core moveset, primary resource, defensive identity,
  weapon affinity.
- **Prefix** = *build direction*. Modifies **how the class's existing moves behave** — damage
  conversion, resource substitution, trigger conditions. Not new moves.
- **Suffix** = *rule breaker*. Hooks arbitrary game events via P2's bus. Explicitly allowed to
  touch non-combat systems (crafting, extraction, loot, professions).

**Roster targets for the slice** (not the full documented pools): **all 10 species** (they are
cheap and define resistances the combat system needs), **4–5 base classes fully playable** of
the 9 (Bastion, Hexslinger, Pitfighter, Wayfarer + one caster), **~12 prefixes**, **~15
suffixes** chosen to span the *categories* of weirdness (combat trigger, loot, extraction,
crafting, movement).

**Names are yours.** I will use the documented roster verbatim and will not name the
Spellblade/Summoner chassis — that decision is open in the doc and I will ask.

**UI.** **Character Lab**: four dropdowns, live diff. Swap one component → see exactly what
changed in stats, moves, resistances, active rules, and equipment compatibility. Modifier
provenance from P2 makes "why is this number what it is" answerable.

**Tests.** Composition across combinations; no duplicated modifiers; rule activation/deactivation
on state change; suffix event hooks fire correctly; save/load of any combination; every
documented ability id resolves (the `KnownUnimplementedAbilities` allowlist should shrink to empty).

**Docs.** `docs/classes.md` gains an implementation-status column per entry. New
`docs/species.md` if species mechanics outgrow the classes doc.

**Depends on.** P2 (event bus — hard dependency), P3 (moves — hard dependency).
**Blocks.** P9.

**Acceptance.** Build `Undead Pyromaniac Bastion Of Improper Safety Procedures`, see all four
layers contributing visibly, and have the suffix do something that makes you say "wait, what?"

---

### P6 — Combat Depth + Combat Lab

**Current status.** Real tick loop with telegraph→execute→recovery and timed block/dodge
stances. Single enemy in practice. No statuses, interrupts, hazards, or intent-based AI.

**⚠️ Explicitly preserving:** the tick-driven continuous simulation. Nothing in this phase
introduces turns, initiative, or alternating actor phases. The `TickEngine` stays authoritative
and the clock keeps running while the player deliberates.

**Reuse.** `CombatEncounter`, `CombatCalculator`, the neutral-profile boundary (D8), `TickEngine`.

**Missing.** Separable lifecycle stages, statuses, interrupts, multi-enemy targeting, AI intent,
auto-combat, Mana consumption.

**Architecture / data.**
- **Split `TimeToImpactTicks` into addressable stages** so "interrupt during windup" is
  expressible. This is the one real structural change and it is contained.
- `StatusEffect` as data: tick duration, stacking rules, periodic effects, modifier
  contribution via P2.
- Multi-enemy: `CombatEncounter` already holds a list; targeting and target-switching need
  surfacing.
- **AI profiles** as data (aggressive / cautious / opportunist / caster), choosing *intent* per
  `combat-spec.md` §20. Not one C# class per enemy.
- **Auto-combat** (§28) uses the same rules with an automated chooser — this is also what makes
  passive realm runs possible later.
- **Positioning**: `combat-spec.md` §17 permits MVP simplification. **Recommend keeping the
  simplification for now** and revisiting after the loop is playable — it multiplies the design
  space of every move and enemy.

**UI.** **Combat Lab**: spawn arbitrary combatant vs arbitrary enemies, step the tick manually,
inspect the timeline of queued/telegraphed/executing actions, force statuses, inspect the damage
pipeline breakdown for the last hit.

**Tests.** Full lifecycle timing; interrupt cancels/delays correctly; status application,
duration, expiry, stacking; multi-enemy targeting; death and loot; resource costs; auto-combat
produces legal actions; determinism from a fixed seed.

**Docs.** `docs/combat-spec.md` — mark implemented sections, record the positioning deferral.

**Depends on.** P2, P3. **Blocks.** P8, P9.

**Acceptance.** Fight two enemies at once, interrupt a telegraphed heavy attack, apply and watch
a status tick, switch targets, and lose to a mistake that was legible in advance.

---

### P7 — Professions Breadth + Melvor Progression Layers

**Current status.** 3 professions, 3 actions, passive/active split working, XP/level curve,
per-action mastery stored but **unused**.

**Reuse.** `ProfessionSystem` (single execute path for passive+active — good), `ActionResolver`,
`PassiveProfessionRunner`, `ProfessionLeveling`, the XP curve.

**Missing.** The entire Melvor progression stack, plus 16 professions.

**Architecture / data.** With P2's modifiers this is mostly data and small systems:

| Layer | Status | Work |
|---|---|---|
| Skill XP / levels | ✅ | — |
| Action intervals | ✅ | — |
| Mastery XP / levels | 🟡 | Stored; needs an XP curve and effects |
| **Mastery Pool** | ⬜ | Per-skill pool + checkpoint rewards at 10/25/50/95% |
| Interval reduction | ⬜ | Modifier key; from level, mastery, equipment |
| Resource preservation | ⬜ | Chance to not consume inputs |
| Doubling / yield | ⬜ | Chance for extra output |
| Level-based unlocks | 🟡 | `requiredLevel` exists; no unlock *rewards* |
| Mastery-based unlocks | ⬜ | Per-action bonuses at mastery thresholds |
| Equipment affecting skills | ⬜ | Tool slots; needs P4 |
| Cross-skill bonuses | ⬜ | e.g. Herblore level improving material infusion |
| Global/passive bonuses | ⬜ | Account-wide modifiers |
| Offline progress | ⬜ | Formula specified in `professions.md` §10 |

**Melvor layers you did not list that are worth considering:** a **purchasable upgrade/shop
axis** (spend gathered resources on permanent skill upgrades — Melvor's main resource sink);
**skill completion milestones** (capes/marks as horizontal goals); **set bonuses** on equipment;
and, for combat specifically, Melvor's **Damage Reduction** as a distinct defensive stat from
armour. Also worth an explicit decision: whether we adopt a **combat triangle** (melee/ranged/
magic advantage) — it shapes class and enemy design and is easier to decide now than later.

**Roster for the slice:** not all 19. Recommend **8**: Mining, Forestry, Fishing, Herblore
(gathering) + Smithing, Alchemy, Cooking (processing/production) + Beast Lore (utility, ties
enemies to resources). Mining first — it removes the seeded-ore hack. Each needs 3–5 actions
against the **real 474-material library**, respecting biome/ecology.

**UI.** **Profession Lab**: set levels/mastery directly, run actions, compare active vs passive
yield over N actions, inspect the modifier stack behind an interval.

**Tests.** XP and mastery curves; mastery pool checkpoints; preservation/doubling with seeded
RNG; unlock gating; offline aggregation matches N sequential actions; inputs/outputs resolve to
real materials.

**Docs.** `docs/professions.md` — mark implemented, add the progression-layer table.

**Depends on.** P2 (all bonuses are modifiers), P4 (tools). **Blocks.** P9.

**Acceptance.** Train Mining passively and actively, feel the difference, hit a mastery
checkpoint, see an interval drop with a visible reason, and mine ore that feeds Smithing that
feeds the emergent crafting system.

---

### P8 — Enemy Ecosystem + AI

**Current status.** 2 enemies, 1 ability each, random selection, single loot item each.

**Reuse.** `ActorDefinition`, `Combatant.FromCharacter`, the loot path.

**Missing.** Breadth, movesets, AI profiles, resistances/vulnerabilities, harvestable anatomy,
elites/bosses.

**Architecture / data.** Extend `ActorDefinition` with tags, resistances, vulnerabilities,
moveset ref, AI profile ref, loot table, **harvestable resources**, biome/depth availability,
and elite/boss modifiers as *composable* data. Elite modifiers should be a modifier set applied
over a base enemy, not a duplicated definition.

**Roster for the slice: 8–10** in the Dark Forest — roughly 5 normal archetypes (melee pressure,
ranged, heavy telegraph, fast/evasive, caster), 2 specials, 1–2 elites, 1 boss. Each must
**exercise a distinct combat mechanic**; that is the selection criterion, not variety for its
own sake.

**Ecology matters:** creature anatomy maps to the real material library (hide/bone/gland/venom),
so Beast Lore harvesting feeds crafting. No "Enemy Loot".

**UI.** **Enemy Lab**: spawn any enemy, inspect stats/resistances/moveset/AI, watch it fight.

**Tests.** Moveset validation (every referenced move exists and is usable by that actor); AI
selects legal actions under constraints; loot and harvest resolution; elite modifier application;
encounter completion.

**Docs.** New `docs/enemies.md`.

**Depends on.** P3, P6. **Blocks.** P9.

**Acceptance.** Fight all archetypes; each requires a different response; harvest a creature and
craft with the result.

---

### P9 — Integrated Realm Vertical Slice

**Current status.** Dark Forest, 10 locations, 2 depths, travel/descend/extract, knowledge counter
that unlocks nothing.

**Reuse.** `RealmRun`, `RealmDefinition`, `RealmExtraction`, the depth/extract decision.

**Missing.** Density, location-type variety, loadout selection, camp, hazards, knowledge that
does something, loot tables.

**Architecture / data.** Widen the *existing* realm per your §16 — no new biomes. Add location
types already documented (`realms.md` §11): Camp, Shrine, Merchant, Elite, Boss, Hidden, Hazard.
Depths 1–4. Loot tables replacing single guaranteed drops. Pre-run loadout selection. Realm
Knowledge unlocking **information and options**, per `realms.md` §8 — not damage bonuses.

**UI.** Pre-run loadout screen; a richer realm view; extraction decision with real stakes shown.

**Tests.** Full-loop integration test extending `FullLoopTests`: gather → craft → fabricate →
equip → enter → fight → harvest → extract → save → reload → verify.

**Docs.** `docs/realms.md`, `docs/vertical-slice.md` updated to the real slice.

**Depends on.** All. **Acceptance.** The §22 acceptance test, end to end, in one sitting.

---

### P10 — Balance & Content Pass

Only once the loop is fun. Widen counts, tune numbers, revisit the two provisional constants from
P1 (`QuantizationTuning.PropertyBucket`, `RefinementTuning.StateDeltaCost`).

---

## 6. Cross-cutting: validation and testing

**Validation** (`ContentValidator`, your §19) extends per phase — each new content type brings
its rules and a failing test per rule, as established. Additions: move refs, moveset validity,
class ability refs, enemy moveset/AI refs, form `requires_tags` satisfiability, profession
input/output refs, trait/essence refs, modifier-key validity, and cycle detection for
supersession chains and form composition.

**Determinism** stays the testing backbone: seeded `IRandomSource` everywhere, and the two
permitted probabilistic points in crafting stay the only ones.

---

## 7. Principal risks

1. **P2 is invisible and will feel like a detour.** It is the highest-leverage phase in the plan.
2. **The 0–100 vs 0–5 scale reconciliation in P4** is a combat re-tune wearing a mapping's
   clothing. Budget it explicitly.
3. **Content volume temptation.** Every phase has a "just author 200 of them" trapdoor. The
   roster targets above are deliberately small and chosen for *mechanical coverage*.
4. **Suffix scope.** ~100 documented suffixes, each potentially touching a different system.
   15 spanning the categories proves the architecture; the rest is P10.
5. **Positioning.** Deferred here. If it lands later it will touch every move and enemy — so the
   deferral should be a conscious decision, not drift.

---

## 8. Open questions for you

1. **P1 option (a) or (b)** — Lab against P1 only, or pull traits forward? I recommend (b);
   the spec argues (a).
2. **The Spellblade-type and Summoner-type chassis need names.** `docs/classes.md` §3 says a
   final project-specific name is required. Yours to choose.
3. **Combat triangle** — adopt melee/ranged/magic advantage, or not? Cheaper to decide before
   classes and enemies.
4. **Durability** — I recommend deferring. Confirm?
5. **Positioning** — confirm deferral past this expansion?
6. **`docs/current-state.md`** — delete it as stale, or keep and update?
