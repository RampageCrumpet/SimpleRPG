# Game asset workflow: ThreeJS Generator → Painter → Unity

Start here for the project's GLP/GLB equipment workflow: procedural modeling, UVs, Substance Painter MCP, PBR export, consolidation and Unity import. The visored sallet is the worked example.

## Source locations

| Purpose | Location |
| --- | --- |
| Editable model | `I:/Art/3d Models/Visored_Sallet/VisoredSallet.blend` |
| Textured interchange | `I:/Art/3d Models/Visored_Sallet/VisoredSallet.glb` |
| Unity interchange | `I:/Art/3d Models/Visored_Sallet/VisoredSallet.fbx` |
| Procedural source | `VisoredSallet.js` and `VisoredSallet.factory.txt` in that model folder |
| Texture sources | `I:/Art/3d Models/Visored_Sallet/Textures/` |
| Layered Painter project | `I:/Art/Substance Painter/Visored_Sallet/VisoredSallet_Steel.spp` |
| Frozen painting mesh | `I:/Art/3d Models/Visored_Sallet/VisoredSallet_Paint.fbx` |
| Workbench | `I:/Tools/ThreeJS-Generator` |
| Authoring scripts/reports | `I:/Art/3d Models/Visored_Sallet/Authoring/` outside the Unity repository |
| Imported Unity FBX | `Assets/Art/3d Models/Items/VisoredSallet/VisoredSallet.fbx` |
| Visual prefab | `Assets/Art/3d Models/Items/VisoredSallet/VisoredSallet.prefab` |
| Shared material | `Assets/Art/3d Models/Items/VisoredSallet/SalletSteel.mat` |

External `I:/Art` sources need separate backups; they are outside this Git repository. Unity consumes project-local copies of the FBX and maps. Changing external files alone does not update those copies. Older external Authoring SPP files are backups, not the current painting source.

## Body and rig (read before touching any rig, body or armor)

There is **one game rig and one game body**. Everything else is a mistake waiting to happen; an earlier tangle of T-pose, 64-bone twist and 66-bone animation rigs shipped the wrong body to the game once and wasted days of work.

- **The rig.** 52 bones, A-pose, names in `I:/Art/Blender/SimpleRPGHuman/Authoring/GameRig_Bones.json`. In the authoring scene it is the armature object `Human_Male_ArmorRig`.
- **The scene.** `I:/Art/Blender/SimpleRPGHuman/Human_base_mesh_added_equipment.blend` is the only source for the body, the armor and the rig. Its body meshes are the `ZN_*_ArmorAuthoring` objects in "Human Base - Editable", skinned to `Human_Male_ArmorRig`. Body textures: `I:/Art/Blender/SimpleRPGHuman/Textures/HumanBody`. The procedural skin and equipment materials (`MAT-skin_caucasian_male`, `MAT-mail_hauberk`, `SRPG - Equipment Atlas Tints` and the rest) live in that scene with fake users.
- **Exports.** Only the checked scripts write game FBX files: `Authoring/export_body.py` (`Human_Male.fbx`, with `Socket_Headgear`) and `Authoring/export_armor.py` (armor). Both refuse a rig that is not exactly the canonical bones. Never export with the FBX operator by hand, and never export a mesh or rig that is not named in those scripts.
- **No second rig, ever.** Do not add twist bones, `MCH_` helpers, T-pose copies, "export rigs" or "returned preview" rigs to this scene or to any file under `I:/Art` that is not named `SCRATCH_*.blend`. If an experiment needs a different skeleton, make a file called `SCRATCH_<what>.blend`, name its rig `SCRATCH_<name>`, and **delete the file when the experiment ends**. If the game ever needs more bones, change `GameRig_Bones.json` and the body first (see `Docs/Equipment.md`), then everything else follows; never fork the rig.
- **No pile of snapshots.** Do not save a copy of a whole scene before each step (`Before<Something>.blend`); those copies, each holding a full old rig, are what caused the confusion. Blender's own `.blend1` is the undo for the last save. For a real milestone keep **one** dated backup in `I:/Art/Blender/SimpleRPGHuman/Backups`, and delete it when the next milestone is saved. A backup is never a place to keep work that is not in the scene.
- **Check.** Run `blender -b --factory-startup --python Authoring/scan_rigs.py` after any rig work. It reads only the armature data of every `.blend` under `I:/Art` and lists any file holding a rig that is not the canonical one; fix or delete what it lists.
- **Animation sources.** Cascadeur scenes and animation FBX files are Humanoid clips and play on the game body through its avatar; they are not rigs to export from. The avatar is made in Unity: map all bones, then **Enforce T-Pose** (the body is authored in an A-pose).
- **Agents.** The Beads memory `canonical-human-male` states this in short. If a file or note contradicts this section, this section wins and the other is out of date: correct it.

