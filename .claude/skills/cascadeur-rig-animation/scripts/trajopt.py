import random
HARD = 0.15
K_ROLL = 3*len(NAMES3); K_SWOFF = 3*NAMES3.index('sw_off'); K_SWDIR = 3*NAMES3.index('sw_dir')
LAM = np.array([0.03, 0.01, 0.01, 0.6, 0.6, 0.6, 0.12, 0.12, 0.06, 0.06, 0.06])
SCALE = np.array([6, 10, 10, 1.0, 1.0, 1.0, 2.0, 2.0, 2.0, 2.0, 2.0])
K_TORSO = 3*NAMES3.index('torso')

def apply_off(base, x):
    v = np.array(base, float)
    v[K_ROLL] += x[0]; v[K_ROLL+1] += x[1]; v[K_ROLL+2] += x[2]
    v[K_SWOFF:K_SWOFF+3] += np.array(x[3:6])
    v[K_SWDIR:K_SWDIR+3] = rot_dir(v[K_SWDIR:K_SWDIR+3], x[6], x[7])
    if len(x) > 8: v[K_TORSO:K_TORSO+3] += np.array(x[8:11])
    return v

def frame_cost(f, x, base, X, TIP, lim_swing=80., w_edge=6.0):
    d = solve(v2p(apply_off(base[f], x)))
    c = side_cost(d['RightWrist'], lim_swing=lim_swing) + side_cost(d['LeftWrist'], lim_swing=lim_swing)
    c += HARD*(max(0.0, d['RightWrist']['swing']-lim_swing)**2 + max(0.0, d['LeftWrist']['swing']-lim_swing)**2)
    c += 50*(abs(d['RightArmErr']) + abs(d['LeftArmErr']))
    a_t = TIP.get(f-1); b_t = TIP.get(f+1)
    if a_t is not None and b_t is not None:
        tv = b_t - a_t; spd = np.linalg.norm(tv)/2*0.6
        we = w_edge*min(1.0, max(0.0, (spd-3.0)/6.0))
        if we > 0: c += we*(max(0.0, edge_angle(d, tv)-10.0)/15.0)**2
    c += (0.3*(sum(t*t for t in x[8:11])**0.5/5.0)**2 if len(x) > 8 else 0.0)
    c += 0.3*(np.linalg.norm(x[3:6])/3.0)**2 + 0.3*(math.hypot(x[6], x[7])/5.0)**2 + 0.0005*(x[0]**2 + x[1]**2 + x[2]**2)
    xm = X.get(f-1); xp = X.get(f+1)
    if xm is not None and xp is not None:
        mid = 0.5*(np.array(xm) + np.array(xp)); dd = np.array(x) - mid
        dd[1] = wrap(x[1] - mid[1]); dd[2] = wrap(x[2] - mid[2])
        c += float(np.sum(LAM*dd*dd))
    return c, d

def sweep(frames, base, X, TIP, trials=30, seed=0, anchor=()):
    rnd = random.Random(seed)
    for f in frames:
        if f in anchor: continue
        cur = list(X[f]); cj, cd = frame_cost(f, cur, base, X, TIP)
        for t in range(trials):
            x = list(cur)
            for k in rnd.sample(range(len(x)), rnd.choice((1, 2, 3))):
                x[k] += rnd.gauss(0, SCALE[k]*(1.0 if t < trials//2 else 0.4))
            x[1] = wrap(x[1]); x[2] = wrap(x[2])
            j, d = frame_cost(f, x, base, X, TIP)
            if j < cj: cur, cj, cd = x, j, d
        X[f] = cur; TIP[f] = cd['tip']
    return X, TIP
