# Armor affix catalog: DRAFT for review

**Status: a draft. Nothing here is in the game.** No production item data has been populated, no assets created, no code changed. Names and numbers are provisional and expected to change for balance and lore. This page exists so the catalog, its numbers and the concerns it raises can be reviewed before anything is authored.

Scope is the eight families requested: four prefixes and four suffixes, three tiers each, for armor. It extends the existing affix system (`AffixDefinition`, `AffixPool`, `AffixRoller`, `ItemAffixes`; see [Item affixes](Affixes.md)) and invents no stat or mechanic.

## Read this first: what the draft found

1. **Four of the eight families cannot work as the existing infrastructure stands.** An affix today is one effect with one rolled value. Weighted/Burdened/Oppressive (three effects), Bonebound/Gravebound/Deathsworn (two), and the secondary effects in Fitted, Hound, Cruel and Whispers all need several effects per affix. Details and the recommended extension are in "Infrastructure gaps".
2. **Some effects have nothing to act on today.** Magical power is read by no shipped spell; attack speed and interaction speed are read by nothing; Dexterity does nothing at all. See "Which effects are implemented".
3. **Agility is a movement-speed stat in disguise.** One point of Agility is +10 percent movement speed (`0.4 per point` on a base of 4). That makes Hound, Fitted and Tailored overlap, and it sets the Agility range far lower than any other attribute.
4. **There is no level, rarity, prefix or suffix concept.** "Minimum affix level", "one prefix and one suffix" and item names ("Riveted Mail Hauberk of the Veteran") are not representable yet.

## The numbers are sized from the real formulas

Baseline is a character with every core attribute at 10 and no gear (`Assets/Stats/PlayerStats.asset`):

| Stat | Formula today | Baseline | One point of the source is worth |
|---|---|---|---|
| Maximum Health | 10 x Constitution | 100 | +10 health (+10 percent) |
| Movement Speed | 0.4 x Agility | 4.0 | +0.4 speed (**+10 percent**) |
| Physical Power | 1 x Strength | 10 | +1 power; melee damage is `weapon x (1 + 0.05 x power)`, so power 10 is x1.5 and +1 power is +3.3 percent damage |
| Magical Power | 1 x Intelligence | 10 | +1 power (read by nothing yet) |
| Physical Reduction | 0.9 x Con/(Con+100) + 0.9 x Armor/(Armor+100) | 8.2 percent | +0.74 points per Constitution; per Armor Rating point: 0.89 at 0, 0.55 at 28, 0.29 at 75 |
| Magical Reduction | 0.9 x Will/(Will+100) | 8.2 percent | +0.74 points per Willpower |

**The budget rule.** Five armor slots (head, chest, hands, legs, feet) can each carry one prefix and one suffix, and nothing stops all five pieces rolling the same family. "Modest" is therefore defined against stacking: **a top-tier affix is worth about 10 percent of the stat's baseline, so a full set of five of the same top-tier affix adds about 50 percent**; tier II is about 6 percent and tier I about 3 percent. Percentages are PercentAdd modifiers (they add together, then apply to the base plus flats), which makes them readable ("+5% Constitution") and independent of any future change to the base.

## The catalog

Common to every row, all provisional:

- **Eligible slots:** all five armor slots (head, chest, hands, legs, feet). No armor-class restriction, as requested. Legs and feet have no items yet, so an item can only roll these on the three slots that exist.
- **Tier exclusion:** the three tiers of a family share one exclusion group, so an item never gets two tiers of one family. A group is a name; the group in the first column is what would be authored.
- **Minimum affix level:** tier I at level 1, tier II at 8, tier III at 16. **No level exists to compare these to** (see the gaps); they are written as intent.
- **Weight:** tier I 10, tier II 5, tier III 2, so higher tiers are rarer.
- **Layer:** `Add` is a flat amount; `PercentAdd` is a fraction (0.05 is +5 percent). Ranges are rolled uniformly and stored in thousandths.
- **Status:** **Live** means the effect changes gameplay today; **Inactive** means the stat exists but nothing reads it, so it would roll and show but do nothing.

### Prefixes

**1. Enhanced Armor Rating** (group `armor_enhance`)

| Tier | Name | Effect | Range | Status |
|---|---|---|---|---|
| I | Riveted | Armor Rating, Add | +2 to +3 | Live |
| II | Reinforced | Armor Rating, Add | +4 to +6 | Live |
| III | Ironbound | Armor Rating, Add | +7 to +9 | Live |

Flat, as requested. Flat armor helps a coif (5) far more than a plate (45) proportionally; a percentage would do the opposite. Worth a decision. A full set of five Ironbound adds 45 Armor Rating: a 75-rating set becomes 120, which is **38.6 to 49.1 percent** physical reduction.

**2. Movement speed** (group `speed_fit`)

