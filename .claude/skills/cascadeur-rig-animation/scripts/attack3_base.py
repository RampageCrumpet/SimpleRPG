# Attack 3 (centerline thrust) base motion from design keys in the torso frame.  Frame 0 is Attack 2's combo end (exact), frame 118 is Ready (exact).
# windup 0-45 (draw the hilt in to the ribs, point on the aim line, rear-leg load), hit 45-59 (hips lead, extension), follow-through/recovery 59-118.
# Needs session6 (mk, GRIP0, RF0, eul, qinv, qrot, p2v ...), retime9, gridopt9 loaded.  Keys give sword grip position in the torso frame and the
# blade direction in WORLD coordinates (converted with the key's torso rotation so the point stays on the aim line while the torso turns).
A3_KEYS = {
    22:  dict(hips=(0, -6, -1.0),   pelvis=(-3, 0, 0), torso=(-8, -1, 3),  head=(5, 0, 0),  sw=(-11, -4, 36),  dirw=(-0.02, 0.55, 0.83), lheel=3),
    45:  dict(hips=(0, -7, -3.5),   pelvis=(-4, 0, 0), torso=(-10, -2, 2), head=(8, 0, 0),  sw=(-10, -15, 22), dirw=(0.0, 0.10, 0.99),  lheel=6),
    52:  dict(hips=(0, -6.5, 8.0),  pelvis=(2, 0, 0),  torso=(-4, 3, 0),   head=(4, -2, 0), sw=(-6, -9, 54),  dirw=(0.0, 0.05, 1.0),   lheel=14, rfoot=(0, 3, 5), lfoot=(0, 0, 1)),
    59:  dict(hips=(0, -6.5, 12.0), pelvis=(4, 0, 0),  torso=(0, 7, -1),   head=(0, -3, 0), sw=(-2, -3, 66),   dirw=(0.0, 0.03, 1.0),   lheel=22, rfoot=(0, 0, 12), lfoot=(0, 0, 3)),
    66:  dict(hips=(0, -6.5, 11.5), pelvis=(4, 0, 0),  torso=(0, 6, -1),   head=(0, -3, 0), sw=(-2.5, -3.5, 64),   dirw=(0.0, 0.03, 1.0),   lheel=21, rfoot=(0, 0, 12), lfoot=(0, 0, 3)),
    82:  dict(hips=(0, -3, 5),      pelvis=(1, 0, 0),  torso=(1, 2, 0),    head=(0, -1, 0), sw=(-4, -10, 44),  dirw=(-0.05, 0.35, 0.93), lheel=8, rfoot=(0, 0, 6),  lfoot=(0, 0, 1.5)),
    100: dict(hips=(0, -1, 1.5),    pelvis=(0, 0, 0),  torso=(0, 0.5, 0),  head=(0, 0, 0),  sw=(0, -11, 48),   dirw=(-0.15, 0.8, 0.55),  lheel=1, rfoot=(0, 0, 1.5)),
}

def a3_key_vec(k):
    t = eul(*k['torso']); dn = qrot(qinv(t), n3(k['dirw']))
    d = mk(hips=k['hips'], pelvis=k['pelvis'], torso=k['torso'], head=k['head'], sw=k['sw'], sw_dir=dn, lheel=k.get('lheel', 0),
           rfoot=k.get('rfoot', (0, 0, 0)), lfoot=k.get('lfoot', (0, 0, 0)))
    return p2v(d)

def hermite_keys(times, vecs, tang_zero=(), vel0=None, tan_scale=1.0):
    """cubic Hermite through (times, vecs): Catmull-Rom tangents, zero at tang_zero times, vel0 at the first key"""
    T = list(times); V = [np.asarray(v, float) for v in vecs]; n = len(T); Mt = []
    for i in range(n):
        if T[i] in tang_zero or i == n-1: Mt.append(np.zeros_like(V[0]))
        elif i == 0: Mt.append(np.zeros_like(V[0]) if vel0 is None else np.asarray(vel0, float))
        else: Mt.append(tan_scale*(V[i+1]-V[i-1])/(T[i+1]-T[i-1]))
    def f(t):
        if t <= T[0]: return V[0].copy()
        if t >= T[-1]: return V[-1].copy()
        j = max(i for i in range(n-1) if T[i] <= t); h = T[j+1]-T[j]; s = (t-T[j])/h
        return (2*s**3-3*s**2+1)*V[j] + (s**3-2*s**2+s)*h*Mt[j] + (-2*s**3+3*s**2)*V[j+1] + (s**3-s**2)*h*Mt[j+1]
    return f

def build_a3_base(start_vec, prev_vec, ready_vec, n_total=118, zero_at=(45, 59, 66), tan_scale=1.0):
    kp = 3*len(NAMES3)
    times = [0] + sorted(A3_KEYS) + [n_total]
    vecs = [np.array(start_vec, float)] + [a3_key_vec(A3_KEYS[t]) for t in sorted(A3_KEYS)] + [np.array(ready_vec, float)]
    vel0 = (np.array(start_vec, float) - np.array(prev_vec, float))
    # sword direction columns need to stay unit length and roll/swivel columns are placeholders (re-solved later): keep the start roll fading to Ready's
    f = hermite_keys(times, vecs, tang_zero=zero_at + (n_total,), vel0=vel0, tan_scale=tan_scale)
    out = {}
    i = 3*NAMES3.index('sw_dir')
    for fr in range(0, n_total+1):
        v = f(fr); v[i:i+3] = v[i:i+3]/np.linalg.norm(v[i:i+3])
        u = smoothstep(fr/float(n_total)); v[kp] = (1-u)*start_vec[kp] + u*ready_vec[kp]; v[kp+1] = (1-u)*start_vec[kp+1] + u*ready_vec[kp+1]; v[kp+2] = (1-u)*start_vec[kp+2] + u*ready_vec[kp+2]
        out[fr] = v
    return out
