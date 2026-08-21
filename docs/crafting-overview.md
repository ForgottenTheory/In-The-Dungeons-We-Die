# Crafting Overview — the identity stack in one place

> The one-stop map of the crafting system as shipped, with real content counts and every
> tuning constant located. Design rationale lives in `identity-foundation.md` (the
> foundation), `transformation-verbs.md` (the bench) and `presentation-architecture.md`
> (the player language); this document is the top-down index.
>
> **History note:** the property-based system this game shipped first (21 hidden 0–100
> properties, the reaction algebra, genome → affix rolling, traits, essences) was replaced
> by the identity redesign across migration Phases 1–7 (D42–D54, 2026-08-20/21) and deleted
> in Phase 7. Its docs are deleted too; git history is the archive.

---

## 1. The stack, top to bottom

```
MATERIAL        capacity · active identities (ranked 1–4) · latents · base stats · profile
   ↓ the verb bench (10 verbs as content actions; professions gate and train)
WORKED MATERIAL fingerprinted, registered emergent, deposited to the bag
   ↓ the identity forge (compose → item-effect pipeline → mint)
ITEM            base delivery + effect sentences (floor / generated / signature / drawback)
   ↓ the equip seam (sentences recompile to grants, deterministically)
COMBAT          stat contributions · trigger rules · gauges · move modifiers
```

- **Materials** (`game/data/materials/`, **1,448**, all migrated — D52): `capacity` (1–4),
  `identities` (**53** materials ship active identities: motes r1, essences/hearts/runes r2,
  cores r3), `latent` (~413 carriers), `base` (Heft/Bite/Toughness/Give on 0–10, structural
  stock only), `signature_profile` (**47** curated personalities; themes are hidden scoring
  metadata, §6.1).
- **The identity roster** (`game/data/identities/`, **24**, pinned to D44 by test): the named
  mechanical doors (Dense, Vital, Ember…). A new identity is a design decision, never casual
  content.
- **The grammar** (D43): **22 triggers** (each bound to a published `GameEvents` event, plus
  the one standing shape `while_worn`) · **11 behaviors** (each with one registered assembler
  in `SentenceAssemblers`; detonate/spread/bloom deliberately absent until their machinery
  exists) · **29 payloads** (`signature_payloads/` — families+rungs, one floor per owning
  identity, validator-enforced; every payload binds to machinery that already resolves — the
  D30 fence) · **16 themes** (never player-facing).
- **The bench** (`core/Crafting/Identity/IdentityCraftingEngine` + `VerbActionRunner`):
  ten verbs — Process, Fuse, Reveal, Transfer, Develop, Extract, Displace, Refine, Restore,
  Expand — offered as **53 verb actions across 11 professions** at their stations
  (`game/data/verb_actions/`, D47/D48). The bench trains (XP + mastery; mastery shaves risk
  via `VerbRequest.RiskReduction`, engine-capped). Preview parity everywhere: the projection
  is the commit minus dice.
- **The forge** (`IdentityFabricationEngine` + `IdentityEquipmentComposer` +
  `ItemEffectResolver`): **23 forms**, all identity-forgeable (D54), offered whole at the
  **6 assembly stations** (`has_assembly` — forms need no per-station routing). Compose =
  D51 union/cap/dormancy + D46 base reads; effects = D50's three categories (floor
  guaranteed · generated drawn from the scored table the preview shows · Signatures earned)
  plus the §10.3 volatile drawback; the noun may be one of D34's ~120 `name_variants`,
  picked deterministically from the derived definition id.
- **The equip seam** (`GameRoot` + `ItemEffectResolver.CompileAll`): persisted sentences
  recompile to stat grants, trigger rules, gauges and move modifiers whenever worn — grants
  are never stored (save v14).
- **The reading path** (`Dungeons.Presentation`, Phase 6/D53): `SentenceReadings` ·
  `MintReadings` · `VerbReadings` · `IdentityMaterialReadings` · `AssayLens` — the only path
  from simulation state to player-facing crafting text (D30).

## 2. The legacy remnant

The fixed-interaction path (`CraftingExperimentSystem`, `crafting_interactions/`) survives
solely to keep the **Healing Salve** brewable until consumable forms land (P5c). It is a
recipe lookup, not a crafting system.

## 3. Where every tuned number lives

| Class | Owns |
|---|---|
| `IdentityCraftTuning` | verb costs and risk chances, rank economy, capacity ceilings, quality steps, trace weights, `RiskReductionCeiling` |
| `ItemEffectTuning` | magnitude positioning, sentence counts, table size + diversity cap, profile/form lean factors, Signature and drawback odds, assembler numbers (amplify/imbue durations, exchange price/boost, store gauge) |
| `PresentationTuning` | likelihood-word multiples (D53), workmanship word floors, leanings shown |
| `EquipmentTuning` | the authored-gear seam: damage-per-mass, windup-per-mass, armour-per-hardness |

**Every identity-system number is provisional until play** — all of them say so.

## 4. Persistent identifiers (do not rename as code)

Content ids (`material.*`, `identity.*`, `form.*`, `craft.*` verb actions, bare payload /
trigger / behavior keys), the `form:`/`state:` tag families, modifier keys, save keys
(`SaveData` / `*Save` member names, save v14), fingerprint inputs (`Fingerprint`), and the
`equip.emergent.` / `equip.emergent.i` prefixes. See `code-map.md` §12.

## 5. See also

| For | Read |
|---|---|
| The foundation: roster, grammar, capacity/condition, item expression, pipeline | `identity-foundation.md` |
| The verbs, rank economy, profession assignment | `transformation-verbs.md` |
| The player language (three languages, D30/D53) | `presentation-architecture.md` |
| The reward layer feeding the bench | `loot.md` |
| Decisions and their rejected alternatives | `DECISIONS.md` D42–D54 |
