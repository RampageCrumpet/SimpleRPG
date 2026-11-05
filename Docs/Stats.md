# Character stats

Six core attributes (Strength, Constitution, Dexterity, Agility, Willpower, Intelligence) and thirteen derived stats (`DerivedStats`) share one `AttributeSet` per character. A `StatProfile` asset chooses how a kind of character gets its derived stats:

- **Derived** (players): each stat is `base + sum of curve(source)` over core attributes or other derived stats. Curves are linear (`scale * x`) or diminishing (`scale * x / (x + halfPoint)`).
- **Direct** (enemies): each stat is an authored value; no core attributes are needed.

Modifiers (items, statuses) apply on top of the resolved value through the existing layers (add, percent, multiply) and are withdrawn by handle. A `StatusDefinition` lists stat modifiers that are active while a stack is; with `StrongestOnly` magnitude only the strongest stack counts.

Baselines live in `Assets/Stats/PlayerStats.asset` and are asserted by `StatBaselineTests`: Maximum Health 10 per Constitution, Movement Speed 0.4 per Agility (100 and 4 at attribute 10), Physical Power per Strength, Magical Power per Intelligence. Attack, casting, interaction and equip speed, carrying capacity and spell preparation capacity are catalogued only.

Consumers: `Character.MaxHealth` (rounded, at least 1, replicated; a character with no Maximum Health stat falls back to its serialized value) and the first person controller's server-side movement (`Character.Stats.MovementSpeed`).

## Damage reduction

Applied by `CombatResolver`, the one path damage takes, through `DamageMitigation` (plain code, tested exactly). A target that implements `IDamageMitigator` (a `Character` does, from its stats) says what fraction of damage of a given type its defences remove.

**Mitigation depends on the damage type, and on nothing else.** Every `DamageRequest` carries a `DamageType`:

- **Physical** is met by Physical Damage Reduction: weapon blows, and effects like a bleed.
- **Magical** is met by Magical Damage Reduction: spells, and effects like a burn or a curse.
- There are only these two. Elemental resistances are not part of the game yet.

How the damage was delivered (`DamageProvenance`: a swing, a spell, a proc, a periodic tick, the environment) does **not** choose the defence. Provenance is for rules, so that an on-hit proc reacts to a weapon blow and not to its own explosion. A burning tick is magical because the burn says so; a bleeding tick is physical because the bleed says so.

**Bypassing is explicit.** A request that skips the defences sets `BypassesMitigation`; nothing is bypassed by default or by inference. The ability graph's damage node has the same two settings, **Type** (physical unless set) and **Bypasses Mitigation**. A bleed that ignores armour once applied is a damage node, or a status tick, with Bypasses Mitigation ticked: a deliberate property of that effect. No such effect exists yet. `Character.TakeDamage` takes both too and mitigates unless told not to; tests and scripted deaths that must not be reduced pass `bypassesMitigation: true`.

Firebolt and Icebolt are declared magical in their assets. A new damage node is physical until set, so a magical effect has to say so.

**The arithmetic:**

- A reduction is the fraction of damage removed, kept between 0 and 0.9, so nothing makes a character immune. The stats come from diminishing curves, and reductions from separate sources stack through the stat's modifier layers (an item's grant, an affix, a status).
- The struck region's factor and the reduction are applied together and the result rounded once, so rounding never compounds. A blow that was above zero and lands at all does at least 1.
- Fatal or not is judged on what lands, and the events, including `DamageResolvedEvent`, report the damage after both adjustments.

A character with no defence stats is unaffected, so enemies authored with a direct profile take what they always did unless that profile gives them a reduction.

## Armor Rating

`simplerpg.stat.armor_rating` (`DerivedStats.ArmorRating`) is a **derived stat, not a seventh core attribute**. It starts at zero and is supplied by equipment: a worn piece grants it with an ordinary `GrantSpec` on the stat key, and an affix can add to it (flat) or scale it (percent or factor) through the same layers, exactly as for any other stat. Taking the piece off releases its modifiers. Nothing about it is special-cased in code.

The player profile (`Assets/Stats/PlayerStats.asset`) turns it into **Physical Damage Reduction** with a contribution of its own, beside Constitution's:

| Contribution to `physical_damage_reduction` | Source | Curve | Scale | Half point |
|---|---|---|---|---|
| 1 | `simplerpg.attr.constitution` | diminishing | 0.9 | 100 |
| 2 | `simplerpg.stat.armor_rating` | diminishing | 0.9 | 100 |

Each contribution has its **own scale and half point**, so the two are tuned independently and an armor point is not assumed to be worth a Constitution point. The values above are a starting point for playtesting, not a decision: they are authored data in the profile asset, and `ArmorRatingTests` checks the mechanics (and builds sheets with deliberately unlike curves), not these numbers. Magical Damage Reduction is not affected by armor. The two contributions can sum past the cap before the resolver holds the total to 0.9.

With the starting numbers a Constitution of 10 alone turns aside about 8 percent of a physical blow, and 100 Armor Rating adds 45 percent on top of that.

Enemies use direct profiles, which take authored values and have no contributions: give an enemy reduction by authoring its Physical Damage Reduction directly.

## Weapon damage

A weapon blow is worked out in three steps, and only weapon attacks take them. Ability and magical damage never do.

1. **The weapon damage calculation** (`WeaponDamageCalculator`, run when the weapon touches a target): `(weapon base + Weapon Damage) x Weapon Damage Multiplier`.
   - *Weapon Damage* (`simplerpg.stat.weapon_damage`) holds only bonuses: flat bonuses from any worn item, and item-local **Enhanced Damage**. Enhanced Damage is an affix stat effect scoped to *This Item*: a percentage of that one weapon's own base damage, applied as a flat amount, so it never scales another item's bonus.
   - *Weapon Damage Multiplier* (`simplerpg.stat.weapon_damage_multiplier`) is 1 plus the percentage Weapon Damage bonuses of worn items. They are the stat's percent layer, so they add together (+10% and +20% are +30%) before they scale.
   - Bonuses of one kind are added together before the kinds multiply.
2. **The melee attack ability** then multiplies by the combo scalar and by Physical Power (1 + 0.05 per point, constants in `MeleeAttack.asset`, tuned there). A starting character (Strength 10) deals the same as before: nothing here changes a blow with no bonuses.
3. **The combat resolver** applies the target's mitigation.

A character whose profile lacks the two stats (the skeleton) deals its weapon's damage as it is. Nothing displays the two stats beyond the stat sheet.
