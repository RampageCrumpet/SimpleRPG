# SimpleRPG.AudioPlayback

Playing sounds: the audio service, emitters, ambience, footsteps and the per-system audio controllers. Observes gameplay events (such as `Inventory.LocalItemMoved` and `MeleeHitConfirmed`) rather than being called by them.

**Does not belong here:** The data of sound (events, tables, tags, profiles). That is `AudioModel`.

Layering rules and the full table: `Docs/CodeLayout.md`. The layering is enforced by `AssemblyLayeringTests`.
