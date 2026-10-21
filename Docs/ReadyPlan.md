# One-handed ready pose: detailed plan (for review, nothing animated yet)

Beads: `simplerpg-1rf.4` (ready pose) and `simplerpg-1rf.9` (sword grip socket). Overall plan: `Docs/SkeletonAnimationPlan.md`. Reference clips: Idle and Walk (approved).

## Purpose and constraints
- Weapon-layer loop that plays **over Idle and Walk** (skeletons keep moving while armed, so elites can attack on the move). It is also the first and last pose of both attacks.
- 30 fps, **60-frame loop** (2.0 s), Humanoid, humanoid bones only.
- Style: a slow, loose, uncoordinated undead grip. The sword never holds perfectly still: it sags, lifts, trembles and the hand re-grips once per loop.
- Right-handed. A left-handed variant is a later mirror, not part of this MVP.

## Layering and mask
- The ready pose only needs the **right shoulder, upper arm, forearm, hand and right-hand fingers**. Everything else comes from the base layer, so the torso keeps the shamble of Idle and Walk and the arm follows it (a layer override replaces the arm's rotations relative to its parent, which still moves with the chest).
- Unity layout: layer **Weapon_Arm** (avatar mask: right arm and hand only) holds the ready loop. A second layer **Weapon_Attack** (mask: spine, chest, upper chest, neck, head, both arms) holds the attacks, which need torso twist. The attacks start and end on this ready pose.
- Author the clip full-body in Cascadeur (legs and torso as Idle) so the pose reads in context, but only the right-arm channels are used.

## Sword and grip socket (new work, `simplerpg-1rf.9`)
What I found in `sword_v5.blend` and the Unity project (`Assets/Art/3d Models/Items/Shortsword/Shortsword_Rusty.prefab`, already imported):
- One mesh, 151 vertices, **0.658 m long**. The origin is at the centre of the grip.
  - Pommel at y -0.076 to -0.046, grip -0.046 to 0.044 (9 cm), crossguard 0.044 to 0.074 (11.2 cm wide), blade 0.074 to 0.582 (50.8 cm, tapering 4.8 cm to a point).
  - Blade along +Y, cutting edges on +-X, flats on +-Z. Double-edged, so edge direction never matters.
- No socket, empty or rig exists, so I will add one.

Socket design (right hand), based on the rest pose of `RightHand`:
- The hand rests **thumb-up**, fingers pointing outward and about 27 degrees forward, knuckle line vertical. Wrist to knuckles is 8.6 cm and the knuckle span is 6.2 cm.
- **Origin:** the palm centre, 55% of the way from the wrist to the knuckle centre, shifted about 1.4 cm toward the palm side so the grip sits under the curled fingers. (The side is checked visually, see below.)
- **Blade axis (Y):** along the knuckle line toward the thumb side (up in the rest pose), **tilted about 25 degrees toward the fingers**. A real sword grip crosses the palm diagonally and is not perpendicular to the forearm. Without the tilt, pointing the blade forward-up would need an impossible wrist deviation.
- **Edge axis (X):** along the forearm / finger direction. **Flat normal (Z):** the palm normal.
- Created as an empty parented to `RightHand` in three places: the Blender preview scene, the Unity `Skeleton_Test` prefab (child `Sword_Socket` of the right-hand bone, with a `Sword_Mount` child that aligns the mesh axes), and (optional, Cascadeur permitting) a sword proxy attached to the right-hand joint so you can review it in Cascadeur.
- **Verification before animating:** a Blender render of the sword in the rest-pose hand from three angles, to check the fingers wrap the 2.4 cm grip, the palm side is right, and the guard sits at the index finger. I iterate the numbers until it looks right and you confirm.

## Pose targets (character-local, in the Idle stance)
Because the pose is defined by where the sword is, I solve the arm by joint angles and then measure the sword with the socket.
| Quantity | Target |
|---|---|
| Blade pitch (above horizontal) | 38 degrees at rest, 32 to 41 over the loop |
| Blade yaw | 10 degrees across the body toward the left of forward |
| Hand (grip centre) position | about 32 cm in front of the chest, 22 cm to the right of the spine, at lower-chest height (about 1.08 m) |
| Elbow | about 75 degrees of flexion |
| Upper arm | about 28 degrees forward, 14 degrees abducted, slight internal rotation |
| Forearm | neutral (thumb up) |
| Wrist | ulnar deviation about 20 degrees, slightly slack |
| Shoulder | 2 degrees raised, 3 degrees protracted |
| Fingers | wrapped on the grip: proximal about 60, intermediate about 80, distal about 50 degrees; thumb across the grip |
| Clearance | blade at least 4 cm from the torso, thigh and left arm |

## Timeline (60 frames, loops; author three cycles and use the middle one, as with the Walk)
| Frames | Beat |
|---|---|
| 0 | Reference ready pose (blade pitch 38 degrees). |
| 0-22 | Slow droop: blade pitch falls to 32 degrees, hand sags about 1.5 cm, the wrist lags the forearm by 4 frames. |
| 22-34 | Hold with tremor: blade shakes about 0.6 degrees every 4-5 frames. At frame 30 the fingers loosen about 8 degrees and re-grip (once per loop). |
| 34-46 | Lift: blade pitch rises to 41 degrees, shoulder raises about 1 cm. |
| 46-60 | Settle back to the frame 0 pose, damped, velocity matching frame 0. |
Asymmetric on purpose (slow droop, quicker lift), no beat on an even interval.

## Keys and quality rules
- Sparse keys on a regular 4-frame grid (0, 4, 8, ... 60), clamped Bezier, no gaps under 3 frames, remove baked keys with `unset_section` (lessons from the Walk).
- Loop closure: pose and velocity at frame 60 equal frame 0. Right-arm jerk kept at or below the Walk's arm values.
- Fingers do not pass through the grip; the elbow never snaps (check frame-to-frame elbow angle).
- Works on top of both Idle and Walk: with the torso leaning 9 to 13 degrees and swaying, the blade must still clear the body and not point into the ground.

## Workflow
1. Socket: Blender preview scene first, render for your check, then the Unity prefab (and Cascadeur proxy if possible).
2. Extend the pose helper with the right-arm solve and a sword-tip readout through the socket; dry-run the targets (no scene writes).
3. Key the loop in `Skeleton_Ready.casc`, render a contact sheet with the sword in hand, **stop for your review**.
4. Polish, export, Unity import (right-arm mask, layer test over Idle and Walk), measure in Play Mode.

## Questions for you
1. **Grip look.** A blade tilted forward-up at about 38 degrees held at lower-chest height. Do you want it lower and more casual (hand near the hip, blade up and out), or more guarded (higher, blade closer to vertical)?
2. **Left arm.** Leave it on the base layer (swinging with Idle and Walk), as planned? Or should the ready pose also hold it a little forward for balance?
3. **Cascadeur sword proxy.** Worth trying so you can review the sword in Cascadeur? If it does not work, previews would stay in Blender renders.

## Update 2026-10-21: socket built and grip fitted
- **Socket:** the sword origin sits 5.9 cm from the wrist bone, **slid 1.8 cm up the blade axis** from the first palm-centred placement, because the guard otherwise sat on the index knuckle and the index finger passed through it (0.46 cm of bone centreline inside the guard). Data: `I:\Art\Cascadeur\Skeleton\Sword_Socket.json`.
- **Finger pose:** the 15 right-hand finger joints were fitted numerically so that no bone centreline enters the handle, guard, blade root or pommel. Worst bone-surface overlap (0.6 cm bone radius assumed) is 0.28 cm at the thumb, i.e. contact. Data: `I:\Art\Cascadeur\Skeleton\Sword_Grip_Pose.json` (per-bone rotations to apply as base * q).
- **Unity:** `Skeleton_Test.prefab` has `RightHand/Sword_Socket/Shortsword_Rusty` (blade direction error 0.0 degrees, tip within 0.12 mm of the Blender frame).
- **Render:** `Docs/Previews/Sword_socket_grip.png`.
- **Cascadeur sword proxy:** not done. Importing the sword mesh hung Cascadeur's script server. Previews stay in Blender.

## Update 2026-10-21 (later): grip re-fitted against the real meshes
The first grip check used bone centrelines and a guessed bone radius, and it forced every finger to curl about one shared axis. Testing against the actual hand and sword meshes (910 right-hand vertices) showed the palm and metacarpal mesh sunk up to 7.3 mm into the handle, so you were right that the hand clipped.
- **Final grip:** blade tilt **8 degrees** toward the fingers (was 25), palm offset **2.6 cm** (was 1.4), sword slid 1.8 cm up the blade. Result: 7 of 910 hand vertices inside the sword, 0.9 mm deep at most (contact), no palm penetration. Alternatives tested: 15 degrees (12 vertices, 1.9 mm), 25 degrees (best case 26 vertices, 3.8 mm, mostly palm).
- **Consequence for the pose:** with the blade nearly perpendicular to the palm line, reaching the approved 38 degree blade pitch would need about 44 degrees of ulnar wrist deviation, well past the anatomical range (about 30-35). Realistic ready-pose blade pitch with this grip is **about 48-55 degrees above horizontal (the blade leans forward about 35-40 degrees from vertical)**. This needs your confirmation before animating.
- **Data:** `Sword_Socket.json`, `Sword_Grip_Pose.json` (per-finger rotations), render `Docs/Previews/Sword_socket_grip.png`, options `Sword_socket_grip_options.png`. Unity `Skeleton_Test.prefab` updated.

## Update 2026-10-21 (3D check)
User: fingers still looked separated in the stills. Added a cohesion term to the grip fit (adjacent fingers may not drift farther apart than their resting spacing plus 3 mm). Result: worst finger separation +4 mm over rest, 6 of 910 hand vertices inside the sword, 0.64 mm deep at most. A 3D copy to orbit in Blender: `I:\Art\Blender\Skeleton\Exports\Sword_Grip_Preview.blend` (a copy, the live file is untouched). Finger data refreshed in `Sword_Grip_Pose.json`.

## Update 2026-10-21 (finger joints)
User spotted definitive splitting at the last finger bone. Cause: the rig's finger and toe bone heads are 5-24 mm away from the points where the bone meshes meet, so rotating a bone about its head swings the mesh away from its neighbour. Curled by rotation only (all Unity Humanoid and Cascadeur can do), neighbouring phalanges open 6-22 mm; at rest they are 1-2.5 mm apart.
- My Blender grip fit now rotates about the true mesh joints (small per-bone translation), which keeps joint gaps at 0.9-1.8 mm, but that pose cannot be played in Unity or Cascadeur.
- **Rig fix prototype** (copy, nothing adopted): bone heads moved onto the mesh joints with orientation preserved. Rest mesh unchanged (0.0002 mm); rotation-only curl gaps 0.5-1.7 mm. File: `I:\Art\Blender\Skeleton\Exports\Skeleton_FingerPivotFix_v66.blend`. Beads: new issue under the epic.
- Needs your approval before adoption because it changes the master skeleton: re-export the model and the Cascadeur humanoid skeleton, re-import in Unity, re-verify Idle and Walk, redo the grip fit on the fixed rig, then animate the ready pose.

## Update 2026-10-21: finger pivot fix ADOPTED into the master skeleton (v66)
- **New master:** `I:\Art\Blender\Skeleton\AnatomicalHumanSkeleton_GameReady_v66_FingerPivots.blend`. v65 is untouched and stays as the fallback. Use v66 from now on; helper scripts point at it.
- **Change:** 48 finger and toe bone heads moved onto the real mesh joints (up to 24 mm), orientation and roll preserved (max 0.08 degrees), 152 bones and all names unchanged. Rest-pose mesh identical to v65 (0.0002 mm).
- **Die/Rise:** all seven baked actions (die, rise, pile pose and their backups) were converted so each key's mesh deformation matches v65: under 0.001 mm at 13 of 15 checked frames; the other two fall between baked keys (0.3 mm and 2.2 mm). The pile looks the same.
- **Re-exported:** `Skeleton_Model.fbx` (Unity, re-imported over the old one so the prefab and sword socket stay linked) and `I:\Art\Cascadeur\Skeleton\Skeleton_Humanoid_v66.fbx` (82-bone skeleton for new Cascadeur projects). The existing Idle and Walk projects stay as authored: they are rotation-based and unaffected.
- **Unity re-verified in Play Mode:** Walk ankles 0.0672 m, hips 0.9065-0.9454 m, knees 16.9-42.6 and 20.7-45.3 degrees (identical to before); Idle ankles 0.0660 m, hips 0.936-0.947 m.

## Sword grip LOCKED (2026-10-21)

- Master rig: `AnatomicalHumanSkeleton_GameReady_v66_FingerPivots.blend`. Final fit: `Scripts/grip_fit7.py` (hilt fixed at the user's hand placement, fingers solved on the real meshes, strict penetration weight). Result: palm max 1.2 mm in, fingers max 0.6 mm, joint gaps under 2.5 mm, fingertips wrap the hilt.
- Sword placement is the user's saved `Exports/Sword_Grip_Preview.blend` (3 mm nudge after the fit). Socket data: `I:/Art/Cascadeur/Skeleton/Sword_Socket.json` (armature-space matrix, blade direction ~50 deg pitch, 8 deg grip tilt). Finger rotations: `Sword_Grip_Pose.json` (quat_wxyz bone-local, rotation-only curls).
- Unity: `Skeleton_Test.prefab` `RightHand/Sword_Socket` set to world (0.8002, 1.4302, 0.1585), `LookRotation(flat, blade)`; sword tip verified at (1.046, 1.957, 0.137).
- Next: build `Skeleton_Ready.casc` from `Skeleton_Humanoid_v66.fbx` and animate per this plan.

## Ready pose keyed, awaiting review (2026-10-21)
- `I:/Art/Cascadeur/Skeleton/Skeleton_Ready.casc`: 46 keys on a 4-frame grid over frames 0-180 (three 60-frame cycles, use 60-120), clamped Bezier, torso/legs as Idle, right arm + grip fingers animated. Scripts: `Scripts/ready_rig.py` (arm solver, sword socket calibrated from the rest hand), `ready_anchors.py`, `ready_key.py`, `ready_fingers.py`, `ready_preview.py`. Contact sheet: `Docs/Previews/Ready_v1_sheet.png`.
- Measured through the real sword: blade pitch 49.1-58.5 degrees (neutral 56), yaw 8-10 degrees left, loop closes exactly (frame 60 = frame 120). Beats: droop 0-22, hold with 0.7 degree wrist tremor 24-32 and finger re-grip at 30, lift 34-44, settle 44-60; wrist lags 4 frames.
- Arm solution needs about 95 degrees of forearm pronation and 62 degrees of upper-arm internal rotation (rest hand is in anatomical position). Hand sits about 28 cm in front of the chest and 8 cm further right than the plan (-28 vs -20 cm); pitch is 55 degrees, not the original 38.
- Not done: polish, Unity export and Weapon_Arm layer test.

## Ready pose revision v3: less pronated (2026-10-21)
- Finger fix: Cascadeur joint Rotation nodes are global, so grips are written parent-relative from the true rest (`ready_fingers.write_fingers2`, `ready/rest_true_all.json`). Earlier v1 had open fingers because of this.
- Arm re-solved with a total-roll penalty (upper-arm twist 46 + forearm twist -66 = about 112 degrees, was 157) and a position box instead of a fixed point. Blade now 65-74 degrees (near-vertical guard), yaw 7-8 degrees left, hand 34 cm right of the spine, 37 cm ahead of the hips, wrist at its ulnar-deviation limit (about 32 degrees). Loop closes at frame 60/120. Sheet: `Docs/Previews/Ready_v3_sheet.png`; 3D copy: `Exports/Skeleton_Ready_Preview.blend`.

## Left arm (2026-10-21)
The left arm is posed limp at the side in this clip: upper arm as in Idle, forearm pronated 65 degrees about its OWN axis (twisting about the world vertical swings the forearm behind the body; Cascadeur also shares one roll between arm and forearm, so the elbow shifts slightly). Result: wrist about 29 cm left of the spine, hanging straight down, thumb side forward. In Unity the left arm comes from the base layer, so Idle/Walk may still show the palm-forward hang.

## Left arm v3: hangs vertically (2026-10-21)
The user marked the target: shoulder, upper arm and forearm stacked on one vertical line. The left arm is now solved geometrically: each bone's axis is rotated to world vertical (`ready_key.left_arm_pose`), forearm pronated 50 degrees about the vertical, 4 degrees of limp elbow bend, 3 degrees of abduction for pelvis clearance, about 1 degree of pendulum sway, loose finger curl. Measured at frame 60: shoulder x 15.0, elbow 17.0, hand 16.7 cm; arm-to-hip/thigh mesh clearance 1.3-3.7 mm.

## Ready pose polished, exported, tested in Unity (2026-10-21)
- Polish: hold tremor softened (0.3/0.45/0.3 deg), lift stretched to frames 36-50, settle eased. Tip peak acceleration 0.46 cm/f^2 (was 0.66), wrist smooth, loop closes exactly (frame 60 = 120, velocity matches). Blade pitch 65.3-72.9 deg in the clip.
- Export: `Assets/Art/Animations/Skeleton/Skeleton_Ready.fbx` (copy at `Art/Cascadeur/Skeleton/Skeleton_Ready.fbx`), Humanoid, avatar from the model, clip `Ready` frames 60-120, loopTime/loopPose on, importer humanDescription copied from Walk. The clip's takeName is the exported file name (`ready_v10`): re-set it if you re-export under another name.
- Controller `Skeleton_Test.controller`: new layer `Weapon_Arm` (Override, weight 1, mask `Weapon_Arm.mask` = right arm + right fingers only) with state Ready.
- Play Mode (deterministic stepping, 60 frames after a 100-frame settle): right hand moves 0.6 cm relative to the chest in both Idle and Walk; sword tip 4-7 cm; blade pitch 64.8-71.8 deg over Idle and 58.0-66.9 deg over Walk; left hand follows the base layer (3 cm in Idle, 18 cm swing in Walk).

## 60 fps conversion and left-hand fix for Idle, Walk, Ready (2026-10-21)
- **Left hand fixed** in `Skeleton_Idle.casc` and `Skeleton_Walk.casc` (only the left arm and left fingers re-keyed, on their existing key frames, tool `Scripts/left_fix.py`): limp hang, forearm pronated 78 degrees, 8 degrees out at the shoulder and 10 at the elbow, loose finger curl (Idle's left-finger twitch keys were rewritten with the same curl, so the twitch is gone). Walk swings the arm as a pendulum (about 8 degrees) with the same pronation. Measured thumb side forward 0.75-0.86 and arm to hip/thigh clearance 22 mm or more across the whole Walk loop, 34 mm or more in Idle.
- **60 fps:** Cascadeur writes 30 fps FBX and its fps is not scriptable, so `Scripts/fbx60.py` upsamples the ASCII export: a cubic midpoint is inserted between every baked sample and the file's frame rate is set to 60 (FBX TimeMode 3). The motion is identical (Unity muscle values differ by at most 0.002) and Unity reads 60 fps clips. Source scenes stay on a 30 fps grid.
- **Final files:** `I:\Art\Cascadeur\Skeleton\Final_60fps\` (Skeleton_Idle_Blocking.fbx, Skeleton_Walk.fbx, Skeleton_Ready.fbx) are the same files that are in `Assets/Art/Animations/Skeleton/`. The 30 fps binary working exports are refreshed next to them (`Idle_blocking.fbx`, `Skeleton_Walk.fbx`, `Skeleton_Ready.fbx`) for the Blender previews. Scenes: `Skeleton_Idle.casc`, `Skeleton_Walk.casc`, `Skeleton_Ready.casc`.
- **Unity clip ranges (60 fps frames):** Idle 0-180 (3.0 s), Walk 108-216 (1.8 s), Ready 120-240 (2.0 s), all Humanoid and looping. Take names kept (`Idle_blocking`, `Skeleton_Walk`, `Skeleton_Ready`). Play Mode: hips y Idle 0.935-0.944, Walk 0.905-0.944 (as before), blade pitch 64.9-72.0 over Idle and 58.0-67.1 over Walk.
- **Re-export recipe:** export ASCII from Cascadeur (`FbxSettings.mode = Ascii`, `ld.export_all_objects`), run `python fbx60.py in.fbx out.fbx <take name>` and copy over the Unity file. The importer's first/last frame are in 60 fps frames.
