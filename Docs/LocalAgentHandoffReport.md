# Report: bring-up and verification of the latest changes

Answers [LocalAgentHandoff.md](LocalAgentHandoff.md). Written by the agent that had the Unity editor and Blender. Branch `claude/zen-brahmagupta-i40kaz`; this session's commits are `e7397ef` and `e01d256`. Nothing is pushed.

**Read this first.** The bring-up found one real problem the hand-off did not anticipate: every skinned armor piece threw on equip, because the art was skinned to a 64-bone authoring rig (12 arm twist bones) and the game body has 52. It is fixed at the source (the authoring scene and a new export script), not in game code. The game body is still the *old* T-pose rig while the armor is the canonical A-pose rig; that is the main open item (section 7).

## 1. Compile

One error, fixed.

| File | Fix |
|---|---|
| `Assets/Tests/AbilitySystem/EditMode/ArmorRatingTests.cs:187` | `Assert.AreNotEqual(a, b, tolerance, msg)` has no such overload in NUnit; now `Assert.That(a, Is.Not.EqualTo(b).Within(1e-6), msg)`. Same intent. |

`python3` on this machine is only the Microsoft Store shortcut; `layering_check.py --summary` ran with `C:\Python314\python` and reports no upward references and no leaf-order violations. Validate Definitions: clean at the start and at the end.

## 2. Tests

- **EditMode (first run):** 1513 passed, 0 failed, 0 skipped. `AssemblyLayeringTests` is in that run and passed.
- **PlayMode (first run):** 359 passed, 8 failed. All 8 were the predicted exact-damage assertions on a *player* victim. Each new number is the formula (`0.9 x 10 / 110`, about 8 percent, rounded once), so the expectations were updated, not loosened:

| Test | Old | New |
|---|---|---|
| `Player_Head_IsAHeadshot`, `ARevivedPlayer_CanBeStruckAgain` | 53 | 48 |
| `Player_Chest_IsATorsoBlow` | 30 | 28 |
| `Player_UpperArm/Forearm/Thigh/Shin` (4 tests) | 23 | 21 |
| `AProjectile_FlyingIntoACharacter_HitsItThroughRealPhysics` (player target, magical) | 20 | 18 |

- **PlayMode (rerun):** 367 passed, 0 failed. The owner then ran PlayMode as well and reported all green.
- **Not rerun since:** everything after that point (Headgear slot, `ArmorSetupTests` change, the new part-bone test, armor re-exports, `Equipment.RaiseChanged`) was checked in the editor and by direct calls, **not** by a full test run. Run both suites before trusting the branch.
- One test changed in `e01d256`: `ArmorSetupTests` now expects a helmet's part slot to be `Headgear`. One test added: `EveryArmorPart_IsSkinnedOnlyToBonesTheBodyRigHas`.

## 3. Tool output (verbatim)

The dialog text is identical to the Console `[Armor]` line. First run:

```
[Armor] Armor: 10 new item(s), 14 part(s) written or updated, 1 to look at.
  - VisoredSallet: no icon at Assets/Art/3d Models/Items/VisoredSallet/VisoredSallet_Icon.png; the item has none.
```

The owner said a placeholder is fine for now, so the sallet's item uses the Kettle Helm icon. Second run, nothing changed (all 51 generated files hash-identical before and after):

```
[Armor] Armor: 0 new item(s), 14 part(s) written or updated, 0 to look at.
```

Later runs (after the headgear slot move, the re-exports and the Gambeson) reported `0 new item(s)`; I did not capture their dialog text.

Checks done on the result: ten `ArmorData` assets with the right slot, size (2x2 head and hands, 2x3 chest) and exactly one grant `simplerpg.stat.armor_rating` Add; every part has **Dresses Item** set and the right slot; `BodyPartDefinition.Problem()` is empty for all; the registry knows them; Validate Definitions clean.

**Warning for automation:** the tool's `EditorUtility.DisplayDialog` blocks the editor's main thread, so the MCP bridge times out and every later call fails until someone clicks OK. A batch or CI path should skip the dialog.

## 4. Armor in play

Checked in a live editor session (host, one client: the host itself). **Not done:** the second-client checks, the helmet-hidden-from-wearer check by eye, the damage-with-and-without-plate fight, and the end-of-delve **Exit dungeon** flow.

| Piece | Result |
|---|---|
| Kettle Helm | Renders on the head in the inventory preview, face visible underneath. Hidden from the owner's first-person view by design. |
| Padded Coif | Same: renders, face visible. |
| Visored Sallet | **Wrong place.** The part sits about 1.1 m below the head bone (world y 0.40 against a head at 1.54), so it is not visible on the head. Owner asked to set it aside; needs more work. |
| Gambeson (retopo) | Equips and renders in the preview. Not judged for look. |
| Reinforced Gloves | Equip and render. |
| Linen Tunic, Mail Hauberk, Plate Armor, Leather Gloves, Plate Gauntlets | Bind with the real `BodyPartBinder` (no exception); not looked at one by one. |

All 22 body and armor parts bind onto the rig prefab with `BodyPartBinder.Create` and no bone or bind-pose mismatch.

## 5. Numbers

Armor Rating and Physical Damage Reduction, read from the character's attributes:

| State | Armor Rating | Physical Reduction |
|---|---|---|
| Bare | 0 | 0.0818 |
| Kettle Helm | 12 | 0.1782 |
| Gambeson + Reinforced Gloves + Kettle Helm | 28 | 0.2787 |

Swapping two identical helms four times kept 28 throughout. Damage with and without plate armor was **not measured**.

