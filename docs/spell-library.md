# The Spell Library (2026-08-19)

The fate of every name in the ~870-name spell/ability request, decided against one rule:
**a player-facing move ships only when its mechanic resolves (D30 / CLAUDE.md rule 7).**
Names whose fiction can be *honestly* expressed with live mechanics shipped (an instant
"Ring of Fire" burst is honest; a persistent wall is not). Names that would lie are **parked**
in §2, keyed to the system that unlocks them — when that system lands, its names are ready.

- **Shipped:** 474 new moves in `game/data/moves/spells_*.json` (517 total with the 43
  pre-existing), one technique item each in `game/data/techniques/` (Grimoire = mana,
  Manual = stamina; 493 techniques total), 10 weapon-imbue move modifiers (11 movemods total).
  `tests/Combat/SpellLibraryTests.cs` proves each mechanic family resolves with this content.
- **Acquisition is deliberately debug-only** (user decision, 2026-08-19): no loot-table entries
  until the balance pass, so live drop odds are untouched. The debug grant button is the faucet.
- **Every number is a placeholder**, tuned relative to `move.fireball` (30 heat @ 18 mana) and
  `move.arcane_dart` (9 @ 6 mana) — same convention as `library_moves.json`. Balance pass owns
  the absolute values.
- A player who learns *everything* gets a very tall move list; a "memorized loadout" cap is
  future design and belongs to the same pass that gives techniques a real faucet.

## §1 Shipped, by family file

Mechanic vocabulary used (all live): typed/aspected damage packets · `max_targets` AoE ·
`applyStatus` (burn, chill, freeze, shock, poison, bleed, corroded, weaken, vulnerable, fear,
silence, stun, guarded, stoneskin, barrier, illuminated) · Resolve buildup for controls (D-08)
· `heal` (instant, and lifesteal riders on `TriggerSource`) · `grantResource` ·
`grantModifier` (attributes, per-lane resists, damage/crit, `combat.windup.mult` for
haste/slow-curses, armor, evade) · `interrupt` · `modifyMove` (weapon imbues) · `grantMove`
(Conjure Weapon) · `triggerMove` (echoes) · health-as-cost (pacts).

### spells_fire.json — heat, Burn (essence:fire) — 26
Flame Bolt, Fire Lance, Ember Shot, Flame Wave, Burning Hands, Firestorm, Ring of Fire,
Flame Burst, Scorch, Ignite, Searing Ray, Blazing Orb, Flame Whip, Inferno, Cinder Blast,
Ash Cloud (AoE Weaken), Ember Rain, Molten Spear, Magma Burst, Lava Wave, Volcanic Eruption,
Flame Shield (Barrier), Heat Ward (resist.heat), Combustion, Smolder, Cinderstorm.

### spells_frost.json — cold, Chill → Freeze (essence:frost) — 25
Frost Bolt, Ice Shard, Frostbite, Freezing Touch, Ice Spike, Frost Nova, Blizzard, Ice Storm,
Frozen Orb, Ice Barrier (Barrier), Frost Armor (armor + resist.cold), Cold Snap, Glacial Spear,
Glacial Wave, Hailstorm, Snowstorm, Frozen Chains (Chill + Freeze buildup), Ice Prison (ditto,
heavier), Winter's Grasp, Crystalline Frost, Flash Freeze, Frost Shield (resist.cold),
Ice Tomb (the big Freeze), Avalanche, Glacial Collapse.

### spells_storm.json — charge, Shock (essence:storm) — 26
Lightning Bolt, Lightning Strike, Chain Lightning (3 targets), Thunderbolt, Thunderclap,
Static Shock, Spark, Arc Lightning (3-target tier above Arc Surge), Storm Bolt, Storm Call,
Thunderstorm, Lightning Storm, Ball Lightning, Storm Shield (resist.charge), Lightning Shield
(Barrier), Thunder Wave, Shockwave, Electrocute, Storm Spear, Tempest, Storm Surge,
Forked Lightning, Skyfire, Rolling Thunder, Tempest Barrier (Barrier), Lightning Nova.

