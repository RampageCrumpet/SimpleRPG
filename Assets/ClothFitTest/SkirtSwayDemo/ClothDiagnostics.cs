using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

// Disposable diagnostics for the hem shimmer: does it persist in a frozen pose, is it tied to the animation/physics timing, does it sit on
// UV-seam vertices? Cloth.normals jitter (deg between consecutive frames) plus, for frozen poses, how many rendered pixels change per frame.
public class ClothDiagnostics : MonoBehaviour
{
    public string outFile = "";
    int fixedSteps;
    void FixedUpdate() { fixedSteps++; }

    class V { public string name; public bool freeze, clothOn = true; public AnimatorUpdateMode mode = AnimatorUpdateMode.Normal; public float fixedDt = 1f / 60f; public bool pixels; }

    IEnumerator Start()
    {
        for (int i = 0; i < 60; i++) yield return null;
        var demo = FindFirstObjectByType<SkirtSwayDemo>();
        var anim = FindFirstObjectByType<Animator>();
        var smr = anim.GetComponentsInChildren<SkinnedMeshRenderer>().First(s => s.GetComponent<Cloth>() != null);
        var cloth = smr.GetComponent<Cloth>();
        foreach (var c in Camera.allCameras) if (c.targetTexture == null) c.enabled = false;

        // Which particles are UV-seam particles: more than one mesh vertex shares the position.
        var verts = smr.sharedMesh.vertices;
        var counts = new System.Collections.Generic.Dictionary<(int, int, int), int>();
        var order = new System.Collections.Generic.List<(int, int, int)>();
        foreach (var v in verts)
        {
            var k = (Mathf.RoundToInt(v.x * 10000f), Mathf.RoundToInt(v.y * 10000f), Mathf.RoundToInt(v.z * 10000f));
            if (!counts.ContainsKey(k)) { counts[k] = 0; order.Add(k); }
            counts[k]++;
        }
        var seam = order.Select(k => counts[k] > 1).ToArray();

        var camGo = new GameObject("DiagCam"); var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.fieldOfView = 30f;
        var rt = new RenderTexture(384, 384, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
        var tex = new Texture2D(384, 384, TextureFormat.RGB24, false);
        Vector3 target = new Vector3(0f, 0.8f, 0f);
        camGo.transform.position = target + new Vector3(0.45f, 0.1f, 0.9f); camGo.transform.LookAt(target);

        var variants = new[]
        {
            new V { name = "anim_normal_dt60" },
            new V { name = "static_cloth_on", freeze = true, pixels = true },
            new V { name = "static_cloth_off", freeze = true, clothOn = false, pixels = true },
        };

        var sb = new StringBuilder("variant,frames,fixed_steps,norm_jitter_all,norm_jitter_seam,norm_jitter_nonseam,pos_jitter_mm,changed_px_per_frame,char_px\n");
        foreach (var v in variants)
        {
            demo.enabled = true; anim.speed = 1f; cloth.enabled = true;
            Time.fixedDeltaTime = v.fixedDt; anim.updateMode = v.mode;
            demo.SendMessage("Restart");
            for (int i = 0; i < 40; i++) yield return null;
            if (v.freeze) { demo.enabled = false; anim.speed = 0f; for (int i = 0; i < 90; i++) yield return null; }
            cloth.enabled = v.clothOn;

            const int frames = 120;
            var N = new Vector3[frames][]; var P = new Vector3[frames][];
            Color32[] prev = null; long changed = 0, charPx = 0, pn = 0; fixedSteps = 0;
            for (int f = 0; f < frames; f++)
            {
                yield return new WaitForEndOfFrame();
                if (v.clothOn) { N[f] = (Vector3[])cloth.normals.Clone(); P[f] = (Vector3[])cloth.vertices.Clone(); }
                if (v.pixels)
                {
                    cam.Render();
                    RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 384, 384), 0, 0); tex.Apply(); RenderTexture.active = null;
                    var px = tex.GetPixels32();
                    if (prev != null)
                    {
                        long c = 0;
                        for (int i = 0; i < px.Length; i++)
                        {
                            int l1 = px[i].r + px[i].g + px[i].b, l0 = prev[i].r + prev[i].g + prev[i].b;
                            if (l1 < 6 && l0 < 6) continue;
                            c++; if (Mathf.Abs(l1 - l0) / 3 > 4) changed++;
                        }
                        charPx += c; pn++;
                    }
                    prev = px;
                }
            }
            double na = 0, ns = 0, nn = 0, ps = 0; long ca = 0, cs = 0, cn = 0, cp = 0;
            if (v.clothOn)
                for (int f = 1; f < frames - 1; f++)
                    for (int i = 0; i < N[f].Length; i++)
                    {
                        float a = Vector3.Angle(N[f][i], N[f + 1][i]);
                        na += a; ca++; if (seam[i]) { ns += a; cs++; } else { nn += a; cn++; }
                        ps += (P[f - 1][i] - 2f * P[f][i] + P[f + 1][i]).sqrMagnitude; cp++;
                    }
            sb.AppendLine(string.Join(",", v.name, frames, fixedSteps,
                v.clothOn ? (na / ca).ToString("F3") : "", v.clothOn ? (ns / System.Math.Max(1, cs)).ToString("F3") : "", v.clothOn ? (nn / System.Math.Max(1, cn)).ToString("F3") : "",
                v.clothOn ? (System.Math.Sqrt(ps / System.Math.Max(1, cp)) * 1000).ToString("F3") : "",
                v.pixels ? (changed / (double)System.Math.Max(1, pn)).ToString("F1") : "", v.pixels ? (charPx / System.Math.Max(1, pn)).ToString() : ""));
        }
        Time.fixedDeltaTime = 1f / 60f; anim.updateMode = AnimatorUpdateMode.Normal; demo.enabled = true; anim.speed = 1f; cloth.enabled = true;
        File.WriteAllText(outFile, sb.ToString());
    }
}
