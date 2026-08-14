# Combat Specification

## 1. Vision

Combat is continuous, tick-driven, tactical, and readable.

It is NOT traditional turn-based combat.

It is also NOT intended to be a twitch-action game.

The goal is:

Real-time pressure
+
Readable enemy intent
+
Meaningful reactions
+
Build expression
+
Preparation

The player should frequently understand what is about to happen and decide what to do about it.

---

# 2. Combat Skill Expression

Skill expression comes from:

- Reading telegraphs
- Timing defensive actions
- Target prioritization
- Resource management
- Positioning
- Interrupt timing
- Ability selection
- Consumable decisions
- Understanding enemies
- Building appropriate equipment

Player knowledge matters.

Character progression matters.

Neither should completely replace the other.

---

# 3. Tick Simulation

Combat uses the shared TickEngine.

All authoritative timing is represented in simulation ticks.

Visual time is derived from ticks.

Example:

Tick Rate: implementation configurable.

Attack:
Start Tick: 100
Execute Tick: 140
Recovery End: 160

The UI converts these intervals into readable timers/progress bars.

---

# 4. Action Lifecycle

Combat actions may use:

QUEUE
→ TELEGRAPH
→ WINDUP
→ EXECUTION
→ RECOVERY
→ READY

Not every action requires every stage.

---

# 5. Queue

The actor commits or requests an action.

Validation includes:

- Actor alive
- Target valid
- Resource available
- Range valid
- Action available
- Actor state permits action

---

# 6. Telegraph

Telegraph communicates intent.

Examples:

Goblin Brute:
"OVERHEAD SMASH"

Wolf:
"LUNGING"

Cultist:
"CHANNELING VOID RITE"

Telegraph information may include:

- Target
- Attack type
- Time until impact
- Area
- Damage category

More dangerous enemies may obscure some information.

---

# 7. Windup

The attack is preparing.

Some actions can be:

- Interrupted
- Dodged
- Blocked
- Countered

during this period.

---

# 8. Execution

Authoritative result occurs.

Examples:

- Damage
- Healing
- Movement
- Status
- Resource cost
- Interrupt

---

# 9. Recovery

Actor cannot immediately repeat unrestricted actions.

Recovery creates pacing and counterattack windows.

---

# 10. Player Actions

Initial actions:

- Attack
- Defend
- Dodge / Move
- Wait
- Use Item

Future:

- Class abilities
- Interrupt
- Counter
- Taunt
- Guard ally
- Cast
- Channel
- Swap equipment where allowed

---

# 11. Action Intervals

Every action has a Base Action Interval.

Effective interval may be modified by:

- Attributes
- Equipment
- Class
- Prefix
- Suffix
- Status
- Profession-related effects where appropriate

Conceptually:

EffectiveInterval =
max(MinimumInterval, BaseInterval × modifiers)

Hard minimums prevent degenerate zero-time actions.

---

# 12. Health

Health does NOT naturally regenerate during normal combat.

This is intentional.

Damage contributes to Realm attrition.

Healing requires resources or mechanics.

---

# 13. Mana

Mana may regenerate.

Wisdom may influence:

- Maximum Mana
- Regeneration
- Efficiency

Exact formulas remain balance data.

---

# 14. Stamina

Stamina represents physical action economy.

Used by actions such as:

- Attacking
- Blocking
- Dodging
- Movement

Endurance affects stamina systems.

---

# 15. Damage Types

Initial categories:

- Slashing
- Crushing
- Piercing
- Magic

Magic may later contain subtypes.

Defensive mechanics include:

- Resistance
- Armor
- Block
- Evasion
- Parry
- Immunities

---

# 16. Damage Pipeline

Conceptually:

Base Damage
→ Attribute Scaling
→ Offensive Modifiers
→ Critical Resolution
→ Defensive Mitigation
→ Block / Special Rules
→ Final Damage

Keep exact formulas data-driven/configurable where appropriate.

---

# 17. Positioning

Production combat should support tactical positioning.

Potential grid:

2x4
or
3x3

per side.

Position affects:

- Melee range
- Ranged distance
- Area effects
- Movement
- Hazards
- Protection
- Targeting

MVP may simplify positioning while preserving Domain concepts.

---

# 18. Movement

Movement consumes time.

Moving is an action.

Movement can therefore compete with:

- Attacking
- Healing
- Blocking
- Casting

This is important for hazard gameplay.

---

# 19. Hazards

Hazards operate on ticks.

Examples:

- Poison cloud
- Fire
- Falling debris
- Trap tile
- Freezing pulse

Hazards may telegraph future resolution.

Players can react before impact.

---

# 20. Enemy AI

Enemy AI should primarily choose intent.

The TickEngine resolves timing.

AI decisions may consider:

- Target
- Health
- Position
- Cooldowns
- Threat
- Player state
- Realm modifiers

Avoid placing AI logic inside Godot animation scripts.

---

# 21. Interrupts

Some actions may be interruptible.

Interrupt may:

- Cancel action
- Delay action
- Increase recovery
- Partially refund resources
- Not refund resources

These properties belong to action definitions.

---

# 22. Blocking

Blocking should be an intentional defensive decision.

Potential behavior:

- Costs stamina
- Reduces damage
- Stronger against specific directions/types
- Requires timing

Holding block forever should not be optimal.

---

# 23. Dodging

Dodging trades action time/stamina for avoidance or repositioning.

Timing matters.

Future equipment/class systems may alter dodge behavior.

---

# 24. Critical Hits

Luck contributes to critical chance.

Critical systems should support class/suffix interactions.

Example:

Of The Exploding Kneecaps:
Certain critical hits create secondary effects.

The suffix is therefore a rules hook, not merely `+CriticalDamage`.

---

# 25. Status Effects

Statuses should be data-driven where possible.

Examples:

- Bleed
- Poison
- Burn
- Stun
- Slow
- Vulnerable
- Guarded

Statuses may operate using tick durations.

---

# 26. Death

When Health reaches zero:

CharacterDefeated event occurs.

Enemy death:
- Resolve loot.
- Resolve XP.
- Update knowledge.

Player death:
- End or fail run according to Realm rules.

---

# 27. Combat Events

Useful events:

- CombatStarted
- CombatEnded
- ActionQueued
- ActionTelegraphed
- ActionStarted
- ActionInterrupted
- ActionResolved
- RecoveryStarted
- DamageDealt
- DamageBlocked
- AttackDodged
- StatusApplied
- CharacterDefeated
- ResourceChanged

---

# 28. Auto Combat

Passive Realm Runs eventually require automatic combat behavior.

Auto combat should use the SAME combat rules.

It does not get a separate fake combat calculator.

Automation chooses actions.

The Domain resolves them normally.

This prevents passive and active combat from becoming two unrelated balance models.

---

# 29. Active Combat Advantage

Active players gain advantage through better decisions.

Examples:

- Blocking heavy attacks
- Dodging hazards
- Interrupting dangerous casts
- Conserving healing
- Focusing targets
- Exploiting weaknesses

Do not simply apply a hidden "+50% active damage" bonus.

Reward actual play.

---

# 30. Combat MVP

Implement:

- TickEngine integration
- Player
- Goblin Raider
- Goblin Brute
- Basic attack
- Heavy telegraphed attack
- Block
- Dodge
- Consumable
- Health
- Stamina
- Death
- Loot

The Goblin Brute is particularly important because it proves the core idea:

"I see something dangerous coming. What do I do before it lands?"