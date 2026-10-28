# Rig structure, anatomy measurements and Unity Humanoid notes

## Rig structure (mixamorig autoposing rig, 67 joints)
Joint orientation is a function of rig points; joints are outputs.

| Joint | main | direction | additional | other |
|---|---|---|---|---|
| LeftArm | LeftArm_MainPoint | LeftForeArm_MainPoint (elbow) | **LeftForeArm_AdditionalPoint** | no LimbDir |
| LeftForeArm | LeftForeArm_MainPoint | LeftHand_MainPoint (wrist) | **LeftForeArm_AdditionalPoint** | LeftForeArm_LimbDir |
| LeftHand | LeftHand_MainPoint | LeftHand_DirectionPoint | LeftHand_AdditionalPoint | own triangle |
| RightFoot | RightFoot_MainPoint | Self0 / Self1 points | | foot triangle |

Experiment (disposable copy of ReadyPose.casc, point rotated 40 degrees about the elbow-to-wrist axis, orientation change in degrees):

| point rotated | Arm | ForeArm | Hand |
|---|---|---|---|
| LeftForeArm_LimbDir | 25.8 | 25.8 | 10.4 |
| LeftForeArm_AdditionalPoint | 10.4 | 17.0 | 25.3 |
| LeftHand_AdditionalPoint | 0.7 | 1.3 | 40.9 |
| LeftHand_DirectionPoint | 1.1 | 0.6 | 65.2 |

Conclusions: Arm and ForeArm roll together (one elbow plane, pure hinge); only the Hand triangle can express pronation. Writing joint rotations (even together with points) does not change this: arm/forearm stayed 29-38 degrees away from the solver. A solver using hinge-plane arm/forearm and `twist_share = 0` matches the scene to 0.1 degrees. (Whether twist joints / extra rig controls can add independent pronation: see the section at the end, updated after testing.)

Hips/Spine also pass through Spline IK helpers; spine joints drift 1-3 degrees from computed values, shoulders 3-6 degrees. Treat positions as accurate to about 0.25 cm and orientations to about 6 degrees except hand (exact) and, with twist_share 0, arm/forearm (0.1).

## Skeleton facts (cm)
Upper arm 27.4, forearm 27.6, thigh 40.6, shin 42.1. Rest reach shoulder to wrist: right 43.4, left 47.5; limit reach in the solver to about 0.955 of full extension to avoid locked elbows. Hand-to-sword grip offset is fixed: right hand 12.1 cm from the guard (blade base), left hand about 21.6 cm along the grip. Torso: spine boxes about 40 cm wide by 37 cm deep (half-thickness 18-20 cm). Ready pose: right foot forward (z +12.6) left back (z -15.7), feet 33 cm apart laterally, hips at 93 cm, blade about 70 degrees above horizontal.

## Wrist anatomy decomposition (how to measure)
```
rel   = inv(Q_forearm) * Q_hand                      (scene globals)
twist = 2*atan2(rel.y, rel.w)  about local Y         (pronation/supination)
swing = rel * inv(twist)                             (flexion + deviation)
w     = (Index1.lp - Pinky1.lp) with y removed       (points to the radial side)
palm  = side where Thumb1.lp lies (assumption, unverified for flexion vs extension labels)
flexion axis = Y x palmar ; deviation axis = Y x w ; positive deviation = radial
components = dot(rotation_vector(swing), axis)
```
The combined swing magnitude (the earlier "82-84 degrees") hides the split. Measured on the evaluated Attack 1 Neutral clip: left wrist extension up to 80 (12 frames above 70), left ulnar up to 69 (24 frames above 35); right wrist ulnar up to 82 (53 frames above 35), flexion up to 64. The Ready grip itself has about 34 degrees extension plus 27 ulnar at the left wrist and 22 / 26 at the right, so the grip uses a large part of the available ulnar range before the cut starts. Twist is measured from the rig's zero; it is not confirmed that this zero is palm-down, so absolute pronation versus supination is unconfirmed (right hand +33 and left -49 at Ready are mirror-consistent).

Typical limits used: flexion 80, extension 70, radial 20, ulnar 35, forearm pronation/supination about 85 each way. Prefer comfortable ranges (about 60-70% of these) during the strike.

