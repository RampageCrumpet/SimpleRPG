# Vertical overhead strike: detailed plan, revision 3 (approved answers folded in; stills review next)

Beads: `simplerpg-1rf.5`. Overall plan: `Docs/SkeletonAnimationPlan.md` (section 5). Reference clips: Idle, Walk, Ready (approved, now 60 fps, `Docs/ReadyPlan.md`). Start and end pose: the approved ready pose.

## Decisions (user, 2026-10-21)
1. **Strike:** 10 frames at 60 fps (0.167 s), as the master plan's 5 frames at 30 fps.
2. **Windup:** **1.5 s** from first movement to release ("2 s is far too long for the player to dodge").
3. **Exaggeration:** highly exaggerated, a ton of wind up, readable for a teaching enemy. The torso amounts below are a starting point and may be revisited.
4. **Idle, Walk, Ready:** left hand fixed to match, all re-exported at 60 fps, finals saved. **Done.**
5. **Engine hook:** add a small layer-aware "Attack tag" check (the melee code only reads layer 0 today). Approved for now, may be reworked.
6. **Chop slightly toward the sword side.**
7. **Separate layers per arm:** right arm and torso on `Weapon_Attack`, left arm on `OffHand_Attack` playing the same clip. The off-hand layer is off when a shield is held.
8. **Target 60 fps** for this clip and everything after it.

## Purpose and constraints
- One-shot weapon attack, **168 frames at 60 fps (2.8 s)**, Humanoid, humanoid bones only, authored in Cascadeur (`Skeleton_Overhead.casc`, built from `Skeleton_Humanoid_Rigged.casc`).
- Early, common enemy that teaches the player to dodge: impossible-to-miss telegraph, fast chop, long clumsy recovery (the punish window).
- Slow, shambling, uncoordinated, and **exaggerated**. The sloppiness is in the windup and recovery. **The strike direction stays clean.**
- Right-handed, same sword and socket as the ready pose.

## Frame rate (60 fps)
- Cascadeur cannot change the scene frame rate from a script. The scene is authored on a **30 fps grid** (frame N of the scene is plan frame 2N) and the FBX is upsampled to 60 fps with `Scripts/fbx60.py` (cubic midpoints, FBX TimeMode 60). Unity then sees a 60 fps clip and events are placed in seconds (frame / 60), as the longsword tests do. This worked for Idle, Walk and Ready (values identical within 0.002).
- All key frames below are on **even 60 fps frames** so they fall exactly on the 30 fps grid.

## Layering, masks and engine integration
- `Weapon_Attack`: transform mask = Spine, Chest, UpperChest, Neck, Head and the **right** shoulder, arm, hand and fingers.
- `OffHand_Attack`: transform mask = the **left** shoulder, arm, hand and fingers. Same clip, same trigger. Weight 0 when a shield is held.
- **Hips and legs excluded from both masks**, so the base layer keeps the Walk sway and legs. A transform mask is needed because the body-part "Body" toggle includes the Hips. Verified in Play Mode.
- Both layers: Override, weight 1, default state an **empty state** (the Ready loop shows through), a trigger enters `Overhead` (0.10 s blend in), exit returns to empty (0.25 s blend out).
- Idle, Walk and Ready now share the same limp left arm, so the `OffHand_Attack` blend is only the swing.
- **Engine hook:** a small, separate, reversible check so a state tagged `Attack` on any layer counts (`MeleeAttackAbilityBehaviour.TryQueue` reads layer 0 only). I will report the change and add a test. Nothing else in the melee code is touched.
- **Events:** `ActivateWeapon(1)` and `DeactivateWeaponH`, placed in seconds (frame / 60).

## Hit window (detectable hitbox)
- Strike: **frames 90-100 (10 frames, 0.167 s)**.
- Hit window: **frames 96-108 (12 frames, 0.2 s)**: `ActivateWeapon` at 96/60 = 1.60 s, `DeactivateWeaponH` at 108/60 = 1.80 s. That is 10 physics steps at the default 50 Hz fixed step, and covers the contact and the first part of the overshoot, so the hitbox registers.
- I will measure the blade at every frame of the window: inside the 90-150 cm height band, 90-140 cm in front of the character, 5-25 cm to the right of the centre line.

