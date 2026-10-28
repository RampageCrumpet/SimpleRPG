# The local player and the UI

The character never knows about the UI, and the game is playable with the UI removed. Two small pieces keep it that way.

## Who the local player is

`LocalPlayer` (static) holds the character this machine's player controls. The owning character announces itself when it spawns and withdraws when it despawns; anything that shows or reacts to the local player observes it:

- `LocalPlayer.Current`, and the events `Ready(character)` and `Gone(character)`.
- `LocalPlayer.InMenu` and `MenuModeChanged(bool)`: true while a menu has the player's attention. The first-person controller holds the camera and movement back while it is true, and the gameplay keys below are not read.

It is cleared at startup, so nothing survives from a previous play session when domain reload is off. Tests call `LocalPlayer.Reset()`.

## Input lives on the character

`LocalPlayerControls` is on the player prefab and is the only thing that reads gameplay input, on the owner's machine only. Each frame it refreshes `Interactor.Target` and turns the keys into actions: ability keys activate slots (a melee swing needs a fresh press, other abilities repeat while held), the interact key starts or stops the interaction with `Interactor.Target`, and a dead player's interact key asks for the next life. While `LocalPlayer.InMenu` is true the ability and interact keys do nothing; the respawn request still works, because the death screen is a menu.

`LocalPlayerControls.Apply(ControlInput)` does the acting, so a test or another input source can drive the character without a keyboard.

## The UI observes

`PlayerUIController` is the root of the HUD. When the local player is announced, or already was when the UI came up, it binds every `ILocalPlayerView` beneath it, including views on inactive objects, and unbinds them when the player goes. To add a piece of HUD, implement `ILocalPlayerView` under that root; nothing on the character changes.

Views only show state. `InteractionUI` reads `Interactor.Target`, `AbilityButton` shows a slot's cooldown, `DeathScreenUI` shows the panel and offers a Respawn button. Menu keys that only change what the UI shows (Inventory, Cancel) stay in the UI.

Containers do not look for the loot menu either: `LootContainer` raises `OpenRequested` and `CloseRequested`, `LootMenuUI` listens while it is bound, and a dedicated server with no UI simply has no listener.
