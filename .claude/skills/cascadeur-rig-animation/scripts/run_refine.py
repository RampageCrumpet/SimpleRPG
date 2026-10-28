def run_refine(keys, frames, RES, prev_frame=None, iters=120, edge_frames=(56, 63, 70, 78), edge_chain=(52, 56, 63, 70, 78, 87), starts=None):
    """sequentially refine design keys; RES updated in place"""
    prev = None
    if prev_frame is not None:
        x = RES[prev_frame]['x']; d = J_key(keys[prev_frame], x, None)[1]
        prev = dict(x=x, rft=d['RightWrist']['fore_twist'], lft=d['LeftWrist']['fore_twist'])
    for f in frames:
        x0 = list(starts[f]) if starts and f in starts else list(RES[f]['x']) if f in RES else [0]*8
        tgt = edge_target(keys, list(edge_chain), f) if f in edge_frames else None
        x, j, d = refine_key(keys[f], x0, prev, tgt, iters=iters, seed=f)
        RES[f] = dict(x=x, J=j)
        prev = dict(x=x, rft=d['RightWrist']['fore_twist'], lft=d['LeftWrist']['fore_twist'])
        w1, w2 = d['RightWrist'], d['LeftWrist']
        print(f, "x", [round(v) if i < 3 else round(v, 1) for i, v in enumerate(x)], "J %.1f" % j,
              "R sw %.0f ftw %.0f el %.0f | L sw %.0f ftw %.0f el %.0f" % (w1['swing'], w1['fore_twist'], w1['elbow_bend'], w2['swing'], w2['fore_twist'], w2['elbow_bend']))
    return RES
