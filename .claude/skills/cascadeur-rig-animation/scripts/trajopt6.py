# Per-frame trajectory optimiser v6: anatomical comfort (flexion/extension, radial/ulnar, twist), torso clearance,
# edge leading, continuity.  Needs posesolver, comfort, interp (NAMES3), opt5 (rot_dir), edgedp (edge_angle), trajopt (apply_off, SCALE, LAM).
import random
COMF_W = 10.0       # scale of the comfort/clearance terms against the edge term

def solve6(base_vec, x, grip):
    p = v2p(apply_off(base_vec, x)); p['twist_share'] = 0.0; p.update(grip)
    return solve(p)

def frame_cost6(f, x, base, X, TIP, grip, w_edge=6.0, edge_fade_end=77, pos_w=0.3, dir_w=0.3):
    d = solve6(base[f], x, grip)
    c = COMF_W*pose_comfort(d) + 50*(abs(d['RightArmErr']) + abs(d['LeftArmErr']))
    a_t = TIP.get(f-1); b_t = TIP.get(f+1)
    fade = min(1.0, max(0.0, (edge_fade_end - f)/6.0))
    if a_t is not None and b_t is not None and fade > 0:
        tv = b_t - a_t; spd = np.linalg.norm(tv)/2*0.6
        we = w_edge*fade*min(1.0, max(0.0, (spd-3.0)/6.0))
        if we > 0: c += we*(max(0.0, edge_angle(d, tv)-10.0)/15.0)**2
    c += pos_w*(np.linalg.norm(x[3:6])/3.0)**2 + dir_w*(math.hypot(x[6], x[7])/5.0)**2 + 0.0005*(x[0]**2 + x[1]**2 + x[2]**2)
    c += (0.3*(sum(t*t for t in x[8:11])**0.5/5.0)**2 if len(x) > 8 else 0.0)
    xm = X.get(f-1); xp = X.get(f+1)
    if xm is not None and xp is not None:
        mid = 0.5*(np.array(xm) + np.array(xp)); dd = np.array(x) - mid
        dd[1] = wrap(x[1] - mid[1]); dd[2] = wrap(x[2] - mid[2])
        c += float(np.sum(LAM*dd*dd))
    return c, d

def sweep6(frames, base, X, TIP, grip, trials=22, seed=0, anchor=(), tie=None, **kw):
    """tie: dict frame -> frame that must share the same offsets (e.g. {130: 0})"""
    rnd = random.Random(seed)
    for f in frames:
        if f in anchor: continue
        cur = list(X[f]); cj, cd = frame_cost6(f, cur, base, X, TIP, grip, **kw)
        for t in range(trials):
            x = list(cur)
            for k in rnd.sample(range(len(x)), rnd.choice((1, 2, 3))):
                x[k] += rnd.gauss(0, SCALE[k]*(1.0 if t < trials//2 else 0.4))
            x[1] = wrap(x[1]); x[2] = wrap(x[2])
            j, d = frame_cost6(f, x, base, X, TIP, grip, **kw)
            if j < cj: cur, cj, cd = x, j, d
        X[f] = cur; TIP[f] = cd['tip']
        if tie and f in tie: X[tie[f]] = list(cur); TIP[tie[f]] = cd['tip']
    return X, TIP
