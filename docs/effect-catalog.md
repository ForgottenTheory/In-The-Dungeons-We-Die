# Starter Effect Catalog

> **Architecture DECIDED** (`effect-foundation.md` §12); these entries remain **for design review** — a possibility space, not a shipping list. Part of the effect-foundation package.
> **~240 modifier concepts.** No balance values: ranges are deliberately absent, because tier
> ranges are a tuning exercise that should happen against a running Item Lab, not a document.
>
> The point of this document is to **show the possibility space** and prove the architecture in
> `effect-foundation.md` can express it without special cases.

## How to read it

| Column | Meaning |
|---|---|
| **Modifier** | Player-facing wording (`$` = the rolled value) |
| **Family** | Anti-stacking group — one affix per family per item (`affixes.md` §3.5) |
| **Trigger / Condition** | Blank = a passive `StatGrant`. Otherwise the event and any condition. |
| **Forms** | `W` weapon · `A` armour · `Sh` shield · `F` focus · `Tl` tool · `*` any |
| **Genetics** | Material properties that gate/weight/tier it (`affixes.md` §2) |
| **C** | **S** Standard · **T** Trigger · **X** Exotic · **Σ** Signature · **A** Anomalous |

**Grant shape**: everything marked **S** is a `StatGrant` (a modifier key + value + scope).
Everything marked **T/X/Σ/A** is a `RuleGrant` (event + conditions + effects) unless noted.
That is the entire implementation surface — there is no third kind.

---

# 1. Offence — flat and increased damage (14)

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 1 | `+$` flat Physical damage | dmg_flat_phys | — | W | hardness, mass | S |
| 2 | Adds `$` Heat damage to attacks | dmg_flat_heat | — | W | heat | S |
| 3 | Adds `$` Cold damage to attacks | dmg_flat_cold | — | W | cold | S |
| 4 | Adds `$` Charge damage to attacks | dmg_flat_charge | — | W | charge, conductivity | S |
| 5 | Adds `$` Toxin damage to attacks | dmg_flat_toxin | — | W | toxicity | S |
| 6 | Adds `$` Corrosion damage to attacks | dmg_flat_corr | — | W | corrosion | S |
| 7 | Adds `$` Arcane damage (unresistable) | dmg_flat_arcane | — | W F | arcane, resonance | X |
| 8 | `$%` increased damage | dmg_inc_all | — | W F | potency, hardness | S |
| 9 | `$%` increased Slashing damage | dmg_inc_slash | — | W | hardness, flexibility | S |
| 10 | `$%` increased Crushing damage | dmg_inc_crush | — | W | mass | S |
| 11 | `$%` increased Piercing damage | dmg_inc_pierce | — | W | hardness | S |
| 12 | `$%` increased Spell damage | dmg_inc_spell | — | F A | resonance, arcane | S |
| 13 | `$%` increased Heat damage | dmg_inc_heat | — | * | heat, essence.fire | S |
| 14 | `$%` more damage while at full Stamina | dmg_more_stam | cond: `resourceAbove stamina 0.99` | W | mass, flexibility | X |

# 2. Offence — timing, critical, stagger (12)

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 15 | `$×` action interval | interval | — | W Tl | flexibility, −mass | S |
| 16 | `$×` windup on Melee moves | windup | scope `delivery:melee` | W | flexibility | S |
| 17 | `$×` recovery after Attacks | recovery | scope `action:attack` | W | flexibility | S |
| 18 | `$×` telegraph on your moves *(harder to read)* | telegraph | — | W | instability | X |
| 19 | `+$%` critical chance | crit_chance | — | W F | hardness, instability | S |
| 20 | `+$%` critical chance with Spells | crit_chance_spell | scope `action:spell` | F | resonance | S |
| 21 | `$×` critical damage | crit_mult | — | W | hardness, mass | S |
| 22 | `+$` stagger power | stagger | — | W | mass, hardness | S |
| 23 | `$%` increased stagger with Crushing | stagger_crush | scope type Crushing | W | mass | S |
| 24 | `$×` cooldown recovery | cooldown | — | * | resonance | S |
| 25 | Critical hits reduce your move cooldowns by `$` ticks | cd_on_crit | `DamageDealt` + `mech:critical` | W | instability, charge | T |
| 26 | Your first attack in an encounter deals `$%` more damage | opener | cond `firstInEncounter` | W | instability | X |