## Model and approve scale

Use the local procedural workbench. Preserve seed, controls and replayable source. Establish silhouette, thickness, openings and pivots before UVs. Distinguish published measurements from inferred design dimensions: photographs alone do not establish absolute scale.

The sallet is a reference-based interpretation with an approved closed envelope of **225 × 240 × 340 mm (X/Y/Z)** and **5,104 triangles**. One unit is one metre, +Y is up and +Z is forward. Root origin is the bottom of the closed helmet, not a head socket. `VisorPivot` is approximately `(0, 0.130244, -0.005503)` metres; local X rotation 0° is closed and -75° is open.

The JS preset regenerates geometry and a base material; final Painter maps are supplied separately. A browser Library entry is profile-specific and is not a source backup.

## Unwrap and freeze the painting input

Use unique 0–1 UVs for exterior, interior, edge strips and hardware. Preserve source normals. UV splitting must not change shape or triangle count. Review numbered checkers outside, inside and with the visor raised. Check overlaps, collapsed charts, tile bounds, padding and density.

Measured sallet atlas: 55 islands, no overlaps or out-of-tile UVs, 47.57% triangle coverage, minimum spacing 21.25 px and border 10.24 px at 2048; median density about 2,285 px/m. These are results for this asset, not universal requirements.

The **painting FBX intentionally retains four named objects and one material**, `Sallet_Paint`, with vertex-color IDs. Preserve this frozen source: same-mesh bake isolation and masks depend on its names. The optimized game model has two meshes. Do not incidentally replace the Painter mesh with the optimized game FBX.

`UVAtlas.json`, `UVLayout.png`, `UVChecker.png` and `VisoredSallet_UV.blend` accompany the canonical package. `Authoring/UnityExportArchive/UnityMeshData.json` and `Authoring/UnwrapInput.json` in the external model folder describe frozen four-part preparation data, not the runtime hierarchy.

## Connect Painter through MCP

Locally verified: Painter 10.1.2, Python API 0.3.3 and the server at `C:/Users/Alex Van Oss/.codex/tools/substance-painter-mcp`. Read its `LOCAL_SETUP.md` before changing the integration; preserve its compatibility fixes and isolated Python environment.

The startup module `C:/Users/Alex Van Oss/Documents/Adobe/Adobe Substance 3D Painter/python/startup/codex_mcp_bridge.py` serves a loopback bridge on **60199**. Native port 60041 overlaps this machine's Windows reserved range. Ordinary Painter launch is sufficient; changing Windows networking/security is unnecessary.

Prefer discovered `substance-painter` MCP tools. If the current Codex session lacks them, the helper opens an actual MCP stdio session and calls `execute_script`:

```powershell
& 'C:/Users/Alex Van Oss/.codex/tools/substance-painter-mcp/.venv/Scripts/python.exe' `
  'I:/Art/3d Models/Visored_Sallet/Authoring/painter_call.py' 'I:/Art/3d Models/Visored_Sallet/Authoring/export_steel.py'
