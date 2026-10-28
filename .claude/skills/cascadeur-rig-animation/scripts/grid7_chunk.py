# Stage-A grid chunk for v7.  Set SP, GRIP_FILE, FINAL_FILE, X_IN, GRID_FRAMES (range), TAB_OUT before exec.
exec(open(SP + "session6.py").read()); exec(open(SP + "gridopt.py").read())
LIM_COMFORT['twist'], LIM_HARD['twist'] = globals().get('TWIST', (45.0, 80.0))
_clip = globals().get('CLIP', 'neutral')
base = BASE_N if _clip == 'neutral' else BASE_C; _FR = list(range(0, 131)) if _clip == 'neutral' else list(range(0, 88))
XS = load_X(X_IN, _FR, 14); TIP = init_motion_cache(base, XS, GRIP, _FR)
_t0 = time.time(); tabs = {}
for f in GRID_FRAMES: tabs[f] = stageA7(f, base, XS, TIP, GRIP)
pickle.dump(tabs, open(SP + TAB_OUT, "wb")); print("grid frames %d-%d in %.0fs" % (GRID_FRAMES[0], GRID_FRAMES[-1], time.time()-_t0))
