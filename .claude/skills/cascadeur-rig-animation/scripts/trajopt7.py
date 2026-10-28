# v7 per-frame optimiser: adds torso and pelvis twist as free variables WITHOUT changing the sword's world pose
# (the sword is specified in the torso frame, so extra torso rotation is compensated in sw_off / sw_dir / roll), a lead rule that
# also covers the overswing and recovery (never flat-first), and despiking.  Needs session6 loaded (posesolver, comfort, trajopt6, ...).
import random
K_PELVIS = 3*NAMES3.index('pelvis'); K_TORSO7 = 3*NAMES3.index('torso')
SCALE7 = np.array([6, 10, 10, 1.5, 1.5, 1.5, 3.0, 3.0, 4.0, 4.0, 4.0, 3.0, 3.0, 3.0])
LAM7 = np.array([0.03, 0.01, 0.01, 0.6, 0.6, 0.6, 0.12, 0.12, 0.04, 0.04, 0.04, 0.04, 0.04, 0.04])
NX = 14
JERK_W = 0.6                       # penalty on elbow / wrist position second differences (cm^2), prevents snaps
ELB = {}; HND = {}                 # frame -> (right, left) elbow / wrist positions, kept current by sweep7 (fill with init_motion_cache)

def init_motion_cache(base, X, grip, frames):
    TIP = {}
    for f in frames:
        d = solve7(base[f], X[f], grip); TIP[f] = d['tip']
        ELB[f] = (d['RightWrist']['elbow_pos'], d['LeftWrist']['elbow_pos']); HND[f] = (d['G'][M+'RightHand'][0], d['G'][M+'LeftHand'][0])
    return TIP

def apply_off7(base, x):
    v = np.array(base, float)
    v[K_ROLL] += x[0]; v[K_ROLL+1] += x[1]; v[K_ROLL+2] += x[2]
    v[K_SWOFF:K_SWOFF+3] += np.array(x[3:6])
    v[K_SWDIR:K_SWDIR+3] = rot_dir(v[K_SWDIR:K_SWDIR+3], x[6], x[7])
    if len(x) > 8 and (x[8] or x[9] or x[10]):
        t_old = eul(*v[K_TORSO7:K_TORSO7+3]); roll0 = v[K_ROLL]
        v[K_TORSO7:K_TORSO7+3] += np.array(x[8:11]); t_new = eul(*v[K_TORSO7:K_TORSO7+3])
        c = qm(qinv(t_new), t_old)                                  # keeps the sword's WORLD pose fixed
        rest = PSR - GR[M+'Spine2'][0]
        v[K_SWOFF:K_SWOFF+3] = qrot(c, rest + v[K_SWOFF:K_SWOFF+3]) - rest
        dn_old = v[K_SWDIR:K_SWDIR+3]/np.linalg.norm(v[K_SWDIR:K_SWDIR+3])
        Rrel_old = qm(qaa(dn_old, roll0*D2R), qm(swing(D0, dn_old), RSR))
        dn_new = qrot(c, dn_old); dn_new /= np.linalg.norm(dn_new)
        q = qm(qm(c, Rrel_old), qinv(qm(swing(D0, dn_new), RSR)))
        if q[0] < 0: q = -q
        roll_new = math.degrees(2*math.atan2(float(np.dot(q[1:], dn_new)), q[0]))
        v[K_SWDIR:K_SWDIR+3] = dn_new; v[K_ROLL] = roll0 + wrap(roll_new - roll0)
    if len(x) > 11: v[K_PELVIS:K_PELVIS+3] += np.array(x[11:14])
    return v

def solve7(base_vec, x, grip):
    p = v2p(apply_off7(base_vec, x)); p['twist_share'] = 0.0; p.update(grip); return solve(p)

def lead_cost(d, tv, f, w_true=5.0, true_until=84, w_either=3.0, either_until=126):
    """true edge must lead during the cut and overswing; afterwards either edge may lead, but never the flat."""
    if tv is None: return 0.0
    spd = np.linalg.norm(tv)/2*0.6
    gate = min(1.0, max(0.0, (spd - 3.0)/6.0))
    if gate <= 0: return 0.0
    a = edge_angle(d, tv)
    if f <= true_until:
        fade = 1.0 if f <= true_until - 4 else (true_until - f)/4.0 + 0.5
        return w_true*gate*fade*(max(0.0, a - 10.0)/15.0)**2
    a2 = min(a, 180.0 - a)
    return w_either*gate*(max(0.0, a2 - 25.0)/15.0)**2

