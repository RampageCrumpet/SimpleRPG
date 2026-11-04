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
- Every outcome is an `EquipResult`: `Success`, or why it was refused (`NotArmor`, `ItemNotInInventory`, `ItemNotReplicable` for an item missing from the `DefinitionRegistry`, `SlotEmpty`, `NoRoomInInventory`, ...). A refusal changes nothing.
- Equip and unequip are server operations. `Equipment.RequestEquip` / `RequestUnequip` run the controller on the server and send an `[Rpc]` from the owning client; the server checks that the sender owns the character and that it is alive.
- Equipment state is correct with no UI open or even created. Nothing in `Equipment`, `ArmorData` or `EquipmentController` refers to the UI.

## Using it in the game

- Right-click armor in your inventory to wear it, or drag it onto the paper doll and let go anywhere over the doll; it goes in the slot it declares. Right-click or left-click a worn slot on the paper doll to take it off.
- `RegisterWithUI` binds the paper doll for the owning client through `LootMenuUI.BindPlayer`. See [Inventory screens](InventoryScreens.md) for where it sits.
- To make armor: create an **Items > ArmorData** asset, set its slot, icon and size. Items are gameplay definitions, so the asset is given a permanent id when it is created; run **SimpleRPG > Definitions > Rebuild Definition Registry** to register it and give it a network id (never reused).

## Observing equipment

Anything that needs to react to what is worn should subscribe to `Equipment.Changed` (every machine, no slot detail: re-read `Get(slot)`) or, on the server, to the gameplay `ItemEquippedEvent` / `ItemUnequippedEvent` (slot, item instance, reason). The paper doll is one such observer.

## Armor items and how they look

An armor piece is two things, made together by one tool and kept apart in the game:

- **The item** (`ArmorData`, in `Assets/Items/Armor`): its slot, size, icon, and what it does, which is a **grant of Armor Rating** (`simplerpg.stat.armor_rating`, a flat amount). Nothing else about protection lives anywhere else: affixes on the piece add to or scale that same stat, and Armor Rating becomes Physical Damage Reduction through the player profile (see [Stats](Stats.md)).
- **The look**: one `BodyPartDefinition` per skinned mesh in the piece's art prefab, each marked as **the look of that item** (`dressesItem`). A pair of gloves has two (left and right); a helmet or chest piece has one.

`EquipmentAppearance` (a server-side component on the player prefab) joins them. It watches the character's `Equipment` and, for each worn piece, finds its parts and puts them on the character's `CharacterAppearance` in place of the bare parts in the same slots; take the armor off and the bare parts return. Clients do nothing: appearance already replicates, so every machine and every late joiner shows what the server decided. A character that spawns already wearing armor (a returning player) is dressed at once. Helmets go in the separate **Headgear** body slot, so they sit over the bare head and the face stays. The owner's first-person view already hides the head and headgear from the inside, so a helmet is hidden from the owner and seen by everyone else; gloves and sleeves show.

```
Equipment (replicated slot -> item)
        |
        +----> PaperDollUI
        |
        +----> EquipmentAppearance (server) -> CharacterAppearance (replicated parts) -> every machine
```

There is no lookup table to keep in step: `ArmorParts` finds an item's parts by asking the definition registry which parts dress it, so making the parts *is* authoring the look.

### Making armor

1. Author the piece in the Blender authoring scene on the **canonical rig** (52 bones, A-pose; its names are listed in `Authoring/GameRig_Bones.json` beside the scene). Add it to `PIECES` in `Authoring/export_armor.py` and run `export_piece("<Name>")`. The script checks the rig and the vertex weights, refuses a piece the game could not wear, and writes `Assets/Art/3d Models/Items/<Name>/<Name>.fbx`; the folder also needs `<Name>.prefab` (a variant of the FBX) and `<Name>_Icon.png`. Textures go through `Authoring/pack_item_textures.py` (BaseColor and Normal copied, Metallic and Roughness packed into `MetallicSmoothness`).
2. Add the piece to the seed in `ArmorSetup.Pieces` (name, slot, grid size, starting Armor Rating).
3. Run **SimpleRPG > Items > Build Armor Items And Parts**. It creates the item and its parts, sets the icon up as a sprite, and rebuilds the definition registry. It shows what it could not do (below).
4. Tune the item **in its asset** from then on. The tool never changes an item that exists; it only updates parts from the art, so ids and references survive a re-export.

**One skeleton.** Armor is skinned to exactly the bones of the body rig and no others, so wearing it never needs a fix-up: the tool reports a piece skinned to a bone the body rig lacks, and `ArmorSetupTests` fails if a shipped part is. The animation rigs in the authoring scene also carry twist and `MCH_` helper bones; those are authoring-only and are not part of the game rig, so armor is never skinned to them. If the game rig gains bones, add them to the body first and to `GameRig_Bones.json` after.

The starting numbers are a seed, not a design decision:

| Slot | Piece | Armor Rating | Grid size |
|---|---|---|---|
| Head | Padded Coif / Kettle Helm / Visored Sallet | 5 / 12 / 18 | 2 by 2 |
| Chest | Linen Tunic / Gambeson / Mail Hauberk / Plate Armor | 2 / 10 / 28 / 45 | 2 wide, 3 tall |
| Hands | Leather Gloves / Reinforced Gloves / Plate Gauntlets | 3 / 6 / 12 | 2 by 2 |

Grid sizes follow the standard of the games this one follows: 2 by 2 for head, hands and feet, 2 wide and 3 tall for body armor (legs are not settled). The ratings are placeholders; everything here will be rebalanced after playtesting.

With the player profile's starting curve (see Stats), a full set of the heaviest pieces so far (45 + 18 + 12 = 75) gives about 39 percent physical reduction from armor, plus Constitution's 8 percent.

### What it cannot do yet

- **Rigid helmets are combined.** A head piece whose prefab has no skinned mesh, such as the **Visored Sallet** (a shell and a visor on a pivot), is built from every plain mesh in its prefab, combined into one mesh and worn rigidly on the head bone (appearance holds one part per slot, so a shell and a visor have to be one part). The visor is as the prefab has it, which is closed; it does not move. The prefab is placed on the head the way the eyebrows are, assuming it shares the body model's space; if it sits wrong, adjust the part's attach position and rotation in its asset. Rigid pieces for other slots are not supported yet and are reported.
- **Hands and feet name their side.** A skinned mesh for gloves or boots must end in `_L` or `_R` (or contain Left or Right); otherwise the tool says so and skips it.
- **Legs and feet** have no art yet; add them to the seed when they do.
- **No world form.** The items have no `WorldPrefab`, so they cannot be dropped on the ground or placed as floor loot (`DungeonPopulation`). They can be found in chests and loot tables, which add item instances straight to an inventory.
- **No equip sound** (`ItemAudioProfile`) and no affix pools yet.
- **Wrist fit** between chest items and gloves is the art's job, not the game's: see `Docs/AssetCreationWorkflow.md`.

## Known limits

- Armor protects only through Armor Rating (see [Stats](Stats.md)). No durability (by design), requirements, weight or set bonuses. Rolled bonuses exist: see [Item affixes](Affixes.md). `Item.Grants` and `Item.Rules` already apply while an item is worn, as for any equipped item.
- There is no item tooltip anywhere in the UI yet, so the paper doll has none either.
- The `DefinitionRegistry` must list every wearable item, or `Equipment` refuses it (clients could not look it up). Validate Definitions fails the build if it does not.
