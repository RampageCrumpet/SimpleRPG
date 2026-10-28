import random

def rot_dir(dirv, dyaw, dpitch):
    d = np.asarray(dirv, float); d = d/np.linalg.norm(d)
    return qrot(qm(qaa(Y, dyaw*D2R), qaa(X, dpitch*D2R)), d)

def apply_x(p, x):
    """x = [roll, svR, svL, dx, dy, dz, dyaw, dpitch]"""
    q = with_roll(p, x[0])
    q['right_swivel'] = x[1]; q['left_swivel'] = x[2]
    q['sw_off'] = np.asarray(p['sw_off'], float) + np.array(x[3:6])
    q['sw_dir'] = rot_dir(p.get('sw_dir', D0), x[6], x[7])
    return q

def J_key(p, x, prev, tgt=None, w_edge=0.5, lim_swing=80.):
    q = apply_x(p, x); d = solve(q)
    c = 0.0
    for sd in ('Right', 'Left'):
        w = d[sd+'Wrist']
        c += side_cost(w, lim_swing=lim_swing) + 50*abs(d[sd+'ArmErr'])
    c += edge_cost_world(d, tgt, w_edge)
    c += 0.5*(np.linalg.norm(x[3:6])/4.0)**2 + 0.5*(math.hypot(x[6], x[7])/8.0)**2
    if prev is not None:
        c += 0.003*(x[0]-prev['x'][0])**2 + 0.004*wrap(x[1]-prev['x'][1])**2 + 0.004*wrap(x[2]-prev['x'][2])**2
        c += 0.003*(d['RightWrist']['fore_twist']-prev['rft'])**2 + 0.003*(d['LeftWrist']['fore_twist']-prev['lft'])**2
    return c, d

def refine_key(p, x0, prev, tgt=None, iters=160, seed=1):
    rnd = random.Random(seed)
    best = list(x0); bj, bd = J_key(p, best, prev, tgt)
    scale = [8, 15, 15, 1.5, 1.5, 1.5, 3, 3]
    for it in range(iters):
        x = list(best)
        for k in rnd.sample(range(8), rnd.choice((1, 2, 3))):
            x[k] += rnd.gauss(0, scale[k])
        x[1] = wrap(x[1]); x[2] = wrap(x[2])
        j, d = J_key(p, x, prev, tgt)
        if j < bj: best, bj, bd = x, j, d
        if it == iters//2: scale = [s*0.5 for s in scale]
    return best, bj, bd