# 3. Offence — penetration and resistance manipulation (12)

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 27 | `+$` Physical penetration | pen_phys | — | W | hardness | S |
| 28 | `+$` Heat penetration | pen_heat | — | W F | heat | S |
| 29 | `+$` Cold penetration | pen_cold | — | W F | cold | S |
| 30 | `+$` Charge penetration | pen_charge | — | W F | charge, conductivity | S |
| 31 | `+$` Toxin penetration | pen_toxin | — | W | toxicity | S |
| 32 | `+$` armour penetration | pen_armour | — | W | hardness | S |
| 33 | Ignore `$%` of the target's Heat resistance | ignore_heat | — | W F | heat, arcane | X |
| 34 | Hits apply Heat Exposure (`−$` heat res, `n` ticks) | exposure_heat | `HitLanded` | W F | heat, corrosion | T |
| 35 | Hits apply Physical Exposure | exposure_phys | `HitLanded` | W | corrosion | T |
| 36 | `$%` chance to **invert** the target's Cold resistance | invert_cold | `HitLanded`, chance | W F | cold, resonance | X |
| 37 | Critical hits **treat positive Charge resistance as zero** | nullify_charge | `DamageDealt` + `mech:critical` | W F | charge, conductivity, essence.storm | Σ |
| 38 | `$%` more damage per 10 of the target's resistance in the hit's lane *(capped)* | res_scaling | — | W F | arcane, instability | X |

# 4. Character — attributes and resources (10)

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 39 | `+$` Strength | attr_str | — | * | mass, hardness | S |
| 40 | `+$` Dexterity | attr_dex | — | * | flexibility | S |
| 41 | `+$` Intelligence | attr_int | — | * | arcane, resonance | S |
| 42 | `+$` Constitution | attr_con | — | * | mass, hardness | S |
| 43 | `+$` Wisdom | attr_wis | — | * | resonance | S |
| 44 | `+$` Endurance | attr_end | — | * | flexibility, mass | S |
| 45 | `+$` Luck | attr_luk | — | * | instability, arcane | S |
| 46 | `+$` maximum Health | max_health | — | A Sh | mass, growth | S |
| 47 | `+$` maximum Mana | max_mana | — | F A | resonance, arcane | S |
| 48 | `+$` maximum Stamina | max_stamina | — | A | flexibility, growth | S |

# 5. Defence — armour, resistance, mitigation (16)

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 49 | `+$` Armour | armour | — | A Sh | hardness, mass | S |
| 50 | `$%` increased Armour | armour_inc | — | A Sh | hardness | S |
| 51 | `+$%` Physical resistance | res_phys | — | A Sh | hardness, mass | S |
| 52 | `+$%` Magic resistance | res_magic | — | A F | resonance, insulation | S |
| 53 | `+$%` Heat resistance | res_heat | — | * | insulation, heat_resistance | S |
| 54 | `+$%` Cold resistance | res_cold | — | * | insulation, cold_resistance | S |
| 55 | `+$%` Charge resistance | res_charge | — | * | insulation | S |
| 56 | `+$%` Toxin resistance | res_toxin | — | * | toxin_resistance | S |
| 57 | `+$%` Corrosion resistance | res_corr | — | * | hardness, insulation | S |
| 58 | `+$%` to **all** resistances | res_all | — | A | insulation | S |
| 59 | `+$%` maximum Heat resistance | maxres_heat | — | A Sh | insulation, essence.frost | X |
| 60 | `+$%` maximum resistance to all lanes | maxres_all | — | A | insulation, resonance | Σ |
| 61 | `$×` damage taken | dmg_taken | — | A | mass, insulation | X |
| 62 | `$×` critical damage taken | crit_taken | — | A Sh | hardness, flexibility | S |
| 63 | `$×` damage taken from Ailments | ail_taken | — | A | insulation, growth | S |
| 64 | Take `$%` less damage from telegraphed attacks | telegraphed_taken | cond: hit was telegraphed | Sh A | mass | X |

# 6. Defence — block, parry, dodge, Resolve (12)

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 65 | `$×` block damage multiplier *(stronger blocks)* | block_str | — | Sh A | hardness, mass | S |
| 66 | `+$` ticks to your block window | block_window | — | Sh | flexibility | S |
| 67 | `+$` ticks to your **Perfect Block** window | perfect_window | — | Sh | flexibility, resonance | X |
| 68 | `$×` block stamina cost | block_cost | — | Sh | −mass | S |
| 69 | Grants the **Parry** capability | parry_grant | — | W Sh | hardness, flexibility | X |
| 70 | `+$` ticks to your parry window | parry_window | — | W Sh | flexibility | X |
| 71 | `+$` ticks to your dodge window | dodge_window | — | A | flexibility | S |
| 72 | `$×` dodge stamina cost | dodge_cost | — | A | −mass | S |
| 73 | `+$%` chance to evade untelegraphed hits | evade | — | A | flexibility | S |
| 74 | `+$` Resolve | resolve | — | A Sh | mass, hardness | S |
| 75 | `$%` increased Resolve recovery | resolve_recov | — | A | growth | S |
| 76 | Gain `$` Resolve when you resist a control effect | resolve_on_resist | `ControlResisted` | A Sh | mass, resonance | T |

