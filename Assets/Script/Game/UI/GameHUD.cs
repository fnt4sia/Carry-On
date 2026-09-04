using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Presentation for one gameplay round. All references point inside this prefab;
/// gameplay rules communicate through GameManager events only.
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject backgroundDimObject;
    [SerializeField] private GameObject gameTimerObject;
    [SerializeField] private GameObject gameOverObject;
    [SerializeField] private GameObject scoreObject;

    [Header("Text")]
    [SerializeField] private TMP_Text countdownTimerText;
    [SerializeField] private TMP_Text gameTimerText;
    [SerializeField] private TMP_Text scoreText;

    [Header("Round Timer Dial")]
    [Tooltip("Radial Image on the round timer icon. Drains like the luggage timer dial.")]
    [SerializeField] private Image gameTimerFill;

    [Header("Stage End Card")]
    [Tooltip("Root that slides in when the round ends. Holds the whole result card.")]
    [SerializeField] private GameObject stageEndObject;
    [SerializeField] private TMP_Text stageText;
    [SerializeField] private TMP_Text flightCodeText;
    [Tooltip("Level screenshot inside the frame. Left as authored when the config has no preview.")]
    [SerializeField] private Image previewImage;
    [SerializeField] private CanvasGroup scoreRow;
    [SerializeField] private TMP_Text totalScoreText;
    [SerializeField] private CanvasGroup timeRow;
    [SerializeField] private TMP_Text roundLengthText;
    [SerializeField] private GameObject approvedStampObject;
    [SerializeField] private GameObject nextStageObject;

    [Header("Stage End — Stars")]
    [Tooltip("Gold star switched on when its tier is earned. One per star tier, in order.")]
    [SerializeField] private GameObject[] starFills;
    [SerializeField] private TMP_Text[] starRequirementTexts;

    [Header("Stage End — Players")]
    [Tooltip("One row per player slot. Rows past the joined player count stay switched off.")]
    [SerializeField] private CanvasGroup[] playerRows;
    [SerializeField] private TMP_Text[] playerNameTexts;
    [SerializeField] private TMP_Text[] playerScoreTexts;

    [Header("Selection")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button nextStageButton;

    private const float RevealDuration = 0.18f;
    private const float RevealGap = 0.12f;
    private const float StarGap = 0.35f;

    private GameManager gameManager;
    private Coroutine resultsRoutine;
    private bool hasStarted;
    private bool isSubscribed;

    private void OnEnable()
    {
        if (hasStarted)
            TryBind();
    }

    private void Start()
    {
        hasStarted = true;
        if (!TryBind())
        {
            Debug.LogError($"{nameof(GameHUD)} needs an active {nameof(GameManager)}.");
            enabled = false;
            return;
        }

        ResetView();
        SyncCurrentState();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private bool TryBind()
    {
        if (isSubscribed)
            return true;

        gameManager = GameManager.Instance;
        if (gameManager == null)
            return false;

        Subscribe();
        return true;
    }

    public void ResumeGame()
    {
        PlayButtonSelectSfx();
        gameManager?.ResumeGame();
    }

    public void ExitToLobby()
    {
        PlayButtonSelectSfx();
        SceneLoader.LoadLobby();
    }

    public void LoadNextStage()
    {
        PlayButtonSelectSfx();
        LevelConfig nextLevel = gameManager != null ? gameManager.Config?.nextLevel : null;
        if (nextLevel != null && !string.IsNullOrWhiteSpace(nextLevel.sceneName))
            SceneLoader.Load(nextLevel.sceneName);
        else
            SceneLoader.LoadStageSelect();
    }

    private void Subscribe()
    {
        if (isSubscribed)
            return;

        gameManager.ScoreChanged += OnScoreChanged;
        gameManager.TimeChanged += OnTimeChanged;
        gameManager.CountdownChanged += OnCountdownChanged;
        gameManager.RoundStarted += OnRoundStarted;
        gameManager.PauseChanged += OnPauseChanged;
        gameManager.RoundEnded += OnRoundEnded;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (gameManager == null)
            return;

        gameManager.ScoreChanged -= OnScoreChanged;
        gameManager.TimeChanged -= OnTimeChanged;
        gameManager.CountdownChanged -= OnCountdownChanged;
        gameManager.RoundStarted -= OnRoundStarted;
        gameManager.PauseChanged -= OnPauseChanged;
        gameManager.RoundEnded -= OnRoundEnded;
        isSubscribed = false;
    }

    private void ResetView()
    {
        SetActive(pausePanel, false);
        SetActive(gameTimerObject, false);
        SetActive(gameOverObject, false);
        SetActive(stageEndObject, false);
        SetActive(scoreObject, false);
        SetActive(approvedStampObject, false);
        SetActive(nextStageObject, false);
        SetActive(backgroundDimObject, true);

        Hide(scoreRow);
        Hide(timeRow);
        foreach (CanvasGroup row in playerRows ?? Array.Empty<CanvasGroup>())
            Hide(row);
        foreach (GameObject fill in starFills ?? Array.Empty<GameObject>())
            SetActive(fill, false);

        if (countdownTimerText != null)
            countdownTimerText.text = string.Empty;
    }

    private void SyncCurrentState()
    {
        OnScoreChanged(gameManager.Scores?.Score ?? 0);
        OnTimeChanged(gameManager.TimeRemaining);
        OnCountdownChanged(gameManager.CurrentCountdown);

        if (gameManager.IsRoundStarted)
            OnRoundStarted();
        if (gameManager.IsPaused)
            OnPauseChanged(true);

        LevelConfig config = gameManager.Config;
        if (config == null)
            return;

        if (stageText != null)
            stageText.text = config.displayName;

        if (flightCodeText != null)
            flightCodeText.text = config.flightCode;

        if (previewImage != null && config.previewImage != null)
            previewImage.sprite = config.previewImage;

        if (roundLengthText != null)
        {
            int roundSeconds = Mathf.RoundToInt(config.gameTime);
            roundLengthText.SetText("{0:00}:{1:00}", roundSeconds / 60, roundSeconds % 60);
        }

        int[] requirements = { config.star1Score, config.star2Score, config.star3Score };
        for (int i = 0; i < (starRequirementTexts?.Length ?? 0) && i < requirements.Length; i++)
            if (starRequirementTexts[i] != null)
                starRequirementTexts[i].text = requirements[i].ToString();
    }

    private void OnScoreChanged(int score)
    {
        if (scoreText != null)
            scoreText.text = score.ToString();
    }

    private void OnTimeChanged(float timeRemaining)
    {
        if (gameTimerFill != null)
        {
            float roundLength = gameManager != null && gameManager.Config != null
                ? gameManager.Config.gameTime
                : 0f;
            gameTimerFill.fillAmount = roundLength > 0f
                ? Mathf.Clamp01(timeRemaining / roundLength)
                : 0f;
        }

        if (gameTimerText == null)
            return;

        int displaySecond = Mathf.CeilToInt(timeRemaining);
        gameTimerText.SetText("{0}:{1:00}", displaySecond / 60, displaySecond % 60);
    }

    private void OnCountdownChanged(int second)
    {
        SetActive(backgroundDimObject, second > 0);
        if (countdownTimerText != null)
            countdownTimerText.text = second > 0 ? second.ToString() : string.Empty;
    }

    private void OnRoundStarted()
    {
        SetActive(backgroundDimObject, false);
        SetActive(gameTimerObject, true);
        SetActive(scoreObject, true);
        StartCoroutine(AnimateHudIntoPlace());
    }

    private void OnPauseChanged(bool paused)
    {
        SetActive(backgroundDimObject, paused);
        SetActive(pausePanel, paused);
        if (paused && resumeButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
    }

    private void OnRoundEnded(GameResult result)
    {
        if (resultsRoutine != null)
            StopCoroutine(resultsRoutine);
        resultsRoutine = StartCoroutine(PlayResultsSequence(result));
    }

    private IEnumerator AnimateHudIntoPlace()
    {
        RectTransform timerRect = gameTimerObject != null ? gameTimerObject.GetComponent<RectTransform>() : null;
        RectTransform scoreRect = scoreObject != null ? scoreObject.GetComponent<RectTransform>() : null;
        if (timerRect == null)
            yield break;

        Vector2 timerStart = Vector2.zero;
        Vector2 timerEnd = timerRect.anchoredPosition;
        Vector2 scoreEnd = scoreRect != null ? scoreRect.anchoredPosition : Vector2.zero;
        timerRect.anchoredPosition = timerStart;
        timerRect.localScale = Vector3.one * 1.5f;

        float elapsed = 0f;
        const float duration = 0.75f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            timerRect.anchoredPosition = Vector2.Lerp(timerStart, timerEnd, t);
            timerRect.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.one, t);
            if (scoreRect != null)
                scoreRect.anchoredPosition = Vector2.Lerp(-scoreEnd, scoreEnd, t);
            yield return null;
        }

        timerRect.anchoredPosition = timerEnd;
        timerRect.localScale = Vector3.one;
        if (scoreRect != null)
            scoreRect.anchoredPosition = scoreEnd;
    }

    // Runs on unscaled time: the round ends with Time.timeScale at 0.
    private IEnumerator PlayResultsSequence(GameResult result)
    {
        SetActive(gameTimerObject, false);
        SetActive(scoreObject, false);
        SetActive(backgroundDimObject, true);
        SetActive(gameOverObject, true);

        yield return ScaleIn(gameOverObject, 0.1f, 3f, 0.65f);
        yield return new WaitForSecondsRealtime(0.5f);

        if (totalScoreText != null)
            totalScoreText.text = result.Score.ToString();
        int activeRows = FillPlayerRows(result);

        SetActive(stageEndObject, true);
        yield return SlideInStageEnd();

        yield return Reveal(scoreRow);
        yield return Reveal(timeRow);
        for (int i = 0; i < activeRows; i++)
            yield return Reveal(playerRows[i]);

        yield return new WaitForSecondsRealtime(0.25f);

        for (int i = 0; i < (starFills?.Length ?? 0); i++)
        {
            if (i >= result.Stars || starFills[i] == null)
                continue;

            SetActive(starFills[i], true);
            AudioManager.Instance?.PlaySFX(Sfx.Star);
            yield return new WaitForSecondsRealtime(StarGap);
        }

        if (result.Stars > 0)
        {
            SetActive(approvedStampObject, true);
            AudioManager.Instance?.PlaySFX(Sfx.Stamp);
            yield return ScaleIn(approvedStampObject, 2.5f, 1f, 0.18f);
        }

        SetActive(nextStageObject, true);
        if (nextStageButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(nextStageButton.gameObject);
    }

    /// <summary>
    /// Switches on one row per joined player, fills its name and score, and hides the rest.
    /// Rows stay hidden-but-active so the grid settles before anything fades in.
    /// Returns how many rows are in play.
    /// </summary>
    private int FillPlayerRows(GameResult result)
    {
        int slots = playerRows?.Length ?? 0;
        if (slots == 0)
            return 0;

        List<PlayerInput> joined = new(PlayerInput.all);
        joined.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));
        int count = Mathf.Clamp(joined.Count, 1, slots);

        for (int i = 0; i < slots; i++)
        {
            if (playerRows[i] == null)
                continue;

            SetActive(playerRows[i].gameObject, i < count);
            Hide(playerRows[i]);
            if (i >= count)
                continue;

            if (i < (playerNameTexts?.Length ?? 0) && playerNameTexts[i] != null)
                playerNameTexts[i].text = PlayerDisplayName(i < joined.Count ? joined[i] : null, i);

            if (i < (playerScoreTexts?.Length ?? 0) && playerScoreTexts[i] != null)
            {
                int score = i < result.PlayerScores.Length ? result.PlayerScores[i] : 0;
                playerScoreTexts[i].text = score.ToString();
            }
        }

        return count;
    }

    // Player bodies are prefab instances, so the character name arrives as "Annie(Clone)".
    private static string PlayerDisplayName(PlayerInput player, int index)
    {
        if (player == null)
            return $"PLAYER {index + 1}";

        string bodyName = player.name;
        int clone = bodyName.IndexOf("(Clone)", StringComparison.Ordinal);
        return clone >= 0 ? bodyName[..clone].Trim() : bodyName;
    }

    private static IEnumerator Reveal(CanvasGroup group)
    {
        if (group == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < RevealDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(elapsed / RevealDuration);
            yield return null;
        }
        group.alpha = 1f;
        yield return new WaitForSecondsRealtime(RevealGap);
    }

    private static void Hide(CanvasGroup group)
    {
        if (group != null)
            group.alpha = 0f;
    }

    private static IEnumerator ScaleIn(GameObject target, float from, float to, float duration)
    {
        if (target == null)
            yield break;

        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            rect.localScale = Vector3.one * Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        rect.localScale = Vector3.one * to;
    }

    private IEnumerator SlideInStageEnd()
    {
        RectTransform rect = stageEndObject != null ? stageEndObject.GetComponent<RectTransform>() : null;
        if (rect == null)
            yield break;

        Vector2 target = rect.anchoredPosition;
        Vector2 start = target + Vector2.up * Screen.height;
        rect.anchoredPosition = start;

        float elapsed = 0f;
        const float duration = 0.8f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            rect.anchoredPosition = Vector2.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        rect.anchoredPosition = target;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private static void PlayButtonSelectSfx()
        => AudioManager.Instance?.PlaySFX(Sfx.ButtonSelect);
}
