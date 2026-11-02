# Hand-off: layering the game code and trimming its biggest classes

**For:** an agent or developer with a Unity compiler and editor. **Why:** the game is meant to grow for years. Right now one assembly (`SimpleRPG`, about 25,000 lines in 13 folders) holds almost all gameplay, UI, audio and world code, and nothing stops any part from reaching into any other. This work makes the layering real and enforced, so new systems land in the right place by construction.

This was prepared without a compiler. Everything below about *what depends on what* is measured by a script (heuristic, see the caveats) and everything about *what to do* is a recommendation to check against the compiler. Where it says "decide with the owner", stop and ask.

## Ground rules (from `CLAUDE.md`, and what the owner has asked for)

- Build generic, reusable primitives. No legacy shims or compatibility layers when replacing something: change the callers, do not leave a forwarding class behind.
- Definitions (ScriptableObjects) are immutable at runtime; mutable state lives in runtime services.
- Track work with `bd`, not markdown TODO lists. Do not commit or push unless the owner's current instructions say to.
- The owner wants this codebase kept pristine: small steps, each one compiling and green, nothing left half-moved. Do **not** mix a pure move (files, assemblies) with a behaviour change in one commit.
- Unity 6000.6.6f1. Run EditMode and PlayMode tests (commands in `CLAUDE.md`) and **SimpleRPG > Definitions > Validate Definitions** after every step.
- Work from a fresh branch off the latest `claude/zen-brahmagupta-i40kaz` (it has about two weeks of unplayed, compiler-checked-only-by-the-owner changes; get a green baseline first).

## What exists today

| Assembly | Where | References |
|---|---|---|
| `SimpleRPG.Gameplay.Core` | `Scripts/AbilitySystem/Core` | nothing |
| `SimpleRPG.Abilities.Runtime` | `Scripts/AbilitySystem/Runtime` | Core |
| `SimpleRPG` (the big one) | everything else under `Scripts/` outside the editor folders | Core, Runtime, Netcode, Input System, URP, and 8 package GUIDs |
| `SimpleRPG.Abilities.Integration` | `Scripts/AbilitySystem/Integration` | `SimpleRPG`, Core, Runtime |
| `SimpleRPG.Abilities.Editor`, `SimpleRPG.Editor` | `AbilitySystem/Editor`, `Tools/Editor` | `SimpleRPG` and friends |
| Four test assemblies, one test-runner | `Assets/Tests/...` | `SimpleRPG` and friends |

Notes: `Integration` sits **above** `SimpleRPG` (it references it; `SimpleRPG` does not reference it). The two lower ability assemblies are already clean. The package GUIDs in `SimpleRPG.asmdef` are not resolved here; resolve them (Netcode, Unity.Collections, Unity.Mathematics, TextMeshPro, UI, Navigation and similar) before changing the file.

Sizes of the areas inside `SimpleRPG` (files / lines): Characters 49 / 5,100, World 40 / 6,300, Audio 28 / 3,400, UI 19 / 2,400, Abilities 10 / 1,600, Inventory 12 / 1,700, Delve 9 / 1,100, Networking 4 / 300, LootGeneration 5 / 280, Rendering 2 / 200, Controls 1 / 120, Encounters 1 / 20, plus 9 files loose in the root of `Scripts` (`Player`, `Weapon`, `WeaponData`, `Projectile`, `ServerClock`, `GameController`, `EnemySpawner`, `NetworkSpawn`, `SwingHitTracker`).

## What depends on what (by folder)

Produced by `python3 Docs/tools/folder_dependencies.py` (re-run it as you work). Each cell is how many distinct type names an area uses from another.

