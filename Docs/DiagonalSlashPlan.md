# Diagonal slash: plan, revision 1 (for review before animating)

Beads: `simplerpg-1rf.6`. Reference: the approved overhead (`Docs/OverheadPlan.md`, revision 13) and the ready pose. Start and end pose: the approved ready pose. Same sword, socket, layers and masks as the overhead.

## Carried over from the overhead (decided by the user, applied here unless you say otherwise)
- **60 fps**, authored on the 30 fps scene grid and upsampled; one clip, Humanoid, no loop.
- **Layers:** `Weapon_Attack` (torso, head, right arm, **legs**) plus `OffHand_Attack` (left arm, an event-free copy of the clip); the off-hand layer is off when a shield is held. New trigger `Slash` and state `Slash` tagged `Attack`.
- **Powerful, not clean:** the whole body is in the swing (hips and legs step and drop, torso folds and twists), the **left arm balances it** by swinging the opposite way, and the sword arm stays **close to straight for the whole cut**.
- **Follow-through, then a small over-recovery wobble**, so the skeleton looks unable to control its own weight.
- **Exaggerated telegraph** so the player can read it and dodge.
- Strike path slightly off the centre line but in line with the swinging arm.
- No frozen unnatural poses, no elbow flips, no snaps; checked frame by frame the same way (per-frame rotation, elbow bend direction, skull/body clearance, sword height).

## What must read differently from the overhead
| | Overhead | Diagonal slash |
|---|---|---|
| Windup | sword straight up, arched back | sword drawn **back and out to the right**, blade cocked near horizontal, torso wound **right** |
| Cut | vertical, front, top to bottom | **high right to low left** across the body |
| Body | folds forward | **rotates**: torso unwinds right to left, hips lead |
| End | sword low in front | sword across the **left hip**, torso wrapped left |
| Dodge | step aside | **back or duck** (a sideways step runs into the cut) |
The frame-level check: the blade's travel direction in the hit window is 40-50 degrees from vertical toward the left, where the overhead is within 10 degrees of vertical.

## Timing (60 fps, even frames only)
| Frames | Phase | Notes |
|---|---|---|
| 0-12 | Anticipation | the sword dips and the body hunches slightly (the same tell as the overhead) |
| 12-66 | Windup (about 0.9 s) | torso winds right, sword drawn back over the right shoulder with a shoulder shrug and a lurch; the left arm swings forward and across for counterbalance |
| 66-78 | Hold | clear freeze with a shiver, the fingers tighten once |
| 78-92 | **Slash (14 frames, 0.23 s)** | hips lead, then torso, then straight arm; the right foot steps and plants at contact |
| 92-104 | Follow-through | sword ends low across the left hip, torso wrapped left, a rebound |
| 104-150 | Recovery | uneven, shoulder first then elbow then wrist, a second sag |
| 150-166 | Over-recovery | the torso comes up past upright, sways, and settles |
| 166-172 | Settle | exact ready pose at the end |
Total about **172 frames (2.9 s at 60 fps)**.
Hit window: **frames 84-98**: `ActivateWeapon(1)` at 1.40 s, `DeactivateWeaponH` at 1.63 s (events as normalized times in the import settings, 0.488 and 0.570).

## Key targets (cm, hips-relative like the overhead; refined when solved)
| Frame | Grip centre | Blade | Torso (twist, fold) | Notes |
|---|---|---|---|---|
| 0 | ready (-34, 103, 37) | pitch 71 | 3 / 6 | matches Ready |
| 66-78 | (-40, 150, -14) | pointing back and right, about 20 degrees above horizontal | twist right about 35, lean back 8 | the telegraph pose |
| 84 | (-26, 138, 62) | about 40 degrees above horizontal | twist right 5 | slash start, arm already near straight |
| 90 | (-8, 112, 72) | about 25 degrees below horizontal | twist left 20 | the cut, mid-point |
| 98 | (+14, 82, 58) | pointing down and left | twist left 35, fold 30 | end of the cut, across the left hip |
| 104-110 | (+10, 86, 54) | | wrapped left | rebound |
The exact numbers come out of the arm solver and are changed there if clearance or reach fails.

## Risks I already know about
- A straight right arm swinging across the body can brush the **chest and the left arm**; the left arm will be held back and low during the cut to stay clear.
- Torso twist plus a straight arm pushes the wrist toward its limit (it hit about 80 degrees in the overhead). I will try to keep the wrist under 60 by choosing the sword roll along the cut, since the blade edge only needs to lead the cut (edge within 15 degrees of travel).
- The cut crosses the centre line; the clip is authored for one skeleton and is not mirrored.

