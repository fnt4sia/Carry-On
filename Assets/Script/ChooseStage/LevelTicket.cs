using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The boarding-pass card that appears over a node once every plane has parked on it.
///
/// The prefab owns the printed frame and every label; this presenter only fills them in,
/// keeps the card over the node, and fades it. Nothing here is built at runtime.
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
    [Tooltip("How far down the screen the card starts while fading in.")]
    [SerializeField, Min(0f)] private float riseDistance = 2.5f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.2f;

    [Header("Wording")]
    [SerializeField] private string unlockedStatus = "ON TIME";
    [SerializeField] private string lockedStatus = "LOCKED";
    [Tooltip("Shown in place of a score a save has no record of yet.")]
    [SerializeField] private string noScoreText = "-";

    private LevelNode current;
    private Coroutine anim;
    private Transform cameraTransform;
    private float riseOffset;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        riseOffset = riseDistance;
    }

    public void Show(LevelNode node)
    {
        if (node == null || node == current)
            return;

        current = node;
        Fill(node);
        PlaceOverNode();
        Animate(1f);
    }

    public void Hide()
    {
        if (current == null)
            return;

        current = null;
        Animate(0f);
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
        // Rotation first: the rise slides along the card's own up, which is screen-up.
        if (cameraTransform == null || !cameraTransform.gameObject.activeInHierarchy)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;
            cameraTransform = cam.transform;
        }

        transform.rotation = cameraTransform.rotation;
        transform.position = current.transform.position + worldOffset - transform.up * riseOffset;
    }

    private void Animate(float targetAlpha)
    {
        if (anim != null)
            StopCoroutine(anim);

        if (!isActiveAndEnabled)
        {
            if (canvasGroup != null)
                canvasGroup.alpha = targetAlpha;
            riseOffset = targetAlpha > 0f ? 0f : riseDistance;
            return;
        }

        anim = StartCoroutine(AnimateRoutine(targetAlpha));
    }

    private IEnumerator AnimateRoutine(float targetAlpha)
    {
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        float startRise = riseOffset;
        float targetRise = targetAlpha > 0f ? 0f : riseDistance;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            float t = elapsed / fadeDuration;
            t = 1f - (1f - t) * (1f - t);   // ease out
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            riseOffset = Mathf.Lerp(startRise, targetRise, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = targetAlpha;
        riseOffset = targetRise;
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
