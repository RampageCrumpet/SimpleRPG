SW4 = list(range(-180, 180, 10))

def table_swivel(p, r):
    """per-arm cost / metrics for each swivel at fixed roll"""
    out = {'Right': {}, 'Left': {}}
    for sv in SW4:
        q = with_roll(p, r); q['right_swivel'] = sv; q['left_swivel'] = sv
        d = solve(q)
        for sd in ('Right', 'Left'):
            w = d[sd+'Wrist']
            out[sd][sv] = (side_cost(w) + 50*abs(d[sd+'ArmErr']), w['fore_twist'], w['swing'])
    return out

def wrap(a):
    return (a + 180) % 360 - 180

def dp_swivel(tabs, order, fixed_first=0, lam_s=0.004, lam_t=0.0035):
    """tabs: frame -> {'Right':{sv:(cost,ftw,swing)},..}; returns frame->{Right:sv, Left:sv}"""
    res = {f: {} for f in order}
    for sd in ('Right', 'Left'):
        best = {}
        for i, f in enumerate(order):
            cur = {}
            cands = [fixed_first] if i == 0 else SW4
            for sv in cands:
                c, ftw, sw = tabs[f][sd][sv]
                if i == 0: cur[sv] = (c, None)
                else:
                    pf = order[i-1]
                    bc, bsv = min(((pc + lam_s*wrap(sv-ps)**2 + lam_t*(ftw-tabs[pf][sd][ps][1])**2, ps) for ps, (pc, _) in best[pf].items()), key=lambda x: x[0])
                    cur[sv] = (c + bc, bsv)
            best[f] = cur
        last = order[-1]
        s = min(best[last], key=lambda k: best[last][k][0])
        res[last][sd] = s
        for i in range(len(order)-1, 0, -1):
            s = best[order[i]][s][1]; res[order[i-1]][sd] = s
    return res
