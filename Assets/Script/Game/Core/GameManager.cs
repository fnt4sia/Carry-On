using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns round rules and state only. Presentation lives in GameHUD, ambient visuals
/// live in their own components, and scoring lives in the plain ScoreBoard class.
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameManager : SingletonBehaviour<GameManager>
{
    private LevelConfig levelConfig;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    private ScoreBoard scoreBoard;
    private InputAction pauseAction;
    private float gameTimer;
    private int lastPublishedSecond = -1;
    private int currentCountdown = 3;
    private bool gameStarted;
    private bool isPaused;
    private bool gameEnded;

    public event Action<int> ScoreChanged;
    public event Action<float> TimeChanged;
    public event Action<int> CountdownChanged;
    public event Action RoundStarted;
    public event Action<bool> PauseChanged;
    public event Action<GameResult> RoundEnded;

    public LevelConfig Config => levelConfig;
    public ScoreBoard Scores => scoreBoard;
    public float TimeRemaining => gameTimer;
    public bool IsRoundStarted => gameStarted;
    public bool IsPaused => isPaused;
    public bool IsRoundEnded => gameEnded;
    public int CurrentCountdown => currentCountdown;

    protected override void OnSingletonAwake()
    {
        if (LevelContext.TryGetConfig(out LevelConfig sceneConfig))
            levelConfig = sceneConfig;

        scoreBoard = new ScoreBoard(4);
        scoreBoard.ScoreChanged += value => ScoreChanged?.Invoke(value);
        scoreBoard.ScoreApplied += OnScoreApplied;
        RoundScoreContext.Bind(scoreBoard);

        if (inputActions != null)
        {
            pauseAction = inputActions.FindAction("System/Pause", throwIfNotFound: false);
            pauseAction?.Enable();
        }
    }

    private void Start()
    {
        if (levelConfig == null)
        {
            Debug.LogError($"{nameof(GameManager)} on {name} has no {nameof(LevelConfig)} assigned.");
            enabled = false;
            return;
        }

        scoreBoard.Reset();
        gameTimer = levelConfig.gameTime;
        PublishTime(force: true);
        StartCoroutine(StartRound());
    }

    protected override void OnSingletonDestroyed()
    {
        pauseAction?.Disable();
        if (scoreBoard != null)
        {
            scoreBoard.ScoreApplied -= OnScoreApplied;
            RoundScoreContext.Unbind(scoreBoard);
        }
    }

    private void Update()
    {
        HandlePauseInput();
        UpdateGameTimer();
    }

    public void PauseGame()
    {
        if (!gameStarted || gameEnded || isPaused)
            return;

        isPaused = true;
        Time.timeScale = 0f;
        PauseChanged?.Invoke(true);
    }

    public void ResumeGame()
    {
        if (gameEnded || !isPaused)
            return;

        isPaused = false;
        Time.timeScale = 1f;
        PauseChanged?.Invoke(false);
    }

    public void ExitGame()
    {
        Time.timeScale = 1f;
        SceneLoader.LoadLobby();
    }

    public void BackToLobby() => ExitGame();

    private IEnumerator StartRound()
    {
        Time.timeScale = 0f;

        for (int second = 3; second > 0; second--)
        {
            currentCountdown = second;
            CountdownChanged?.Invoke(currentCountdown);
            float elapsed = 0f;
            while (elapsed < 1f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        currentCountdown = 0;
        CountdownChanged?.Invoke(currentCountdown);
        gameStarted = true;
        Time.timeScale = 1f;
        AudioManager.Instance?.PlaySFX(Sfx.Start);
        AudioManager.Instance?.PlayGameplayMusic();
        RoundStarted?.Invoke();
    }

    private void HandlePauseInput()
    {
        if (!gameStarted || gameEnded || pauseAction == null || !pauseAction.WasPressedThisFrame())
            return;

        if (isPaused)
            ResumeGame();
        else
            PauseGame();
    }

    private void UpdateGameTimer()
    {
        if (!gameStarted || isPaused || gameEnded)
            return;

        gameTimer = Mathf.Max(0f, gameTimer - Time.deltaTime);
        PublishTime();

        if (gameTimer <= 0f)
            EndRound();
    }

    private void PublishTime(bool force = false)
    {
        int displaySecond = Mathf.CeilToInt(gameTimer);
        if (!force && displaySecond == lastPublishedSecond)
            return;

        lastPublishedSecond = displaySecond;
        TimeChanged?.Invoke(gameTimer);
    }

    private void EndRound()
    {
        if (gameEnded)
            return;

        gameEnded = true;
        Time.timeScale = 0f;
        RoundScoreContext.Unbind(scoreBoard);
        AudioManager.Instance?.PlaySFX(Sfx.TimesUp);

        GameResult result = new(
            levelConfig.levelId,
            scoreBoard.Score,
            scoreBoard.DeliveryCount,
            scoreBoard.CopyPlayerScores(),
            scoreBoard.CopyPlayerDeliveries(),
            levelConfig.CalculateStars(scoreBoard.Score));

        ProgressionService.Instance?.RecordResult(levelConfig, result);
        RoundEnded?.Invoke(result);
    }

    private static void OnScoreApplied(int scoreDelta, int _)
    {
        if (scoreDelta > 0)
            AudioManager.Instance?.PlaySFX(Sfx.Score);
        else if (scoreDelta < 0)
            AudioManager.Instance?.PlaySFX(Sfx.Wrong);
    }
}
