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

Copy canonical FBX/maps into the Unity asset folder. Use Unity MCP for Editor operations. Configure ModelImporter with global scale 1, file scale on, axis-conversion baking off, imported normals, calculated MikkTSpace tangents, mesh compression off, and animation/cameras/lights off. Remap `SalletSteel` to the existing URP Lit material with `AddRemap`.

`prepare_textures.py` copies exports to runtime/viewer locations and packs **R = metallic, A = 1 - roughness** into `Sallet_MetallicSmoothness.png`. Also synchronize updated maps back into the canonical Art folder. Roughness is not smoothness.

| Map | Import | URP property |
| --- | --- | --- |
| BaseColor | Default, sRGB on | `_BaseMap` |
| Normal | NormalMap, sRGB off | `_BumpMap` |
| MetallicSmoothness | Default, sRGB off, input alpha | `_MetallicGlossMap` |
| Occlusion | Default, sRGB off | `_OcclusionMap` |

Use mipmaps, 2048 max size and high-quality compression. Enable `_NORMALMAP`, `_METALLICSPECGLOSSMAP` and `_OCCLUSIONMAP`. Use white tint, metallic/smoothness multipliers 1, normal strength 1 and occlusion strength 0.6. Smoothness comes from metallic-map alpha. The tested normal requires no green-channel flip.

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
