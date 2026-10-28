KEYED = [M+n for n in ('Hips','Spine','Spine1','Spine2','Neck','Head','RightShoulder','RightArm','RightForeArm','RightHand',
                       'LeftShoulder','LeftArm','LeftForeArm','LeftHand','RightUpLeg','RightLeg','RightFoot','LeftUpLeg','LeftLeg','LeftFoot')]

def _rot(q):
    q = np.asarray(q, float); q = q/np.linalg.norm(q)
    return csc.math.Rotation.from_quaternion(csc.math.Quaternion(float(q[0]), float(q[1]), float(q[2]), float(q[3])))

def write_keys(ds, sols, label="write keys", joints=KEYED, sword=True):
    mv = ds.model_viewer(); bv = mv.behaviour_viewer()
    ids = {mv.get_object_name(i): i for i in mv.get_objects()}
    def did(name, data):
        return bv.get_behaviour_data(bv.get_behaviour_by_name(ids[name], 'Transform'), data)
    def mod(model, update, scene_updater):
        de = model.data_editor(); le = model.layers_editor(); lv = ds.layers_viewer()
        objs = list(joints) + (['Longsword_01'] if sword else [])
        layer_ids = list(lv.layer_ids_by_obj_ids([ids[n] for n in objs]))
        for fr, d in sorted(sols.items()):
            actuals = set()
            for n in joints:
                i = did(n, 'local_rotation'); de.set_data_value(i, fr, _rot(d['lq'][n])); actuals.add(i)
            hp = did(M+'Hips', 'local_position'); de.set_data_value(hp, fr, np.array(d['hips'], dtype=np.float32)); actuals.add(hp)
            if sword:
                P0, R0 = d['sword']
                gp = did('Longsword_01', 'global_position'); de.set_data_value(gp, fr, np.array(P0, dtype=np.float32)); actuals.add(gp)
                gr = did('Longsword_01', 'global_rotation'); de.set_data_value(gr, fr, _rot(R0)); actuals.add(gr)
            for l in layer_ids: le.set_fixed_interpolation_or_key_if_need(l, fr, True)
            model.set_fixed_interpolation_if_need(actuals, fr)
            scene_updater.run_update(actuals, fr)
    return ds.modify_update(label, mod)

def read_globals(ds, names, frame):
    mv = ds.model_viewer(); bv = mv.behaviour_viewer(); dv = mv.data_viewer()
    ids = {mv.get_object_name(i): i for i in mv.get_objects()}
    ds.set_current_frame(frame)
    out = {}
    for n in names:
        b = bv.get_behaviour_by_name(ids[n], 'Transform')
        out[n] = np.array(dv.union_get_data_value(bv.get_behaviour_data(b, 'global_position')), float)
    return out