## Sword geometry used for every target
- Blade length 58.2 cm from the grip centre. Double-edged. In the ready pose the **flats face left and right and the edges face forward and back**. **The blade does not roll about its axis**, so the edge leads the cut. Check: edge within 15 degrees of the tip's travel direction at the hit window.
- Cascadeur centimetres: +X is the character's left (the sword hand is at **negative X**), +Y up, +Z forward. "Pitch" is the blade's angle above horizontal: 90 is vertical, **above 90 the tip leans back past vertical**, negative points below horizontal.
- Landmarks: skull top 178 cm, right shoulder joint (-16, 142, 8), chest (0, 118, 7), hips (0, 94, 8). Ready pose: grip centre (-34, 103, 37), pitch 71, yaw 8 left.

## How the exaggeration is built (1.5 s telegraph)
1. **Anticipation dip (0-10, 0.17 s):** the sword drops and draws back, torso hunches 6 degrees.
2. **Raise (10-60, 0.83 s):** lurching lift, shoulder shrug about 5 cm, elbow leads high, an overshoot past the top at 50-60.
3. **Back arch and wobble (60-78):** the sword tips back to about 138-142 degrees, wobbles, then arches further. Chest about **31 degrees back of the idle slump**, head thrown back, 8 frames late.
4. **Top hold (78-90, 0.2 s):** a clear freeze with a shiver of about 2 degrees (keys at 78 and 84) and the fingers tighten once.
5. **Chop (90-100, 0.167 s):** violent against everything before it.
6. **Overshoot and sag (100-114):** drives low, torso folds about **26 degrees past the idle slump**, rebound.
7. **Recovery (114-168, 0.9 s):** slow, uneven, shoulder first, then elbow, then wrist, a second sag near frame 130.
8. **Left arm** (off-hand layer): flings back and out as a counterweight at the top, swings forward on the chop.
Legs are on the base layer, so the clip cannot plant or step. A stomp or weight shift would be a later base-layer addition.

## Key poses (60 fps frames; arm solved to sword targets, never keyed by eye)
Spine and Chest are absolute pitch values in degrees (+ forward; Idle is Spine 3, Chest 6).

| Frame | Beat | Grip centre (cm) | Blade pitch | Spine / Chest | Notes |
|---|---|---|---|---|---|
| 0 | Ready | (-34, 103, 37) | 71 | 3 / 6 | Matches the Ready clip. |
| 10 | Anticipation dip | (-31, 95, 30) | 55 | 6 / 12 | Drops and draws back, torso hunches. |
| 20 | Lurch up | (-30, 112, 26) | 80 | 4 / 8 | Shoulder shrugs, elbow leads. |
| 30 | Raise | (-31, 130, 34) | 98 | 1 / 3 | Blade passes vertical. (Re-targeted forward: the first target was only 19 cm from the shoulder joint, which needs the arm folded shut.) |
| 40 | High raise | (-29, 155, 30) | 112 | -2 / -4 | Elbow above the shoulder, head starts to tip back. (Re-targeted forward, same reason.) |
| 50 | Overshoot | (-24, 180, 4) | 138 | -6 / -14 | Past the top, tip far behind the head. |
| 60 | Wobble back | (-25, 172, 8) | 128 | -4 / -10 | Over-correction. |
| 68 | Max arch | (-22, 188, 0) | 142 | -8 / -18 | Maximum telegraph pose. |
| 78 | Top hold (shiver) | (-22, 187, 0) | 140 | -8 / -18 | Shiver of about 2 degrees. |
| 84 | Top hold (shiver) | (-22, 187, 0) | 138 | -8 / -18 | Opposite shiver. Fingers tighten. |
| 90 | Release | (-22, 186, 2) | 138 | -8 / -18 | Last still frame. |
| 100 | **Contact** | (-26, 126, 52) | -5 | 8 / 20 | Hit zone. The chop is a single 10 frame segment from the release. |
| 106 | Impact overshoot | (-28, 98, 60) | -52 | 14 / 32 | Torso folds, tip about 57 cm above the ground. Left arm swings forward. |
| 114 | Rebound | (-28, 96, 58) | -45 | 12 / 28 | Sag. The punish window opens here. |
| 126 | Clumsy lift | (-30, 98, 50) | -10 | 10 / 20 | Shoulder first, wrist late. |
| 138 | Pull-back | (-32, 98, 44) | 25 | 8 / 14 | Second small sag near 130. |
| 150 | Re-grip | (-33, 101, 40) | 55 | 6 / 10 | Fingers loosen about 8 degrees and re-close. |
| 160 | Settle | (-34, 103, 37) | 68 | 4 / 7 | Almost ready. |
| 168 | Ready | (-34, 103, 37) | 71 | 3 / 6 | Velocity near zero. |

