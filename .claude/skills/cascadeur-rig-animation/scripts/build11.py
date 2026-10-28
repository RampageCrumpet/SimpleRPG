# v11: build the follow-through around a user-posed sword at a given frame (the user edits the sword at frame 80 in Cascadeur; read it back from
# the scene, convert it to solver parameters, time-map the old path so the old pose nearest to it happens at that frame, then add the leftover
# difference as a bell-shaped correction).  Needs session6 + retime9 loaded.
K11 = 3*len(NAMES3); SO11 = 3*NAMES3.index('sw_off'); SD11 = 3*NAMES3.index('sw_dir'); TI11 = 3*NAMES3.index('torso')

def sol11(v, grip):
    p = v2p(np.array(v, float)); p['twist_share'] = 0.0; p.update(grip); return solve(p)

def sword_params_from_world(v, grip, Pt, Rt):
    """invert Psw = Spine2 + tor*(PSR - s2_rest + sw_off), Rsw = tor*qaa(dir, roll)*swing(D0, dir)*RSR for the torso in vector v"""
    d = sol11(v, grip); tor = eul(*v[TI11:TI11+3]); s2_rest = GR[M+'Spine2'][0]
    off = qrot(qinv(tor), Pt - d['G'][M+'Spine2'][0]) - (PSR - s2_rest)
    Rrel = qm(qinv(tor), Rt); dirv = qrot(Rrel, [0, 0, -1.]); dirv /= np.linalg.norm(dirv)
    A = qm(Rrel, qinv(qm(swing(D0, dirv), RSR)))
    if A[0] < 0: A = -A
    return off, dirv, math.degrees(2*math.atan2(float(np.dot(A[1:], dirv)), A[0]))

def clamped_spline3(t, y, m0, m2):
    h0, h1 = t[1]-t[0], t[2]-t[1]; d0, d1 = (y[1]-y[0])/h0, (y[2]-y[1])/h1
    m1 = ((6*d1 - 2*m2)/h1 + (6*d0 - 2*m0)/h0)/(4/h1 + 4/h0)
    def s(x):
        if x <= t[1]: a, b, ya, yb, ma, mb, h = t[0], t[1], y[0], y[1], m0, m1, h0
        else: a, b, ya, yb, ma, mb, h = t[1], t[2], y[1], y[2], m1, m2, h1
        u = (x - a)/h
        return (2*u**3-3*u**2+1)*ya + (u**3-2*u**2+u)*h*ma + (-2*u**3+3*u**2)*yb + (u**3-u**2)*h*mb
    return s

def timemap11(V, grip, f_peak=60, f_anchor=80, g_anchor=73.0, g_extreme=88, f_ends=(90, 92, 94, 96, 98), fmax=126):
    """returns g(f) for f in f_peak..fmax: arclength of the tip is a clamped cubic through (peak, anchor, extreme) so speed decays monotonically"""
    gs = np.arange(f_peak, g_extreme+0.001, 0.25)
    tips = [sol11(catmull(V, g), grip)['tip'] for g in gs]
    So = np.concatenate([[0.0], np.cumsum([np.linalg.norm(b-a) for a, b in zip(tips[:-1], tips[1:])])]) + np.arange(len(gs))*1e-6
    S_of_g = lambda g: float(np.interp(g, gs, So))
    v0 = float(np.linalg.norm(sol11(V[f_peak+1], grip)['tip'] - sol11(V[f_peak], grip)['tip']))
    for f_end in f_ends:
        s = clamped_spline3([f_peak, f_anchor, f_end], [0.0, S_of_g(g_anchor), So[-1]], v0, 0.0)
        S = np.array([s(f) for f in range(f_peak, f_end+1)]); sp = np.diff(S)
        if sp.min() >= -1e-6 and np.all(np.diff(sp) <= 0.35): break          # monotone, speed never rises
    g = {f: float(f) for f in range(0, f_peak)}
    for f in range(f_peak, f_end+1): g[f] = float(np.interp(S[f-f_peak], So, gs))
    for f in range(f_end+1, fmax+1): g[f] = g_extreme + (f - f_end)*(fmax - g_extreme)/float(fmax - f_end)
    for f in range(fmax+1, max(V)+1): g[f] = float(f)
    return g, f_end, sp

def build_v11(V, grip, Pt, Rt, f_anchor=80, g_anchor=73.0, half_width=12.0, **kw):
    g, f_end, sp = timemap11(V, grip, f_anchor=f_anchor, g_anchor=g_anchor, **kw)
    New = {f: catmull(V, g[f]) if g[f] != f else np.array(V[f], float) for f in V}
    off, dirv, roll = sword_params_from_world(New[f_anchor], grip, Pt, Rt)
    d_off = off - New[f_anchor][SO11:SO11+3]
    d_dir = dirv - New[f_anchor][SD11:SD11+3]/np.linalg.norm(New[f_anchor][SD11:SD11+3])
    d_roll = wrap(roll - New[f_anchor][K11])
    for f in New:
        u = (f - f_anchor)/half_width
        if abs(u) >= 1: continue
        w = smoothstep(1 - abs(u))
        New[f][SO11:SO11+3] += w*d_off
        dn = New[f][SD11:SD11+3]/np.linalg.norm(New[f][SD11:SD11+3]) + w*d_dir
        New[f][SD11:SD11+3] = dn/np.linalg.norm(dn)
        New[f][K11] += w*d_roll
    return New, g, f_end, sp, (off, dirv, roll)
