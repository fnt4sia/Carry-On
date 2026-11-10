using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The terminal PA heard in the main menu: every so often a chime, an announcement, and the
// closing chime, like a departures hall. Only the timing lives here. Each sound's volume and
// pitch — the announcement shifts its pitch a little every time — is its row on the
// AudioManager prefab.
public class MenuAnnouncements : MonoBehaviour
{
    [Tooltip("Quiet seconds between one announcement ending and the next starting, min and max.")]
    [SerializeField] private Vector2 gapSeconds = new(15f, 20f);
    [Tooltip("Seconds into the opening chime when the voice starts. The chime's tone is done by " +
             "about 2.5 s; its tail rings out under the voice.")]
    [SerializeField, Min(0f)] private float voiceAfterChime = 2.8f;
    [Tooltip("Seconds the PA fades out over when the menu is left mid-announcement.")]
    [SerializeField, Min(0f)] private float leaveFade = 0.5f;

    private readonly List<AudioSource> playing = new();

    private IEnumerator Start()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(gapSeconds.x, gapSeconds.y));

            AudioManager audio = AudioManager.Instance;
            if (audio == null)
                continue;

            playing.Clear();
            Play(audio, Sfx.AnnouncementBell);
            yield return new WaitForSeconds(voiceAfterChime);

            // Pitch changes the length, so wait out the voice as it actually plays.
            AudioSource voice = Play(audio, Sfx.Announcement);
            if (voice != null)
                yield return new WaitForSeconds(voice.clip.length / voice.pitch);

            AudioSource closing = Play(audio, Sfx.AnnouncementBellEnd);
            if (closing != null)
                yield return new WaitForSeconds(closing.clip.length / closing.pitch);
        }
    }

    private AudioSource Play(AudioManager audio, string sound)
    {
        AudioSource source = audio.PlayDedicatedSFX(sound);
        if (source != null)
            playing.Add(source);
        return source;
    }

    // Leaving the menu mid-announcement fades it rather than letting it carry on into the map.
    private void OnDisable()
    {
        if (AudioManager.Instance != null)
            foreach (AudioSource source in playing)
                AudioManager.Instance.StopSFX(source, leaveFade);
        playing.Clear();
    }
}