```
Root           -> Characters(10) World(4) Delve(3) Abilities(2) Inventory(2) Audio(2) LootGeneration(2) Networking(1) Encounters(1)
Abilities      -> Characters(4) Root(4) Audio(1)
Audio          -> Characters(4) Abilities(4) Root(4) Inventory(4)
Characters     -> Root(9) Abilities(9) Inventory(7) Audio(3) World(2) Controls(2) Networking(1) Delve(1)
Controls       -> Inventory(1)
Delve          -> Inventory(7) Characters(6) Root(2) Networking(1) World(1)
Encounters     -> LootGeneration(1)
Inventory      -> Audio(7) Characters(3) World(2) Networking(1)
LootGeneration -> Inventory(1)
Networking     -> Abilities(1)
Rendering      -> World(1)
UI             -> Characters(20) Inventory(8) World(6) Controls(4) Abilities(2) Delve(1) Networking(1) Rendering(1)
World          -> Inventory(7) Characters(7) Audio(6) LootGeneration(5) Networking(3) Root(2) Delve(1) Encounters(1)
```

**Fourteen pairs of areas use each other**, so moving folders into assemblies as they stand will not compile. The ones that matter, with the types behind them (`folder_dependencies.py A B` lists them):

| Cycle | The types that make it |
|---|---|
| `Characters` <-> `Abilities` | `Character`, `BodyLocation` used by the melee code; `GraphAbilitySlot`, `IAbilitySlot`, `MeleeDeliveryBase`, `MeleeDelivery`, `AttackAnimation` used by `Characters` |
| `Characters` <-> `Inventory` | `Equipment`/`Character` used by `EquipmentController`; `Item`, `Inventory`, `ItemIds`, `ItemAffixes`, `EquipmentSlot` used by `Equipment` |
| `Characters` <-> `Root` | `ServerClock`, `Weapon`, `WeaponData`, `Player` used by `Characters`; `Character`, `Hitbox`, `DamageInfo` used by the root files |
| `Characters` -> `World` | `Interactor` uses `Interactable` |
| `Characters` <-> `Delve` | `Character.RequestRespawn` and its RPC ask `DelveController.AllowsRespawn` |
| `Inventory` <-> `Audio` | `Item` holds `ItemAudioProfile`, `MovementAudioProfile`, `AudioTag`; `Inventory` calls `InventoryAudio.Play`; the audio code reads `Item`, `EquipmentSlot` |
| `Inventory` -> `World` | `ItemDropController` creates a `WorldItemPickup` |
| `Audio` <-> `Abilities`, `Audio` <-> `Characters`, `Audio` <-> `Root` | `MeleeHitCue`, `MeleeDeliveryBase`, `ComboStep`; `Character`, `Equipment`; `Weapon`, `WeaponData`, `Projectile` |
| `World` <-> `Inventory`, `World` <-> `Characters`, `World` <-> `Delve`, `World` <-> `Root` | `LootContainer` and pickups use `Inventory`/`Item`; `Inventory` uses `WorldItemPickup`; `Player` |

**Caveats on the numbers.** Names are matched as words, so common ones inflate them. Known false positives: `Inventory -> Chest` (it is the word in the armor-slot tooltip), anything called `Instance` or `Pending`, and the `AbilitySystem/Core -> Characters/Inventory` rows in the "other assemblies" output (Core cannot reference the main assembly, so those are name collisions). Treat the table as a map to dig from. The compiler is the authority.

## Decisions already made by the owner

1. **Split the core finer.** One big core assembly would invite new code to be dumped into it. The layering below has many small assemblies with a stated purpose each.
2. **Audio data used by items moves down with `Item`** (into the `Items` assembly), rather than hiding behind an interface. It can be moved behind an assembly boundary later if that proves worth it.

## The target layering

Lower rows may not use higher rows. The leaves are ordered (a leaf uses only leaves earlier in its list). The measured check for all of this is `python3 Docs/tools/layering_check.py`; it holds the plan as data, so change the plan there if you change it here.

