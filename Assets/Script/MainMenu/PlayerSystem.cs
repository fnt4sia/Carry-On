using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Persistent owner of the PlayerInputManager. Replaces the old Boot scene: it lives in
// MainMenu and survives scene loads via DontDestroyOnLoad, so joined players keep their
// device + control-scheme assignment and a stable playerIndex across the
// lobby -> ChooseStage -> stage flow. Joining is only allowed while in the lobby.
[RequireComponent(typeof(PlayerInputManager))]
public class PlayerSystem : MonoBehaviour
{
    public static PlayerSystem Instance { get; private set; }

    [SerializeField] private string lobbySceneName = "MainMenu";

    private PlayerInputManager manager;

    // The surviving (persisted) PlayerInputManager. Always read this instead of
    // FindObjectOfType: on back-to-lobby a duplicate is created and destroyed, but
    // Destroy is deferred a frame, so a Find could return the doomed copy.
    public PlayerInputManager Manager => manager;

    private void Awake()
    {
        // Singleton: when MainMenu reloads (back-to-lobby), the fresh scene copy
        // destroys itself and the original persisted instance keeps the players.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        manager = GetComponent<PlayerInputManager>();

        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyJoinState(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyJoinState(scene.name);

    private void ApplyJoinState(string sceneName)
    {
        if (manager == null) return;

        if (sceneName == lobbySceneName)
            manager.EnableJoining();
        else
            manager.DisableJoining();
    }
}