### spells_earth.json — physical Crushing/Piercing, stagger, Corroded — 21
Stone Spike, Stone Lance, Rockfall, Boulder Toss, Earthquake, Tremor, Rock Armor (armor),
Earth Shield (Barrier), Sand Blast (Vulnerable), Sandstorm (AoE Weaken), Dust Cloud,
Earth Spike, Ground Slam, Fault Line, Seismic Wave, Stone Fist, Crushing Earth,
Earthen Pillar, Granite Barrier (Barrier), Shatterstone (Corroded), Mountain's Wrath.

### spells_water.json — magic lane / Crushing waves — 19
Water Bolt, Water Jet, Water Lance, Tidal Wave, Wave Crash, Water Whip, Water Shield (Barrier),
Bubble Shield (Guarded), Geyser, Torrent, Whirlpool, Maelstrom, Riptide, Flood,
Tidal Barrier (Barrier), Soothing Waters (heal), Crashing Wave, Deep Current, Sea's Fury.

### spells_wind.json — physical Slashing/Crushing, evade, tempo — 21
Wind Blade, Wind Bolt, Gust (interrupt), Gale, Cyclone, Tornado, Air Shield (evade),
Vacuum Burst, Pressure Wave, Razor Wind (Bleed), Tailwind (own windup down),
Headwind (enemy windup up), Tempest Slash, Air Lance, Stormwind, Gale Force, Slicing Gust,
Cyclone Armor (evade, bigger), Breath of Wind, Vacuum Sphere, Hurricane.

### spells_radiant.json — magic lane + heat for solar (essence:radiant) — 40
Light: Light Bolt, Radiant Beam, Radiant Burst, Holy Light (heal), Sunbeam, Solar Flare,
Blinding Light (AoE Vulnerable), Radiant Nova, Light Shield (Barrier), Radiant Barrier,
Illuminate (status.illuminated), Flash, Sunfire, Dawn Ray, Light Spear, Pillar of Light,
Halo (Barrier), Sunward (resist.heat), Dawn Shield (Guarded), Judgment, Celestial Light
(big heal), Sunburst, Radiant Ward (resist.magic).
Celestial: Meteor Strike, Falling Star, Star Bolt, Starfall, Star Shower, Astral Bolt,
Astral Spear, Astral Shield (Barrier), Moon Beam, Moon Shield (Guarded), Moonfire,
Lunar Blessing, Lunar Curse, Solar Beam, Solar Shield, Solar Blessing, Celestial Spear.

### spells_shadow.json — magic lane (essence:abyssal), Fear/Silence, void — 31
Shadow: Shadow Bolt, Shadow Lance, Shadow Cloak (evade), Umbral Blade, Umbral Spear,
Dark Shield (Guarded), Shadow Barrier (Barrier), Nightmare, Dread, Terror, Silence,
Shadow Nova, Dark Pulse, Umbral Wave, Nightfall, Shadow Mark (Vulnerable), Dark Pact
(health → mana), Void Shadow.
Void: Void Bolt, Void Lance, Void Burst, Void Nova, Void Wave, Void Orb, Void Shield
(Barrier), Void Barrier, Void Mark, Void Drain (lifesteal), Void Pulse, Void Storm,
Abyssal Bolt.

