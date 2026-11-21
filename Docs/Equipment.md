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
| Legs | Linen Breeches | 2 | 2 wide, 3 tall |

The breeches use one skinned renderer, one submesh and one shared masked material. Atlas RGBA selects linen, thread, lower-leg wraps and cords; their colors remain independent without extra material slots. `CordMotionProfile` supplies an item-local skin with 22 accessory bones: three for each waist tail and two for each of the eight short leg tails. `CordBoneMotion` uses critically damped bone springs driven by smoothed attachment motion after animation. Segment lengths stay fixed; inward pose corrections do not feed back as velocity, and teleports reset the springs. The canonical 52-bone body and exported FBX rig stay unchanged. `BodyPartBinder` creates the accessory bones when equipped; they are children of the item and disappear with it. No Unity Cloth or physics collider components are used.

`AppearanceView` hides the waist string beneath chest armor in game and inventory previews by switching to a prebuilt index-buffer variant that omits its triangles. This retains one renderer, one submesh and the same material. Hidden waist chains stop simulating.

Use **SimpleRPG > Items > Build Breeches Bone Motion** after exporting the consolidated mesh and atlas. Authoring scripts `consolidate_breeches.py` and `bake_unified_atlas.py` are under `I:/Art/3d Models/Breeches/Authoring`. Editable source pieces remain hidden in the canonical Blender scene; `BreechesGame` is the visible consolidated model. **SimpleRPG > Items > Open Breeches Motion Review** opens the saved review scene; press Play for the sway and leg-motion loop. The review command replaces the active scene, so save other scene edits first. Full running, deep hip/knee flexion and future tall-boot combinations still need fit review.

### Mail hauberk skirt: native Unity Cloth

The hauberk's lower skirt is simulated with Unity's built-in `Cloth` component and nothing custom: no solver, no accessory bones, no proxy meshes. It is cosmetic and client-side. **The hauberk is one mesh, `GAME_MailHauberk`, with one Cloth: the torso and sleeves are pinned (Max Distance 0) and only the skirt moves.** A part's piece may reference a `ClothProfile`; `BodyPartBinder` then adds a `ClothGarment` to that piece's skinned renderer, which adds the `Cloth` and the thigh colliders. Nothing is built when the view has `SimulateCloth` off: a dedicated server (`CharacterAppearance` turns it off when it is a server with no client), a machine with no graphics device, edit mode, and the paper doll's still preview, which all show the same garment as plain skinned mesh.

- **The mesh rules (these are what stop the shimmer).** Cloth keeps one particle, and one normal, per vertex position. So the game mesh must have no coincident back-to-back faces and no custom normals, and every face is smooth. Two opposite-facing faces on the same positions gave those particles normals that flipped by 20 to 80 degrees a frame (measured: 360 of the old skirt's 768 particles, 9.8 degrees average over all of them, against 0.03 on the rest), which on a metallic normal-mapped surface was constant shimmer at the hem. `MailHauberk_Work` in the authoring scene keeps its coincident back faces and custom normals; `GAME_MailHauberk` is cut from it with every coincident back face removed (the one facing away from the body is dropped), custom normals cleared and all faces smooth, and rebuilt that way after any change to the work mesh. The back of the mail is drawn by the material (`MailHauberk.mat` has Render Face: Both), not by duplicated faces.
- **Profile.** `MailHauberk_ClothProfile` belongs to the mesh and holds the component settings, the thigh colliders and the baked per-particle Max Distance: 0 at and above z 0.78, a smoothstep to 5 cm at z 0.65, 5 cm below. Settings: stretching 0.84, bending 0.10, damping 0, friction 0.5, world velocity scale 0.3, world acceleration scale 0.3, solver 120 Hz, tethers and gravity on. All of it is set by `ClothProfileBaker`; the values were tuned by eye in the sway demo (`Assets/ClothFitTest`, disposable).
- **Colliders.** One tapered sphere pair per thigh on the wearer's own `UpperLeg` bone, centres in the bone's own space: 139.5 mm at the buttock and hip, 103 mm at mid-thigh. They are fitted to the animated breeches (the smallest pair holding every breeches vertex between z 0.60 and 0.96 over idle, walk, run, stop and lunge, with 3 mm to spare). They sit on a child object of the bone, on the cloth-collision layer, which collides with nothing in gameplay physics, so hit and interaction queries and other characters never touch them. They are made from the rig the garment is bound to, so they always belong to the wearer. `BodyPartInstance` owns them and destroys exactly those when the part is removed.
- **Particle order is baked, not assumed.** Cloth welds vertices that share a position, and the coefficients are indexed by Cloth's own particle order. **SimpleRPG > Items > Bake Hauberk Cloth Profile** computes them from the mesh, checks that Unity's own particle count agrees with the welding it assumed, and stores the mesh and a fingerprint of its vertex positions and triangles (the FBX is therefore marked Read/Write). When the garment is bound, `ClothProfile.Problem` refuses a profile whose mesh, fingerprint or particle count do not match, logs the reason and leaves the garment as plain skinned mesh. After a re-export or a reimport that changes the mesh, rebake; the bake runs `Build Armor Items And Parts` before and after, which keeps the link by finding the profile for the mesh.
- **Lifecycle.** Motion is cleared on equip, on enable and when the wearer moves more than 0.75 m in a frame (a teleport or respawn), so a jump is never taken as acceleration. `updateWhenOffscreen` is on, because Cloth keeps stale vertices while its renderer is culled.
- **Shimmer check.** With the one clean mesh, Cloth's normals change 0.15 degrees a frame while the wearer idles (0.05 frozen), against 9.8 with the back-to-back faces; in a frozen pose about 113 of 137,000 character pixels change per frame, against none with the Cloth off. Measured with the disposable `ClothDiagnostics` in `Assets/ClothFitTest`.
- **Tests.** `HauberkClothEquipmentTests` (play mode, a host and a separate client) covers equip, movement, stopping, turning, teleport, unequip, re-equip, two wearers and a refused profile through `Equipment` -> `EquipmentAppearance` -> `CharacterAppearance`.

Grid sizes start from the standard of the games this one follows: 2 by 2 for head, hands and feet, 2 wide and 3 tall for chest and legs. That is only the default the tool starts a new piece with (`ArmorSetup.TryDefaultSize`): an item's size is its own data in its asset, a seed piece may give its own, and nothing enforces a size for a slot. The ratings are placeholders; everything here will be rebalanced after playtesting.

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

## World forms

An armor item has a world form so it can lie on the ground (dropped, left by an enemy, placed in the dungeon): `Item.WorldPrefab`, an ordinary `WorldItemPickup` prefab. The same item instance, with its affixes, goes through a drop and a pickup (`ItemTransferIdentityTests`).

**SimpleRPG > Items > Build Armor World Forms** makes one for each armor item that has none:

- **Generated by default.** The parts that dress the item are put on the body in its rest pose, baked, and folded into something that lies on a floor (`GarmentFold`): sleeves drawn in, laid face up, brought down to a floor-pickup size, set on its base. A helmet stands on its base instead. Worn armor is never dropped onto the floor in the pose of someone wearing it.
- **Authored override.** Put a prefab named like the item (`PlateArmor.prefab`) in `Assets/Art/WorldForms/Overrides` and it is used in place of the generated model.
- The prefab is added to `DefaultNetworkPrefabs`, so it can be spawned. An item that already has a world form is left alone; to remake one, clear the item's *World Prefab* and delete the prefab.

The fold's numbers (`GarmentFold.Settings.Default`) are a first guess. The result needs looking at in the editor; a piece that folds badly is a candidate for an authored override.
