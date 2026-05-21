using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Serializable]
    public class AudioClipData
    {
        public string name;
        public AudioClip clip;
    }

    [Header("Audio Clips")]
    [SerializeField] private List<AudioClipData> audioClips = new();

    [Header("Settings")]
    [SerializeField, Min(1)] private int sfxSourceCount = 4;
    [SerializeField, Min(0f)] private float musicFadeTime = 1f;

    [Header("Scene Music")]
    [SerializeField] private string menuMusicClipName = "Main Menu";
    [SerializeField] private string gameplayMusicClipName = "Carry On Main Theme Mastered";

    private readonly Dictionary<string, AudioClip> clipDictionary = new(StringComparer.OrdinalIgnoreCase);
    private AudioSource[] sfxSources;
    private AudioSource musicSource;
    private Coroutine musicFadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildClipDictionary();
        CreateSources();

        SceneManager.sceneLoaded += OnSceneLoaded;
        ConfigureSceneAudio(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (Instance == this)
            Instance = null;
    }

    public void PlayMusic(string clipName)
    {
        if (!TryGetClip(clipName, out AudioClip clip))
            return;

        if (musicSource.clip == clip && musicSource.isPlaying)
            return;

        if (musicFadeRoutine != null)
            StopCoroutine(musicFadeRoutine);

        musicFadeRoutine = StartCoroutine(FadeToNewMusic(clip, musicFadeTime));
    }

    public void StopMusic(float fadeDuration = 0.75f)
    {
        if (!musicSource.isPlaying)
            return;

        if (musicFadeRoutine != null)
            StopCoroutine(musicFadeRoutine);

        musicFadeRoutine = StartCoroutine(FadeOutAndStop(fadeDuration));
    }

    public void PlaySFX(string clipName, float volume = 1f)
    {
        if (!TryGetClip(clipName, out AudioClip clip))
            return;

        AudioSource source = GetAvailableSfxSource();
        source.Stop();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.loop = false;
        source.Play();
    }

    public AudioSource PlayLoopingSFX(string clipName, float volume = 1f)
    {
        if (!TryGetClip(clipName, out AudioClip clip))
            return null;

        AudioSource source = GetAvailableSfxSource();
        source.Stop();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.loop = true;
        source.Play();
        return source;
    }

    public void StopSFX(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        source.loop = false;
        source.clip = null;
    }

    private void BuildClipDictionary()
    {
        clipDictionary.Clear();

        foreach (AudioClipData data in audioClips)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.name) || data.clip == null)
                continue;

            clipDictionary[data.name] = data.clip;
        }
    }

    private void CreateSources()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;

        sfxSources = new AudioSource[Mathf.Max(1, sfxSourceCount)];
        for (int i = 0; i < sfxSources.Length; i++)
        {
            GameObject sourceObject = new($"SFX_Source_{i}");
            sourceObject.transform.SetParent(transform);

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            sfxSources[i] = source;
        }
    }

    private bool TryGetClip(string clipName, out AudioClip clip)
    {
        clip = null;
        return !string.IsNullOrWhiteSpace(clipName) && clipDictionary.TryGetValue(clipName, out clip);
    }

    private AudioSource GetAvailableSfxSource()
    {
        foreach (AudioSource source in sfxSources)
        {
            if (!source.isPlaying)
                return source;
        }

        return sfxSources[0];
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ConfigureSceneAudio(scene);
    }

    private void ConfigureSceneAudio(Scene scene)
    {
        if (scene.path.Contains("/Scenes/Level/"))
            StopMusic(musicFadeTime);
        else
            PlayMenuMusic();
    }

    public void PlayMenuMusic() => PlayMusic(menuMusicClipName);
    public void PlayGameplayMusic() => PlayMusic(gameplayMusicClipName);

    private IEnumerator FadeToNewMusic(AudioClip newClip, float fadeTime)
    {
        if (musicSource.isPlaying)
        {
            float startVolume = musicSource.volume;
            for (float elapsed = 0f; elapsed < fadeTime; elapsed += Time.unscaledDeltaTime)
            {
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeTime);
                yield return null;
            }

            musicSource.Stop();
        }

        musicSource.clip = newClip;
        musicSource.volume = fadeTime > 0f ? 0f : 1f;
        musicSource.Play();

        if (fadeTime <= 0f)
            yield break;

        for (float elapsed = 0f; elapsed < fadeTime; elapsed += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(0f, 1f, elapsed / fadeTime);
            yield return null;
        }

        musicSource.volume = 1f;
        musicFadeRoutine = null;
    }

    private IEnumerator FadeOutAndStop(float duration)
    {
        float startVolume = musicSource.volume;

        if (duration > 0f)
        {
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }
        }

        musicSource.Stop();
        musicSource.volume = 1f;
        musicFadeRoutine = null;
    }
}
