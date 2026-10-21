# Skeleton enemy: animation plan (draft for approval)

Rig: `Skeleton_Rig` in `I:\Art\Blender\Skeleton\AnatomicalHumanSkeleton_GameReady_v65_PhysicsDriven_Final.blend`. 30 fps, T-pose rest, 152 bones.
Flow: Blender (model) -> Unity; Blender (skeleton) -> Cascadeur (animate) -> Unity directly as animation FBX.

## What the review of the existing actions found

- `skeleton_die` (0-48) and `skeleton_rise` (0-72) are **physics bakes of the bone pieces coming apart / reassembling**, not character animation.
  - 106 bones move. 90 are `Piece_*` bones (ribs, vertebrae, long bones) plus Head, Jaw, hands, fingers and feet. Piece locations travel up to ~1.6 m and rotate up to 340 degrees.
  - The humanoid chain (Root, Hips, Spine, Chest, UpperChest, shoulders, upper arms, lower arms, upper legs, lower legs) has **zero** keys in both actions.
- `skeleton_pile_pose` is identical to `skeleton_rise` frame 0 and `skeleton_die` frame 48.
- `skeleton_rise` ends on the exact rest pose, which is a **T-pose** (hands 0.73 m out at shoulder height). It does not end on an idle stance.
- Piece bones are children of the humanoid bones (e.g. `Piece_Left_femur` under `LeftUpperLeg`), and the 19,746-vertex `Skeleton_GameReady` mesh is skinned to both. So rotating only the humanoid chain moves everything, provided the Piece bones sit at rest.

### Consequences
1. Death can stay as baked. It is a Generic-rig clip: a Humanoid retarget would drop the Piece-bone motion.
2. The existing Rise is the intended design: a reassembly from bones, mirroring Die. It only needs a short settle from the T-pose into Idle at its end.
3. Cascadeur gets a **humanoid-only skeleton** (82 bones with fingers and toes, no `Piece_*`; the rig has 70 `Piece_*` bones, 152 total). Exported to `I:\Art\Cascadeur\Skeleton\Skeleton_Humanoid.fbx`; Cascadeur setup scene `I:\Art\Cascadeur\Skeleton\Skeleton_Humanoid_Setup.casc`. Animation FBXs carry humanoid bones only; Piece bones follow their parents in Unity and stay at rest.
4. Piece-bone clips (Die, Rise reassembly) are not retargetable. Everything else is.

## Style rules (all animations)

Slow, shambling, slightly uncoordinated undead.
- Weight shifts arrive late. Head and sword arm lag the torso by 3-6 frames.
- Left and right sides never mirror exactly (timing, amplitude, or both). Loops are asymmetric on purpose.
- No clean arcs: add small corrective wobbles, dropped shoulder, favoured leg.
- Exception: the attack **telegraph must stay readable**. The sloppiness goes into the windup's start and the recovery, not into the strike's direction.

## Shared skeleton animations (base layer)

### 1. Idle (loop, 90 frames = 3 s)
- Start/end pose is the "neutral stance" every other clip returns to. Arms hang, slight forward slump, head tilted a few degrees, knees soft.
- Motion: slow breathing in the ribcage (very small, ribs are rigid bone), weight drifting left to right, head drooping then jerking up once per loop, occasional finger twitch on the left hand.
- Loop: first and last frame match. Keep the sword arm neutral. The weapon layer owns it.
- Check: no foot slide, nothing moves more than ~3 cm at the hips.

### 2. Walk forward (loop, 54 frames = 1.8 s, 2 uneven steps; see `Docs/WalkPlan.md`)
- Pace about 0.44 m/s: about 0.4 m per step (leg reach limits it), uneven steps of 26 and 28 frames.
- Shamble: left leg drags (longer ground contact, shorter swing, toe scuffs), right step is a heavy lurch. Torso leans forward and rocks side to side. Arms swing out of phase with uneven amplitude; head lags a few frames behind the torso.
- Root motion: baked on `Root` at constant forward velocity. Unity agent speed should match it.
- Check: feet planted during contact, left and right cycle lengths differ in shape but loop cleanly at frame 48.