### spells_arcane.json — kinetic aspect (renamed from `arcane`, D44; unresistable, low) + magic lane + chrono/force — 47
Arcane: Arcane Bolt, Arcane Missile (multi-packet), Arcane Lance, Arcane Burst, Arcane Nova,
Arcane Wave, Arcane Orb, Arcane Barrage, Arcane Shield (Barrier), Arcane Barrier,
Arcane Armor (armor + resist.magic), Mana Shield (Barrier), Mana Bolt, Mana Burst, Mana Surge
(grantResource mana), Mana Infusion (health → mana), Spell Break (interrupt), Counterspell
(interrupt), Arcane Mark (Vulnerable), Arcane Echo (triggerMove arcane_bolt), Arcane Storm.
Generic magic: Magic Missile, Magic Bolt, Magic Arrow, Magic Spear, Magic Blade, Magic Shield
(Barrier), Magic Barrier, Magic Burst, Magic Wave, Magic Nova.
Chrono: Accelerate (own windup down), Decelerate (enemy windup up), Temporal Shield (Barrier),
Chrono Bolt, Chrono Burst, Temporal Echo (triggerMove chrono_bolt).
Force/gravity: Gravity Bolt, Gravity Crush, Gravity Wave, Crushing Force, Gravity Slam,
Force Bolt, Force Wave, Force Barrier (Barrier), Heavy Gravity (enemy windup up), Singularity.

### spells_toxin.json — toxin/corrosion, Poison/Corroded (essence:nature) — 24
Poison Bolt, Poison Cloud, Poison Nova, Poison Spray, Toxic Cloud, Toxic Burst, Venom Spray,
Venomous Touch, Acid Bolt, Acid Spray, Acid Rain, Acid Wave, Corrosive Touch, Corrosive Blast,
Plague Cloud, Plague Bolt, Disease (Weaken + Poison), Infection, Blight, Blight Bolt, Rot,
Decay, Contagion, Toxic Ward (resist.toxin).

### spells_necrotic.json — decay, Bleed, lifesteal, pacts (essence:necrotic) — 57
Blood: Blood Bolt, Blood Lance, Blood Spear, Blood Blade, Blood Nova (health cost), Blood
Burst, Blood Wave, Blood Shield (Barrier, health cost), Blood Barrier, Blood Pact (health →
mana), Blood Mark, Blood Curse, Blood Drain, Life Drain, Vampiric Touch, Vampiric Strike,
Blood Sacrifice (health → big damage), Blood Infusion (health → stamina), Blood Armor,
Crimson Mist, Hemorrhage (big Bleed), Blood Boil, Blood Rain (AoE lifesteal), Blood Ritual
(health → mana + stamina), Sanguine Burst (lifesteal), Crimson Tide.
Bone: Bone Spear, Bone Spike, Bone Shards (multi-packet), Bone Armor (armor), Bone Shield
(Barrier), Bone Storm, Death Bolt, Death Touch, Death Nova, Death Coil (lifesteal),
Death Mark, Death Ward (resist.decay), Grave Bolt, Grave Mist (AoE Weaken).
Soul: Soul Bolt, Soul Spear, Soul Drain (lifesteal), Soul Burn (Weaken), Soul Shield (Guarded),
Soul Barrier (Barrier), Soul Mark, Soul Harvest (AoE lifesteal), Spirit Bolt, Spirit Lance,
Spirit Shield (Guarded), Spirit Heal (heal), Ancestor's Blessing (buff), Ancestor's Wrath,
Spectral Blade, Spectral Spear, plus Conjure Weapon (grantMove → Spectral Blade).

### spells_nature.json — thorn physicals + toxin (essence:nature) — 12
Thorn Whip, Thorn Volley (multi-packet), Poison Ivy, Vine Lash (Weaken), Nature's Wrath,
Nature's Blessing (heal + resist.toxin), Barkskin (armor), Ironbark (armor, bigger),
Spore Cloud, Nature Ward (resist.toxin), plus the two illusion-group survivors: Blur (evade)
and False Life (Barrier).

### spells_restoration.json — heals, resource restores — 16
Heal, Greater Heal, Minor Heal, Healing Touch, Healing Light, Healing Wave, Mend Wounds,
Restore, Vitality (max health), Life Surge, Lifeblood (max health), Second Wind (stamina),
Restoration (heal + stamina), Revitalize, Resurgence (heal + mana), Protective Light
(Guarded + heal).