def frame_cost7(f, x, base, X, TIP, grip, pos_w=0.04, dir_w=0.04, torso_w=0.02, **kw):
    d = solve7(base[f], x, grip)
    c = COMF_W*pose_comfort(d) + 50*(abs(d['RightArmErr']) + abs(d['LeftArmErr']))
    a_t = TIP.get(f-1); b_t = TIP.get(f+1)
    c += lead_cost(d, (b_t - a_t) if (a_t is not None and b_t is not None) else None, f, **kw)
    c += pos_w*(np.linalg.norm(x[3:6])/3.0)**2 + dir_w*(math.hypot(x[6], x[7])/5.0)**2 + 0.0005*(x[0]**2 + x[1]**2 + x[2]**2)
    c += torso_w*((sum(t*t for t in x[8:11])**0.5/8.0)**2 + (sum(t*t for t in x[11:14])**0.5/8.0)**2)
    xm = X.get(f-1); xp = X.get(f+1)
    if xm is not None and xp is not None:
        mid = 0.5*(np.array(xm) + np.array(xp)); dd = np.array(x) - mid
        dd[1] = wrap(x[1] - mid[1]); dd[2] = wrap(x[2] - mid[2])
        c += float(np.sum(LAM7*dd*dd))
    em, ep = ELB.get(f-1), ELB.get(f+1); hm, hp = HND.get(f-1), HND.get(f+1)
    if em and ep:
        for i, e in enumerate((d['RightWrist']['elbow_pos'], d['LeftWrist']['elbow_pos'])):
            c += JERK_W*float(np.sum((e - 0.5*(em[i] + ep[i]))**2))
    if hm and hp:
        for i, nm_ in enumerate(('RightHand', 'LeftHand')):
            c += JERK_W*float(np.sum((d['G'][M+nm_][0] - 0.5*(hm[i] + hp[i]))**2))
    return c, d

def sweep7(frames, base, X, TIP, grip, trials=22, seed=0, anchor=(), tie=None, scale=None, **kw):
    rnd = random.Random(seed); sc = SCALE7 if scale is None else scale
    for f in frames:
        if f in anchor: continue
        cur = list(X[f]); cj, cd = frame_cost7(f, cur, base, X, TIP, grip, **kw)
        for t in range(trials):
            x = list(cur)
            for k in rnd.sample(range(len(x)), rnd.choice((1, 2, 3))):
                x[k] += rnd.gauss(0, sc[k]*(1.0 if t < trials//2 else 0.4))
            x[1] = wrap(x[1]); x[2] = wrap(x[2])
            j, d = frame_cost7(f, x, base, X, TIP, grip, **kw)
            if j < cj: cur, cj, cd = x, j, d
        X[f] = cur; TIP[f] = cd['tip']
        ELB[f] = (cd['RightWrist']['elbow_pos'], cd['LeftWrist']['elbow_pos']); HND[f] = (cd['G'][M+'RightHand'][0], cd['G'][M+'LeftHand'][0])
        if tie and f in tie: X[tie[f]] = list(cur); TIP[tie[f]] = cd['tip']; ELB[tie[f]] = ELB[f]; HND[tie[f]] = HND[f]
    return X, TIP

def despike_smooth(Xd, lo, hi, cols=None, spike_thresh=None, passes=3):
    """replace single-frame outliers by the neighbour median, then smooth with [1 2 1]/4"""
    cols = list(range(NX)) if cols is None else cols
    X2 = {f: list(v) for f, v in Xd.items()}
    for c in cols:
        for f in range(lo+2, hi-1):
            nb = [X2[f-2][c], X2[f-1][c], X2[f+1][c], X2[f+2][c]]
            med = float(np.median(nb)); spread = max(nb) - min(nb)
            th = (0.35*SCALE7[c] if spike_thresh is None else spike_thresh)
            if abs(X2[f][c] - med) > max(th, 1.5*spread): X2[f][c] = med
    for _ in range(passes):
        Y = {f: list(v) for f, v in X2.items()}
        for f in range(lo+1, hi):
            for c in cols: Y[f][c] = 0.25*X2[f-1][c] + 0.5*X2[f][c] + 0.25*X2[f+1][c]
        X2 = Y
    return X2
