# Network authority model

One rule: **the server owns the world; clients ask, the server decides, state flows back through Netcode.**
A host is a server that also has a local player, so it follows the server rules.

| State | Owner | How clients get it | How clients change it |
|---|---|---|---|
| Dungeon layout | Server picks `LevelGenerationParameters` (random seed + room count) | Server generates, then sends the parameters to connected clients (`GenerateLevelRpc`). A late joiner asks (`RequestLevelGenerationRpc`) and the server replies to that client only. Geometry is rebuilt locally from the seed. | Never |
| Chests and other room `NetworkObject`s | Server spawns them (`RoomNetworkObjects.Apply`) | Netcode spawn message (late joiners get existing ones on connect). Clients **destroy** their locally instantiated copy of any nested `NetworkObject` so there are no duplicates. | Never spawn; use the inventory request |
| Navmesh | Server only (`GameController`) | Not sent. AI runs only on the server. | n/a |
| Health | Server (`Character.networkHealth`, a `NetworkVariable<int>`) | Replicated, so late joiners see current health. `OnTakeDamage` fires on every machine when it drops. | `Character.TakeDamage` is server-only; hit detection already runs on the server |
| Inventory and chest contents | Server (`Inventory.entries`, a `NetworkList<InventoryEntry>`) | Replicated list of (instance id, item registry id, cell). Clients rebuild their grid from it, including on spawn for late joiners. `Inventory.Changed` tells views to redraw. | `Inventory.RequestMoveItem` -> `MoveItemRpc`. The server checks the item is in the source, the placement is valid, and the sender owns the inventory or (for chests, `SharedAccessRange`) has a character in range. A rejected move changes nothing. |
| Ability use, cooldowns, item charges | Server (`AbilityUser` with a tick-based `CooldownTracker`; item charges in `ItemStateStore`) | Cooldowns: not sent, the client's own display is a prediction. Item charges: `Equipment.charges`, a server-written `NetworkList<ChargeEntry>` readable only by the owner. | `AbilityUser.RequestActivate` and `RequestActivateItem` send an ability short id, or an equipment slot and ability position, and nothing else. The server checks life, ownership of the ability, cooldown (3 ticks of jitter tolerance), eligibility and charges. A refusal spends nothing. |
| Player life: dead or alive, respawn | Server (`Character.networkIsDead`, committed by `CombatResolver`) | Replicated `NetworkVariable<bool>`; `Character.OnDeath` / `OnRevive` fire on every machine when it flips. | `Character.RequestRespawn` (owner only, an `[Rpc]` to the server) asks for the next life; the server revives only a dead character, at its spawn point with full health. Movement, turning, abilities, melee, interactions, item moves and key use all check `IsAlive` on the server, so a dead player's requests do nothing whatever the client sends. The client UI also stops offering them. |

## Rules for new code

- A `NetworkObject` nested in a room prefab is spawned by the server and discarded on clients. Don't call `Spawn()` from `Start()`.
- Mutate replicated state only where `IsServer` is true. The `Inventory` Add/Remove methods log an error on clients.
- Gameplay an action needs the actor alive for is gated on `Character.IsAlive`, on the server, at the entry point that acts (RPC handler or server method). Client-side checks are courtesy only.
- Anything a client wants to change is a `[Rpc(SendTo.Server)]` request that validates before acting. Use `RequireOwnership = true` when only the owner may call it.
- Item identity on the wire is the `ItemRegistry` id (position in the registry list, **append only**). New `Item` assets need `Items > Update Item Registry` run in the editor. Items are never serialized themselves.
- Ability, status, rule and attribute definitions are `GameplayDefinition` assets with permanent ids in the `DefinitionRegistry`; the wire carries short ids only. Runtime state lives in managers and stores, never in the assets.

## Extending

- Level generation inputs go in `LevelGenerationParameters` so late-join stays a single value. Both sides must also have the same `Roomset` asset.
- `LevelSpawner.RoomInstantiated` is the hook where the network rule is applied to a new room. Layout generation (`LayoutGenerator`) is Unity-free and runs identically on every machine from the seed; only the spawner creates objects.
- Server-spawned dungeon content should be keyed off the completed layout (room and connection ids), not off scene positions, so multiple floors and connection types (stairs, gates) can be added.
