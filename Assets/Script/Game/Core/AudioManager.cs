using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : SingletonBehaviour<AudioManager>
{
    private const string ResourcePath = "Runtime/AudioManager";

    // One row per sound. Code asks for a sound by its row name; the row's volume and pitch are
    // where the mix is tuned, so no caller ever retypes them.
    [Serializable]
    public class AudioClipData
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Each play picks a pitch between min and max, so repeats don't sound identical. " +
                 "1 and 1 plays the clip as recorded. Music ignores pitch.")]
        [Range(0.5f, 1.5f)] public float minPitch = 1f;
        [Range(0.5f, 1.5f)] public float maxPitch = 1f;

        public float PickPitch() => Mathf.Max(0.1f, UnityEngine.Random.Range(minPitch, maxPitch));
    }

    // A sound on its own AudioSource: a loop, or a long one-shot the pool could cut off or the
    // caller shapes while it plays. Its volume is rebuilt every frame from these parts.
    private class Voice
    {
        public AudioSource Source;
        public AudioClipData Row;
        public float Volume = 1f; // the caller's share, 0-1
        public float Fade = 1f;   // fade-in / fade-out envelope, 0-1
        public Coroutine Fading;
    }

    [Header("Audio Clips")]
    [SerializeField] private List<AudioClipData> audioClips = new();

    [Header("Settings")]
    [SerializeField, Min(1)] private int sfxSourceCount = 8;
    [SerializeField, Min(0f)] private float musicFadeTime = 1f;

    [Header("Scene Music")]
    [SerializeField] private string menuMusicClipName = "Menu Music";
    [SerializeField] private string gameplayMusicClipName = "Gameplay Music";
    [Tooltip("The menu track isn't cut to loop: its last sample jumps back to a loud first one and " +
             "pops. The volume dips over this many seconds before the loop point and comes back in " +
             "a quarter of that. 0 plays a plain loop, for a track made to loop.")]
    [SerializeField, Min(0f)] private float menuMusicSeamFade = 2f;

    private readonly Dictionary<string, AudioClipData> rows = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> missingClipWarnings = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Voice> voices = new();
    private AudioSource[] sfxSources;
    private AudioSource musicSource;
    private AudioClipData music;
    private float musicSeamFade;
    private float musicFade;
    private Coroutine musicFadeRoutine;
    private Coroutine sceneMusicRoutine;

    protected override bool PersistAcrossScenes => true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        AudioManager prefab = Resources.Load<AudioManager>(ResourcePath);
        if (prefab != null)
            Instantiate(prefab);
    }

    protected override void OnSingletonAwake()
    {
        BuildRowLookup();
        CreateSources();

        SceneManager.sceneLoaded += OnSceneLoaded;
        ConfigureSceneAudio(SceneManager.GetActiveScene());
    }

    protected override void OnSingletonDestroyed()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Volumes are rebuilt from the rows every frame, so a row tuned in Play mode is heard at
    // once on the music and on loops too, not only on the next one-shot.
    private void Update()
    {
        if (music != null && musicSource.isPlaying)
            musicSource.volume = music.volume * musicFade * SeamFade();

        for (int i = voices.Count - 1; i >= 0; i--)
        {
            Voice voice = voices[i];
            if (voice.Source == null)
            {
                voices.RemoveAt(i);
                continue;
            }

            // A one-shot voice cleans itself up once it has played out.
            if (!voice.Source.loop && !voice.Source.isPlaying)
            {
                Release(voice);
                continue;
            }

            voice.Source.volume = voice.Row.volume * voice.Volume * voice.Fade;
        }
    }

#if UNITY_EDITOR
    // An Inspector edit in Play mode can hand back fresh row objects, so re-point what's playing.
    private void OnValidate()
    {
        if (!Application.isPlaying || sfxSources == null)
            return;

        BuildRowLookup();
        if (music != null && rows.TryGetValue(music.name, out AudioClipData musicRow))
            music = musicRow;
        foreach (Voice voice in voices)
            if (rows.TryGetValue(voice.Row.name, out AudioClipData row))
                voice.Row = row;
    }
