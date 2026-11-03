# SimpleRPG.Combat

What a combatant *is*: `Character`, stats, loadout, hit detection, `Weapon`, `Projectile`, the ability slot types, and the small interfaces through which combat asks other layers for things (`IEquippedWeaponSource`, `IRespawnPolicy`, `IPlayerCharacterSource`).

**Does not belong here:** how a combatant is carried, animated or controlled.

Layering rules and the full table: `Docs/CodeLayout.md`. The layering is enforced by `AssemblyLayeringTests`.
