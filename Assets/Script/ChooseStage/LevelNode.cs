using UnityEngine;

/// <summary>
/// Stage-select presentation for one LevelConfig. Progression data belongs to the
/// save service; this component only reflects it in the map scene.
/// </summary>
public class LevelNode : MonoBehaviour
{
    [SerializeField] private LevelConfig level;
    [SerializeField] private GameObject unlockedVisual;
    [SerializeField] private GameObject lockedVisual;

    public LevelConfig Level => level;
    public string DisplayName => level != null ? level.displayName : "Unconfigured Level";
    public string Description => level != null ? level.description : string.Empty;
    public bool IsUnlocked => level != null
        && (ProgressionService.Instance == null
            ? level.unlockedByDefault
            : ProgressionService.Instance.IsUnlocked(level));
    public int BestStars => level != null && ProgressionService.Instance != null
        ? ProgressionService.Instance.GetBestStars(level)
        : 0;
    public int BestScore => level != null && ProgressionService.Instance != null
        ? ProgressionService.Instance.GetBestScore(level)
        : 0;
    public int LastScore => level != null && ProgressionService.Instance != null
        ? ProgressionService.Instance.GetLastScore(level)
        : 0;

    private void OnEnable()
    {
        if (ProgressionService.Instance != null)
            ProgressionService.Instance.ProgressChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (ProgressionService.Instance != null)
            ProgressionService.Instance.ProgressChanged -= Refresh;
    }

    public void Refresh()
    {
        bool unlocked = IsUnlocked;
        if (unlockedVisual != null)
            unlockedVisual.SetActive(unlocked);
        if (lockedVisual != null)
            lockedVisual.SetActive(!unlocked);
    }
}
