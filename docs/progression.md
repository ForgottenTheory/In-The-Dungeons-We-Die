# Progression

> **Status after Phase 8 (D40): every track below is built and read.** The implementation map is
> `docs/code-map.md` §10.16c; the decisions and their reasoning are `DECISIONS.md` D40. The one
> exception is form/schematic knowledge — schematics drop and bind to no form (D29.2, M6), and
> `ProgressionEcosystemTests` exempts it by name rather than weakening the rule.

## 1. Philosophy

Progression exists across multiple independent tracks.

This prevents one giant Character Level from representing everything the player has accomplished.

**The rule that keeps it true in the code:** *character XP comes from Realm activity only.*
Professions, crafting and discoveries award none. The moment fishing raises combat attributes,
every track has collapsed into one power number and this section is a comment rather than a rule.
Progression should unlock **new actions, better information, new routes, materials, crafting
options and build possibilities** — not only bigger numbers.

---

# 2. Character Progression

Persistent.

Includes:

- **Level and XP** — earned in **Realms only**: defeating something pays by its health and rank,
  extracting pays a flat award, and **dying pays nothing**. That is the extraction decision
  applied to progression itself.
- **Attributes** — each level distributes the Base's own growth weights over the same 4.0-point
  budget every Base gets. The shape is the Base's; the distance is the level's. Levelling raises
  the ceiling and never refills what is under it.
- Class unlocks, abilities and passive progression — still designed, not built.

---

# 3. Profession Progression

Persistent.

Each profession tracks:

- **Level and XP** — gate which actions exist at all.
- **Per-action Mastery** — 0–99, one point per landed attempt. Buys interval reduction and
  rare-find chance from the first level, **input preservation at 20** and **output doubling at
  40**, plus opportunity odds and risk. The magnitudes are content (`game/data/mastery/`), so a
  balance pass is a JSON edit rather than a rebuild.
- **Unlocks** — level gates on the ladder, and a mastery gate on a handful of the highest-risk
  opportunities. Below that gate they are *not rolled at all*, so deep experience in one action
  buys a different list of things that can happen rather than a better number.

Death does not remove profession progress.

---

# 4. Realm Knowledge

Persistent per Realm. Represents accumulated understanding — *Dark Forest Knowledge*, *City of
Infinite Alleys Knowledge*, *Ashlands Knowledge*.

**It buys information and options, never damage.** A percentage would make Knowledge a second
power curve and the realm would quietly get easier for reasons the player cannot see. Instead the
realm stays exactly as lethal and the player stops walking into it blind (GDD §11.4).

Seven rungs, read both inside a run and on the preparation screen before one: what the place is
made of → what lives here and how it dies → where the ground turns against you → which workings
pay → the ways nobody marked → the ways out → **and last, the right to start at a deeper door**.

That last rung is the only one that hands over an option rather than a fact, and it is priced:
starting deep skips the shallow fights, their loot *and* the knowledge they would have paid.

---

# 5. Crafting Discovery

Persistent.

Tracks:

- Recipes discovered
- Material interactions
- Infusions
- Special outcomes

The player's crafting knowledge becomes a long-term collection system.

---

# 6. Equipment Progression

Partially persistent and partially risk-based.

Extracted equipment becomes owned.

Equipment taken into future Realm Runs may be risked.

This creates a meaningful difference between:

Progression capability

and

Currently owned gear.

---

# 7. Account / World Unlocks

Potential persistent unlocks:

- Realms
- Realm tiers
- Species
- Base Classes
- Prefixes
- Suffixes
- Hideout upgrades
- Crafting stations

Use sparingly.

Character progression should remain meaningful.

---

# 8. Loss

Death should remove run-specific gains without deleting hundreds of hours of persistent progression.

Potentially lost:

- Unsecured loot
- Carried consumables
- Equipped gear depending on rules

Retained:

- Profession levels
- Realm Knowledge
- Discoveries
- Major unlocks
- Safe Stash

---

# 9. Starter Recovery

Players always have a recovery path.

Starter equipment should allow:

- Basic gathering
- Basic combat
- Low-tier Realm entry

But should be substantially weaker than carefully crafted/extracted gear.

---

# 10. Progression Loop

Profession Progress
→ Better Preparation
→ Better Realm Performance
→ Better Extraction
→ Better Materials
→ Better Crafting
→ Deeper Realms
→ New Profession Opportunities

The systems form a circle rather than independent ladders.

---

# 11. Horizontal Progression

Avoid relying entirely on bigger numbers.

Progression should also unlock:

- New tactical options
- New recipes
- New material combinations
- New routes
- New class mechanics
- New preparation strategies

This preserves meaningful decision-making at high progression.

---

# 12. Endgame Philosophy

Long-term progression should revolve around:

- Mastery
- Build experimentation
- Deep Realm runs
- Rare materials
- Class combinations
- Crafting discoveries
- Realm Knowledge
- Chase equipment

Exact endgame systems are intentionally deferred.