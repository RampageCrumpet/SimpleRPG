# Branch layout helpers: the Combo continuation and the Neutral recovery must start from the SAME pose, the last frame of the active hit.
# Needs session6 + retime9 (catmull, smoothstep via gridopt9) loaded.

def fix_combo_branch(N, C, branch, end, fade=12):
    """Combo frames 0..branch := Neutral frames 0..branch (shared attack).  Frames branch+1..end keep the old Combo vectors plus the pose difference
    at the branch frame, faded out smoothly over `fade` frames, so the Combo END pose (the next attack's first frame) is unchanged."""
    new = {f: np.array(C[f], float) for f in C}
    for f in range(0, branch+1): new[f] = np.array(N[f], float)
    delta = np.array(N[branch], float) - np.array(C[branch], float)
    for f in range(branch+1, end+1):
        w = 1.0 - smoothstep((f - branch)/float(fade)); new[f] = np.array(C[f], float) + w*delta
        d = new[f][3*NAMES3.index('sw_dir'):3*NAMES3.index('sw_dir')+3]; new[f][3*NAMES3.index('sw_dir'):3*NAMES3.index('sw_dir')+3] = d/np.linalg.norm(d)
    return new, delta

def retime_attack(Vn, Vc, hit_start_old, shift, hit_end_new):
    """Move the end of the hit `shift` frames earlier without touching the movements: the windup (frames 0..hit_start_old) is resampled (Catmull) to
    hit_start_old-shift frames, everything after is copied with an offset.  Neutral and Combo are treated identically so they still branch from the
    same pose.  Returns (new neutral, new combo)."""
    hs_new = hit_start_old - shift
    def one(V, last):
        out = {}
        for f in range(0, hs_new+1): out[f] = catmull(V, f*hit_start_old/float(hs_new)) if f > 0 else np.array(V[0], float)
        for f in range(hs_new+1, last-shift+1): out[f] = np.array(V[f+shift], float)
        return out
    return one(Vn, max(Vn)), one(Vc, max(Vc))


def hermite01_(tau, m0, m1=0.0):
    t = tau; return (t**3-2*t**2+t)*m0 + (-2*t**3+3*t**2) + (t**3-t**2)*m1

def retime_attack_ease(Vn, Vc, hit_start_old, shift, m0_old=3.0, exponent=2.5):
    """Like retime_attack, but also replaces the windup's ease-in/out tail (which parks the sword for ~12 frames before the hit) with an ease-out
    u = 1-(1-t)^exponent over the shortened windup.  The source pose at windup progress u is found by inverting the old ease hermite01(g/old, m0_old)."""
    hs_new = hit_start_old - shift
    def g_of(u):
        lo, hi = 0.0, 1.0
        for _ in range(50):
            mid = 0.5*(lo+hi)
            if hermite01_(mid, m0_old) < u: lo = mid
            else: hi = mid
        return 0.5*(lo+hi)*hit_start_old
    def one(V, last):
        out = {}
        for f in range(0, hs_new+1):
            t = f/float(hs_new); u = 1.0 - (1.0-t)**exponent
            out[f] = np.array(V[0], float) if f == 0 else (np.array(V[hit_start_old], float) if f == hs_new else catmull(V, g_of(u)))
        for f in range(hs_new+1, last-shift+1): out[f] = np.array(V[f+shift], float)
        return out
    return one(Vn, max(Vn)), one(Vc, max(Vc))
