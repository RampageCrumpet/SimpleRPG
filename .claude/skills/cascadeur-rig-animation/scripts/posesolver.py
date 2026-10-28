exec(open(SP + "rigmath.py").read()) if 'fk' not in globals() else None

M = 'mixamorig:'
GR = fk(LQ, S[M+'Hips']['gp'])
I4 = np.array([1., 0, 0, 0])
Y = np.array([0, 1., 0]); X = np.array([1., 0, 0]); Z = np.array([0, 0, 1.])
D2R = math.pi/180

def eul(yaw=0, pitch=0, roll=0):
    return qm(qaa(Y, yaw*D2R), qm(qaa(X, pitch*D2R), qaa(Z, roll*D2R)))

def swing(a, b):
    a = np.asarray(a, float); a /= np.linalg.norm(a)
    b = np.asarray(b, float); b /= np.linalg.norm(b)
    c = np.cross(a, b); s = np.linalg.norm(c); d = float(np.dot(a, b))
    if s < 1e-8:
        return I4.copy() if d > 0 else qaa(X, math.pi)
    return qaa(c/s, math.atan2(s, d))

PSR = np.array(S['Longsword_01']['gp']); RSR = np.array(S['Longsword_01']['lq'])
D0 = qrot(RSR, [0, 0, -1.])
BONE_AXIS = {}
for n in S:
    ch = [c for c in S if PARENT[c] == n and c not in ('Longsword_02',)]
    if ch:
        v = LP[ch[0]]
        BONE_AXIS[n] = v/np.linalg.norm(v)

HINGE = {}
def _hinge(a, b, c):
    u = GR[b][0]-GR[a][0]; f = GR[c][0]-GR[b][0]
    n = np.cross(u, f); n /= np.linalg.norm(n)
    HINGE[a] = qrot(qinv(GR[a][1]), n); HINGE[b] = qrot(qinv(GR[b][1]), n)
for sd in ('Right', 'Left'):
    _hinge(M+sd+'Arm', M+sd+'ForeArm', M+sd+'Hand')
    _hinge(M+sd+'UpLeg', M+sd+'Leg', M+sd+'Foot')

def _restoff(a, b, c):
    p0 = GR[a][0]; pe = GR[b][0]; pt = GR[c][0]
    d = (pt-p0)/np.linalg.norm(pt-p0)
    o = (pe-p0) - d*np.dot(pe-p0, d)
    return o/np.linalg.norm(o)
REST_OFF = {}
for sd in ('Right', 'Left'):
    REST_OFF[sd+'Arm'] = _restoff(M+sd+'Arm', M+sd+'ForeArm', M+sd+'Hand')
    REST_OFF[sd+'Leg'] = _restoff(M+sd+'UpLeg', M+sd+'Leg', M+sd+'Foot')

# --- anatomical wrist axes (hand-local), from the finger layout ---------------------------------
def _anat(side):
    idx, pnk, th = LP[M+side+'HandIndex1'], LP[M+side+'HandPinky1'], LP[M+side+'HandThumb1']
    w = np.array(idx - pnk, float); w[1] = 0; w /= np.linalg.norm(w)          # points to the radial (thumb) side
    pm = np.cross(Y, w); pm /= np.linalg.norm(pm)
    pal = pm if np.dot(th, pm) >= 0 else -pm                                   # palmar side (assumed from the thumb base)
    fa = np.cross(Y, pal); fa /= np.linalg.norm(fa)                            # +rotation about fa = flexion (fingers toward the palm)
    ra = np.cross(Y, w); ra /= np.linalg.norm(ra)                              # +rotation about ra = radial deviation
    return fa, ra
ANAT = {s_: _anat(s_) for s_ in ('Right', 'Left')}

def rotvec(q):
    q = np.asarray(q, float); q = q/np.linalg.norm(q)
    if q[0] < 0: q = -q
    a = 2*math.acos(max(-1, min(1, q[0]))); sn = math.sqrt(max(1e-12, 1-q[0]**2))
    return math.degrees(a)*q[1:]/sn

