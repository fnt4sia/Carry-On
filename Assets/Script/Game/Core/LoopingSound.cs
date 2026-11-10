using UnityEngine;

// Plays one looping sound for as long as this object is enabled: the hum of a fullset conveyor,
// a map plane's idle drone, the sea wind under the stage-select map. Put it on the object the
// sound belongs to, so each one has exactly one. Volume and pitch are the sound's row on the
// AudioManager prefab — tune them there, not here.
public class LoopingSound : MonoBehaviour
{
    [Tooltip("Row name on the AudioManager prefab.")]
    [SerializeField] private string sound;
    [Tooltip("Seconds to fade in on enable and out on disable, so a scene change never cuts it dead.")]
    [SerializeField, Min(0f)] private float fade = 0.5f;

    private AudioSource source;

    private void OnEnable()
    {
        if (AudioManager.Instance != null)
            source = AudioManager.Instance.PlayLoopingSFX(sound, 1f, fade);
    }

    private void OnDisable()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopSFX(source, fade);
        source = null;
    }
}
