using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The boarding pass. It slides in from the left of the screen the moment every plane is on one
/// stage, so the group can read where they're going while the landing ring fills, and slides back
/// out if anyone leaves. When the ring is full the pass gets the APPROVED stamp and stays up under
/// the loading clouds (<see cref="MapController"/> drives all three).
///
/// The prefab owns the printed frame and every label; this presenter only fills them in and
/// animates the card. Nothing here is built at runtime.
/// </summary>
[DisallowMultipleComponent]
public class LevelTicket : MonoBehaviour
{
    [Header("Stub")]
    [SerializeField] private TMP_Text flightCodeText;
    [SerializeField] private TMP_Text originCodeText;
    [SerializeField] private TMP_Text originNameText;
    [SerializeField] private TMP_Text destinationCodeText;
    [SerializeField] private TMP_Text destinationNameText;
    [SerializeField] private TMP_Text durationText;
    [SerializeField] private TMP_Text statusText;

    [Header("Panel")]
    [SerializeField] private Image previewImage;
    [SerializeField] private Image[] starImages;
    [SerializeField] private TMP_Text[] starThresholdTexts;
    [SerializeField] private Sprite starFilledSprite;
    [SerializeField] private Sprite starEmptySprite;
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private TMP_Text latestScoreText;

    [Header("Stamp")]
    [Tooltip("APPROVED stamp, hidden until the group is committed. Its authored pose is where it lands.")]
    [SerializeField] private Image stamp;

    [Header("Animation")]
    [Tooltip("Seconds the card takes to slide in from off the left edge, or back out.")]
    [SerializeField, Min(0.05f)] private float slideSeconds = 0.35f;
    [Tooltip("Seconds the stamp takes to thump down.")]
    [SerializeField, Min(0.05f)] private float stampSeconds = 0.2f;

    [Header("Wording")]
    [SerializeField] private string waitingStatus = "ON TIME";
    [SerializeField] private string boardingStatus = "BOARDING";
    [Tooltip("Shown in place of a score for a stage that hasn't been finished yet.")]
    [SerializeField] private string noScoreText = "-";

    private RectTransform card;
    private Vector2 shownPosition;
    private Vector2 hiddenPosition;
    private Vector3 stampScale;
    private Coroutine slide;

    private void Awake()
    {
        // The card's authored position is where it sits when shown. It is anchored to the left
        // edge with its pivot on its own left side, so this pushes it fully off screen.
        card = (RectTransform)transform;
        shownPosition = card.anchoredPosition;
        hiddenPosition = new Vector2(-card.rect.width * card.localScale.x - 40f, shownPosition.y);
        card.anchoredPosition = hiddenPosition;

        if (stamp != null)
        {
            stampScale = stamp.rectTransform.localScale;
            stamp.gameObject.SetActive(false);
        }
    }

    public void Show(LevelNode node)
    {
        if (node == null)
            return;

        Fill(node);
        if (stamp != null)
            stamp.gameObject.SetActive(false);
        SlideTo(shownPosition, arriving: true);
    }

    public void Hide() => SlideTo(hiddenPosition, arriving: false);

    /// <summary>The group is committed: stamp the pass. It stays up under the loading clouds.</summary>
    public void Stamp()
    {
        SetText(statusText, boardingStatus);
        AudioManager.Instance?.PlaySFX(Sfx.Stamp);
        if (stamp != null)
            StartCoroutine(StampRoutine());
    }

    private void Fill(LevelNode node)
    {
        LevelConfig level = node.Level;

        SetText(flightCodeText, level != null ? level.flightCode : string.Empty);
        SetText(originCodeText, level != null ? level.originCode : string.Empty);
        SetText(originNameText, level != null ? level.originName : string.Empty);
        SetText(destinationCodeText, level != null ? level.destinationCode : string.Empty);
        SetText(destinationNameText, level != null ? level.destinationName : string.Empty);
        SetText(durationText, level != null ? FormatDuration(level.gameTime) : "--:--");
        SetText(statusText, waitingStatus);

        if (previewImage != null)
        {
            Sprite preview = level != null ? level.previewImage : null;
            previewImage.sprite = preview;
            previewImage.enabled = preview != null;
        }

        int stars = node.BestStars;
        int[] thresholds = level != null
            ? new[] { level.star1Score, level.star2Score, level.star3Score }
            : new[] { 0, 0, 0 };

        for (int i = 0; i < (starImages?.Length ?? 0); i++)
            if (starImages[i] != null)
                starImages[i].sprite = i < stars ? starFilledSprite : starEmptySprite;

        for (int i = 0; i < (starThresholdTexts?.Length ?? 0); i++)
            if (starThresholdTexts[i] != null)
                starThresholdTexts[i].text = i < thresholds.Length ? thresholds[i].ToString() : string.Empty;

        // A stage nobody has finished has no score yet, and a zero would read as a bad run.
        bool played = node.IsCompleted;
        SetText(bestScoreText, played ? node.BestScore.ToString() : noScoreText);
        SetText(latestScoreText, played ? node.LastScore.ToString() : noScoreText);
    }

    private void SlideTo(Vector2 target, bool arriving)
    {
        if (slide != null)
            StopCoroutine(slide);
        slide = isActiveAndEnabled ? StartCoroutine(SlideRoutine(card.anchoredPosition, target, arriving)) : null;
        if (slide == null)
            card.anchoredPosition = target;
    }

    // In: decelerates and settles with a small bounce. Out: speeds away. Starts from wherever the
    // card is, so a plane hopping in and out mid-slide reverses it smoothly.
    private IEnumerator SlideRoutine(Vector2 from, Vector2 to, bool arriving)
    {
        for (float t = 0f; t < slideSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / slideSeconds;
            card.anchoredPosition = Vector2.LerpUnclamped(from, to, arriving ? EaseOutBack(k) : k * k);
            yield return null;
        }

        card.anchoredPosition = to;
        slide = null;
    }

    // Drops from big and see-through onto the card, accelerating — a thump, not a pop.
    private IEnumerator StampRoutine()
    {
        stamp.gameObject.SetActive(true);
        Color color = stamp.color;
        for (float t = 0f; t < stampSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / stampSeconds;
            stamp.rectTransform.localScale = stampScale * Mathf.Lerp(1.8f, 1f, k * k);
            color.a = k;
            stamp.color = color;
            yield return null;
        }

        stamp.rectTransform.localScale = stampScale;
        color.a = 1f;
        stamp.color = color;
    }

    private static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;   // about 10 % overshoot
        float u = k - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }

    private static string FormatDuration(float seconds)
    {
        int whole = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{whole / 60:00}:{whole % 60:00}";
    }
}
