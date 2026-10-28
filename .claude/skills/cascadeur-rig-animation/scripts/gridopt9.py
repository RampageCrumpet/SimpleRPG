# v9 grid + DP over (sword roll, right swivel, left swivel) with a wider roll range, a stronger lead rule and an "elbow up" term
# (raising the elbow rotates the hand so the blade can keep its edge forward).  Needs session6, gridopt (dp_path) and retime9 loaded.
ROLLS9 = list(range(-100, 101, 25))
SVS9 = list(range(-180, 180, 24))
K9 = 3*len(NAMES3)

def elbow_up_cost(d, side, w_up=6.0, target=-6.0):
    nm = 'Right' if side == 'R' else 'Left'
    rel = float(d[nm+'Wrist']['elbow_pos'][1] - d['G'][M+nm+'Arm'][0][1])
    return w_up*(max(0.0, min(3.0, (target - rel)/10.0)))**2

def stageA9(f, base, tv, grip, w_up=6.0, rolls=ROLLS9, svs=SVS9, w_true=8.0, w_either=3.0, anchor=None, pen=0.02, lead_kw=None, eval_rolls=None):
    """base: frame -> vector (roll/swivels in the vector are the reference).  tv: tip travel vector at f.  anchor: (roll_ref, svR_ref, svL_ref)
    adds a quadratic pull to those values (used at the ends of a DP window)."""
    edge = []; cR = []; cL = []
    for r in rolls:
        rowR = []; rowL = []; e = None
        if eval_rolls is not None and r not in eval_rolls:
            edge.append(1e5); cR.append([1e5]*len(svs)); cL.append([1e5]*len(svs)); continue
        for sv in svs:
            v = np.array(base[f], float); v[K9] += r; v[K9+1] = sv; v[K9+2] = sv
            p = v2p(v); p['twist_share'] = 0.0; p.update(grip); d = solve(p)
            if e is None:
                e = lead_cost(d, tv, f, w_true=w_true, w_either=w_either, **(lead_kw or {}))
                if anchor is not None: e += pen*(r - (anchor[0] - base[f][K9]))**2
            cr = COMF_W*(wrist_comfort_cost(d['RightWrist'], 'Right') + clearance_cost(d['clear']['Right'])) + 50*abs(d['RightArmErr']) + elbow_up_cost(d, 'R', w_up)
            cl = COMF_W*(wrist_comfort_cost(d['LeftWrist'], 'Left') + clearance_cost(d['clear']['Left'])) + 50*abs(d['LeftArmErr']) + elbow_up_cost(d, 'L', w_up)
            if anchor is not None:
                cr += pen*wrapd(sv - anchor[1])**2; cl += pen*wrapd(sv - anchor[2])**2
            rowR.append(cr); rowL.append(cl)
        edge.append(e); cR.append(rowR); cL.append(rowL)
    return dict(edge=np.array(edge), cR=np.array(cR), cL=np.array(cL))

def apply_path9(path, base):
    """returns frame -> new vector with roll (base roll + chosen offset) and swivels written in"""
    out = {}
    for f, p in path.items():
        v = np.array(base[f], float); v[K9] += float(p['roll']); v[K9+1] = float(p['svR']); v[K9+2] = float(p['svL']); out[f] = v
    return out


def smoothstep(t):
    t = max(0.0, min(1.0, t)); return 3*t*t - 2*t**3

def yaw_profile_neutral(f, peak=18.0):
    """extra torso yaw (deg, sword world pose held fixed): left shoulder/arm pulled back through the end of the swing"""
    return peak*smoothstep((f-68)/12.0)*(1.0 - smoothstep((f-92)/10.0))

def yaw_profile_combo(f, peak=14.0):
    return peak*smoothstep((f-68)/10.0)*(1.0 - smoothstep((f-78)/9.0))

def apply_yaw(V, prof):
    out = {}
    for f, v in V.items():
        y = prof(f)
        out[f] = apply_off7(v, [0, 0, 0, 0, 0, 0, 0, 0, y, 0, 0, 0, 0, 0]) if abs(y) > 1e-6 else np.array(v, float)
    return out