```
Session      Player, GameController, EnemySpawner, NetworkSpawn
UI
Rendering
Delve        the rules of a delve: death drop, extraction, reconnect
World        level generation, rooms, doors, keys, chests, loot bags, world pickups, population
AI           enemy behaviour and senses
PlayerControl  first-person controller, local controls, camera
Loot         loot tables, encounter tables
Interaction  Interactable, InteractableManager, Interactor
Presentation animation, appearance, articulation, the character's look
AudioPlayback  the audio service, emitters, ambience, footsteps, per-system audio controllers
------------------------------------------------------------------ leaves above, core below
Melee        MeleeDeliveryBase, MeleeDelivery            Carrying   Inventory, Equipment, move/equip/drop rules
Combat       Character, stats, combat loadout, hit detection, Weapon, Projectile, ability slots
Items        Item, ItemIds, affixes, ArmorData, WeaponData, item audio profile
AudioModel   audio events, tables, tags, contexts, profiles (data only)      Combos   combo timing and data (pure)
Foundation   ServerClock, networking primitives, GameInput, DamageInfo, BodyLocation
Existing     SimpleRPG.Abilities.Runtime -> SimpleRPG.Gameplay.Core
```

| Assembly | What belongs | What does **not** |
|---|---|---|
| `Foundation` | Primitives everything needs: the server clock, request throttling, the connection handshake and player sessions, input actions, `DamageInfo`, `BodyLocation` | Anything that knows about characters, items or the world |
| `Combos` | The combo machine, predictor, step timing and the `ComboDefinition` asset. No network or character types | Anything that plays an animation or reads a character |
| `AudioModel` | The data of sound: event definitions, tables, tags, contexts, profiles | Anything that plays a sound |
| `Items` | What an item *is*: `Item`, ids, affixes and their rolling, armor and weapon data, the item's audio profile | Where an item *is* (inventories, slots, the world) |
| `Combat` | What a combatant *is*: `Character`, stats, loadout, hit detection, `Weapon`, `Projectile`, the ability slot types | How it is carried, animated or controlled |
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

That is about eighteen assemblies for about 25,000 lines. It is a lot, deliberately: the owner's priority is that the structure resists dumping and the cost is a few extra `.asmdef` files.

### What the checker reports today