### 3. Rise (one-shot, reassembly from bones)
Design rule (from the user): the skeleton falls apart into bones on death and **reassembles itself from bones** on rise. There is no humanoid get-up. **Rise frame 0 must equal the last frame of Die** (the pile), so a skeleton can be killed and reanimated repeatedly without a pop.
- Verified in the file: `skeleton_rise` frame 0, `skeleton_pile_pose` and `skeleton_die` frame 48 match on every bone sampled (Root, Hips, Spine, Chest, Neck, Head, both arms and legs, feet).
- Base: the existing `skeleton_rise` (0-72) piece reassembly. This is a Generic-rig clip authored in Blender, not Cascadeur.
- It ends on the T-pose rest, not on the Idle stance. **Decided:** no settle is baked in. Unity crossfades Rise -> Idle (start with about 0.5 s and tune). The Rise clip stays untouched.
- Open: whether the reassembly itself needs to be re-timed or restyled (72 frames at 30 fps is 2.4 s) and whether the pieces should visibly click into place in a deliberate order (feet and legs, pelvis, spine and ribs, arms, skull last).

## Weapon-specific animations (first skeleton, weapon layer, upper-body mask)

Mask: Spine, Chest, UpperChest, both shoulders, arms, hands, fingers, neck, head. Legs and Hips stay on the base layer so a swing works while standing or walking. Sword is attached to `RightHand` and exported separately.

### 4. One-handed ready pose (loop, 60 frames = 2 s)
- Right arm bent about 90 degrees, sword held low and forward with the blade angled up (~35 degrees), wrist slightly slack. Left arm hangs loose (still base layer or lightly masked).
- Motion: slow droop and re-lift of the sword tip, small tremor. Never fully still.
- This is the start and end pose of both attacks.

### 5. Vertical overhead strike (one-shot, ~66 frames = 2.2 s)
Punishable downward attack: the player dodges to the side and punishes during recovery.
| Frames | Phase | Notes |
|---|---|---|
| 0-24 | Windup | Slow, lurching raise of the sword over the head; torso leans back; a shaky wobble at the top. This is the telegraph. |
| 24-30 | Top hold | Visible pause, tremor. |
| 30-35 | Strike | Fast vertical chop, 5 frames. Hit window 33-35. |
| 35-39 | Impact | Sword overshoots low, body pitches forward. |
| 39-66 | Recovery | Slow, clumsy pull-back to the ready pose. This is the punish window. |
- Torso carries the strike (spine pitch), not only the wrist.
- Check: the blade's true edge leads the cut, no body clipping, no elbow snap.

### 6. Diagonal slash (one-shot, ~61 frames = 2 s)
Reads clearly different from the overhead so the player picks the opposite dodge: windup goes *sideways*, the cut is diagonal.
| Frames | Phase | Notes |
|---|---|---|
| 0-20 | Windup | Sword drawn back over the right shoulder, torso twisting right. |
| 20-25 | Hold | Short tremor. |
| 25-31 | Slash | High right to low left. Hit window 27-31. |
| 31-37 | Follow-through | Sword ends across the left hip, torso wrapped left. |
| 37-61 | Recovery | Uneven unwind back to the ready pose. |
- Check: the swing direction is distinguishable from the overhead at the frame level, not just in total motion.

## Decisions for the user

1. **Rise settle.** Decided: crossfade from Rise to Idle in Unity. If the arms visibly drop too mechanically, revisit with a baked settle.
2. **Legs during attacks.** Recommended: legs keep playing (upper-body mask), so enemies can swing while walking. Alternative: legs plant (full-body attacks, root stays put).
3. **Walk root motion.** Recommended: baked on Root; Unity agent speed matches it.
4. **FBX export from Cascadeur.** Verified 2026-10-21: `FbxSceneLoader.export_fbx_scene` wrote a valid FBX (82 bones, no Piece bones, round-tripped through Blender). `is_export_available()` is True, `is_pro_features_available()` is False. An earlier note said FBX export was refused; that did not reproduce. Still to test: an animated multi-frame export.

## Risk: one Die/Rise for every skeleton variety

Intent: a single Die and a single Rise, blended, serve every skeleton variant. Unverified. Things that could break it:
- Die/Rise are Generic clips keyed on this rig's bone names, including 70 `Piece_*` bones. A variant with different or missing bones will not play the missing channels, and one with different proportions will reassemble to the wrong places (piece offsets are in each bone's local space, measured for this skeleton).
- Humanoid retargeting does not carry `Piece_*` motion, so Die/Rise cannot be Humanoid clips.
- Cheapest way to find out: before building more clips, set up one variant in Unity (different proportions, same bone names and hierarchy) and play Die -> Rise -> Idle on it. Variants that keep the same bone names and hierarchy are the likely safe path.

## Work order (proposed)

Idle -> Walk -> Ready pose -> Overhead strike -> Diagonal slash. Die and Rise already exist; they only need importing into Unity and the Rise -> Idle crossfade set up.
For each animation: plan reviewed here -> block key poses in Cascadeur -> user review -> polish -> export.
