using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// A ground or object material as heard, kept apart from any visual (PBR) material. Footsteps, impacts and drops resolve against it.
    /// </summary>
    [CreateAssetMenu(fileName = "AcousticSurface", menuName = "Audio/Acoustic Surface")]
    public sealed class AcousticSurface : AudioTag
    {
    }
}