# 7. Avoidance (10) — *rare and hard-capped by design*

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 77 | `$%` chance to negate Heat hits | avoid_heat | `HitLanded` lane heat | A Sh | insulation, heat_resistance | X |
| 78 | `$%` chance to negate Cold hits | avoid_cold | lane cold | A Sh | insulation, cold_resistance | X |
| 79 | `$%` chance to negate Charge hits | avoid_charge | lane charge | A Sh | insulation, conductivity | X |
| 80 | `$%` chance to negate Toxin hits | avoid_toxin | lane toxin | A | toxin_resistance | X |
| 81 | `$%` chance to negate Magic hits | avoid_magic | lane magic | F A | resonance | X |
| 82 | `$%` chance to negate Physical hits | avoid_phys | lane physical | A Sh | hardness, mass | X |
| 83 | `$%` chance to avoid Poison application | avoid_ail_poison | on status application | A | toxin_resistance, growth | S |
| 84 | `$%` chance to avoid Burn application | avoid_ail_burn | on status application | A | insulation | S |
| 85 | `$%` chance to avoid **all** ailment application | avoid_ail_all | on status application | A | growth, insulation | Σ |
| 86 | `$%` chance to negate hits from **notable** enemies | avoid_elite | cond `targetHasTag tier:notable` *(derived — D-11)* | A Sh | resonance, arcane | Σ |

# 8. Retaliation / Thorns (16)

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 87 | `+$` Physical Thorns damage | thorns_flat_phys | `HitLanded` on self | A Sh | hardness | S |
| 88 | `+$` Heat Thorns damage | thorns_flat_heat | `HitLanded` | A Sh | heat | S |
| 89 | `+$` Cold Thorns damage | thorns_flat_cold | `HitLanded` | A Sh | cold | S |
| 90 | `+$` Charge Thorns damage | thorns_flat_charge | `HitLanded` | A Sh | charge, conductivity | S |
| 91 | `+$` Toxin Thorns damage | thorns_flat_toxin | `HitLanded` | A | toxicity | S |
| 92 | `+$` Corrosion Thorns damage | thorns_flat_corr | `HitLanded` | A | corrosion | S |
| 93 | `$%` increased Thorns damage | thorns_inc | — | A Sh | hardness | S |
| 94 | `$%` chance to retaliate when hit | thorns_chance | `HitLanded` chance | A Sh | hardness, instability | S |
| 95 | Retaliate for `$` when you **Block** | thorns_block | **`Blocked`** — fires on normal *and* perfect blocks (D-06) | Sh | hardness, mass | S |
| 96 | Retaliate for `$` when you **Parry** | thorns_parry | `Parried` | W Sh | hardness | X |
| 97 | Retaliate for `$` after you **Dodge** | thorns_dodge | `HitAvoided` via dodge | A | flexibility | X |
| 98 | Return `$%` of damage **mitigated** | thorns_reflect | `DamageMitigated` | A Sh | mass, hardness | X |
| 99 | Thorns can apply **Bleed** | thorns_bleed | rider on thorns | A Sh | hardness, corrosion | X |
| 100 | Thorns can apply **Shock** | thorns_shock | rider on thorns | A Sh | charge, conductivity | X |
| 101 | Thorns can **critically strike** | thorns_crit | — | A Sh | instability, hardness | X |
| 102 | **Stored Retaliation** — blocking banks mitigated damage; your next attack releases it | thorns_stored | `HitLanded` + `mech:block`, release on `ActionCompleted` | Sh | mass, hardness, resonance | Σ |

# 9. Ailments — application, magnitude, duration (16)

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 103 | `$%` chance to **Bleed** on hit | ail_bleed_chance | `HitLanded` | W | hardness | S |
| 104 | `$%` chance to **Poison** on hit | ail_poison_chance | `HitLanded` | W | toxicity | S |
| 105 | `$%` chance to **Burn** on hit | ail_burn_chance | `HitLanded` | W F | heat | S |
| 106 | `$%` chance to **Chill** on hit | ail_chill_chance | `HitLanded` | W F | cold | S |
| 107 | `$%` chance to **Shock** on hit | ail_shock_chance | `HitLanded` | W F | charge | S |
| 108 | `$%` chance to **Corrode** on hit | ail_corr_chance | `HitLanded` | W | corrosion | S |
| 109 | `$%` increased Bleed damage | ail_bleed_mag | — | W | hardness | S |
| 110 | `$%` increased Poison damage | ail_poison_mag | — | W | toxicity | S |
| 111 | `$%` increased Burn damage | ail_burn_mag | — | W F | heat, essence.fire | S |
| 112 | `$%` increased ailment duration | ail_duration | — | * | insulation, decay | S |
| 113 | `$×` duration of ailments **on you** | ail_duration_self | — | A | growth, insulation | S |
| 114 | Ailments you apply spread to a nearby enemy | ail_spread | `StatusApplied` | W F | growth, corrosion | X |
| 115 | **Consume** Poison stacks on a heavy hit for burst damage | ail_consume_poison | `HitLanded` + `hasTag heavy` | W | toxicity, instability | X |
| 116 | Poison you apply **does not expire** while the target is below 25% Health | ail_poison_exec | cond target health | W | toxicity, decay | Σ |
| 117 | `+$` maximum Poison stacks on a target | ail_poison_stacks | — | W | toxicity | X |
| 118 | Your Burns apply **Wither** as well | ail_burn_wither | `StatusApplied` burn | W F | heat, decay, essence.necrotic | Σ |

