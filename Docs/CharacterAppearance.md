# Character appearance

A character is one animated skeleton plus swappable visual parts. The skeleton carries everything gameplay cares about (the `Animator`, hurtboxes, the sword, the eye); parts are only meshes, so swapping one never touches combat.

## Pieces

| Type | Job |
|---|---|
| `BodyPartSlot` | The places on a body (head, torso, legs, feet, hands, eyebrows, headgear). Headgear is separate from the head so a helmet is worn over the face, not instead of it. Network-visible; append only, never renumber. |
| `BodyPartDefinition` | A `GameplayDefinition`: slot, mesh, materials and how it attaches. Skinned parts list the rig bone for each bind pose; rigid parts name one bone and a local pose. Immutable at runtime, with a permanent id and a short network id in the `DefinitionRegistry`. |
| `RigBoneIndex` | Finds a rig's bones by name. |
| `BodyPartBinder` | Builds the renderer for a part and attaches it to a rig. The only code that knows how a part meets a skeleton. |
| `AppearanceView` | Pure presentation: which part is shown per slot. No network or gameplay knowledge. |
| `IBodyPartCatalog` | Part to short id and back. `RegistryBodyPartCatalog` uses the registry; tests can substitute another. |
| `CharacterAppearance` | The `NetworkBehaviour` on the character. |
| `EquipmentAppearance` | Server-side: dresses the character in the parts of the armor it wears, and back to the bare body when it comes off. A part's `dressesItem` names the armor it is the look of. See [Equipment](Equipment.md). |

## Authority

Server-authoritative, like equipment. The server owns the worn parts in a `NetworkList<PartEntry>` (slot and short id) and the only mutators are `ServerEquip` and `ServerClear`. A spawn starts from `defaultParts`. Clients, late joiners included, rebuild their `AppearanceView` from the list. No client request changes appearance. Because every machine animates the same skeleton, the hurtboxes the server tests are where clients see the body. The player's `Animator` is `AlwaysAnimate` for the same reason.

## Adding or swapping parts

1. Export a piece skinned to the shared humanoid rig (or a rigid piece, parented to a bone).
2. Add its mesh piece to `SlotsByPiece` in `HumanMaleSetup` (or create a `BodyPartDefinition` from the asset menu) and run **SimpleRPG > Appearance > Build Human Male Parts And Rig**. It updates assets in place, so ids and references survive.
3. Run **SimpleRPG > Definitions > Rebuild Definition Registry** (the builder does this too).
4. Put it in a character's `defaultParts`, or call `ServerEquip` at runtime.

Re-exporting the model updates the same assets and regenerates `HumanMale_Rig.prefab`, the model with its meshes and animator stripped. `CharacterAppearance` has *Preview Default Parts* and *Clear Preview* context-menu commands for seeing the parts in the editor; previews are never saved.

## Body source and longsword sources

The body FBX (`Assets/Art/3d Models/Human/Human_Male.fbx`) is exported from the authoring scene `I:/Art/Blender/SimpleRPGHuman/Human_base_mesh_added_equipment.blend` by `Authoring/export_body.py`: the canonical 52-bone A-pose rig `Human_Male_ArmorRig` and the `ZN_*_ArmorAuthoring` meshes, plus `Socket_Headgear`. Nothing else is a source for the body; the earlier `Human_Male_Corrected` / `Human_Male_APose` files and every 64- or 66-bone rig are gone (see "Body and rig" in [AssetCreationWorkflow.md](AssetCreationWorkflow.md)). Editable Cascadeur scenes and animation exports are in `I:/Art/Cascadeur/Longsword/HumanMale/Corrected`; the grip and audit data of the 2026-10-27 audit are in `I:/Art/Cascadeur/Longsword/RigAudit_20261027_data`.

The skeleton has 52 bones. Wrist weights blend across the forearm/hand transition.

### Holding the sword (2026-11-04, **not approved yet**)

Three things make the hands work on the canonical body, and the result is still being reviewed in play.

