# MVP Vertical Slice

## 1. Purpose

The vertical slice exists to answer one question:

IS THE CORE GAME LOOP FUN?

It is not intended to demonstrate the final quantity of content.

The vertical slice should prove the relationship between:

- Professions
- Preparation
- Crafting
- Realm exploration
- Combat
- Loot
- Risk
- Extraction
- Persistent improvement

---

# 2. MVP Loop

The complete playable loop is:

Create Character
→ Enter Hideout
→ Train / Gather
→ Craft Supplies
→ Choose Equipment
→ Open Portal
→ Enter Dark Forest
→ Explore
→ Encounter Enemies / Resources / Events
→ Fight
→ Gather Loot
→ Decide Continue or Extract
→ Go Deeper OR Extract
→ Return To Hideout
→ Bank Loot
→ Craft Better Equipment
→ Improve Professions
→ Repeat

The MVP is not complete until this entire loop works.

---

# 3. MVP Presentation

Use functional Godot Control-based 2D UI.

Production graphics are not required.

The UI should prioritize:

- State visibility
- Debugging
- Fast iteration
- Testing
- Understanding system interactions

Buttons, panels, progress bars, lists, labels, and simple icons are sufficient.

---

# 4. MVP Character

One playable character.

Character has:

## Attributes

- Strength
- Dexterity
- Intelligence
- Constitution
- Wisdom
- Endurance
- Luck

## Resources

- Health
- Mana
- Stamina

Health does not naturally regenerate during combat.

---

# 5. MVP Character Identity

Implement enough of the class composition system to prove the architecture.

Required:

- At least 2 Species
- At least 2 Base Classes
- At least 3 Prefixes
- At least 5 Suffixes

This is not intended to represent the final roster.

The MVP must prove that composition can alter actual gameplay.

At least one suffix should implement a rule-changing mechanic rather than a simple stat bonus.

---

# 6. MVP Professions

Implement:

## Forestry

Gather wood and bark.

Demonstrates:

- Passive gathering
- Active gathering
- Profession XP
- Mastery
- Material generation

## Herblore

Gather and understand botanical materials.

Demonstrates:

- Cross-profession crafting
- Material properties
- Discovery

## Smithing

Create basic metal materials and equipment.

Demonstrates:

- Passive crafting
- Active crafting
- Quality
- Infusion

## Cooking

Optional if schedule permits, but strongly preferred.

Demonstrates consumable preparation for Realm Runs.

---

# 7. Active vs Passive MVP

At least Forestry and Smithing must support both.

Example Forestry:

PASSIVE:
Select Oak Tree.
Character gathers automatically.

ACTIVE:
Player performs a simple interaction that improves yield, speed, or material quality.

Example Smithing:

PASSIVE:
Produce normal ingots/equipment.

ACTIVE:
Timing/heat interaction can improve quality.

The final production minigames are not required.

Simple prototype interactions are sufficient.

---

# 8. MVP Crafting Discovery

Implement at least one cross-profession discovery.

Example:

Iron Ingot
+ Oak Bark
+ Herblore requirement
=
Barkbound Iron

Barkbound Iron receives a meaningful property.

Equipment crafted from it inherits or derives an effect.

The discovery should become permanently recorded.

---

# 9. MVP Realm

Realm:

Dark Forest

The Dark Forest must demonstrate the Realm architecture.

It contains:

- Tier
- Depth
- Locations
- Enemies
- Gathering location
- Event
- Extraction point

---

# 10. Realm Exploration

Production exploration will eventually resemble For The King 2.

For MVP, represent locations spatially in 2D.

The player should move between locations rather than merely clicking "next encounter."

Example:

Camp Entrance
    |
Forest Path
 /         \
Grove      Ruins
 |           |
Goblin Camp  Shrine
 \           /
 Extraction

The exact layout may be generated or fixed initially.

The important feature is spatial decision-making.

---

# 11. Realm Depth

MVP should contain at least:

Depth 1
Depth 2

After reaching a meaningful checkpoint, the player chooses:

EXTRACT

or

GO DEEPER

Depth 2 increases:

- Enemy danger
- Reward quality
- Material opportunities

---

# 12. Realm Knowledge

Track Dark Forest Knowledge.

