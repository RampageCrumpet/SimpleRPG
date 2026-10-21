# Idle: detailed plan (for review, nothing animated yet)

Beads: `simplerpg-1rf.2` (epic `simplerpg-1rf`). Overall plan: `Docs/SkeletonAnimationPlan.md`.

## Purpose and constraints
- Shared base-layer clip. Every other clip (Walk, Rise crossfade, weapon layer) is judged against this pose, so it stays **neutral**: no sword pose, no attack hints.
- 30 fps, **90-frame loop (3.0 s)**, frames 0-90 keyed, frame 90 identical to frame 0.
- Humanoid-retargetable. Humanoid bones only (no `Piece_*`); `Root` does not move. Hips height is baked into the Hips bone.
- Style: slow, shambling, slightly uncoordinated undead. It does **not breathe** (it is a skeleton): motion comes from a slow sag, creaking weight shifts and a loose head, not from chest rise and fall.

## Base stance (frame 0 = frame 90)
Values are starting targets in the rig's T-pose rest. Adjust in Cascadeur by eye.
| Part | Target |
|---|---|
| Feet | ~0.28 m apart (rest is 0.15 m), toes turned out ~10 degrees, both flat on the ground |
| Knees | Soft: upper leg ~6 degrees forward, lower leg ~10 degrees back; hips lowered ~2 cm from rest height |
| Pelvis | Roll 2 degrees (right side low), yaw 2 degrees |
| Spine chain | Forward lean total ~9 degrees (Spine 3, Chest 3, UpperChest 3) |
| Neck / head | Neck 6 degrees forward, head droops 10 degrees, tilted 6 degrees to the right |
| Shoulders | Left shoulder drops 3 degrees more than the right |
| Arms | Hanging from the T-pose about 68 degrees down, elbows bent 12 degrees (left slightly more), hands relaxed and slightly open. Both arms hang plain: the sword arm is handled by the weapon layer |
| Fingers | Loose partial curl, left hand a little tighter than the right |
| Jaw | Slightly open, 3 degrees |

## Timeline (30 fps)
| Frames | Beat |
|---|---|
| 0-28 | Slow sag onto the **left** leg: hips drop ~2 cm and shift ~3 cm left, shoulders tilt the other way. Head follows 4 frames late. |
| 28-40 | Hold, tiny tremor in the head and left hand. Left index and middle fingers twitch once (curl ~12 degrees, frames 34-40). |
| 40-52 | Late, uneven correction: the hips come back first, the torso follows 3 frames later, the head 6 frames later. The right foot heel lifts a few mm as weight transfers. |
| 52-60 | Head jerk: head snaps up ~8 degrees in 4 frames, jaw drops a little more, then slumps back in 6 frames. |
| 60-80 | Drift to the **right**, shallower than the left sag (about 60% amplitude). Arms swing slightly behind the torso. |
| 80-90 | Return to the frame 0 pose with a small damped overshoot, ending at rest velocity matching frame 0. |

Asymmetry is intentional: the left sag is bigger and longer than the right drift, the finger twitch and head jerk happen once per loop, and no beat is timed on an even interval.

## Contact and loop rules
- Both feet stay planted: no sliding, only a slight heel lift at 40-52. Use Cascadeur's ground contact / foot locking and check it.
- Loop closure: pose and velocity at frame 90 equal frame 0. Compare frames 0 and 89 for velocity as well as pose.
- Nothing moves more than ~3 cm at the hips, ~4 cm at the head, or more than ~12 degrees at any single joint from the base stance.

## Workflow
1. Blender: build the humanoid-only skeleton for Cascadeur (`simplerpg-1rf.1`) and confirm FBX export from Cascadeur works.
2. Cascadeur: block key poses at frames 0, 28, 40, 52, 60, 80, 90 (7 keys) and send you preview stills or a quick video for review.
3. Polish timing (the overlaps and lag are the style), loop check, contact check.
4. Export FBX at 30 fps, frames 0-90; Unity import with Loop Pose and a check of the loop mismatch.

## Acceptance checks
- Frame 0 and frame 90 match; the loop shows no pop.
- No foot slide; knees do not hyperextend; no elbow snaps or flips.
- Reads as undead and uncoordinated at normal speed, without looking broken.
- Neutral enough that the weapon layer and Walk blend without twisting.

## Questions for you
1. Weight on the left leg first, as planned, or the right?
2. Should the sword arm hang plain in the base Idle (my plan, with the weapon layer overriding it), or should the base Idle already hold the arm slightly bent?
3. Is a rare longer variation (a slow head turn every few loops) wanted? I left it out of the MVP.
