RF0 = GR[M+'RightFoot'][0].copy(); LF0 = GR[M+'LeftFoot'][0].copy()
GRIP0 = PSR - GR[M+'Spine2'][0]      # rest grip origin relative to Spine2 (torso frame)

def n3(v):
    v = np.array(v, float); return v/np.linalg.norm(v)

def mk(hips=(0,0,0), pelvis=(0,0,0), torso=(0,0,0), head=(0,0,0), sw=None, sw_dir=None, sw_edge=None, sw_roll=0,
       rheel=0, lheel=0, rfoot=(0,0,0), lfoot=(0,0,0), rfoot_rot=(0,0,0), lfoot_rot=(0,0,0),
       r_elbow=(0,0,0), l_elbow=(0,0,0), rknee=(0,0,0), lknee=(0,0,0), rshrug=(0,0,0), lshrug=(0,0,0)):
    off = (0,0,0) if sw is None else (np.array(sw, float) - GRIP0)
    d = dict(hips=hips, pelvis=pelvis, torso=torso, head=head, sw_off=off, sw_roll=sw_roll,
             right_elbow=r_elbow, left_elbow=l_elbow, right_knee=rknee, left_knee=lknee,
             right_shrug=rshrug, left_shrug=lshrug)
    if sw_dir is not None: d['sw_dir'] = n3(sw_dir)
    if sw_edge is not None: d['sw_edge'] = sw_edge
    rp = RF0 + np.array(rfoot); lp = LF0 + np.array(lfoot)
    d['right_foot'] = foot_lift(rp, rheel, side='Right') if rheel else rp
    d['left_foot'] = foot_lift(lp, lheel, side='Left') if lheel else lp
    d['right_foot_rot'] = (rfoot_rot[0], rfoot_rot[1] + rheel, rfoot_rot[2])
    d['left_foot_rot'] = (lfoot_rot[0], lfoot_rot[1] + lheel, lfoot_rot[2])
    return d

# Conventions (cm, degrees). +X = character left, +Z = forward, +Y = up.
# yaw about +Y: negative = turn to character's right. sw = grip (blade-base) position relative to Spine2, in torso frame.
A1 = {}
A1[0]  = mk()
A1[12] = mk(hips=(2,-3,-4), pelvis=(-6,0,0), torso=(-8,-2,0), head=(-2,0,0),
            sw=(-5,-2,40), sw_dir=(-0.15,0.95,0.20), sw_edge=(-1,0,0))
A1[28] = mk(hips=(3,-5,-8), pelvis=(-14,0,0), torso=(-26,-4,0), head=(-3,0,0),
            sw=(-9,8,28), sw_dir=(-0.30,0.88,-0.30), sw_edge=(-1,0,0))
A1[46] = mk(hips=(4,-5,-9), pelvis=(-20,0,0), torso=(-38,-6,-4), head=(-4,0,0),
            sw=(-11,18,20), sw_dir=(-0.35,0.85,-0.40), sw_edge=(-0.6,-0.6,0.2))
A1[56] = mk(hips=(1,-5,-3), pelvis=(-12,0,0), torso=(-28,-4,-3), head=(-3,0,0),
            sw=(-9,12,26), sw_dir=(-0.20,0.80,-0.10), sw_edge=(0.2,-0.7,0.4), lheel=10)
A1[63] = mk(hips=(-2,-6,5), pelvis=(4,0,0), torso=(6,4,2), head=(2,0,0),
            sw=(3,0,40), sw_dir=(0.20,0.10,0.97), sw_edge=(0.5,-0.4,0.7), lheel=14)
A1[70] = mk(hips=(-3,-6,8), pelvis=(26,0,0), torso=(34,10,6), head=(5,0,0),
            sw=(10,-16,38), sw_dir=(0.55,-0.45,0.70), sw_edge=(0.8,-0.5,0.2), lheel=14)
A1[87] = mk(hips=(-3,-8,6), pelvis=(30,0,0), torso=(40,12,8), head=(6,0,0),
            sw=(12,-28,30), sw_dir=(0.75,-0.45,0.50), sw_edge=(0.8,-0.5,0.2), lheel=10)
A1[130] = mk()
A1_NEUTRAL_MID = {100: mk(hips=(-1,-4,2), pelvis=(14,0,0), torso=(18,4,3), head=(3,0,0),
            sw=(8,-10,36), sw_dir=(0.45,0.30,0.84), sw_edge=(0.5,-0.5,0.3))}

def solve_all(poses):
    return {f: solve(p) for f, p in sorted(poses.items())}
