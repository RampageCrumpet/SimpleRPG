# v9: clean single-swing tip path.  The v8 base vectors were built by LINEAR interpolation between frames in the time warp, which left
# speed bumps (13 -> 24 -> 14 -> 27 cm/frame) and 30-80 degree direction changes of the tip between frames 64-81.  Fix: Gaussian-smooth the pose
# columns, then re-time along the smoothed path with a monotone speed decay and CUBIC (Catmull-Rom) sampling.  Needs session6 loaded.
KS9 = 3*len(NAMES3)

def tip9(vec, grip):
    p = v2p(np.array(vec, float)); p['twist_share'] = 0.0; p.update(grip); return solve(p)['tip']

def smooth9(V, lo, hi, sigma, ramp=4, cols=KS9):
    out = {f: np.array(v, float) for f, v in V.items()}
    fl = list(range(lo-8, hi+9)); fmax = max(V)
    for f in range(lo, hi+1):
        w = max(0.0, min(1.0, (f-lo)/float(ramp) + 1e-4, (hi-f)/float(ramp) + 1e-4))
        ks = np.array([math.exp(-0.5*((g-f)/sigma)**2) for g in fl]); ks /= ks.sum()
        sm = sum(k*np.array(V[min(max(g, 0), fmax)], float) for k, g in zip(ks, fl))
        out[f][:cols] = (1-w)*np.array(V[f][:cols], float) + w*sm[:cols]
    return out

def catmull(V, g):
    fmax = max(V); i = int(math.floor(g)); t = g - i
    P = [np.array(V[min(max(j, 0), fmax)], float) for j in (i-1, i, i+1, i+2)]
    return 0.5*((2*P[1]) + (-P[0] + P[2])*t + (2*P[0] - 5*P[1] + 4*P[2] - P[3])*t*t + (-P[0] + 3*P[1] - 3*P[2] + P[3])*t**3)

def smooth_retime(V, grip, lo=58, hi=90, sigma=2.0, f_peak=60, f_end=88, tail=2, v0=None, floor=0.0):
    """V: frame -> full vector.  Smooths the pose columns, then re-times f_peak..f_end along the smoothed tip path with a monotone speed decay
    that starts at the actual speed at f_peak and ends at `floor` cm/frame (0 = comes to rest at f_end).  Returns (new V, g, speeds cm/frame, tau)."""
    Vs = smooth9(V, lo, hi, sigma)
    fr = list(range(f_peak, f_end+1))
    tips = [tip9(Vs[f], grip) for f in fr]
    S_old = [0.0]
    for a, b in zip(tips[:-1], tips[1:]): S_old.append(S_old[-1] + float(np.linalg.norm(b-a)))
    S_old = np.maximum.accumulate(np.array(S_old) + np.arange(len(S_old))*1e-6)
    n = len(fr)-1; S_tot = S_old[-1] - S_old[0]
    ss = [ (lambda t: 3*t*t - 2*t**3)(max(0.0, min(1.0, (k - tail)/float(n - tail)))) for k in range(1, n+1)]
    v0 = float(np.linalg.norm(tips[1] - tips[0])) if v0 is None else v0       # keep the speed continuous at the peak
    def prof(tau_):
        sc = (v0 - floor*ss[0])/(math.exp(-1.0/tau_)*(1.0 - ss[0]))
        return np.array([sc*math.exp(-(k+1)/tau_)*(1.0 - ss[k]) + floor*ss[k] for k in range(n)])
    lo_t, hi_t = 2.0, 200.0
    for _ in range(60):
        tau = 0.5*(lo_t + hi_t)
        if prof(tau).sum() > S_tot: hi_t = tau
        else: lo_t = tau
    p = prof(tau); p = p*S_tot/p.sum()
    S_new = S_old[0] + np.concatenate([[0.0], np.cumsum(p)])
    out = {f: np.array(v, float) for f, v in Vs.items()}
    g = {}
    for k, f in enumerate(fr):
        gk = float(np.interp(S_new[k], S_old, np.arange(len(fr)))) + f_peak
        g[f] = gk; out[f] = catmull(Vs, gk)
    return out, g, p, tau
