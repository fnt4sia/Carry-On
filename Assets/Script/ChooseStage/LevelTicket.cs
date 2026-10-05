using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The boarding-pass card that pops up over a node once the group has boarded it — the moment
/// its border fills — and stays up as the transition into the level (<see cref="MapController"/>
/// holds it a few seconds, then loads).
///
/// The prefab owns the printed frame and every label; this presenter only fills them in,
/// keeps the card over the node, and animates it. Nothing here is built at runtime.
/// </summary>
[DisallowMultipleComponent]
public class LevelTicket : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;

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

    [Header("Placement")]
    [Tooltip("Offset from the node, in world space. The Y term lifts the card clear of the " +
             "map so it draws over the island; the Z term is what pushes it up the screen " +
             "under a top-down camera.")]
    [SerializeField] private Vector3 worldOffset = new(0f, 6f, 14f);

    [Header("Animation")]
    [Tooltip("Seconds the pop takes: up past full size, back under it, then settled.")]
    [SerializeField, Min(0.05f)] private float popSeconds = 0.45f;
    [Tooltip("How far past full size the first bounce goes (0.25 = 25 % bigger).")]
    [SerializeField, Range(0.01f, 1f)] private float popOvershoot = 0.25f;
    [Tooltip("Seconds to fade out if boarding is called off.")]
    [SerializeField, Min(0f)] private float fadeDuration = 0.2f;

    [Header("Wording")]
    [SerializeField] private string unlockedStatus = "ON TIME";
    [SerializeField] private string lockedStatus = "LOCKED";
    [Tooltip("Shown in place of a score a save has no record of yet.")]
    [SerializeField] private string noScoreText = "-";

    private LevelNode current;
    private Coroutine anim;
    private Transform cameraTransform;
    private Vector3 baseScale;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        baseScale = transform.localScale;
    }

    public void Show(LevelNode node)
    {
        if (node == null || node == current)
            return;

        current = node;
        Fill(node);
        PlaceOverNode();
        Play(PopRoutine(), shown: true);
    }

    public void Hide()
    {
        if (current == null)
            return;

        current = null;
        Play(FadeOutRoutine(), shown: false);
    }

    private void LateUpdate()
    {
        if (current != null)
            PlaceOverNode();
    }

    private void Fill(LevelNode node)
    {
        LevelConfig level = node.Level;
        bool unlocked = node.IsUnlocked;

        SetText(flightCodeText, level != null ? level.flightCode : string.Empty);
        SetText(originCodeText, level != null ? level.originCode : string.Empty);
        SetText(originNameText, level != null ? level.originName : string.Empty);
        SetText(destinationCodeText, level != null ? level.destinationCode : string.Empty);
        SetText(destinationNameText, level != null ? level.destinationName : string.Empty);
        SetText(durationText, level != null ? FormatDuration(level.gameTime) : "--:--");
        SetText(statusText, unlocked ? unlockedStatus : lockedStatus);

        if (previewImage != null)
        {
            Sprite preview = level != null ? level.previewImage : null;
            previewImage.sprite = preview;
            previewImage.enabled = preview != null;
        }

        int stars = unlocked ? node.BestStars : 0;
        int[] thresholds = level != null
            ? new[] { level.star1Score, level.star2Score, level.star3Score }
            : new[] { 0, 0, 0 };

        for (int i = 0; i < (starImages?.Length ?? 0); i++)
        {
            if (starImages[i] == null)
                continue;
            starImages[i].sprite = i < stars ? starFilledSprite : starEmptySprite;
        }

        for (int i = 0; i < (starThresholdTexts?.Length ?? 0); i++)
        {
            if (starThresholdTexts[i] == null)
                continue;
            starThresholdTexts[i].text = i < thresholds.Length ? thresholds[i].ToString() : string.Empty;
        }

        // A locked stage has never been played, so both readouts would be a misleading zero.
        SetText(bestScoreText, unlocked ? node.BestScore.ToString() : noScoreText);
        SetText(latestScoreText, unlocked ? node.LastScore.ToString() : noScoreText);
    }

    private void PlaceOverNode()
    {
        // The map camera holds one fixed angle, so copying its rotation is the whole
        // billboard — and it keeps every card on screen at the same readable tilt.
        if (cameraTransform == null || !cameraTransform.gameObject.activeInHierarchy)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;
            cameraTransform = cam.transform;
        }

        transform.rotation = cameraTransform.rotation;
        transform.position = current.transform.position + worldOffset;
    }

    private void Play(IEnumerator routine, bool shown)
    {
        if (anim != null)
            StopCoroutine(anim);
        anim = null;

        if (isActiveAndEnabled)
        {
            anim = StartCoroutine(routine);
            return;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = shown ? 1f : 0f;
        transform.localScale = baseScale;
    }

    // Big, a little small, then settled: a damped spring from nothing to full size.
    private IEnumerator PopRoutine()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        float swing = 2.5f * Mathf.PI / popSeconds;                       // two and a half swings
        float damping = -Mathf.Log(popOvershoot) * swing / Mathf.PI;      // first swing peaks at 1 + popOvershoot
        for (float t = 0f; t < popSeconds; t += Time.deltaTime)
        {
            transform.localScale = baseScale * (1f - Mathf.Exp(-damping * t) * Mathf.Cos(swing * t));
            yield return null;
        }

        transform.localScale = baseScale;
        anim = null;
    }

    private IEnumerator FadeOutRoutine()
    {
        float start = canvasGroup != null ? canvasGroup.alpha : 0f;
        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Lerp(start, 0f, t / fadeDuration);
            yield return null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        transform.localScale = baseScale;
        anim = null;
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
