using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

// Disposable: with the demo's real idle animation (restarted for every variant, so each sees the same motion) and a fixed camera, measures how much the
// hauberk's pixels change from frame to frame with the Cloth on or off and with material changes (on copies; production assets untouched).
public class ShimmerTest : MonoBehaviour
{
    public string outFile = "";

    class Variant { public string name; public bool cloth = true; public System.Action<Material> mat; }

    IEnumerator Start()
    {
        for (int i = 0; i < 60; i++) yield return null;
        var demo = FindFirstObjectByType<SkirtSwayDemo>();
        var anim = FindFirstObjectByType<Animator>();
        var smr = anim.GetComponentsInChildren<SkinnedMeshRenderer>().First(s => s.GetComponent<Cloth>() != null);
        var cloth = smr.GetComponent<Cloth>();
        foreach (var c in Camera.allCameras) if (c.targetTexture == null) c.enabled = false;
        var orig = smr.sharedMaterial;

        var root = anim.transform;
        var camGo = new GameObject("ShimmerCam"); var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.fieldOfView = 30f;
        var rt = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
        var tex = new Texture2D(768, 768, TextureFormat.RGB24, false);
        Vector3 target = new Vector3(0f, 0.8f, 0f);
        camGo.transform.position = target + new Vector3(0.45f, 0.1f, 0.9f); camGo.transform.LookAt(target);

        var variants = new[]
        {
            new Variant { name = "0a_cloth_on" },
            new Variant { name = "0b_cloth_on_again" },
            new Variant { name = "1_cloth_off", cloth = false },
            new Variant { name = "2_cloth_on_no_normal_map", mat = x => { x.SetTexture("_BumpMap", null); x.DisableKeyword("_NORMALMAP"); } },
            new Variant { name = "3_cloth_on_specular_off", mat = x => { x.SetFloat("_SpecularHighlights", 0f); x.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); } },
            new Variant { name = "4_cloth_on_no_normal_map_spec_off", mat = x => { x.SetTexture("_BumpMap", null); x.DisableKeyword("_NORMALMAP"); x.SetFloat("_SpecularHighlights", 0f); x.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); } },
            new Variant { name = "5_cloth_on_smoothness_0.5_metal_0.5", mat = x => { x.SetFloat("_Smoothness", 0.5f); x.SetFloat("_Metallic", 0.5f); } },
            new Variant { name = "6_cloth_on_last", },
        };

        var sb = new StringBuilder("variant,mean_abs_diff_per_char_px,frac_px_diff_gt8,char_px\n");
        foreach (var v in variants)
        {
            var m = new Material(orig); smr.sharedMaterial = m; v.mat?.Invoke(m);
            cloth.enabled = v.cloth;
            demo.SendMessage("Restart");
            for (int i = 0; i < 30; i++) yield return null;
            Color32[] prev = null; double sum = 0; long gt8 = 0, charPx = 0, n = 0;
            for (int f = 0; f < 60; f++)
            {
                yield return new WaitForEndOfFrame();
                cam.Render();
                RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 768, 768), 0, 0); tex.Apply(); RenderTexture.active = null;
                var px = tex.GetPixels32();
                if (prev != null)
                {
                    for (int i = 0; i < px.Length; i++)
                    {
                        int l1 = px[i].r + px[i].g + px[i].b, l0 = prev[i].r + prev[i].g + prev[i].b;
                        if (l1 < 6 && l0 < 6) continue;
                        charPx++; int d = Mathf.Abs(l1 - l0) / 3; sum += d; if (d > 8) gt8++;
                    }
                    n++;
                }
                prev = px;
                if (f == 30) File.WriteAllBytes(Path.ChangeExtension(outFile, null) + "_" + v.name + ".png", tex.EncodeToPNG());
            }
            sb.AppendLine(v.name + "," + (sum / System.Math.Max(1, charPx)).ToString("F3") + "," + ((double)gt8 / System.Math.Max(1, charPx)).ToString("F4") + "," + (charPx / System.Math.Max(1, n)));
        }
        smr.sharedMaterial = orig; cloth.enabled = true;
        File.WriteAllText(outFile, sb.ToString());
    }
}
