import json, math, os
import numpy as np

SP = globals().get("SP") or (os.getcwd().replace(chr(92), "/") + "/")   # work dir holding skel.json / points.json and these scripts (set SP before exec)
S = json.load(open(SP + "skel.json"))

def qm(a, b):
    w1, x1, y1, z1 = a; w2, x2, y2, z2 = b
    return np.array([w1*w2-x1*x2-y1*y2-z1*z2, w1*x2+x1*w2+y1*z2-z1*y2,
                     w1*y2-x1*z2+y1*w2+z1*x2, w1*z2+x1*y2-y1*x2+z1*w2])

def qinv(q):
    return np.array([q[0], -q[1], -q[2], -q[3]]) / np.dot(q, q)

def qrot(q, v):
    u = np.array(q[1:]); v = np.asarray(v, float)
    return v + 2*np.cross(u, np.cross(u, v) + q[0]*v)

def qaa(axis, ang):
    a = np.asarray(axis, float); a = a/np.linalg.norm(a)
    return np.array([math.cos(ang/2), *(a*math.sin(ang/2))])

def qnorm(q):
    return q/np.linalg.norm(q)

def qslerp(a, b, t):
    a = np.asarray(a, float); b = np.asarray(b, float)
    d = np.dot(a, b)
    if d < 0: b = -b; d = -d
    if d > 0.9995: return qnorm(a + t*(b-a))
    th = math.acos(d)
    return (math.sin((1-t)*th)*a + math.sin(t*th)*b)/math.sin(th)

def q2m(q):
    w, x, y, z = q
    return np.array([[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
                     [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
                     [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]])

def m2q(m):
    t = m[0,0]+m[1,1]+m[2,2]
    if t > 0:
        s = math.sqrt(t+1)*2
        q = [s/4, (m[2,1]-m[1,2])/s, (m[0,2]-m[2,0])/s, (m[1,0]-m[0,1])/s]
    elif m[0,0] > m[1,1] and m[0,0] > m[2,2]:
        s = math.sqrt(1+m[0,0]-m[1,1]-m[2,2])*2
        q = [(m[2,1]-m[1,2])/s, s/4, (m[0,1]+m[1,0])/s, (m[0,2]+m[2,0])/s]
    elif m[1,1] > m[2,2]:
        s = math.sqrt(1+m[1,1]-m[0,0]-m[2,2])*2
        q = [(m[0,2]-m[2,0])/s, (m[0,1]+m[1,0])/s, s/4, (m[1,2]+m[2,1])/s]
    else:
        s = math.sqrt(1+m[2,2]-m[0,0]-m[1,1])*2
        q = [(m[1,0]-m[0,1])/s, (m[0,2]+m[2,0])/s, (m[1,2]+m[2,1])/s, s/4]
    return qnorm(np.array(q))

def frame_q(local_vecs, global_vecs):
    """rotation taking orthonormal-ized local basis (a,b) to global (A,B)"""
    def basis(a, b):
        a = np.asarray(a, float); a = a/np.linalg.norm(a)
        b = np.asarray(b, float); b = b - a*np.dot(a, b); b = b/np.linalg.norm(b)
        return np.stack([a, b, np.cross(a, b)], axis=1)
    return m2q(basis(*global_vecs) @ basis(*local_vecs).T)

PARENT = {n: S[n]['parent'] for n in S}
LP = {n: np.array(S[n]['lp']) for n in S}
LQ = {n: np.array(S[n]['lq']) for n in S}
ORDER = []
def _order(n):
    if n in ORDER: return
    if PARENT[n]: _order(PARENT[n])
    ORDER.append(n)
for _n in S: _order(_n)

def fk(lq, hips_pos, skip_root=('Longsword_01','Longsword_02')):
    """lq: dict name->local quat. returns dict name->(pos,quat) globals"""
    G = {}
    for n in ORDER:
        if n in skip_root: continue
        p = PARENT[n]
        if p is None:
            G[n] = (np.array(hips_pos, float), np.array(lq[n]))
        else:
            pp, pq = G[p]
            G[n] = (pp + qrot(pq, LP[n]), qm(pq, lq[n]))
    return G
