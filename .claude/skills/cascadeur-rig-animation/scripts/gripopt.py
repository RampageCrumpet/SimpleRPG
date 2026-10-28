# Search for a hand-to-sword grip (rotation of each hand about its fist centre, hand-local degrees) that keeps both wrists
# inside the comfort ranges at Ready and through the animation.  Needs posesolver, comfort, interp loaded.
import random

def grip_cost(vecs, frames, grip, weights=None, twist_w=1.0):
    """vecs: frame -> parameter vector; grip: dict(grip_right=(x,y,z), grip_left=(x,y,z))"""
    c = 0.0
    for f in frames:
        p = v2p(np.array(vecs[f])); p['twist_share'] = 0.0
        p.update(grip)
        d = solve(p)
        w = 1.0 if weights is None else weights.get(f, 1.0)
        c += w*pose_comfort(d)
    return c

def grip_search(vecs, frames, start, iters=25, seed=0, scale=6.0, limit=32.0, weights=None, best=None):
    rnd = random.Random(seed)
    cur = dict(start); cj = grip_cost(vecs, frames, cur, weights)
    if best is not None and best[0] < cj: cur, cj = dict(best[1]), best[0]
    for it in range(iters):
        g = {k: list(v) for k, v in cur.items()}
        for _ in range(rnd.choice((1, 2, 3))):
            k = rnd.choice(('grip_right', 'grip_left')); i = rnd.randrange(3)
            g[k][i] = max(-limit, min(limit, g[k][i] + rnd.gauss(0, scale*(1.0 if it < iters//2 else 0.4))))
        g = {k: tuple(v) for k, v in g.items()}
        j = grip_cost(vecs, frames, g, weights)
        if j < cj: cur, cj = g, j
    return cj, cur


# ---- extended grip: tilt about hand axes (deg) + roll around the hilt (deg) for each hand -------------------------
GRIP_LIMITS = [25, 25, 25, 25, 25, 25, 30, 100]       # right tilt xyz, left tilt xyz, right roll (keeps the Unity socket close), left roll

def vec_to_grip(v):
    return dict(grip_right=tuple(v[0:3]), grip_left=tuple(v[3:6]), roll_right=float(v[6]), roll_left=float(v[7]))

def grip_to_vec(g):
    return list(g.get('grip_right', (0, 0, 0))) + list(g.get('grip_left', (0, 0, 0))) + [g.get('roll_right', 0.0), g.get('roll_left', 0.0)]

def grip_search_ext(vecs, frames, start_vec, iters=24, seed=0, scale=8.0, weights=None, right_pref=0.002):
    rnd = random.Random(seed)
    cost = lambda v: grip_cost(vecs, frames, vec_to_grip(v), weights) + right_pref*(v[6]**2)       # mild pull to keep the right (socket) roll small
    cur = list(start_vec); cj = cost(cur)
    for it in range(iters):
        v = list(cur)
        for _ in range(rnd.choice((1, 2, 3))):
            i = rnd.randrange(8)
            v[i] = max(-GRIP_LIMITS[i], min(GRIP_LIMITS[i], v[i] + rnd.gauss(0, scale*(2.0 if i >= 6 else 1.0)*(1.0 if it < iters//2 else 0.4))))
        j = cost(v)
        if j < cj: cur, cj = v, j
    return cj, cur
