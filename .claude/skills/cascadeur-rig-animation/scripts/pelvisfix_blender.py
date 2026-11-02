"""Hold the pelvis fixed and move its rotation into the spine, keeping UpperChest and everything below it exactly as authored.

usage: blender -b --python pelvisfix_blender.py -- <src.fbx> <dst.fbx> <report.json> [fix|identity] [shared_pelvis_ref.json]
The ref file is written by the first clip and read by the others so every clip in the chain shares one pelvis. See Docs/Animations.md.
identity re-keys the animation one frame earlier without changing it (round-trip check).
"""
import bpy, sys, math, json
from mathutils import Matrix, Quaternion, Vector

SRC, DST, REPORT = sys.argv[sys.argv.index('--') + 1:][:3]
MODE = (sys.argv[sys.argv.index('--') + 1:] + ['fix'])[3]
REF = (sys.argv[sys.argv.index('--') + 1:] + [None] * 5)[4]
import os
ACTION_NAME = 'Cascadeur Export - baked deform skeleton'

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC, use_anim=True, automatic_bone_orientation=False)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
pommel = bpy.data.objects.get('Longsword_pommel_lowpoly')
sc = bpy.context.scene
f0, f1 = int(arm.animation_data.action.frame_range[0]), int(arm.animation_data.action.frame_range[1])
frames = list(range(f0, f1 + 1))
bones = [b.name for b in arm.data.bones]
pb = arm.pose.bones
MW = arm.matrix_world.copy()
U = (MW.to_3x3().inverted() @ Vector((0, 0, 1))).normalized()     # world up, in armature space

# ---- pass 1: read everything that the original animation did -------------------------------------------------------
basis = {}; M = {}
pom = {}
for f in frames:
    sc.frame_set(f)
    basis[f] = {b: (pb[b].location.copy(), pb[b].rotation_quaternion.copy(), pb[b].scale.copy()) for b in bones}
    M[f] = {b: pb[b].matrix.copy() for b in ('Hips', 'Spine', 'Chest', 'UpperChest', 'LeftUpperArm', 'RightUpperArm', 'LeftUpperLeg', 'RightUpperLeg', 'RightHand', 'LeftHand')}
    if pommel is not None:
        pom[f] = (pommel.location.copy(), pommel.rotation_quaternion.copy() if pommel.rotation_mode == 'QUATERNION' else pommel.rotation_euler.copy(), pommel.scale.copy())

def rest_rel(name):
    b = arm.data.bones[name]
    return b.parent.matrix_local.inverted() @ b.matrix_local if b.parent else b.matrix_local

def horiz(v):
    return (v - U * v.dot(U)).normalized()

def descendants(name):
    out = []
    for b in arm.data.bones[name].children_recursive: out.append(b.name)
    return [name] + out

def twist_deg(q):                       # twist about the bone's own Y axis
    return math.degrees(2 * math.atan2(q.y, q.w)) if q.w >= 0 else math.degrees(2 * math.atan2(-q.y, -q.w))

def signed_heading(a, b):               # angle from a to b about world up
    return math.degrees(math.atan2(a.cross(b).dot(U), a.dot(b)))

report = {'mode': MODE, 'frames': [f0, f1]}

# ---- pass 2: new basis --------------------------------------------------------------------------------------------
new = {f: dict(basis[f]) for f in frames}
if MODE == 'fix':
    m0 = M[f0]
    sh = horiz(m0['LeftUpperArm'].translation - m0['RightUpperArm'].translation)
    lg = horiz(m0['LeftUpperLeg'].translation - m0['RightUpperLeg'].translation)
    alpha = signed_heading(lg, sh)          # turn the pelvis so that at Ready it faces the way the shoulders do
    report['alpha_deg'] = alpha
    R_h0 = m0['Hips'].to_3x3(); h0 = m0['Hips'].translation.copy()
    Hn_rot = Matrix.Rotation(math.radians(alpha), 3, U) @ R_h0
    if REF and os.path.exists(REF):                    # every clip of the chain shares the pelvis of the first one
        ref = json.load(open(REF)); Hn_rot = Matrix(ref['rot']); h0 = Vector(ref['pos']); report['shared_pelvis'] = True
    elif REF:
        json.dump({'rot': [list(r) for r in Hn_rot], 'pos': list(h0)}, open(REF, 'w'))
    Mh = Matrix.Translation(h0) @ Hn_rot.to_4x4()
    qh = Hn_rot.to_quaternion()
    leg_bones = descendants('LeftUpperLeg') + descendants('RightUpperLeg')
    tw = {'Spine': [], 'Chest': [], 'UpperChest': []}
    heading = []
    ucpos_shift = {}
    for f in frames:
        qu = M[f]['UpperChest'].to_3x3().to_quaternion()
        if qh.dot(qu) < 0: qu = -qu
        want = {'Spine': qh.slerp(qu, 0.5), 'Chest': qh.slerp(qu, 0.75), 'UpperChest': qu}
        parent_M = Mh
        new[f]['Hips'] = ((Mh.copy() if False else (arm.data.bones['Hips'].matrix_local.inverted() @ Mh)).to_translation(),
                          (arm.data.bones['Hips'].matrix_local.inverted() @ Mh).to_quaternion(), basis[f]['Hips'][2])
        for name in ('Spine', 'Chest', 'UpperChest'):
            base = parent_M @ rest_rel(name)
            Mb = Matrix.Translation(base.translation) @ want[name].to_matrix().to_4x4()
            bm = base.inverted() @ Mb
            new[f][name] = (bm.to_translation(), bm.to_quaternion(), basis[f][name][2])
            tw[name].append(twist_deg(bm.to_quaternion()))
            parent_M = Mb
        ucpos_shift[f] = parent_M.translation - M[f]['UpperChest'].translation
        for lb in leg_bones:
            new[f][lb] = basis[f0][lb]
        a = horiz(M[f]['LeftUpperArm'].translation - M[f]['RightUpperArm'].translation)
        heading.append(signed_heading(lg, a))
    report['new_spine_twist_range'] = {k: [min(v), max(v)] for k, v in tw.items()}
    report['shoulder_heading_vs_ready_pelvis'] = [min(heading), max(heading)]
    # the same numbers for the original animation
    otw = {'Spine': [], 'Chest': [], 'UpperChest': []}
    for f in frames:
        for name in otw: otw[name].append(twist_deg(basis[f][name][1]))
    report['orig_spine_twist_range'] = {k: [min(v), max(v)] for k, v in otw.items()}
    report['hips_shift_cm'] = [max((M[f]['Hips'].translation - h0).length for f in frames) * 100]
    report['upperchest_shift_cm'] = [min(v.length for v in ucpos_shift.values()) * 100, max(v.length for v in ucpos_shift.values()) * 100]
    if pommel is not None:
        for f in frames:
            d = MW.to_3x3() @ ucpos_shift[f]
            l, r, s = pom[f]
            pom[f] = (l + d, r, s)

