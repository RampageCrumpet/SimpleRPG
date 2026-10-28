# One optimisation chunk (keep each MCP call under ~25 s: about 40 frames x 22 trials).  Set before exec:
#   SP, GRIP_FILE, FINAL_FILE, X_IN, X_OUT, FRAMES (range), SEED, TRIALS, optional TIE, TWIST (comfort, hard), CLIP ('neutral'|'combo')
exec(open(SP + "session6.py").read())
LIM_COMFORT['twist'], LIM_HARD['twist'] = globals().get('TWIST', (45.0, 80.0))
_clip = globals().get('CLIP', 'neutral')
base = BASE_N if _clip == 'neutral' else BASE_C
_FR = list(range(0, 131)) if _clip == 'neutral' else list(range(0, 88))
XS = load_X(X_IN, _FR, 14)
TIP = init_motion_cache(base, XS, GRIP, _FR)
_t0 = time.time()
sweep7(FRAMES, base, XS, TIP, GRIP, trials=TRIALS, seed=SEED, tie=globals().get('TIE'), anchor=globals().get('ANCHOR', ()))
save_X(X_OUT, XS)
sols = {f: solve7(base[f], XS[f], GRIP) for f in _FR}
_tips = {f: sols[f]['tip'] for f in sols}
def _viol(f):
    d = sols[f]; v = []
    for s in ('Right', 'Left'):
        w = d[s+'Wrist']; tw = w['hand_twist'] - TWIST_REF[s]
        if w['dev'] < -LIM_HARD['uln']: v.append("%s ulnar %.0f" % (s[0], -w['dev']))
        if w['dev'] > LIM_HARD['rad']: v.append("%s radial %.0f" % (s[0], w['dev']))
        if abs(w['flex']) > LIM_HARD['flex']: v.append("%s flex %.0f" % (s[0], w['flex']))
        if abs(tw) > LIM_HARD['twist']: v.append("%s twist %.0f" % (s[0], tw))
        c = d['clear'][s]
        if c['elbow'] < 20: v.append("%s elbow %.1f" % (s[0], c['elbow']))
        if c['fore'] < 18: v.append("%s fore %.1f" % (s[0], c['fore']))
    return v
_fl = [f for f in FRAMES]
print("chunk %d-%d done in %.0fs" % (_fl[0], _fl[-1], time.time()-_t0))
_V = {f: _viol(f) for f in _fl if _viol(f)}
print("  violations in chunk:", _V if _V else "none")
print("  left twist vs Ready (every 4):", [(f, round(sols[f]['LeftWrist']['hand_twist']-TWIST_REF['Left'])) for f in _fl[::4]])
print("  right twist vs Ready (every 4):", [(f, round(sols[f]['RightWrist']['hand_twist']-TWIST_REF['Right'])) for f in _fl[::4]])
print("  torso/pelvis extra magnitude (deg, every 4):", [(f, round(float(np.linalg.norm(XS[f][8:11])),1), round(float(np.linalg.norm(XS[f][11:14])),1)) for f in _fl[::4]])
