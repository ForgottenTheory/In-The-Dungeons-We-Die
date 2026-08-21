# Presentation Architecture — the three languages

> **Source of truth for how the game talks to the player about materials, crafting and items.**
> ⚠ §1–§5.1 record the correction as run against the PROPERTY system (R0–R4); that system and
> its grammar (tiers/pips/glyphs/trends) were deleted in migration Phase 7 (D54). The living
> rules are §0 and §6; the living voices are §5.2 (the identity system, Phase 6/D53).
> Adopted 2026-08-16 from a user design directive: the emergent simulation is sophisticated but
> the player-facing experience speaks the wrong language. This is a foundational correction,
> not UI polish — loot, combat depth and profession work will inherit whatever language is
> established here, so it lands **before** them (slices R0–R4, then the C2c playtest).
>
> Mechanics live in `emergent-item-system.md`, `affixes.md`, `damage-and-defense.md`; the
> player arc lives in `how-it-plays.md`. This document owns the **translation between them**.

---

# 0. The rule — hard invariants

The game has three languages, and every surface must know which one it speaks:

```
SIMULATION LANGUAGE            0–100 properties, rates, severities, coefficients, weights.
  (the cause layer)            For the engine, tests, Advanced views, Assay depth, debugging.
        ↓ one-way
PLAYER CRAFTING LANGUAGE       Icon + qualitative state + intensity + direction + context.
  (the reasoning layer)        For choosing, combining, predicting, experimenting.
        ↓ one-way
GAMEPLAY / ITEM LANGUAGE       Damage, speed, Armour, Crit, Block, Thorns, resistances,
  (the payoff layer)           statuses, triggers, Move modification, resource effects.
```

1. **Raw simulation values never appear on normal play surfaces.** Exact 0–100 values,
   process severities, transfer rates, coefficients and weights live only behind Advanced
   toggles, Assay depth, and debug/lab views.
2. **The semantic layer is a one-way, derived read-model in Core** (`Dungeons.Presentation`).
   It reads simulation state and never writes it; it is deterministic and unit-tested; it is
   the **only** path from simulation state to player-facing crafting text. Sibling rule to
   "formatting never touches mechanics": **presentation never feeds back into simulation.**
   Display tiers are for eyes only — identity quantization (`QuantizationTuning`) is untouched
   and must never read presentation code.
3. **A player-facing modifier ships only when its mechanic resolves.** Every modifier must
   answer *"what actually happens when I equip this?"* with a real combat/profession behaviour.
   Internal content referencing unbuilt systems stays visibly inert (D23); **player-offered**
   content may not. Each affix family lands paired with its mechanic (§4 ledger).
4. **Items speak gameplay language.** A finished item's primary card is identity → combat
   stats → effects; material properties are causes, shown as influence, not as the reward.
5. **Display metadata is data.** Property glyphs, player-facing glosses and role text live on
   `PropertyDefinition` (JSON), never in code switches — properties remain data (CLAUDE rule 6).
6. **Knowledge gates legibility, never capability** (D29). The semantic grammar is visible from
   the first craft; Assay/codex deepen *precision* (proximity hints, genome readouts, known
   rules), never switch features on.

---

# 1. The audit (2026-08-16)

## 1.1 What each surface says today

| Surface | Today | Verdict |
|---|---|---|
| Process picker | `Smelt — thermal, severity 0.6, Smithing L1` + `Opens: hardness 0.3, mass 0.35…` | Simulation numbers as primary text |
| Substrate inspector | `Iron Ore — potency 40, integrity 100, gen 0` + top-5 `prop value` + raw tags | Number wall; no "what is this good at" |
| Reagent/catalyst pickers | `Name ×qty` only | No profile, no receptiveness, no fit |
| Pre-commit projection | `Expect: <name> (never made before) / Potency 39 Integrity → 87 (cost 2.6 ± 1.1) / ⚠ 12% chance…` | The best surface — names and warnings are player language — but potency/integrity are bare numbers, and **no property movement is shown before commit** |
| Reaction Log (post-commit) | Structured entries, styled by kind, "because" language | The one excellent surface; numbers-first but reasons present |
| Fabrication slot pickers | `slotName:` + `Name ×qty` | Binary eligibility only; no fit reasons, no expression preview |
| Fabrication reveal | One log line: `[Fabricate] ✦ <name> — trait.emberveined (1 dormant).` | Raw trait **ids**; no stats, no drawback, no hierarchy — the experiment has no payoff screen |
| Item labels (inventory/equipment) | `ItemFormat.InstanceLabel` → `Name #7 (mass 12, hardness 55, …)` | The exact property wall this document bans, enshrined as the standard label |

