using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persistent asynchronous scene transition service. A swappable Resources prefab
/// may provide the loading view; a code-built fallback keeps direct scene testing safe.
/// </summary>
public class SceneLoader : SingletonBehaviour<SceneLoader>
{
    private const string ResourcePath = "Runtime/SceneLoader";

    [Header("Loading View")]
    [SerializeField] private CanvasGroup loadingCanvas;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private Image progressFill;
    [SerializeField, Min(0f)] private float fadeDuration = 0.2f;

    private bool isLoading;

    protected override bool PersistAcrossScenes => true;
    public bool IsLoading => isLoading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        SceneLoader prefab = Resources.Load<SceneLoader>(ResourcePath);
        if (prefab != null)
            Instantiate(prefab);
        else
            new GameObject(nameof(SceneLoader)).AddComponent<SceneLoader>();
    }

    protected override void OnSingletonAwake()
    {
        if (loadingCanvas == null)
            BuildFallbackView();

        loadingCanvas.alpha = 0f;
        loadingCanvas.blocksRaycasts = false;
        loadingCanvas.interactable = false;
        loadingCanvas.gameObject.SetActive(false);
    }

    public static bool Load(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("Cannot load a scene with an empty name.");
            return false;
        }

        if (Instance == null)
            Bootstrap();

        return Instance != null && Instance.BeginLoad(sceneName);
    }

    public static bool LoadLobby() => Load(SceneNames.MainMenu);
    public static bool LoadStageSelect() => Load(SceneNames.ChooseStage);

    private bool BeginLoad(string sceneName)
    {
        if (isLoading)
            return false;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"Scene '{sceneName}' is not enabled in Build Settings.");
            return false;
        }

        StartCoroutine(LoadRoutine(sceneName));
        return true;
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        isLoading = true;
        loadingCanvas.gameObject.SetActive(true);
        loadingCanvas.blocksRaycasts = true;
        loadingCanvas.interactable = true;
        if (loadingText != null)
            loadingText.text = "Loading…";
        SetProgress(0f);

        yield return Fade(0f, 1f);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            isLoading = false;
            yield return Fade(1f, 0f);
            loadingCanvas.gameObject.SetActive(false);
            yield break;
        }

        operation.allowSceneActivation = false;
        while (operation.progress < 0.9f)
        {
            SetProgress(Mathf.Clamp01(operation.progress / 0.9f));
            yield return null;
        }

        SetProgress(1f);
        operation.allowSceneActivation = true;
        while (!operation.isDone)
            yield return null;

        Time.timeScale = 1f;
        yield return Fade(1f, 0f);

        loadingCanvas.blocksRaycasts = false;
        loadingCanvas.interactable = false;
        loadingCanvas.gameObject.SetActive(false);
        isLoading = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeDuration <= 0f)
        {
            loadingCanvas.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            loadingCanvas.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }

        loadingCanvas.alpha = to;
    }

    private void SetProgress(float value)
    {
        if (progressFill != null)
            progressFill.fillAmount = value;
    }

    private void BuildFallbackView()
    {
        GameObject canvasObject = new("LoadingScreen");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
        loadingCanvas = canvasObject.AddComponent<CanvasGroup>();

        GameObject background = new("Background");
        background.transform.SetParent(canvasObject.transform, false);
        RectTransform backgroundRect = background.AddComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(0.035f, 0.05f, 0.08f, 1f);

        GameObject textObject = new("LoadingText");
        textObject.transform.SetParent(background.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.25f, 0.42f);
        textRect.anchorMax = new Vector2(0.75f, 0.58f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        loadingText = textObject.AddComponent<TextMeshProUGUI>();
        loadingText.alignment = TextAlignmentOptions.Center;
        loadingText.fontSize = 42f;
        loadingText.text = "Loading…";
    }
}