## Decisions for you
1. **Windup length.** Recommended **0.9 s of windup** (1.4 s to the hit). The overhead telegraph is 1.5 s. A shorter slash makes the two attacks feel different and trains different reactions. Alternative: 1.5 s as for the overhead.
2. **Legs.** Recommended: a **forward step** with the right foot at contact (like the overhead's stomp). Alternative: a pivot in place.
3. **Cut direction.** The plan above is high right to low left (the master plan). Reverse (low left to high right) would be a different attack.

## Blocking v1 (2026-10-21, approved answers: 0.9 s windup, forward step, high right to low left)
- Authored as `I:\Art\Cascadeur\Skeleton\Skeleton_Slash.casc` (39 keys on the 30 fps grid, scene frames 0-86 = plan frames 0-172). Generator `Scripts/slash2_rig.py`, keying `Scripts/slash_key.py`, solution `Scripts/slash_ik4.json`.
- The cut (plan frames 78-98) is keyed every scene frame with the arm held at a 15 degree bend; the cut ends at about 28 cm left of the centre line, hand 73 cm high, blade pointing down-forward-left.
- Edge-leading roll was tried and dropped: it forced the wrist past 100 degrees. The blade keeps the ready grip roll; wrist flex stays at or below about 70, no per-frame spikes (max single-frame rotation 24 degrees).
- Hit window planned at plan frames 84-98 (scene frames 42-49).

## Blocking v2 (user notes: elbow turned backwards, more follow-through and recovery, whole body)
- Elbow: during the cut the bend side is forced to point **back** (and slightly down/out), never forward; the DP now measures swivel and forearm roll as periodic angles (a 36 / -324 degree roll was being treated as a 360 degree jump). Cost: the wrist bends to about 77 degrees during the cut.
- Follow-through: the sword carries on past the contact to low left (hand about 55 cm high, 37 cm left, 55 cm forward), the straight arm holds until frame ~110 and releases by ~124, torso twist keeps going (sum about 57 degrees left), left arm swings back and out further.
- Whole body: hips yaw with the torso (half of the chest twist), deeper crouch (hips down about 14 cm) and a longer forward lunge (hips 26 cm forward at contact), head tracks the target against the torso twist, the windup pulls the hips further back and lower. Recovery is slower (the sword rises from low left in an arc clear of the left leg; sword/body clearance at least 13.8 cm).
- Files: `Scripts/slash2_rig.py`, `slash_key.py`, `slash_ik13.json`.

## Redesign: horizontal sweep (user decision 2026-10-21) - four key poses blocked
Broad lateral cleave, right to left at upper-chest/shoulder height. Constraints from the user: arm near full extension through the middle 60 percent of the swing, visible daylight between hand/weapon and torso, finish at the left waist/rib area well outside the silhouette (not beside the thigh), large hip and torso rotation with a step into it, much less crouch, one elbow bend plane established in the windup and kept through the attack.
- Poses (plan frames 72 / 90 / 100 / 110; `Skeleton_Sweep_Poses.casc`, stills `Docs/Previews/Slash_sweep_poses.png`): windup hand (-71, 137, -18) with the blade cocked up behind the right shoulder and the arm wide; mid-sweep hand (-15, 129, 87) arm 15 degrees from straight; end of sweep (57, 120, 65); recovery start (58, 111, 56). Sword to body clearance 41-50 cm. Blade held about 68 degrees from the forearm so the wrist stays at or below 18 degrees in the sweep (49 in the windup).
- Elbow bend side points down in all four poses (y -0.68 to -0.79).
- Not yet verified: the transition frames (a roll jump near plan frame 60 in the solution, about 100 degrees of forearm change in one key interval).
- Scripts: `Scripts/slash2_rig.py` (blade held at a fixed angle to the forearm, elbow plane target from the windup), `slash_key.py`, `slash_ikw3.json`.

## Horizontal sweep v1 (full clip keyed; user note: frame 45 not outstretched enough, poses otherwise right)
- `Skeleton_Slash.casc` is now the sweep (39 keys, 172 plan frames). Arm bend 12 degrees from plan frame 88 to 104 (reach limit clamp lowered, hips and chest lean further forward at contact); hand at (-33,133,84), (2,126,98), (36,128,86), (57,120,65), (55,110,55) across scene frames 44-56.
- Fixed the plan-frame-60 jump: the early windup rows still had the old blade angles; all rows from 36 now hold the blade at a fixed angle from the forearm (85 degrees in the early windup, 68 later), which also keeps the wrist at or below 35 degrees in the sweep.
- Elbow bend side points down from the windup to recovery (y -0.66 to -0.86), except frame 54 where the arm is almost straight and the side is undefined. Max joint rotation per frame 17 degrees, no spikes. Sword 12.7 cm from the body at the closest (in the recovery), arm 11 cm from the skull.

## Horizontal sweep v2 (notes: hand moved out before swinging, left hand too far forward, blade vertical, unnatural)
- Windup path: the hand now draws back and out in one monotonic path (extra windup keys at plan frames 42 and 54) to the hold at (-77, 138, -22); the arm is nearly straight (12 degree bend) from scene frame 34 through the whole sweep, so the swing is a rotation around the shoulder and torso with no outward move.
- Blade: horizontal through the sweep (tip and grip within 0-5 cm of the same height, scene frames 40-56), angled 40 degrees to the trailing side of the arm, flats facing up and down so the edge leads. The roll and blade angle blend in during the hold (plan frames 60-82) and out through the recovery (112-142).
- Left arm: shoulder raise cut from 75 to 22 degrees in the windup (hand about 40 cm in front of the hips instead of about 51), swings back and out during the sweep as before.
- Measured: max joint rotation 14 degrees per frame except a 47 degree hand roll at scene frame 32 (the roll into the horizontal blade); wrist 24-48 degrees through the sweep; sword to body clearance at least 10.7 cm (recovery), arm to skull 11.4 cm.
- Scripts: `Scripts/mk_sweep3.py` (builds `slash2_rig.py` from the v1 base), `slash_key.py`, `slash_iksw5.json`.

## Horizontal sweep v3 (notes: wiggle in the sword hand around scene frame 30, add overshoot)
- Wiggle: the blade turned from upright to horizontal in a few frames near the hold, which pushed the wrist 43 -> 60 -> 75 -> 43. The blade angle and the flat roll now blend over plan frames 36-84 and 36-76 (`mk_sweep3.py`), so the wrist rises once (64 at scene frame 24) and eases down (24 at frame 30), with a hand rotation of at most 16 degrees per frame through frames 20-42.
- Overshoot: the hand now carries on to (58, 114, 56) at scene frame 52 (finish was 54 / 116 / 57 before the tweak, but the rows now peak at x 54 and settle back), torso twist peaks at 19/26 degrees at plan frame 104 and eases back by 118, and the hand returns through (49,107,54) at frame 58 to the ready path.
- Measured: max joint rotation 14 degrees per frame except the hand at 28; sword/body 9.5 cm at the closest (recovery), arm/skull 11.2 cm.
- Files: `Skeleton_Slash.casc`, scripts `mk_sweep3.py`, `slash2_rig.py`, `slash_iksw7.json`; backups `Skeleton_Slash_sweep1_backup.casc`, `Skeleton_Slash_sweep2_backup.casc`.

## Horizontal sweep v4 (notes: pull back to the charged state should be one fluid motion; right pinky toe dissolves and flies about)
- Pull back: removed the anticipation dip (plan frame 12 row), the stiff hitch at frame 32, the draw wobble and the recovery stall-and-lurch (these made the hand stop-start: speed 5.5 / 2.7 / 5.4 / 2.4 cm per frame). The hand speed is now one hump (1.2 to 4.0 cm per frame, easing to 0 at the hold) and the recovery is one bell (peak 9 cm per frame, easing out). The blade also turns back from horizontal to the ready angle gradually through the recovery (plan frames 112-148); the first attempt left a 6-frame turn that drove the wrist to -139 degrees, now at most 67.
- Toe: the toe phalange joints (28 of them, 14 per foot) are not driven by any point, so they kept their rest rotations while the foot turned underneath them; the right little toe distal bone moved 1-3 cm relative to the foot. `slash_key.py` now sets every toe phalange rotation each key to follow the foot (rest rotation relative to the foot). Verified: the little toe distal head stays at (1.9, 14.7, 3.9) cm in foot space at every frame. The overhead clip was keyed before this fix and may carry the same drift on its right toes.
- Measured: max rotation per frame 20 degrees (hand), none over 30; sword/body 15.6 cm, arm/skull 11.2 cm.