## 1.2 What structured data already exists to translate (all BUILT)

The simulation already computes everything the player language needs. **Nothing in the algebra
changes.**

- `PropertyChange { Property, Before, After, Kind }` per reagent step, with
  `PropertyChangeKind ∈ { Channel, StructuralBlend, Dilution, Pruned, Annihilation,
  DerivedResistance }` — direction, reinforcement, suppression and opposition are typed facts.
- `IntegrityProjection { ProjectedIntegrity, ExpectedCost, CostSpread, DestructionChance,
  IsCertainDestruction, IsAtRisk }` — risk bands are a mapping.
- `CraftProjection { ProjectedPotency, ProjectedName, WouldBeFirstDiscovery, Preview }` — the
  pre-commit trace exists; it just isn't rendered semantically.
- `TraitDefinition { Condition: property ranges, Consumes, Drawback, Category }` — emergence
  proximity ("within reach of Resilient — needs flexibility") is arithmetic over authored data.
- `FormTemplateDefinition.Slots { RequiresTags, MassShare, Aperture }` + `stat_map` — slot fit
  and contextual meaning ("the edge reads hardness heavily") are derivable, per form, from data.
- `EssenceAlgebra` + essence definitions (anchors, opposition) + resonance capacity — essence
  readings and strain warnings.
- `EquipmentResolver` — resolved moves (mass-adjusted packets/windup) and armour/lane
  resistances in real combat units since C2b: the item card's combat stats exist today.
- Naming ladders (`name_grammar`, 4 flavour words per property) — the naming voice.
- `PropertyDefinition` registry (JSON) — the natural home for display metadata.

## 1.3 The gaps

| # | Gap | Fixed in |
|---|---|---|
| G1 | No semantic tier/trend/risk/fit layer exists | R1 |
| G2 | `CraftProjection` doesn't expose typed per-property changes (only log text carries Before/After; `Kind` survives only inside step results) | R1 (read-model exposure, no algebra change) |
| G3 | Slot eligibility is binary; fit quality and reasons are computed nowhere | R1 (compute) + R3 (render) |
| G4 | No reveal hierarchy; `InstanceLabel` property wall is the item language | R3 |
| G5 | `PropertyDefinition` lacks glyph/gloss display metadata | R1 (schema + content) |
| G6 | No knowledge gating anywhere (everything shows to everyone) | Acceptable now: semantics ungated, numbers behind Advanced; Assay/codex depth arrives E5/P6 per D29 |
| G7 | **The payoff layer is absent** — no genome, innates, or rolled modifiers (E5 unbuilt); `move_modifiers.json` is an empty array awaiting its author | R4 |
| G8 | Modifier-key drift vs the lane model: registry still has `combat.resist.slashing/crushing/piercing/magic` (4-type) while the pipeline runs 8 lanes; `combat.dodge.chance` is D-07 debt; no keys exist for thorns, penetration, status application/duration/potency | R4 alignment pass |

## 1.4 Fabricated-item reality check

A fabricated item today carries: derived damage/speed (mass on packets), armour and lane
resistances (hardness/response properties), granted moves, expressed traits (+ dormant), name.
It carries **no** innates, rolled modifiers, or triggered effects — those are E5. So the flat
reveal is half presentation failure (fixable now, R3) and half missing payoff layer (R4). Both
halves are in scope; they are different work.

---

# 2. The hybrid grammar

Every material/property reading combines six elements. None is optional in the design; a
surface may compress but never revert to bare numbers.

