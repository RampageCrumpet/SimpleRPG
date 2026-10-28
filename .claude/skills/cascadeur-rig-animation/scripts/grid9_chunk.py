# One stage-A grid chunk for v9.  Set before exec: SP, SK (skill scripts dir), GRIP_FILE, FINAL_FILE, BASE_IN (json of frame->vector in SP),
# GRID_FRAMES (range), TAB_OUT (pickle name in SP), optional ANCHOR_FRAMES (frames pulled toward the base vector's own roll/swivels).
exec(open(SP + "session6.py").read()); exec(open(SP + "gridopt.py").read()); exec(open(SK + "retime9.py").read()); exec(open(SK + "gridopt9.py").read())
ROLLS_USE = globals().get('ROLLS_USE', ROLLS9)
LIM_COMFORT['twist'], LIM_HARD['twist'] = globals().get('TWIST', (45.0, 80.0))
W_UP = globals().get('W_UP', 3.0)
_B = {int(k): np.array(v, float) for k, v in json.load(open(SP + BASE_IN)).items()}
_tip = {}
def _t(f):
    f = min(max(f, min(_B)), max(_B))
    if f not in _tip: _tip[f] = tip9(_B[f], GRIP)
    return _tip[f]
_t0 = time.time(); tabs = {}
for f in GRID_FRAMES:
    tabs[f] = stageA9(f, _B, _t(f+1) - _t(f-1), GRIP, rolls=ROLLS_USE, w_up=W_UP, w_true=globals().get('W_TRUE', 8.0), w_either=globals().get('W_EITHER', 3.0), lead_kw=globals().get('LEAD_KW'), eval_rolls=globals().get('EVAL_ROLLS'), pen=globals().get('ANCHOR_PEN', 0.02), anchor=(tuple(_B[f][K9:K9+3]) if f in globals().get('ANCHOR_FRAMES', ()) else None))
try: _old = pickle.load(open(SP + TAB_OUT, "rb"))
except Exception: _old = {}
_old.update(tabs); pickle.dump(_old, open(SP + TAB_OUT, "wb"))
print("grid frames %d-%d in %.0fs" % (GRID_FRAMES[0], GRID_FRAMES[-1], time.time() - _t0))