### spells_blessing.json — buffs, blessings, shouts — 50
Attribute: Strength, Greater Strength, Agility, Greater Agility, Endurance, Greater Endurance,
Intellect, Greater Intellect, Fortitude, Blessing of Strength, Blessing of Agility, Blessing
of Endurance, Blessing of Wisdom, Blessing of Protection, Blessing of Fortune.
Combat: Battle Focus (crit chance), Battle Trance (crit mult), War Cry*, Heroism*, Courage*,
Fury* (damage up, taken up), Rage* (ditto, flat), Bloodlust (crit + damage), Quickness,
Swiftness (windup down), Fleet Foot (evade), Iron Skin (armor), Elemental Resistance
(heat+cold+charge), Haste (windup down), Divine Favor (crit).
Blessing/divine: Bless, Greater Blessing, Bless Armor, Divine Shield (big Barrier),
Divine Protection, Divine Wrath, Divine Strike, Divine Hammer, Divine Spear, Divine Nova,
Divine Barrier, Sacred Flame, Sacred Shield (Guarded), Holy Ward (resist.magic),
Holy Barrier, Holy Strike, Holy Nova, Prayer of Healing, Prayer of Protection,
Prayer of Strength.  (* = stamina Manual, the rest Grimoires.)

### spells_curse.json — debuffs, marks, hexes — 34
Curse, Weakness Curse, Frailty Curse, Slow Curse (enemy windup up), Silence Curse, Misfortune
(luck down), Doom, Mark of Doom, Mark of Weakness, Mark of Pain, Mark of Death, Mark of Flame
(resist.heat shred), Mark of Frost, Mark of Storm, Mark of Shadow (resist.decay shred), Hex,
Greater Hex, Witch's Mark, Withering Curse, Crippling Curse, Vulnerability, Armor Break
(Corroded), Magic Vulnerability (resist.magic shred), Elemental Weakness (tri-shred),
Soul Curse, Grave Curse, plus control-group survivors: Fear, Panic, Stun, Daze, Weaken,
Enfeeble, Cripple, Mental Shock — authored here with the curses.

### spells_ward.json — shields and wards — 15
Shield (Guarded), Greater Shield, Barrier, Greater Barrier, Ward (resist.magic), Greater Ward,
Protection (damage taken down), Physical Barrier (resist.physical), Damage Absorption (big
Barrier), Absorb Magic (Barrier + resist.magic), Absorb Elements (Barrier + tri-resist),
Guardian Ward (Guarded, long), Protective Dome (Barrier + armor), Aegis (capstone Barrier),
Fortify (armor).

### spells_imbue.json + move_modifiers.json — weapon imbue casts — 10
Weapon Flame (heat), Weapon Frost (cold), Weapon Shock (charge), Weapon Poison (toxin),
Weapon Shadow (decay), Weapon Light (magic), Weapon Arcane (arcane), Weapon Blessing
(+10% damage), Vampiric Weapon (heal rider), Elemental Weapon (heat+cold+charge slivers).
Each cast `modifyMove`-attaches its `movemod.imbue_*`; every movemod matches
`tags_all: ["action:attack"]` — never match-all (the Emberbrand lesson).

## §2 Parked — the name is ready, the system is not (~360)

**Summons / minions — needs a `spawnEntity` handler and minion combatants (~65):**
Raise Skeleton / Archer / Warrior / Mage, Raise Zombie, Raise Dead, Animate Corpse, Animate
Bones, Summon Undead / Wraith / Ghost / Ghoul / Grave Hound / Spirit / Ancestor / Wolf / Dire
Wolf / Bear / Boar / Raven / Eagle / Spider / Serpent / Treant / Vine Beast / Fire / Water /
Earth / Air / Ice / Storm Elemental / Shadow / Familiar / Imp / Golem / Stone Golem / Spirit
Wolf / Wisp / Swarm / Roots / Vines, Call Beast, Call Spirits, Call Elemental, Call of the
Wild, Spirit Call, Shadow Clone, Living Shadow, Phantom Army, Phantasmal Beast.