- **Chop toward the sword side:** the whole chop stays on the right of the body. Grip centre x: -22 (top), -26 (contact), -28 (impact), blade yaw about 8 degrees across, so the tip lands 5-25 cm to the right of the centre line.
- **Scene key frames (30 fps grid):** 0, 5, 10, 15, 20, 25, 30, 34, 39, 42, 45, 50, 53, 57, 63, 69, 75, 80, 84. Smallest gap 3. Clamped Bezier, set after the keys exist, no baked per-frame keys.
- **Strike speed:** the grip travels about 60 cm and the tip sweeps about 180 degrees in 10 frames, a tip speed near 12 m/s. Judged by its own check.

## Timeline summary
| Frames | Phase | Seconds |
|---|---|---|
| 0-10 | Anticipation dip | 0.17 |
| 10-78 | Raise, overshoot, wobble, arch | 1.13 |
| 78-90 | Top hold with shiver | 0.20 |
| 90-100 | **Strike** | 0.17 |
| 96-108 | **Hit window** (events) | 0.20 |
| 100-114 | Impact, overshoot, rebound | 0.23 |
| 114-168 | Recovery (punish window) | 0.90 |
Total 2.8 s. The telegraph, from the first movement to the release, is **1.5 s**.

## Quantitative checks (measured in the real mesh)
1. Reach: every target within reach, no hyperextended elbow, shoulder and clavicle in range.
2. Joint ranges: shoulder elevation up to about 160 degrees, elbow flexion 20-140, forearm roll within about 110 degrees total, wrist within 35 degrees of neutral, elbow flexion change under 6 degrees per frame outside the strike.
3. Clearance: blade, hand and forearm at least 4 cm from the skull, ribs and left arm at every key (the raise and the overshoot pass near the head).
4. Edge lead within 15 degrees at the hit window.
5. Hit zone across frames 96-108: tip 90-140 cm in front, 90-150 cm high, 5-25 cm right of the centre line.
6. Telegraph: tip above the skull top by frame 40, hold of at least 12 frames with a measurable shiver, telegraph pose at least 130 degrees of pitch.
7. Smoothness: wrist and elbow acceleration no worse than the Ready clip outside the strike. The strike has one acceleration and one deceleration.
8. Bookends: frames 0 and 168 match the Ready pose within 1.5 cm and 3 degrees.
9. Export: the clip reports 60 fps, 168 frames, and the two events sit at 1.60 s and 1.80 s.

## Workflow
1. **Pose stills first (stop for your review):** solve the key poses, render them in Blender with the real mesh and the sword (front, side, head-clearance), plus the numbers above.
2. Build `Skeleton_Overhead.casc`, key on the grid, interpolation, export binary (Blender previews) and ASCII (60 fps), contact sheet and scrubbable preview (`Skeleton_Overhead_Preview.blend`).
3. Polish, upsample to 60 fps, install `Skeleton_Overhead.fbx` in `Assets/Art/Animations/Skeleton/` (Humanoid clip `Overhead` 0-168, no loop, two events), save the final in `Final_60fps`.
4. Unity: `Weapon_Attack` and `OffHand_Attack` layers with transform masks, empty and `Overhead` states with one trigger, the layer-aware check, and Play Mode tests over Idle and Walk with the off-hand layer on and off.

## Risks
- **Solver range:** the ready-pose solver covers shoulder angles up to about 90 degrees; the overhead needs about 150-160 degrees (clavicle and upper-arm roll, one shared roll in Cascadeur). I will extend the solver and iterate with the clearance numbers, and tell you which number moves if a pose does not fit.
- **Head clearance:** the blade tip far behind an arched head and torso can touch the spine and skull. Measured at every key.
- **Exaggeration versus anatomy:** big arches and head throws may look wrong on a rigid-ribbed skeleton. The stills review is where this is tuned.
- **Mask and Hips:** verified only in Play Mode.
- **Upsampled 60 fps:** keys stay on the 30 fps grid, so the finest timing in the strike is a 5 frame (at 30 fps) segment. A single contact key makes the chop one segment; the speed profile is checked.

