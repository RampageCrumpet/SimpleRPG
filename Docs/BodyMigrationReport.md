# Report: the body is on the canonical A-pose rig; the hands holding the sword are not approved

Answers [BodyMigrationHandoff.md](BodyMigrationHandoff.md). Written 2026-11-04 by the local agent that had Blender and Unity open, with the owner. Nothing is committed or pushed. Beads: `simplerpg-do1` is **still open** (the hands are not approved); the follow-up is `simplerpg-5e5`.

**Read this first.** The body, rig, avatar, parts, socket and armor fit are done and tested. What is not done is the hands on the longsword: after the move the hands were mangled, and the fixes below improve them but the owner has not approved them. Do not close `simplerpg-do1` until `simplerpg-5e5` passes.

## 1. Blender export (done)

- Source: `I:/Art/Blender/SimpleRPGHuman/Human_base_mesh_added_equipment.blend` (the scene the owner works in; the earlier `..._Canonical52Bone_20261104.blend` backup was identical in rig and body meshes). Body meshes are the `ZN_*_ArmorAuthoring` objects on `Human_Male_ArmorRig` (52 bones). The look-alike `ZN_*` meshes on the 66-bone `Human_Male_Rig` were the old body.
- New script `Authoring/export_body.py` (beside `export_armor.py`) writes `Human_Male.fbx`: it refuses a rig that is not exactly `GameRig_Bones.json`, renames the armature to `Human_Male_Rig` for the file only, exports the nine meshes (`Human_Male_Head/_Chest/_Pants/_Boot_L/R/_Glove_L/R/_Eye_L/R`; the eyebrows stay the separate rigid asset), adds `Socket_Headgear` under `Head` at (0, -0.05, 1.46) in Blender axes (upright, facing forward; sized from the sallet's 0.24 m height and eye slit), and reads the file back in a clean Blender to check it. `Authoring/verify_body_fbx.py` is that check; `Authoring/scan_rigs.py` lists any `.blend` under `I:/Art` that holds a variant of the game rig.
- The chest's stray `Shoulder_Deformation_Only` weight group is dropped on export (as the armor export does).

## 2. Unity (done, with one trap)

- **The avatar was stale.** `Human_Male.fbx.meta` had not been rewritten since 2026-10-26, so the Humanoid avatar still held the old body's T-pose and fist data: 49 of 62 bones differed from the model by over 3 degrees (left thumb 147). The arms looked fine because the old T-pose data is a valid T-pose; the fingers did not. The Enforce T-Pose step was run through `AvatarSetupTool` (MakePoseValid, then TransferPoseToDescription) and the meta is rewritten. **After every body re-export, confirm the meta changed.**
- `HumanMaleSetup` and `ArmorSetup` ran (by the owner); the socket test passes. The three Longsword FBXes were reimported (`avatarSetup` stays 1, valid Humanoid). `TestCharacter.prefab` points at the new avatar.

## 3. Acceptance

- `RigConformanceTests`: all six green. `EveryArmorPart_IsBoundToTheRestPoseOfTheBodyRig` was red for a reason in the test, not the art: FBX meshes keep their node's axis conversion (Blender Z-up to Y-up), so every bone read 207.2 cm off, and the body's own chest read the same. All 21 skinned armor pieces match the body's bind poses (largest difference 0.00 cm). Fixed in `RigConformance` (new `MeshToRig`, optional argument on `Compare`) and the test, which now also asserts the body's own chest fits the rig.
- EditMode: 1531 passed, 0 failed (latest run, with the hand changes). PlayMode: 369 passed, 0 failed, **run before the hand and grip changes; rerun it**.
- Shoulder-yaw table (A1 at frames 1, 56, 70, 80, 93, 131): 0, 26, 47, 57, 67, 0 degrees, the doc's magnitudes exactly (the signs differ only by the measuring convention).
- Idle arms and the sallet: the owner confirmed the arms are fine; the sallet is being reworked and is not part of this.

## 4. What you asked me to report

- **Sword mount.** Old: `(-0.055777, 0.104543, -0.037857)`, fitted to the old closed hands. It was wrong on the new body: the hilt axis was about 54 degrees off the line through the two fists. New values come from the approved grip fit (see section 5): local position `(-0.0507, 0.0692, 0.0456)`, rotation `(-0.5793, -0.4546, 0.2882, 0.6120)`.
- **Hands reach `Grip_Left`.** `HandGripIK` pulls the left hand onto it (residual under 0.01 cm). The grip itself is the unapproved part.
- **Hurtboxes.** Every collider centre is within 5.1 cm of its bone's midpoint; none is detached.
- **Height.** Body mesh 1.684 m; character controller 1.72 m tall, spanning -0.56 to 1.16 from the root (feet at -0.525, so 3.5 cm under the soles); first-person eye 1.568 m above the feet against the body's 1.57 m.
- **Where the Blender scene is saved:** the path above (it is open in the owner's Blender; the owner must reload it, see section 7).
- **Gambeson `simplerpg-ujf`:** not touched.

## 5. The hands (open: `simplerpg-5e5`)

What was wrong, in order:
1. The stale avatar (section 2).
2. The mount and grip were calibrated to the old closed-hand body.
3. The hand skin weights stretched and squashed at the strike poses (38 edges over 1.5x and 42 under 0.6x on the left glove at A1 frame 70).
4. The attack clips curl no fingers on this body (they were authored for the old closed-hand rest), so the hands came out open.

What was done:
- Hand weights of the body's glove meshes re-fitted in Blender against poses captured from Unity (rest, Idle, strikes of all three attacks, open and closed fingers; edge-length stretch minimised, seam to the chest untouched, rest pose unchanged). Blender's skinning was checked against Unity's to 0.0001 cm first. At the strike: over 1.5x 38 to 2, under 0.6x 42 to 15; right hand worst 1.49x; one left-hand edge still 2.8x in two strike poses.
- The sword mount, `Grip_Left` and a new `Grip_Right` come from the approved grip fit (`I:/Art/Cascadeur/Longsword/RigAudit_20261027_data/approved_grip_matrices.json`, all finger bones of both hands in the sword's frame, mirrored in X for Unity).
- `HandGrip` now carries a closed-hand finger pose (15 bones) and a `placeHand` flag; `HandGripIK` applies it to every hand holding the item. `LongswordGripTests` has two new checks.

Still wrong: the fingers do not wrap the handle cleanly. The canonical hands fit the approved grip to about 2 cm rms, some finger joints sit 0.8 cm from the hilt axis, and the right thumb is about 4 cm off. Next step: check whether the canonical hand skeleton matches the one the approved grip was fitted on; if not, fit the finger joints to the real handle surface (radius about 1.45 cm, y from -0.12 to 0.12 in the sword's space) and use the approved grip only to place the hand. The armor gloves (`GloveGame`, `GloveReinf`, `GauntletPlate`) have their own weights and have not been re-fitted.

Traps found: editing Animator poses in edit mode is unreliable for the finger and hand layers (capture in Play mode or sample the clip). `TestCharacter.prefab` carries arm overrides, so take the rest pose from `Human_Male.fbx`, not from a prefab instance.

## 6. Cleanup done (to stop the old body returning)

- Removed from the authoring scene: every 64- and 66-bone rig, `REFERENCE_Old_Rig`, their body copies, the old sword copies, 127 animations keyed to the old bones, the old shoulder scripts. Kept: the 52-bone body and gambeson rigs and the 27-bone plate rig.
- Deleted on disk: the whole `3d Models/Human_Male` folder and 128 snapshot `.blend` files holding old rigs (36 GB); the list is `I:/Art/Blender/SimpleRPGHuman/DELETED_old_rig_files_20261104.txt`. 16 source files that also held an old rig had the armature removed and their meshes kept. `scan_rigs.py` now reports none. The small grip and audit data of the 2026-10-27 audit are kept in `I:/Art/Cascadeur/Longsword/RigAudit_20261027_data`.
- The Beads memory `canonical-human-male` was rewritten (it said the 64-bone `Human_Male_APose` was canonical); `AssetCreationWorkflow.md` has a "Body and rig" section with the rules (one rig, scripted exports only, no per-step scene snapshots, `SCRATCH_` for throwaway rigs).

## 7. For you

- Nothing is committed. `git status` also shows `SampleScene.unity` modified (three network-button anchors, from saving the scene at a Unity dialog) and `SimpleRPG.slnx` (already modified before this work); revert or keep them as you see fit.
- The owner's open Blender scene is older than the saved file (the hand weights were saved from a separate process); it must be reverted before saving there.
- The fit scripts and pose capture used for the hand weights are not yet in `Authoring/`.
