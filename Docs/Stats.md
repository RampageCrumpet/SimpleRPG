# Character stats

Six core attributes (Strength, Constitution, Dexterity, Agility, Willpower, Intelligence) and twelve derived stats (`DerivedStats`) share one `AttributeSet` per character. A `StatProfile` asset chooses how a kind of character gets its derived stats:

- **Derived** (players): each stat is `base + sum of curve(source)` over core attributes or other derived stats. Curves are linear (`scale * x`) or diminishing (`scale * x / (x + halfPoint)`).
- **Direct** (enemies): each stat is an authored value; no core attributes are needed.

Modifiers (items, statuses) apply on top of the resolved value through the existing layers (add, percent, multiply) and are withdrawn by handle. A `StatusDefinition` lists stat modifiers that are active while a stack is; with `StrongestOnly` magnitude only the strongest stack counts.

Baselines live in `Assets/Stats/PlayerStats.asset` and are asserted by `StatBaselineTests`: Maximum Health 10 per Constitution, Movement Speed 0.4 per Agility (100 and 4 at attribute 10), Physical Power per Strength, Magical Power per Intelligence. Attack, casting, interaction and equip speed, carrying capacity and spell preparation capacity are catalogued only.

Consumers: `Character.MaxHealth` (rounded, at least 1, replicated; a character with no Maximum Health stat falls back to its serialized value) and the first person controller's server-side movement (`Character.Stats.MovementSpeed`).

## Damage reduction

Applied by `CombatResolver`, the one path damage takes, through `DamageMitigation` (plain code, tested exactly). A target that implements `IDamageMitigator` (a `Character` does, from its stats) says what fraction of a blow its defences remove:

- **Which defence.** A blow meets the defence its origin names: a weapon blow is **physical** (Physical Damage Reduction); a spell, or an item's triggered effect, is **magical** (Magical Damage Reduction). A damage-over-time tick, a hazard and a raw request meet neither and are never reduced. This mapping is a decision, `DamageMitigation.KindOf`; change it there if damage over time should be armoured.
- **The range.** A reduction is the fraction of damage removed, kept between 0 and 0.9, so nothing makes a character immune. The stats themselves come from a diminishing curve, and reductions from separate sources stack through the stat's modifier layers (an item's grant, a status).
- **One rounding.** The struck region's factor and the reduction are applied together and the result rounded once, so rounding never compounds. A blow that was above zero and lands at all does at least 1.
- **Fatal or not is judged on what lands**, and the events, including `DamageResolvedEvent`, report the damage after both adjustments.

A character with no defence stats, and damage of no kind, are unaffected, so enemies authored with a direct profile take what they always did unless that profile gives them a reduction.

**What this changes for players today.** The player profile derives Physical Damage Reduction from Constitution and Magical from Willpower with a diminishing curve (0.9 times attribute over attribute plus 100), so at the baseline attribute of 10 a player turns aside about 8 percent of a weapon blow or a spell. Armour does not exist as a stat yet: see the open question below.

### Open: how armour feeds the reduction

Worn armour has no rating in the stat system, so it cannot reduce damage until it is given one. The two ways to do it, for a decision:

1. **An armour rating attribute** (for example `simplerpg.attr.armor`): armour grants points, and the player profile adds a contribution to Physical Damage Reduction from it with the same diminishing curve as Constitution. Pieces are authored in readable numbers (a chestplate gives 20), the diminishing curve keeps stacking from reaching the cap, and affixes can add or scale armour like any attribute. Recommended.
2. **Armour grants reduction directly** (a grant on Physical Damage Reduction, authored as a fraction like 0.04). Simplest, but designers write raw fractions and stacking is linear until it hits the 0.9 cap.

Either way an item grant needs no code: it is a `GrantSpec` on an attribute, applied by the grant engine.
