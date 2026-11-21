using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

// Disposable: Cloth-on normal/position jitter of the skirt by height band, for a set of Cloth settings, to find what drives the hem shimmer.
public class ClothJitterVariants : MonoBehaviour
{
    public string outFile = "";
    public int frames = 240;

    class V { public string name; public System.Action<Cloth> apply; }

    IEnumerator Start()
    {
        for (int i = 0; i < 40; i++) yield return null;
        var smr = FindFirstObjectByType<Animator>().GetComponentsInChildren<SkinnedMeshRenderer>().First(s => s.GetComponent<Cloth>() != null);
        var cloth = smr.GetComponent<Cloth>();
        var demo = FindFirstObjectByType<SkirtSwayDemo>();
        var sph = cloth.sphereColliders; var cap = cloth.capsuleColliders;
        float damp = cloth.damping, stretch = cloth.stretchingStiffness, bend = cloth.bendingStiffness, freq = cloth.clothSolverFrequency, fr = cloth.friction;
        float cm = cloth.collisionMassScale;
        var variants = new[]
        {
            new V { name = "as_built", apply = c => { } },
            new V { name = "as_built_again", apply = c => { } },
            new V { name = "no_colliders", apply = c => { c.sphereColliders = new ClothSphereColliderPair[0]; c.capsuleColliders = new CapsuleCollider[0]; } },
            new V { name = "damping_0.1", apply = c => c.damping = 0.1f },
            new V { name = "damping_0.3", apply = c => c.damping = 0.3f },
            new V { name = "friction_0", apply = c => c.friction = 0f },
            new V { name = "stretch_1_bend_1", apply = c => { c.stretchingStiffness = 1f; c.bendingStiffness = 1f; } },
            new V { name = "freq_240", apply = c => c.clothSolverFrequency = 240f },
            new V { name = "world_vel0_acc0", apply = c => { c.worldVelocityScale = 0f; c.worldAccelerationScale = 0f; } },
            new V { name = "gravity_off", apply = c => c.useGravity = false },
        };
        float wv = cloth.worldVelocityScale, wa = cloth.worldAccelerationScale; bool grav = cloth.useGravity;
        var sb = new StringBuilder("variant,phase,band,n,pos_jitter_mm,normal_jitter_deg\n");
        foreach (var v in variants)
        {
            cloth.sphereColliders = sph; cloth.capsuleColliders = cap; cloth.damping = damp; cloth.stretchingStiffness = stretch; cloth.bendingStiffness = bend;
            cloth.clothSolverFrequency = freq; cloth.friction = fr; cloth.worldVelocityScale = wv; cloth.worldAccelerationScale = wa; cloth.useGravity = grav;
            v.apply(cloth);
            demo.SendMessage("Restart");
            for (int i = 0; i < 20; i++) yield return null;
            var P = new System.Collections.Generic.List<Vector3[]>(); var N = new System.Collections.Generic.List<Vector3[]>();
            for (int f = 0; f < frames; f++)
            {
                yield return new WaitForEndOfFrame();
                P.Add((Vector3[])cloth.vertices.Clone()); N.Add((Vector3[])cloth.normals.Clone());
            }
            var rest = P[0]; int n = rest.Length;
            string Band(int i) { float y = rest[i].y; return y < 0.70f ? "hem" : y < 0.77f ? "mid" : "top"; }
            var band = Enumerable.Range(0, n).Select(Band).ToArray();
            foreach (var b in new[] { "hem", "mid", "top" })
            foreach (var (name, a, e) in new[] { ("idle", 10, 100), ("walk", 150, frames - 2) })
            {
                double ps = 0, ns = 0; long cnt = 0;
                for (int f = a; f < e; f++)
                    for (int i = 0; i < n; i++)
                    {
                        if (band[i] != b) continue;
                        var d2 = P[f - 1][i] - 2f * P[f][i] + P[f + 1][i];
                        ps += d2.sqrMagnitude; cnt++;
                        ns += Vector3.Angle(N[f][i], N[f + 1][i]);
                    }
                sb.AppendLine(v.name + "," + name + "," + b + "," + (cnt / (e - a)) + "," + (System.Math.Sqrt(ps / System.Math.Max(1, cnt)) * 1000).ToString("F3") + "," + (ns / System.Math.Max(1, cnt)).ToString("F3"));
            }
        }
        File.WriteAllText(outFile, sb.ToString());
    }
}
