# Metrics for a frame -> vector dict (solver-side).  Needs session6 + retime9.  Prints tip path cleanliness, flat-leading, limit and elbow numbers.
def metrics9(V, frames, grip, label="", true_until=84):
    S = {f: (lambda p: solve(p))(dict(v2p(np.array(V[f], float)), twist_share=0.0, **grip)) for f in range(frames[0]-1, frames[-1]+2)}
    tips = {f: S[f]['tip'] for f in S}
    out = {}
    sp = {f: float(np.linalg.norm(tips[f+1]-tips[f]))*0.6 for f in frames}                  # m/s (step f -> f+1)
    turn = {}
    for f in frames[1:]:
        a = tips[f]-tips[f-1]; b = tips[f+1]-tips[f]
        if np.linalg.norm(a) > 5 and np.linalg.norm(b) > 5:
            turn[f] = math.degrees(math.acos(max(-1, min(1, float(np.dot(a, b)/np.linalg.norm(a)/np.linalg.norm(b))))))
    flat = []
    for f in frames:
        tv = tips[f+1]-tips[f-1]; spd = float(np.linalg.norm(tv))/2*0.6
        if spd > 3.0:
            a = edge_angle(S[f], tv); a2 = a if f <= true_until else min(a, 180-a)
            if (f <= true_until and a > 35) or (f > true_until and a2 > 55): flat.append((f, int(round(a)), round(spd, 1)))
    viol = []
    for f in frames:
        d = S[f]
        for nm in ('Right', 'Left'):
            w = d[nm+'Wrist']; tw = w['hand_twist']-TWIST_REF[nm]
            if w['dev'] < -LIM_HARD['uln'] or w['dev'] > LIM_HARD['rad'] or abs(w['flex']) > LIM_HARD['flex'] or abs(tw) > LIM_HARD['twist']:
                viol.append((f, nm[0], int(round(w['flex'])), int(round(w['dev'])), int(round(tw))))
    e = {nm: np.array([S[f][nm+'Wrist']['elbow_pos'] for f in frames]) for nm in ('Right', 'Left')}
    esp = {nm: np.linalg.norm(np.diff(e[nm], axis=0), axis=1) for nm in e}
    rel = {nm: [float(S[f][nm+'Wrist']['elbow_pos'][1]-S[f]['G'][M+nm+'Arm'][0][1]) for f in frames] for nm in ('Right', 'Left')}
    print(label, "frames", frames[0], "-", frames[-1])
    print("  tip speed m/s:", [round(sp[f], 1) for f in frames])
    print("  max tip direction change deg/frame (speed>5cm/f):", max(turn.items(), key=lambda kv: kv[1]) if turn else None, " count >25:", sum(1 for t in turn.values() if t > 25))
    print("  flat-leading frames (frame, angle, m/s):", flat)
    print("  beyond hard limits (frame, side, flex, dev, twist):", viol)
    print("  left twist vs Ready:", [int(round(S[f]['LeftWrist']['hand_twist']-TWIST_REF['Left'])) for f in frames[::3]], " right:", [int(round(S[f]['RightWrist']['hand_twist']-TWIST_REF['Right'])) for f in frames[::3]])
    print("  elbow height rel shoulder R:", [int(round(x)) for x in rel['Right'][::3]], " L:", [int(round(x)) for x in rel['Left'][::3]])
    print("  elbow speed max cm/frame R %.1f L %.1f; min clearance R elbow %.1f L elbow %.1f L fore %.1f" % (esp['Right'].max(), esp['Left'].max(),
          min(S[f]['clear']['Right']['elbow'] for f in frames), min(S[f]['clear']['Left']['elbow'] for f in frames), min(S[f]['clear']['Left']['fore'] for f in frames)))
    return S
