# Longsword combo: clip ranges and Unity transitions

60 fps, frame numbers are the Cascadeur timeline frames of each project. Skeleton only (sword is a separate object on the right-hand weapon socket). No root translation: feet stay in place (foot steps are in-place IK adjustments).

Every attack is split into three independently usable pieces. The **combo continuation** and the **neutral recovery** are alternative branches that start from the SAME pose, the last frame of the active hit. Only Attack 1 starts from the Ready stance; Attack 2 starts on Attack 1's combo end, Attack 3 on Attack 2's combo end.

| Attack (project) | Attack range (windup + hit) | Combo continuation | Neutral recovery |
|---|---|---|---|
| 1 Right Diagonal (`Longsword_Attack1_RightDiagonal.casc`) | 0-70 (windup 0-56, hit 56-70) | 210-227 (= 70-87 of the combo track, 140 + frame) | 70-130 (ends on Ready) |
| 2 Left Diagonal (`Longsword_Attack2_LeftDiagonal.casc`) | 0-55 (windup 0-38, hit 38-55) | 195-198 (= 55-58 of the combo track) | 55-112 (ends on Ready) |
| 3 Centerline Thrust (`Longsword_Attack3_CenterlineThrust.casc`) | 0-59 (windup 0-45, hit 45-59) | none | 59-118 (ends on Ready) |

Durations (60 fps): A1 attack 1.167 s, combo 0.283 s, recovery 1.0 s. A2 attack 0.917 s, combo 0.05 s, recovery 0.95 s. A3 attack 0.983 s, recovery 0.983 s.
Attack 2 note: the hit was deliberately slowed on request (17 frames = 0.283 s instead of 0.233 s) and the windup shortened to 38 frames (0.633 s instead of 0.679 s) so the original 0-55 / 55-58 / 55-112 boundaries are kept.

## Timeline layout inside each project
- Neutral track: frames 0 to N (attack then recovery in one continuous strip). Attack = first part, Recovery = the rest.
- Combo track (Attacks 1 and 2): frames 140 + f. Frames 140 to 140 + hit end repeat the attack frames exactly (so Combo frame 210 of Attack 1 equals Neutral frame 70, to 0.0 cm); only the continuation part is needed for export.
- Attack 3 has a single track 0-118.

## Boundary guarantees (measured on the evaluated skeleton)
- Attack 1 frame 70 (Neutral) == frame 210 (Combo): all joints identical (0.0 cm). Attack 2 frame 55 == frame 195: identical (0.0 cm). Frames 0 to hit end identical in both tracks.
- Attack 2 frame 0 == Attack 1 combo end (frame 227): same body, hands and sword pose (within the 0.3 cm solver/scene tolerance); Attack 3 frame 0 == Attack 2 combo end (frame 198): same vectors.
- Tip speed across the seams: A1 combo end 2.4 m/s -> A2 start 1.8 m/s; A2 combo end 1.2 m/s -> A3 start 0.8 m/s. All neutral recoveries end on the Ready pose (0.0 difference from Ready).
- Active hit windows (use for damage sampling): A1 frames 56-70 (blade peak 23 m/s), A2 frames 38-55 (peak 19.6 m/s), A3 frames 45-59 (tip peak 3.2 m/s, the thrust).

## Unity import (one FBX per project when export is available)
Model importer, Rig = Humanoid, Animation tab, add clips (loop off for all):
| Clip name | First frame | Last frame |
|---|---|---|
| Longsword_A1_Attack | 0 | 70 |
| Longsword_A1_Combo | 210 | 227 |
| Longsword_A1_Recovery | 70 | 130 |
| Longsword_A2_Attack | 0 | 55 |
| Longsword_A2_Combo | 195 | 198 |
| Longsword_A2_Recovery | 55 | 112 |
| Longsword_A3_Attack | 0 | 59 |
| Longsword_A3_Recovery | 59 | 118 |
Bake rotation and XZ position into pose (no root motion), Y position original. Do not include the sword joints in the export; attach the sword to the right-hand socket (same relation as used in Cascadeur, the right-hand grip was not changed).

## Animator transitions
- Idle/locomotion -> A1_Attack: normal crossfade (about 0.1-0.15 s). Only this entry starts from Ready.
- End of an Attack clip (exit time 1.0, the active hit has just finished): if the next attack is queued -> `Combo` clip with 0 s transition and no offset; at the end of the Combo clip -> next attack's `Attack` clip with 0 s transition. Their first and last poses are identical, so no crossfade, no exit-time offset and no extra delay are needed. If no attack is queued -> `Recovery` clip with 0 s transition; at its end -> Idle with a short crossfade (the clip ends exactly on Ready).
- Chain: A1_Attack -> (A1_Combo -> A2_Attack -> (A2_Combo -> A3_Attack -> A3_Recovery | A2_Recovery) | A1_Recovery). Attack 3 always ends in its Recovery.
- Queue window: accept the next-attack input from the start of the attack until its last frame (the end of the active hit); decide at that frame. A press during a Recovery clip may start the next attack with a short crossfade (about 0.15 s) from the recovery pose to the next Attack clip's first frame (poses do not match there, so keep it short), or be ignored.
- Disable "interruption" and "can transition to self" on these states; transitions are on the clip end only.
- First-person camera on the head: head yaw stays within 15-23 deg of the body's forward direction and rotates under 1.1 deg per frame in all clips.

## Known limits
Unity import and the FBX export have not been run (no Unity licence; Cascadeur free licence blocks FBX export). Wrist limits are respected except a few frames about 1 deg past them (A2 windup right flexion 73 vs 72, A1 left ulnar 36 vs 35).