## Status (2026-10-21): key poses blocked, stills for review
- `Skeleton_Overhead.casc` holds all 19 keys on the planned grid (scene frames 0, 5, 10, 15, 20, 25, 30, 34, 39, 42, 45, 50, 53, 57, 63, 69, 75, 80, 84), keyed from solved sword targets: torso, right arm, counter-swinging left arm, right-hand grip (tightens at 84, plan frame), loose left hand. Interpolation is not set yet.
- Stills (real mesh and sword, front-3/4 and side): `Docs/Previews/Overhead_keys_A_frames0-50.png`, `_B_frames60-100.png`, `_C_frames106-168.png`.
- Measured: grip within 1 cm of target except keys 30 (2.4), 40 (3.9), 50 (3.9), 106 (1.4), 114 (3.2), 126 (2.1); blade angle within 1 degree; blade at least 4 cm clear of the skull at every key (tightest 4.2 cm at plan frame 60); shoulder elevated up to 154 degrees at the top, elbow flexion 68-113.
- Known compromises: the arm changes configuration between plan frames 40 and 50 (upper arm turns about 124 degrees in 10 frames) and between 106 and 114 (about 153 degrees in 8 frames); both are fast moves (the overshoot snap and the chop), but they depend on Cascadeur's interpolation between key positions, so the in-betweens need checking before polish. The roll constraint (edge leads) is strict only for the chop keys 100-114; the telegraph keeps the ready pose's roll.
- Solver: `Scripts/overhead_rig.py` (per-key candidates plus a smooth-path search), `dp_blender.py`, `finalize_path.py`, `overhead_key.py`. Solutions: `Scripts/overhead_final_pose.json`.

## Status (2026-10-21, later): poses approved, interpolation set, in-betweens measured
- Poses approved by the user. Clamped Bezier is set on all 19 keys and `Skeleton_Overhead.casc` is saved. Scrubbable preview (30 fps grid, 85 frames, real time 2.8 s): `I:\Art\Blender\Skeleton\Exports\Skeleton_Overhead_Preview.blend`.
- Measured on every in-between frame in the real mesh (`Scripts/overhead_check.py`): the sword never comes within 13.6 cm of the body (closest at the dip), nearest approach to the skull 16 cm, the right arm never closer than 10.8 cm to the skull. The chop is one smooth acceleration and one deceleration, tip speed 17 -> 41 -> 55 -> 53 -> 44 -> 33 -> 24 -> 14 cm per 30 fps frame (peak about 16 m/s at the tip, mid-strike), and the raise snap peaks at 14 cm/frame. No stutter.
- Weak spot: the top hold moves only about 0.5 cm/frame at the tip (about 1 degree in total), so the shiver is hard to read. Polish should roughly triple it.
- Still to do: polish (shiver, wobble, loosen the recovery), export (binary for Blender, ASCII upsampled to 60 fps), Unity import with the two events, layers and the layer-aware attack check.


## Revision 4 (2026-10-21): user feedback after scrubbing the first blocking
User notes: (1) the arm snaps back unnaturally between scene frames 25 and 30 (plan 50-60); (2) **use the legs in the attack too, layered over the base** (the clip is full body, so the attack layers need to include Hips and legs and the clip is authored with its own weight shift and step); (3) several frames hold completely unnatural poses (for example scene frame 56, the folded, hanging-head rebound pose held almost still); (4) add follow-through; (5) reference: Dark and Darker's Skeleton Footman (heavy committed swings, the body travelling through the strike).

Cause of the snap: the arm solutions for keys 50 and 60 sat in a different arm configuration from keys 40 and 68, and Cascadeur interpolated between them (a 100+ degree forearm change in 10 frames). The old solver searched joint angles directly and could not make a continuous path; it is replaced by an exact arm solver (shoulder, elbow swivel and forearm roll chosen as continuous curves).

