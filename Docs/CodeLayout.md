# Code layout: which assembly does new code go in?

The game's code is split into assemblies, one folder each under `Assets/Scripts`. Lower layers never use higher ones, so a new system lands in the right place by construction and a mistake fails a test instead of growing quietly.

**If you cannot say which assembly a piece of code belongs to, ask. Never add to `Session` because it is convenient.**

## The layers

Lower rows may not use higher rows. The leaves (rank 10) are ordered: a leaf uses only the leaves listed before it.

```
Session        Player, GameController, EnemySpawner, NetworkSpawn
UI
Rendering
Delve          the rules of a delve: death drop, extraction, reconnect
World          level generation, rooms, doors, keys, chests, loot bags, world pickups, population
AI             enemy behaviour and senses
PlayerControl  first-person controller, local controls, camera
Loot           loot tables, encounter tables
Interaction    Interactable, InteractableManager, Interactor
Presentation   animation, appearance, articulation, the character's look
AudioPlayback  the audio service, emitters, ambience, footsteps, per-system audio controllers
------------------------------------------------------------------ leaves above, core below
Melee          MeleeDeliveryBase, MeleeDelivery            Carrying   Inventory, Equipment, move/equip/drop rules
Combat         Character, stats, combat loadout, hit detection, Weapon, Projectile, ability slots
Items          Item, ItemIds, affixes, ArmorData, WeaponData, item audio profile
Combos         combo timing and data (a step names its swing sound, so it sits above the audio model)
AudioModel     audio events, tables, tags, contexts, profiles (data only)
Foundation     ServerClock, networking primitives, GameInput, DamageInfo, BodyLocation
Existing       SimpleRPG.Abilities.Runtime -> SimpleRPG.Gameplay.Core
```

`SimpleRPG.Abilities.Integration` wires the ability system to the game and sits above everything; nothing in the layering may use it.

| Assembly | What belongs | What does **not** |
|---|---|---|
| `Foundation` | Primitives everything needs: the server clock, request throttling, the connection handshake and player sessions, input actions, `DamageInfo`, `BodyLocation` | Anything that knows about characters, items or the world |
| `AudioModel` | The data of sound: event definitions, tables, tags, contexts, profiles | Anything that plays a sound |
| `Combos` | The combo machine, predictor, step timing and the `ComboDefinition` asset. May use the audio model (a step names its swing sound); no network or character types | Anything that plays an animation or reads a character |
| `Items` | What an item *is*: `Item`, ids, affixes and their rolling, armor and weapon data, the item's audio profile | Where an item *is* (inventories, slots, the world) |
| `Combat` | What a combatant *is*: `Character`, stats, loadout, hit detection, `Weapon`, `Projectile`, the ability slot types, and the small interfaces combat asks other layers through | How it is carried, animated or controlled |
| `Carrying` | Holding and wearing items: `Inventory`, `Equipment` and the rules that move, equip and drop items | The world form of an item, or any UI |
| `Melee` | Turning a swing into hits: the melee delivery classes | Animation, input |
| `AudioPlayback` | Playing sounds | The data of sound |
| `Presentation` | How a character looks and moves on screen | Rules, state the server owns |
| `Interaction` | The act of interacting: `Interactable`, `Interactor` | What a chest or a door does |
| `Loot` | Weighted tables and encounter tables | What spawns them |
| `PlayerControl` | The local player's first-person control, camera and input handling | Anything a server does |
| `AI` | Enemy states, senses, controllers | Players' controls |
| `World` | The dungeon: layout, rooms, doors, keys, containers, pickups, population | The rules of a delve |
| `Delve` | The run: death, extraction, reconnect, the reward sink | Anything needed by the code below it |
| `Rendering`, `UI` | Screen-space and view code | State. A view reads state and forwards intent |
| `Session` | Bootstrapping the game | Anything another assembly needs; if it is used elsewhere it does not belong here |

Each assembly folder has a short `README.md` saying the same.

## When a lower layer needs something from a higher one

Do not reach up. Put a small interface in the lower layer and let the higher layer implement it. The code already does this in several places; follow the same shape:

| Lower layer asks through | Implemented by | Instead of reaching |
|---|---|---|
| `IEquippedWeaponSource` (Combat) | `Equipment` (Carrying) | `CombatLoadout` -> `Equipment` |
| `IRespawnPolicy` and `RespawnPolicies` (Combat) | `DelveController` (Delve) | `Character` -> `DelveController` |
| `IPlayerCharacterSource` and `PlayerCharacters` (Combat) | `Player` (Session) | World, Delve -> `Player` |
| `IMeleeSwingHost` (Abilities.Runtime) | `MeleeDeliveryBase` (Melee) | the ability slot -> the melee class |
| `IDroppedItemForm` (Carrying) | `WorldItemPickup` (World) | the drop rules -> the world pickup |
| `Inventory.LocalItemMoved` (Carrying, an event) | `InventoryMoveSounds` (AudioPlayback) | `Inventory` -> sound playing |

Observing an event, as the last row does, is the right shape when a lower layer only wants to say that something happened.

## `internal` members

Anything `internal` that crosses an assembly stops compiling. Do **not** add `InternalsVisibleTo`: it is a back door that defeats the layering. Give the type a deliberate, narrow public surface. For example `Inventory` offers `TryTakeOut` (take an item out and get back what it was), `PutBack` (put it here under its old instance id) and `TryFindRoom`; an item is in exactly one place.

## How it is enforced

- **`AssemblyLayeringTests`** (EditMode) reads every `.asmdef` and fails if a game assembly references one that is not strictly below it, if the assemblies form a cycle, if a new game assembly is not placed in the table, or if a layered assembly is auto-referenced. This is the check that matters most. Its table of ranks is the plan as data.
- **`python Docs/tools/layering_check.py`** reads the code and lists every use of a type that points the wrong way, with the files behind it. `--uses` prints, for each assembly, which others it actually uses: the starting point for its `.asmdef` references. It matches type names as words, so a word that is also a type's name in a string or comment shows up as a violation that is not one; real ones found by hand are listed in `KNOWN_COLLISIONS` with the reason. The plan in it must match the ranks in the test; change both together.
- **`autoReferenced` is false** on every layered assembly, so nothing is referenced implicitly.

## Adding a new assembly

1. Decide what it is for and which rank it sits at, and add it to the table above, to `Ranks` in `AssemblyLayeringTests`, and to `PLAN` in `layering_check.py`.
2. Create the folder and the `.asmdef` (`autoReferenced` false) and reference only what `layering_check.py --uses` says it uses, plus the packages it needs.
3. Add a short `README.md` beside the `.asmdef`.
4. Run the EditMode tests.

Namespaces are not assemblies. They are mixed (`SimpleRPG`, `SimpleRPG.InventorySystem`, `SimpleRPG.ObjectInteractions`, ...) and were left alone by the split; aligning them with assemblies is a separate, later change.
