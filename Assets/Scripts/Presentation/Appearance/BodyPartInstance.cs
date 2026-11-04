using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Appearance
{
    /// <summary>
    /// The built, on-character form of one <see cref="BodyPartDefinition"/>: everything <see cref="BodyPartBinder"/> made for it. The root object
    /// holds skinned pieces; rigid pieces live under their bones, outside the root, so this component remembers them, answers for all of the
    /// part's renderers, and removes them when the part is removed.
    /// </summary>
    [ExecuteAlways]
    public sealed class BodyPartInstance : MonoBehaviour
    {
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<GameObject> attached = new List<GameObject>();

        /// <summary>Every renderer of every piece of the part, wherever the piece is parented.</summary>
        public IReadOnlyList<Renderer> Renderers => renderers;

        internal void Add(Renderer renderer, bool parentedElsewhere)
        {
            renderers.Add(renderer);
            if (parentedElsewhere) attached.Add(renderer.gameObject);
        }

        /// <summary>Applies hide flags to the root and every piece (editor previews use this so nothing is saved).</summary>
        public void SetHideFlags(HideFlags flags)
        {
            gameObject.hideFlags = flags;
            foreach (GameObject go in attached) go.hideFlags = flags;
        }

        /// <summary>Removes the whole part: the root and every piece parented elsewhere.</summary>
        public void Remove()
        {
            foreach (GameObject go in attached)
            {
                if (go == null) continue;
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }

            attached.Clear();
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private void OnDestroy()
        {
            // Destroyed some other way at runtime (the character going away): take the pieces on bones with it. In the editor the hierarchy
            // owns them and destroying them here would destroy them twice; use Remove.
            if (!Application.isPlaying) return;
            foreach (GameObject go in attached)
            {
                if (go != null) Destroy(go);
            }
        }
    }
}