Changes to the choreography (plan frames at 60 fps, 180 frames = 3.0 s; the telegraph is still 1.5 s to the release at frame 90):
- **Legs and hips:** the weight rocks back during the windup (hips back 12 cm, standing slightly taller at the top); at the release the right foot **slides forward 34 cm** (a dragging, scuffing step, which suits an undead) while the hips lunge forward 22 cm and drop 6 cm; follow-through drives them to +32 cm and -10 cm; the recovery shuffles the right foot back and the hips return over 60 frames. Feet stay planted otherwise.
- **Torso twist:** the torso winds 13 degrees to the right in the windup and unwinds 14 degrees to the left through the chop, so the swing comes from the body.
- **Follow-through:** the sword sweeps to about 30 cm off the ground (grip 88 cm high, blade 62 degrees down) at frame 106, bounces back up about 10 cm at 114 as the torso springs back, then drags up during the recovery. No pose is held: every key differs from its neighbours in the strike and recovery (only the top hold is a hold).
- **Head:** tracks the target (it compensates the torso fold) instead of hanging; it still lags and throws back at the arch.
- **Top shiver:** doubled: about 6 degrees of blade pitch, 2 cm of grip and 1.5 degrees of chest at keys 80 and 86.
- **Smoother raise:** the overshoot-then-wobble keys (50 / 60) are replaced by a continuous rise to the arch; the cocked blade tops out at 142 degrees at frame 70.
- **Layers:** `Weapon_Attack` now includes Hips and legs (a full-body override, with root motion kept off) and `OffHand_Attack` still carries the left arm. Over Walk the attack replaces the legs, so the enemy stops stepping and lunges (like the reference).

## Status (2026-10-21, evening): revision 4 blocked, interpolated, measured
- **Arm solver replaced** (`Scripts/overhead2_rig.py`): exact inverse kinematics from the sword pose; the elbow swivel and forearm roll are chosen as continuous curves by dynamic programming, and the upper-arm twist blends from the approved ready pose's split. Rotation change between neighbouring keys stays at 5-45 degrees through the whole raise and hold (it was 100+). The arm solver reproduces the approved ready pose (forearm orientation within 0.15 degrees, elbow exact).
- **Full body:** hips shift back 12 cm in the windup, the right foot slides 32 cm forward as the hips lunge 18-26 cm and drop 5-8 cm, torso twist -13 to +14 degrees, head tracks, counter-swinging left arm. Keys (plan frames): 0, 10, 20, 30, 40, 50, 60, 70, 76, 82, 88, 94, 100, 106, 114, 126, 138, 150, 162, 172, 180 (21 keys; scene frames 0, 5, 10, 15, 20, 25, 30, 35, 38, 41, 44, 47, 50, 53, 57, 63, 69, 75, 81, 86, 90).
- **Changed from rev. 4 text:** the release is at frame 88 (not 90) and a mid-chop key at 94 steers the arm around the skull, so release-to-contact is 12 frames (the first 4 are the start from the hold). Hold shiver keys are at 76 and 82. Fold at impact reduced (chest 28, spine 10) and lunge depth reduced after the first blocking folded the torso to about 50 degrees.
- **Measured over every frame** (`Scripts/overhead_check2.py`): no frame touches the skull (closest 12.2 cm) or body (sword 12.7 cm), tip never below 38 cm off the ground, no near-still frame outside the top hold and the settle into ready. Tip speed peaks at about 17 m/s mid-chop and falls through the follow-through.
- Files: `Skeleton_Overhead.casc` (saved, clamped Bezier), scrubbable `Exports/Skeleton_Overhead_Preview.blend`, sheets `Docs/Previews/Overhead_v2_keys_*.png`.
- Still to do: polish pass on the user's notes, export (binary and the 60 fps ASCII), Unity import with `ActivateWeapon` at 96/60 s and `DeactivateWeaponH` at 108/60 s, `Weapon_Attack` layer mask including Hips and legs (root motion off), `OffHand_Attack`, and the layer-aware attack check.

## Revision 5 (2026-10-21, night): frame-by-frame review and fixes
**User review:** the arm snapped at Blender frames 44-45 and 68-69, and the animation lacked life. We stepped through all 91 frames.

**Snap, root causes (both mine, now fixed):**
1. The arm's elbow-plane reference direction ("down, projected") became unstable when the arm pointed straight up, so a 24-degree change in my swivel number was a roughly 140-degree swing of the elbow. Swivel is now measured from a stable reference (inward, +X) and chosen by dynamic programming with the whole swivel route checked for skull clearance.
2. The rig's arm solver gets the elbow side from the `RightLowerArm_LimbDir` pole point; I was writing the rest-pose direction (mostly along the arm), which became parallel to the shoulder-wrist axis at frames 43/44 and 67/68, so the in-between elbow flipped 37-39 cm in one frame. The pole is now written sideways toward the elbow (40 cm), so it is always well conditioned.
Result: no per-frame joint rotation spike anywhere (largest 30 degrees, in the chop), elbow travel at most 20 cm per frame (in the chop), arm to skull at least 13 cm, sword to body at least 12.7 cm, tip at least 37 cm above the ground.