**Teleports / repositioning / knockback — needs positioning (~40):**
Blink, Teleport, Short Teleport, Portal, Town Portal, Gateway, Phase Step, Dimensional Step,
Shadow Step, Wind Step, Spirit Step, Arcane Step, Astral Step, Storm Step, Time Step,
Void Step, Swap Position, Pull, Push, Repel, Attract, Gravity Pull, Force Push, Force Pull,
Levitate, Levitation, Flight, Featherfall, Water Walk, Wall Climb, Phase, Ethereal Form,
Return, Escape, Blink Strike, Burrow, Forest Step, Current Step, Cloud Step, Updraft,
Skyward Launch, Spirit Walk, Ghost Walk, Ghostly Form, Astral Projection.

**Walls, zones, ground effects, traps, runes — needs a zone system (~70):**
Wall of Fire, Ring-of-fire-style persistents, Burning Ground, Wall of Ice, Frozen Ground,
Permafrost, Static Field, Stone Wall, Fortified Ground, Sanctified Ground, Consecration,
Sacred Ground, Acid Pool, Blight Field, Bramble Field, Thorn Wall, Fungal Growth, Mud Trap,
Quicksand, Black Fog, Darkness, Deep Darkness, Mist, Heavy Mist, Raincall, Gravity Field,
Repulsion Field, Attraction Field, Gravity Prison, Force Cage, Illusory Wall, Illusory
Terrain, Illusory Maze, all 20 Runes (Rune of Fire … Rune of Detonation), all 14 traps
(Fire Trap … Mana Trap), Bone Wall, Beacon.

**Binds / prisons / grabs — needs Root (deferred with positioning, D-09) (~30):**
Entangling Roots, Grasping Vines, Nature's Grasp, Root, Snare, Bind, Paralyze, Frozen-style
chains outside cold (Shadowbind, Shadow Chains, Shadow Grasp, Shadow Prison, Crimson Chains,
Crimson Prison, Grave Chains, Spirit Chains, Spectral Chains, Soul Bind, Soul Chain, Radiant
Chains, Thunder Cage, Stone Prison, Earthen Grasp, Water Prison, Drowning Sphere, Wind Prison,
Void Prison, Void Grasp, Void Chains, Abyssal Grasp, Arcane Prison, Briar Prison, Ice-lane
exceptions shipped via Freeze), Petrify, Temporal Prison, Illusory Prison, Bone Prison,
Bone Cage.

**Detection / divination — needs `revealInfo` handling (knowledge lives outside runs) (30):**
Detect Magic / Life / Undead / Poison / Traps / Treasure / Hidden, True Sight, Night Vision,
Far Sight, Clairvoyance, Scry, Reveal, Reveal Invisible, Identify, Analyze, Arcane Sight,
Spirit Sight, See Through Walls, Track Creature, Locate Object, Locate Person, Sense Danger /
Magic / Undead / Spirits, Read Aura, Read Thoughts, Echo Location, Divination.

**Illusion / stealth — needs a visibility model (~26):**
Invisibility, Greater Invisibility, Camouflage, Concealment, Mirror Image, Illusion, Illusory
Double, Disguise, False Appearance, Phantom Sound, Phantom Image, Decoy, Misdirection, Veil,
Shadow Veil, Night Veil, Silent Step, Muffle, Vanish, Hallucination, Nightmare Vision,
Phantasmal Weapon, Spectral Decoy, Shadow Cloak's stealth reading (shipped as evade).

**Status removal — needs a cleanse effect kind (9):**
Cleansing Touch, Purify, Cleanse, Remove Poison, Remove Disease, Remove Curse, Dispel,
Purifying Light, Cleansing Water.

**Heal over time — needs a heal-tick status (7):**
Regeneration, Renew, Rejuvenation, Life Bloom, Wild Growth, Rapid Growth, Regrowth, Bloom,
Healing Rain, Healing Aura.

