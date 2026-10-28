def travel_dirs(vecs_by_frame):
    """tip travel (central difference) per frame, from solved sols {frame: diag}"""
    fr = sorted(vecs_by_frame); out = {}
    for i, f in enumerate(fr):
        a = vecs_by_frame[fr[max(i-1, 0)]]['tip']; b = vecs_by_frame[fr[min(i+1, len(fr)-1)]]['tip']
        out[f] = b - a
    return out

def edge_angle(d, travel):
    P0, R0 = d['sword']
    blade = qrot(R0, [0, 0, -1.]); e = qrot(R0, [-1., 0, 0])
    t = travel - blade*np.dot(travel, blade); n = np.linalg.norm(t)
    if n < 1e-6: return 0.0
    t /= n
    return math.degrees(math.acos(max(-1, min(1, float(np.dot(e, t))))))

def edge_dp(base_vecs, f0, f1, travel, deltas=range(-90, 91, 6), w_e=5.0, trans_w=0.03, lim_swing=80.):
    """choose a per-frame roll offset (deg) in [f0,f1]; 0 at both ends. base_vecs: frame->param vector"""
    k = 3*len(NAMES3)
    frames = list(range(f0, f1+1))
    table = {}
    for f in frames:
        row = {}
        cs = [0] if f in (f0, f1) else list(deltas)
        for dl in cs:
            v = np.array(base_vecs[f]); v[k] += dl
            d = solve(v2p(v))
            c = side_cost(d['RightWrist'], lim_swing=lim_swing) + side_cost(d['LeftWrist'], lim_swing=lim_swing) + 50*(abs(d['RightArmErr'])+abs(d['LeftArmErr']))
            a = edge_angle(d, travel[f])
            c += w_e*(max(0.0, a-8.0)/15.0)**2
            row[dl] = (c, a, d['RightWrist']['swing'], d['LeftWrist']['swing'])
        table[f] = row
    best = {}
    for i, f in enumerate(frames):
        cur = {}
        for dl, (c, a, rs, ls) in table[f].items():
            if i == 0: cur[dl] = (c, None)
            else:
                pf = frames[i-1]
                bc, bd = min(((pc + trans_w*(dl-pd)**2, pd) for pd, (pc, _) in best[pf].items()), key=lambda x: x[0])
                cur[dl] = (c + bc, bd)
        best[f] = cur
    last = frames[-1]; dl = min(best[last], key=lambda k_: best[last][k_][0]); out = {last: dl}
    for i in range(len(frames)-1, 0, -1):
        dl = best[frames[i]][dl][1]; out[frames[i-1]] = dl
    return out, table
