# Hand-off: move the game body to the canonical A-pose rig (needs Blender and Unity)

Context: `Docs/RigMigrationReport.md` (approved). Do not change code; stop and report if a step needs a design change. Leave unrelated and in-progress files alone.

## 1. Blender: export the body
1. Open the scene whose edits are in `Backups/..._Canonical52Bone_20261104.blend` (or the cleaned-up successor) and confirm it is the canonical 52-bone A-pose body.
2. Add an **empty named `Socket_Headgear`** parented to the `Head` bone, at the bottom centre of where a helmet sits (the base of a helmet like the sallet), with +Y up and +Z forward the way the face looks. Keep it out of the skinned meshes' hierarchy changes otherwise.
3. Export `Human_Male.fbx` with the same validated settings as `export_armor.py` (same node names: the model has a `Human_Male_Rig` child, meshes `Human_Male_Head`, `_Chest`, `_Pants`, `_Boot_L/R`, `_Glove_L/R`, 52 bones, no twist/`MCH_` bones). Export the eyebrows the same way if their space changed.
4. Replace `Assets/Art/3d Models/Human/Human_Male.fbx` (keep its `.meta`; do not let the importer regenerate the GUID).

## 2. Unity: avatar and parts
1. Humanoid avatar from this model; in the avatar editor map all bones, then **Enforce T-Pose** and Done.
2. Run **SimpleRPG > Appearance > Build Human Male Parts And Rig**, then **SimpleRPG > Items > Build Armor Items And Parts**. Report anything either lists, especially the missing-socket error.
3. Re-import the three Longsword FBXes (`avatarSetup` stays 1).
4. Check `TestCharacter.prefab` still points at the new avatar (`m_Avatar`) and the Animator is valid and human.

## 3. Acceptance (report each result)
- `RigConformanceTests`: all five green, in particular `EveryArmorPart_IsBoundToTheRestPoseOfTheBodyRig` (paste the worst deviation if it fails) and `TheBodyRig_HasTheHeadgearSocket_UnderTheHead`.
- Full EditMode and PlayMode suites. `LongswordGripTests` and the hit-region tests may need the recalibration below; report, do not edit the numbers blind.
- In play, third person and first person: Idle arms are not drifting; plate armor and the mail undercoat both show; the sallet sits on the head; gauntlets and sleeves fit.
- Shoulder-yaw table in `Docs/Animations.md` (A1 at frames 1, 56, 70, 80, 93, 131: 0, -26, 47, 57, 67, 0), measured the way that doc describes.

## 4. Report back, do not fix
- The Longsword mount (`Longsword` under `RightHand`, was `(-0.055777, 0.104543, -0.037857)`): where the blade sits relative to the right hand now.
- Whether hands reach `Grip_Left` (`HandGripIK`).
- Hurtboxes: any that no longer cover their body part.
- Body height and the capsule/camera height against about 1.69 m.
- Open items from the earlier report: where the Blender scene is saved; Gambeson (`simplerpg-ujf`); close `simplerpg-do1` only if all of the above passes.