**Allies — needs party combat (6):**
Shield Ally, Shared Barrier, Life Link, Spirit Link, Healing Circle, Sanctuary.

**Resource drain — `drainResource` has no handler (2):** Mana Drain, Mana Burn.

**Mind control — no such statuses; D-08 keeps control additions deliberate (~12):**
Confusion, Charm, Sleep, Deep Sleep, Mesmerize, Hypnotize, Forgetfulness, Mind Fog, Pacify,
Taunt, Enrage, Berserk, Disarm.

**Shapeshift — needs the replacement-moveset design (8):**
Treeform, Stoneform, Animal Form, Wolf Form, Bear Form, Raven Form, Serpent Form.

**Time magic beyond tempo (~10):** Time Stop, Time Warp, Temporal Rewind, Temporal Lock, Age,
Rejuvenate Time, Stasis, Stasis Field, Temporal Rift, Borrowed Time.

**Out-of-combat utility — no mechanics; several belong to crafting/professions (~37):**
Create Food, Create Water, Purify Water, Light, Floating Light, Mage Light, Extinguish,
Kindle Flame, Warmth, Cooling Touch, Repair, Unlock, Lock, Knock, Alarm, Magic Alarm, Message,
Whisper, Far Whisper, Comprehend Language, Speak with Animals / Spirits / Dead, Water
Breathing, Air Bubble, Breathe Underwater, Cleanse Item, Preserve Food, Enchant Weapon,
Enchant Armor, Disenchant, Transmute, Duplicate, Shrink Object, Enlarge Object, Floating Disk,
Telekinetic Hand. (Enchant/Disenchant/Transmute are the crafting system's identity — if they
ever ship it is as professions, not spells.)

**Odd ones out:** Phoenix Flame (on-death mechanics), Soul Trap / Soul Exchange / Soul Harvest
meta beyond lifesteal, Weapon Curse (imbues attach to the player's moves only), Blindness
Curse / Blind (no blind status), Slow (D-09 cut the neutral Slow status — Chill is the slow;
the *tempo curse* reading shipped as Slow Curse/Decelerate/Headwind/Heavy Gravity), Reflect /
Spell Reflection (no grantable retaliation key — the mechanic exists only as an affix trigger),
Thorn Armor (same), Radiant Resistance (no radiant lane by design — light damage rides the
magic lane), Moonlight (utility light).

## §3 Skipped — duplicates and collisions (each noted once)

**Already shipped moves keep their names (one name per concept):** Fireball, Venom Bolt,
Wither, Expose Weakness, Stoneskin (→ "Stone Skin"), Whirlwind (martial; the wind spell
reading is covered by Cyclone/Tornado), Mend (→ utility "Mend"), Recall (Mnemonic capstone;
the movement reading is parked), Frost Lance (→ "Ice Lance").

**Cross-group duplicates authored once:** Dread, Silence, Blind, Stone Skin, Featherfall,
Water Walk, Wind Step, Shadow Step, Gravity Well, Magic Shield, Spell Reflection, Blood Curse,
Frozen Weapon, Charged Weapon, Venomous Weapon, Death Ward, Spirit Ward, Levitate/Levitation.

**Same-concept consolidations:** Bad Luck (→ Misfortune), Time Haste/Time Slow (→ Haste /
Slow Curse), Fire/Frost/Lightning/Poison/Magic/Shadow Resistance (→ Heat Ward, Frost Shield,
Storm Shield, Toxic Ward, Ward, Death Ward), Elemental Barrier (→ Elemental Resistance /
Absorb Elements), Spell Ward (→ Ward), Fire/Frost/Storm/Poison/Shadow/Light Ward in the
shields group (→ the per-lane wards above), Sanguine Ward (→ Death Ward), Flaming / Frozen /
Charged / Venomous / Spectral / Arcane / Holy / Shadow Weapon and Bone / Blood / Radiant /
Sacred / Holy Weapon (→ the ten Weapon-X imbue casts), Ice Lance (→ Frost Lance).
