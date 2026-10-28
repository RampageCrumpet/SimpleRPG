# Per-frame torso-yaw choice: for each frame, try several torso yaw extras (sword world pose held fixed), each with a small roll x swivel grid,
# keep the best.  Set SP, GRIP_FILE, FINAL_FILE, X_IN, TY_FRAMES, TY_OUT, optional TY_VALUES before exec.
exec(open(SP + "session6.py").read()); exec(open(SP + "gridopt.py").read())
LIM_COMFORT['twist'], LIM_HARD['twist'] = globals().get('TWIST', (45.0, 80.0))
base = BASE_N; _FR = list(range(0, 131))
XS = load_X(X_IN, _FR, 14); TIP = init_motion_cache(base, XS, GRIP, _FR)
TYV = globals().get('TY_VALUES', (0, 10, 20, 30)); _t0 = time.time(); res = {}
for f in TY_FRAMES:
    tv = (TIP[f+1] - TIP[f-1]) if (f+1 in TIP and f-1 in TIP) else None
    best = None; rows = {}
    for ty in TYV:
        cb = None
        for r in range(-40, 41, 20):
            for sv in range(-150, 151, 30):
                x = list(XS[f]); x[0] = r; x[1] = 0.0; x[2] = 0.0; x[8] = ty
                v = apply_off7(base[f], x); v[K_ROLL_IDX+1] = sv; v[K_ROLL_IDX+2] = sv
                p = v2p(v); p['twist_share'] = 0.0; p.update(GRIP); d = solve(p)
                cR = COMF_W*(wrist_comfort_cost(d['RightWrist'], 'Right') + clearance_cost(d['clear']['Right']))
                cL = COMF_W*(wrist_comfort_cost(d['LeftWrist'], 'Left') + clearance_cost(d['clear']['Left']))
                rows.setdefault((ty, r), [1e9, 1e9, None]); e = rows[(ty, r)]
                if cR < e[0]: e[0] = cR
                if cL < e[1]: e[1] = cL
                if e[2] is None: e[2] = lead_cost(d, tv, f)
        for (t_, r), e in rows.items():
            if t_ != ty: continue
            c = e[0] + e[1] + e[2]
            if best is None or c < best[0]: best = (c, ty, r)
    res[f] = {'best': best, 'by_ty': {ty: min(e[0]+e[1]+e[2] for (t_, r), e in rows.items() if t_ == ty) for ty in TYV}}
json.dump({str(f): v for f, v in res.items()}, open(SP + TY_OUT, "w"))
print("ty scan frames %d-%d in %.0fs" % (TY_FRAMES[0], TY_FRAMES[-1], time.time()-_t0))
print({f: (res[f]['best'][1], round(res[f]['best'][0],1)) for f in TY_FRAMES})
