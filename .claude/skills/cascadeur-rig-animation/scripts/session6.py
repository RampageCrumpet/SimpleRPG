# Session preamble for the v6 optimisation chunks.  Set SP (work dir) before exec.  Provides:
#   grip, base (frame -> vector), X (offsets), TIP, save_X(), report6(frames)
import csc, json, time
for _f in ("rigmath", "posesolver", "comfort", "attack1", "attack1_v3", "optroll", "optroll2", "opt3", "opt4", "opt5", "interp",
           "run_refine", "edgedp", "trajopt", "trajopt6", "trajopt7", "gripopt"):
    exec(open(SP + _f + ".py").read())
_d0 = solve(dict(right_foot=GR[M+'RightFoot'][0], left_foot=GR[M+'LeftFoot'][0]))
TWIST_REF['Right'] = _d0['RightWrist']['hand_twist']; TWIST_REF['Left'] = _d0['LeftWrist']['hand_twist']     # twist at the ORIGINAL Ready grip
GRIP = {k: (tuple(v) if isinstance(v, (list, tuple)) else float(v)) for k, v in json.load(open(SP + GRIP_FILE))['grip'].items()}
_FINAL = json.load(open(SP + globals().get("FINAL_FILE", "final_vecs2.json")))
BASE_N = {int(f): np.array(v) for f, v in _FINAL['neutral'].items()}
BASE_C = {int(f): np.array(v) for f, v in _FINAL['combo'].items()}

def load_X(name, frames, n=14):
    try: raw = {int(k): list(v) + [0.0]*(n-len(v)) for k, v in json.load(open(SP + name)).items()}
    except Exception: raw = {}
    return {f: raw.get(f, [0.0]*n) for f in frames}

def save_X(name, X): json.dump({str(f): [float(t) for t in v] for f, v in X.items()}, open(SP + name, "w"))

def tips_for(base, X, frames):
    return {f: solve6(base[f], X[f], GRIP)['tip'] for f in frames}

def report6(base, X, frames, label, every=1):
    sols = {f: solve6(base[f], X[f], GRIP) for f in frames}
    return sols, summarize(sols, label, frames)

# ---- optimiser settings used for Attack 1 v6 (priority: hard anatomical limits > torso clearance > comfort > edge lead) ----
LIM_COMFORT['twist'] = 70.0; LIM_HARD['twist'] = 100.0          # forearm/hand twist relative to the Ready grip
_cc = clearance_cost
def clearance_cost(clear, weight=2.5): return _cc(clear, weight)
SCALE = np.array([6, 10, 10, 1.5, 1.5, 1.5, 3.0, 3.0, 3.0, 3.0, 3.0])
_fc6 = frame_cost6
def frame_cost6(f, x, base, X, TIP, grip, **kw):
    kw = dict(kw); kw.update(pos_w=0.04, dir_w=0.04, w_edge=5.0)
    return _fc6(f, x, base, X, TIP, grip, **kw)
