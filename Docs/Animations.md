# Animations: what the hips do, and why attacks must not own them

Lessons from the longsword attacks (A1-A3), recorded 2026-11-02 so they are not relearned. The short version: **under an upper-body mask the hips (Root) come from locomotion, so an attack clip must not carry pelvis motion. Its pelvis is held fixed and all body rotation lives in the spine.**

## The setup that causes the problem

- The player's `Attack` layer ([PlayerAnimationController.controller](../Assets/Prefabs/Characters/Player/PlayerAnimationController.controller)) uses the mask [Longsword_UpperBody.mask](../Assets/Prefabs/Characters/Player/Longsword_UpperBody.mask): **Body, Head, both arms and fingers on; Root and both legs off.** Locomotion (Idle, walk, run) plays underneath on the Base layer.
- The held stance (`Longsword_Ready`) is frame 1 of the A1 clip frozen (speed 0) on that layer.
- `UpperBodyArticulation` then turns the character root with the view and adds spine twist and pitch on top; `HandGripIK` pulls the off hand onto the hilt after both.

## What goes wrong when a clip moves the pelvis

Measured on a fresh Human_Male with the real controller, A1 played on the masked layer (shoulder line yaw, degrees):

| | Pelvis in clip | Result in game |
|---|---|---|
| Original clips | yaws about 25 to 70, drops about 11 cm, shifts sideways | hips never move in game: the shoulder swing is 46 degrees instead of the authored 94 |
| Ready pose | pelvis 27 degrees off the shoulders (a bladed stance) | legs from Idle face forward, torso looks twisted against them |

Two separate losses stack up:

1. **Mask.** `Root` off means the hips' position and orientation come from the other layer. Whatever pelvis motion the clip had is dropped. The spine muscles are relative to the hips, so only the part of the swing the spine itself did survives. In A1 the pelvis did about half the yaw.
2. **Import.** With `keepOriginalOrientation` and `keepOriginalPositionXZ` off, the body's yaw and sideways shift become root motion. The Animator has `applyRootMotion = false`, so they are thrown away too. (Baking them into the pose does not help while Root is masked out.)

It is not an export fault. The exported clip contains the pelvis motion correctly; it just has nowhere to go.

## The rule

- **A clip played under this mask keeps the pelvis fixed.** Hips position and orientation are constant for the whole clip; rotation of the body is spine (Spine, Chest, UpperChest) rotation relative to that fixed pelvis.
- **Every clip in a chain shares the same pelvis** (A1's), or the spine jumps at the seam between clips by the difference in pelvis heading.
- **Make the Ready square.** Choose the fixed pelvis heading so the spine twist at Ready is about zero. Then Ready matches the Idle legs, and the swing runs from a neutral spine.
- The legs are masked out. Their pose in the FBX only matters for preview; it is kept as the Ready legs.
- The pelvis height of the stance is Idle's, not the clip's. Authored crouches and dips do not reach the game. Use spine bend and arm reach for commitment instead.

## How the existing clips were converted

[pelvisfix_blender.py](../.claude/skills/cascadeur-rig-animation/scripts/pelvisfix_blender.py), run headless on each FBX:

```
blender.exe -b --python pelvisfix_blender.py -- <src.fbx> <dst.fbx> <report.json> fix <shared_pelvis_ref.json>
```

- Hips: constant position (frame 1) and orientation (the Ready pelvis turned to face the shoulders; alpha was -26.7 degrees for A1). The first clip writes the reference file; the others read it.
- UpperChest and everything below it: world orientation **exactly as authored**, so arms, hands and sword are untouched (hand-to-UpperChest error under 0.001 mm on every clip).
- Spine and Chest: interpolated between the fixed pelvis and the authored UpperChest (50 percent and 75 percent), so twist is spread as about 50 / 25 / 25 percent. This keeps every bone inside Unity's default humanoid twist limits (Spine 40, Chest 20, UpperChest 20): worst case A1 Spine 29.5, Chest 14.6, UpperChest 14.7; A2 31.1 / 15.4 / 15.5; A3 6.3 / 3.1 / 3.1.
- Legs: frame-1 pose for every frame.
- The animated pommel mesh in the FBX follows the upper body so a Blender preview still lines up.

Result in game, A1 on the masked layer: shoulder swing 93 degrees (was 46; authored 94), Ready square with the legs. Hand speed through the strike, relative to the character: peak 4.5 m/s, mean 3.3 over frames 56-70, against 4.9 and 3.9 in the authored clip. About 8 percent of peak and 15 percent of mean is hip translation and pelvis turn that cannot reach the game; the blade's path and arc are the same.

## Blender FBX round trip: the off-by-one

Blender's FBX exporter writes frame N at time N/60 and the importer puts time t at frame t*60+1, so an imported clip that starts at frame 2 and is exported as it stands comes back one frame late (max error 9.5 cm, 14 degrees on a fast hand). The script re-keys the action one frame earlier before exporting. With no edit at all it then round-trips within 0.01 mm and 0.15 degrees. Export range is 1 to last-1 (A1: 1-131, Unity clip frames 1-131). Keep the take name `Cascadeur Export - baked deform skeleton`.

Blender 5.x layered actions have no `Action.fcurves`; read and write animation through pose bones and `keyframe_insert`.

## How to check a clip under the mask

Do not judge by eye in the full-body preview. Instantiate `Human_Male.fbx` with the controller, `Play` the attack state on layer 1 at each normalized time, `Update(0)`, and read bone positions. Compare shoulder-line yaw (right upper arm minus left upper arm) and thigh yaw with the clip sampled without layers. The thigh yaw must stay 0 and the shoulder swing must match the authored range.

Reference numbers for A1 (frames 1, 56, 70, 80, 93, 131): shoulder yaw 0, -26, 47, 57, 67, 0.

## Still open

- Whether the swing still feels powerful in play: the pelvis drives much of what looks heavy in a real strike. The view-driven pelvis turn in `UpperBodyArticulation` is the place to add it back (it is limited to 180 degrees per second during the active phase).
- Seams: every clip uses A1's pelvis, so the chest orientation at each seam matches the previous clip's end by construction, but the A1-to-A2-to-A3 chain was measured only clip by clip (A2 peaks at 31 degrees of Spine twist). View the chain in play.
- The EditMode tests (`LongswordGripTests`) compare hand and sword relative to each other, which this change does not move, but they were not rerun after the change.
