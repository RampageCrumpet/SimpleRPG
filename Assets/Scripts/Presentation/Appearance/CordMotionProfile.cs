using System;
using UnityEngine;

namespace SimpleRPG.Appearance
{
    /// <summary>Item-local accessory bones and their skin; shared body bind poses remain canonical.</summary>
    public sealed class CordMotionProfile : ScriptableObject
    {
        [Serializable]
        public struct Chain
        {
            public string name;
            public string attachment;
            public bool waist;
            public Vector3[] points; // In the attachment bone's rest-local space; last point is a tip, not another bone.
            public Vector3[] normals; // Local outward garment normals constrain inward motion without physics colliders.
        }
        /// <summary>A capsule fixed to a rig bone that simulated points may not enter (the thigh under a hanging skirt). Analytic: no physics components.</summary>
        [Serializable]
        public struct Blocker
        {
            public string bone;
            public Vector3 start; // In the bone's local space.
            public Vector3 end;
            public float radius;
        }

        [SerializeField] private float gravityScale;
        /// <summary>How strongly hanging chains settle toward straight down; zero (an older asset) means 1.</summary>
        public float GravityScale => gravityScale > 0f ? gravityScale : 1f;
        [SerializeField] private Blocker[] blockers = Array.Empty<Blocker>();
        public Blocker[] Blockers => blockers ?? Array.Empty<Blocker>();
        [SerializeField] private Mesh source;
        [SerializeField] private Mesh visibleMesh;
        [SerializeField] private Mesh hiddenWaistMesh;
        [SerializeField] private Chain[] chains = Array.Empty<Chain>();
        public Mesh Source => source;
        public Mesh VisibleMesh => visibleMesh;
        public Mesh HiddenWaistMesh => hiddenWaistMesh;
        public Chain[] Chains => chains;
#if UNITY_EDITOR
        public void Configure(Mesh original, Mesh visible, Mesh hidden, Chain[] settings)
        { source = original; visibleMesh = visible; hiddenWaistMesh = hidden; chains = settings; }
        public void SetBlockers(Blocker[] settings) => blockers = settings;
        public void SetGravityScale(float scale) => gravityScale = scale;
#endif
    }
}
