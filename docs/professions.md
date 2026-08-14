# Professions

## 1. Vision

Professions provide persistent Melvor-inspired progression.

Players can train professions:

- Passively
- Actively
- Inside the Hideout
- Inside Realms where appropriate

Profession systems ultimately support Realm preparation, survival, exploration, crafting, or extraction.

---

# 2. Universal Profession Concepts

Each profession may track:

- Level
- XP
- Action Interval
- Mastery
- Unlocks

Individual activities may also have Mastery.

Example:

Forestry Level: 45

Oak Mastery: 72

Petrified Tree Mastery: 18

---

# 3. Passive Training

Passive training emphasizes convenience.

Typical characteristics:

- Automatic
- Reliable
- Lower yield
- Lower quality ceiling
- Reduced rare outcome chance

---

# 4. Active Training

Active training adds player interaction.

Potential rewards:

- Increased yield
- Faster effective actions
- Better quality
- Rare materials
- Masterwork outcomes
- Discovery opportunities

Active play should reward actual performance rather than simply clicking "Active Mode."

---

# 5. Gathering Professions

## Mining

Produces:

- Ore
- Gems
- Runic stone
- Rare minerals

Supports:

- Smithing
- Jewelcrafting
- Enchanting

## Forestry

Produces:

- Logs
- Bark
- Resin
- Heartwood
- Petrified wood

Supports:

- Fletching
- Smithing handles
- Cooking
- Campcraft
- Infusions

## Fishing

Produces:

- Food
- Oils
- Scales
- Rare curiosities

Supports:

- Cooking
- Alchemy
- Crafting

## Herblore

Produces and identifies:

- Herbs
- Roots
- Bark
- Fungi
- Toxins

Supports:

- Alchemy
- Medicine
- Cooking
- Material infusion

## Farming

Produces controlled renewable resources at the Hideout.

Supports:

- Cooking
- Herblore
- Alchemy

---

# 6. Crafting Professions

## Smithing

Creates:

- Ingots
- Weapons
- Armor
- Metal components

## Alchemy

Creates:

- Potions
- Elixirs
- Oils
- Extracts

## Cooking

Creates:

- Meals
- Rations
- Buff food
- Realm supplies

## Enchanting

Creates:

- Runes
- Magical modifications
- Affix manipulation

## Fletching

Creates:

- Bows
- Crossbows
- Arrows
- Bolts

## Tailoring

Creates:

- Cloth armor
- Leather armor
- Bags
- Utility equipment

## Medicine

Creates:

- Bandages
- Antidotes
- Splints
- Advanced recovery supplies

---

# 7. Utility Professions

## Beast Lore

Provides:

- Creature information
- Tracking
- Harvesting
- Beast interactions

## Sleight of Hand

Provides:

- Lockpicking
- Trap bypass
- Event options
- Opportunistic interactions

## Agility

Improves:

- Movement efficiency
- Hazard response
- Realm traversal

## Campcraft

Improves:

- Campsite duration
- Campsite recipes
- Recovery
- Field preparation

## Wayfinding

Improves:

- Realm targeting
- Realm information
- Affix manipulation
- Navigation
- Hidden-route discovery

## Devotion

Provides:

- Faith mechanics
- Sacrifice systems
- Toggleable buffs

## Summoning

Uses:

- Essences
- Crafted components

to create temporary companions.

---

# 8. Profession Interactions

Professions should NOT exist in isolation.

Example:

Forestry
→ Oak Bark

Herblore
→ Understands Oak Bark properties

Smithing
→ Infuses Iron with treated Oak Bark

Result:
Barkbound Iron

This principle should appear throughout the game.

---

# 9. Mastery

Individual activities gain Mastery.

Mastery may provide:

- Interval reduction
- Increased yield
- Reduced costs
- Rare material chance
- Active interaction improvements

Mastery level target:

1-99

subject to balancing.

---

# 10. Offline Progress

Passive professions support offline progress.

Conceptually:

CompletedActions =
floor(ElapsedTime / EffectiveInterval)

Apply:

- Resource constraints
- Inventory constraints
- Action caps where necessary

Offline simulation should aggregate rather than replay every tick.

---

# 11. Realm Professions

Some profession activities occur during Realm Runs.

Examples:

Mining rare ore.

Harvesting Realm herbs.

Fishing dangerous waters.

These activities create risk because time passes and encounters/hazards may occur.

---

# 12. Profession Goal

Profession progression should produce the feeling:

"I spent time mastering this skill, and now I can prepare for this Realm in ways I couldn't before."

Not merely:

"My number is 73 now."