# 10. Crowd control (10)

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 119 | `+$%` control buildup applied | cc_buildup | — | W | mass | S |
| 120 | `+$%` Stun buildup | cc_stun | — | W | mass, hardness | S |
| 121 | `+$%` Freeze buildup | cc_freeze | — | W F | cold, essence.frost | S |
| 122 | `$%` chance to **Fear** on critical hit | cc_fear | `DamageDealt` + `mech:critical` | W | decay, essence.necrotic | X |
| 123 | `$%` chance to **Silence** on Spell hit | cc_silence | `HitLanded` + `action:spell` | F | resonance, arcane | X |
| 124 | `$%` increased control duration | cc_duration | — | W F | mass, cold | S |
| 125 | Reduce the target's Resolve by `$%` while it is Chilled | cc_resolve_chill | cond `targetHasStatus chill` | W F | cold | X |
| 126 | Controls you apply also apply **Vulnerable** | cc_vuln | `StatusApplied` control | W | corrosion | X |
| 127 | Freezing a target **Shatters** it for `$%` more physical damage | cc_shatter | cond `targetHasStatus freeze` | W | cold, hardness | X |
| 128 | Your controls **bypass Control Immunity once per encounter** | cc_bypass | `StatusApplied` control, once/encounter | F | resonance, arcane, essence.storm | Σ |

# 11. Resource engine (12)

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 129 | Gain `$` Stamina on hit | res_stam_hit | `DamageDealt` | W | growth | S |
| 130 | Gain `$` Stamina on critical hit | res_stam_crit | `DamageDealt` + `mech:critical` | W | growth, instability | S |
| 131 | Gain `$` Mana on Block | res_mana_block | `Blocked` | Sh F | resonance | T |
| 132 | Gain `$` Stamina on Block | res_stam_block | `Blocked` | Sh | mass, growth | T |
| 133 | Gain `$` Mana on kill | res_mana_kill | `Killed` | F | resonance, decay | T |
| 134 | Gain `$` Stamina when you avoid a hit | res_stam_avoid | `HitAvoided` | A | flexibility | T |
| 135 | Gain `$` of your Gauge when you apply a status | res_gauge_status | `StatusApplied` | * | arcane | T |
| 136 | `$×` Stamina costs | cost_stam | — | A W | flexibility | S |
| 137 | `$×` Mana costs | cost_mana | — | F | resonance | S |
| 138 | `$×` Mana costs of Spells with the Heat aspect | cost_mana_heat | scope aspect heat | F | heat, resonance | S |
| 139 | `$%` increased Gauge generation | gauge_gain | — | * | charge, arcane | S |
| 140 | Spending `$`+ Stamina **Empowers** your next attack | empower_spend | `ResourceSpent` ≥ n | W | charge, instability | X |

# 12. Recovery — Barrier-first (10)

> **Design rule (D-15):** no affix grants passive Health regeneration. Recovery is Barrier, or
> conditional and capped healing.

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 141 | Gain `$` Barrier on kill | bar_kill | `Killed` | A | growth | S |
| 142 | Gain `$` Barrier on Block | bar_block | `Blocked` | Sh | growth, mass | S |
| 143 | Gain `$` Barrier on critical hit | bar_crit | `DamageDealt` + `mech:critical` | W | growth, instability | T |
| 144 | `+$` maximum Barrier | bar_max | — | A | growth, resonance | S |
| 145 | `$%` increased Barrier effectiveness | bar_eff | — | A | resonance | S |
| 146 | `$×` Barrier decay rate | bar_decay | — | A | insulation | S |
| 147 | Recover `$` Health when you **Parry** *(capped per second)* | heal_parry | `Parried` | W Sh | growth | X |
| 148 | Recover `$` Health on kill *(capped per second)* | heal_kill | `Killed` | W | growth, essence.nature | X |
| 149 | **Cleanse** one ailment when you Dodge | cleanse_dodge | `HitAvoided` via dodge | A | growth, flexibility | X |
| 150 | When your Barrier breaks, gain `$` Resolve and burst Cold damage around you | bar_break | `BarrierBroken` | A | cold, resonance, essence.frost | Σ |

