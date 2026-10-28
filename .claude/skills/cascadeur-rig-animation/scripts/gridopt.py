# Grid + dynamic-programming optimiser over (sword roll, right elbow swivel, left elbow swivel) per frame.
# Random search failed to find feasible poses for the cut; a coarse grid found them.  Needs session6 loaded.
import pickle
ROLLS = list(range(-100, 101, 20))
SVS = list(range(-180, 180, 24))
K_ROLL_IDX = 3*len(NAMES3)

def stageA(f, base, XS, TIP, grip, rolls=ROLLS, svs=SVS, w_edge=6.0, edge_fade_end=77):
    """returns dict: edge[ri], cR[ri][si], cL[ri][si] for frame f (other offsets = current XS[f])."""
    tv = (TIP[f+1] - TIP[f-1]) if (f+1 in TIP and f-1 in TIP) else None
    spd = np.linalg.norm(tv)/2*0.6 if tv is not None else 0.0
    fade = min(1.0, max(0.0, (edge_fade_end - f)/6.0))*min(1.0, max(0.0, (spd-3.0)/6.0))
    edge = []; cR = []; cL = []
    base_v = apply_off(base[f], XS[f])
    for r in rolls:
        rowR = []; rowL = []; e = None
        for sv in svs:
            v = np.array(base_v); v[K_ROLL_IDX] = base[f][K_ROLL_IDX] + r; v[K_ROLL_IDX+1] = sv; v[K_ROLL_IDX+2] = sv
            p = v2p(v); p['twist_share'] = 0.0; p.update(grip); d = solve(p)
            if e is None: e = (w_edge*fade*(max(0.0, edge_angle(d, tv)-10.0)/15.0)**2) if (tv is not None and fade > 0) else 0.0
            rowR.append(COMF_W*(wrist_comfort_cost(d['RightWrist'], 'Right') + clearance_cost(d['clear']['Right'])) + 50*abs(d['RightArmErr']))
            rowL.append(COMF_W*(wrist_comfort_cost(d['LeftWrist'], 'Left') + clearance_cost(d['clear']['Left'])) + 50*abs(d['LeftArmErr']))
        edge.append(e); cR.append(rowR); cL.append(rowL)
    return dict(edge=np.array(edge), cR=np.array(cR), cL=np.array(cL))

def wrapd(a): return (a + 180) % 360 - 180

def dp_path(tabs, frames, rolls=ROLLS, svs=SVS, lam_r=0.01, lam_s=0.03, start=None, max_step=30):
    """tabs: frame -> stageA dict.  Roll path first (node cost = edge + best swivels), then each arm's swivel path."""
    n = len(frames); nr = len(rolls)
    node = np.array([tabs[f]['edge'] + tabs[f]['cR'].min(axis=1) + tabs[f]['cL'].min(axis=1) for f in frames])   # n x nr
    best = np.zeros((n, nr)); back = np.zeros((n, nr), int)
    best[0] = node[0] + (0 if start is None else lam_r*(np.array(rolls)-start)**2)
    for i in range(1, n):
        for j in range(nr):
            cand = best[i-1] + lam_r*(np.array(rolls)-rolls[j])**2
            k = int(np.argmin(cand)); back[i, j] = k; best[i, j] = node[i, j] + cand[k]
    j = int(np.argmin(best[-1])); rp = [j]
    for i in range(n-1, 0, -1): j = back[i, j]; rp.append(j)
    rp = rp[::-1]
    out = {f: {'roll': rolls[rp[i]]} for i, f in enumerate(frames)}
    for side, key in (('R', 'cR'), ('L', 'cL')):
        ns = len(svs); bs = np.zeros((n, ns)); bk = np.zeros((n, ns), int)
        bs[0] = np.array([tabs[frames[0]][key][rp[0]][s] for s in range(ns)])
        for i in range(1, n):
            node_s = np.array([tabs[frames[i]][key][rp[i]][s] for s in range(ns)])
            for s in range(ns):
                cand = bs[i-1] + lam_s*np.array([wrapd(svs[s]-svs[q])**2 + (1e6 if abs(wrapd(svs[s]-svs[q])) > max_step else 0.0) for q in range(ns)])   # swivel may move at most max_step degrees per frame
                q = int(np.argmin(cand)); bk[i, s] = q; bs[i, s] = node_s[s] + cand[q]
        s = int(np.argmin(bs[-1])); sp = [s]
        for i in range(n-1, 0, -1): s = bk[i, s]; sp.append(s)
        sp = sp[::-1]
        for i, f in enumerate(frames): out[f]['sv'+side] = svs[sp[i]]
    return out

def apply_path(path, base, XS):
    """write the chosen roll/swivels into the offset table (offsets are relative to the base vector)."""
    for f, p in path.items():
        XS[f][0] = float(p['roll'])
        XS[f][1] = float(wrapd(p['svR'] - base[f][K_ROLL_IDX+1]))
        XS[f][2] = float(wrapd(p['svL'] - base[f][K_ROLL_IDX+2]))
    return XS


# ---- v7: grid over (roll, swivel) with torso/pelvis offsets and the lead rule of trajopt7 -------------------------
ROLLS7 = list(range(-60, 61, 20))
def stageA7(f, base, XS, TIP, grip, rolls=ROLLS7, svs=SVS):
    tv = (TIP[f+1] - TIP[f-1]) if (f+1 in TIP and f-1 in TIP) else None
    edge = []; cR = []; cL = []
    for r in rolls:
        rowR = []; rowL = []; e = None
        for sv in svs:
            x = list(XS[f]); x[0] = r; x[1] = 0.0; x[2] = 0.0
            v = apply_off7(base[f], x); v[K_ROLL_IDX+1] = sv; v[K_ROLL_IDX+2] = sv
            p = v2p(v); p['twist_share'] = 0.0; p.update(grip); d = solve(p)
            if e is None: e = lead_cost(d, tv, f)
            rowR.append(COMF_W*(wrist_comfort_cost(d['RightWrist'], 'Right') + clearance_cost(d['clear']['Right'])) + 50*abs(d['RightArmErr']))
            rowL.append(COMF_W*(wrist_comfort_cost(d['LeftWrist'], 'Left') + clearance_cost(d['clear']['Left'])) + 50*abs(d['LeftArmErr']))
        edge.append(e); cR.append(rowR); cL.append(rowL)
    return dict(edge=np.array(edge), cR=np.array(cR), cL=np.array(cL))

def apply_path7(path, base, XS):
    for f, p in path.items():
        XS[f][0] = float(p['roll'])
        XS[f][1] = float(wrapd(p['svR'] - base[f][K_ROLL_IDX+1]))
        XS[f][2] = float(wrapd(p['svL'] - base[f][K_ROLL_IDX+2]))
    return XS