def hand_pose(sd, Psw, Rsw, P):
    """hand joint position/orientation for a given sword pose. P['grip_right'|'grip_left'] = (x,y,z) degrees: extra hand rotation
    relative to the sword, in hand-local axes, about the fist centre (Middle1). Zero reproduces the original Ready grip exactly."""
    c = M+sd+'Hand'
    g = P.get('grip_'+sd.lower(), (0, 0, 0))
    c_loc = LP[M+sd+'HandMiddle1']
    q_rest, p_rest = GR[c][1], GR[c][0]
    c_off = qrot(qinv(RSR), p_rest + qrot(q_rest, c_loc) - PSR)
    h_sw = qm(qinv(RSR), q_rest)
    roll = P.get('roll_'+sd.lower(), 0.0)                  # degrees: roll the hand around the hilt (sword local Z axis, which passes through the origin)
    if roll:
        qr = qaa(Z, roll*D2R); c_off = qrot(qr, c_off); h_sw = qm(qr, h_sw)
    dq = qm(qaa(X, g[0]*D2R), qm(qaa(Y, g[1]*D2R), qaa(Z, g[2]*D2R)))
    hq = qm(Rsw, qm(h_sw, dq))
    return Psw + qrot(Rsw, c_off) - qrot(hq, c_loc), hq

def _seg_dist(p, a, b):
    ab = b - a; t = max(0.0, min(1.0, float(np.dot(p-a, ab)/np.dot(ab, ab)))); return float(np.linalg.norm(p-(a+t*ab)))

def clearance(G, side):
    """distance (cm) of elbow / mid-forearm / wrist / mid-upper-arm from the spine axis polyline"""
    ax = [G[M+n][0] for n in ('Hips', 'Spine', 'Spine1', 'Spine2', 'Neck')]
    dist = lambda p: min(_seg_dist(p, ax[i], ax[i+1]) for i in range(len(ax)-1))
    sh, el, wr = G[M+side+'Arm'][0], G[M+side+'ForeArm'][0], G[M+side+'Hand'][0]
    return dict(elbow=dist(el), fore=dist(0.5*(el+wr)), wrist=dist(wr), upper=dist(0.5*(sh+el)))

def two_bone(a, b, c, target, pole, swivel=0.0):
    """a,b,c joint names; returns (upper_q, fore_q, reach_error)"""
    p0 = G[a][0]
    l1 = np.linalg.norm(LP[b]); l2 = np.linalg.norm(LP[c])
    t = np.asarray(target, float)
    dv = t-p0; d = np.linalg.norm(dv); dirn = dv/d
    dc = min(max(d, abs(l1-l2)+1e-3), l1+l2-1e-3)
    err = d-dc
    aa = (l1*l1-l2*l2+dc*dc)/(2*dc); h = math.sqrt(max(l1*l1-aa*aa, 0))
    pl = np.asarray(pole, float); pl = pl - dirn*np.dot(pl, dirn)
    pl /= np.linalg.norm(pl)
    if swivel:
        sv = swivel*D2R; pl = pl*math.cos(sv) + np.cross(dirn, pl)*math.sin(sv)
    elbow = p0 + dirn*aa + pl*h
    wrist = p0 + dirn*dc
    u = (elbow-p0)/l1; f = (wrist-elbow)/l2
    nrm = np.cross(u, f); nrm /= np.linalg.norm(nrm)
    qu = frame_q([BONE_AXIS[a], HINGE[a]], [u, nrm])
    qf = frame_q([BONE_AXIS[b], HINGE[b]], [f, nrm])
    return qu, qf, err, elbow