# 13. Triggered effects (16)

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 151 | `$%` chance on hit to deal a burst of Heat damage | trig_hit_heat | `HitLanded` | W | heat | T |
| 152 | `$%` chance on critical hit to Shock the target | trig_crit_shock | `DamageDealt` + crit | W | charge | T |
| 153 | On kill, gain `$%` increased damage for `n` ticks | trig_kill_dmg | `Killed` | W | decay | T |
| 154 | On Block, apply Weaken to the attacker | trig_block_weaken | `Blocked` | Sh | corrosion | T |
| 155 | On Dodge, your next attack cannot be blocked | trig_dodge_unblock | `HitAvoided` via dodge | A | flexibility, instability | T |
| 156 | On Parry, apply Vulnerable to the attacker | trig_parry_vuln | `Parried` | W Sh | hardness | T |
| 157 | On taking a hit, gain `$` Guard for `n` ticks | trig_hit_guard | `HitLanded` on self | A Sh | mass | T |
| 158 | On taking damage below 35% Health, gain Barrier | trig_low_barrier | `DamageTaken` + `selfHealthBelow 0.35` | A | growth | T |
| 159 | On applying a status, gain `$%` increased damage against that target | trig_status_dmg | `StatusApplied` | W F | arcane | T |
| 160 | On resisting a control effect, deal a burst of damage around you | trig_resist_burst | `ControlResisted` | A Sh | charge | T |
| 161 | On spending Mana, gain `$` Barrier | trig_mana_barrier | `ResourceSpent` mana | F | resonance, growth | T |
| 162 | On combat start, gain `$` Barrier and `$` Resolve | trig_encounter_open | `EncounterStarted` | A | mass, growth | T |
| 163 | Every 4th hit deals `$%` more damage | trig_cadence | `HitLanded` count | W | conductivity | X |
| 164 | On Perfect Block, refund the full Stamina cost and gain `$` Gauge | trig_perfect_refund | `HitAvoided` via perfect_block | Sh | resonance, hardness | X |
| 165 | On kill, the target's ailments transfer to a nearby enemy | trig_kill_transfer | `Killed` | W | decay, corrosion | Σ |
| 166 | On Barrier break, become **immune to control** for `n` ticks | trig_bar_ccimm | `BarrierBroken` | A | mass, resonance | Σ |

# 14. Move modification (16)

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 167 | Melee moves gain `+$` Heat damage | mv_add_heat | match `delivery:melee` | W | heat | S |
| 168 | Spells gain `+$` Charge damage | mv_add_charge | match `action:spell` | F | charge | S |
| 169 | **All** of your Attack damage is converted to Heat | mv_aspect_heat | match `action:attack`, `convert` fraction 1.0 | W | heat, essence.fire | X |
| 170 | Convert `$%` of Physical damage to Cold | mv_conv_cold | — | W | cold, conductivity | X |
| 171 | Gain `$%` of Physical damage as extra Charge | mv_extra_charge | — | W | charge, conductivity | X |
| 172 | Your Spells hit `+$` additional targets | mv_targets | match `action:spell` | F | resonance | X |
| 173 | Your Projectiles **chain** to `$` further targets | mv_chain | match `delivery:projectile` | F W | conductivity, charge | X |
| 174 | `$×` Windup on your Heavy moves | mv_windup_heavy | match `hasTag heavy` | W | flexibility | S |
| 175 | `$×` Mana cost of your Channelled moves | mv_cost_channel | match `action:channel` | F | resonance | S |
| 176 | Your Defensive moves also apply Guarded | mv_def_guard | match `action:defensive` | Sh | mass | X |
| 177 | **Heavy Strike** gains a chance to Shock | mv_specific_shock | match `move.heavy_strike` | W | charge, conductivity | X |
| 178 | Your Melee attacks have `$%` chance to repeat at reduced power | mv_repeat | match `delivery:melee` | W | instability, charge | X |
| 179 | Grants the move **Riptide Cast** | mv_grant | — | F | resonance, essence.storm | Σ |
| 180 | Grants the move **Bulwark Slam** | mv_grant_2 | — | Sh | mass, hardness | Σ |
| 181 | Your Dodge also **cleanses one impairment** | mv_dodge_cleanse | match dodge | A | growth, flexibility | X |
| 182 | Attacks tagged `heavy` **trigger** your granted move on a critical hit | mv_trigger | `DamageDealt` + crit, once/chain | W | arcane, instability | Σ |

