# Profession Tools & the Yield Pipeline

> **DECIDED** — settled by the 27 decisions in `effect-foundation.md` §12. Not yet built. Part of the effect-foundation package (`effect-foundation.md`).
> **Supersedes `ActionResolver`.** Extends `professions.md` and GDD §7.
> Labels: **[EXISTING/PRESERVE]** · **[DECIDED]** · **[UNRESOLVED]**

---

# 1. Current state

`ActionResolver.Resolve` is **20 lines** and is the entire profession outcome pipeline:

```csharp
foreach (var output in action.Outputs) produced.Add(output);          // copy outputs verbatim
foreach (var bonus in action.BonusOutputs)                            // roll bonus chances
    if (rng.NextDouble() < bonus.Chance + masteryBonus + activeBonus) produced.Add(bonus.Stack);
var xp = action.Experience * (1 + performance × ActiveXpBonus);
```

That is it. No interval modifiers, no preservation, no doubling, no quality, no rare weighting.

> **Updated after Phase 8 (D40).** Half of this section's gaps are closed. Mastery now buys
> interval reduction, input preservation, output doubling, rare-find chance and opportunity
> odds/risk, all from `game/data/mastery/` — see GDD §7.3. **What remains for E6 is the tool
> half**, and `MasteryBenefitKind` deliberately uses the same six names as the `profession.*`
> modifier keys so the two sources can merge into one pipeline without a rename.

| Gap | Status |
|---|---|
| ~~**Mastery does nothing**~~ | ✅ **Closed (D40)** — six data-driven benefits, all consumed |
| **Six `profession.*` modifier keys exist and none are read** | Still open — they are E6's. Mastery computes directly rather than routing through them, because routing *is* the yield pipeline |
| **No tool concept at all** | Still open — no tool slots, no tool forms. `EquipmentSlot` now has nine members and none is a tool |
| **Melvor layers** | GDD §4.1: 12 of 15 built. Tools, cross-skill bonuses and global passives remain |

GDD §19.3 calls professions *"the least-built system relative to its documented ambition."*
It is also **the cheapest to fix**, because once scoped modifier contributions exist
(`effect-foundation.md` §4.2) almost all of it is plumbing.

The brief's principle 8 — *"noncombat progression deserves real equipment too"* — is the target:
fishing rods that are **items you get excited about**, not `+2 Fishing`.

---

# 2. The yield pipeline **[DECIDED]**

The profession analogue of the damage pipeline. Same discipline: an **ordered list of discrete
stages**, each testable, each appending to a trace.

```
── SETUP ────────────────────────────────────────────────────────────────────
 1  ELIGIBILITY   requirements (level, tool, inputs)  → abort with a reason
 2  INTERVAL      base × Π profession.interval.mult [scope: profession + action]
                  clamped by the modifier key's floor
 3  START                                             → ActionStarted (domain:profession)

── EXECUTION (on the shared TickEngine) ────────────────────────────────────
 4  CONSUME       for each input: roll preservation → consume or preserve
 5  SUCCESS       success/failure roll where the action has one   → ActionFailed?

── OUTPUT ───────────────────────────────────────────────────────────────────
 6  PRIMARY       base quantity × Π profession.yield.mult
 7  DOUBLING      per output: roll profession.double.chance (diminishing)
 8  BONUS         per bonus entry: chance + mastery + performance
 9  RARE WEIGHT   re-weight the rare table by profession.rare.weight [scoped by family]
10  QUALITY       for crafted outputs: quality roll → potency/craft-quality band
11  BIAS          apply output-family bias (tool steers *which* rare, not *whether*)

── SETTLE ───────────────────────────────────────────────────────────────────
12  DEPOSIT       per output                          → OutputProduced (one event each)
13  XP            skill XP × Π profession.xp.mult
14  MASTERY       mastery XP × Π profession.mastery.mult
15  COMPLETE                                          → ActionCompleted (domain:profession)
16  PROCS         trigger rules at depth 1 — duplicate, bonus, cleanse, resource…
```

**Stage 12 raising `OutputProduced` per output is the load-bearing detail.** It gives *one* hook
that gathering, crafting **and loot** all pass through, so `duplicateOutput` is authored once and
works everywhere.

**Stages 2 and 15 use the domain-neutral `ActionStarted`/`ActionCompleted` events**
(`moves.md` §4.2), which is what lets a combat affix and a fishing affix be the same shape.

