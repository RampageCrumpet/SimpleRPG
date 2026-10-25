# View-driven upper-body articulation

The player's view (mouse look) drives the character's spine, on top of the authored animation. Turning into a swing carries the blade further along its arc; turning away drags it; looking up or down tilts the whole upper body, hurtboxes included. Nothing changes a clip's timing or a weapon's damage: the blade's real path through the world changes, and the existing hit detection sees it.

Bead: `simplerpg-7du.1.8`. Owner yaw prediction: `simplerpg-7du.1.6`. Camera at the eye: see "The camera" below.

## The pieces

| Piece | Where | Does |
|---|---|---|
| `FirstPersonLook` | `Characters/Player/Camera` | Owner only. Keeps view yaw and pitch, aims the camera **in world space** (it never inherits body rotation), reports `(yawDelta, pitch)` to an `IViewSink`. Sole owner of camera **orientation**. |
| `FirstPersonCharacterController` | same | Owner only. In `LateUpdate` (after articulation) puts the camera at the `Eye` transform's world position. Sole owner of camera **position**. Sets the camera's near plane. |
| `UpperBodySolver` | `Characters/Articulation` | Pure maths. Steps three yaws: **view** (free), **torso** (chases the view at a phase-limited speed; the weapon follows this) and **pelvis** (the character root; chases the torso, dragged along past the twist limit). Spine twist = torso - pelvis. |
| `SpineRig` | same | Adds the twist/tilt to the humanoid spine chain (Spine, Chest, UpperChest, Neck, Head by default) after the animator has posed it. Undoes its own previous offset if the animator did not rewrite a bone, so offsets never accumulate. |
| `UpperBodyArticulation` | same | `NetworkBehaviour`. Takes look input, runs the solver, turns the root, replicates the pose, poses the spine in `LateUpdate`. |
| `ArticulationProfile` | same | `ScriptableObject` holding every tunable (`ArticulationSettings`). Player asset: `Assets/Prefabs/Characters/Player/PlayerArticulationProfile.asset`. |
| `IArticulationPhaseSource` | same | What tells the solver the attack phase. `ComboMeleeDelivery` implements it from `ComboMachine.PhaseAt`, so no clip or event is read. |
| `Hitbox` sweep | `Characters/Hitbox.cs` | Tests the blade at poses between physics steps as well as where the trigger sees it. |

## Where it sits in the frame

1. `Update`: look input, `UpperBodyArticulation` steps the solver; on the server it turns the root. (On the owning client the root is turned in `LateUpdate`, after the `NetworkTransform` has written it.)
2. Animator evaluates (authored attack on the Attack layer, locomotion underneath).
3. `LateUpdate`: `SpineRig.Apply` adds twist and tilt to the animated spine. Arms, weapon and head hang below the spine, so they follow. `FirstPersonCharacterController.LateUpdate` (execution order 100, so after the articulation) re-aims the camera from the view and moves it to the evaluated `Eye` position.
4. Next `FixedUpdate`: `Hitbox` sweeps from last step's pose to this one, then Unity syncs transforms to physics and runs its trigger pass. Both therefore see the final, articulated pose, the one that was drawn. `Physics.autoSyncTransforms` stays off; the sweep calls `Physics.SyncTransforms()` itself when it needs hurtboxes at their current pose.

The server's animators are forced to `AlwaysAnimate`, so the server runs the same chain every frame.

## View, torso, pelvis and weapon

