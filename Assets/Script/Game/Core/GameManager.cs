using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Level Config")]
    [SerializeField] private LevelConfig levelConfig;

    [Header("Airplane")]
    [SerializeField] private GameObject airplanePrefab;
    [SerializeField] private Vector3 spawnPosition;
    [SerializeField] private Vector3 destroyPosition;

    [Header("UI — Panels")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject backgroundDimObject;
    [SerializeField] private GameObject gameTimerObject;
    [SerializeField] private GameObject gameOverObject;
    [SerializeField] private GameObject visaObject;
    [SerializeField] private GameObject scoreObject;
    [SerializeField] private GameObject approvedStampObject;
    [SerializeField] private GameObject nextStageObject;

    [Header("UI — Text")]
    [SerializeField] private TextMeshProUGUI countdownTimerText;
    [SerializeField] private TextMeshProUGUI gameTimerText;
    [SerializeField] private TextMeshProUGUI totalDeliveredText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI[] playerDeliveredTexts;
    [SerializeField] private TextMeshProUGUI stageText;
    [SerializeField] private TextMeshProUGUI firstStarRequirement;
    [SerializeField] private TextMeshProUGUI secondStarRequirement;
    [SerializeField] private TextMeshProUGUI thirdStarRequirement;

    [Header("UI — Stars")]
    [SerializeField] private Image firstStar;
    [SerializeField] private Image secondStar;
    [SerializeField] private Image thirdStar;

    [Header("UI — Buttons")]
    [SerializeField] private Button resumeButton;

    private float gameTimer;
    private float randomSpeed;
    private bool gameStarted;
    private bool isPaused;
    private int gameScore;
    private int[] playerScores = new int[4];
    private GameObject airplaneObject;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        } 
    }

    void Start()
    {
        scoreText.text = gameScore.ToString();
        gameTimer = levelConfig.gameTime + 1.5f;
        StartCoroutine(StartGameCountdown());
        StartCoroutine(SpawnAirplane());
    }
    
    public void ExitGame()
    {
        SceneManager.LoadScene(0);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1;
        backgroundDimObject.SetActive(false);
        pausePanel.SetActive(false);
    }

    public void AddScore(int score)
    {
        gameScore += score;
        if (gameScore < 0) gameScore = 0;
        scoreText.text = gameScore.ToString();
        // if (score > 0) secondAudioSource.PlayOneShot(scoreSound);
        // else secondAudioSource.PlayOneShot(wrongSound);
    }

    public LevelConfig GetLevelConfig() => levelConfig;
    public int GetCorrectDeliveryScore() => levelConfig.scoreCorrectDelivery;
    public int GetMissingProcessPenalty() => levelConfig.scoreMissingProcess;
    public int GetBombDeliveredPenalty() => levelConfig.scoreBombDelivered;
    public int GetTimerExpiredPenalty() => levelConfig.scoreTimerExpired;

    public void AddPlayerScore(int playerIndex, int score)
    {
        if (playerIndex >= 0 && playerIndex < playerScores.Length)
            playerScores[playerIndex] += score;
    }

    private IEnumerator StartGameCountdown()
    {
        backgroundDimObject.SetActive(true);

        Time.timeScale = 0;

        float countdown = 3f;
        while (countdown > -1)
        {
            countdownTimerText.text = Mathf.Ceil(countdown).ToString();
            countdown -= Time.unscaledDeltaTime;
            yield return null;
        }

        countdownTimerText.text = "";

        backgroundDimObject.SetActive(false);
        gameStarted = true;
        Time.timeScale = 1;

        scoreObject.SetActive(true);
        RectTransform rectTransform = scoreObject.GetComponent<RectTransform>();
        rectTransform.position = new(150f, 80f, 0f);

        yield return StartCoroutine(MoveTimerToCorner());
    }

    private IEnumerator EndGame()
    {
        Time.timeScale = 0;

        gameTimerObject.SetActive(false);
        scoreObject.SetActive(false);
        backgroundDimObject.SetActive(true);

        yield return StartCoroutine(AnimateGameOverScale());
        yield return new WaitForSecondsRealtime(1f);

        yield return StartCoroutine(SlideVisaObjectFromTop());
        yield return new WaitForSecondsRealtime(1f);

        totalDeliveredText.text = (gameScore / 10).ToString();
        yield return new WaitForSecondsRealtime(1f);
        for (int i = 0; i < playerDeliveredTexts.Length; i++)
        {
            if (playerDeliveredTexts[i] != null)
            {
                playerDeliveredTexts[i].text = (playerScores[i] / 10).ToString();
                yield return new WaitForSecondsRealtime(1f);
            }
        }

        yield return new WaitForSecondsRealtime(1.5f);

        if (gameScore >= levelConfig.star1Score)
        {
            yield return new WaitForSecondsRealtime(0.325f);
            firstStar.color = Color.white;
            yield return new WaitForSecondsRealtime(0.5f);
        }

        if (gameScore >= levelConfig.star2Score)
        {
            yield return new WaitForSecondsRealtime(0.325f);
            secondStar.color = Color.white;
            yield return new WaitForSecondsRealtime(0.5f);
        }

        if (gameScore >= levelConfig.star3Score)
        {
            yield return new WaitForSecondsRealtime(0.325f);
            thirdStar.color = Color.white;
            yield return new WaitForSecondsRealtime(0.5f);
        }

        yield return new WaitForSecondsRealtime(0.5f);

        yield return new WaitForSecondsRealtime(0.02f);
        approvedStampObject.SetActive(true);
        nextStageObject.SetActive(true);

        EventSystem.current.SetSelectedGameObject(nextStageObject.gameObject);
    }

    private IEnumerator MoveTimerToCorner()
    {
        gameTimerObject.SetActive(true);

        approvedStampObject.SetActive(false);

        firstStar.color = new Color(0.4f, 0.4f, 0.4f);
        secondStar.color = new Color(0.4f, 0.4f, 0.4f); 
        thirdStar.color = new Color(0.4f, 0.4f, 0.4f);

        firstStarRequirement.text  = levelConfig.star1Score.ToString();
        secondStarRequirement.text = levelConfig.star2Score.ToString();
        thirdStarRequirement.text  = levelConfig.star3Score.ToString();

        RectTransform rectTransform = gameTimerObject.GetComponent<RectTransform>();

        Vector3 startPos = new(Screen.width / 2f, Screen.height / 2f, 0f);
        Vector3 endPos = new(Screen.width - 150f, 80f, 0f);

        rectTransform.position = startPos;
        rectTransform.localScale = Vector3.one * 1.5f;

        yield return new WaitForSeconds(0.75f);

        float duration = 1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            rectTransform.position = Vector3.Lerp(startPos, endPos, t);

            rectTransform.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.one, t);

            yield return null;
        }

        rectTransform.position = endPos;
        rectTransform.localScale = Vector3.one;
    }

    private IEnumerator AnimateGameOverScale()
    {

        gameOverObject.SetActive(true);

        RectTransform rect = gameOverObject.GetComponent<RectTransform>();
        Vector3 startScale = Vector3.one * 0.1f;
        Vector3 endScale = Vector3.one * 3f;
        float duration = 1f;
        float elapsed = 0f;

        rect.localScale = startScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            rect.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }

        rect.localScale = endScale;
    }

    private IEnumerator SlideVisaObjectFromTop()
    {

        visaObject.SetActive(true);

        RectTransform rect = visaObject.GetComponent<RectTransform>();

        Vector2 startPos = new Vector2(0f, Screen.height); 
        Vector2 endPos = Vector2.zero; 

        float duration = 2f;
        float elapsed = 0f;

        rect.anchoredPosition = startPos;
        rect.localScale = Vector3.one;
        visaObject.SetActive(true);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        rect.anchoredPosition = endPos;
    }

    private IEnumerator SpawnAirplane()
    {
        int randomNumber = Random.Range(1, 15);
        if (randomNumber == 1)
        {
            airplaneObject = Instantiate(airplanePrefab, spawnPosition, Quaternion.identity);
            randomSpeed = Random.Range(10f, 40f);
            yield return StartCoroutine(MoveAirplane(airplaneObject, randomSpeed));
        }

        else yield return new WaitForSeconds(1f);

        StartCoroutine(SpawnAirplane());
    }

    private IEnumerator MoveAirplane(GameObject airplane, float speed)
    {
        while (gameStarted && Vector3.Distance(airplane.transform.position, destroyPosition) > 0.1f)
        {
            airplane.transform.position = Vector3.MoveTowards(
                airplane.transform.position,
                destroyPosition,
                speed * Time.deltaTime
            );
            
            yield return null;
        }

        Destroy(airplane);
    }

    public void BackToLobby()
    {
        SceneManager.LoadScene(0);
    }

}
