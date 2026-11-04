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

## Corrected longsword sources

The current body FBX comes from `I:/Art/3d Models/Human_Male/Human_Male_Corrected.blend`. The companion `Human_Male_Longsword_Corrected.blend` contains all three baked attacks and a sword for review. Editable Cascadeur scenes and animation exports are in `I:/Art/Cascadeur/Longsword/HumanMale/Corrected`. The audit and validation evidence are in `I:/Art/3d Models/Human_Male/Authoring/RigAudit_20261027/REPORT.md`.

The skeleton retains its 52 bones and original proportions. Wrist weights blend across the forearm/hand transition. The TestCharacter prefab's sword mount and Grip_Left target are calibrated to the corrected closed hands; retain these transforms when replacing the weapon model. HandGripIK removes the small residual from Unity's compressed Humanoid curves.

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

## Known limits

- The rig faces +Z with the sword mounted at `RightHand` (`Longsword`). Its calibrated local position is approximately `(-0.055777, 0.104543, -0.037857)` metres; its rotation is also essential. Hurtbox shapes still need a visual check in play.
- The `CharacterController` capsule (1.98 m) was left as it was and is taller than the 1.69 m body.
