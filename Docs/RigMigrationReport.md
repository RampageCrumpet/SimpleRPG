# Moving the game body to the canonical A-pose rig

Status: **report for approval, nothing implemented.** Written 2026-11-04 from the repository and `Docs/LocalAgentHandoffReport.md`; not run in Unity.

## Where things are

- **Canonical rig:** 52 bones, A-pose, names in `Authoring/GameRig_Bones.json` (outside this repo, in the Blender folder). All armor is authored and exported on it (`Docs/Equipment.md`).
- **Game body:** `Assets/Art/3d Models/Human/Human_Male.fbx` is the discarded T-pose rig. `HumanMaleSetup` builds `HumanMale_Rig.prefab` and the eight bare-body parts from it, and `TestCharacter.prefab` uses its Humanoid avatar (`m_Avatar` -> `Human_Male.fbx`).
- **Effect today:** armor binds without error (same 52 names) but is skinned A-pose bind poses onto a T-pose skeleton, so the arms are pulled from A to T. Sleeves and gauntlets sit slightly off, and this cannot be fixed per item.
- The three longsword attack FBXes (and the skeleton clips) each import with `avatarSetup: 1` (an avatar from their own file). They are Humanoid, so they play through muscle space, not bone angles.

## What has to change, and what probably does not

| Area | Needs work? | Why |
|---|---|---|
| Body mesh, skeleton, avatar (`Human_Male.fbx`) | **Yes, the core change** | Export the body on the canonical rig with the same validated settings as `export_armor.py`; configure the Humanoid avatar (Enforce T-Pose in the avatar editor so muscle zero is correct for an A-pose model). |
| Body parts and `HumanMale_Rig.prefab` | Regenerate | Run `HumanMaleSetup` again. It updates parts in place, so ids survive. The player's 8 default parts stay registered. |
| Armor parts | **No change** | Already skinned to the canonical bones; they start fitting. Re-run the conformance check below to prove it. |
| Longsword and other Humanoid clips | **Probably no re-authoring** | Muscle-space playback is independent of the body's rest pose. Needs re-import against the new avatar and a visual check; the pelvis/mask rules in `Docs/Animations.md` still hold. If a clip FBX is itself T-pose-derived, its own avatar is the thing to verify. |
| Animator controller, masks | No | They name Humanoid body parts, not bones. |
| `HandGripIK`, `Grip_Left` | Check | The solver is built to survive "a body whose arms are not the proportions the clip was made for". Grip positions are on the sword, not the body. Confirm after the swap (`LongswordGripTests`). |
| Sword mount (`Longsword` under `RightHand`) | **Yes, recalibrate** | Local position `(-0.055777, 0.104543, -0.037857)` and rotation were fitted to the T-pose hand. The hand's rest orientation changes, so the mount changes. |
| Hurtboxes | **Yes, re-fit** | They are shapes on bones; the bone axes and lengths change slightly. Visual check in play; hit-region tests still apply. |
| Eyebrows (rigid, on `Head`), Headgear | Re-check | Rigid pieces take their pose relative to the head bone. The head-socket work (below) replaces the "same space as the body model" assumption. |
| `Docs/IdlePlan.md` | Re-derive | Its arm angles are measured from T-pose rest. |
| Skeleton monster | **Not affected** | Separate model and clips (`Skeleton_Model.fbx`, own rig). |
| Network, gameplay, stats | None | Appearance ids and bone names are unchanged. |

## Order of work

1. **Conformance validator (no migration needed, do first).** An editor test that loads the canonical bone list and compares, for each rig in the project (body, every armor part's skinning), bone names, parent chain and bind-pose world positions to within a tolerance. Today it reports the body as the outlier; after the migration it must pass. This is the one reliable guard against the mismatch returning.
2. Export the A-pose body (Blender, local agent), import, set up the avatar, re-run `HumanMaleSetup`.
3. Re-import the Humanoid clips; play each state and compare with the numbers in `Docs/Animations.md` (shoulder yaw table).
4. Recalibrate sword mount; verify `HandGripIK`; re-fit hurtboxes.
5. Put a `Headgear` socket on the rig (below); move the sallet and eyebrows onto it.
6. Run all EditMode and PlayMode tests; look at the first-person and third-person views.

## Head attachment (independent of the migration)

Rigid pieces on a head must not depend on the body model's space. The rig prefab gets a named child under `Head` (a socket such as `Headgear_Socket`) at an authored pose, built by `HumanMaleSetup` so it survives regeneration. Rigid parts attach to a socket by name, and the sallet's origin (the bottom of the helmet) is authored to match the socket. Sockets are also where the visor would hang later. This is a small addition and is not a rig redesign.

## Risks and open items

- Muscle-space playback assumes the avatar's T-pose is right; the check is the shoulder-yaw table, hands on the hilt, and no arm drift in Idle.
- Body proportions may differ between the two body files; if so, hurtboxes, camera height (`AssetScale.md` says about 1.69 m) and the capsule are checked too.
- I have not seen the A-pose body FBX; this needs the local agent to export it and confirm the Blender scene path (open item 5 of the hand-off report, where the saved `..._Canonical52Bone_...` copy lives).
- Cleanup of the agent's leftover in-progress Blender files, which you are dispatching separately, should come before the export so the right scene is used.

## Decision needed

Approve steps 1 to 6 as the migration (step 1 and the head socket can start now). If you want the clips re-authored on the A-pose rig instead of re-imported, say so; I think that is unnecessary.
