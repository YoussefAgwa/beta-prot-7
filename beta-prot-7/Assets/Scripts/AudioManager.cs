using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public AudioSource sfxSource;
    public AudioClip flipClip;
    public AudioClip matchClip;
    public AudioClip mismatchClip;
    public AudioClip gameOverClip;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
    }

    public void PlayFlip() => PlayOneShot(flipClip);
    public void PlayMatch() => PlayOneShot(matchClip);
    public void PlayMismatch() => PlayOneShot(mismatchClip);
    public void PlayGameOver() => PlayOneShot(gameOverClip);

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip);
    }
}