# 15. Conversion (8)

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 183 | Convert `$%` of Physical damage to Heat | conv_phys_heat | — | W | heat, conductivity | S |
| 184 | Convert `$%` of Physical damage to Toxin | conv_phys_toxin | — | W | toxicity, solubility | S |
| 185 | Convert `$%` of Magic damage to Arcane *(unresistable)* | conv_magic_arcane | — | F | arcane, resonance | X |
| 186 | Gain `$%` of Cold damage as extra Physical | conv_extra_phys | — | W | cold, hardness | X |
| 187 | `$%` of damage taken is dealt to Mana instead of Health | conv_dmg_mana | `DamageTaken` | F A | resonance, arcane | X |
| 188 | `$%` of Barrier gained is also gained as Stamina | conv_bar_stam | `StatusApplied` barrier | A | growth, flexibility | X |
| 189 | `$%` of your Armour also applies as Magic resistance | conv_armour_magic | — | A | hardness, resonance | Σ |
| 190 | `$%` of your Thorns damage is converted to Healing *(capped)* | conv_thorns_heal | on thorns | A Sh | growth, essence.nature | Σ |

# 16. Conditional (12)

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 191 | `$%` more damage while below 35% Health | cond_low_dmg | `selfHealthBelow 0.35` | W | decay, instability | X |
| 192 | `+$%` resistances while below 35% Health | cond_low_res | `selfHealthBelow 0.35` | A | insulation | X |
| 193 | `$%` more damage against **Frozen** enemies | cond_vs_frozen | `targetHasStatus freeze` | W | cold, hardness | S |
| 194 | `+$%` critical chance against **Shocked** enemies | cond_vs_shocked | `targetHasStatus shock` | W | charge | S |
| 195 | `$%` more damage against **Burning** enemies | cond_vs_burning | `targetHasStatus burn` | W F | heat | S |
| 196 | `$%` more damage against **Poisoned** enemies | cond_vs_poisoned | `targetHasStatus poison` | W | toxicity | S |
| 197 | `$%` more damage against **notable** enemies | cond_vs_elite | `targetHasTag tier:notable` *(derived — D-11)* | W | arcane | X |
| 198 | `$%` increased damage while **Guarded** | cond_guarded | `selfHasStatus guarded` | Sh W | mass | S |
| 199 | `$%` more damage while your Barrier is active | cond_barrier | `selfHasStatus barrier` | * | growth, resonance | X |
| 200 | `$%` more damage after Blocking, for `n` ticks | cond_after_block | `Blocked` | Sh W | mass | T |
| 201 | `$%` more damage while at high Stamina | cond_high_stam | `resourceAbove stamina 0.75` | W | flexibility | S |
| 202 | `+$%` all resistances while in a Realm with a matching hazard affix | cond_realm | `realmHasAffix` | A | insulation, resonance | X |

# 17. Profession — gathering tools (14)

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 203 | `$×` Fishing action interval | pf_fish_interval | scope `profession:fishing` | rod | flexibility | S |
| 204 | `$×` Mining action interval | pf_mine_interval | scope `profession:mining` | pick | −mass, flexibility | S |
| 205 | `+$%` chance to preserve bait | pf_bait_preserve | scope fishing, on consume | rod | hardness (hook) | S |
| 206 | `+$%` chance to double the catch | pf_fish_double | `OutputProduced` fishing | rod | growth | S |
| 207 | `+$%` chance to double ore | pf_ore_double | `OutputProduced` mining | pick | hardness | S |
| 208 | `$×` weighting for **rare** fish | pf_fish_rare | scope fishing, rare table | rod | resonance, instability | S |
| 209 | `$×` weighting for **rare** ore | pf_ore_rare | scope mining, rare table | pick | resonance, instability | S |
| 210 | `+$` Harvest penetration | pf_harvest_pen | scope gathering | pick axe | hardness, mass | S |
| 211 | `+$%` catch quality *(higher potency)* | pf_fish_quality | scope fishing | rod | resonance, growth | S |
| 212 | `$×` Mastery XP | pf_mastery | scope profession | Tl | arcane, resonance | S |
| 213 | `$×` Profession XP | pf_xp | scope profession | Tl | arcane | S |
| 214 | `$×` weighting toward `class:venomous` catches | pf_bias_venom | scope fishing, family bias | rod | toxicity | X |
| 215 | `$%` chance to recover bait after a failed cast | pf_bait_recover | `ActionFailed` fishing | rod | flexibility, growth | T |
| 216 | While in a Realm with the **Eternal Night** affix, `$×` rare weighting | pf_realm_night | `realmHasAffix` | rod pick | resonance, decay, essence.abyssal | Σ |