## 2.1 The Yield Log **[DECIDED — required scope]**

The third legibility artefact. Same argument as the Hit Log and the Reaction Log: a system with
eight multiplicative sources is unplayable if it cannot say why.

```
Fishing — Deep Pool Cast                                    [Fishing 34 · Mastery 41]
  Interval      120 → 92 ticks   (×0.85 Tidecaller Rod · ×0.90 Mastery 41)
  Inputs        Riverworm ×1 → PRESERVED   (18%: 15% rod, 3% mastery)
  Primary       Silverfin ×1
  Doubling      roll 0.07 < 0.11 → Silverfin ×2   (11%: 10% rod, 1% mastery)
  Rare table    Glass Eel weight 4 → 9   (rod: +125% rare weighting)
                roll → no rare
  XP            +48   (×1.10 rod)
  Mastery       +12   (×1.25 rod)
  Procs (d1)    —
  ⇒ Silverfin ×2, Riverworm preserved
```

Every line names its sources. `ModifierContribution.Source` already carries the provenance; this
is just rendering it.

---

# 3. Tools as real equipment **[DECIDED — D-23]**

## 3.1 Slots **[DECIDED — D-23]**

`EquipmentSlot` gains **two Tool slots**. Tools are **worn**, never auto-selected, and they occupy
their own slots so a player is never choosing between a sword and a pickaxe.

| Slot | Forms |
|---|---|
| `Tool.Gathering` | `form:pick` · `form:axe_tool` · `form:rod` · `form:sickle` |
| `Tool.Crafting` | `form:hammer_tool` · `form:apparatus` · `form:mortar` · `form:needle` |

**Why two slots rather than one.** Crafting tools are used in the **Hideout**; gathering tools are
used in the **Realm**. Making them compete for a single slot would force a swap every time the
player changed activity — friction with no decision in it.

**Why worn rather than selected per action.** Being worn is what makes a tool a *preparation
decision*, which is most of what makes tools interesting: *which pick am I taking?* If the game
picks the best applicable tool automatically, tools become a stat lookup, and the GDD §11.7
preparation payoff that the resistance model (D-05a) already invested in gets weaker rather than
stronger.

**Accepted cost:** a player who wants to fish *and* mine on one run must choose. That is the
decision, not a defect. Quick-swap lives in the Hideout.

**[UNRESOLVED — U-12]** whether a tool carried into a Realm is lost on death. Under **D10** gear
is safe by default, so tools currently inherit that. Losing a good pick is exactly the tension the
extraction loop trades in, so this is a natural candidate for the deferred "gear at risk"
difficulty toggle rather than a default.

## 3.2 Tools are fabricated exactly like weapons

Same forms system, same slots, same apertures, same stat maps, same Genome, same affix rolling,
same crafting operations, same Overreach. A `form.rod` declares:

```jsonc
{ "id": "form.rod", "type": "Tool", "profession": "profession.fishing",
  "slots": {
    "shaft": { "requires_tags": ["form:wood","form:bone","form:metal"], "mass_share": 0.60,
               "aperture": { "structural": 1.0, "vital": 0.6, "arcane": 0.7, "thermal": 0.2 } },
    "line":  { "requires_tags": ["form:fiber","form:hide"],             "mass_share": 0.25,
               "aperture": { "structural": 0.7, "vital": 0.4, "arcane": 0.3 } },
    "hook":  { "requires_tags": ["form:metal","form:bone"],             "mass_share": 0.15,
               "aperture": { "structural": 0.9, "toxic": 0.6 } }
  },
  "stat_map": {
    "action_interval":  [ {"slot":"shaft","property":"flexibility","w":0.6},
                          {"slot":"*",    "property":"mass",       "w":-0.4} ],
    "catch_strength":   [ {"slot":"line", "property":"flexibility","w":0.5},
                          {"slot":"line", "property":"hardness",   "w":0.5} ],
    "bait_efficiency":  [ {"slot":"hook", "property":"hardness",   "w":0.7} ]
  },
  "trait_cap": 3 }
```

**Material genetics matter physically, and differently than for a sword:**

| Material | In a longsword | In a fishing rod |
|---|---|---|
| High `flexibility` | bad (rigid weapons want stiffness) | **excellent** — fast, forgiving casts |
| High `mass` | more damage, slower swing | **bad** — slower casts, no upside |
| High `hardness` | armour penetration | good in the hook only |
| High `resonance` + essence | caster stat | **unlocks supernatural catch affixes** |