#endif

    public void PlayMusic(string clipName, float seamFade = 0f)
    {
        if (!TryGetRow(clipName, out AudioClipData row))
            return;

        if (music == row && musicSource.isPlaying)
            return;

        if (musicFadeRoutine != null)
            StopCoroutine(musicFadeRoutine);

        musicFadeRoutine = StartCoroutine(FadeToNewMusic(row, seamFade, musicFadeTime));
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
        if (!TryGetRow(clipName, out AudioClipData row))
            return;

        AudioSource source = GetAvailableSfxSource();
        source.Stop();
        source.clip = row.clip;
        source.volume = row.volume * Mathf.Clamp01(volume);
        source.pitch = row.PickPitch();
        source.loop = false;
        source.Play();
    }

    /// <summary>Loops a sound on its own source until <see cref="StopSFX"/>, optionally fading in.</summary>
    public AudioSource PlayLoopingSFX(string clipName, float volume = 1f, float fadeIn = 0f)
        => StartVoice(clipName, volume, loop: true, fadeIn);

    /// <summary>
    /// A one-shot on its own source: for long sounds the shared pool could cut off, or ones the
    /// caller shapes with <see cref="SetSFXVolume"/> while they play. Cleans itself up at the end.
    /// </summary>
    public AudioSource PlayDedicatedSFX(string clipName, float volume = 1f)
        => StartVoice(clipName, volume, loop: false, fadeIn: 0f);

    /// <summary>Sets a looping or dedicated sound's own volume (0-1). Its row volume still applies.</summary>
    public void SetSFXVolume(AudioSource source, float volume)
    {
        Voice voice = FindVoice(source);
        if (voice != null)
            voice.Volume = Mathf.Clamp01(volume);
    }

    /// <summary>Stops a looping or dedicated sound, optionally fading it out first.</summary>
    public void StopSFX(AudioSource source, float fadeOut = 0f)
    {
        Voice voice = FindVoice(source);
        if (voice == null)
            return;

        if (fadeOut > 0f && isActiveAndEnabled)
            FadeVoice(voice, 0f, fadeOut, release: true);
        else
            Release(voice);
    }

    private AudioSource StartVoice(string clipName, float volume, bool loop, float fadeIn)
    {
        if (!TryGetRow(clipName, out AudioClipData row))
            return null;

        GameObject sourceObject = new($"{(loop ? "Loop" : "Voice")} {row.name}");
        sourceObject.transform.SetParent(transform, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.clip = row.clip;
        source.loop = loop;
        source.pitch = row.PickPitch();

        Voice voice = new() { Source = source, Row = row, Volume = Mathf.Clamp01(volume), Fade = fadeIn > 0f ? 0f : 1f };
        source.volume = row.volume * voice.Volume * voice.Fade;
        voices.Add(voice);
        source.Play();

        if (fadeIn > 0f)
            FadeVoice(voice, 1f, fadeIn, release: false);
        return source;
    }

    private void FadeVoice(Voice voice, float target, float duration, bool release)
    {
        if (voice.Fading != null)
            StopCoroutine(voice.Fading);
        voice.Fading = StartCoroutine(FadeVoiceRoutine(voice, target, duration, release));
    }

    // Unscaled like the music fades, so a sound still fades while the game is paused.
    private IEnumerator FadeVoiceRoutine(Voice voice, float target, float duration, bool release)
    {
        float start = voice.Fade;
        for (float elapsed = 0f; elapsed < duration && voice.Source != null; elapsed += Time.unscaledDeltaTime)
        {
            voice.Fade = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }

        voice.Fade = target;
        voice.Fading = null;
        if (release)
            Release(voice);
    }

    private Voice FindVoice(AudioSource source)
    {
        if (source == null)
            return null;

        foreach (Voice voice in voices)
            if (voice.Source == source)
                return voice;
        return null;
    }

    private void Release(Voice voice)
    {
        voices.Remove(voice);
        if (voice.Source != null)
            Destroy(voice.Source.gameObject);
    }

    private void BuildRowLookup()
    {
        rows.Clear();

        foreach (AudioClipData data in audioClips)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.name) || data.clip == null)
                continue;

            rows[data.name] = data;
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

    private bool TryGetRow(string clipName, out AudioClipData row)
    {
        row = null;
        if (!string.IsNullOrWhiteSpace(clipName) && rows.TryGetValue(clipName, out row))
            return true;

        if (!string.IsNullOrWhiteSpace(clipName) && missingClipWarnings.Add(clipName))
            Debug.LogWarning($"Audio clip id '{clipName}' is not registered on {nameof(AudioManager)}.");
        return false;
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

    // Only the lobby has music of its own: stage select is ambience only, and a level's music waits
    // for its countdown (GameManager). A transition has already faded the old music out
    // (SceneLoader), so this only ever starts the lobby's.
    private void ConfigureSceneAudio(Scene scene)
    {
        if (sceneMusicRoutine != null)
            StopCoroutine(sceneMusicRoutine);

        if (scene.name == SceneNames.MainMenu)
            sceneMusicRoutine = StartCoroutine(PlayMenuMusicAfterTransition());
        else
            StopMusic(musicFadeTime);
    }

    // The lobby's music waits until the transition's clouds have parted instead of starting
    // behind them. With no transition running (the first scene, or Play pressed in a scene) it
    // starts at once.
    private IEnumerator PlayMenuMusicAfterTransition()
    {
        while (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading)
            yield return null;

        sceneMusicRoutine = null;
        PlayMenuMusic();
    }

    public void PlayMenuMusic() => PlayMusic(menuMusicClipName, menuMusicSeamFade);
    public void PlayGameplayMusic() => PlayMusic(gameplayMusicClipName);

    // Dips the volume across the loop point of a track that wasn't cut to loop, so the jump from
    // its last sample back to its first is never heard as a pop.
    private float SeamFade()
    {
        if (musicSeamFade <= 0f || musicSource.clip == null)
            return 1f;

        float time = musicSource.time;
        float untilLoop = musicSource.clip.length - time;
        return Mathf.Clamp01(Mathf.Min(untilLoop, time * 4f) / musicSeamFade);
    }

    private IEnumerator FadeToNewMusic(AudioClipData row, float seamFade, float fadeTime)
    {
        if (musicSource.isPlaying)
        {
            float startFade = musicFade;
            for (float elapsed = 0f; elapsed < fadeTime; elapsed += Time.unscaledDeltaTime)
            {
                musicFade = Mathf.Lerp(startFade, 0f, elapsed / fadeTime);
                yield return null;
            }

            musicSource.Stop();
        }

        music = row;
        musicSeamFade = seamFade;
        musicSource.clip = row.clip;
        musicFade = fadeTime > 0f ? 0f : 1f;
        musicSource.volume = row.volume * musicFade;
        musicSource.Play();

        for (float elapsed = 0f; elapsed < fadeTime; elapsed += Time.unscaledDeltaTime)
        {
            musicFade = Mathf.Lerp(0f, 1f, elapsed / fadeTime);
            yield return null;
        }

        musicFade = 1f;
        musicFadeRoutine = null;
    }

    private IEnumerator FadeOutAndStop(float duration)
    {
        float startFade = musicFade;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            musicFade = Mathf.Lerp(startFade, 0f, elapsed / duration);
            yield return null;
        }

        musicSource.Stop();
        musicFade = 0f;
        musicFadeRoutine = null;
    }
}