**A. Icon.** One glyph per property, aspect and essence, authored as data on
`PropertyDefinition` / essence definitions. Placeholder Unicode now (greybox rule: the grammar
matters, the art doesn't yet).

**B. Qualitative state.** `PropertyTier`: **Trace · Low · Moderate · Strong · Extreme** (absent
= unshown). One shared threshold function in `Dungeons.Presentation` (`PresentationTuning`),
used by every surface, so "Strong" always means the same thing. The naming ladders
(Warmed → Emberlit → Cindered → Searing) remain the **naming** voice and map onto tiers ≥ Low;
they are flavour, not UI state (decided: neutral tier words for reading, ladder words for
names).

**C. Visual intensity.** Pips: `●●●○○` = tier ordinal of five. Same pip count everywhere.

**D. Direction / change** (crafting previews and logs). `Trend`, derived from typed
`PropertyChangeKind`, never re-inferred from numbers:

| Trend | From | Reads as |
|---|---|---|
| Rising | Channel, Δ>0 | `⬆` strengthening (double glyph past a tier boundary) |
| Falling | Channel, Δ<0 | `⬇` weakening |
| Drifting | StructuralBlend | `≈` settling toward the mixture |
| Fading | Dilution | `▽` washing out (off-channel) |
| Opposed | Annihilation | `⇄` fighting an opposite — strain |
| Vanishing | Pruned | `✕` lost to trace |
| Emerging | 0 → nonzero | `✦` newly present |

**E. Contextual meaning.** Derived from real data, never generic strings: slot fit from
`stat_map` weights + apertures ("the edge reads hardness heavily — strong structural fit
here"), process receptiveness from the medium ("gives freely under thermal — highly unstable"),
trait proximity from authored conditions ("within reach of *Resilient* — needs more
flexibility"), essence capacity from resonance ("a worthy vessel: strong resonance, capacity to
spare").

**F. Advanced detail.** Exact values, coefficients, arithmetic — one toggle away, never the
default. The Reaction Log keeps its numbers (it is the tutor and the debugger) but leads each
line with the semantic reading. Labs and debug views are exempt from all of this by design.

---

# 3. Surface specifications (target state)

**Pickers.**
- Process: name, profession gate, **what it works on and what it moves, in words** ("Thermal —
  works metals; drives heat and hardness; violent"). Severity shows as a gentleness/violence
  word, exact number in Advanced.
- Substrate/reagent: name ×qty + top-tier glyph strip (`◆●●●●○ ⚡●●●○○`) + one receptiveness
  phrase for the selected process ("gives freely under thermal").
- Catalyst: what it steadies or accelerates, in words.

**Pre-commit preview** (the §6.2c projection, re-voiced). Groups, not a paragraph:
`Strengthening` (rising properties with glyph+pips+arrows) · `Weakening / Washing out` ·
`Opposition` (⇄ with strain note) · **Risk band** — `SAFE · COSTLY · STRAINED · PERILOUS ·
DESTROYS` mapped from `IntegrityProjection` (destruction % shown from PERILOUS; the §6.2c
fairness guarantees are unchanged, re-dressed) · `Emergence` — trait proximity hints
("? something unusual is close" → named once discovered, per knowledge rules) · first-discovery
flag. Advanced toggle reveals today's exact text and the numeric trace.

**Fabrication.** Per-slot: fit reading with reasons (eligibility stays tag-law; fit quality is
advisory), expression preview ("Emberveined would express on the edge; dormant in the core"),
and a projected stat direction line ("heavier, slower, harder-hitting than your current
blade" when an equivalent is equipped). Reveal follows the §6 hierarchy: **identity → combat
stats (damage by lane, speed, armour, resistances, granted moves) → [R4: innates → rolled
modifiers] → traits as named effects with drawbacks → strengths/weaknesses → one line of
material influence** ("the Stormglass edge carries the charge; the oak core keeps it light").
Properties: Advanced only.

**Item cards everywhere.** `InstanceLabel`'s property wall is retired from player surfaces
(survives in labs/debug). Inventory/equipment rows use identity + a compact gameplay strip.

**Reaction Log.** Semantic lead, numeric tail: "⚡ Charge — strengthening (Moderate → Strong) —
the process drove it hard *(38 → 61, rate 0.8)*" with the parenthetical styled as Advanced.

**Knowledge gating (now vs later).** R1–R3 ship the grammar ungated (numbers behind Advanced —
that alone is the correction). Assay depth, codex naming of emergent hints, and the Genome
Readout arrive with E5/P6 exactly as designed (§15, `affixes.md` §2.3), deepening precision per
D29's spectrum (Unknown → Observed → Understood → Mastered), never gating the grammar itself.

---

# 4. The gameplay vocabulary (R4 charter)

`effect-catalog.md` already designs **254 modifier concepts across 21 families** that cover the
directive's required families (offence, timing/crit, penetration, defence, block/parry/dodge,
avoidance, **Thorns/retaliation (16 concepts — a full build family)**, ailments, CC, resources,
recovery-as-Barrier, triggers, Move modification, conversion, conditional, profession tools,
Anomalous, essence-conditional). The gap is not concepts — it is **data, keys and mechanics**:

1. **Affix architecture** per `affixes.md` (genome → eligibility/weight/tier; innates; slots) —
   E5's front half, pulled forward.
2. **Representative content, breadth not balance**: 2–5 affixes per family so every family is
   real, rollable and displayable; the full catalog fills over later slices.
3. **Modifier-key alignment pass** (G8): add the eight-lane resistance keys, thorns keys,
   status application/potency/duration keys, penetration keys; retire/replace the 4-type resist
   keys; resolve D-07's `dodge.chance` → `evade/avoid` swap **at the same time** (it is already
   filed as a decision, and this pass is the natural moment).
4. **The mechanics ledger** — a family may ship only with its mechanic (invariant 3):

| Family | Mechanic it needs | Status today |
|---|---|---|
| Ailment application/potency/duration | application-chance source + duration/potency mods | **Plumbed and tested; waiting for affixes to be the source** — cheapest win |
| Thorns / retaliation | a retaliation stage in the hit pipeline | Designed (`damage-and-defense.md`); **not implemented** |
| Parry | gear-declared parry window + counter-window | Designed; not implemented |
| Evade / lane avoidance | avoidance rolls in the defence order | Designed; not implemented (D-07) |
| Penetration / exposure / inversion | resistance-order stages | Designed; not implemented |
| Barrier-granting | Barrier absorption in HitPipeline | **Known debt** (HANDOFF) — implement before any barrier affix ships |
| Triggers (On Hit/Block/Crit/…) | event bus + TriggerRules | **BUILT** — affixes carry rules |
| Move modification | 11-op system | **BUILT and empty** — affixes are its author |
| Resources / attributes / speed / crit | modifier keys | **BUILT** (51-key registry) |

---

# 5. Implementation map

| Slice | Delivers |
|---|---|
| **R0** | This document; the audit; GDD/CLAUDE/ROADMAP updates ✅ |
| **R1** | `Dungeons.Presentation`: tiers, trends, risk bands, slot-fit readings, material readings; `PropertyDefinition` display metadata; `CraftProjection` exposes typed changes; `CraftFormat` re-voiced; Advanced formatter; tests pin determinism and the no-raw-numbers rule |
| **R2** | Bench UX: pickers + preview speak the grammar; Advanced toggle; user editor-verifies |
| **R3** | Fabrication fit/expression previews; the reveal hierarchy; item cards; `InstanceLabel` retired from player surfaces |
| **R4** | Affix architecture + representative families + key alignment + paired mechanics + translated Genome panel — plan below |
| Then | **C2c playtest** — the new language and the parked balance backlog, together |

## 5.1 The R4 plan (approved 2026-08-16; four sub-decisions locked)

**R4a — lane alignment.** `combat.resist.slashing/crushing/piercing` → **`combat.resist.physical`**
(decided: collapse — per-type weakness stays in D-02 enemy vulnerabilities); add the six
aspect-lane resist keys; D-07 executes (`dodge.chance` → `evade.chance`, add `avoid.lane`,
danger-capped). Content migrated, read paths + tests updated. Keys land here; affixes wait for
mechanics.

**R4b — Genome + engine + already-resolving families.** Genome per `affixes.md` §2.2, stored at
fabrication, **persisted — save v6 (decided)**. `Dungeons.Affixes` per §3.2; grants reuse the
Grant vocabulary (stat → scoped contributions with provenance; rule → TriggerRules on
equip/unequip); §3.5 anti-stacking. Rolling per §4 on `IRandomSource`, 3+3 uniform (U-8 v1);
**innates as `class:"innate"` affix definitions (decided)** — deterministic eligibility → weight
rank → top ≤3, potency-positioned value, no variance, never rerollable (U-7). **Exotic class
deferred to E7 (decided)**; Signature waits for P4; Anomalous is Overreach-only. ~30–36
representative affixes across offence / character / defence / resource / **ailment application
(the long-missing source)** / triggered / move-mod / conditional. Reveal + strips gain
innate/modifier lines; the fabrication preview gains the pre-roll genome translation; the §2.3
Genome Readout ships with exact numbers behind Advanced. §8 validation table + seeded
distribution tests. Debug reroll + roll-inspector dump (the Item Lab's seed).

**R4c — mechanics with their families**, one row at a time, each with Hit Log trace lines and a
worked-example probe. **R4c-1 ✅ (2026-08-16):** retaliation shipped as pure content — thorns
is a depth-1 triggered damage rule per §7 of `damage-and-defense.md`, and the existing proc
safety already contained the loop (when-hit `DamageTaken` / on-block `Blocked`, perfect blocks
included / after-dodge `Dodged` / poison barbs; e2e-pinned). Evade lives (untelegraphed only,
zero RNG draws without a source), lane avoidance negates per-packet (arcane exempt by
construction), flat lane penetration applies after the cap (eats overcap; exposure — a
negative pre-cap contribution — does not; pinned), capped/raw shows on the armour summary, and
the on-crit trigger family exists (`CriticalLanded` was already on the bus).
**R4c-2 ⬜:** Parry (encounter stance, gear-granted, `Parried` event) · Barrier absorption
(HitPipeline debt) · status potency/duration keys (StatusController wiring) · the
`DamageMitigated` event → reflect-% + **stored retaliation** (the archetype headline) ·
move-modifier affix grants (moveset seam) · inversion/ignore (Exotic tier, with E7).

**Excluded from R4:** operations/Overreach/Anomalous (E7) · Signature affixes (P4) · Exotic
rare-roll (E7) · profession/realm families (E6) · the full 150–250 catalog · balance numbers
(C2c) · durability-dependent affixes (U-3) · trade (U-9).

**Run order:** R4a+R4b as one run → checkpoint → R4c (split per mechanic if a row runs deep).
Then C2c plays the whole language.

# 5.2 The identity-system voice (migration Phase 6, 2026-08-21, D53)

The identity redesign made the simulation itself legible, so its presentation layer is
smaller than the property system's: no tiers, no pips, no glyph strips — names, rung words,
ladder words and sentences. All of it lives in `Dungeons.Presentation`, one-way and
unit-tested, replacing the engine spelling (`on_block → store → bulwark 0.15`) the panels
deliberately showed until this pass.

| Reader | Renders | Voice |
|---|---|---|
| `SentenceReadings` | one `ItemEffectSentence` → one player line | "On Block: 30% chance to gain Barrier (4)". Bound to what `SentenceAssemblers` actually compiles: drain reads damage-plus-recovery, store reads charge-plus-band, exchange repeats the assembler's own arithmetic. Modifier units derive from the key registry (multiplicative = distance from ×1, capped-fraction = percent, else flat) — never a hardcoded key list |
| `ItemReadings` + `SemanticFormat.Item`/`ItemStrip` | the item card's identity layer | `Identities:` with rung words · effect lines labelled `Guaranteed:`/`Signature:`/`Drawback:` (bare lines are the roll — D50's taxonomy stays legible) · dormant identities join the dormant line |
| `MintReadings` | the forge preview | the draw table as **likelihood words** (Likely / Possible / A long shot, measured against the uniform share — D53); breach rows read "beyond its families"; exact scores in the Advanced toggle |
| `VerbReadings` | bench refusals + change lines | refusals in words (never enum names); previews/outcomes **diffed from the states the engine produced** — awakens / settles in / deepens / is ejected, condition and workmanship in ladder words, §4 risk odds intact. The engine's `VerbStep` text is the Advanced voice |
| `IdentityMaterialReadings` | the bench substrate inspector | every §11.2 facet in a sentence: stakes and slots, latents, carrier fidelity, condition + meaning, workmanship word, overfill meaning |
| `AssayLens.IdentityMaterial` | the Assay panel (re-aimed, D45/D48) | same facts, redacted by rung: always stakes + overfill (D42; chosen risk is never hidden) → Vessel → Latency → Latent names → **Leanings** (D53: strongest few, in words; themes never, §6.1) → Potential ("on gear, promises …", quoting `ItemEffectResolver.FloorPayloadOf` — the same rule generation mints from) |

Words the pass fixed: ranks render as the §4 rung ladder (*improved / advanced /
build-changing*; basic unmarked; numerals banned by D44), workmanship 0–100 renders as
*rough / decent / fine / excellent / masterwork* (`IdentityPhrases.QualityWord`), and the
`Stability`/`Condition` enum words are themselves the player vocabulary (§10.3–§10.4).
Thresholds live in `PresentationTuning` (likelihood multiples, quality floors, leanings
count). Picker rows everywhere carry `GameRoot.MaterialStakeSummary` — stakes, overfill,
wear — because an Unstable component is a choice made at the menu.

Phase 6 also caught the shipped `bulwark` payload authoring a delta (0.08–0.2) on a
multiplicative key — an 85% Block-Strength *nerf* wearing a buff's description. Fixed to
factors (1.08–1.2) behind a new validator fence (`MultiplicativePayloadRangeFloor`): a
truthful renderer flushes out lying data, which is this architecture doing its job.

# 6. Acceptance test (recorded from the directive, verbatim in intent)

A. Raw numbers are no longer the primary normal-player representation. B. Materials read as
icon + state + intensity + context. C. Previews communicate reinforcement, opposition, change,
risk, emergence. D. Players can experiment without formulas. E. Finished items display familiar
RPG stats/effects. F. The simulation still determines all outcomes. G. The effect vocabulary is
broad enough for genuinely different builds (Thorns included). H. Advanced players can inspect
deeper data. I. Works for generated and dropped equipment alike (sealed uniques flow through
the same card). J. Documented here as foundational; downstream systems inherit this language.