## 6. Prefab table

After re-export every skinned armor renderer has **52 bones**, one skin per piece, no null bones.

| Prefab | Renderers | Bones | Materials |
|---|---|---|---|
| Gambeson | `Gambeson` (skinned, 4446 vertices after import) | 52 | GambesonGame (new) |
| Kettle Helm | `Armor_KettleHelm` (skinned, own rig) | 6 | KettleHelmSteel |
| Leather Gloves | `GloveGame_L`, `GloveGame_R` | 52 each | LeatherGloves_L, LeatherGloves_R |
| Linen Tunic | `FINAL_LinenTunic` | 52 | LinenTunic |
| Mail Hauberk | `GAME_MailHauberk` | 52 | MailHauberk |
| Padded Coif | `PaddedCoif` (was `PaddedCoif.001`) | 52 | PaddedCoif |
| Plate Armor | `PlateArmor`, plus a `MailHauberk` undercoat renderer (from `MailHauberk.fbx`; intentional) | 52 each | PlateArmor, MailHauberk |
| Plate Gauntlets | `GauntletPlate_L`, `GauntletPlate_R` | 52 each | PlateGauntletSteel + PlateGauntletBrass |
| Reinforced Gloves | `GloveReinf_L`, `GloveReinf_R` | 52 each | ReinforcedGloves_L, ReinforcedGloves_R |
| Visored Sallet | `SalletVisor`, `SalletShell` (plain, rigid) | none | SalletSteel |

## 7. Anything surprising, and questions for the owner

**The rig.** Every rig in the Blender authoring scene (armor, body, attack and controls-review, export rigs) has the same 52 canonical bones at identical A-pose positions (max difference 0); extras are authoring-only twist bones and `MCH_` scapula helpers on the animation rigs. The one outlier is `REFERENCE_Old_Rig`, and Unity's `HumanMale_Rig` prefab is that old rig, in T-pose. The owner confirmed the canonical rig is the 52-bone A-pose one.

**What was done about it**
- The twist bones were removed from `Human_Male_ArmorRig` and `Gambeson_Rig` in the open scene, weights merged into the parent arm bones (exact in rest pose: no pose, constraints, drivers or parented objects on them). Saved only as copies: `Backups/Human_base_mesh_added_equipment_BeforeTwistRemoval_20261104.blend` (before) and `..._Canonical52Bone_20261104.blend` (after). The open file and the original were not overwritten.
- New scripts in `I:/Art/Blender/SimpleRPGHuman/Authoring/` (outside this repo): `export_armor.py` (validates the rig and weights, exports with settings proven to reproduce the old files bone-for-bone, reads the FBX back), `pack_item_textures.py` (BaseColor/Normal copied, Metallic and Roughness packed; reproduced the shipped Linen maps exactly), `GameRig_Bones.json`.
- The seven pieces that matched the scene were re-exported. The **Gambeson** was different in the scene (2143-vertex retopology) from the shipped file (29,000 vertices); the owner chose the scene's, so it was exported with its own maps and a new `GambesonGame.mat`. The old Gambeson textures, shader and cloth prototypes are untouched.
- A tool-side bone-fitting workaround was tried and reverted at the owner's direction: no stopgaps.
- `ArmorSetup` now refuses a skinned part with bones the body lacks, and a test fails if a shipped part has one.

**Other changes in `e7397ef` and `e01d256`**
- Skeletons carry one random armor piece (Always entry in `SkeletonFootmanLoot`); camera can look down to 85 degrees; armor can be equipped by dragging onto the paper doll as well as right-click.
- New body-part slot **Headgear** (value 8, append-only) so a helmet is worn over the bare head instead of replacing it (the helmet had hidden the face). `OwnerViewBodyHider` hides Headgear from the owner too.
- `Equipment` now raises `Changed` one listener at a time and logs a failure, so a failing view can no longer abort an equip before its stat grants apply. Added because the owner saw the stat panel change as if a helm were unequipped while still wearing one; I could not reproduce that and this is a plausible cause, **not confirmed**.
- Unity reserialized `TestCharacter.prefab` (moved a component block, filled two default fields), nine `.mat` files and `Flat_Ceiling_High.prefab` by itself; the owner approved committing those.

**Decisions and questions needing the owner**
1. **Move the game body, rig and animations to the canonical A-pose rig** (beads `simplerpg-do1`). Until then armor binds without error but is posed A-to-T by skinning, so sleeves and gauntlets may sit slightly off. Risks to check: attack clips (Humanoid, own avatars), hurtboxes, `HandGripIK`/`Grip_Left` and the sword mount calibration in `TestCharacter.prefab`.
2. **Visored Sallet seat.** The tool places a rigid helmet assuming the prefab shares the body model's space, but the sallet's origin is the bottom of the helmet. Needs an authored head socket; a hand-edit of the part asset would be overwritten by the next tool run.
3. **Should hats be visible to their owner in first person?** Currently hidden by design; if "hats don't render" meant that, say so.
4. **Twist bones later.** If wanted, add them to the body rig first and to `GameRig_Bones.json` after, and something must drive them (Unity humanoid animation does not).
5. **Where should the Blender scene be saved?** The open file is a crash-recovery copy under `Backups/`; the edits exist only in the `..._Canonical52Bone_...` copy.
6. **Beads.** `simplerpg-do1` (body rig) and `simplerpg-ujf` (Gambeson, not closed until seen in play) are open.

Run the full EditMode and PlayMode suites on this branch before relying on it.
