# SimpleRPG.Combos

Combo timing and data: the combo machine, predictor, step timing and the `ComboDefinition` asset. A step names its swing sound, so this sits above `AudioModel`.

**Does not belong here:** anything that plays an animation, reads a character, or touches the network.

Layering rules and the full table: `Docs/CodeLayout.md`. The layering is enforced by `AssemblyLayeringTests`.