## Unity Humanoid
Verified from Unity documentation:
- `ArmDof` includes `ArmRollInOut`, `ForeArmRollInOut`, `HandDownUp`, `HandInOut` (arm roll and forearm roll exist as muscle degrees of freedom; the hand has two).
- `HumanDescription.upperArmTwist` / `lowerArmTwist` (0..1, default 0.5): how roll is distributed between shoulder/elbow and elbow/wrist. Sources: https://docs.unity3d.com/ScriptReference/ArmDof.html and https://docs.unity3d.com/ScriptReference/HumanDescription-lowerArmTwist.html .
Not verified (needs an import test in the real project, with the SimpleRPG project open in Unity): that a pronation authored entirely in the Hand joint is converted to forearm roll without loss; the exact default muscle clamps (recalled as hand down-up about +-80, in-out about +-40, forearm and arm twist about +-90) and whether values beyond them are clamped on import. Redistributing twist between ForeArm and Hand joints at export is lossless for the hand's world orientation because twist about the forearm axis moves neither elbow nor wrist.
The sword is a separate object on the right-hand weapon socket, so its world orientation equals the hand's; anything clamped in the hand changes the blade.

## Can independent forearm pronation be added to the rig? (tested on disposable copies, 2026-10-15)
Result: not practically. Evidence:
- **Cascadeur's native twist** (`rigging/.../box/twist`, `prototypes/main_actions/actions.py: set_twist`, JSON fields `TwistFor`, `TwistStrength`, `TwistAxis`) adds auxiliary **twist joints** (e.g. `forearm_twist` following the hand's roll by a strength). They are extra bones needing their own skin weights. The Mixamo skeleton has none, and the main ForeArm joint stays a rigid hinge, so this does not give the ForeArm joint its own pronation.
- **Rig mode** (`rig_mode.on.run_raw(domain_scene, [0.0, 0.5, 0.0])` then `rig_mode.off.run(domain_scene, True)`) turns the rig into editable proto objects (Rig Element, Proto Box/Point/Inner, `HingeSettings`, `TechnicalLinks`, `PointsUpdateSettings`) in T-pose, and regenerates it on exit. In rig mode each arm element has its own `ProtoAdditionalPoint_*`, but Arm and ForeArm points are coincident (0.000 cm), which is the hinge. The generator decides parent connection type (Null / Ball / Hinge / HingeAdditional) in `ProxyPointsBuilder.get_joint_to_parent_type`: Hinge when the parent's direction and additional points hold no explicit data.
- Moving `ProtoAdditionalPoint_LeftArm` 9.5 cm away and regenerating changed nothing: after re-entering rig mode the two points were coincident again, so proto point positions are not the source of truth; the stored quick-rig (QRT biped) definition is. `HingeDirectionType` only offers NONE / LEFT / RIGHT (hinge direction, not a ball option).
- Setting `RigElement_LeftForeArm` -> `HingeSettings.create_direction_controller = False` removed `LeftForeArm_LimbDir` (326 objects) but Arm and ForeArm still shared `LeftForeArm_AdditionalPoint` and rolled together (11.8 / 17.8 degrees for a 40 degree point rotation).
- Every regeneration warned that local rotations/positions of Neck, LeftUpLeg, RightUpLeg, both shoulders, LeftArm, RightArm, LeftForeArm, LeftHand, Spine1, Spine2 and Longsword_01 changed at frame 0. Regeneration is therefore not lossless for an existing skeleton or animation and would have to be redone and revalidated for every project.
Unexplored: editing the QRT definition stored on the `Animation info` object (`QrtInfo`) or rebuilding the rig from a custom rig JSON with a Ball elbow; treat as a research task, not a quick fix.

## Forearm roll: practical options
- Author with `twist_share = 0` (solver equals scene) and keep the hand's pronation within a comfortable range, because in the scene all of it lives in the Hand joint.
- Export-side redistribution (Blender or any DCC): split the hand's twist about the forearm axis between ForeArm and Hand joints (about 50/50 matches Unity's `lowerArmTwist` default); keep the hand's world orientation fixed. Lossless for the hand and sword. Needs an export path because FBX export from this Cascadeur licence is blocked.
- Reduce the pronation and deviation demand by choosing a better Ready grip (same for every attack).
