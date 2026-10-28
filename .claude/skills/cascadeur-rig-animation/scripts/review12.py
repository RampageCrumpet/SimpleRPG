# Frame review on the evaluated Cascadeur scene.  Needs session6 + validate.py loaded.  review(ds, f0, frames, step) prints a table for every
# `step`-th frame and flags anomalies over ALL frames: joint position/rotation spikes, wrist limits, clearance, blade-vs-body distance, head motion.
BODY_SEGS = [('Hips', 'Spine'), ('Spine', 'Spine1'), ('Spine1', 'Spine2'), ('Spine2', 'Neck'), ('Neck', 'Head'),
             ('RightUpLeg', 'RightLeg'), ('RightLeg', 'RightFoot'), ('LeftUpLeg', 'LeftLeg'), ('LeftLeg', 'LeftFoot'),
             ('RightArm', 'RightForeArm'), ('LeftArm', 'LeftForeArm')]
CHECK_JOINTS = ['Hips', 'Spine2', 'Head', 'RightArm', 'RightForeArm', 'RightHand', 'LeftArm', 'LeftForeArm', 'LeftHand', 'RightUpLeg', 'RightLeg', 'RightFoot', 'LeftUpLeg', 'LeftLeg', 'LeftFoot']

def seg_dist(a, b, c, d):
    """distance between segments a-b and c-d (clamped closest points)"""
    u = b-a; v = d-c; w = a-c
    A = float(u@u); B = float(u@v); C = float(v@v); D = float(u@w); E = float(v@w)
    den = A*C-B*B
    s = min(1.0, max(0.0, (B*E-C*D)/den)) if den > 1e-9 else 0.0
    t = (B*s+E)/C if C > 1e-9 else 0.0
    if t < 0.0: t = 0.0; s = min(1.0, max(0.0, -D/A)) if A > 1e-9 else 0.0
    elif t > 1.0: t = 1.0; s = min(1.0, max(0.0, (B-D)/A)) if A > 1e-9 else 0.0
    return float(np.linalg.norm(w + s*u - t*v))

def review(ds, f0, frames, label, step=3):
    fr = list(frames); S_ = scene_state(ds, [f0+f for f in fr]); n = len(fr)
    def J(i, nm): return S_[f0+fr[i]][M+nm][0]
    # positional spikes: second difference vs median of neighbours
    flags = []
    for nm in CHECK_JOINTS:
        P = np.array([J(i, nm) for i in range(n)])
        acc = np.linalg.norm(P[2:]-2*P[1:-1]+P[:-2], axis=1)
        med = np.median(acc) + 0.3
        for i in range(1, n-1):
            if acc[i-1] > 6*med and acc[i-1] > 2.5: flags.append((fr[i], nm, 'pos accel %.1f cm/f^2 (median %.1f)' % (acc[i-1], med-0.3)))
    for nm in CHECK_JOINTS:
        Q = np.array([S_[f0+fr[i]][M+nm][1] for i in range(n)])
        ang = np.array([math.degrees(2*math.acos(min(1.0, abs(float(np.dot(Q[i], Q[i+1])))))) for i in range(n-1)])
        for i in range(1, n-2):
            nb = 0.5*(ang[i-1]+ang[i+1])
            if ang[i] > 3*nb+4.0: flags.append((fr[i+1], nm, 'rotation jump %.1f deg (neighbours %.1f)' % (ang[i], nb)))
    rows = []
    for i, f in enumerate(fr):
        s = S_[f0+f]; tip = s['Longsword_02'][0]; Ps, Qs = s['Longsword_01']
        sp = float(np.linalg.norm(S_[f0+fr[i+1]]['Longsword_02'][0]-tip))*0.6 if i+1 < n else 0.0
        ed = None
        if 0 < i < n-1:
            tv = S_[f0+fr[i+1]]['Longsword_02'][0]-S_[f0+fr[i-1]]['Longsword_02'][0]; blade = qrot(Qs, [0, 0, -1.]); e = qrot(Qs, [-1., 0, 0])
            t = tv-blade*np.dot(tv, blade); nn = np.linalg.norm(t)
            ed = math.degrees(math.acos(max(-1, min(1, float(np.dot(e, t/nn)))))) if nn > 1e-6 else None
        w = {sd: wrist_components(s, sd) for sd in ('Right', 'Left')}
        cl = {sd: clearance(dict(s), sd) for sd in ('Right', 'Left')}
        # blade against body segments (blade = guard->tip), ignoring the arms that hold it
        blade_a = Ps; blade_b = tip; dmin = 1e9; who = ''
        for a, b in BODY_SEGS:
            if 'Arm' in a: continue
            dd = seg_dist(blade_a, blade_b, s[M+a][0], s[M+b][0])
            if dd < dmin: dmin = dd; who = a+'-'+b
        hd = qrot(s[M+'Head'][1], [0, 0, 1.]); yaw = math.degrees(math.atan2(hd[0], hd[2])); pit = math.degrees(math.asin(max(-1, min(1, hd[1]))))
        rows.append(dict(f=f, sp=sp, ed=ed, w=w, cl=cl, bd=dmin, who=who, yaw=yaw, pit=pit, hips=s[M+'Hips'][0]))
        if dmin < 12: flags.append((f, 'blade', 'blade within %.1f cm of %s' % (dmin, who)))
        for sd in ('Right', 'Left'):
            c = w[sd]; tw = c[2]-TWIST_REF[sd]
            if c[1] < -LIM_HARD['uln'] or c[1] > LIM_HARD['rad'] or abs(c[0]) > LIM_HARD['flex'] or abs(tw) > LIM_HARD['twist']:
                flags.append((f, sd+' wrist', 'beyond hard limit: flex %.0f dev %.0f twist %.0f' % (c[0], c[1], tw)))
            if cl[sd]['elbow'] < 20 or cl[sd]['fore'] < 18: flags.append((f, sd+' arm', 'close to torso: elbow %.1f fore %.1f' % (cl[sd]['elbow'], cl[sd]['fore'])))
    print(label, "- every %d frames (R flex/dev/twist | L flex/dev/twist | tip m/s | edge deg | elbow clr R/L | blade-body cm | head yaw/pitch)" % step)
    for r in rows[::step]:
        a = r['w']['Right']; b = r['w']['Left']
        print(" f%3d  R %3.0f/%4.0f/%4.0f | L %3.0f/%4.0f/%4.0f | %5.1f | %s | %4.1f/%4.1f | %5.1f %-14s | %4.0f/%3.0f" % (
            r['f'], a[0], a[1], a[2]-TWIST_REF['Right'], b[0], b[1], b[2]-TWIST_REF['Left'], r['sp'], ('%4.0f' % r['ed']) if r['ed'] is not None else '  - ',
            r['cl']['Right']['elbow'], r['cl']['Left']['elbow'], r['bd'], r['who'], r['yaw'], r['pit']))
    print("FLAGS over all frames:", sorted(flags) if flags else "none")
    return rows, flags


