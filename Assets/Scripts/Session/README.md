# SimpleRPG.Session

Bootstrapping the game: the player object, the game controller, the enemy spawner.

**Does not belong here:** Anything another assembly needs. If something here is used elsewhere it does not belong here; never add to Session because it is convenient.

Layering rules and the full table: `Docs/CodeLayout.md`. The layering is enforced by `AssemblyLayeringTests`.
