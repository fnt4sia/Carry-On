using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Persistent owner of the PlayerInputManager. Lives in MainMenu and survives scene
// loads, so joined players keep their device + control scheme and a stable playerIndex
// across lobby -> ChooseStage -> stage.
//
// Joining is manual (not PlayerInputManager auto-join) so one keyboard can host two
// players on separate control schemes:
//   Space       -> KeyboardLeft  (WASD half)
//   Right Shift -> KeyboardRight (arrows half)
//   (A) south   -> Gamepad       (any gamepad not yet paired)
// A slot only joins while it is still free, so what's "available" is detected
// automatically. Joining is polled only in the lobby.
//
// Runs before the EventSystem so a join press is registered before UI submit fires
// on the same frame (see LastJoinFrame).
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlayerInputManager))]
public class PlayerSystem : SingletonBehaviour<PlayerSystem>
{
    private const string ResourcePath = "Runtime/PlayerSystem";

    public const string SchemeKeyboardLeft  = "KeyboardLeft";
    public const string SchemeKeyboardRight = "KeyboardRight";
    public const string SchemeGamepad       = "Gamepad";

    [SerializeField] private string lobbySceneName = "MainMenu";

    [Header("Characters")]
    [SerializeField, Tooltip("Bodies handed out in join order (first player gets element 0, " +
        "wrapping around). Set these instead of the PlayerInputManager's hidden Player " +
        "Prefab field.")]
    private GameObject[] characterPrefabs;

    private PlayerInputManager manager;
    private bool joinAllowed;

    // The surviving (persisted) PlayerInputManager. Always read this instead of
    // FindObjectOfType: on back-to-lobby a duplicate is created and destroyed, but
    // Destroy is deferred a frame, so a Find could return the doomed copy.
    public PlayerInputManager Manager => manager;

    // Frame of the most recent join. UI button handlers check this so the button
    // press that joined a player doesn't also activate the selected button.
    public int LastJoinFrame { get; private set; } = -1;

    protected override bool PersistAcrossScenes => true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        PlayerSystem prefab = Resources.Load<PlayerSystem>(ResourcePath);
        if (prefab != null)
            Instantiate(prefab);
    }

    protected override void OnSingletonAwake()
    {
        manager = GetComponent<PlayerInputManager>();

        // Visible override for the manager's hidden Player Prefab field (hidden because
        // Join Behavior = Manual). Runs before any JoinPlayer call; JoinPlayer swaps in
        // the next roster body right before each join.
        if (characterPrefabs != null && characterPrefabs.Length > 0)
            manager.playerPrefab = characterPrefabs[0];

        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyJoinState(SceneManager.GetActiveScene().name);
    }

    protected override void OnSingletonDestroyed()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyJoinState(scene.name);

    private void ApplyJoinState(string sceneName) => joinAllowed = sceneName == lobbySceneName;

    private void Update()
    {
        if (!joinAllowed || manager == null) return;
        if (PlayerInput.all.Count >= manager.maxPlayerCount) return;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame && IsSchemeFree(SchemeKeyboardLeft))
            {
                Join(SchemeKeyboardLeft, kb);
                return;
            }
            if (kb.rightShiftKey.wasPressedThisFrame && IsSchemeFree(SchemeKeyboardRight))
            {
                Join(SchemeKeyboardRight, kb);
                return;
            }
        }

        foreach (var pad in Gamepad.all)
        {
            if (pad.buttonSouth.wasPressedThisFrame && !IsDeviceTaken(pad))
            {
                Join(SchemeGamepad, pad);
                return;
            }
        }
    }

    private void Join(string scheme, InputDevice device)
    {
        var player = JoinPlayer(scheme, device);
        if (player != null)
            LastJoinFrame = Time.frameCount;
    }

    // Single join entry point (lobby polling and DebugAutoJoin both come through here),
    // so every joined player gets the next character body in roster order.
    public PlayerInput JoinPlayer(string scheme, InputDevice device)
    {
        if (characterPrefabs != null && characterPrefabs.Length > 0)
            manager.playerPrefab = characterPrefabs[PlayerInput.all.Count % characterPrefabs.Length];
        return manager.JoinPlayer(-1, -1, scheme, device);
    }

    // A keyboard half is free while no player uses that scheme (the keyboard device
    // itself is shared between both halves).
    public static bool IsSchemeFree(string scheme)
    {
        foreach (var p in PlayerInput.all)
            if (p.currentControlScheme == scheme)
                return false;
        return true;
    }

    public static bool IsDeviceTaken(InputDevice device)
    {
        foreach (var p in PlayerInput.all)
            foreach (var d in p.devices)
                if (d == device)
                    return true;
        return false;
    }

    public static int FreeGamepadCount()
    {
        int count = 0;
        foreach (var pad in Gamepad.all)
            if (!IsDeviceTaken(pad))
                count++;
        return count;
    }
}
