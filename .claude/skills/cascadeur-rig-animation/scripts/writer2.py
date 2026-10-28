PTS = json.load(open(SP + "points.json"))
def _owner(pn):
    base = pn.split(':', 1)[1] if pn.startswith('mixamorig:') else pn
    for suf in ('_MainPoint', '_AdditionalPoint', '_DirectionPoint', '_Self0Point', '_Self1Point', '_LimbDir'):
        if base.endswith(suf): return M + base[:-len(suf)]
    return None
PT_OWNER = {pn: _owner(pn) for pn in PTS}
PT_OFF = {pn: qrot(qinv(GR[o][1]), np.array(PTS[pn]) - GR[o][0]) for pn, o in PT_OWNER.items() if o in GR}

def point_targets(d):
    G = d['G']
    return {pn: G[PT_OWNER[pn]][0] + qrot(G[PT_OWNER[pn]][1], off) for pn, off in PT_OFF.items()}

def write_keys2(ds, sols, label="write point keys", with_joints=False, sword=True, frames_only=None):
    mv = ds.model_viewer(); bv = mv.behaviour_viewer()
    ids = {mv.get_object_name(i): i for i in mv.get_objects()}
    def did(name, data):
        return bv.get_behaviour_data(bv.get_behaviour_by_name(ids[name], 'Transform'), data)
    def mod(model, update, scene_updater):
        de = model.data_editor(); le = model.layers_editor(); lv = ds.layers_viewer()
        objs = list(PT_OFF.keys()) + (['Longsword_01'] if sword else []) + ([M+'Hips'] if with_joints else [])
        layer_ids = list(lv.layer_ids_by_obj_ids([ids[n] for n in objs]))
        for fr, d in sorted(sols.items()):
            actuals = set()
            for pn, p in point_targets(d).items():
                i = did(pn, 'global_position'); de.set_data_value(i, fr, np.array(p, dtype=np.float32)); actuals.add(i)
            if sword:
                P0, R0 = d['sword']
                gp = did('Longsword_01', 'global_position'); de.set_data_value(gp, fr, np.array(P0, dtype=np.float32)); actuals.add(gp)
                gr = did('Longsword_01', 'global_rotation'); de.set_data_value(gr, fr, _rot(R0)); actuals.add(gr)
            if with_joints:
                for n in KEYED:
                    i = did(n, 'local_rotation'); de.set_data_value(i, fr, _rot(d['lq'][n])); actuals.add(i)
            for l in layer_ids: le.set_fixed_interpolation_or_key_if_need(l, fr, True)
            model.set_fixed_interpolation_if_need(actuals, fr)
            scene_updater.run_update(actuals, fr)
    return ds.modify_update(label, mod)

def compare(ds, sols, frames, names=None):
    mv = ds.model_viewer(); bv = mv.behaviour_viewer(); dv = mv.data_viewer()
    ids = {mv.get_object_name(i): i for i in mv.get_objects()}
    names = names or [M+n for n in ('RightHand','LeftHand','Head','RightFoot','LeftFoot','RightToeBase','LeftToeBase','Spine2','RightArm','RightLeg','LeftLeg','RightForeArm','LeftForeArm')]
    worst = {}
    for fr in frames:
        for n in names:
            b = bv.get_behaviour_by_name(ids[n], 'Transform')
            a = np.array(dv.get_data_value(bv.get_behaviour_data(b, 'global_position'), fr), float)
            e = sols[fr]['G'][n][0]
            err = float(np.linalg.norm(a-e))
            if err > worst.get(n, (0, 0))[0]: worst[n] = (round(err, 2), fr)
    return worst