**Frame-by-frame findings and what was done:**
- Raise 6-21: one steady speed, rigid pendulum -> now a hitch at the shoulder line (frame 38), small lift irregularities, anticipation dip kept.
- Hold 33-44: frozen, knees locked at 179 degrees, hips 1 cm higher than rest -> hips now lower than rest in the hold (knees 150-165), a decaying tremble and sway (torso, hips, blade pitch), and a coil into the release (hips down 3 cm, chest back 2 degrees more).
- Release 42-46: explosion from stillness -> coil first, grip tightens at frame 84.
- Step 45-54: right foot skated flat for 34 cm -> it now lifts (to 13.7 cm) and stomps, with a small bounce.
- Impact 51-57: nearly still, no recoil -> damped bounce through hips, torso and sword, and the torso winds through.
- Recovery 57-90: uniform rise -> stalls and lurches (pitch and grip oscillate while it rises).
- Key density: 31 keys on a 6-plan-frame (3 scene frame) grid instead of 21, so the shape is controlled throughout.
Scripts: `Scripts/overhead2_rig.py` (arm solver, anchors plus secondary-motion generator), `overhead_key2.py`, `overhead_check2.py`, `rates.py`, `bodyrows.py`, `review.py`. Solution file `overhead_ik5.json`.

## Rev 6 - ground-up redesign (power chain, active left arm)
- Sword arm: shorter reach at contact (grip ~54 cm out), stricter limits; measured elbow bend 71-125 deg, wrist bend max 55 (ready baseline), no rotation spikes, max per-frame rotation 21 deg (sword arm).
- Left arm now takes part: raises with the windup, clenched fist, drives down/in across the strike (scene frames 45-54), relaxes to the exact limp ready arm at both ends. On a shield variant the OffHand_Attack layer is weight 0.
- Legs/hips lunge: right foot lifts and plants at contact, hips drop and travel forward, chest follow-through.
- Files: I:\Art\Cascadeur\Skeleton\Skeleton_Overhead.casc, preview I:\Art\Blender\Skeleton\Exports\Skeleton_Overhead_Preview.blend, sheets Docs/Previews/Overhead_v6_*.png (scene frames, 30 fps grid).

## Rev 7-13 (user review rounds)
- 7: sword arm near-straight at the strike, left arm swung behind for balance, whole body (hips + spine + chest) folds forward after the strike.
- 8: shoulder bookend jolt fixed (ready shoulder blended in/out), recovery reaches ready earlier with a small settle, relaxed left thumb (fist peak 1.8, thumb tucks as the hand closes).
- 9-10: arm held ~15 deg bend through the whole downswing (target projected onto the reach sphere); swing keyed every scene frame 44-56 so Cascadeur interpolation does not shorten the arm.
- 11-12: gradual straightening through the hold (plan 62-90); strike path moved to ~12-15 cm off centerline, eased inward only after the arm clears the head (skull clearance ~10 cm).
- 13: elbow bend side transported with the arm during the swing (no flip); wrist bend now ~80 deg during the swing (trade-off); small over-recovery wobble (chest/spine back past ready, hip sway, chest twist) at plan frames ~138-170.
- Final: `I:\Art\Cascadeur\Skeleton\Skeleton_Overhead.casc`, `Final_60fps\Skeleton_Overhead.fbx` (60 fps, clip 0-180, no loop). Events: ActivateWeapon(1) at 96/180, DeactivateWeaponH at 108/180 (normalized times 0.5333 / 0.6).

## Toe fix (2026-10-21)
The toe phalange joints are not driven by any point, so they kept rest rotations while the foot moved: the right little toe distal bone drifted 1-3 cm relative to the foot in the v13 export. `Scripts/overhead_key3.py` (same IK, `ik14.json`) now sets every toe phalange rotation to follow the foot at each key. Re-keyed `Skeleton_Overhead.casc` (backup `Skeleton_Overhead_v13_backup.casc`), re-exported at 60 fps to `Final_60fps/Skeleton_Overhead.fbx` and the Unity project. Verified: the toe stays fixed in foot space on every frame, the arm numbers are unchanged (elbow 15 degrees through the swing, max hand rotation 31 degrees per frame), the Unity clip is 3 s at 60 fps with the hit events at 1.6 s and 1.8 s, and a Play Mode check shows the hitbox opening once and closing once.
Check of the other clips (2026-10-21): Idle, Walk and Ready show no toe drift when measured in their parent Toes bone frame (spread 0.00 cm; 0.17 cm in Ready), so they were left as they were. The Toes bone itself bends with the heel lift in Walk, which only looks like drift when measured in the Foot frame.


