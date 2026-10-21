# Walk forward: detailed plan (for review, nothing animated yet)

Beads: `simplerpg-1rf.3` (epic `simplerpg-1rf`). Overall plan: `Docs/SkeletonAnimationPlan.md`. Idle (approved) is the reference stance: `Docs/IdlePlan.md`.

## Purpose and constraints
- Shared base-layer clip: the skeleton pursuing the player. Slow, shambling, uncoordinated undead.
- 30 fps, loopable, Humanoid-retargetable, humanoid bones only (no `Piece_*`). Starts from and returns to the Idle stance family (same feet width, toe-out and lean), so Idle <-> Walk crossfades stay clean.
- The weapon layer masks the upper body (Spine and up), so on a skeleton holding a sword the **legs and Hips** are what the walk must get right. Arm and torso motion only shows on the unarmed arm and the torso under the mask. Author it anyway so an unarmed skeleton reads correctly.

## Numbers I checked against the rig
- Thigh 47.1 cm, shin 40.7 cm, maximum leg reach 87.8 cm. Idle hip joint height is about 94.7 cm, ankle height 6.7 cm.
- A foot 25 cm ahead of or behind the hip needs a hip-to-ankle distance of about 90 cm at idle hip height, which is **more than the leg can reach**. So at heel strike and toe-off the pelvis must drop about 4 cm and the knees bend. That fits a crouched shamble. It also means the pelvis bobs 3.5-4.5 cm per step.
- Stance excursion: speed times stance time. A target of about 0.44 m/s with a stance of 32-34 frames gives about 47-50 cm of foot travel per stance (about +-25 cm), which stays inside leg reach once the pelvis drops.

## Timing (54-frame cycle = 1.8 s, 0.8 m per cycle, about 0.44 m/s)
Deliberately uneven. The left leg is the dragging leg.
| Frame | Event |
|---|---|
| 0 | Left heel strike (also frame 54, the loop point) |
| 6 | Right toe-off (double support 0-6) |
| 6-28 | Right swing (22 frames) |
| 28 | Right heel strike |
| 34 | Left toe-off (double support 28-34) |
| 34-54 | Left swing (20 frames), low, toe skimming the ground |
Left stance 34 frames, right stance 32. Heel strikes are 28 and 26 frames apart, so the rhythm limps and the step lengths differ (right foot lands about 41 cm ahead, left about 38.5 cm). Double support is about 22% of the cycle, typical for a slow walk.

## Key poses (11 keys, Bezier between, same method as Idle)
| Frame | Pose | Pelvis (relative to Idle base) | Notes |
|---|---|---|---|
| 0 | Left contact | height -4.0 cm, roll toward right 2 deg, yaw -4 deg (left hip forward) | Left heel lands, toe up ~15 deg. Right arm forward. |
| 4 | Left down (loading) | height -4.5 cm (lowest), shift left 1.5 cm | Left foot flat, knee flexes ~25 deg. |
| 11 | Right early swing | height -2.0 cm, shift left 2.7 cm | Right toe drags a moment after toe-off. |
| 17 | Left midstance / right passing | height -0.3 cm (highest), shift left +3.2 cm, roll toward right 4 deg | Right foot clears 3.5-4 cm, passes the stance knee. Weight clearly on the weak left leg. |
| 23 | Right late swing | height -2.2 cm | Right leg extends, foot reaches forward. |
| 28 | Right contact | height -4.2 cm, yaw +3 deg (right hip forward) | Right heel lands, shift passes through centre. |
| 32 | Right down (loading) | height -4.6 cm (lowest) | Left heel starts to rise. |
| 38 | Left early swing | height -2.2 cm, shift right 1.5 cm | **Scuff hitch:** left toe catches the ground for ~2 frames (hips hitch 1.5 cm, torso lurches forward). |
| 44 | Right midstance / left passing | height -0.5 cm, shift right -2.6 cm, roll toward left 2.5 deg | Left foot clears only 1.2 cm and drags the toe line. |
| 49 | Left late swing | height -2.5 cm | Left foot swings short and lands flat-toed. |
| 54 | = frame 0 | | Loop closes. |

