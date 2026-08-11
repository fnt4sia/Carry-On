using UnityEngine;

// Breathes a UI element's alpha between two values so an idle prompt reads as "waiting
// for you" instead of static text. Drives a CanvasGroup, so it fades the whole authored
// subtree at once and never touches the elements themselves.
//
// Unscaled time on purpose: the lobby can sit at timeScale 0 and the prompt should keep
// pulsing.
[RequireComponent(typeof(CanvasGroup))]
public class UIPulse : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.3f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 1f;
    [Tooltip("Seconds for one full dim -> bright -> dim cycle.")]
    [SerializeField] private float period = 1.6f;

    private CanvasGroup group;

    private void Awake() => group = GetComponent<CanvasGroup>();

    // Start bright so the prompt never appears mid-fade the frame it is revealed.
    private void OnEnable()
    {
        if (group != null) group.alpha = maxAlpha;
    }

    private void Update()
    {
        if (group == null || period <= 0f) return;

        float t = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * (2f * Mathf.PI / period));
        group.alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
    }
}
