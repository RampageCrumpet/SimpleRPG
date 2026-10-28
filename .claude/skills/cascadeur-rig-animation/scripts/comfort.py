# Anatomical comfort + body clearance costs and reporting (needs posesolver.py loaded).
# Flexion/extension labels depend on the assumed palmar side (thumb base); the magnitudes and radial/ulnar sign are reliable.
LIM_COMFORT = dict(flex=50.0, ext=50.0, rad=12.0, uln=25.0, twist=45.0)   # working range for the strike
LIM_HARD    = dict(flex=72.0, ext=72.0, rad=20.0, uln=35.0, twist=80.0)   # never exceed
CLEAR_MIN   = dict(elbow=22.0, fore=20.0, wrist=15.0, upper=12.0)         # cm from the spine axis (torso half-thickness ~18-20)
TWIST_REF = {'Right': 0.0, 'Left': 0.0}                                   # set to the Ready pose's twist

def _excess(v, lim): return max(0.0, v - lim)

def wrist_comfort_cost(w, side, soft_w=0.004, hard_w=0.25):
    f, dv, tw = w['flex'], w['dev'], w['hand_twist'] - TWIST_REF[side]
    c = 0.0
    for val, key_pos, key_neg in ((f, 'flex', 'ext'),):
        c += soft_w*_excess(val, LIM_COMFORT[key_pos])**2 + soft_w*_excess(-val, LIM_COMFORT[key_neg])**2
        c += hard_w*_excess(val, LIM_HARD[key_pos])**2 + hard_w*_excess(-val, LIM_HARD[key_neg])**2
    c += soft_w*_excess(dv, LIM_COMFORT['rad'])**2 + soft_w*_excess(-dv, LIM_COMFORT['uln'])**2
    c += hard_w*_excess(dv, LIM_HARD['rad'])**2 + hard_w*_excess(-dv, LIM_HARD['uln'])**2
    c += soft_w*_excess(abs(tw), LIM_COMFORT['twist'])**2 + hard_w*_excess(abs(tw), LIM_HARD['twist'])**2
    c += 0.02*max(0.0, w['elbow_bend']-140)**2 + 0.02*max(0.0, 25-w['elbow_bend'])**2
    return c

def clearance_cost(clear, weight=0.5):
    c = 0.0
    for k, lim in CLEAR_MIN.items():
        c += weight*_excess(lim - clear[k], 0.0)**2
    return c

def pose_comfort(d):
    return (wrist_comfort_cost(d['RightWrist'], 'Right') + wrist_comfort_cost(d['LeftWrist'], 'Left')
            + clearance_cost(d['clear']['Right']) + clearance_cost(d['clear']['Left']))

def summarize(sols, label, frames=None):
    fr = list(frames if frames is not None else sorted(sols))
    out = {}
    for sd in ('Right', 'Left'):
        W = [sols[f][sd+'Wrist'] for f in fr]
        fl = np.array([w['flex'] for w in W]); dv = np.array([w['dev'] for w in W]); tw = np.array([w['hand_twist'] for w in W])
        out[sd] = dict(flex_max=fl.max(), ext_max=-fl.min(), rad_max=dv.max(), uln_max=-dv.min(), twist_lo=tw.min(), twist_hi=tw.max(),
                       n_flex=int((fl > LIM_COMFORT['flex']).sum()), n_ext=int((-fl > LIM_COMFORT['ext']).sum()),
                       n_rad=int((dv > LIM_COMFORT['rad']).sum()), n_uln=int((-dv > LIM_COMFORT['uln']).sum()))
    cl = {sd: {k: min(sols[f]['clear'][sd][k] for f in fr) for k in CLEAR_MIN} for sd in ('Right', 'Left')}
    print(label)
    for sd in ('Right', 'Left'):
        o = out[sd]
        print("  %s wrist: flex max %.0f ext max %.0f | radial max %.0f ulnar max %.0f | twist [%.0f, %.0f] | frames beyond comfort: flex %d ext %d rad %d uln %d" % (
            sd, o['flex_max'], o['ext_max'], o['rad_max'], o['uln_max'], o['twist_lo'], o['twist_hi'], o['n_flex'], o['n_ext'], o['n_rad'], o['n_uln']))
        print("  %s min clearance from spine axis (cm): elbow %.1f (>=%.0f)  forearm %.1f (>=%.0f)  wrist %.1f (>=%.0f)  upper arm %.1f (>=%.0f)" % (
            sd, cl[sd]['elbow'], CLEAR_MIN['elbow'], cl[sd]['fore'], CLEAR_MIN['fore'], cl[sd]['wrist'], CLEAR_MIN['wrist'], cl[sd]['upper'], CLEAR_MIN['upper']))
    return out, cl