- **View yaw/pitch**: the camera. Never limited, never forced. Movement input is relative to it.
- **Torso**: chases the view at the speed the phase allows (below). A limit that tightens (a swing begins) applies at once; one that relaxes (the strike ends) eases up over `limitBlendSeconds`, so control returns smoothly rather than snapping.
- **Pelvis (the root)**: chases the torso slowly inside `pelvisDeadZone` and faster beyond it, never faster than the torso may turn, and is dragged along when the twist would pass `maxTwist` (so the limit is never exceeded, and the legs turn at the view's pace past it).
- **Weapon**: the authored swing, rotated by whatever the torso does underneath it.

Initial values (all in `ArticulationSettings`, to be playtested):

| Setting | Value |
|---|---|
| `maxTwist` | 70 deg either way |
| `maxPitchDown` / `maxPitchUp` / `pitchGain` | 90 / 90 / 1.5 (the camera's 60 deg range reaches the full 90 deg bend) |
| `pelvisDeadZone` / `pelvisRecenterSpeed` / `pelvisFollowSpeed` | 15 deg / 90 / 300 deg/s |
| Yaw / pitch speed, Free | 20000 / 20000 deg/s (effectively unlimited) |
| Windup | 540 / 360 |
| Active (blade live) | 180 / 120 |
| Recovery | 360 / 240 |
| `limitBlendSeconds` | 0.12 |
| Bone shares (yaw and pitch each) | Spine 0.2, Chest 0.3, UpperChest 0.3, Neck 0.1, Head 0.1 |
| `deathFadeSeconds` | 0.25 |
| `remoteSmoothingSeconds` / `replicationThreshold` | 0.06 s / 0.25 deg |

Phases come from the combo data: Windup is step start to `HitStart`, Active is `HitStart` to `HitEnd`, Recovery is `HitEnd` to `Duration`, Free otherwise.

## Collision

The blade's `Hitbox` and all hurtboxes already ride bones, so the articulated pose **is** the collision pose; there is no second representation. Pitching down moves the head and chest hurtboxes; twisting moves the chest and arms. The `CharacterController` capsule is untouched (it is for locomotion).

`Hitbox` now reports contacts from two sources that share one geometry and one event: Unity's trigger messages, and a sweep over the poses between the previous fixed step and this one (position lerp, rotation slerp, at most 12 samples, spaced by the blade's thinnest extent). Box, sphere and capsule colliders are swept; other shapes rely on the trigger alone. The existing `contacts` set and `SwingHitTracker` still make a target hit at most once per swing. A hitbox that was disabled forgets its old pose, so a window never sweeps across the move that preceded it. The path between two steps is approximated as a straight interpolation, and targets are taken at their current pose.

## Network

| State | Owner | Wire |
|---|---|---|
| View yaw and pitch (input) | Owning client sends; server holds | `SubmitLookServerRpc(yawDelta, pitch)` replaces the old per-frame yaw RPC. Sent only when the view changed. Dead characters ignored; non-finite values dropped; one message's yaw clamped to `maxYawPerMessage`. |
| Pelvis yaw | Server | Existing `NetworkTransform` (rotation Y). The server rotates the root from the solver. Everyone but the owner shows this. |
| Torso twist, tilt and pelvis yaw | Server | One `NetworkVariable<ArticulationPose>`: three `short`s in 0.01 deg, written only when one moved by `replicationThreshold`. Read by everyone; the owner uses the pelvis yaw only to check its prediction. |
| Phase | Server (combo machine) | Not sent. The server and the owner each derive it from their own combo machine. |

A client can only say where it looks. The speed limits, twist limit and phase are computed on the server, so no pose value can be forged. Remote clients ease the replicated pose (the server's pose is exact and decides hits). The owning client predicts the whole solution with the same solver: torso, tilt **and pelvis**. Each frame, after the `NetworkTransform` has written the root, it turns its own copy of the root to its predicted pelvis yaw (alive only), so the torso twist limit, the weapon and the camera answer the mouse at once and the owner's twist obeys the same limits as the server's. Nothing in the prediction integrates: the solver chases the absolute view, so the owner and the server agree whenever the view holds still, whatever frame rate or latency, and no error accumulates. If the view has been still for `reconcileStillSeconds` (longer than a round trip), the owner's solver has settled, and the server's replicated pelvis still differs by more than `reconcileThreshold`, the server must hold a different view (a clamped or lost message); the owner then sends `ResyncViewServerRpc(viewYaw)` once (throttled), which moves only the server's view; the server's body then follows at the phase's speed. Hits are still tested against the server's own pose, which moves only on what the server received. While the view is moving the owner and server differ transiently by up to a round trip; that is expected. Respawn: the server resets the view to the respawn rotation and tells the owner (`ResetViewClientRpc`).

## The camera

The camera is **not** a child of the head. One owner for each thing:

- **Position**: `FirstPersonCharacterController.LateUpdate` copies the world position of the `Eye` transform, a child of the humanoid head bone (`head.x`) at the character's eyes (local `(0, 0.12, 0.10)` in head space on `TestCharacter`). It runs after the animator and `SpineRig.Apply`, so the eye already carries locomotion, crouching, leaning, torso articulation and authored head movement. Nothing smooths or corrects it; small authored head movement moves the viewpoint.
- **Orientation**: `FirstPersonLook.ApplyCamera`, from the owner's view yaw and pitch only. The head's rotation never reaches the camera.
- The camera is parented to the character root only so it goes where the character goes; its local pose is overwritten each frame.
- **Near plane**: `FirstPersonCharacterController.nearClipPlane` (0.04 m), applied when the camera attaches. The vertical field of view is unchanged.

## Limitations to check by hand

- **Feel of dragging and acceleration.** The tests prove the torso moves the weapon and that collision follows it. Whether the numbers feel right needs playtesting, ideally with a host and a remote client.
- **Owner on a remote client**: the owner sees its own root turn at once; the server's collision pose lags it by a latency. Attacking while turning fast is the case to feel on a laggy connection. The owner's own *position* is still the server's (movement is not predicted), so the camera, which now rides the eye, lags walking by a round trip on a remote client (separate from this work).
- **Enemy weapons** use the same `Hitbox` sweep but have no articulation; adding `UpperBodyArticulation` plus a phase source to another humanoid is all it takes, and it does not need a camera.
- The hurtboxes are static colliders moved by animation (no Rigidbody), which Unity treats as expensive; unchanged by this work.
