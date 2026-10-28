# Cascadeur Python API notes (as used through the MCP script server)

Cascadeur 2026.2.x. The MCP server (`MCP.Start script server` in Cascadeur) listens on `127.0.0.1:8765`; each script runs on the next scene-idle event. Log: `%LOCALAPPDATA%\Nekki Limited\Cascadeur\logs\cascadeur_log.log` (python tracebacks inside `modify` callbacks appear there even when the MCP reply says ok; the `modify_update` return value is False on failure, always check it). A modify callback exception is also printed as an Error message in the reply.

## Getting scenes
```python
import csc
app = csc.app.get_application(); sm = app.get_scene_manager()
ns = sm.current_scene()            # csc.view.Scene: name(), get_path_name(), save(path), animation_boundary(), domain_scene()
ds = ns.domain_scene()             # model_viewer(), modify_update(name, cb), set_current_frame(f), layers_viewer(), assets_manager()
mv = ds.model_viewer(); bv = mv.behaviour_viewer(); dv = mv.data_viewer(); lv = ds.layers_viewer()
ids = {mv.get_object_name(i): i for i in mv.get_objects()}      # 328 objects in the rig scene
```
- Load a file into a new tab (does not touch other tabs): `t = sm.create_application_scene(); sm.set_current_scene(t); csc.app.ProjectLoader.load_from(path, t.domain_scene())`. It is named `untitled.casc` with no path; `t.save(path)` attaches the path. Remove with `sm.remove_application_scene(t)` (does not save). After removing tabs call `sm.set_current_scene(...)`.
- Undo by the user can empty a tab (0 objects) while the file on disk is intact. Recovery: back up the disk file, load it into a fresh tab, close the emptied tab without saving, save the good tab to the path.
- Object types: Joint (67), Point (50), Limb Direction (4), Box (60), Basic (53, includes Triangle / Default Rotation), Edge, Rigid Body, Spline IK (2), Transform helpers, Mesh Object (3), Center of Mass.
- Per-joint rig links live on the joint's `_Box` object (`RigAdditionalInfo`: joint, main_point, direction_point, additional_point, limb_direction, triangle). `bv.get_behaviour_object(b, prop)` or `bv.get_behaviour_owner(bv.get_behaviour_reference(b, prop))`.

## Reading data
```python
b = bv.get_behaviour_by_name(ids[name], 'Transform')   # behaviours: Transform, Basic, FbxExport, RigAdditionalInfo (on _Box), BoxView ...
d = bv.get_behaviour_data(b, 'global_position')        # also local_position, global_rotation, local_rotation
v = dv.get_data_value(d, frame)                        # numpy float32[3]  or csc.math.Rotation
q = rot.to_quaternion(); w, x, y, z = q.w(), q.x(), q.y(), q.z()    # methods, not attributes
```
`dv.union_get_data_value(d)` is **frame 0 only**. `dv.get_data_value(d, frame)` raises if frame >= animation size ("last frame in animation is N").
Rotation constructors: `csc.math.Rotation.from_quaternion(csc.math.Quaternion(w, x, y, z))`, `from_angle_axis(angle, np.float32 vec3)`, `from_euler(x, y, z)`. A `Mesh` asset: `ds.assets_manager().at(bv.get_behaviour_asset(mesh_behaviour, 'mesh')).positions()` (homogeneous, metres).

## Timeline
- A file loaded from the ReadyPose has animation size 2 (frames 0-1). To get N frames: for every layer `le.set_section(csc.layers.layer.Section(), N, layer_id)` then `model.fit_animation_size_by_layers()` and `scene_updater.generate_update()`. This **clears all keys** and leaves default-valued stray keys at frame 1 and frame N; remove them with `le.unset_section(frame, layer_id)`. Resulting animation size is N+1.
- `ns.animation_boundary()` has settable `first_frame` / `last_frame` (playback/export range). It reverts to the loaded file's range (0..100) on reload, so set it again before saving.

## Writing keys
```python
def mod(model, update, su):
    de = model.data_editor(); le = model.layers_editor()
    layer_ids = list(lv.layer_ids_by_obj_ids([obj_id, ...]))
    de.set_data_value(data_id, frame, value)                       # float32 numpy vec3 / csc.math.Rotation
    for l in layer_ids: le.set_fixed_interpolation_or_key_if_need(l, frame, True)
    model.set_fixed_interpolation_if_need({data_id}, frame)
    su.run_update({data_id}, frame)
ok = ds.modify_update("label", mod)            # check ok
```
- New keys are `Interpolation.STEP`. To smooth: copy `dict(layer.sections)`, set `sec.interval.interpolation = csc.layers.layer.Interpolation.CLAMPED_BEZIER` for sections before the last, `le.set_sections(secs, layer_id)`. `BEZIER` (continuous tangents) **overshoots wildly** on unevenly spaced keys (hand moved 30 cm off path); `LINEAR` is safe; `CLAMPED_BEZIER` is smooth and safe. Layer inspection: `lv.layer(lid).key_frame_indices()`, `.section(frame).interval.interpolation`.
- Constant data (e.g. `FbxExport.is_exported`) are set with the no-frame overload: `de.set_data_value(data_id, False)`; the frame overload raises "Only supported Animation data".
- Reading `dv.get_data_value` right after `ds.set_current_frame` in the same script gives frame-0 style stale values; use the explicit frame argument instead.

## Export
`app.get_tools_manager().get_tool("FbxSceneLoader").get_fbx_loader(ns).export_joints(path)` (also `export_joints_selected_frames`, `export_model`, ...). With the free/trial licence it logs "Feature not available. The free and trial versions only support .CASC file format." and writes nothing. Exclude the sword from a skeleton export by setting `FbxExport.is_exported = False` on `Longsword_01` and `Longsword_02` (the sword is a separate Unity object on the weapon socket).

## Python environment
Cascadeur embeds Python 3.11 with numpy; the system Python on the dev machine has no numpy. Do heavy numeric work inside Cascadeur, in chunks under about 25 s. Files can be read/written with ordinary `open()` (used for JSON caches). A mistaken `open(path,'w').write(...)` that raises while encoding truncates the file to 0 bytes; build content first, write second.
