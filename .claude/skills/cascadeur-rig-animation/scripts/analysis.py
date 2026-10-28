def _ctx():
    mv = ds.model_viewer(); bv = mv.behaviour_viewer(); dv = mv.data_viewer(); lv = ds.layers_viewer()
    ids = {mv.get_object_name(i): i for i in mv.get_objects()}
    return mv, bv, dv, lv, ids

def tdata(bv, ids, n, what):
    return bv.get_behaviour_data(bv.get_behaviour_by_name(ids[n], 'Transform'), what)

GHq = GR[M+'RightHand'][1]; GHp = GR[M+'RightHand'][0]
OFFP = qrot(qinv(GHq), PSR - GHp); OFFQ = qm(qinv(GHq), RSR)

def bake_sword(frames):
    mv, bv, dv, lv, ids = _ctx()
    sw = {}
    for fr in frames:
        hp = np.array(dv.get_data_value(tdata(bv, ids, M+'RightHand', 'global_position'), fr), float)
        q = dv.get_data_value(tdata(bv, ids, M+'RightHand', 'global_rotation'), fr).to_quaternion()
        hq = np.array([q.w(), q.x(), q.y(), q.z()])
        sw[fr] = (hp + qrot(hq, OFFP), qm(hq, OFFQ))
    def mod(model, update, su):
        de = model.data_editor(); le = model.layers_editor()
        layer_ids = list(lv.layer_ids_by_obj_ids([ids['Longsword_01']]))
        for fr in frames:
            P0, R0 = sw[fr]; acts = set()
            gp = tdata(bv, ids, 'Longsword_01', 'global_position'); de.set_data_value(gp, fr, np.array(P0, dtype=np.float32)); acts.add(gp)
            gr = tdata(bv, ids, 'Longsword_01', 'global_rotation'); de.set_data_value(gr, fr, _rot(R0)); acts.add(gr)
            for l in layer_ids: le.set_fixed_interpolation_or_key_if_need(l, fr, True)
            model.set_fixed_interpolation_if_need(acts, fr)
            su.run_update(acts, fr)
    return ds.modify_update("bake sword to hand", mod)

def metrics(frames=range(131)):
    mv, bv, dv, lv, ids = _ctx()
    J = lambda n, fr: np.array(dv.get_data_value(tdata(bv, ids, n, 'global_position'), fr), float)
    tip = np.array([J('Longsword_02', f) for f in frames]); grip = np.array([J('Longsword_01', f) for f in frames])
    spd = np.linalg.norm(np.diff(tip, axis=0), axis=1)*0.6
    out = {'tip': tip, 'grip': grip, 'spd': spd}
    out['feet'] = {n: np.array([J(M+n, f) for f in frames]) for n in ('RightFoot', 'LeftFoot', 'RightToeBase', 'LeftToeBase')}
    out['head'] = np.array([J(M+'Head', f) for f in frames]); out['hips'] = np.array([J(M+'Hips', f) for f in frames])
    out['rh'] = np.array([J(M+'RightHand', f) for f in frames]); out['lh'] = np.array([J(M+'LeftHand', f) for f in frames])
    return out

def print_metrics(m, step=5):
    spd = m['spd']
    print("tip m/s:", [(i, round(float(spd[i]), 1)) for i in range(0, len(spd), step)])
    print("peak %.1f m/s at f%d; hit window 56-70 mean %.1f max %.1f" % (spd.max(), spd.argmax(), spd[56:70].mean(), spd[56:70].max()))
    for n, pts in m['feet'].items():
        print(n, "range x%.1f y%.1f z%.1f" % tuple(np.ptp(pts, axis=0)), "min height", round(float(pts[:, 1].min()), 1))
    print("head range", np.round(np.ptp(m['head'], axis=0), 1), "hips range", np.round(np.ptp(m['hips'], axis=0), 1))
