using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

// Disposable: with the demo running (idle then walk), records the hauberk's vertex positions and normals every frame with the Cloth on
// (Cloth.vertices / Cloth.normals) and then with the Cloth off (BakeMesh), and reports high-frequency jitter per region:
// position jitter = RMS of the second difference (mm), normal jitter = mean angle between consecutive frames (deg).
public class ClothJitterTest : MonoBehaviour
{
    public string outFile = "";
    public int frames = 300;
    SkinnedMeshRenderer smr; Cloth cloth; Mesh bake;

    IEnumerator Start()
    {
        for (int i = 0; i < 40; i++) yield return null;
        smr = FindFirstObjectByType<Animator>().GetComponentsInChildren<SkinnedMeshRenderer>().First(s => s.GetComponent<Cloth>() != null);
        cloth = smr.GetComponent<Cloth>(); bake = new Mesh();
        var demo = FindFirstObjectByType<SkirtSwayDemo>();
        var coef = cloth.coefficients;
        var sb = new StringBuilder("mode,phase,region,n,pos_jitter_mm,normal_jitter_deg\n");

        for (int pass = 0; pass < 2; pass++)
        {
            bool on = pass == 0;
            cloth.enabled = on;
            demo.SendMessage("Restart");
            for (int i = 0; i < 20; i++) yield return null;
            var P = new System.Collections.Generic.List<Vector3[]>(); var N = new System.Collections.Generic.List<Vector3[]>();
            for (int f = 0; f < frames; f++)
            {
                yield return new WaitForEndOfFrame();
                if (on) { P.Add((Vector3[])cloth.vertices.Clone()); N.Add((Vector3[])cloth.normals.Clone()); }
                else { smr.BakeMesh(bake, true); P.Add(bake.vertices); N.Add(bake.normals); }
            }
            int n = P[0].Length;
            // regions by object-space position in the first frame
            var rest = P[0];
            string Region(int i)
            {
                var v = rest[i];
                if (Mathf.Abs(v.x) > 0.22f && v.y > 1.05f) return "sleeve";
                if (v.y < 0.93f) return "skirt";
                return "torso";
            }
            var reg = Enumerable.Range(0, n).Select(Region).ToArray();
            foreach (var r in new[] { "sleeve", "skirt", "torso" })
            foreach (var (name, a, b) in new[] { ("idle", 10, 100), ("walk", 150, frames - 2) })
            {
                double ps = 0, ns = 0; long cnt = 0, ncnt = 0;
                for (int f = a; f < b; f++)
                    for (int i = 0; i < n; i++)
                    {
                        if (reg[i] != r) continue;
                        var d2 = P[f - 1][i] - 2f * P[f][i] + P[f + 1][i];
                        ps += d2.sqrMagnitude; cnt++;
                        ns += Vector3.Angle(N[f][i], N[f + 1][i]); ncnt++;
                    }
                sb.AppendLine((on ? "cloth_on" : "cloth_off") + "," + name + "," + r + "," + (cnt / (b - a)) + "," + (System.Math.Sqrt(ps / System.Math.Max(1, cnt)) * 1000).ToString("F3") + "," + (ns / System.Math.Max(1, ncnt)).ToString("F3"));
            }
        }
        File.WriteAllText(outFile, sb.ToString());
    }
}
