# Character appearance

A character is one animated skeleton plus swappable visual parts. The skeleton carries everything gameplay cares about (the `Animator`, hurtboxes, the sword, the eye); parts are only meshes, so swapping one never touches combat.

## Pieces

| Type | Job |
|---|---|
| `BodyPartSlot` | The places on a body (head, torso, legs, feet, hands, eyebrows). Network-visible; append only, never renumber. |
| `BodyPartDefinition` | A `GameplayDefinition`: slot, mesh, materials and how it attaches. Skinned parts list the rig bone for each bind pose; rigid parts name one bone and a local pose. Immutable at runtime, with a permanent id and a short network id in the `DefinitionRegistry`. |
| `RigBoneIndex` | Finds a rig's bones by name. |
| `BodyPartBinder` | Builds the renderer for a part and attaches it to a rig. The only code that knows how a part meets a skeleton. |
| `AppearanceView` | Pure presentation: which part is shown per slot. No network or gameplay knowledge. |
| `IBodyPartCatalog` | Part to short id and back. `RegistryBodyPartCatalog` uses the registry; tests can substitute another. |
| `CharacterAppearance` | The `NetworkBehaviour` on the character. |

## Authority

Server-authoritative, like equipment. The server owns the worn parts in a `NetworkList<PartEntry>` (slot and short id) and the only mutators are `ServerEquip` and `ServerClear`. A spawn starts from `defaultParts`. Clients, late joiners included, rebuild their `AppearanceView` from the list. No client request changes appearance. Because every machine animates the same skeleton, the hurtboxes the server tests are where clients see the body. The player's `Animator` is `AlwaysAnimate` for the same reason.

## Adding or swapping parts

1. Export a piece skinned to the shared humanoid rig (or a rigid piece, parented to a bone).
2. Add its mesh piece to `SlotsByPiece` in `HumanMaleSetup` (or create a `BodyPartDefinition` from the asset menu) and run **SimpleRPG > Appearance > Build Human Male Parts And Rig**. It updates assets in place, so ids and references survive.
3. Run **SimpleRPG > Definitions > Rebuild Definition Registry** (the builder does this too).
4. Put it in a character's `defaultParts`, or call `ServerEquip` at runtime.

Re-exporting the model updates the same assets and regenerates `HumanMale_Rig.prefab`, the model with its meshes and animator stripped. `CharacterAppearance` has *Preview Default Parts* and *Clear Preview* context-menu commands for seeing the parts in the editor; previews are never saved.

## Known limits

- The rig faces +Z with the sword mounted at `RightHand` (`Longsword`), grip offset `(0, 0.07, 0)`. The offset and the hurtbox shapes were fitted by measurement from the T-pose and need a visual check in play.
- The `CharacterController` capsule (1.98 m) was left as it was and is taller than the 1.69 m body.
