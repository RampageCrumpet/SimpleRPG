# Triggered effects

A **triggered effect** is "when this happens to an entity, and these conditions hold, maybe run this ability, aimed at that party". It is how a sword reduces an enemy's armor when it hits, how a spell leaves a lasting reaction, and how an affix gives its item behavior beyond stat changes. It is a `TriggeredEffect` asset (`Runtime/Rules`), run by one server-only `TriggeredEffectEngine`.

The point is to make new behavior without new code. **The asset is a trigger, conditions and a reference to an ordinary ability graph (plus a status asset for anything lasting)**; what the behavior does is graph and status data. New content is new assets. New *kinds* of trigger, condition or host are small classes, added when something needs them.

## The asset

| Field | Meaning |
|---|---|
| Trigger | `OnHit`, `OnKill`, `OnTakeDamage`, `OnOwnerDeath`. Added when the game raises the event behind it; there is no `OnCrit` because there are no crits. |
| Scope | `Owner`: anything the owner does. `Host`: only what the host itself did (the sword that carries it). Chosen per effect. |
| Conditions | A list; **all** must hold. `FatalCondition`, `BodyLocationCondition` today. A new test is a new `EffectCondition` subclass. |
| Chance | Rolled from the gameplay stream, only after the conditions pass and only when the outcome is in doubt. |
| Target | `Other` (the one struck, or whoever struck the owner) or `Owner`. |
| Ability | The ability to run. It starts at a **Triggered Effect** node and can read **Trigger Target** and **Trigger Other**. |

Several effects on one thing are independent: "a great deal extra damage on a fatal head blow" and "hurts the wielder on a blow to the arm" are two assets with their own triggers and conditions.

Timing, stacks, durations and refresh are **not** part of the effect. They belong to the ability and the status it applies (`StatusDefinition`: grouping, reapplication, duration, magnitude). That keeps one place for them. Stacks are kept per status group, and each host's effects run independently.

## Hosts

The engine knows nothing about what brings an effect. A **host** activates effects for an owner (`Activate(owner, host, effects)`) and removes them (`Deactivate`). The effects are active for as long as the host is.

- **A worn item** (`ItemEffectHost`): active while equipped. The effects are those the item asset declares plus those its rolled affixes bring (`Item.AllTriggeredEffects`).
- **An applied status** (`StatusEffectHost`): active for the holder from application until the last stack ends. This is how a spell's lasting effects work: the spell applies a status; the status carries the effects (`StatusDefinition.TriggeredEffects`).
- **An affix** brings effects through its item. A `TriggeredAffixEffect` is an `AffixEffect` that attaches a `TriggeredEffect`; an affix may have any number of them next to stat effects. Nothing is rolled for it yet.

A "while equipped" condition for an effect that must only last while a weapon is out is not built; the effect's own duration (its status) times it out. Adding it later is a new condition or a status lifetime tied to its host; the host model already carries the information.

## What a trigger carries

`TriggerContext`: the trigger, the owner, the other party, the source (the item or ability behind the damage), the body location, the amount actually dealt, whether it was fatal, and a position. The engine builds it from the event; conditions read it; the ability is given the other party, the body location and its target (`ExecutionRequest.EventVictim`, `BodyLocation`, `CurrentTarget`).

- `OnHit` fires after the damage is resolved (it comes from `DamageResolvedEvent`, direct weapon blows only), so it knows the amount and whether the blow killed. What an effect itself causes is a spell, proc or periodic damage and never triggers a hit effect again.
- `OnKill` fires for the killer of any death, by any means.
- `OnTakeDamage` fires for the one struck, with the attacker as the other party.
- `OnOwnerDeath` runs even if its host is removed during the death.

## Adding one

1. Build the behavior as an ability (starting at a **Triggered Effect** node) and, for lasting behavior, a status. Use **Trigger Target** to say who it affects.
2. Create a **Triggered Effect** asset: trigger, scope, conditions, chance, target, the ability.
3. Give it to a host: the item's *Triggered Effects*, a status's *Triggered Effects*, or an affix through a *Triggered* effect.
4. Run **SimpleRPG > Definitions > Rebuild Definition Registry**.

## Not built yet

`OnCrit`, `OnAttack` (a swing starting), `OnBlock`; a "while equipped" condition; rolled parameters for a triggered affix (chance, magnitude); showing an effect's text to the player beyond the affix description. Each waits for its event or its system.
