# Overswing shaping (v8): shrink the excursion past the end of the hit by a factor, and push the sword outward along the chest-to-grip
# line as the swing runs into the overswing ("the weight of the blade pulls away from the character").  Needs session6 loaded.
# Roll and swivel columns are NOT scaled (they are re-solved afterwards with the grid + DP).
KS8 = 3*len(NAMES3)                      # first roll/swivel column

def shape_overswing(V, alpha=0.88, push_cm=5.0, f0=70, f_peak=84, f_fade_end=108, f_scale_full=104, f_scale_end=118, f_last=None):
    """V: frame -> full parameter vector (numpy).  Returns a new dict."""
    f_last = max(V) if f_last is None else f_last
    out = {f: np.array(v, float) for f, v in V.items()}
    ref = np.array(V[f0], float)
    rest = PSR - GR[M+'Spine2'][0]
    for f in range(f0+1, f_last+1):
        wscale = 1.0 if f <= f_scale_full else max(0.0, (f_scale_end - f)/float(f_scale_end - f_scale_full))
        v = out[f]
        for c in range(KS8):                                         # pose columns only
            v[c] = V[f][c] - wscale*(1.0 - alpha)*(V[f][c] - ref[c])
        d = v[3*NAMES3.index('sw_dir'):3*NAMES3.index('sw_dir')+3]; n = np.linalg.norm(d)
        if n > 0: v[3*NAMES3.index('sw_dir'):3*NAMES3.index('sw_dir')+3] = d/n
        if f <= f_peak: t = (f - f0)/float(f_peak - f0); push = push_cm*(3*t*t - 2*t**3)
        elif f <= f_fade_end: t = (f_fade_end - f)/float(f_fade_end - f_peak); push = push_cm*(3*t*t - 2*t**3)
        else: push = 0.0
        if push:
            so = 3*NAMES3.index('sw_off'); r_vec = rest + v[so:so+3]; u = r_vec/np.linalg.norm(r_vec)
            v[so:so+3] = v[so:so+3] + push*u
    return out
