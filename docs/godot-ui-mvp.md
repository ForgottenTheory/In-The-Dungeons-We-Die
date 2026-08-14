# Godot UI MVP

## 1. Purpose

The MVP UI is a developer-facing playable client for the game.

It replaces the original console-first development strategy.

The objective is to interact with the actual game systems through Godot while avoiding production art requirements.

Use Godot Control nodes heavily.

Ugly but understandable is acceptable.

Unusable is not.

---

# 2. UI Philosophy

The MVP UI should expose:

- State
- Actions
- Timing
- Inventory
- Progression
- Debug information

The developer should be able to understand why something happened.

---

# 3. Main Navigation

Primary screens:

- Hideout
- Professions
- Crafting
- Character
- Portal
- Realm
- Combat

These may initially exist inside one development dashboard with tabs.

---

# 4. Recommended Root

MainMvpUI : Control

Potential children:

TopBar
NavigationTabs
ContentArea
EventLog
DebugPanel

---

# 5. Top Bar

Always visible.

Displays:

- Character name
- Level
- Current activity
- Coins
- Current Realm status

Optional:

- Current tick
- Simulation speed

---

# 6. Hideout Screen

Provides access to:

- Stash
- Equipment
- Professions
- Crafting
- Portal

This represents the safe macro-management phase.

No free-roaming character is required.

---

# 7. Character Screen

Displays:

## Identity

Species
Prefix
Base Class
Suffix

Example:

Undead Pyromaniac Bastion
Of The Exploding Kneecaps

## Attributes

STR
DEX
INT
CON
WIS
END
LCK

## Resources

HP
Mana
Stamina

## Equipment

Basic equipment slots.

## Effects

Active passives and class effects.

---

# 8. Profession Screen

Left:

Profession list.

Center:

Available actions.

Right:

Selected action details.

Display:

- Profession level
- XP
- Mastery
- Base interval
- Effective interval
- Outputs

Controls:

[Start Passive]
[Start Active]

---

# 9. Active Profession Prototype

Active profession gameplay may initially use extremely simple controls.

Example Forestry:

Progress bar with timing zone.

Player presses:

[CHOP]

Correct timing increases:

- Yield
- Bark chance
- XP efficiency

This is a prototype of active skill expression, not final UI.

---

# 10. Crafting Screen

Panels:

Recipes
Materials
Infusion Slot
Craft Result
Discovery Information

Controls:

[Craft Passively]
[Craft Actively]
[Experiment]

---

# 11. Crafting Experimentation

Allow:

Base Recipe
+
Optional Material

Example:

Iron Ingot
+
Oak Bark

If valid and requirements are met:

New Discovery:

BARKBOUND IRON

Display:

- New property
- Required professions
- Discovery recorded

The interface should make experimentation understandable without revealing every hidden recipe.

---

# 12. Stash

Display persistent safe inventory.

Columns:

Item
Quantity
Type
Rarity

Support:

- Filtering
- Selection
- Equipment
- Realm loadout preparation

Fancy inventory UX is deferred.

---

# 13. Portal Screen

Displays available Realms.

MVP:

The Dark Forest

Display:

- Tier
- Realm Knowledge
- Known enemies
- Known resources
- Known hazards
- Expected difficulty

Loadout panel:

- Equipment
- Consumables
- Food

Control:

[OPEN PORTAL]

---

# 14. Realm Screen

Realm exploration should visually communicate spatial locations.

MVP can use a simple 2D map.

Each location is represented by:

Panel / Button / Node marker

Connections show traversable paths.

Examples:

Entrance
Forest Path
Old Grove
Goblin Camp
Ruins
Extraction Portal

---

# 15. Realm HUD

Display:

- Realm
- Tier
- Depth
- Current location
- Party health
- Supplies
- Unsecured loot
- Campsite availability

Important:

UNSECURED LOOT should be visually distinct from Stash inventory.

The player should understand what is at risk.

---

# 16. Location Interaction

Selecting current location displays actions.

Examples:

[Travel]
[Gather]
[Investigate]
[Enter Combat]
[Extract]

Only valid actions should be enabled.

---

# 17. Go Deeper Decision

At depth transition:

Display clear decision.

EXTRACT