Running `layering_check.py --summary` against this plan finds a short list of upward references. **Six of the rows below are not real** (a type's name appears as a word in a string or comment); the rest are the work.

| Reference | Real? | The fix |
|---|---|---|
| `Foundation -> Combos: Pending`, `Foundation -> Carrying: Inventory` (in `GameInput`), `Items -> Carrying: Inventory` (a tooltip), `Items -> World: Chest` (a tooltip), `Presentation -> World: Chest`, `Items -> AudioPlayback: Instance` | No | Confirm by reading the file, then ignore. If one is real, treat it like the others |
| `AudioModel -> AudioPlayback`: `IAudioRandom`, `IntermittentSound`, `MovementLayer`, `MovementLayerPicker` | Yes | These data types are declared inside playback files. Move them into the audio model, then `MovementAudioProfile` and friends no longer reach up |
| `Items -> AudioPlayback`: `InventoryAudioAction` | Yes | The enum is declared in the playback side; it is part of the item audio profile's data. Move it down with the profile |
| `Combat -> Melee`: `MeleeDeliveryBase` (in `IAbilitySlot.cs`) | Yes | The ability slot should use `IMeleeSwingHost`, which already exists in `Abilities.Runtime`, not the concrete base class |
| `Combat -> Carrying`: `Equipment` (in `CombatLoadout`) | Yes | `CombatLoadout` needs only the weapon in the main hand. Add a tiny `IEquippedWeaponSource` in `Combat`, implemented by `Equipment`, found with `GetComponent<T>()` |
| `Combat -> Delve`: `DelveController` (in `Character`) | Yes | A respawn-policy interface in `Combat` (for example `IRespawnPolicy`), implemented by the delve and found through the network manager the way `DelveController.Find` works now. No static |
| `Carrying -> World`: `WorldItemPickup` (in `ItemDropController`) | Yes | An interface in `Carrying` for "a world form that can take an item instance" (`InitializeDropped(Item, uint)`), implemented by `WorldItemPickup` |
| `Carrying -> AudioPlayback`: `InventoryAudio` (in `Inventory`) | Yes | `Inventory` raises an item-moved event; `AudioPlayback` observes it, as `CombatAudio` already observes `MeleeHitConfirmed` |
| `Delve/World/AI -> Session`: `Player` | Yes | These only want "the character belonging to this client", or "is this character a player's". Add a small `PlayerCharacters` lookup and an `IPlayerControlled` marker in `Combat` (implemented by `FirstPersonCharacterController`, which `PlayerProximity` and `DelveController` already use as the marker). Then nothing below `Session` reaches `Player` |

Three moves the plan assumes, none of which is a code change beyond moving files:

- `Characters/Damage.cs` and `BodyLocations.cs` to `Foundation` (so `WeaponData` and the audio contexts do not reach up into `Combat`).
- `Abilities/Combo/*` to `Combos` (so `WeaponData` can hold a `ComboDefinition` without reaching up into `Melee`).
- `Delve/PartyScaling.cs` to `World` (so the dungeon does not reach up into `Delve`), and `Interactable`, `InteractableManager` and `Interactor` together into `Interaction` (they used each other across `World` and `Characters`).

### The `internal` members

After the split, anything `internal` that crosses an assembly stops compiling. Today: `Inventory.Detach`, `Attach`, `TryFindRoom`, `CommitMove`, used inside `Carrying` by `EquipmentController`, `InventoryMoveController` and `ItemDropController`, and from outside it by `DeathDrop`, `ParkedCharacter` (in `Delve`) and `WorldItemPickup` (in `World`); and `Weapon.ResolvesHits`, used inside `Combat` only. Do **not** add `InternalsVisibleTo`: it is a back door that defeats the layering. Give `Inventory` a deliberate, narrow public surface instead (take an item out and get back what it was, put it here under its old instance id) and keep the rule that an item is in exactly one place.

### Suggested order

Each step ends compiling, with tests green and Validate Definitions clean.

0. **Baseline.** Compile, run EditMode and PlayMode, run Validate Definitions, record the results. Resolve the eight package GUIDs in `SimpleRPG.asmdef`.
1. **Guard first.** An EditMode test that reads every `.asmdef` and fails on a reference the plan does not allow (an `AssemblyLayeringTests` that parses the JSON and checks it against the table above). It passes trivially at first and protects every later step.
2. **Move the plain files** that the plan relocates (`Damage`, `BodyLocations`, `Combo/*`, `PartyScaling`, the interaction trio, the loose root files). Pure moves: `git mv` the `.cs` **and** its `.meta`. Re-run `layering_check.py`.
3. **Untangle audio inside the one assembly:** move the data types out of the playback files (see the table), and move `ItemAudioProfile` and `InventoryAudioAction` beside `Item`. Make `Inventory` raise an event instead of calling `InventoryAudio`.
4. **Break the remaining core edges** (`IAbilitySlot`, `IEquippedWeaponSource`, `IRespawnPolicy`, the world-form interface, `PlayerCharacters`/`IPlayerControlled`), one at a time, each with a test. Behaviour must not change.
5. **Create the assemblies bottom-up:** Foundation, Combos, AudioModel, Items, Combat, Carrying, Melee, then the leaves in `LEAF_ORDER`. For each: move the folder with its `.meta` files, add the `.asmdef` (set `autoReferenced` to false), set references, compile, run tests. Update the test assemblies, `SimpleRPG.Editor`, `SimpleRPG.Abilities.Editor` and `Integration` as each appears.
6. **Retire the old `SimpleRPG` assembly** (or leave it as `Session`).

### Keeping it from eroding (the owner's real worry)

The layering only helps if the next agent puts new code in the right place. The cheapest, most reliable ways:

- **The layering test** from step 1 fails the build the moment something reaches the wrong way. This is the one that matters most.
- **A "where new code goes" rule in `CLAUDE.md`**, pointing at a short `Docs/CodeLayout.md`: the assembly table above, and "if you cannot say which assembly it belongs to, ask; never add to `Session` because it is convenient".
- **A short `README.md` beside each `.asmdef`** stating what belongs and what does not, taken from the table. An agent that opens the folder reads the rule.
- **Set `autoReferenced` to false** on every new assembly, so nothing is referenced implicitly.
- **Align namespaces with assemblies** as a later, separate change, so a type's namespace tells you where it lives. Not part of this work: do not mix a namespace rename with the split.

## After the layering: three classes that have grown

Do this only after the split, as separate changes. The point is separate concerns, not smaller files.

- **`Character`** (about 360 lines): health replication, life and death state, respawn point and teleport, combatant registration, region damage modifier, damage entry point. A reasonable split is plain C# collaborators owned by `Character` (a vitals holder; the respawn point and teleport) with the `NetworkVariable`s staying where Netcode needs them.
- **`Inventory`** (about 650): the grid, replication, the instance-id maps, access control (`CanBeAccessedBy`, `SharedAccessRange`), RPC endpoints (move, drop) and their throttles. The placement logic (`ValidateItemPlacement`, `TryFindFreeLocation`, `FillCells`) is pure and can be a tested `InventoryGrid` class; the access policy can be its own class.
- **`Equipment`** (about 470): replication, charges, equip/unequip RPCs, item-state sync.

**Do not add or remove `NetworkBehaviour` components on prefabs to do this** unless it is clearly worth it. Each one means editing prefabs and the scene, and component order matters to Netcode. Extract plain classes instead.

## Hazards specific to this project

- **Move `.meta` files with their scripts.** Script GUIDs live in the `.meta`; every prefab and scene refers to the GUID, not the path or the assembly. A lost `.meta` silently breaks references.
- **Do not rename types or fields casually.** Tests reach private members by name: `<ItemSize>k__BackingField` (10 uses), `<InventorySize>k__BackingField` (8), `MoveCharacterServerRPC`, `ActivateItemServerRpc`, `viewYaw`, `statProfile`, `grants`, `charges`, `entries`, `abilities`, `rules`, and others. Search the tests before renaming anything.
- **Namespaces are not assemblies.** Namespaces here are mixed (`SimpleRPG`, `SimpleRPG.InventorySystem`, `SimpleRPG.ObjectInteractions`, `SimpleRPG.AI`, ...). Leave them alone during the split; a namespace cleanup is a separate, later change.
- **Serialized `m_EditorClassIdentifier` strings** in prefabs include an assembly name (for example `SimpleRPG::SimpleRPG.Keys.DungeonKeyRing`). They go stale harmlessly when a type moves; do not rewrite them by hand.
- **`[RuntimeInitializeOnLoadMethod]` statics** reset state (`Interactable`s, `LocalPlayer`, `PlayerProximity`, `PlayerSessions`, `DelveController`, ...). They keep working when moved; do not drop them.
- **Network prefabs.** `Assets/Scenes/NetworkPrefabs-24876.asset` (used by the scene) and `Assets/DefaultNetworkPrefabs.asset` list prefabs by asset GUID; moving scripts does not affect them.
- **The `DefinitionRegistry`** is built from assets, not types, so moving a `GameplayDefinition` subclass between assemblies is safe, but run Validate Definitions to be certain.
- **Behaviour must not change.** This is a restructure. If a test changes, something was changed that should not have been.

## Still to settle with the owner

1. The public surface that replaces `Inventory`'s `internal` members: names and shape.
2. Whether `Presentation` and `PlayerControl` stay separate or are better merged (they are both about the local view of a character; the data does not force either answer).
3. Whether a separate `Session` assembly is wanted, or the bootstrap stays wherever the old `SimpleRPG` assembly ends up.

## Done means

- The agreed reference graph is enforced by an EditMode test, and has no cycles.
- `layering_check.py` reports no upward references and no leaf-order violations (apart from any confirmed name collisions).
- Compile is clean, EditMode and PlayMode are green, and Validate Definitions passes.
- No behaviour or serialized data changed: a play-through of the delve (die, extract, drop an item, reconnect) behaves exactly as before.
- `Docs/CodeLayout.md` exists (the assembly table and the rule for where new code goes), `CLAUDE.md` points at it, every assembly has its short `README.md`, `Docs/README.md` links the layout page in place of this hand-off, and `bd` shows what is left.