def solve(P):
    global G
    lq = {n: LQ[n].copy() for n in S}
    hips = np.array(S[M+'Hips']['gp']) + np.array(P.get('hips', [0, 0, 0]))
    pel = eul(*P.get('pelvis', (0, 0, 0)))
    tor = eul(*P.get('torso', (0, 0, 0)))
    head = eul(*P.get('head', (0, 0, 0)))
    gq = {}
    gq[M+'Hips'] = qm(pel, GR[M+'Hips'][1])
    lq[M+'Hips'] = gq[M+'Hips']
    for nm, w in (('Spine', 1/3), ('Spine1', 2/3), ('Spine2', 1.0)):
        d = qslerp(pel, tor, w)
        gq[M+nm] = qm(d, GR[M+nm][1])
    par = {M+'Spine': M+'Hips', M+'Spine1': M+'Spine', M+'Spine2': M+'Spine1'}
    for nm in (M+'Spine', M+'Spine1', M+'Spine2'):
        lq[nm] = qm(qinv(gq[par[nm]]), gq[nm])
    gq[M+'Neck'] = qm(qslerp(tor, head, 0.5), GR[M+'Neck'][1])
    lq[M+'Neck'] = qm(qinv(gq[M+'Spine2']), gq[M+'Neck'])
    gq[M+'Head'] = qm(head, GR[M+'Head'][1])
    lq[M+'Head'] = qm(qinv(gq[M+'Neck']), gq[M+'Head'])
    for sd in ('Right', 'Left'):
        sh = M+sd+'Shoulder'
        shrug = eul(*P.get(sd.lower()+'_shrug', (0, 0, 0)))
        gq[sh] = qm(qm(shrug, tor), GR[sh][1])
        lq[sh] = qm(qinv(gq[M+'Spine2']), gq[sh])
    G = fk(lq, hips)
    # sword frame, specified in torso frame
    s2_rest = GR[M+'Spine2'][0]; s2_new = G[M+'Spine2'][0]
    dirv = np.asarray(P.get('sw_dir', D0), float)
    roll = P.get('sw_roll', 0)*D2R
    R1 = qm(swing(D0, dirv), RSR)
    if 'sw_edge' in P:
        dn = dirv/np.linalg.norm(dirv)
        e1 = qrot(R1, [-1., 0, 0]); e1 = e1 - dn*np.dot(e1, dn); e1 /= np.linalg.norm(e1)
        et = np.asarray(P['sw_edge'], float); et = et - dn*np.dot(et, dn); et /= np.linalg.norm(et)
        roll = math.atan2(np.dot(np.cross(e1, et), dn), np.dot(e1, et)) + P.get('sw_roll', 0)*D2R
    Rsw = qm(tor, qm(qaa(dirv, roll), R1))
    Psw = s2_new + qrot(tor, PSR - s2_rest + np.asarray(P.get('sw_off', [0, 0, 0]), float))
    # keep both wrists comfortably inside arm reach (no locked-out elbows): pull the sword back if needed
    reach = P.get('reach_frac', 0.955)*(np.linalg.norm(LP[M+'RightForeArm']) + np.linalg.norm(LP[M+'RightHand']))
    for _it in range(10):
        moved = False
        for sd in ('Right', 'Left'):
            tgt = hand_pose(sd, Psw, Rsw, P)[0]
            sh = G[M+sd+'Arm'][0]; dv_ = tgt - sh; dist = np.linalg.norm(dv_)
            if dist > reach:
                Psw = Psw - dv_/dist*(dist-reach)*1.02; moved = True
        if not moved: break
    diag = {'sword': (Psw, Rsw)}
    # arms
    for sd in ('Right', 'Left'):
        a, b, c = M+sd+'Arm', M+sd+'ForeArm', M+sd+'Hand'
        target, hand_q = hand_pose(sd, Psw, Rsw, P)
        trest = qm(gq[M+'Spine2'], qinv(GR[M+'Spine2'][1]))
        pole = qrot(trest, REST_OFF[sd+'Arm']) + np.asarray(P.get(sd.lower()+'_elbow', [0, 0, 0]), float)
        qu, qf, err, elbow = two_bone(a, b, c, target, pole, P.get(sd.lower()+'_swivel', 0.0)*(1 if sd=='Right' else -1))
        # share forearm pronation/supination between ForeArm and Hand
        rel = qm(qinv(qf), hand_q)
        if rel[0] < 0: rel = -rel
        tw = 2*math.atan2(rel[2], rel[0])          # twist about local Y (bone axis)
        tau = max(-math.radians(90), min(math.radians(90), P.get('twist_share', 0.0)*tw))
        qf = qm(qf, qaa(Y, tau))
        lq[a] = qm(qinv(gq[M+sd+'Shoulder']), qu)
        lq[b] = qm(qinv(qu), qf)
        hrel = qm(qinv(qf), hand_q)
        if hrel[0] < 0: hrel = -hrel
        hq_tw = 2*math.atan2(hrel[2], hrel[0])
        sw_ang = 2*math.acos(max(-1, min(1, hrel[0])))     # total hand deviation from forearm axis frame
        swing_q = qm(hrel, qinv(np.array([math.cos(hq_tw/2), 0, math.sin(hq_tw/2), 0])))
        if swing_q[0] < 0: swing_q = -swing_q
        swing_a = 2*math.acos(max(-1, min(1, swing_q[0])))
        lq[c] = hrel
        u_dir = (elbow - G[a][0]); f_dir = (target - elbow)
        el = math.degrees(math.acos(max(-1, min(1, float(np.dot(u_dir, f_dir)/np.linalg.norm(u_dir)/np.linalg.norm(f_dir))))))
        rv = rotvec(swing_q); fa_, ra_ = ANAT[sd]
        diag[sd+'Wrist'] = dict(swing=math.degrees(swing_a), hand_twist=math.degrees(hq_tw), fore_twist=math.degrees(tau), elbow_bend=el,
                                flex=float(np.dot(rv, fa_)), dev=float(np.dot(rv, ra_)), elbow_pos=elbow)
        diag[sd+'ArmErr'] = err
        G = fk(lq, hips)
    # legs
    for sd in ('Right', 'Left'):
        a, b, c = M+sd+'UpLeg', M+sd+'Leg', M+sd+'Foot'
        ft = np.asarray(P[sd.lower()+'_foot'], float)
        fd = eul(*P.get(sd.lower()+'_foot_rot', (0, 0, 0)))
        foot_q = qm(fd, GR[c][1])
        trest = qm(gq[M+'Hips'], qinv(GR[M+'Hips'][1]))
        pole = qrot(trest, REST_OFF[sd+'Leg']) + np.asarray(P.get(sd.lower()+'_knee', [0, 0, 0]), float)
        qu, qf, err, knee = two_bone(a, b, c, ft, pole)
        lq[a] = qm(qinv(gq[M+'Hips']), qu)
        lq[b] = qm(qinv(qu), qf)
        lq[c] = qm(qinv(qf), foot_q)
        diag[sd+'LegErr'] = err
        G = fk(lq, hips)
    diag['lq'] = lq; diag['hips'] = hips; diag['G'] = G
    diag['clear'] = {sd_: clearance(G, sd_) for sd_ in ('Right', 'Left')}
    diag['tip'] = Psw + qrot(Rsw, qrot(qinv(RSR), np.array([0, 0, 0.])) + np.array([0, 0, -109.44]))
    return diag

def pose_report(name, d):
    G = d['G']
    f = lambda n: np.round(G[M+n][0], 1).tolist()
    print(name, "armErr R/L %.1f/%.1f legErr R/L %.1f/%.1f" % (d['RightArmErr'], d['LeftArmErr'], d['RightLegErr'], d['LeftLegErr']),
          "| tip", np.round(d['tip'], 0).tolist(), "grip", np.round(d['sword'][0], 0).tolist(),
          "| head", f('Head'), "hips", f('Hips'))


def foot_lift(base, heel_deg, toe_pivot_rest=None, side='Right'):
    """returns (ankle_pos, foot_rot tuple) for heel lift by pivoting about toe base"""
    base = np.asarray(base, float)
    tb = base + (GR[M+side+'ToeBase'][0] - GR[M+side+'Foot'][0])
    q = qaa(X, heel_deg*D2R)
    ank = tb + qrot(q, base - tb)
    return ank
