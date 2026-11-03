# SimpleRPG.Foundation

The primitives everything else needs: the server clock, request throttling, the connection handshake and player sessions, input actions, `DamageInfo`, `BodyLocation`.

**Does not belong here:** anything that knows about characters, items or the world.

Layering rules and the full table: `Docs/CodeLayout.md`. The layering is enforced by `AssemblyLayeringTests`.
