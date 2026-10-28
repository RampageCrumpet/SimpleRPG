# Per-key pose polish for a designed key frame (used for Attack 3).  Needs session6 + gridopt9 loaded.  Random search over x = [roll, svR, svL,
# sw_off xyz, (dir yaw, dir pitch: kept at 0 so the blade stays on the aim line), torso yaw/pitch/roll extras, pelvis yaw/pitch/roll extras]
# with the sword WORLD pose held by apply_off7.  Chain the keys in time order and pass the previous key's x as `prev`, otherwise each key lands on a
# different swivel branch and the in-between frames violate limits.  zmax limits how far in front of Spine2 the grip may be (chamber draw-in).
import random
KP_WX = np.array([0.03, 0.4, 0.4, 0.03, 0.4, 0.4])       # yaw extras are cheap, pitch/roll extras expensive (they look wrong)

def kp_cost(Bf, x, prev, zmax=None):
    v = apply_off7(Bf, x); v[K9] = Bf[K9] + x[0]; v[K9+1] = x[1]; v[K9+2] = x[2]
    d = solve(dict(v2p(v), twist_share=0.0, **GRIP))
    c = COMF_W*pose_comfort(d) + 50*(abs(d['RightArmErr']) + abs(d['LeftArmErr']))
    c += 0.05*(np.linalg.norm(x[3:6])/3.0)**2 + float(np.sum(KP_WX*np.array(x[8:14])**2))/16.0
    pv = np.array(prev)
    c += 0.002*float(np.sum((np.array(x[8:14]) - pv[8:14])**2)) + 0.0008*(wrap(x[1]-pv[1])**2 + wrap(x[2]-pv[2])**2) + 0.001*float(np.sum((np.array(x[3:6]) - pv[3:6])**2))
    if zmax is not None: c += 0.4*max(0.0, float(d['sword'][0][2] - d['G'][M+'Spine2'][0][2]) - zmax)**2
    for nm in ('Right', 'Left'):
        if d[nm+'Wrist']['elbow_bend'] < 25: c += 20*((25 - d[nm+'Wrist']['elbow_bend'])/10.0)**2        # never lock the elbow
    return c, d

def kp_polish(Bf, prev, trials=260, zmax=None, seed=0):
    rnd = random.Random(seed); cur = list(prev); cur[0] = 0.0
    cj, cd = kp_cost(Bf, cur, prev, zmax); sc = np.array([20, 30, 30, 3, 3, 3, 0, 0, 5, 5, 5, 4, 4, 4])
    for t in range(trials):
        y = list(cur)
        for k in rnd.sample([0, 1, 2, 3, 4, 5, 8, 9, 10, 11, 12, 13], rnd.choice((1, 2, 3))): y[k] += rnd.gauss(0, sc[k]*(1.0 if t < trials//2 else 0.4))
        j, d = kp_cost(Bf, y, prev, zmax)
        if j < cj: cur, cj, cd = y, j, d
    return cj, cur, cd
