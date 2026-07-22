using UnityEngine;

/// <summary>
/// The one intentionally scene-owned reference for level-specific data.
/// Reusable gameplay prefabs read this context instead of storing per-stage overrides.
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class LevelContext : SingletonBehaviour<LevelContext>
{
    [SerializeField] private LevelConfig levelConfig;

    public LevelConfig Config => levelConfig;
    public static LevelConfig CurrentConfig => Instance != null ? Instance.levelConfig : null;

    public static bool TryGetConfig(out LevelConfig config)
    {
        config = CurrentConfig;
        return config != null;
    }

    protected override void OnSingletonAwake()
    {
        if (levelConfig == null)
            Debug.LogError($"{nameof(LevelContext)} on '{name}' needs a {nameof(LevelConfig)}.", this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (levelConfig == null)
            return;

        string activeSceneName = gameObject.scene.name;
        if (!string.IsNullOrEmpty(activeSceneName) &&
            !string.IsNullOrWhiteSpace(levelConfig.sceneName) &&
            levelConfig.sceneName != activeSceneName)
        {
            Debug.LogWarning(
                $"{nameof(LevelContext)} in '{activeSceneName}' references '{levelConfig.name}', " +
                $"which targets scene '{levelConfig.sceneName}'.",
                this);
        }
    }
#endif
}