def extra_checks(ds, f0, frames, label):
    """floor contact, knee bend direction, foot slide, head angular speed, hands against the body; prints findings (everything flagged, nothing if clean)"""
    fr = list(frames); S_ = scene_state(ds, [f0+f for f in fr]); n = len(fr); out = []
    def J(i, nm): return S_[f0+fr[i]][M+nm][0]
    allj = [k for k in S_[f0+fr[0]] if k.startswith(M)]
    miny = min((float(S_[f0+f][k][0][1]), f, k) for f in fr for k in allj)
    out.append("lowest joint %.1f cm (%s, frame %d)" % (miny[0], miny[2].replace(M, ''), miny[1]))
    tipy = min((float(S_[f0+f]['Longsword_02'][0][1]), f) for f in fr); out.append("lowest blade tip %.1f cm at frame %d (floor is 0)" % tipy)
    for sd in ('Right', 'Left'):                                    # knees: bend must be forward of the hip-ankle line, and the leg should not lock straight
        for i, f in enumerate(fr):
            h, k, a = J(i, sd+'UpLeg'), J(i, sd+'Leg'), J(i, sd+'Foot')
            u = (a-h)/np.linalg.norm(a-h); kv = (k-h) - u*np.dot(k-h, u); fwd = kv[2]
            ang = math.degrees(math.acos(max(-1, min(1, float(np.dot((h-k)/np.linalg.norm(h-k), (a-k)/np.linalg.norm(a-k)))))))
            if fwd < -0.5 or ang > 172: out.append("%s knee frame %d: bend angle %.0f deg, forward offset %.1f cm (locked or backward)" % (sd, f, ang, fwd))
    for sd in ('Right', 'Left'):                                    # planted foot sliding
        P = np.array([J(i, sd+'Foot') for i in range(n)]); y = P[:, 1]; y0 = y.min()
        for i in range(1, n):
            if y[i] < y0+1.5 and y[i-1] < y0+1.5 and np.linalg.norm(P[i, [0, 2]]-P[i-1, [0, 2]]) > 0.4: out.append("%s foot slides %.1f cm at frame %d while grounded" % (sd, np.linalg.norm(P[i, [0, 2]]-P[i-1, [0, 2]]), fr[i]))
    Q = np.array([S_[f0+fr[i]][M+'Head'][1] for i in range(n)])
    hr = [math.degrees(2*math.acos(min(1.0, abs(float(np.dot(Q[i], Q[i+1])))))) for i in range(n-1)]
    out.append("head rotation per frame: max %.2f deg at frame %d (mean %.2f)" % (max(hr), fr[int(np.argmax(hr))], float(np.mean(hr))))
    for sd in ('Right', 'Left'):                                    # hands against the legs / pelvis
        dm = min((min(seg_dist(J(i, sd+'Hand'), J(i, sd+'Hand'), J(i, a), J(i, b)) for a, b in (('Hips', 'Spine'), ('RightUpLeg', 'RightLeg'), ('LeftUpLeg', 'LeftLeg'))), fr[i]) for i in range(n))
        out.append("%s hand closest to hips/thighs: %.1f cm at frame %d" % (sd, dm[0], dm[1]))
    print(label, "extra checks:"); [print("  ", o) for o in out[:40]]
    return out