```

Inspect `project.file_path()`, `needs_saving()` and busy state first. Preserve user changes and save a separate copy before rebuilding layers. Use installed API source documentation for the installed version rather than guessing methods. The bridge dispatches Painter calls on its main Qt thread.

## Bake, paint and export

The current project has one 2048 texture set and eleven editable layers: steel grain, protected grime, roughness variation, handled-edge polish, localized scuffs, scratch lips, small dings, fastener buildup, broken edge abrasion, darker nicks and fresh-steel highlights.

Bake AO, curvature, world-space normals, position and vertex-color ID. The low-poly mesh is its own bake source; there is no high-poly detail transfer. Restrict AO/curvature self-occlusion to the same mesh name so closed-visor contact shadows do not remain painted on the shell when it opens.

Place damage in contact areas and along physical edges, not UV seams. `paint_wear_masks.py` and `paint_edge_masks.py` create surface-positioned masks; `add_wear.py` and `add_edge_wear.py` create separate editable layers. These are imported fill/mask layers, not recorded manual brush strokes. Do not blindly rebuild them over a user-edited project.

`export_steel.py` exports the current project using PBR Metallic Roughness into `I:/Art/3d Models/Visored_Sallet/Authoring/PainterExports`, saves the SPP and writes a report. Confirm the canonical SPP is open first. Export base color, roughness, metallic, OpenGL normal and mesh AO at 2048. Retain height/bake maps for authoring; they are not extra runtime material slots.

## Consolidate after restoring the UV atlas

One UV sheet does not automatically imply one material or draw call. Output one shared steel material and two rigid meshes: `SalletShell` includes stationary hardware; `SalletVisor` includes moving hardware. Preserve `VisorPivot`.

The procedural factory combines meshes **after** restoring the four-part baked atlas. Merge transformed positions/normals, retain UVs and winding, and share one material object. Combining earlier breaks frozen name/topology lookup. The current preset already contains this step; `consolidate_source.py` is a one-time migration, not an idempotent export command.

Workbench `TestExports/Sallet/texture-review.mjs` captures neutral/warm/open-visor reviews and exports an embedded-texture GLB. Cache-bust changed generated modules: this diagnostic folder may retain Vite/browser module caches. `review-consolidated.mjs` exercises the actual isolated workbench import, controls and Library save. Its geometry-only export must not replace the textured delivery.

## Blender/FBX export and Unity setup

`export_consolidated_blender.py` reads the canonical GLB and writes the editable Blender file and FBX. After glTF import it restores atlas V with **`v = 1 - v`** and reconnects original PNGs before saving. Otherwise the Blender/glTF image convention flips the atlas relative to Unity's original maps, causing misplaced details and severe normal-map artifacts. The Blender file packs the four principal PBR images.

Tested FBX settings: global scale 1, apply unit scale, `FBX_SCALE_ALL`, forward -Z, up Y, **`bake_space_transform=False`**, no animation or leaf bones. Experimental space-transform baking broke the child visor hierarchy in this pipeline; do not enable it without a round-trip test.

After re-exporting an FBX or rebuilding a mesh asset in place, the open Unity editor can keep drawing the old mesh from a stale GPU copy even though the data is correct (`BakeMesh` or the inspector shows the new geometry). Before judging a render, force `AssetDatabase.ImportAsset(path, ForceUpdate | ForceSynchronousImport)` and re-upload the mesh (`mesh.UploadMeshData(false)`), or restart the editor.

Copy canonical FBX/maps into the Unity asset folder. Use Unity MCP for Editor operations. Configure ModelImporter with global scale 1, file scale on, axis-conversion baking off, imported normals, calculated MikkTSpace tangents, mesh compression off, and animation/cameras/lights off. Remap `SalletSteel` to the existing URP Lit material with `AddRemap`.

`prepare_textures.py` copies exports to runtime/viewer locations and packs **R = metallic, A = 1 - roughness** into `Sallet_MetallicSmoothness.png`. Also synchronize updated maps back into the canonical Art folder. Roughness is not smoothness.

| Map | Import | URP property |
| --- | --- | --- |
| BaseColor | Default, sRGB on | `_BaseMap` |
| Normal | NormalMap, sRGB off | `_BumpMap` |
| MetallicSmoothness | Default, sRGB off, input alpha | `_MetallicGlossMap` |
| Occlusion | Default, sRGB off | `_OcclusionMap` |

Use mipmaps, 2048 max size and high-quality compression. Enable `_NORMALMAP`, `_METALLICSPECGLOSSMAP` and `_OCCLUSIONMAP`. Use white tint, metallic/smoothness multipliers 1, normal strength 1 and occlusion strength 0.6. Smoothness comes from metallic-map alpha. The tested normal requires no green-channel flip.

**Metal reads black in this game.** The scenes set `reflectionIntensity` to 0 and have no reflection probes, and metal has no diffuse colour, so a surface with metallic near 1 shows only the direct highlights of the torch. The longsword rendered black until the R channel of `Longsword_MetallicSmoothness.png` was scaled by 0.7 (mean metallic 0.77 to 0.54; alpha, the smoothness, untouched). URP ignores the `_Metallic` slider when a metallic map is assigned, so the remap is baked into the texture; the original Painter export is outside the project. For a new metal item, cap the metallic channel around 0.5 to 0.6 when packing the map and check it under the exploration light before shipping.

`setup_consolidated_unity.cs.txt` preserves the existing prefab GUID and references the FBX mesh subassets. Both renderers share the same material. The prefab root has zero rotation and unit scale; FBX axis conversion stays on renderer leaf transforms. The usable hinge is Y-up with a local X opening axis. Do not zero all imported leaf rotations.

This prefab is a visual equipment asset. Collider, inventory definition, head fit and character socket attachment require separate integration for the target character.

## Validate and hand off

Check two renderers, one shared material, one submesh per renderer, 5,104 triangles, complete UVs/normals/tangents, expected bounds, maps/keywords and the moving hinge. Counts are **3,626 GLB vertices** and **3,653 Unity FBX vertices**; FBX adds vertex splits. Maximum UV difference from the frozen atlas is approximately `2.98e-8`.

Independently reload the GLB and check embedded maps, scale and open-visor bounds. Render the actual URP prefab in isolation and review neutral/warm lighting plus the raised visor. Two renderers do not imply two total frame draw calls: shadow/depth passes and rendering features also contribute.

Current reports are `ExportValidation.json` and `TextureValidation.json` in `I:/Art/3d Models/Visored_Sallet/Authoring/UnityExportArchive`; `PainterFinalValidation.json` and `PainterSourceRelocation.json` in the external Authoring folder; and `ModelValidation.json` in the canonical model folder. `check-textured.mjs` validates GLB reloads; `render_unity_preview.cs.txt` renders without changing SampleScene.

Update handoff notes, close the Beads work item and inspect Git status. Under the conservative profile, do not commit, push or sync Dolt without explicit user authorization.

## Repository boundary

Keep authoring sources, Painter projects, masks, export scripts, previews and validation reports outside the Unity repository, under `I:/Art`. The sallet authoring workspace is `I:/Art/3d Models/Visored_Sallet/Authoring`. `UnityExportArchive` inside it retains former project export/review files and their metadata. Historical one-time migration scripts are disabled to prevent recreating authoring folders or report files in the project.

The project sallet folder contains only its FBX, prefab, shared material and four used texture maps, with Unity metadata. Standalone GLB, procedural JS/factory, raw roughness/metallic maps, JSON geometry and screenshots live externally. Keep workflow documentation discoverable in Docs. Do not recreate a project ArtSource folder.

## Blender MCP setup

Use only the **Blender Lab "MCP" add-on** (Blender extension `lab_blender_org/mcp`) with the Claude Desktop "Blender" extension (`blmcp`, tools `mcp__Blender__*`). Both use port **9876**. Never enable Siddharth Ahuja's legacy "MCP for Blender" (`scripts/addons/blender_mcp.py`) or the `uvx blender-mcp` server: it also listens on 9876 but speaks a different JSON protocol, so calls time out.

- Set the Lab add-on's port to 9876 and start its server. If it comes up on 9877, 9876 was busy; free it, set 9876 and restart the server.
- "Cannot connect" means the server is not started or the port is wrong; "timed out" means the wrong add-on or a second client holds the port. `Get-NetTCPConnection -LocalPort 9876` shows the owner.
- The same note is stored as the Beads memory `blender-mcp-setup`.

## Armor wrist interface (approved 2026-11-03)

The game wears **one item per armor slot**, so chest items (linen, gambeson, mail, plate) are alternatives and are never stacked. The only combination that has to work is a chest item with a glove, so each chest item agrees with the glove at the wrist; chest items are never measured against each other. The same rule will hold for plate gloves later.

- **Contract.** `I:/Art/Blender/SimpleRPGHuman/Authoring/WristContract_v1.0_APPROVED.json`. Offsets are measured from the skin surface, along the forearm from the wrist joint (the `LeftHand`/`RightHand` bone head). Every sleeve or vambrace rim sits at least 2.5 cm above the wrist. Hems inside the first 6 cm: linen 2 mm and gambeson 5 mm (cuff over sleeve), mail and plate (sleeve over cuff) pushed clear of the glove. Beyond that a chest item blends back to its original shape by 12 cm.
- **Glove.** The cuff is "tucked" at about 10 mm; its binding is a generated six-sided roll built from the shell rim (`wrist_fit.build_band`), not a hand-modelled band. A rigid cuff, such as a plate gauntlet, will declare a "flared" tier.
- **Check.** `wrist_fit.pair_table()` runs each chest item against the glove on both sides: `[triangle crossings, glove samples within 1.5 mm]`, all zero is clean. Test both quad diagonals, because the engine picks its own at import.
- **Approved state.** `Authoring/ApprovedWristFit_*.npy` and `.json` (vertex snapshot, manifest), plus `Backups/..._WristApproved_*.blend`. The script is **locked**: editing functions refuse to run until `unlock()` is called; `restore_approved()` puts everything back.
- **Which object is shown.** `FINAL_PlateArmor` is a consolidated copy of the editable `GAME_*` plate pieces, with identical vertices; carry a change across with `wrist_fit.propagate_final()`. `FINAL_LinenTunic` and `Gambeson` are not synced yet.
- **Gotchas.** Never run the old stacking entry points (`run_all`, `run_all_hems`): they stack the layers and inflate mail and plate. When an edit looks wrong in the viewport, pick the object under the pixel (`view3d.select`) to find out which one is actually drawn.

## Efficient iteration for procedural models, UVs and skinning

These lessons were recorded after the reinforced leggings work exhausted conversation context twice (2026-11-08). Keep detailed evidence outside the conversation and prove a representative fix before expanding its scope.

- **Bound tool output.** Return explicit names, counts, bounds, maxima and pass/fail, with at most three examples. Store geometry arrays, collision hit lists and edge coordinates in external files and return their paths. Never spread entire part dictionaries into a result: one query included `physicalEdgeSegments` and produced an output reported at roughly 68,000 tokens before truncation. Aim for ordinary results below 1,000 tokens; correct a truncated query instead of repeating it. Discover only the needed tool and avoid printing its full description repeatedly.
- **Keep a short recovery record.** Record the canonical asset, accepted checkpoint, current experimental state, verified passes, unresolved failures and next experiment. Link detailed reports and rejected trials instead of copying their contents into each handoff. Keep authoring code, snapshots and reports under `I:/Art`.
- **Isolate experiments.** Use one hypothesis, one representative garment/pose, a bounded edit and a measurable acceptance criterion. After two or three failed variants, reassess construction or deformation rather than continuing a parameter sweep. Expand to other garments and extreme poses only after the representative case passes.
- **Review poses early.** Inspect an actual posed render alongside collision and coverage checks. A clean rest mesh or lower triangle-pair count does not prove acceptable deformation. Distinguish local fitting failures from different body regions touching in deep flexion. Preserve waist overlap while fixing hardware contacts.
- **Preserve construction and layers.** Move attachments with their host while retaining their original spacing. Independently clamping fabric, straps and hardware to the same target surface collapses the layers. Keep linked buckle shapes rigid and seated. Filter contact-driven weight edits by the original anatomical region; do not bind a calf to a thigh merely because they touch in a bent pose.
- **Make edits reproducible.** Start trials from immutable mesh/weight snapshots, avoid compounding repeated blends, and keep a small external experiment ledger. Save accepted revisions deliberately and label experimental checkpoints separately. Verify the file path and save timestamp; `bpy.data.is_dirty` alone did not reliably indicate whether Python-driven mesh/weight edits were saved.
- **Respect shared boundaries.** Body zones, consolidated body meshes, garment collars and chest hems can share a deformation problem. Inspect their weights together, preserve the canonical rig, and synchronize editable/final copies deliberately. Separate rest-only, standard-motion and complete validation; resolve known geometry failures before final baking/export.
- **Retain Blender/UV fixes.** Refresh UV-layer RNA references after Edit/Object mode changes. Use exact 2D triangle clipping for UV overlaps; coplanar 3D BVH overlap missed defects. For procedural stitching, reject samples separated along the target surface normal so hidden layers do not stamp markings onto the visible layer.

Detailed leggings evidence and recovery notes: `I:/Art/3d Models/ReinforcedLeatherLeggings/Authoring/CONTEXT_LESSONS.md` and `MotionChestFit_Resume.md` in the same folder. These are supporting records; this section is the reusable project workflow guidance.

### Animation fitting and runtime handoff

- Agree the required motion range early, including whether deep crouching and high steps are required. Identify actual controller clips separately from synthetic joint tests.
- Inspect the **actual Armature modifier target** on every consolidated mesh. Rest-identical rig copies must receive the same pose during comparisons. The gambeson initially appeared to clip in walking because its rig copy was left standing while the leggings moved.
- Freeze the **runtime triangulation before fitting animated geometry** and retain editable quads separately. Blender changed quad diagonals as corrective shapes moved; a fit against the original tessellation could fail on the newly evaluated triangles. Preserve vertex order, weights, UV corners, normals and shape coordinates when freezing diagonals.
- Suspend skirt panels from their waist attachment when shaping folds. Treat rigid hardware and its supporting fabric together; let leather bend while metal moves as a rigid assembly. Verify the final evaluated mesh, not only the solver's temporary arrays.
- Blender pose drivers do not become Unity game logic merely by exporting an FBX. Preserve the blend shapes and provide a runtime controller with the same bone-axis convention, Euler order and weight calculation. Compile **and execute** the adapter in Unity; compilation alone did not catch a nested configuration deserialization failure in the validation harness. The current adapter uses the project's existing Newtonsoft package.
- Reuse verified UVs and PBR maps when only skin weights and pose corrections change and the neutral mesh/UVs remain identical. Re-unwrapping and rebaking those unchanged surfaces adds cost without fixing deformation.

The leggings controller source, runtime JSON and validation reports are external under `I:/Art/3d Models/ReinforcedLeatherLeggings/Authoring`. Final Unity delivery must include the updated body waist weights and the pose controller, followed by an equipped animation check. A successful weight-math test is not a geometry or visual round-trip test.
