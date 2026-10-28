# Attack 1 v3 design poses. torso=(yaw,pitch,roll): yaw<0 = turn right; pitch<0 = lean back; roll>0 = lean toward character's RIGHT.
# Charge leans back + right (counter to the left-down swing); the cut then folds forward + left.
COMBO0 = 140
D = {}
D[0]  = mk()
D[14] = mk(hips=(2,-3,-3), pelvis=(-5,0,0), torso=(-7,-3,3), head=(0,0,0),
           sw=(-3,-2,48), sw_dir=(-0.12,0.97,0.10))
D[28] = mk(hips=(3,-4,-6), pelvis=(-11,0,0), torso=(-20,-6,8), head=(0,0,0),
           sw=(-7,8,44), sw_dir=(-0.22,0.93,-0.06))
D[42] = mk(hips=(5,-5,-8), pelvis=(-17,0,0), torso=(-32,-9,12), head=(0,0,0),
           sw=(-10,17,40), sw_dir=(-0.34,0.86,-0.32))
D[52] = mk(hips=(6,-5,-9), pelvis=(-21,0,0), torso=(-38,-12,15), head=(0,0,0),
           sw=(-11,22,36), sw_dir=(-0.40,0.80,-0.45))
D[56] = mk(hips=(2,-5,-3), pelvis=(-12,0,0), torso=(-28,-7,8), head=(0,0,0),
           sw=(-9,16,40), sw_dir=(-0.20,0.80,-0.10), sw_edge=(0.0,-0.5,0.85), lheel=10)
D[63] = mk(hips=(-2,-6,5), pelvis=(4,0,0), torso=(6,3,-3), head=(0,0,0),
           sw=(2,4,56), sw_dir=(0.20,0.10,0.97), sw_edge=(0.45,-0.55,0.7), lheel=14)
D[70] = mk(hips=(-3,-6,8), pelvis=(22,0,0), torso=(28,9,-7), head=(2,0,0),
           sw=(9,-6,52), sw_dir=(0.42,-0.42,0.80), sw_edge=(0.7,-0.5,0.5), lheel=14)
D_NEUTRAL = {
    # overswing: blade carries past the target, torso fully wound to the left, weight sinks onto the front leg
    84: mk(hips=(-4,-9,6), pelvis=(34,0,0), torso=(46,14,-12), head=(3,0,0),
           sw=(15,-22,34), sw_dir=(0.80,-0.50,0.33), lheel=12),
    # catch: arrest the swing, start pulling the blade back across the body
    98: mk(hips=(-2,-5,3), pelvis=(18,0,0), torso=(24,8,-4), head=(1,0,0),
           sw=(8,-12,40), sw_dir=(0.50,0.35,0.78), lheel=6),
    # rebound: blade rises slightly past Ready toward the right as the body re-centres
    112: mk(hips=(0,0.8,0.5), pelvis=(-3,0,0), torso=(-5,-2,2), head=(0,0,0),
            sw=(0,-9,47), sw_dir=(-0.30,0.94,0.14), lheel=1),
    # settle: small dip and forward sag of the tip
    122: mk(hips=(0,-1.2,0), pelvis=(-1,0,0), torso=(-1,1,0), head=(0,0,0),
            sw=(1,-13,50.5), sw_dir=(-0.15,0.90,0.40)),
    130: mk()}
D_COMBO = {78: mk(hips=(-3,-7,7), pelvis=(28,0,0), torso=(37,11,-9), head=(2,0,0),
                  sw=(13,-16,44), sw_dir=(0.65,-0.45,0.62), sw_edge=(0.7,-0.5,0.5), lheel=12),
           87: mk(hips=(-3,-8,6), pelvis=(30,0,0), torso=(40,12,-10), head=(3,0,0),
                  sw=(13,-22,40), sw_dir=(0.75,-0.45,0.50), sw_edge=(0.7,-0.5,0.5), lheel=10)}
