using UnityEngine;

/// <summary>
/// Lightweight looping background music controller for gameplay scenes.
/// Put this on a scene object with an AudioSource and assign a loop clip.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class GameplayMusicController : MonoBehaviour
{
    [SerializeField] private AudioClip musicLoop;
    [SerializeField, Range(0f, 1f)] private float volume = 0.18f;
    [SerializeField] private bool playOnStart = true;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
        audioSource.volume = volume;

        if (musicLoop != null)
        {
            audioSource.clip = musicLoop;
        }
    }

    private void Start()
    {
        if (playOnStart && audioSource.clip != null && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        if (audioSource != null)
        {
            audioSource.volume = volume;
        }
    }
}