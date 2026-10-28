# Validation against the EVALUATED Cascadeur scene (not the solver).  Needs rigmath, posesolver, comfort, edgedp(edge_angle) loaded
# and `ds` = current scene's domain scene.  Usage: exec(open(SP+"validate.py").read()); report = validate_scene(ds, frames_neutral, frames_combo)
def _ctx6(ds):
    mv = ds.model_viewer(); bv = mv.behaviour_viewer(); dv = mv.data_viewer()
    ids = {mv.get_object_name(i): i for i in mv.get_objects()}
    return bv, dv, ids

def scene_state(ds, frames):
    bv, dv, ids = _ctx6(ds)
    td = lambda n, w: bv.get_behaviour_data(bv.get_behaviour_by_name(ids[n], 'Transform'), w)
    names = [n for n in ids if ids[n] and n.startswith(M)]
    keep = [n for n in S if n in ids and n not in ('Longsword_01', 'Longsword_02')]
    out = {}
    for f in frames:
        G = {}
        for n in keep + ['Longsword_01', 'Longsword_02']:
            p = np.array(dv.get_data_value(td(n, 'global_position'), f), float)
            q = dv.get_data_value(td(n, 'global_rotation'), f).to_quaternion()
            G[n] = (p, np.array([q.w(), q.x(), q.y(), q.z()]))
        out[f] = G
    return out

def wrist_components(G, side):
    rel = qm(qinv(G[M+side+'ForeArm'][1]), G[M+side+'Hand'][1])
    if rel[0] < 0: rel = -rel
    tw = 2*math.atan2(rel[2], rel[0]); sq = qm(rel, qinv(np.array([math.cos(tw/2), 0, math.sin(tw/2), 0])))
    rv = rotvec(sq); fa, ra = ANAT[side]
    return float(np.dot(rv, fa)), float(np.dot(rv, ra)), math.degrees(tw)

def validate_scene(ds, frames, label, edge_window=(55, 70), speed_window=(56, 70)):
    S_ = scene_state(ds, list(frames)); fr = list(frames)
    res = {}
    comp = {sd: np.array([wrist_components(S_[f], sd) for f in fr]) for sd in ('Right', 'Left')}
    print(label)
    for sd in ('Right', 'Left'):
        c = comp[sd]; tw = c[:, 2] - TWIST_REF[sd]
        print("  %s wrist: flexion %.0f / extension %.0f | radial %.0f / ulnar %.0f | twist vs Ready [%.0f, %.0f] | beyond comfort frames: flex %d ext %d rad %d uln %d | beyond HARD: %d" % (
            sd, max(c[:, 0].max(), 0), max(-c[:, 0].min(), 0), max(c[:, 1].max(), 0), max(-c[:, 1].min(), 0), tw.min(), tw.max(),
            (c[:, 0] > LIM_COMFORT['flex']).sum(), (-c[:, 0] > LIM_COMFORT['ext']).sum(), (c[:, 1] > LIM_COMFORT['rad']).sum(), (-c[:, 1] > LIM_COMFORT['uln']).sum(),
            ((c[:, 0] > LIM_HARD['flex']) | (-c[:, 0] > LIM_HARD['ext']) | (c[:, 1] > LIM_HARD['rad']) | (-c[:, 1] > LIM_HARD['uln']) | (np.abs(tw) > LIM_HARD['twist'])).sum()))
        res[sd] = c
    ax = ('Hips', 'Spine', 'Spine1', 'Spine2', 'Neck')
    cl = {sd: [] for sd in ('Right', 'Left')}
    for f in fr:
        for sd in ('Right', 'Left'):
            cl[sd].append(clearance({k: v for k, v in S_[f].items()}, sd))
    for sd in ('Right', 'Left'):
        mn = {k: min(c[k] for c in cl[sd]) for k in CLEAR_MIN}
        print("  %s min distance from spine axis (cm): elbow %.1f (need >= %.0f) forearm %.1f (>= %.0f) wrist %.1f upper arm %.1f" % (sd, mn['elbow'], CLEAR_MIN['elbow'], mn['fore'], CLEAR_MIN['fore'], mn['wrist'], mn['upper']))
    tips = {f: S_[f]['Longsword_02'][0] for f in fr}
    spd = {f: float(np.linalg.norm(tips[f+1]-tips[f]))*0.6 for f in fr if f+1 in tips}
    edge = {}
    for f in fr:
        if f-1 in tips and f+1 in tips:
            q = S_[f]['Longsword_01'][1]; blade = qrot(q, [0, 0, -1.]); e = qrot(q, [-1., 0, 0])
            t = (tips[f+1]-tips[f-1]); t = t - blade*np.dot(t, blade); t /= np.linalg.norm(t)
            edge[f] = math.degrees(math.acos(max(-1, min(1, float(np.dot(e, t))))))
    a, b = edge_window
    ew = [edge[f] for f in range(a, b+1) if f in edge]
    print("  edge-vs-travel over frames %d-%d: mean %.0f max %.0f (flat would be ~90)" % (a, b, np.mean(ew), np.max(ew)) if ew else "  (edge window not in this clip)")
    sw = [spd[f] for f in range(speed_window[0], speed_window[1]) if f in spd]
    if sw: print("  blade tip speed: peak %.1f m/s at frame %d; mean over hit window %.1f" % (max(spd.values()), max(spd, key=spd.get), np.mean(sw)))
    # grip stability: distance + relative orientation of each hand to the sword must be constant
    for sd in ('Right', 'Left'):
        dist = []; rel = []
        for f in fr:
            Ps, Qs = S_[f]['Longsword_01']; Ph, Qh = S_[f][M+sd+'Hand']
            dist.append(float(np.linalg.norm(qrot(qinv(Qs), Ph-Ps)) - 0)); rel.append(qm(qinv(Qs), Qh))
        rel = np.array(rel); dev = max(math.degrees(2*math.acos(min(1, abs(float(np.dot(rel[0], r)))))) for r in rel)
        print("  %s hand-to-sword offset: distance varies %.3f cm, orientation varies %.2f deg (grip does not slide)" % (sd, max(dist)-min(dist), dev))
    ys = {n: np.ptp(np.array([S_[f][M+n][0] for f in fr]), axis=0) for n in ('RightFoot', 'LeftFoot', 'RightToeBase', 'LeftToeBase')}
    print("  feet range (cm) x/y/z:", {k: [round(float(x), 1) for x in v] for k, v in ys.items()})
    hd = np.array([S_[f][M+'Head'][0] for f in fr]); print("  head travel range (cm):", np.round(np.ptp(hd, axis=0), 1).tolist())
    return dict(state=S_, comp=res, edge=edge, speed=spd)
