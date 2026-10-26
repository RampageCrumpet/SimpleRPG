> Historical: this investigation was written against the Magician_RIO rig, which has been replaced by the human male (`HumanMale_Rig`, bones named after Unity's humanoid bones). Bone names below are the old ones.

# First-person camera and weapon readability investigation

**Scope.** Evidence-only inspection performed on 2026-10-24. No gameplay code, animation clips, prefabs, or camera settings were edited. Runtime checks used a local host session and were discarded when Play mode stopped.

## Conclusion

The current readability problem is a **combination of the shared full-body attack trajectories and the first-person camera/near-plane relationship**, rather than a separate weapon, camera-only motion, or a collision representation mismatch.

The authoritative longsword is correctly attached below the animated right hand and is driven by the same humanoid animation as third person. Its guard is often much closer than the camera near plane; the rest of the blade is also commonly outside the vertical frame. Therefore hiding the local body would remove body self-occlusion, but would not make several sampled attack phases readable. View-driven torso articulation substantially changes the real weapon path and is intentionally coupled to it.

## A. Current architecture

```text
Mouse input
  -> FirstPersonLook (owner, world yaw/pitch)
  -> UpperBodyArticulation.SubmitLook
       -> server: solver + character-root/pelvis yaw
       -> owner: local torso prediction
  -> Animator (Base + Attack layers; shared humanoid clips)
  -> UpperBodyArticulation.LateUpdate / SpineRig.Apply
       Spine -> Chest -> UpperChest -> Neck -> Head -> right arm -> hand -> Longsword
  -> FirstPersonCharacterController.LateUpdate
       camera world rotation from view; position at CameraSnap plus head-bend delta
  -> next FixedUpdate: Hitbox sweep + Physics.SyncTransforms
```

The player root has `FirstPersonCharacterController`, `Animator`, `ComboMeleeDelivery`, and `UpperBodyArticulation`. The visual rig is `Magician_RIO/Magician_RIO_Unity/root/root.x`; the sword is a single `Weapon`/`Hitbox`/kinematic-`Rigidbody` object under `.../hand.r/Longsword`. Its `Longsword Model` child has a 100x import scale; its measured blade length is about 1.23 m (local mesh Y maximum 0.01226, multiplied by that scale).

`CameraSnap` is under `Magician_RIO`, not under `head.x`, with prefab-local position `(0, 1.738, 0.056)`. At runtime the Main Camera is parented to it with local position approximately zero. The controller derives an `eyeInHead` offset once, then adds only the head displacement produced by the spine bend; it deliberately assigns the camera's world rotation from `FirstPersonLook`, so camera rotation does not inherit the head/torso rotation.

Runtime camera configuration: vertical FOV **60 degrees**; near clipping plane **0.30 m**.

The animator has two active layers: `Base Layer` and `Attack` (both weight 1). The combo uses `Longsword_A1`, `Longsword_A2`, and `Longsword_A3`; their active hit windows are respectively 0.933--1.167 s, 0.633--0.917 s, and 0.750--0.983 s.

### Transform writers and order

| Target | Writer | When |
| --- | --- | --- |
| Camera parent/local position | `FirstPersonLookMath.AttachCamera` | owner `Start` |
| Camera world rotation | `FirstPersonLook.ApplyCamera` | input `Update`, then controller `LateUpdate` |
| Camera world position | `FirstPersonCharacterController.LateUpdate` | after spine articulation |
| Root/pelvis yaw | `UpperBodyArticulation.StepServer` | `Update`, server |
| Spine, chest, upper chest, neck, head | Animator, then `SpineRig.Apply` | animation evaluation, then articulation `LateUpdate` |
| Sword/hand | Animator and inherited spine pose | same frame; no direct camera writer |
| Weapon collision | `Hitbox.FixedUpdate` | next physics step, swept from prior final pose |

There is intentional shared ownership of camera position: `CameraSnap` supplies the baseline, while the controller overwrites the final world position with the measured head-bend delta. There is no detected camera-to-head feedback loop: the camera never drives a bone directly. There is, however, a presentation consequence: the camera is not a true skull child, so it does not receive full authored head motion/rotation.

## B. Confirmed findings

1. **Near-plane clipping is severe at close phases** (high confidence; presentation).
   The guard was repeatedly closer than 0.30 m, including A2 recovery at 0.071 m (guard), 0.051 m (blade midpoint), and 0.030 m (tip). This is full-weapon clipping, not just a tip leaving the frame. A1 windup also places the guard at 0.139 m.

2. **Several phases are out of the image despite being in front of the camera** (high confidence; presentation).
   A3 active has guard/mid/tip depths 0.207/0.700/1.194 m, but all are below the image (camera-space Y -0.51/-0.86/-1.20 m). That is composition/trajectory, not near clipping. A1 recovery and A2 windup similarly place the whole sampled blade below frame.

3. **The camera mount is an approximate eye mount, not a skull attachment** (high confidence; presentation).
   The `CameraSnap` parent is the visual rig root rather than the humanoid head. The compensating position delta follows procedural spine bending only; authored head animation and head rotation are intentionally excluded from the camera. This differs from the stated intended skull-bone mounting and makes close-camera alignment dependent on the authored `CameraSnap` offset.

4. **View manipulation does change the authoritative visual sword path** (high confidence; presentation and combat mechanics).
   In an A1 active-pose runtime check, a +30 degree horizontal input produced 24.39 degrees torso twist and 5.61 degrees root yaw. A +30 degree vertical input produced 45 degrees torso pitch (`pitchGain` 1.5). The sword is below that articulated chain, so it moves with the pose. The active phase limits are 180 degrees/s yaw and 120 degrees/s pitch; windup/recovery allow more response. This is intentional, not a constraint preventing dragging.

5. **The collider and rendered sword use the same articulated transform chain** (high confidence; combat mechanics).
   The single sword's `Hitbox` is on the authoritative object under the hand. `SpineRig.Apply` runs in `LateUpdate`; `Hitbox.FixedUpdate` sweeps the recorded transform from the prior final pose, calls `Physics.SyncTransforms`, and queries hurtboxes. No duplicate first-person collider or independent weapon trajectory exists. The documented ordering means a normal next fixed step sees the pose that was rendered. A residual network risk remains for an owning remote client: local torso is predicted but pelvis/root yaw is replicated, so the local visual pose can differ temporarily from the server's collision pose.

## C. Weapon visibility samples

Coordinates are camera-space metres. `inside` requires depth at least 0.30 m and viewport X/Y in [0,1]. Guard is the sword model origin; midpoint and tip are halfway and maximum along its measured mesh-Y blade extent. Samples use no mouse movement and nominal middle of each phase.

| Attack / phase | Guard (x,y,z), inside | Midpoint, inside | Tip, inside | Result |
| --- | --- | --- | --- | --- |
| A1 windup, 0.467 s | (0.282,-0.266,0.139), no | (0.382,0.285,0.387), no | (0.482,0.836,0.635), no | Entire sampled blade out; guard inside near plane. |
| A1 active, 1.050 s | (0.301,-0.334,0.375), no | (-0.025,-0.376,0.892), yes | (-0.351,-0.417,1.409), yes | Upper blade visible; guard is below image. |
| A1 recovery, 1.667 s | (0.103,-0.498,0.359), no | (-0.091,-1.047,0.551), no | (-0.286,-1.595,0.743), no | Entire blade below image. |
| A2 windup, 0.317 s | (0.171,-0.550,0.326), no | (-0.201,-0.978,0.560), no | (-0.572,-1.406,0.793), no | Entire blade below image. |
| A2 active, 0.775 s | (0.290,-0.418,0.335), no | (0.027,-0.294,0.874), yes | (-0.236,-0.169,1.414), yes | Upper blade visible; guard below image. |
| A2 recovery, 1.392 s | (0.358,-0.315,0.071), no | (0.277,0.292,0.051), no | (0.196,0.899,0.030), no | Full sampled weapon is behind the near plane. |
| A3 windup, 0.375 s | (0.317,-0.480,0.067), no | (0.252,-0.588,0.666), no | (0.187,-0.697,1.266), yes | Tip alone appears at lower edge; weapon is not readable. |
| A3 active, 0.867 s | (0.405,-0.510,0.207), no | (0.291,-0.855,0.700), no | (0.177,-1.200,1.194), no | Entire blade below image; guard also near-clipped. |
| A3 recovery, 1.475 s | (0.260,-0.537,0.201), no | (0.140,-0.658,0.790), no | (0.020,-0.779,1.378), yes | Tip at lower edge only; not readable. |

Manipulation check at A1 active: baseline guard/tip camera positions were `(0.300,-0.336,0.376)` / `(-0.352,-0.421,1.410)`. With +30 degree horizontal view movement and matching camera yaw, they became `(0.298,-0.336,0.408)` / `(-0.440,-0.421,1.383)`. With +30 degree vertical view movement and matching camera pitch, they became `(0.300,-0.350,0.668)` / `(-0.352,-0.543,1.688)`. Vertical movement improves A1's sampled blade depth/framing in this direction, while torso pitch itself remains a powerful weapon-path change.

## D. Recommended corrections

1. **P0 / low-to-medium risk: author and validate the shared attack trajectories against the intended eye camera.** Keep the one humanoid rig, the shared clips, and the one sword. Establish a visibility envelope for guard/mid/tip over every phase; the samples show that camera placement alone cannot make A3 active or the below-frame recoveries readable.
2. **P1 / low risk: establish a true eye/skull reference and compare it against the current `CameraSnap`.** Correct the reference relationship and offset at the rig/authoring source. Retain world-space view rotation if desired, but document whether authored head motion should translate the eye. Do not compensate with camera-only sword motion.
3. **P1 / low-to-medium risk: retest near clipping after the eye relationship is corrected.** 0.30 m demonstrably cuts the guard and, in A2 recovery, the entire sword. Lowering the near plane without resolving the trajectory only trades disappearance for large, distorted close geometry; choose it only after measuring facial/arm clipping at the corrected eye point.
4. **P2 / medium risk: implement the planned first-person hands as render-only presentation after the above.** Hide only the local full-body renderer and render hands driven by the existing animator. Keep the physical full rig, one sword, hitbox, and animation path authoritative. This can remove face/shoulder/body self-occlusion but cannot correct the sampled shared-sword framing on its own.
5. **P2 / medium risk: verify owner-versus-server pose under remote latency.** Root yaw is not locally predicted while the torso is. Measure the visual/collision difference on an owning remote client before changing gameplay limits.

## E. Limitations

- The runtime tooling could measure pose and frustum geometry, but did not provide a deterministic frame-step capture pipeline that preserves a manually sampled animator state through the following Game View render. A normal runtime capture was saved outside the project, but it is not used as phase evidence because the following frame may advance/replace the forced pose.
- Full continuous trajectories were not sampled at every frame; the table uses a representative midpoint in windup, active, and recovery. The confirmed full invisibility cases are sufficient to establish the diagnosis, but any correction needs a frame-by-frame acceptance test.
- No separate remote client with induced latency was launched, so the owner prediction discrepancy is an architectural risk documented in code rather than a reproduced defect.
- Facial/shoulder renderer clipping was not isolated by toggling renderers, because the assignment forbids changing that arrangement. The hierarchy shows the local full body remains rendered, so it remains a plausible secondary source of occlusion to test after the camera reference is corrected.
