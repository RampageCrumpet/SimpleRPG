NAMES3 = ['hips', 'pelvis', 'torso', 'head', 'sw_off', 'sw_dir', 'right_foot', 'left_foot', 'right_foot_rot', 'left_foot_rot']

def p2v(p):
    v = []
    for n in NAMES3:
        v += list(np.asarray(p.get(n, (0, 0, 0)) if n != 'sw_dir' else p.get('sw_dir', D0), float))
    v += [float(p.get('sw_roll', 0)), float(p.get('right_swivel', 0)), float(p.get('left_swivel', 0))]
    return np.array(v)

def v2p(v):
    p = {}
    for i, n in enumerate(NAMES3):
        p[n] = np.array(v[3*i:3*i+3])
    k = 3*len(NAMES3)
    p['sw_dir'] = p['sw_dir']/np.linalg.norm(p['sw_dir'])
    p['sw_roll'] = v[k]; p['right_swivel'] = v[k+1]; p['left_swivel'] = v[k+2]
    for n in ('right_elbow', 'left_elbow', 'right_knee', 'left_knee', 'right_shrug', 'left_shrug'): p[n] = (0, 0, 0)
    return p

def hermite_curve(times, vecs, m0=None, mN=None, tan_scale=1.0):
    T = list(times); V = [np.asarray(v, float) for v in vecs]
    n = len(T); M = []
    for i in range(n):
        if i == 0: M.append(np.zeros_like(V[0]) if m0 is None else m0)
        elif i == n-1: M.append(np.zeros_like(V[0]) if mN is None else mN)
        else: M.append(tan_scale*(V[i+1]-V[i-1])/(T[i+1]-T[i-1]))
    def f(t):
        if t <= T[0]: return V[0]
        if t >= T[-1]: return V[-1]
        j = max(i for i in range(n-1) if T[i] <= t)
        h = T[j+1]-T[j]; s = (t-T[j])/h
        h00 = 2*s**3-3*s**2+1; h10 = s**3-2*s**2+s; h01 = -2*s**3+3*s**2; h11 = s**3-s**2
        return h00*V[j] + h10*h*M[j] + h01*V[j+1] + h11*h*M[j+1]
    return f

def build_chain(design, order, res, end_tan=None):
    """design: frame->params; res: frame->{'x':[...]}; returns curve over frames in order"""
    ps = {f: apply_x(design[f], res[f]['x']) for f in order}
    vs = [p2v(ps[f]) for f in order]
    # unwrap swivels
    k = 3*len(NAMES3)
    for idx in (k+1, k+2):
        arr = np.unwrap(np.radians([v[idx] for v in vs]))
        for v, a in zip(vs, np.degrees(arr)): v[idx] = a
    return vs

def make_curves(design, res):
    out = {}
    nchain = [0, 14, 28, 42, 52, 56, 63, 70, 84, 98, 112, 122, 130]
    cchain = [0, 14, 28, 42, 52, 56, 63, 70, 78, 87]
    for name, chain, last_mode in (('neutral', nchain, 'zero'), ('combo', cchain, 'slow')):
        vs = build_chain(design, chain, res)
        i70 = chain.index(70); i63 = chain.index(63)
        m70 = 0.8*(vs[i70]-vs[i63])/7.0
        # custom tangents: shared at F70 for both branches
        T = chain; V = vs; n = len(T); M = []
        for i in range(n):
            if i == 0: M.append(np.zeros_like(V[0]))
            elif i == n-1:
                M.append(np.zeros_like(V[0]) if last_mode == 'zero' else 0.6*(V[-1]-V[-2])/(T[-1]-T[-2]))
            elif T[i] == 70: M.append(m70)
            else: M.append((V[i+1]-V[i-1])/(T[i+1]-T[i-1]))
        def make(T=T, V=V, M=M):
            def f(t):
                if t <= T[0]: return V[0]
                if t >= T[-1]: return V[-1]
                j = max(i for i in range(len(T)-1) if T[i] <= t)
                h = T[j+1]-T[j]; s = (t-T[j])/h
                return (2*s**3-3*s**2+1)*V[j] + (s**3-2*s**2+s)*h*M[j] + (-2*s**3+3*s**2)*V[j+1] + (s**3-s**2)*h*M[j+1]
            return f
        out[name] = make()
    return out
