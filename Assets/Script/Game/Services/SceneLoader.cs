using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persistent asynchronous scene transition service. The Resources prefab provides the cloud
/// transition (<see cref="SceneTransition"/>); a code-built fade keeps direct scene testing safe
/// without it.
/// </summary>
public class SceneLoader : SingletonBehaviour<SceneLoader>
{
    private const string ResourcePath = "Runtime/SceneLoader";

    [Header("Loading View")]
    [SerializeField] private CanvasGroup loadingCanvas;
    [Tooltip("Clouds and plane that cover the screen while the next scene loads. Without one the " +
             "loading view just fades in and out.")]
    [SerializeField] private SceneTransition transition;
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

        yield return Cover(IsForward(sceneName));

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation != null)
        {
            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
                yield return null;

            // Reset before activating, so a scene that freezes time on Start (a round's
            // countdown) keeps it frozen.
            Time.timeScale = 1f;
            operation.allowSceneActivation = true;
            while (!operation.isDone)
                yield return null;

            // One frame under the cover, so the new scene's first placement (cameras, spawns)
            // has happened before anyone sees it.
            yield return null;
        }

        yield return Uncover();

        loadingCanvas.blocksRaycasts = false;
        loadingCanvas.interactable = false;
        loadingCanvas.gameObject.SetActive(false);
        isLoading = false;
    }

    private IEnumerator Cover(bool forward)
    {
        if (transition == null)
        {
            yield return Fade(0f, 1f);
            yield break;
        }

        loadingCanvas.alpha = 1f;
        transition.Play(forward);
        while (!transition.IsCovered)
            yield return null;
    }

    private IEnumerator Uncover()
    {
        if (transition == null)
        {
            yield return Fade(1f, 0f);
            yield break;
        }

        transition.Release();
        while (transition.IsPlaying)
            yield return null;
        loadingCanvas.alpha = 0f;
    }

    /// <summary>
    /// Deeper into the game — lobby, map, stages, next stage, replay — flies the plane top-left to
    /// bottom-right; back out flies it the other way. Build Settings lists the scenes in that order.
    /// </summary>
    private static bool IsForward(string sceneName)
    {
        int current = SceneManager.GetActiveScene().buildIndex;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            if (Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i)) == sceneName)
                return i >= current;
        return true;
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
        TextMeshProUGUI loadingText = textObject.AddComponent<TextMeshProUGUI>();
        loadingText.alignment = TextAlignmentOptions.Center;
        loadingText.fontSize = 42f;
        loadingText.text = "Loading…";
    }
}