# ---- pass 3: re-key one frame earlier (the exporter writes frame N at time N/60; the original sits at N+1) --------------
arm.animation_data_clear()
if pommel is not None: pommel.animation_data_clear()
act = bpy.data.actions.new(ACTION_NAME)
arm.animation_data_create(); arm.animation_data.action = act
for f in frames:
    kf = f - 1
    for b in bones:
        loc, rot, scl = new[f][b]
        p = pb[b]; p.rotation_mode = 'QUATERNION'
        p.location = loc; p.rotation_quaternion = rot; p.scale = scl
        p.keyframe_insert('location', frame=kf, group=b)
        p.keyframe_insert('rotation_quaternion', frame=kf, group=b)
        p.keyframe_insert('scale', frame=kf, group=b)
if pommel is not None:
    pact = bpy.data.actions.new(ACTION_NAME)
    pommel.animation_data_create(); pommel.animation_data.action = pact
    for f in frames:
        l, r, s = pom[f]
        pommel.location = l; pommel.scale = s
        pommel.keyframe_insert('location', frame=f - 1)
        if pommel.rotation_mode == 'QUATERNION':
            pommel.rotation_quaternion = r; pommel.keyframe_insert('rotation_quaternion', frame=f - 1)
        else:
            pommel.rotation_euler = r; pommel.keyframe_insert('rotation_euler', frame=f - 1)
        pommel.keyframe_insert('scale', frame=f - 1)

# ---- verify on the re-keyed scene -----------------------------------------------------------------------------------
worst_rel = 0; worst_rot = 0; hips_move = 0
Mh_ref = None
for f in frames[::5] + [f1]:
    sc.frame_set(f - 1)
    uc = pb['UpperChest'].matrix; hand = pb['RightHand'].matrix; lh = pb['LeftHand'].matrix
    rel = (uc.inverted() @ hand).to_translation()
    rel0 = (M[f]['UpperChest'].inverted() @ M[f]['RightHand']).to_translation()
    worst_rel = max(worst_rel, (rel - rel0).length)
    rl = (uc.inverted() @ lh).to_translation(); rl0 = (M[f]['UpperChest'].inverted() @ M[f]['LeftHand']).to_translation()
    worst_rel = max(worst_rel, (rl - rl0).length)
    worst_rot = max(worst_rot, math.degrees((uc.to_quaternion().rotation_difference(M[f]['UpperChest'].to_quaternion())).angle))
    hp = pb['Hips'].matrix.translation
    if Mh_ref is None: Mh_ref = hp.copy()
    hips_move = max(hips_move, (hp - Mh_ref).length)
report['hand_to_upperchest_error_cm'] = worst_rel * 100
report['upperchest_orientation_error_deg'] = worst_rot
report['hips_motion_cm'] = hips_move * 100

sc.frame_start = 1; sc.frame_end = f1 - 1
bpy.ops.export_scene.fbx(
    filepath=DST, check_existing=False, use_selection=False,
    object_types={'ARMATURE', 'MESH'}, use_mesh_modifiers=True, add_leaf_bones=False,
    bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True, bake_anim_step=1.0,
    bake_anim_simplify_factor=0.0, axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_NONE', primary_bone_axis='Y', secondary_bone_axis='X',
    armature_nodetype='NULL', mesh_smooth_type='OFF', global_scale=1.0, path_mode='AUTO')
json.dump(report, open(REPORT, 'w'), indent=1)