## Gameplay retiming (2026-10-23)

The user requested an overhead lasting 0.8-1.3 seconds. The current gameplay version is **66 frame intervals / 1.1 seconds at 60 fps**, played at Animator speed 1.

| Phase | Original export frames | Current export frames | Duration |
|---|---|---|---|
| Wind-up and coil | 0-90 | 0-27 | 0.45 s |
| Strike and follow-through | 90-114 | 27-51 | 0.40 s |
| Recovery | 114-180 | 51-66 | 0.25 s |

The strike retains the original samples and speed. ActivateWeapon(1) moves from frame 96 to 33 (0.55 s); DeactivateWeaponH moves from frame 108 to 45 (0.75 s). Both Overhead and Overhead_OffHand import ranges end at 66; the off-hand clip has no damage events. Both the test and gameplay controllers blend back to Empty over 0.1 s after clip completion (about 1.2 s including this final blend).

Retiming is applied at the export stage. The editable Cascadeur master `I:/Art/Cascadeur/Skeleton/Skeleton_Overhead.casc` retains its original 30 fps, 3-second timeline. The current 60 fps FBX in `Final_60fps` and the Unity source/gameplay clips contain the shortened timing. Before replacing a fresh original-length export, run `I:/Art/Cascadeur/Skeleton/Scripts/retime_overhead.py input_3s_60fps.fbx output_1p1s_60fps.fbx`. The script checks the original 181-sample input and preserves strike samples. It must not be run on an already shortened export. The original export is preserved in `Final_60fps/Backups/Overhead_3s_20261023/Skeleton_Overhead.fbx`.


## Approved motion and Unity installation (2026-10-23)

The user approved the final overhead after scrubbing, including the elbow path and the corrected left thumb. The installed clip is **1.3 seconds / 78 intervals at 60 fps**, with the damage window still at **0.55-0.75 seconds**. The earlier 1.1-second export-stage retiming above is superseded.

The approved source is `I:/Art/Blender/Skeleton/Exports/Skeleton_Overhead_Approved.blend`. It includes the larger continuing cross-body follow-through, eased recovery, planted left foot, outward elbow throughout the raise/backswing/return, and stable modest left-thumb curl. The older Cascadeur `.casc` remains historical; the current animation FBX at `I:/Art/Cascadeur/Skeleton/Final_60fps/Skeleton_Overhead.fbx` and `Assets/Art/Animations/Skeleton/Skeleton_Overhead.fbx` is exported directly from this approved Blender source, with 82 humanoid bones and no Piece bones.

This FBX has metre-valued translation curves. `SkeletonGameplayClips` therefore does not apply the legacy Cascadeur 100x translation correction to Overhead. Curve resampling and compression are disabled for this asset to preserve the approved motion between displayed frames. Import take is `Scene`, source frames 1-79; both gameplay clips are 1.3 seconds. The off-hand clip has no damage events. Animator speed remains 1 and the return blend remains 0.1 seconds.

The gameplay Ready arm is aligned to the approved overhead entry by applying the approved rotation offset to its upper arm, forearm and hand curves. Its existing small looping motion is retained. This alignment runs as part of rebuilding clips, so a rebuild cannot restore the old inward elbow at the transition. Idle and the other animation exports are unchanged.

Direct comparison of all 82 humanoid bone heads across 157 time samples in Unity and the approved Blender source found maximum position error 0.0083 mm. Regression checks cover outward elbow, planted left foot, stable hand-relative thumb, matching Ready entry, and hit events.

Validation completed: all 6 SkeletonGameplayAnimationTests (EditMode) and all 5 SkeletonOverheadStateMachineTests (PlayMode) pass. PlayMode tests use the gameplay hierarchy/controller, omit network behaviours, and allow the spawn/rise state to finish before attacking. The hitbox activates once at 0.55 s and deactivates once at 0.75 s; both attack layers return correctly.