| Tier | Name | Effect | Range | Status |
|---|---|---|---|---|
| I | Fitted | Movement Speed, PercentAdd | +1 to +2 percent | Live |
| II | Tailored | Movement Speed, PercentAdd | +2 to +3 percent | Live |
| III | Masterworked | Movement Speed, PercentAdd | +3 to +4 percent | Live |
| III | Masterworked | Interaction Speed, PercentAdd | +3 to +5 percent | **Inactive, and cannot work yet** (see gaps) |

A full set of Masterworked is +20 percent speed (4.0 to 4.8). The interaction-speed line is the optional secondary effect you described; it needs a baseline for that stat first.

**3. Willpower and magical reduction** (group `ward_bone`; **two effects per tier**)

| Tier | Name | Willpower, PercentAdd | Magical Reduction, Add | Status |
|---|---|---|---|---|
| I | Bonebound | +3 to +4 percent | +1.0 to +1.5 points | Live |
| II | Gravebound | +5 to +6 percent | +2.0 to +2.5 points | Live |
| III | Deathsworn | +7 to +8 percent | +3.0 to +3.5 points | Live |

Willpower already produces magical reduction (0.74 points per point), so the direct line is a second route to the same stat. A full set of Deathsworn: Willpower 10 to 14 (reduction 8.2 to 11.1 percent) plus 17.5 points direct, about **28.6 percent** magical reduction.

**4. Armor, Constitution and a speed penalty** (group `burden`; **three effects per tier**)

| Tier | Name | Armor Rating, Add | Constitution, PercentAdd | Movement Speed, PercentAdd | Status |
|---|---|---|---|---|---|
| I | Weighted | +3 to +4 | +3 to +4 percent | -1 percent | Live |
| II | Burdened | +5 to +7 | +5 to +6 percent | -2 percent | Live |
| III | Oppressive | +8 to +10 | +7 to +8 percent | -3 percent | Live |

A full set of Oppressive: +50 Armor Rating, +4 Constitution (+40 health and +2.7 points reduction), and **-15 percent speed**. The penalty is a plain negative modifier, which the stat system supports. It can be offset by Masterworked on other pieces, which is an interaction to be aware of.

### Suffixes

**5. Maximum health** (group `vigor`)

| Tier | Name | Effect | Range | Status |
|---|---|---|---|---|
| I | of the Veteran | Maximum Health, Add | +3 to +4 | Live |
| II | of the Survivor | Maximum Health, Add | +5 to +7 | Live |
| III | of the Undying | Maximum Health, Add | +8 to +10 | Live |

A full set of Undying is +50 health (100 to 150).

**6. Agility and attack speed** (group `hound`; **two effects per tier**)

| Tier | Name | Agility, PercentAdd | Attack Speed | Status |
|---|---|---|---|---|
| I | of the Hound | +1 to +2 percent | placeholder | Agility Live; attack speed **Inactive, and cannot work yet** |
| II | of the Wolf | +2 to +3 percent | placeholder | same |
| III | of the Predator | +3 to +4 percent | placeholder | same |

Agility is deliberately small because **each percent of Agility is a percent of movement speed**: a full set of Predator is +20 percent speed, on top of any Fitted. The attack-speed numbers are left out because the stat has no baseline (see gaps).

**7. Physical power and Strength** (group `cruel`)

| Tier | Name | Physical Power, Add | Strength, PercentAdd | Status |
|---|---|---|---|---|
| I | of the Cruel | +0.5 to +1.0 | | Live |
| II | of the Butcher | +1.0 to +1.5 | | Live |
| III | of the Executioner | +1.5 to +2.0 | +3 to +4 percent | Live (two effects at tier III) |

Melee damage reads physical power, so this is the one offensive family that works today. A full set of Executioner: power +10, Strength +2 (another +2 power), power 22 and melee damage x2.1 against x1.5, about **+40 percent**. That is the largest full-set effect in the catalog, so it is the most likely to want its numbers cut.

**8. Magical power and Intelligence** (group `whisper`)

| Tier | Name | Magical Power, Add | Intelligence, PercentAdd | Status |
|---|---|---|---|---|
| I | of Whispers | +0.5 to +1.0 | | **Inactive** |
| II | of the Hollow | +1.0 to +1.5 | | **Inactive** |
| III | of the Abyss | +1.5 to +2.0 | +3 to +4 percent | **Inactive** (two effects at tier III) |

## Which effects are implemented

