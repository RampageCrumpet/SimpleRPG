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

## What depends on what

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

## The shape this suggests

The pattern in the data is not a ring of equal partners. There is a **tightly bound core**: a character carries items, wields weapons, takes hits and runs abilities, so `Characters`, `Inventory`, `Equipment`, the melee code in `Abilities`, and the combat types loose in the root (`Weapon`, `WeaponData`, `Projectile`, `SwingHitTracker`) are one concept and will always know each other. Forcing them apart would add interfaces for their own sake. Around that core sit **leaves** that should depend on it and never the reverse: audio, world and level, delve rules, UI, loot tables, and the session bootstrap.

Recommended layering (bottom depends on nothing above it). **Decide this with the owner before moving anything:**

```
Leaves        Audio     World/Level     Delve     UI     Session (Player, GameController, EnemySpawner, NetworkSpawn)
                 \          |            |        /               |
Core gameplay    Characters + Inventory/Equipment + melee Abilities + combat root types (Weapon, WeaponData, Projectile, hits)
                                   |
Foundation       ServerClock, Networking primitives (RequestThrottle, PlayerSessions, SessionHandshake), GameInput
                                   |
Existing         SimpleRPG.Abilities.Runtime  ->  SimpleRPG.Gameplay.Core
```

`SimpleRPG.Abilities.Integration` and the editor assemblies stay above all of it. A finer split of the core (for example separating `Items` from `Characters`) is possible later; it is deliberately not proposed first, because it needs the most seams and buys the least.

### What has to change for that to compile

The core-to-leaf edges are the ones to break. Each of these is a small, local change:

1. **`Characters -> Audio`** (`GameAudio`, `AudioEventBase`). The character should raise events and the audio side should subscribe (as `CombatAudio` already does with `MeleeHitConfirmed`). Do the same for `Inventory -> Audio`: `Inventory.PlayMoveSound` calls `InventoryAudio.Play`; make `Inventory` raise an item-moved event and let `Audio` observe it. The audio *data* the item holds (`ItemAudioProfile`, `MovementAudioProfile`, `AudioTag`) is the harder half: either those three types move down with `Item`, or `Item` holds them behind a small interface. Decide with the owner; moving the data down is simpler.
2. **`Characters -> World`** (`Interactor` uses `Interactable`). `Interactor` is the player's interaction component. It most naturally lives with `Interactable` in the world leaf; check what in `Characters` and `UI` then reaches it.
3. **`Characters -> Delve`**. Replace the static lookup in `Character.RequestRespawn` / `RespawnServerRpc` with a small policy interface in the core (for example `IRespawnPolicy`) that the delve implements, found by the character through its network manager the way `DelveController.Find` works today. No static.
4. **`Inventory -> World`**. `ItemDropController` needs a world form that can take an item instance. Put a tiny interface for it in the core (`InitializeDropped(Item, uint)`) and have `WorldItemPickup` implement it; the controller finds it with `GetComponent<T>()`.
5. **The loose root files** go to their proper homes. `ServerClock` to Foundation; `Weapon`, `WeaponData`, `Projectile`, `SwingHitTracker` next to `Hitbox`/`HurtBox` in the core; `Player`, `GameController`, `EnemySpawner`, `NetworkSpawn` to a Session area at the top. `WeaponData` is an `Item` subclass, so it sits with `Item`.
6. **`Controls` (`GameInput`) and `Networking`** are small and used from everywhere; they belong in Foundation. `Controls -> Inventory(1)` and `Networking -> Abilities(1)` need looking at (the second is probably the `Pending` false positive).
7. **`internal` members.** After a split, anything `internal` that crosses an assembly stops compiling. Today: `Inventory.Detach`, `Attach`, `TryFindRoom`, `CommitMove` (used by `EquipmentController`, `InventoryMoveController`, `ItemDropController`, and, from the delve, `DeathDrop`, `ParkedCharacter`, `WorldItemPickup`), and `Weapon.ResolvesHits`. Do **not** add `InternalsVisibleTo`; that is a back door. Give these a deliberate public surface instead, a narrow "take this item out / put it here under its old instance id" API with a name that says what it is, and keep the rule that an item is in exactly one place.

### Suggested order

Each step ends compiling, with tests green and Validate Definitions clean.

0. **Baseline.** Compile, run EditMode + PlayMode, run Validate Definitions, record the results. Resolve the eight package GUIDs.
1. **Guard first.** Add an EditMode test that reads every `.asmdef` and fails if a reference points the wrong way (the table above, once agreed). It will fail on nothing now and protects every later step. (An `AssemblyLayeringTests` that parses the JSON is enough.)
2. **Break the core-to-leaf edges** (1 to 4 above), one at a time, all still in the single assembly. This is ordinary refactoring and each is testable on its own. Re-run `folder_dependencies.py` after each; the two-way pairs should disappear one by one.
3. **Move the loose root files** to their areas (a pure move: `git mv` the `.cs` **and** its `.meta`, so script GUIDs, and with them every prefab and scene reference, are untouched).
4. **Create assemblies bottom-up:** Foundation, then core gameplay, then each leaf. Move folders with their `.meta` files, add the `.asmdef`, set references, compile, test. Update the test assemblies' references and `SimpleRPG.Editor`, `SimpleRPG.Abilities.Editor` and `Integration` as each appears.
5. **Retire `SimpleRPG`** (or leave it as the Session assembly if that is where the bootstrap ends up).

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

## Decisions to settle with the owner before step 2

1. Is "one core plus leaves" the right shape, or do they want a finer split of the core?
2. For `Item`'s audio references: move the three audio data types down with `Item`, or hide them behind an interface?
3. Where does `Interactor` belong (the world leaf, or stay in the core)?
4. The public surface that replaces `Inventory`'s `internal` members (names and shape).
5. Is a separate Session assembly wanted, or should the bootstrap stay in whatever `SimpleRPG` becomes?

## Done means

- The agreed reference graph is enforced by an EditMode test, and has no cycles.
- `folder_dependencies.py` shows no core-to-leaf edges.
- Compile is clean, EditMode and PlayMode are green, and Validate Definitions passes.
- No behaviour or serialized data changed: a play-through of the delve (die, extract, drop an item, reconnect) behaves exactly as before.
- `Docs/README.md` links this document's outcome (a short "Code layout" page replacing this hand-off), and `bd` shows what is left.
