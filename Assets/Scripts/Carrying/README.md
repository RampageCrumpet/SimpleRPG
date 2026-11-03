# SimpleRPG.Carrying

Holding and wearing items: `Inventory`, `Equipment` and the rules that move, equip and drop items. An item is in exactly one place; `Inventory.TryTakeOut` hands back what an item was and `PutBack` places it again under its old instance id.

**Does not belong here:** the world form of an item, or any UI.

Layering rules and the full table: `Docs/CodeLayout.md`. The layering is enforced by `AssemblyLayeringTests`.
