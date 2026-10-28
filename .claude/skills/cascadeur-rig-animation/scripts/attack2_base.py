# Attack 2 (rising left-diagonal return) base motion, built by re-using Attack 1's solved full-body vectors:
#   windup 0-41   : Attack 1's combo end pose (exact) eased into a "drawn" chamber: Attack 1's follow-through extreme, wound a little further
#                   left, hilt drawn back/down and the point trailing
#   hit 41-55     : Attack 1's cut played BACKWARDS (extreme -> top), bell-shaped tip speed (arclength re-timing)
#   combo 55-58   : three frames of settling at the top (the thrust chamber starts from there)
#   neutral 55-112: Attack 1's windup played backwards (top -> Ready), S-curve in arclength, ending exactly on the Ready pose
# Roll and swivels are NOT meaningful here (re-solved by grid + DP afterwards).  Needs session6 + retime9 + gridopt9 loaded.
A2_HIT0, A2_HIT1, A2_COMBO_END, A2_NEUTRAL_END, A2_NEUTRAL_TAIL = 41, 58, 61, 115, 69
SO_A2 = 3*NAMES3.index('sw_off'); SD_A2 = 3*NAMES3.index('sw_dir')

def bell_profile(n, peak=0.45, tail=0.15):
    """normalised cumulative distance for n steps with a bell-shaped speed that ends at `tail` of peak"""
    t = (np.arange(n)+0.5)/n
    s = np.sin(np.pi*t**(np.log(0.5)/np.log(peak))) + tail*t
    c = np.concatenate([[0.0], np.cumsum(s)]); return c/c[-1]

def hermite01(tau, m0, m1=0.0):
    t = tau; return (t**3-2*t**2+t)*m0 + (-2*t**3+3*t**2) + (t**3-t**2)*m1

class ArcPath:
    """arclength parametrisation of the tip along a source-frame range g_a -> g_b of the vectors V (g_a may be larger than g_b)"""
    def __init__(self, V, grip, g_a, g_b, step=0.5):
        n = int(round(abs(g_b-g_a)/step)); self.gs = np.linspace(g_a, g_b, n+1)
        tips = [tip9(catmull(V, g), grip) for g in self.gs]
        self.S = np.concatenate([[0.0], np.cumsum([np.linalg.norm(b-a) for a, b in zip(tips[:-1], tips[1:])])]) + np.arange(n+1)*1e-6
        self.V = V
    def g_at(self, s): return float(np.interp(min(max(s, 0.0), self.S[-1]), self.S, self.gs))
    def vec_at(self, s): return catmull(self.V, self.g_at(s))

def build_a2_base(N, C, grip, g_ext=93.0, g_top=57.5, extra_yaw=10.0, draw=(0.0, -3.0, -10.0), point_yaw=28.0, point_pitch=-8.0, peak=0.5, tail=0.35, tail_cm=14.0, blend_hit=7, pelvis_yaw=10.0, hips_delta=(0.0, -3.0, 0.0)):
    start = np.array(C[87], float)
    ext = catmull(N, g_ext)
    ext = apply_off7(ext, [0, 0, 0, 0, 0, 0, 0, 0, extra_yaw, 0, 0, 0, 0, 0])
    ext[SO_A2:SO_A2+3] += np.array(draw)
    ext[SD_A2:SD_A2+3] = rot_dir(ext[SD_A2:SD_A2+3]/np.linalg.norm(ext[SD_A2:SD_A2+3]), point_yaw, point_pitch)
    ext[3*NAMES3.index('pelvis')] += pelvis_yaw                      # body-only loading: costs the arms nothing (sword is in the torso frame)
    ext[3*NAMES3.index('hips'):3*NAMES3.index('hips')+3] += np.array(hips_delta)
    ready = np.array(N[0], float)
    ts, te = tip9(start, grip), tip9(ext, grip); dt = te - ts
    vt = tip9(np.array(C[87], float), grip) - tip9(np.array(C[86], float), grip)       # tip velocity (cm/frame) Attack 1's combo clip ended with
    m0 = float(max(0.0, min(3.0, np.dot(vt, dt)/max(float(np.dot(dt, dt)), 1e-9)*A2_HIT0)))   # start the windup with that velocity
    cut = ArcPath(N, grip, g_ext, g_top)                       # extreme -> top
    S_hit = cut.S[-1]
    ext_on_path = catmull(N, g_ext); d_ext = ext - ext_on_path
    neutral = {}
    for f in range(0, A2_HIT0+1):                              # windup: start -> drawn chamber, smooth ease
        u = hermite01(f/float(A2_HIT0), m0); neutral[f] = (1-u)*start + u*ext
    cum = bell_profile(A2_HIT1 - A2_HIT0, peak=peak, tail=tail)
    for k in range(1, A2_HIT1 - A2_HIT0 + 1):
        f = A2_HIT0 + k; v = cut.vec_at(cum[k]*S_hit)
        w = max(0.0, 1.0 - k/float(blend_hit)); neutral[f] = v + (smoothstep(w))*d_ext     # the draw offset fades out over the first hit frames
    # speed at the end of the hit (cm per frame) drives the tail and the recovery start
    v_end = float(np.linalg.norm(tip9(neutral[A2_HIT1], grip) - tip9(neutral[A2_HIT1-1], grip)))
    tail = {}
    n_t = A2_COMBO_END - A2_HIT1
    # continue beyond g_top along the (reversed Attack 1) path while decelerating from the speed the hit ended with
    cut2 = ArcPath(N, grip, g_top, g_top-5.0)
    def tail_vec(k, n):
        u = k/float(n); s_extra = v_end*n/2.0*(1-(1-u)**2); return cut2.vec_at(s_extra)
    n_t = A2_COMBO_END - A2_HIT1
    for k in range(1, n_t+1): tail[A2_HIT1+k] = tail_vec(k, n_t)                       # combo: 3 frames
    n_tn = A2_NEUTRAL_TAIL - A2_HIT1
    for k in range(1, n_tn+1): neutral[A2_HIT1+k] = tail_vec(k, n_tn)                    # neutral: longer follow-through
    top = np.array(neutral[A2_NEUTRAL_TAIL], float); n_r = A2_NEUTRAL_END - A2_NEUTRAL_TAIL
    for k in range(1, n_r+1):                                                            # then smootherstep to Ready
        tau = k/float(n_r); u = tau**3*(tau*(6*tau-15)+10)
        neutral[A2_NEUTRAL_TAIL+k] = (1-u)*top + u*ready
    neutral[A2_NEUTRAL_END] = ready
    combo = {f: neutral[f] for f in range(0, A2_HIT1+1)}; combo.update(tail)
    for D_ in (neutral, combo):
        for f in D_:
            d = D_[f][SD_A2:SD_A2+3]; D_[f][SD_A2:SD_A2+3] = d/np.linalg.norm(d)
    return neutral, combo, dict(S_hit=S_hit, v_end=v_end, m0=m0)