| Effect | Status | Evidence |
|---|---|---|
| Armor Rating | **Live** | Physical Damage Reduction contribution in the player profile; combat applies it |
| Maximum Health | **Live** | `Character` resolves it from the stat |
| Movement Speed | **Live** | The server moves a character by it |
| Constitution, Agility, Strength | **Live** | Feed health and reduction, speed, and physical power |
| Physical Power | **Live** | Both melee abilities scale damage by `1 + 0.05 x power` |
| Willpower, Magical Reduction | **Live** | Magical damage type is mitigated by it |
| **Magical Power, Intelligence** | **Inactive** | Firebolt and Icebolt deal a fixed 20; no shipped ability reads magical power |
| **Attack Speed** | **Inactive** | Catalogued and shown on the stat sheet; nothing reads it, and it has no baseline |
| **Interaction Speed** | **Inactive** | Same; interaction time is a fixed number on each interactable |
| **Dexterity** | **No effect at all** | No derived stat is built from it and no code reads it |

Two of these are worse than "unread": **a percentage on a stat whose base is zero does nothing.** Attack speed and interaction speed have no base value, so a PercentAdd affix on them multiplies nothing. They need a baseline (1.0 meaning normal speed, say) before any affix can work, and that is a stat-design decision this draft does not make.

**Dexterity is also absent from the catalog.** You asked for the pool to include all six core attributes, but the eight families touch five (Constitution, Agility, Strength, Intelligence, Willpower). The sixth has no family, and would have no effect if it had one. Either add a family with no effect yet, or leave it out until Dexterity does something.

## Infrastructure gaps

What the existing system already does well, and is reused unchanged: eligibility by slot (`allowedSlots`), mutual exclusion by group (`exclusiveGroup`, which handles "related tiers are mutually exclusive"), weights, ranges, the fixed-at-drop result, and replication of the result.

| Need | Today | Concern |
|---|---|---|
| **Several effects in one affix** (families 2 to 4, 6 to 8) | One attribute, one layer, one range per affix; `ItemAffixes` stores one value per affix | The largest gap. See below |
| **Prefix and suffix** | None. An affix has a stat label, not a name or a kind | A pool can ask for 1 to 2 affixes but cannot guarantee one prefix and one suffix, or stop two prefixes. Item names need a tier name ("Riveted") separate from the effect label ("Armor Rating") |
| **Minimum affix level** | None | There is no item level, dungeon depth or loot tier on a roll to compare against |
| **Rarity** | None | Could be derived from the number of affixes; not needed yet |
| **Exclusion across families** | One exclusion group string per affix | Riveted and Weighted both give flat armor and can stack on one item; excluding them would need a group set. Probably acceptable as is |

**On several effects in one affix.** The system stores the *result* of the roll, not a seed, so that editing an affix later cannot change an item already rolled. A multi-effect affix has to follow the same rule, which rules out storing one value and deriving the others from it. Recommended extension, to review:

- `AffixDefinition` gets a list of effects (attribute, layer, range) in place of its single one, with the single-effect form being a list of one.
- `ItemAffixes` stores up to three rolled values per affix instead of one. That is four affixes by three values, sixteen numbers instead of eight on the wire and in the stash record.
- Each effect rolls its own value independently (as Diablo II does) rather than at a shared fraction of its range.
- `Describe` returns one line per effect.

Nothing replaces the roller, the pool, the grant engine or the registry; this widens two types.

## Compatibility concerns, in one place

1. **Multi-effect storage** (above) changes the replicated and saved shape of an affix. Do it before any production items exist, not after.
2. **Agility overlaps movement speed.** If both Fitted and Hound are common, speed stacks to about +40 percent at a full set. There is no cap on movement speed from modifiers, and adding one would be a new rule. Options: lower the speed-per-Agility slope in the profile, lower these ranges further, or accept it.
3. **Flat versus percent Armor Rating** changes which pieces benefit (see family 1).
4. **Executioner stacking** is the strongest full-set effect and double counts (power directly and through Strength).
5. **Prefix and suffix limits, names and levels** are three small additions that all need a decision before the catalog can be populated as designed.
6. **Legs and feet** have no items, so eligibility on those slots cannot be exercised yet.
7. **A pool is a flat list today.** A catalog of 24 tier entries plus kinds and levels is the first use that will show whether the pool's shape needs to carry more (tier grouping, per-kind counts).

## Decisions for the owner before anything is authored

1. Approve extending `AffixDefinition` and `ItemAffixes` for several effects per affix (recommended: independent rolls, up to three values).
2. Add an affix **kind** (prefix or suffix) with per-kind limits and a tier name, so items can be named and capped at one of each?
3. Where does a **level** come from: the dungeon floor, a loot table tier, or something else? Until it is answered, tier gating cannot be implemented and all tiers would be available everywhere.
4. Flat or percent for Armor Rating?
5. Give attack speed and interaction speed baselines now, or leave those effects out of the catalog until they are consumed?
6. Dexterity: add a family, or leave it out until it has an effect?
7. Do you want the speed overlap (Agility and movement speed) dealt with in the profile or in the ranges?
8. Should Magical Power families roll before any spell reads magical power, or wait?
