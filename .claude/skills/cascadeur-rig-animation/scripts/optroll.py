def wrist_cost(d, lim_swing=68., lim_htw=55., lim_ftw=80.):
    c = 0.0
    for sd in ('Right', 'Left'):
        w = d[sd+'Wrist']
        c += 0.02*max(0, w['swing']-lim_swing)**2 + 0.02*max(0, abs(w['hand_twist'])-lim_htw)**2 + 0.02*max(0, abs(w['fore_twist'])-lim_ftw)**2
        c += 0.0006*w['swing']**2                      # mild preference for neutral wrists
        c += 0.02*max(0, w['elbow_bend']-150)**2 + 0.02*max(0, 12-w['elbow_bend'])**2
    c += 50*(abs(d['RightArmErr']) + abs(d['LeftArmErr']))
    return c

def edge_cost(p, roll_deg, w_edge):
    if 'sw_edge' not in p or w_edge == 0: return 0.0
    dirv = np.asarray(p['sw_dir'], float); dn = dirv/np.linalg.norm(dirv)
    R1 = qm(swing(D0, dirv), RSR)
    e = qrot(qm(qaa(dn, roll_deg*D2R), R1), [-1., 0, 0])
    t = np.asarray(p['sw_edge'], float); t = t/np.linalg.norm(t)
    a = math.degrees(math.acos(max(-1, min(1, float(np.dot(e, t))))))
    return w_edge*(a/30.)**2

def with_roll(p, r):
    q = dict(p); q.pop('sw_edge_used', None)
    q['sw_roll'] = r
    q.pop('sw_edge', None)
    return q

def dp_rolls(keys, order, w_edge_for, fixed=None, step=6, trans_w=0.012, span=180):
    """keys: dict frame->params (with sw_edge as a SOFT preference). returns dict frame->roll_deg"""
    fixed = fixed or {}
    cands = list(range(-span, span+1, step))
    table = {}
    for f in order:
        cs = [fixed[f]] if f in fixed else cands
        row = {}
        for r in cs:
            d = solve(with_roll(keys[f], r))
            row[r] = (wrist_cost(d) + edge_cost(keys[f], r, w_edge_for.get(f, 0.0)), d['RightWrist']['swing'], d['LeftWrist']['swing'])
        table[f] = row
    best = {}; back = {}
    prev = None
    for i, f in enumerate(order):
        cur = {}
        for r, (c, _, _) in table[f].items():
            if prev is None: cur[r] = (c, None)
            else:
                pf = order[i-1]
                bc, br = min(((pc + trans_w*(r-pr)**2*(1.0), pr) for pr, (pc, _) in best[pf].items()), key=lambda x: x[0])
                cur[r] = (c + bc, br)
        best[f] = cur; prev = f
    last = order[-1]
    r = min(best[last], key=lambda k: best[last][k][0]); out = {last: r}
    for i in range(len(order)-1, 0, -1):
        f = order[i]; r = best[f][r][1]; out[order[i-1]] = r
    return out, table
