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

## One part per slot: what is and is not supported

Inspected against the code as it is (`BodyPartDefinition`, `BodyPartBinder`, `AppearanceView`, `CharacterAppearance`, `ArmorAppearancePlan`). This records the limit; it does not change the design, which is for the owner to review.

**What one item can have today**

| Need | Supported? | How |
|---|---|---|
| Several materials on one piece | **Yes** | A part's `Materials` array maps onto the mesh's submeshes, drawn by one renderer. Plate Gauntlets (steel and brass) use it. |
| Parts in several different slots (a left and right glove, a boot pair) | **Yes** | An item can have any number of parts; each is in its own slot. `EquipmentAppearance` puts all of them on. |
| Skinned or rigid | **Either, per part** | Skinned parts list a bone per bind pose; rigid parts name one bone and a pose. |
| **Several meshes (several renderers) in the same slot** | **No** | See below. |
| A skinned mesh and a rigid mesh together in a slot | **No** | Same reason. |

**Why not.** A `BodyPartDefinition` holds exactly one `Mesh`. `BodyPartBinder.Create` builds exactly one renderer from it. `AppearanceView` keeps one object and one part per `BodyPartSlot` (`Show` clears the slot first), and `CharacterAppearance` replicates one `(slot, partId)` entry per slot. So a slot shows one renderer, and the way to get more than one mesh onto a character is more slots.

**Where it already bites.**

- **Plate Armor** has two skinned renderers, the plate and a mail undercoat, and the tool makes a part for each: `PlateArmor_PlateArmor` and `PlateArmor_MailHauberk`, both in `Torso` and both marked as the look of the plate armor. `EquipmentAppearance` hands the plan both and the later one wins (`ArmorAppearancePlan.Desired`); "later" is the order the definition registry lists them, which is stable but arbitrary. **Only one of the two shows, and which one is not defined.** The authoring tool now reports this when it happens.
- **The Visored Sallet** was a shell and a visor. It was worked around by combining every mesh into one rigid mesh (`ArmorSetup`, in a design where one part is one mesh), which loses nothing visible while the visor is fixed shut but means the two cannot move separately.
- **Headgear** was added as its own slot (value 8) so a helmet is worn over the bare head. That solved a different problem (the face) by adding a slot; it is the pattern that more slots would repeat.

**What would have to be true of a change.** Whichever way it goes, these constraints exist in the code today:

- `BodyPartSlot` is sent over the network and is append-only.
- `AppearanceView.ShownIn(slot)` returns one `GameObject`, and `OwnerViewBodyHider` and the first-person view use it. It already hides by `GetComponentsInChildren<Renderer>`, so it copes with several renderers under that one object.
- `CharacterPreviewUI` shows `CurrentParts` (the definitions), not objects.
- Parts are server-authoritative state; a client never chooses one.

**Options for review** (none implemented):

1. **A part with several pieces.** `BodyPartDefinition` holds a list of pieces (mesh, materials, bone names, root bone), each drawn by its own renderer under one object per slot. No network change, no new slots, `ShownIn` still returns one object, and every existing part is a list of one. The plate and its undercoat become one part; so would a shell and a separately animated visor if the visor were later given its own bone. The cost is a change to the definition's shape and its editor tooling.
2. **Several parts per slot, layered.** The slot holds an ordered list of parts. This changes what the network replicates (one entry per slot becomes several), `AppearanceView`, and every place that assumes a slot has one part.
3. **Keep one mesh per part and combine at export.** What the sallet does. No code change; the cost is that anything combined cannot be separated in game, and mixed skinned and rigid content cannot be combined at all.
4. **More slots for layers** (an `Undercoat`, as `Headgear` was added). Cheap each time, but it is the pattern that grows without end.

Option 1 meets the case in front of us (a plate over an undercoat) with the smallest change to what already exists; option 3 is what is in place.

## Known limits

- The rig faces +Z with the sword mounted at `RightHand` (`Longsword`). Its calibrated local position is approximately `(-0.055777, 0.104543, -0.037857)` metres; its rotation is also essential. Hurtbox shapes still need a visual check in play.
- The `CharacterController` capsule (1.98 m) was left as it was and is taller than the 1.69 m body.
