using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Persistent owner of the PlayerInputManager. Lives in MainMenu and survives scene
// loads, so joined players keep their device + control scheme and a stable playerIndex
// across lobby -> ChooseStage -> stage.
//
// Joining is manual (not PlayerInputManager auto-join) so one keyboard can host two
// players on separate control schemes:
//   Space      -> KeyboardLeft  (WASD half)
//   Right Ctrl -> KeyboardRight (arrows half)
//   (A) south  -> Gamepad       (any gamepad not yet paired)
// A slot only joins while it is still free, so what's "available" is detected
// automatically. Joining is polled only in the lobby.
//
// Runs before the EventSystem so a join press is registered before UI submit fires
// on the same frame (see LastJoinFrame).
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlayerInputManager))]
public class PlayerSystem : MonoBehaviour
{
    public const string SchemeKeyboardLeft  = "KeyboardLeft";
    public const string SchemeKeyboardRight = "KeyboardRight";
    public const string SchemeGamepad       = "Gamepad";

    public static PlayerSystem Instance { get; private set; }

    [SerializeField] private string lobbySceneName = "MainMenu";

    private PlayerInputManager manager;
    private bool joinAllowed;

    // The surviving (persisted) PlayerInputManager. Always read this instead of
    // FindObjectOfType: on back-to-lobby a duplicate is created and destroyed, but
    // Destroy is deferred a frame, so a Find could return the doomed copy.
    public PlayerInputManager Manager => manager;

    // Frame of the most recent join. UI button handlers check this so the button
    // press that joined a player doesn't also activate the selected button.
    public int LastJoinFrame { get; private set; } = -1;

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
            if (kb.rightCtrlKey.wasPressedThisFrame && IsSchemeFree(SchemeKeyboardRight))
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
        var player = manager.JoinPlayer(-1, -1, scheme, device);
        if (player != null)
            LastJoinFrame = Time.frameCount;
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
