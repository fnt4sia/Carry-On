using System.Collections;
using TMPro;
using UnityEngine;

// Screen-space info card for the ChooseStage map. When the airplane token parks on a
// level node, MapMover calls Show(node) and the card slides + fades in with that node's
// name and description; Hide() slides it back out. One card, reused for every node.
public class LevelInfoPopup : MonoBehaviour
{
    [SerializeField] private RectTransform card;     // the panel that moves
    [SerializeField] private CanvasGroup canvasGroup; // for fade
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Animation")]
    [SerializeField] private Vector2 shownAnchoredPos = new Vector2(0f, 220f);
    [SerializeField] private Vector2 hiddenAnchoredPos = new Vector2(0f, 420f);
    [SerializeField] private float animDuration = 0.25f;

    private Coroutine anim;
    private LevelNode current;

    private void Awake()
    {
        if (card == null) card = transform as RectTransform;
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        // start hidden
        if (card != null) card.anchoredPosition = hiddenAnchoredPos;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    public void Show(LevelNode node)
    {
        if (node == null || node == current) return;
        current = node;

        if (titleText != null) titleText.text = node.levelName;
        if (descriptionText != null) descriptionText.text = node.description;

        Animate(shownAnchoredPos, 1f);
    }

    public void Hide()
    {
        if (current == null) return;
        current = null;
        Animate(hiddenAnchoredPos, 0f);
    }

    private void Animate(Vector2 targetPos, float targetAlpha)
    {
        if (anim != null) StopCoroutine(anim);
        if (isActiveAndEnabled)
            anim = StartCoroutine(AnimateRoutine(targetPos, targetAlpha));
        else
        {
            if (card != null) card.anchoredPosition = targetPos;
            if (canvasGroup != null) canvasGroup.alpha = targetAlpha;
        }
    }

    private IEnumerator AnimateRoutine(Vector2 targetPos, float targetAlpha)
    {
        Vector2 startPos = card != null ? card.anchoredPosition : Vector2.zero;
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        float elapsed = 0f;

        while (elapsed < animDuration)
        {
            float t = elapsed / animDuration;
            t = 1f - (1f - t) * (1f - t); // ease-out
            if (card != null) card.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (card != null) card.anchoredPosition = targetPos;
        if (canvasGroup != null) canvasGroup.alpha = targetAlpha;
    }
}