# 18. Profession — crafting tools (12)

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 217 | `$×` Smithing action interval | pc_smith_interval | scope `profession:smithing` | hammer_tool | flexibility | S |
| 218 | `+$%` chance to preserve crafting inputs | pc_input_preserve | scope crafting, on consume | hammer_tool apparatus | hardness | S |
| 219 | `+$` Craftsmanship *(raises the quality band)* | pc_craftsmanship | — | hammer_tool | hardness, resonance | S |
| 220 | `$×` Integrity cost of transformations | pc_integrity | — | hammer_tool | mass, insulation | S |
| 221 | `+$%` chance of an **exceptional** fabrication | pc_exceptional | on craft quality roll | hammer_tool | resonance, instability | S |
| 222 | `$×` catalyst effectiveness | pc_catalyst | — | apparatus | affinity, solubility | S |
| 223 | `$×` outcome **variance** *(a precision tool)* | pc_variance | — | hammer_tool apparatus | hardness, −instability | X |
| 224 | Biases the reaction channel toward **thermal** properties | pc_bias_thermal | — | hammer_tool | heat, conductivity | X |
| 225 | Biases the reaction channel toward **biological** properties | pc_bias_bio | — | apparatus | growth, toxicity, solubility | X |
| 226 | `+$%` potency retention *(offsets the weighted-mean penalty)* | pc_potency | — | apparatus | affinity, resonance | X |
| 227 | `$%` chance for a transformation to consume **no** Integrity | pc_free_transform | `ActionCompleted` crafting | hammer_tool | resonance, arcane, essence.radiant | Σ |
| 228 | `$×` chance of an unusual reaction result *(wider signature matching)* | pc_unusual | — | apparatus | instability, arcane | X |

# 19. Realm and utility (8)

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 229 | `$×` Realm Knowledge gained | rl_knowledge | — | * | resonance, arcane | S |
| 230 | `$×` loot quantity | rl_loot | `OutputProduced` loot | * | instability, luck-ish (arcane) | S |
| 231 | `$×` rare loot weighting | rl_loot_rare | loot rare table | * | resonance, instability | S |
| 232 | `+$` harvest yield from creatures | rl_harvest | `OutputProduced` harvest | W | hardness (edge) | S |
| 233 | Reveals enemy resistances on encounter start | rl_assay_enemy | `EncounterStarted` | F | resonance, arcane | X |
| 234 | Reveals one hidden location per Realm run | rl_reveal_loc | `RealmEntered` | * | resonance | X |
| 235 | `$×` damage taken from Realm hazards | rl_hazard | — | A | insulation | S |
| 236 | On extraction, `$%` chance to duplicate one secured material | rl_extract_dupe | `ExtractionCompleted` | * | arcane, instability, essence.storm | Σ |

# 20. Anomalous — Overreach only (12)

> These are the **only** affixes permitted to bend proc-safety rules
> (`effect-foundation.md` §6.3), and the only ones that may raise `MAX_PROC_DEPTH` — by exactly 1.
> Every one is drawn from the item's own genetic families (`affixes.md` §6.1).

| # | Modifier | Family | Notes | Genetics | C |
|---|---|---|---|---|---|
| 237 | **Thorns trigger your on-hit effects** | an_thorns_proc | `max_depth 3`, once/chain | hardness, charge | A |
| 238 | **Your retaliation chains** to one further enemy | an_thorns_chain | `max_depth 3` | conductivity, charge | A |
| 239 | Thorns inherit `$%` of your weapon's aspects | an_thorns_inherit | thorns lane = weapon lane | affinity, arcane | A |
| 240 | Burns you apply can **Burn a second target** | an_burn_spread | `max_depth 3`, once/target | heat, essence.fire | A |
| 241 | Your critical hits **do not consume** Control Immunity | an_cc_free | the closest thing to a chain-lock the game permits | resonance, arcane | A |
| 242 | You may exceed the maximum-resistance ceiling by `$%` | an_maxres | raises the 0.90 hard cap | insulation, essence.frost | A |
| 243 | Resistance **inversion** no longer has a floor | an_invert_free | removes `INVERSION_FLOOR` | arcane, resonance, instability | A |
| 244 | Your Barrier absorbs **control buildup** as though it were damage | an_bar_cc | buildup drains Barrier instead of your Resolve | resonance, mass | A |
| 245 | Ailments on you are **converted into Barrier** | an_ail_barrier | inverts the attrition rule | growth, essence.nature | A |
| 246 | Killing a Feared enemy **resets your cooldowns** | an_fear_reset | once/encounter | decay, essence.necrotic | A |
| 247 | Your Stored Retaliation **never empties** | an_stored_keep | releases without consuming | mass, hardness, resonance | A |
| 248 | This item's affixes **re-roll their values on every Realm entry** | an_chaos | the item is never twice the same | instability, arcane | A |