- **Avatar.** The Humanoid avatar of `Human_Male.fbx` must have been through *Enforce T-Pose* and saved (the model rests in an A-pose). A stale avatar keeps the old body's T-pose and fist data and twists the fingers. After every body re-export, check that `Human_Male.fbx.meta` changed.
- **Mount and grips.** The sword mount (`Longsword` under `RightHand`), `Grip_Left` and `Grip_Right` in `TestCharacter.prefab` come from the approved grip fit of 2026-10-27 (`I:/Art/Cascadeur/Longsword/RigAudit_20261027_data/approved_grip_matrices.json`: every finger bone of both hands in the sword's frame, in the authoring frame mirrored in X). Each hand's wrist and four knuckles are fitted onto the approved ones (Horn/Kabsch best fit); the right hand's pose gives the mount, the left's gives `Grip_Left`. The fit leaves about 2 cm rms because the canonical hands are not exactly the shape the grip was fitted for. Re-derive them the same way when the body, avatar or clips change.
- **Finger pose.** The attack clips were authored for the old closed-hand body and curl nothing on this one. `HandGrip.FingerPose` (15 finger bones per hand) is the closed hand on the sword, and `HandGripIK` applies it to every hand that holds the item (`placeHand` is off for the right hand, which the mount already carries). The pose aligns each finger bone to its approved direction. Known limits: some finger joints sit about 0.8 cm from the handle axis, and the right thumb is about 4 cm off it.
- **Hand skin weights.** The glove meshes' hand weights were re-fitted in Blender against the real poses (rest, Idle, strikes of all three attacks, with open and closed fingers): at the strike, edges stretched over 1.5x fell from 38 to 2 and squashed ones under 0.6x from 42 to 15. The armor gloves (`GloveGame`, `GloveReinf`, `GauntletPlate`) have their own weights and have **not** been re-fitted; expect the same stretch there. The fit works from poses captured in Unity (`rest` from the FBX instance, not from `TestCharacter.prefab`, which carries arm overrides) and checks them against Unity's own skinning. The fit scripts are not yet kept in `Authoring/`; until they are, a re-fit means writing them again.

`LongswordGripTests` checks that both grips close 15 finger bones and that each fingertip joint rests 0.5 to 4 cm from the hilt axis.


## A part is one or more pieces

A character shows **one part per slot**, and a part is **one or more `BodyPartPiece`s**. A piece is a mesh, its materials, and how it binds: skinned to the shared skeleton (a bone per bind pose, in order) or rigid on one named bone with its own local position, rotation and scale. Each piece chooses skinned or rigid for itself, so one part can be a skinned plate cuirass over a skinned mail undercoat plus a rigid buckle.

- `BodyPartDefinition.Pieces` is the one view: the primary piece (the flat `mesh`/`materials`/bone fields every existing part already has) followed by `AdditionalPieces`. Every existing part is a list of one and is built exactly as before: a one-piece part is its own renderer object.
- `BodyPartBinder.Create` returns the part's root object, which carries a `BodyPartInstance`. For a part of several pieces the root is an empty object under the parts root with each skinned piece beneath it; rigid pieces sit under their bones, outside the root, so the instance remembers them, lists every renderer (`Renderers`) and removes them when the part is removed.
- **No network change.** The wire still carries one `(slot, partId)` per slot; the pieces come from the definition the id names. Equipment identity decides the visual assets, as for single-piece parts.
- `OwnerViewBodyHider` hides by `BodyPartInstance.Renderers`, so rigid pieces on bones are covered.
- A part is buildable only if every piece is (`BodyPartDefinition.Problem`).

**The authoring tool** (`SimpleRPG > Items > Build Armor Items And Parts`) turns all skinned meshes of one slot in an item's prefab into the pieces of one part: the first mesh is the primary piece and names the asset (so an existing part keeps its id), the others are additional pieces. A part an earlier run wrote for the item that is now merged into another is detached from the item (`Dress(null)`), reported, and left for you to delete. A test fails if two parts dress the same slot of the same item.

Plate Armor is the first user: `PlateArmor_PlateArmor` is the plate with the mail undercoat as an additional piece; `PlateArmor_MailHauberk` no longer dresses the item.

**Not done:** rigid pieces are only produced by hand or by the helmet path (one combined mesh on the head bone); a shell and a separately moving visor would be two pieces once the visor has its own bone or socket. Rigid pieces for a head piece are placed in the body model's space, which depends on the rig pose question (A-pose armor vs the game's T-pose body), not on a per-item fix.

## Sockets

Where something sits on the body is body art data. A **socket** is an empty transform authored in the body model under the bone it belongs to, posed in Blender, and carried into `HumanMale_Rig.prefab` by `HumanMaleSetup` (which reports a missing one; it never invents one). Rigid pieces attach to a socket by name exactly as to a bone. The names are in `RigSockets`:

- `Socket_Headgear` under `Head`: origin where a helmet's own origin goes (bottom centre of the helmet), axes up and forward the way the face looks. Head pieces are authored with their origin and orientation to match, so `ArmorSetup` attaches them with an identity pose and assumes nothing about the body model's space. The sallet's origin is the bottom of the helmet, which is why it needs this.

`RigConformanceTests` fails if the body rig lacks the socket, if a rigid part names a bone or socket the rig lacks, or if armor is bound to a different rest pose than the body rig (`RigConformance`, which compares a mesh's bind poses with the rig's bones).

## Known limits

- The rig faces +Z with the sword mounted at `RightHand` (`Longsword`). Its calibrated local position is approximately `(-0.055777, 0.104543, -0.037857)` metres; its rotation is also essential. Hurtbox shapes still need a visual check in play.
- The `CharacterController` capsule (1.98 m) was left as it was and is taller than the 1.69 m body.
