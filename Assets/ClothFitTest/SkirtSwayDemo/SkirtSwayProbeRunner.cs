using System.Collections;
using System.IO;
using SimpleRPG.Appearance;
using UnityEngine;

// Disposable: runs SkirtSwayDemo's exposure probe once per configuration, one after the other, in a single Play session.
public class SkirtSwayProbeRunner : MonoBehaviour
{
    [System.Serializable]
    public class Config
    {
        public string name = "run";
        public bool asBuilt, useFlareMesh = true, useColliders = true, clothOff, signedX;
        public float pinAbove = 0.78f, allowanceScale = 1f, fitRa = 0.100f, fitRb = 0.095f;
        public Mesh flareMesh;   // overrides the template mesh when set
        public Vector3[] chainCentres; public float[] chainRadii;
        public float damping = -1f, stretching = -1f, bending = -1f, worldVelocityScale = -1f, worldAccelerationScale = -1f;
    }

    public GameObject characterPrefab; public RuntimeAnimatorController controller; public Avatar avatar; public BodyPartDefinition[] parts; public Mesh flareMesh;
    public string outRoot = "";
    public Config[] configs = new Config[0];

    IEnumerator Start()
    {
        yield return null;
        foreach (var c in configs)
        {
            string dir = Path.Combine(outRoot, c.name);
            var go = new GameObject("Probe " + c.name); go.SetActive(false);
            var d = go.AddComponent<SkirtSwayDemo>();
            d.characterPrefab = characterPrefab; d.controller = controller; d.avatar = avatar; d.parts = parts; d.flareMesh = c.flareMesh != null ? c.flareMesh : flareMesh;
            d.clothOff = c.clothOff; d.asBuilt = c.asBuilt; d.useFlareMesh = c.useFlareMesh; d.useColliders = c.useColliders; d.signedX = c.signedX; d.pinAbove = c.pinAbove; d.allowanceScale = c.allowanceScale; d.fitRa = c.fitRa; d.fitRb = c.fitRb; d.chainCentres = c.chainCentres; d.chainRadii = c.chainRadii;
            d.damping = c.damping; d.stretching = c.stretching; d.bending = c.bending; d.worldVelocityScale = c.worldVelocityScale; d.worldAccelerationScale = c.worldAccelerationScale;
            d.probe = true; d.probeDir = dir; d.doubleSided = false;
            go.SetActive(true);
            float timeout = Time.realtimeSinceStartup + 600f;
            while (!File.Exists(Path.Combine(dir, "DONE.txt")) && Time.realtimeSinceStartup < timeout) yield return null;
            Destroy(go);
            yield return null; yield return null; yield return null;
        }
        File.WriteAllText(Path.Combine(outRoot, "ALL_DONE.txt"), "done " + configs.Length);
    }
}