MVP knowledge can increase from:

- Entering the Realm
- Discovering locations
- Defeating enemies
- Gathering unique materials
- Successful extraction

Knowledge may reveal:

- Enemy details
- Resource information
- Hidden route
- Better extraction information

Keep implementation small but prove persistence.

---

# 13. MVP Combat

Combat must demonstrate:

- Continuous tick simulation
- Enemy telegraphs
- Action timing
- Player reaction
- Health attrition
- Combat rewards

Player actions:

- Attack
- Defend
- Dodge / Move
- Wait
- Use Item

At minimum.

---

# 14. Combat Timing

Enemy attacks should demonstrate:

Telegraph
→ Windup
→ Execution
→ Recovery

Example:

Goblin begins Heavy Swing.

UI displays:

HEAVY SWING
Impact in 2.5 seconds

Player can:

- Continue attacking
- Block
- Dodge
- Use ability
- Use item if timing permits

This is the core combat skill-expression test.

---

# 15. MVP Enemies

At least:

## Goblin Raider

Basic fast attacker.

## Goblin Brute

Slow telegraphed heavy attacks.

## Forest Creature

Demonstrates a different damage/status pattern.

One elite variant is desirable.

---

# 16. MVP Loot

Enemies and locations may produce:

- Coins
- Basic metal
- Wood
- Bark
- Herbs
- Consumables
- Equipment

Realm loot remains unsecured until extraction.

---

# 17. Extraction

Player can extract from designated opportunities.

On extraction:

Unsecured Realm loot
→ Persistent Stash

Realm Knowledge updates.

Run ends.

Player returns to Hideout.

---

# 18. Death

Death must matter in the MVP.

On death:

- Unsecured Realm loot is lost.
- Run ends.
- Character returns to Hideout.
- Previously secured Stash remains safe.

Exact equipped-item loss can remain configurable during balancing.

The architecture must support gear loss even if the MVP initially uses a simplified rule.

---

# 19. Starter Loadout

The player must never become permanently unable to play.

If equipment is lost, the game provides access to a weak starter loadout.

The starter loadout is intentionally inferior to extracted/crafted equipment.

Death hurts without destroying the save.

---

# 20. Hideout

MVP Hideout is UI-driven.

Required panels:

- Character
- Stash
- Equipment
- Professions
- Crafting
- Portal

No free movement is required.

---

# 21. Portal

Portal screen allows:

- Select Dark Forest
- View Realm Knowledge
- View Tier
- View known threats
- Configure loadout
- Launch Realm

Future affix rerolling is not required for MVP.

---

# 22. Vertical Slice Milestones

## Milestone 1: Foundation

- Project structure
- DataStore
- Save skeleton
- TickEngine
- Event infrastructure

## Milestone 2: Character

- Attributes
- Resources
- Species
- Base class
- Prefix
- Suffix

## Milestone 3: Professions

- Forestry
- Herblore
- Smithing
- Passive actions
- Active prototype

## Milestone 4: Crafting

- Inventory
- Recipes
- Material properties
- Barkbound Iron discovery

## Milestone 5: Combat

- Encounter
- Enemy AI
- Telegraph
- Attack
- Defend
- Dodge
- Damage
- Death

## Milestone 6: Realm

- Dark Forest
- Spatial locations
- Travel
- Encounters
- Depth

## Milestone 7: Extraction

- Run inventory
- Extraction
- Loss
- Stash transfer

## Milestone 8: Progression

- Profession persistence
- Realm Knowledge
- Character progression
- Crafting discoveries

## Milestone 9: Complete Loop

Gather
→ Craft
→ Prepare
→ Realm
→ Combat
→ Loot
→ Extract
→ Upgrade

---

# 23. MVP Success Criteria

The vertical slice succeeds when:

1. Starting weak feels meaningfully different from being prepared.
2. Profession progression improves Realm preparation.
3. Crafting produces useful Realm equipment.
4. Active gameplay offers meaningful advantages.
5. Enemy telegraphs create decisions.
6. Going deeper creates tension.
7. Extraction feels valuable.
8. Death creates meaningful loss.
9. Successful extraction enables noticeable improvement.
10. The player wants to start another run.

Number 10 matters more than having fifty systems.