## Other body parts
- **Torso:** about 4 degrees more forward lean than Idle (Spine 5, Chest 9, Neck 8, Head 10 degrees). Chest counter-rotates against the pelvis, about 6 degrees. The torso lags the pelvis by 3 frames.
- **Head:** lags the torso 5 frames, with a 3 degree nod on each heel strike and a slow tilt toward the stance side.
- **Arms:** uneven and late. Right arm swings about 16 degrees (opposite to the left leg), left arm only about 8 degrees and 6 frames late; both elbows stay bent 20-30 degrees. A small outward flare on the right arm at 17 and 44.
- **Feet:** toe-out 10 degrees as in Idle; the dragging left foot swings out to about 18 degrees during its swing. Toe-off roll ~20 degrees, heel-strike dorsiflexion ~15 degrees.
- **Fingers and jaw:** loose, jaw rattle of ~2 degrees on each heel strike. Polish items.

## Root motion and Unity
- Author the walk in world space with feet planted during stance and the pelvis travelling forward at constant velocity; export includes the forward travel (0.8 m per cycle).
- Unity: use the root motion speed (about 0.44 m/s) as the base and scale it with Animator speed or let the NavMeshAgent speed match it. Foot slide appears if the agent speed and the clip speed disagree.
- Loop Time and Loop Pose on; root rotation and height baked, XZ not baked (root motion on).

## Contact and quality rules
- Stance foot slide under 0.5 cm per stance; no foot or toe passes through the ground; knees never hyperextend (always at least 8 degrees of flexion, maximum about 62 degrees in swing).
- Pelvis height stays within the leg reach limit at every frame (no stretching).
- Loop closure: pose and velocity at frame 54 equal frame 0, apart from the forward travel.
- Feet width and toe-out match Idle so the Idle <-> Walk blend does not twist the legs.

## Work needed beyond the Idle tooling
The Idle solver keeps both feet planted for the whole clip. Walking needs three additions:
1. **Moving planted feet:** each foot's world position fixed during its stance and following a swing arc otherwise (hip-to-ankle leg IK already exists).
2. **Foot roll:** heel-strike dorsiflexion, flat foot, and toe-off pitch, using the foot and toe rig points.
3. **Pelvis travel and sway** in world space, plus the Root joint following the pelvis.
I will extend the helper scripts for these and check them on a dry run (no scene writes) before keying anything.

## Workflow and review
1. Extend the helper scripts and dry-run the 11 poses for leg reach, knee angles and foot contact.
2. Block the 11 key poses in `Skeleton_Walk.casc` (a separate project from the clean rig), render a contact sheet like the Idle one, and **stop for your review**.
3. Polish (arm lag, head nod, fingers, jaw rattle), loop and contact checks, export, Unity import test with loop and speed measurement.

## Questions for you
1. **Target speed.** About 0.44 m/s is slow, and a long step would need a faster, less shambling stride (leg reach limits step length to roughly 0.7 m). Is that speed fine, with the Animator speed scaled up to about 1.5x when chasing? Or do you want a faster base walk?
2. **Dragging leg.** Left, as planned, or right?
3. **Scuff hitch.** Keep the stumble-like toe catch at frame 38 in every loop, or leave it out so the walk reads smoother?

## Revision 2026-10-21 (after blocking review)
- **Head stabilized at the scuff hitch.** The hitch is now a smooth gaussian bump (center frame 41, about 9 frames wide) with smaller amplitudes (hips dip 0.4 cm, pelvis pitch 0.4 deg, chest lurch 0.65 deg), and the neck and head counter-rotate (0.6 and 0.8 deg) so the head's pitch stays steady. Head peak acceleration at the hitch fell from 1.05 to 0.39 cm/frame^2 (2nd cycle, frames 92-94).
- **Sparse keys instead of a key on every frame.** 49 keys over frames 0-162 (3 cycles) on a regular 3-4 frame grid (cycle frames 0,3,6,10,14,18,21,24,28,31,34,37,41,44,47,50) with clamped Bezier sections; Cascadeur interpolates everything in between. Baked per-frame keys were removed.
- **Other smoothing:** swing foot path is a C1 Hermite curve that matches the stance exit and entry velocities, the ground clamp is smooth, and the heel-strike nod is wider and smaller (2.5 deg).
- Measured in the scene (frames 50-115): jerk hips 0.18, head 0.46, feet 0.24-0.26, knees 0.43-0.57; ankle drift 0.000; loop closure 0.000 cm. Use frames 54-108 as the loop.
