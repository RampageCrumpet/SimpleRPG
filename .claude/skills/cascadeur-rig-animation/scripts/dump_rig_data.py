# Run INSIDE Cascadeur (mcp__cascadeur__run_script) with the canonical Ready-pose scene as the current scene
# (or load it into a throw-away tab first). Writes skel.json and points.json into SP (the work dir).
#
#   SP = "C:/work/cascadeur/"          # trailing slash, forward slashes
#   exec(open(SCRIPTS + "dump_rig_data.py").read())
#
# skel.json   : per joint {parent, lp (local pos), lq (local quat w,x,y,z), gp (global pos)}  - units cm
# points.json : global position of every rig Point / Limb Direction at the Ready pose (frame 0)
import csc, json, numpy as np
app = csc.app.get_application()
_sc = app.get_scene_manager().current_scene()
_ds = _sc.domain_scene(); _mv = _ds.model_viewer(); _bv = _mv.behaviour_viewer(); _dv = _mv.data_viewer()
_ids = {_mv.get_object_name(i): i for i in _mv.get_objects()}

def _tdata(o, what):
    b = _bv.get_behaviour_by_name(o, 'Transform'); d = _bv.get_behaviour_data(b, what)
    return None if d.is_null() else _dv.get_data_value(d, 0)      # NOTE: pass the frame; union_get_data_value() silently means frame 0

skel = {}
for n, o in _ids.items():
    if _mv.get_object_type_name(o) != 'Joint': continue
    par = _bv.get_behaviour_object(_bv.get_behaviour_by_name(o, 'Basic'), 'parent')
    lq = _tdata(o, 'local_rotation'); q = lq.to_quaternion()
    skel[n] = dict(parent=_mv.get_object_name(par) if not par.is_null() else None,
                   lp=[float(x) for x in _tdata(o, 'local_position')],
                   lq=[q.w(), q.x(), q.y(), q.z()],                  # w(), x() ... are METHODS
                   gp=[float(x) for x in _tdata(o, 'global_position')])
json.dump(skel, open(SP + "skel.json", "w"))

pts = {n: [float(x) for x in _tdata(o, 'global_position')] for n, o in _ids.items()
       if _mv.get_object_type_name(o) in ('Point', 'Limb Direction')}
json.dump(pts, open(SP + "points.json", "w"))
print(len(skel), "joints,", len(pts), "points written to", SP)
