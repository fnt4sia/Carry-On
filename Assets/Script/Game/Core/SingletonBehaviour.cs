using UnityEngine;

/// <summary>
/// Shared singleton lifecycle for scene services. Subclasses only implement the
/// hooks; duplicate detection and Instance cleanup stay consistent everywhere.
/// </summary>
public abstract class SingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance { get; private set; }
    protected virtual bool PersistAcrossScenes => false;

    /// <summary>
    /// Unity re-enables live components after an Editor domain reload, while their
    /// static fields have been reset. Re-register here without adding lookup cost to
    /// every Instance access.
    /// </summary>
    protected virtual void OnEnable()
    {
        if (Instance == null)
            Instance = this as T;
    }

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this as T;
        if (PersistAcrossScenes)
            DontDestroyOnLoad(gameObject);

        OnSingletonAwake();
    }

    protected virtual void OnDestroy()
    {
        if (Instance != this)
            return;

        OnSingletonDestroyed();
        Instance = null;
    }

    protected virtual void OnSingletonAwake() { }
    protected virtual void OnSingletonDestroyed() { }
}
