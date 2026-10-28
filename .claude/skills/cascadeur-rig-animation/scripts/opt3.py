SWIV = list(range(-180, 180, 20))
ROLLS = list(range(-120, 121, 20))

def tip_world(p):
    d = solve(with_roll(p, 0)); return d['tip']

def edge_target(keys, frames, f):
    """world edge target: direction of tip travel at key f (perpendicular part)"""
    i = frames.index(f)
    a = frames[max(i-1, 0)]; b = frames[min(i+1, len(frames)-1)]
    return tip_world(keys[b]) - tip_world(keys[a])

def edge_cost_world(d, tgt, w):
    if tgt is None or w == 0: return 0.0
    P0, R0 = d['sword']
    blade = qrot(R0, [0, 0, -1.]); e = qrot(R0, [-1., 0, 0])
    t = np.asarray(tgt, float); t = t - blade*np.dot(t, blade)
    n = np.linalg.norm(t)
    if n < 1e-6: return 0.0
    t /= n
    a = math.degrees(math.acos(max(-1, min(1, float(np.dot(e, t))))))
    return w*(a/30.)**2

def eval3(p, r, tgt, w_edge):
    cr = []; cl = []; ea = None
    for sv in SWIV:
        q = with_roll(p, r); q['right_swivel'] = sv; q['left_swivel'] = sv
        d = solve(q)
        cr.append(side_cost(d['RightWrist']) + 50*abs(d['RightArmErr']))
        cl.append(side_cost(d['LeftWrist']) + 50*abs(d['LeftArmErr']))
        if ea is None: ea = edge_cost_world(d, tgt, w_edge)
    ir = int(np.argmin(cr)); il = int(np.argmin(cl))
    return cr[ir] + cl[il] + ea, SWIV[ir], SWIV[il]