Secure current loot and end Realm Run.

GO DEEPER

Increase danger and reward.

Do not hide this decision behind ambiguous UI.

It is one of the game's central moments.

---

# 18. Combat Screen

Recommended layout:

+------------------------------------------------+
| ENEMY INTENT / TIMELINE                        |
+----------------------+-------------------------+
| PLAYER               | ENEMIES                 |
| HP                    | Goblin Brute            |
| Mana                  | HP                      |
| Stamina               | Intent                  |
+----------------------+-------------------------+
| ACTION TIMELINE                                 |
+------------------------------------------------+
| [Attack] [Block] [Dodge] [Item] [Ability]      |
+------------------------------------------------+
| COMBAT LOG                                     |
+------------------------------------------------+

---

# 19. Telegraph UI

Telegraphs must be extremely readable during MVP.

Example:

GOBLIN BRUTE

OVERHEAD SMASH

██████████████░░░░

1.4 seconds until impact

Target: Player

Damage: Crushing

The production game may obscure exact values.

MVP should expose information for balancing.

---

# 20. Timeline

Display scheduled actions.

Example:

0.8s Player Attack
1.4s Goblin Heavy Swing
2.0s Poison Cloud Pulse

This makes tick behavior testable.

---

# 21. Combat Controls

Required:

[Attack]

[Block]

[Dodge]

[Wait]

[Use Item]

Class abilities may appear dynamically.

---

# 22. Event Log

Use RichTextLabel.

Events:

- Profession gains
- Crafting
- Discovery
- Realm travel
- Combat
- Damage
- Loot
- Extraction
- Death

Example:

[Forestry] +1 Oak Log
[Discovery] You discovered Barkbound Iron!
[Realm] Entered Dark Forest Depth 2.
[Combat] Goblin Brute begins Overhead Smash.
[Combat] You blocked 12 Crushing damage.
[Loot] Acquired Rusted Helm.
[Extraction] 14 items secured.

---

# 23. Debug Panel

Development only.

Useful controls:

[Advance Tick]

[Pause Simulation]

[1x]

[2x]

[10x]

[Spawn Enemy]

[Give Item]

[Kill Player]

[Reset Save]

[Show Seed]

Debug tools must invoke legitimate application/domain operations where possible.

Do not create a second fake game state.

---

# 24. GameNode Autoload

GameNode may act as Godot's composition root.

Responsibilities:

- Construct Infrastructure
- Load definitions
- Construct Domain services
- Construct Application services
- Manage save lifecycle
- Expose appropriate application-facing services to Godot

Avoid putting actual gameplay rules inside GameNode.

GameNode wires systems together.

It should not become GameGodObject.cs.

---

# 25. UI Update Strategy

Prefer event-driven updates for immediate state changes.

Use query/state snapshots for screen refresh.

Avoid:

`_Process()` repeatedly rebuilding every UI panel every frame.

Gameplay does not need to be bound to render frames.

---

# 26. UI Components

Reusable Controls may include:

CharacterSummaryPanel
ResourceBar
InventoryPanel
ItemTooltip
ProfessionPanel
ActionProgressBar
RealmMapControl
RealmLocationControl
EnemyPanel
TelegraphBar
CombatTimeline
EventLogPanel
CraftingPanel
LootPanel

These are presentation components.

---

# 27. Production Migration

The MVP UI should not dictate final visuals.

Eventually:

Realm map
→ richer 3D/2D exploration

Combat placeholders
→ 3D tactical combat presentation

Developer progress bars
→ animations and visual telegraphs

The Domain remains.

That is the point of the architecture.

---

# 28. MVP UI Success Criteria

A developer can launch the project and:

1. Create/select a character.
2. Train Forestry.
3. Gather materials.
4. Use Herblore-related material interaction.
5. Smith equipment.
6. Equip a loadout.
7. Open the Dark Forest portal.
8. Move through Realm locations.
9. Enter combat.
10. Read enemy telegraphs.
11. React to attacks.
12. Win.
13. Receive unsecured loot.
14. Choose to go deeper or extract.
15. Extract successfully.
16. See loot transferred to Stash.
17. Craft an improvement.
18. Start another run.

When those work through the Godot UI, the vertical slice has proven the basic architecture.