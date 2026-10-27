# HUD and inventory screens

Three layouts, drawn from the 1920x1080 wireframes: the game view HUD, the opened inventory and the opened interactable. The wireframes are layout specifications; the art is placeholder.

## Canvas

`PlayerUI` and its nested loot canvas use Scale With Screen Size at 1920x1080 in Expand mode. The inventory screens live in a fixed 1920x1080 `Frame` centred on the canvas, so the composition is the same at any aspect and never leaves the screen. The HUD anchors to the bottom centre.

## Game view

- `HealthBarUI` (existing art and shader) sits bottom centre and shows current / maximum as text.
- `BuffBarUI` sits above it. A slot exists only while its effect is active; with none the strip is hidden. It reads an `IBuffSource` on the character. `AbilityUser` implements it from the server's `StatusManager`, so a **remote client's bar is empty** until statuses are replicated (bead).

## Inventory screens

`LootMenuUI` owns the open state (`ScreenMode`) and arranges one set of panels:

| Mode | Left | Right |
|---|---|---|
| `Inventory` | `StatSheetUI` | the player's panel (paper doll with `CharacterPreviewUI`, inventory) |
| `Interactable` | the player's panel | the object's inventory, with a compact paper doll only if the object has `Equipment` |

The player's panel is the same objects in both modes, moved between two rectangles, so inventory and equipment state are untouched by switching. Opening and closing never touches storage.

- **Cells.** Every `InventoryUI` draws 64-unit cells (`InventoryUI.ReferenceCellSize`); paper doll slots are the same size. Drag and drop and moves go through `Inventory.RequestMoveItem`, unchanged.
- **Inventory sizes.** The player is 12x4 and chests 12x7 so the grids fit both layouts at that cell size.
- **Paper doll.** `PaperDollUI` (`Full` or `Compact` layout) shows the five armor slots only. Nothing is bound to rings, belt, ranged or consumable slots, which do not exist.
- **Character preview.** `CharacterPreviewUI` renders a copy of the bare rig, wearing the character's current appearance parts and playing its idle animator, into a render texture, from a stage far below the world. It draws only while the screen is open. Armor is not drawn yet: armor does not feed the appearance system (see the fitted armor visuals bead). Sharing the character rendering with the world is its own bead.
- **Stat sheet.** `StatSheetUI` lists health, the six core attributes and the derived stats the character's profile defines. Core and derived values are the server's `AttributeSet`, so a remote client shows health only until they are replicated (bead). Name, level, class, mana, stamina and carry weight do not exist and are not shown.

## Rebuilding the layout

**SimpleRPG > UI > Build HUD And Inventory Screens** (`InventoryScreenBuilder`) rebuilds the screens, HUD additions and scalers in the open scene. Change the layout there and rebuild; do not hand-edit what it generates.
