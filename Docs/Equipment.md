# Equipment and the paper doll

Armor is worn on five slots: head, chest, hands, legs, feet. This page covers who owns that state, how armor moves between the inventory and the slots, how the paper doll shows it, and where fitted armor visuals will plug in later. Weapons, the neck slot and item abilities use the same `Equipment` component but are not part of the paper doll.

## Pieces

| Type | Job |
|---|---|
| `ArmorData` | An `Item` that declares the one armor slot it fits (`Slot`). An item that is not `ArmorData`, or declares a non-armor slot, cannot be worn through the inventory. |
| `Equipment` | The `NetworkBehaviour` on the character: what is in each `EquipmentSlot`, replicated to every client, with `Get(slot)`, `Equipped` and the `Changed` event. It also raises gameplay `ItemEquippedEvent` / `ItemUnequippedEvent` on the server, which grants and rules follow. |
| `EquipmentController` | The rules for moving armor between an `Inventory` and the slots. Validates the whole request first, then commits, so a refused request changes and raises nothing. |
| `PaperDollUI` / `EquipmentSlotView` | The five armor squares, drawn around the character preview on the inventory screens. They read `Equipment` and forward clicks. They hold no equipment state. |

## Rules

- An item is in the inventory or in a slot, never both and never neither. It keeps the same instance ID as it moves, so charges and other per-item state follow it.
- Equipping into an occupied slot returns the previous item to the inventory. The previous item may use the room the new item leaves behind.
- Every outcome is an `EquipResult`: `Success`, or why it was refused (`NotArmor`, `ItemNotInInventory`, `ItemNotReplicable` for an item missing from the `ItemRegistry`, `SlotEmpty`, `NoRoomInInventory`, ...). A refusal changes nothing.
- Equip and unequip are server operations. `Equipment.RequestEquip` / `RequestUnequip` run the controller on the server and send an `[Rpc]` from the owning client; the server checks that the sender owns the character and that it is alive.
- Equipment state is correct with no UI open or even created. Nothing in `Equipment`, `ArmorData` or `EquipmentController` refers to the UI.

## Using it in the game

- Right-click armor in your inventory to wear it. Right-click or left-click a worn slot on the paper doll to take it off. Dragging is not used for equipping.
- `RegisterWithUI` binds the paper doll for the owning client through `LootMenuUI.BindPlayer`. See [Inventory screens](InventoryScreens.md) for where it sits.
- To make armor: create an **Items > ArmorData** asset, set its slot, icon and size, and append it to the `ItemRegistry` (append only; the position is its network id).

## Observing equipment

Anything that needs to react to what is worn should subscribe to `Equipment.Changed` (every machine, no slot detail: re-read `Get(slot)`) or, on the server, to the gameplay `ItemEquippedEvent` / `ItemUnequippedEvent` (slot, item instance, reason). The paper doll is one such observer.

## Future: fitted armor visuals

Not implemented; there are no armor models yet. The intended flow:

```
EquipmentController / Equipment.Equip
        |
        v
Equipment (replicated slot -> item)
        |
        +----> PaperDollUI
        |
        +----> character presentation (future)
```

A presentation component on the character will observe `Equipment.Changed` and, for each armor slot, resolve the worn `ArmorData` to a `BodyPartDefinition` and show it through the existing appearance system (`CharacterAppearance`, see [Character appearance](CharacterAppearance.md)), hiding the body region the armor covers. The mapping from armor to part (a reference on `ArmorData`, or a lookup table) is a decision for that task, once real meshes exist to fit; nothing is stubbed for it now. Tracked in the follow-up bead for fitted armor visuals.

## Known limits

- No armor statistics, durability, requirements, weight or set bonuses. `Item.Grants` and `Item.Rules` already apply while an item is worn, as for any equipped item.
- There is no item tooltip anywhere in the UI yet, so the paper doll has none either.
- The `ItemRegistry` must list every wearable item, or `Equipment` refuses it (clients could not look it up).
