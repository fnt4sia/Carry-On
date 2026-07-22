using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    [SerializeField] private GameObject visaObject;
    [SerializeField] private GameObject scoreObject;
    [SerializeField] private GameObject approvedStampObject;
    [SerializeField] private GameObject nextStageObject;

    [Header("Text")]
    [SerializeField] private TMP_Text countdownTimerText;
    [SerializeField] private TMP_Text gameTimerText;
    [SerializeField] private TMP_Text totalDeliveredText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text[] playerDeliveredTexts;
    [SerializeField] private TMP_Text stageText;
    [SerializeField] private TMP_Text[] starRequirementTexts;

    [Header("Stars")]
    [SerializeField] private Image[] stars;

    [Header("Selection")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button nextStageButton;

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
        SetActive(visaObject, false);
        SetActive(scoreObject, false);
        SetActive(approvedStampObject, false);
        SetActive(nextStageObject, false);
        SetActive(backgroundDimObject, true);

        if (countdownTimerText != null)
            countdownTimerText.text = string.Empty;

        foreach (Image star in stars ?? System.Array.Empty<Image>())
            if (star != null)
                star.color = new Color(0.4f, 0.4f, 0.4f);
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

    private IEnumerator PlayResultsSequence(GameResult result)
    {
        SetActive(gameTimerObject, false);
        SetActive(scoreObject, false);
        SetActive(backgroundDimObject, true);
        SetActive(gameOverObject, true);

        yield return ScaleIn(gameOverObject, 0.1f, 3f, 0.65f);
        yield return new WaitForSecondsRealtime(0.5f);

        SetActive(visaObject, true);
        yield return SlideInVisa();

        if (totalDeliveredText != null)
            totalDeliveredText.text = result.DeliveryCount.ToString();

        for (int i = 0; i < (playerDeliveredTexts?.Length ?? 0); i++)
        {
            int delivered = i < result.PlayerDeliveries.Length ? result.PlayerDeliveries[i] : 0;
            if (playerDeliveredTexts[i] != null)
                playerDeliveredTexts[i].text = delivered.ToString();
        }

        yield return new WaitForSecondsRealtime(0.5f);
        for (int i = 0; i < (stars?.Length ?? 0); i++)
        {
            if (i >= result.Stars || stars[i] == null)
                continue;

            stars[i].color = Color.white;
            AudioManager.Instance?.PlaySFX(Sfx.Star);
            yield return new WaitForSecondsRealtime(0.35f);
        }

        SetActive(approvedStampObject, true);
        AudioManager.Instance?.PlaySFX(Sfx.Stamp);
        SetActive(nextStageObject, true);
        if (nextStageButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(nextStageButton.gameObject);
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

    private IEnumerator SlideInVisa()
    {
        RectTransform rect = visaObject != null ? visaObject.GetComponent<RectTransform>() : null;
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
