# How a character is put together

A character is a few components on one object, each with one job. `Character` is the combatant and the others are its collaborators.

| Component | Owns | Notes |
| --- | --- | --- |
| `Character` | identity (`Id`), replicated health, death, revive and respawn, the damage entry point, the combat registration | Implements `ICombatant`. `Health`, `MaxHealth`, `IsAlive`, `LifeId`, `OnDeath`, `OnRevive`, `OnTakeDamage`, `TakeDamage`, `RequestRespawn`. |
| `CharacterStats` | the character's `AttributeSet`: the six primaries plus what worn items grant, and the derived stats resolved from them | Server only. `Stats.Attributes`, `Stats.MovementSpeed`, `Stats.HasDerivedStat`. Not a network behaviour: `Character` initialises it under its own identity when it spawns and releases it when it despawns, so the order components start in does not matter. |
| `CombatLoadout` | the abilities on the bar and the weapon used when nothing is wielded | `Loadout.Abilities`, `Loadout.Slots`, `Loadout.Weapon` (the equipped main-hand weapon, else the default) and `Loadout.DefaultWeapon`. |
| `Equipment` | what the character wears or wields | Optional. Enemies such as the skeleton have none, which is why the default weapon is in the loadout, not here. |

`Character` requires `CharacterStats` and `CombatLoadout`, so adding a `Character` in the editor adds them. A script reaches them through `character.Stats` and `character.Loadout`; `Character` does not repeat their members.

## Why the ability slots are kept

`CombatLoadout.Slots` builds one `GraphAbilitySlot` per ability the first time it is read and returns those same instances from then on. A slot holds the cooldown the owner's controls and the ability bar show, so rebuilding the list would reset cooldowns and drop everyone's subscriptions. `CharacterComponentsTests` pins this.

## Adding a character

1. A prefab with a `NetworkObject`, a server-authoritative `NetworkTransform` and a `Character` (the loadout and stats come with it).
2. On `CharacterStats`, a stat profile (or the six primaries for a derived one). On `CombatLoadout`, the abilities in bar order and a default weapon.
3. Optionally `Equipment`, `LootableCorpse` and so on.
