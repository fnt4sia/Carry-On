using UnityEngine;

// Tired-fluorescent flicker for a signage canvas: mostly steady, with occasional short
// bursts of dimming. Drives a CanvasGroup so it covers every graphic under it at once.
[RequireComponent(typeof(CanvasGroup))]
public class SignFlicker : MonoBehaviour
{
    [SerializeField, Range(0f, 1f), Tooltip("Alpha while the sign is behaving.")]
    private float litAlpha = 1f;
    [SerializeField, Range(0f, 1f), Tooltip("Alpha at the bottom of a flicker. Keep it above 0 so the sign never fully blanks.")]
    private float dimAlpha = 0.4f;

    [SerializeField, Tooltip("Seconds of steady light between bursts, picked at random in this range.")]
    private Vector2 quietRange = new(3f, 8f);
    [SerializeField, Tooltip("How long one burst lasts, picked at random in this range.")]
    private Vector2 burstRange = new(0.06f, 0.25f);
    [SerializeField, Min(1f), Tooltip("Dips per second inside a burst.")]
    private float burstRate = 14f;

    private CanvasGroup group;
    private float nextChange;
    private bool bursting;

    private void Awake() => group = GetComponent<CanvasGroup>();

    private void OnEnable()
    {
        bursting = false;
        group.alpha = litAlpha;
        nextChange = Time.time + Random.Range(quietRange.x, quietRange.y);
    }

    private void Update()
    {
        if (Time.time >= nextChange)
        {
            bursting = !bursting;
            Vector2 range = bursting ? burstRange : quietRange;
            nextChange = Time.time + Random.Range(range.x, range.y);

            if (!bursting)
                group.alpha = litAlpha;
        }

        if (bursting)
            group.alpha = Mathf.Sin(Time.time * burstRate * Mathf.PI * 2f) > 0f ? litAlpha : dimAlpha;
    }
}
