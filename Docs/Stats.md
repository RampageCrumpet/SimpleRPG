# Character stats

Six core attributes (Strength, Constitution, Dexterity, Agility, Willpower, Intelligence) and twelve derived stats (`DerivedStats`) share one `AttributeSet` per character. A `StatProfile` asset chooses how a kind of character gets its derived stats:

- **Derived** (players): each stat is `base + sum of curve(source)` over core attributes or other derived stats. Curves are linear (`scale * x`) or diminishing (`scale * x / (x + halfPoint)`).
- **Direct** (enemies): each stat is an authored value; no core attributes are needed.

Modifiers (items, statuses) apply on top of the resolved value through the existing layers (add, percent, multiply) and are withdrawn by handle. A `StatusDefinition` lists stat modifiers that are active while a stack is; with `StrongestOnly` magnitude only the strongest stack counts.

Baselines live in `Assets/Stats/PlayerStats.asset` and are asserted by `StatBaselineTests`: Maximum Health 10 per Constitution, Movement Speed 0.4 per Agility (100 and 4 at attribute 10), Physical Power per Strength, Magical Power per Intelligence. Attack, casting, interaction and equip speed, carrying capacity and spell preparation capacity are catalogued only.

Consumers: `Character.MaxHealth` (rounded, at least 1, replicated; a character with no Maximum Health stat falls back to its serialized value) and the first person controller's server-side movement (`Character.MovementSpeed`).

Damage reduction semantics (decided, not yet applied; damage work waits for equipment): reduction is a fraction of damage removed, 0 to 0.9, from a diminishing curve; reductions from separate sources stack through the stat's modifier layers; damage is rounded once after reduction and stays at least 1 when it was above 0.
