# The delve: dying, extracting, and what happens after

A **delve** is one trip into the dungeon, from arriving to getting out or dying. Players group up, go in, and either leave together with
what they found or lose everything. There is no levelling; gear is the only thing that carries over, and that lives outside the dungeon.

`DelveController` (`Assets/Scripts/Delve/`) owns the rules. It sits on the `Level` child of the `GameManager` prefab, next to the key ring,
and follows the rule in [Network authority](NetworkAuthority.md): the server decides, clients read replicated state.

## Decisions

| Question | Decision |
|---|---|
| What does dying cost? | **Everything.** The inventory and all worn gear move into a loot bag where the player fell. |
| Who can take it? | Anyone alive within reach, like a chest. A partner can recover it; an enemy cannot. |
| Can a dead player respawn mid-delve? | No. They wait for the delve to end. |
| How does the party get out? | **Together.** Every living player must stand in the extraction zone for the whole channel (10 s). If anyone steps out or dies the countdown starts over. |
| When does a delve end? | When the party extracts, or when every player is dead (a wipe). |
| Where does the outcome go? | To an `IDelveRewardSink`. See below. |
| A player's connection drops? | Their character is **kept for them**, not killed. See "Players who drop out". |

## Pieces

| Piece | What it is |
|---|---|
| `DelveRules`, `ExtractionChannel` | Plain code, no engine types. The end conditions and the countdown, tested in `DelveRulesTests`. |
| `DeathDrop` | Moves the same items, with the same instance IDs, from an inventory and equipment into a bag. Nothing is copied. An item the bag has no room for stays with its owner and is reported; the bag prefab is sized to hold a full inventory and all worn gear. |
| `LootBag` | A `LootContainer` that despawns once it has held items and been emptied. Prefab: `Prefabs/Interactables/LootBag.prefab`, registered in the network prefab list. |
| `DelveController` | Finds the players (4 times a second, not every frame), drops the belongings of the dead, runs the channel, decides the phase, and gates respawn through `DelveController.AllowsRespawn`. Without one in the session nothing changes. |
| `DungeonPopulation.ExtractionPosition` | Chosen after enemies and treasure so existing seeds place them identically: the furthest reachable spot from the arrival among the last few rooms. |

The drop runs on the poll after the death, not inside the damage event that caused it.

## Players who drop out

A dropped connection is not a death. Losing a hard-won equipment set to an internet hiccup would be a harsh penalty.

- **Identity.** The connection handshake carries a stable player id (`PlayerIdentity`). A client id changes on every connection; the player id does not, so the server can tell a returning player from a new one. **The id is a stand-in**: one the client makes up proves nothing, so official servers must replace it with an identity from the login service. Two clients on one machine would share an id; start one with `-playerId <other-id>`.
- **Away.** When a client disconnects, the server captures its character (items with their instance IDs and places, worn gear, health, position) as a `ParkedCharacter` and despawns it. It cannot be hurt, and it cannot be used to dodge a death.
- **Back.** When the same player id connects again, `Player` makes a fresh character where the old one stood, with everything restored. A new character goes through the normal spawn path, so the owner's camera and controls are set up the usual way. A returning player whose character was already dead comes back dead.
- **Waiting.** While anyone is away the party cannot extract and cannot be wiped: someone may still come back.
- **Too long.** After `reconnectGraceSeconds` (default 180; 0 waits for ever) the player is treated as dead: their belongings go into a loot bag where they stood.
- **Restarting.** A player reconnecting before the server noticed the old connection is gone is handled: the old character is put aside first.

What this does **not** do: leave a visible body in the world. The character is out of the world while the player is away. A body that stays behind, and can be seen, needs ownership of an already-spawned character to move to the returning client, and the client-side setup (camera, controls, HUD) has to be redone when that happens. That needs testing against a real second client.

## Weapons and walls

A blade cannot reach through a wall. Each physics step a swing's `Hitbox` checks a line from the wielder's body to the blade's far end against the level's solid geometry (`obstructionLayer`, default `World`). If the line is blocked the blade has met a wall: the hitbox reports it once, finds nothing more that swing, and the weapon deals no more damage until the next swing.
`MeleeDeliveryBase.MeleeBounced` tells every machine where the wall was struck and which way it faces, for sound, sparks and a recoil.

Walls, doors and pillars belong on the `World` layer; floors stay on `Walkable`, so a low chop is not stopped by the ground. Projectiles and weapons nobody carries are not checked.

### What a bounce does

1. **The swing ends.** The server releases the swing exactly as a death mid-swing does: the ability is told it completed, the hit window closes, the combo chain stops. No more damage is dealt that swing.
2. **The wielder is thrown off balance.** For `bounceStaggerSeconds` (0.6 s on `ComboMeleeDelivery`) the server refuses a new swing, and the owning client does not predict one, so it never shows a swing the server will refuse. Enemies recover for `bounceRecoverySeconds` (1 s on `MeleeDelivery`) instead of the rest of the attack's length.
3. **Everyone sees it stop.** Every machine cancels the attack animation: the attack layer fades back to the ready stance. The owning client also drops its guess about the combo.
4. **Presentation hooks.** `MeleeBounced` carries the wall point and surface normal, for a sound, sparks or a decal. Nothing listens yet.

The stop is abrupt: there is no recoil clip, only the existing fade to the ready stance. A recoil animation would be played from the same place, `OnBounceShown`, and would need to be a short clip that starts from any point in a swing (or one per swing direction). Until it exists the bounce reads as the swing being stopped dead.

## The reward sink

Official servers will keep characters and the stash in a SQL database; a local game keeps its own, entirely separate, storage (the
Diablo II Battle.net versus single-player split). The dungeon must not know which it is talking to, so a finished delve is handed to
`IDelveRewardSink.Deposit(DelveResult)` with the survivors' and the dead's client IDs. The default only logs.
Set `DelveController.RewardSink` to plug a real store in.

What a store must know: at the moment of `Deposit`, what a survivor carries is theirs; what the dead carried is in the loot bags and is no
longer theirs. Loading a character into a delve is the opposite direction and is not built yet.

## Not built yet

- The **HUD** (`DelveHud`, in the `UI` assembly, created by code) is a plain on-screen placeholder, and the extraction point is a flat green disc made the same way. Both need real art and UI. When a delve ends the HUD takes the cursor through `PlayerUIController`'s reference-counted lock, as the death screen does, and offers **Exit dungeon**: `MainMenuReturn` shuts the session down, clears what it left behind (`LocalPlayer`, the player sessions, the gameplay services), frees the cursor and reloads the scene, which brings the menu back. A client that loses its connection, or is refused on joining, returns to the menu the same way.
- `StartNextDelve` (no longer offered in the UI; leaving is) resets the rules and brings everyone back to the spawn; it does **not** rebuild the dungeon. A new dungeon per delve needs
  a decision about scene or session handling and testing in the editor.
- A fixed party size, joining a delve in progress, and disconnect handling beyond "a player who leaves is no longer counted".
- The loot bag has no time limit and no owner-only window.
