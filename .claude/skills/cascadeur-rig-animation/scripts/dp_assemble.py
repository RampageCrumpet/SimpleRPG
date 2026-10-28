# Run the roll/swivel DP over (possibly sub-sampled) grid tables and assemble full per-frame vectors: interpolate the missing frames, restore exact
# start/end roll+swivel values with a short blend, then smooth the three columns.  Needs session6 + gridopt + gridopt9 loaded.
def dp_assemble(B, tabs, rolls, f_first, f_last, start_exact=None, end_exact=None, lam_r=0.004, lam_s=0.03, max_step=30, blend=10, passes=6, pin=()):
    frames = sorted(f for f in tabs if f_first <= f <= f_last)
    path = dp_path(tabs, frames, rolls=rolls, svs=SVS9, lam_r=lam_r, lam_s=lam_s, max_step=max_step)
    rr = np.array([B[f][K9] + path[f]['roll'] for f in frames])
    sR = np.unwrap(np.radians([path[f]['svR'] for f in frames])); sL = np.unwrap(np.radians([path[f]['svL'] for f in frames]))
    allf = np.arange(f_first, f_last+1); V = {f: np.array(B[f], float) for f in B}
    cr = np.interp(allf, frames, rr); cR = np.degrees(np.interp(allf, frames, sR)); cL = np.degrees(np.interp(allf, frames, sL))
    for i, f in enumerate(allf): V[f][K9] = cr[i]; V[f][K9+1] = cR[i]; V[f][K9+2] = cL[i]
    if start_exact is not None:
        d0 = np.array(start_exact, float) - V[f_first][K9:K9+3]
        for f in range(f_first, f_first+blend+1): V[f][K9:K9+3] += max(0.0, 1 - (f-f_first)/float(blend))*d0
    if end_exact is not None:
        d1 = np.array(end_exact, float) - V[f_last][K9:K9+3]
        for f in range(f_last-blend, f_last+1): V[f][K9:K9+3] += max(0.0, 1 - (f_last-f)/float(blend))*d1
    for _ in range(passes):
        W = {f: v.copy() for f, v in V.items()}
        for f in range(f_first+1, f_last):
            if f in pin: continue
            for c in (K9, K9+1, K9+2): W[f][c] = 0.25*V[f-1][c] + 0.5*V[f][c] + 0.25*V[f+1][c]
        V = W
    return V, path
