using UnityEngine;

/// <summary>
/// Plays the audio clip an animation event names, so a clip carries its own sounds and the same component serves every character. Set the
/// event's function to <c>PlayClip</c> and its Object parameter to the <see cref="AudioClip"/>. The clip plays on this object's
/// <see cref="AudioSource"/>, whose volume and 3D settings set how loud and how far it is heard.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public sealed class AnimationAudioEvents : MonoBehaviour
{
    private AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
    }

    /// <summary>
    /// Animation event receiver.
    /// </summary>
    /// <param name="clip">The event's Object parameter; anything that is not an <see cref="AudioClip"/> is ignored.</param>
    public void PlayClip(Object clip)
    {
        if (clip is AudioClip audioClip)
        {
            source.PlayOneShot(audioClip);
        }
    }
}
