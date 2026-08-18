using TMPro;
using UnityEngine;

// LED-ticker scroll for a signage label: slides the text right-to-left across this
// object's rect and restarts once the tail has left. Set dressing only.
//
// The label is authored in the prefab, never built here — this only moves it. This object
// needs a RectMask2D or the text spills past the sign's edge, and the label wants
// word-wrap off with a left-middle pivot so preferredWidth is its true length.
[RequireComponent(typeof(RectTransform))]
public class MarqueeText : MonoBehaviour
{
    [SerializeField, Tooltip("The text to scroll. Authored as a child of this viewport.")]
    private TMP_Text label;
    [SerializeField, Min(0f), Tooltip("Canvas units per second.")]
    private float scrollSpeed = 90f;
    [SerializeField, Min(0f), Tooltip("Blank canvas units between the tail leaving and the head returning.")]
    private float gap = 250f;
    [SerializeField, Min(0f), Tooltip("Hold the text offscreen this long before the first pass, so a row of signs is not in step.")]
    private float startDelay;

    private RectTransform viewport;
    private RectTransform labelRect;
    private float travel;

    private void Awake()
    {
        viewport = (RectTransform)transform;
        if (label != null)
            labelRect = label.rectTransform;
    }

    private void OnEnable()
    {
        // Negative travel parks the head off the right edge, which the mask hides.
        travel = -startDelay * scrollSpeed;
        Apply();
    }

    private void Update()
    {
        travel += scrollSpeed * Time.deltaTime;
        Apply();
    }

    private void Apply()
    {
        if (labelRect == null) return;

        float span = viewport.rect.width + label.preferredWidth + gap;
        if (span <= 0f) return;

        if (travel > span)
            travel -= span * Mathf.Floor(travel / span);

        float x = viewport.rect.width * 0.5f - travel;
        labelRect.anchoredPosition = new Vector2(x, labelRect.anchoredPosition.y);
    }
}