# 21. Essence-conditional (6) — **[D-04]**

> Essence rides on packets, moves and effects as a **tag**, readable by the existing `hasTag`
> condition. It is never a resistance lane and never enters the mitigation stages. These entries
> exist to prove the capability is content, not just architecture.

| # | Modifier | Family | Trigger / Condition | Forms | Genetics | C |
|---|---|---|---|---|---|---|
| 249 | Storm-essenced hits **chain** to one further target | es_storm_chain | `hasTag essence:storm` | W F | charge, conductivity, essence.storm | X |
| 250 | Radiant effects deal `$%` more damage to **Undead** | es_radiant_undead | `hasTag essence:radiant` + `targetHasTag origin:undead` | W F | resonance, essence.radiant | X |
| 251 | Abyssal spells have `$%` chance to **invert** the target's resistance | es_abyssal_invert | `hasTag essence:abyssal` + `actionHasTag action:spell` | F | corrosion, arcane, essence.abyssal | Σ |
| 252 | When struck by a **Frost-essenced** effect, gain `$` Barrier | es_frost_struck | `HitLanded` on self + `hasTag essence:frost` | A | insulation, growth, essence.frost | T |
| 253 | `$%` increased **Storm** damage | es_storm_inc | scope `essence:storm` | W F | charge, essence.storm | S |
| 254 | Your Burns inherit **Fire essence**, gaining `$%` magnitude and duration | es_fire_burn | `StatusApplied` burn | W F | heat, essence.fire | X |

**Note on 253.** Because scoped contributions (D-12) accept any tag as a scope, an essence-scoped
`increased` falls out for free — a rarer, stronger sibling of `+$% increased Charge damage`
**[13-pattern]** that never becomes a resistance. **Keep these rare**: they compete directly with
lane-scoped increases and exist to make essence feel supernatural, not to be the default.

**Note on 252.** This is a *trigger* reading an incoming essence tag, which is permitted. An
essence-scoped **resistance** or **avoidance** affix would not be — that is the D-04 invariant,
and the validator rejects it.

---

# Distribution summary

| Class | Count | Intended role |
|---|---|---|
| **S** Standard | ~119 | The fundamentals. Common, readable, `StatGrant`s. |
| **T** Trigger | ~27 | The first layer of "this creates gameplay". |
| **X** Exotic | ~75 | Build-defining. Genetically gated. Rare rolls or Overreach. |
| **Σ** Signature | ~21 | Requires a fabrication signature to have fired. Never rollable normally. |
| **A** Anomalous | 12 | Overreach only. The only proc-rule breakers. |

**254 entries.**

**Roughly half are Standard**, which is deliberate: the brief's principle 9 says weird effects
belong at the top, which requires a solid, boring base for them to sit on. A pool that is all
Exotic is a pool where nothing feels special.

# Coverage against the brief

Every example the brief listed maps to an entry above:

| Brief's example | # |
|---|---|
| +Flat Thorns Damage · % increased Thorns | 87–93 |
| chance to retaliate when hit / when blocking | 94, 95 |
| chance for Thorns to apply Bleed / Fear | 99, (122 pattern) |
| +Physical Damage Reduction | 51 *(it is the physical resistance lane)* |
| +Max Health / Mana / Stamina | 46–48 |
| Heat / Cold / Charge Resistance | 53–55 |
| chance to negate a Charge-tagged hit | 79 |
| chance to invert an enemy's resistance | 36, 243 |
| resistance penetration | 27–32 |
| status resistance · reduced status duration · chance to avoid Poison | 63, 113, 83 |
| chance to Freeze / Ignite(Burn) / Shock / Fear | 121, 105, 107, 122 |
| damage against Frozen · crit chance against Shocked | 193, 194 |
| Stamina on Crit · Mana on Block · healing after Parry | 130, 131, 147 |
| Heavy Strike gains additional Heat damage | 167, 177 |
| a spell chains to another target | 173 |
| Block releases stored retaliation damage | 102 |
| Dodge cleanses a status | 149, 181 |
| spending Stamina empowers the next attack | 140 |
| an item grants a Move · changes how a Move behaves | 179–180, 167–178 |
| fishing rod: interval, catch quantity, bait preservation, rare chance, quality, family targeting, additional fish, bait recovery, Mastery XP, Realm conditions | 203, 206, 205, 208, 211, 214, 206, 215, 212, 216 |
| smithing hammer: interval, input preservation, Craftsmanship, Integrity, exceptional fabrication, family bias, catalyst, Mastery XP | 217–224, 212 |
| mining tool: interval, yield, double, rare weighting, harvest penetration, family effects | 204, 207, 209, 210 |
