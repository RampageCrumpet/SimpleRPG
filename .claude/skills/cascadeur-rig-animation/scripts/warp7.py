# Re-time the overswing: decelerate the blade smoothly after the hit (so it never sweeps flat-first at speed), creep through a short
# catch, then recover.  Poses and path are reused; only the timing along the path changes.  Needs session6 loaded.
SPEED_AFTER_HIT = {70: 9.0, 71: 8.0, 72: 7.0, 73: 6.0, 74: 5.0, 75: 4.2, 76: 3.5, 77: 2.8, 78: 2.2, 79: 1.7, 80: 1.4, 81: 1.1, 82: 0.8}   # m/s targets

def tip_arclength(base, frames, grip):
    tips = [solve7(base[f], [0.0]*14, grip)['tip'] for f in frames]
    S_ = [0.0]
    for i in range(1, len(tips)): S_.append(S_[-1] + float(np.linalg.norm(tips[i] - tips[i-1])))
    return dict(zip(frames, S_))

def lerp_vec(base, g):
    """base vector at fractional frame g (linear; swivel/roll columns unwrapped already)"""
    i = int(math.floor(g)); t = g - i
    if i >= max(base): return np.array(base[max(base)])
    return (1-t)*np.array(base[i]) + t*np.array(base[i+1])

def build_warp(base, grip, g_stop_old=79, f_stop=82, f_creep_end=90, g_extreme_old=84, f_last=130):
    S_ = tip_arclength(base, list(range(60, 131)), grip)
    S70, Send = S_[70], S_[g_stop_old]
    sp = [SPEED_AFTER_HIT[f]*100.0/60.0 for f in range(70, f_stop+1)]          # cm per frame
    cum = [0.0]
    for v in sp[:-1]: cum.append(cum[-1] + v)
    k = (Send - S70)/cum[-1]; dist = [c*k for c in cum]                       # scaled so the new stop pose equals the old pose at g_stop_old
    def old_frame_at(s):
        fr = sorted(S_)
        for a, b in zip(fr[:-1], fr[1:]):
            if S_[a] <= s <= S_[b]: return a + (s - S_[a])/max(S_[b]-S_[a], 1e-9)
        return float(fr[-1])
    g = {}
    for f in range(0, 71): g[f] = float(f)
    for i, f in enumerate(range(70, f_stop+1)): g[f] = old_frame_at(S70 + dist[i])
    for f in range(f_stop+1, f_creep_end+1): g[f] = g_stop_old + (f - f_stop)*(g_extreme_old - g_stop_old)/(f_creep_end - f_stop)
    for f in range(f_creep_end+1, f_last+1): g[f] = g_extreme_old + (f - f_creep_end)*(f_last - g_extreme_old)/(f_last - f_creep_end)
    new = {f: lerp_vec(base, min(g[f], f_last)) for f in range(0, f_last+1)}
    return new, g


# ---- v2: one arclength schedule from the end of the hit to the old overswing extreme, then the old recovery ------------
SCHED2 = {70: 16.0, 71: 14.0, 72: 12.0, 73: 10.2, 74: 8.6, 75: 7.2, 76: 6.0, 77: 5.0, 78: 4.1, 79: 3.4, 80: 2.8, 81: 2.3, 82: 1.9, 83: 1.5, 84: 1.2,
          85: 1.0, 86: 0.9, 87: 1.0, 88: 1.4, 89: 2.2}          # m/s targets, hit end -> overswing extreme -> catch

def build_warp2(base, grip, g_end_old=84, f_end=90, f_last=130, sched=SCHED2):
    S_ = tip_arclength(base, list(range(60, 131)), grip)
    S70, Send = S_[70], S_[g_end_old]
    frs = list(range(70, f_end)); sp = [sched[f]*100.0/60.0 for f in frs]
    cum = [0.0]
    for v in sp[:-1]: cum.append(cum[-1] + v)
    k = (Send - S70)/cum[-1]; dist = [c*k for c in cum]
    def old_frame_at(s):
        fr = sorted(S_)
        for a, b in zip(fr[:-1], fr[1:]):
            if S_[a] <= s <= S_[b]: return a + (s - S_[a])/max(S_[b]-S_[a], 1e-9)
        return float(fr[-1])
    g = {f: float(f) for f in range(0, 70)}
    for i, f in enumerate(frs): g[f] = old_frame_at(S70 + dist[i])
    for f in range(f_end, f_last+1): g[f] = g[f_end-1] + (f - (f_end-1))*(f_last - g[f_end-1])/(f_last - (f_end-1))
    new = {f: lerp_vec(base, min(g[f], f_last)) for f in range(0, f_last+1)}
    return new, g, k