That table is the whole argument for tools using the same system. **The same material is
excellent in one form and useless in another** — `emergent-item-system.md` §16.2's rule,
extended to professions for free. And it means the flexible bogwillow nobody wanted for a sword
is suddenly the best rod shaft in the game.

## 3.3 Tool affixes are ordinary affixes

No new machinery. A tool affix is an `AffixDefinition` whose `grants` target `profession.*`
modifier keys with a **scope** (`effect-foundation.md` §4.2):

```jsonc
{ "id": "affix.of_the_patient_tide", "slot": "suffix", "family": "fishing_interval",
  "eligibility": { "forms_any": ["rod"], "requires": [{ "property": "flexibility", "min": 55 }] },
  "weight": { "base": 100, "scale": [{ "property": "flexibility", "per10": 12 }] },
  "tiers": [ { "tier": 3, "requires": {"flexibility":55}, "range": [0.94, 0.97] },
             { "tier": 2, "requires": {"flexibility":70}, "range": [0.90, 0.94] },
             { "tier": 1, "requires": {"flexibility":85}, "range": [0.85, 0.90] } ],
  "grants": [ { "type": "stat", "key": "profession.interval.mult",
                "scope": "profession:fishing", "value": "$roll" } ],
  "description": "$roll×  Fishing action interval." }
```

And a **triggered** tool affix is a `RuleGrant` on the shared action events:

```jsonc
{ "id": "affix.of_the_second_bite", "slot": "prefix", "family": "fishing_duplicate",
  "class": "trigger",
  "eligibility": { "forms_any": ["rod"], "requires": [{ "property": "growth", "min": 40 }] },
  "grants": [ { "type": "rule", "rule": {
      "id": "second_bite", "event": "OutputProduced",
      "when": [ { "kind": "actionHasTag", "text": "profession:fishing" },
                { "kind": "targetHasTag", "text": "class:edible" } ],
      "chance": "$roll",
      "effects": [ { "kind": "duplicateOutput" } ] } } ],
  "description": "$roll% chance to catch an additional fish." }
```

**Identical machinery to a combat affix.** Same engine, same validator, same Item Lab, same
tests. That is the payoff of the shared vocabulary.

---

# 4. New modifier keys **[DECIDED]**

The existing six `profession.*` keys cover most of it once scoped. Additions:

| Key | Kind | Scope | Cap | Serves |
|---|---|---|---|---|
| `profession.rare.weight` | multiplicative | `profession` + `family` | 4.0 | rare ore/fish weighting; family bias |
| `profession.quality` | additive | `profession` | 0.5 | catch/craft quality band |
| `profession.success.chance` | additive | `profession` | 0.95 | actions with a failure mode |
| `profession.harvest.pen` | additive | `profession` | — | vs `harvest_resistance` on nodes |
| `craft.craftsmanship` | additive | — | — | fabrication quality (§5) |
| `craft.catalyst.mult` | multiplicative | — | 2.0 | catalyst effectiveness |
| `craft.potency.retain` | additive | — | 0.25 | offsets the potency weighted-mean penalty |
| `craft.variance.mult` | multiplicative | — | floor 0.25 | narrows outcome variance — the "precision" stat |
| `loot.rare.weight` | multiplicative | — | 4.0 | Realm loot, same shape as profession rare weighting |

`craft.integrity_cost.mult` and `craft.quality` and `craft.discovery.chance` **already exist** and
are unread. They become live in E6.

**`profession.harvest.pen` earns its slot**: `harvest_resistance` is an authored property on
every material with role `Sourcing`, explicitly *"read only by gathering"*
(`emergent-item-system.md` §2.2) — and today nothing reads it. A pickaxe that penetrates harvest
resistance is the reason a hard material makes a good pick, and it finally gives that property a
job.

---

# 5. Crafting tools and the reaction engine **[DECIDED]**

A smithing hammer influences the **reaction algebra** (`emergent-item-system.md` §8) without
changing a line of it. Every lever is already a coefficient:

| Tool affix | Where it lands | Existing spec reference |
|---|---|---|
| reduced crafting interval | pipeline stage 2 | — |
| input preservation | stage 4 | — |
| **increased Craftsmanship** | `quality_norm` in §7.4 | drives potency ceiling `max(input)+8×quality_norm` |
| **reduced Integrity damage** | `craft.integrity_cost.mult` on §6.2's cost | key exists, unread |
| increased chance of exceptional fabrication | the `CraftQuality` band roll | §7.4 |
| **bias toward a material-property family** | nudges the process **channel weights** | §7.2 — the single most interesting tool effect in the game |
| improved catalyst effectiveness | `craft.catalyst.mult` on catalyst rate modification | §7.1 |
| **narrowed variance** | `VariancePerturbation` magnitude | §12.3 — high skill already narrows variance; a precision tool does too |
| increased Mastery XP | stage 14 | — |

**Channel bias is the standout.** A hammer that biases `heat` means the same reagents in the same
order land in a *different* region of state-space — so the tool changes **what you can invent**,
not merely how efficiently you invent it. That is a tool affix worth chasing for hundreds of
hours, and it costs one coefficient.

⚠ **Constraint:** tools must never break the algebra's invariants. Convergence can still never
exceed the strongest input (§8.2); potency is still a weighted mean (§6.1); integrity is still
monotonically non-increasing (§17). Tools move **rates and costs**, never **bounds**. Validator
rule: no `craft.*` key may target a bound.

---

# 6. Melvor layers — what this closes

| GDD §4.1 layer | Status | After E6 |
|---|---|---|
| Skill XP and levels | Built | — |
| Action intervals | Built | now modifiable, scoped, capped |
| Active vs passive | Built | — |
| Mastery XP / levels | *stored but unused* | **read at 4 pipeline stages** |
| Mastery Pool + checkpoints | Needs Design | still needs design |
| Interval reduction | Planned | ✅ stage 2 |
| Resource preservation | Planned | ✅ stage 4 |
| Doubling / increased yield | Planned | ✅ stages 6–7 |
| Level-based unlocks | Partial | unchanged |
| Mastery-based unlocks | Planned | hooks exist (`ActionCompleted` + mastery conditions) |
| **Equipment affecting skills (tools)** | Planned | ✅ **the whole of §3** |
| Cross-skill bonuses | Planned | scoped modifiers make it trivial |
| Global/account passives | Planned | unscoped contributions |
| Offline progression | Designed | unchanged — the formula now has real modifiers to read |
| Progression milestones | Needs Design | still needs design |

**Ten of fifteen layers land in one slice**, because the modifier registry was built data-first
and the scoped-contribution change makes it addressable.

---

# 7. Validation and testing **[DECIDED]**

| Rule | Catches |
|---|---|
| Every `profession.*` grant has a scope matching the key's `scoped_by` | a global fishing bonus |
| Every scope value names a real profession/action/family | `profession:fishng` |
| Tool forms declare a `profession` and their affixes' forms match | a mining affix on a rod |
| Every tool form's `stat_map` references only real properties | typo |
| No `craft.*` key targets an algebra **bound** | tools breaking crafting invariants |
| Every profession has ≥1 tool form once tools ship | a profession with no gear |

**Tests.** Deterministic seeds throughout.
- Pipeline **golden traces** — fixed seed + fixed modifiers → assert the whole Yield Log.
- **Distribution (N ≥ 100k)** — preservation, doubling and rare-weight rates match the resolved
  modifier values within tolerance.
- **Cap enforcement** — twenty preservation sources never exceed 0.80.
- **Scope isolation** — a Fishing interval modifier does not affect Mining. (Trivial to state,
  easy to break, worth a permanent test.)
- **A→B comparison** — the Profession Tool Lab's 10k-run comparison, asserted as a test for two
  fixture tools.

---

# 8. Open questions

- **U-10** Tools worn vs selected per action. Recommend **worn**.
- **U-11** Do tools have durability? Ties to U-3. Recommend **no**, consistent with weapons.
- **U-12** Can tools be taken into a Realm and used there? GDD §7 says professions are trainable
  in Realms *"where time passing carries real risk"* — so yes, and losing a good pick on death
  would be a genuine tension. But it collides with **D10 (gear safe on death)**. Recommend: tools
  follow the same gear-safe rule for now; revisit with the difficulty toggle.
- **U-13** Should Beast Lore harvesting use this pipeline? It should (`OutputProduced` on
  creature parts), but it needs the harvest system to exist first.
