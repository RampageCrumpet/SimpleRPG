import zlib, struct

def _png(path, img):
    h, w, _ = img.shape
    raw = b"".join(b"\x00" + img[y].tobytes() for y in range(h))
    def ch(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    open(path, "wb").write(b"\x89PNG\r\n\x1a\n" + ch(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)) + ch(b"IDAT", zlib.compress(raw)) + ch(b"IEND", b""))

def _line(img, p, q, col, th=1):
    x0, y0 = p; x1, y1 = q
    n = int(max(abs(x1-x0), abs(y1-y0))) + 1
    for i in range(n+1):
        t = i/max(n, 1); x = int(round(x0+(x1-x0)*t)); y = int(round(y0+(y1-y0)*t))
        for dx in range(-th, th+1):
            for dy in range(-th, th+1):
                if 0 <= y+dy < img.shape[0] and 0 <= x+dx < img.shape[1]: img[y+dy, x+dx] = col

SEGS = [('Hips','Spine'),('Spine','Spine1'),('Spine1','Spine2'),('Spine2','Neck'),('Neck','Head'),
        ('Spine2','RightShoulder'),('RightShoulder','RightArm'),('RightArm','RightForeArm'),('RightForeArm','RightHand'),
        ('Spine2','LeftShoulder'),('LeftShoulder','LeftArm'),('LeftArm','LeftForeArm'),('LeftForeArm','LeftHand'),
        ('Hips','RightUpLeg'),('RightUpLeg','RightLeg'),('RightLeg','RightFoot'),('RightFoot','RightToeBase'),
        ('Hips','LeftUpLeg'),('LeftUpLeg','LeftLeg'),('LeftLeg','LeftFoot'),('LeftFoot','LeftToeBase')]

def render_png(sols, path, frames=None, S_=1.4, W=290, H=360, trail=None):
    """3 columns: front (viewer sees char-left on right), side (forward = right), top (forward = up)"""
    fr = [f for f in sols if (frames is None or f in frames)]
    img = np.full((H*len(fr), W*3, 3), 250, np.uint8)
    for r, f in enumerate(fr):
        d = sols[f]; G = d['G']
        for c, (ax, ay, ox, oy, sc) in enumerate([(0,1,145,H-30,S_),(2,1,95,H-30,S_),(0,2,145,H-100,S_*0.85)]):
            def pt(v, ax=ax, ay=ay, ox=ox, oy=oy, sc=sc):
                return (c*W + ox + v[ax]*sc, r*H + oy - v[ay]*sc)
            if ay == 1: _line(img, (c*W+5, r*H+oy), (c*W+W-5, r*H+oy), (200,200,200))
            _line(img, (c*W, r*H), (c*W, r*H+H-1), (180,180,180))
            if trail:
                pts = [t[1] for t in trail if t[0] <= f]
                for a, b in zip(pts[:-1], pts[1:]): _line(img, pt(a), pt(b), (160,200,160))
            for a, b in SEGS:
                colr = (210,40,40) if ('Right' in a or 'Right' in b) else ((40,110,210) if ('Left' in a or 'Left' in b) else (60,60,60))
                _line(img, pt(G[M+a][0]), pt(G[M+b][0]), colr, 1)
            for jn, cc in (('Head',(60,60,60)),('RightHand',(210,40,40)),('LeftHand',(40,110,210))):
                hp = pt(G[M+jn][0]); rr = 7 if jn=='Head' else 3
                for ang in range(0,360,20):
                    _line(img, (hp[0]+rr*math.cos(math.radians(ang)), hp[1]+rr*math.sin(math.radians(ang))), (hp[0]+rr*math.cos(math.radians(ang+20)), hp[1]+rr*math.sin(math.radians(ang+20))), cc, 1)
            P0, R0 = d['sword']
            _line(img, pt(P0), pt(d['tip']), (230,160,0), 1)
            e = qrot(R0, [-14., 0, 0])
            _line(img, pt(P0+e), pt(P0-e), (150,90,0), 1)
            m = P0 + qrot(R0, [-6., 0, -35.])
            _line(img, pt(m), pt(m+0.01), (0,170,0), 3)   # green dot = true-edge side of blade
    _png(path, img)
