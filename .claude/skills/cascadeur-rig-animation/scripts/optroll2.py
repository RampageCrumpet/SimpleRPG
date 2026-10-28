BIAS = [np.array(v, float)*1.3 for v in [(0,0,0),(0,1,0),(0,-1,0),(1,0,0),(-1,0,0),(0,0,1),(0,0,-1),(0.7,0.7,0),(-0.7,0.7,0),(0.7,-0.7,0),(-0.7,-0.7,0)]]

def side_cost(w, lim_swing=70., lim_htw=10., lim_ftw=85.):
    c = 0.02*max(0, w['swing']-lim_swing)**2 + 0.08*max(0, abs(w['hand_twist'])-lim_htw)**2 + 0.02*max(0, abs(w['fore_twist'])-lim_ftw)**2
    c += 0.0006*w['swing']**2 + 0.0004*w['fore_twist']**2
    c += 0.02*max(0, w['elbow_bend']-140)**2 + 0.02*max(0, 15-w['elbow_bend'])**2
    return c

SWIV = list(range(-180, 180, 15))
def eval_key_roll(p, r, w_edge):
    cr = []; cl = []
    for sv in SWIV:
        q = with_roll(p, r); q['right_swivel'] = sv; q['left_swivel'] = sv
        d = solve(q)
        cr.append(side_cost(d['RightWrist']) + 50*abs(d['RightArmErr']))
        cl.append(side_cost(d['LeftWrist']) + 50*abs(d['LeftArmErr']))
    ir = int(np.argmin(cr)); il = int(np.argmin(cl))
    return cr[ir] + cl[il] + edge_cost(p, r, w_edge), SWIV[ir], SWIV[il]

def dp2(keys, order, w_edge_for, fixed, step=8, trans_w=0.012, span=180):
    cands = list(range(-span, span+1, step))
    table = {}
    for f in order:
        cs = [fixed[f]] if f in fixed else cands
        table[f] = {r: eval_key_roll(keys[f], r, w_edge_for.get(f, 0.0)) for r in cs}
    best = {}
    for i, f in enumerate(order):
        cur = {}
        for r, (c, ir, il) in table[f].items():
            if i == 0: cur[r] = (c, None)
            else:
                pf = order[i-1]
                bc, br = min(((pc + trans_w*(r-pr)**2, pr) for pr, (pc, _) in best[pf].items()), key=lambda x: x[0])
                cur[r] = (c + bc, br)
        best[f] = cur
    last = order[-1]
    r = min(best[last], key=lambda k: best[last][k][0]); out = {last: r}
    for i in range(len(order)-1, 0, -1):
        f = order[i]; r = best[f][r][1]; out[order[i-1]] = r
    return out, table
