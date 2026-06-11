using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Lobby controller. Opens on a "press to start" gate with the camera looking away; the
// first player to press (keyboard half or gamepad) sets the input mode, swings the
// camera round to the menu, reveals the UI and spawns as the first character. Further
// players join into a camera-facing lineup that re-centres as players join.
//
// Top-right slot cards: one card per joined player (number, device, colour) plus a
// single translucent "press to join" card that lists only the join options still free
// (keyboard halves / gamepads) — it disappears when nothing is left.
public class MainMenuManager : MonoBehaviour
{
    [Header("Player Lineup")]
    [SerializeField] private Transform spawnCenter;     // row centre, on the floor, +X = row axis
    [SerializeField] private Camera lobbyCamera;        // characters face this
    [SerializeField] private float spacing = 2.6f;      // max world units between characters (few players)
    [SerializeField] private float bandWidth = 4.6f;    // row never spreads wider than this (many players pack in)
    [SerializeField] private float lobbyScale = 0.55f;  // character display scale in the lobby
    [SerializeField] private float feetPivotOffset = 1.005f; // model feet sit this far below pivot at scale 1

    [Header("Player Slots")]
    [SerializeField] private Transform playerPanel;
    [SerializeField] private GameObject playerUIPrefab;     // card: Title / Subtitle / ColorBar
    [SerializeField] private GameObject joinHintRoot;       // "press to join" card inside the panel
    [SerializeField] private TMP_Text joinHintDevices;      // available options, e.g. "Space · Right Ctrl · (A)"

    [Header("Start Gate / Intro")]
    [SerializeField] private GameObject startPrompt;        // "Press ... to Start"
    [SerializeField] private GameObject[] menuObjects;      // MenuPanel, PlayerPanel — shown after start
    [SerializeField] private InputModeManager inputModeManager;
    [SerializeField] private Transform startView;           // camera pose before start (looking away)
    [SerializeField] private Transform menuView;            // camera pose framing the lineup
    [SerializeField] private float introDuration = 1f;

    private static readonly Color[] playerColors =
    {
        new Color(0.31f, 0.64f, 0.89f), // P1 blue
        new Color(0.89f, 0.44f, 0.31f), // P2 orange
        new Color(0.44f, 0.75f, 0.35f), // P3 green
        new Color(0.89f, 0.77f, 0.31f), // P4 yellow
    };

    private readonly List<PlayerInput> joinedPlayers = new();
    private PlayerInputManager manager;
    private bool started;

    private void Start()
    {
        manager = PlayerSystem.Instance != null
            ? PlayerSystem.Instance.Manager
            : FindFirstObjectByType<PlayerInputManager>();

        if (manager != null)
            manager.onPlayerJoined += HandlePlayerJoined;

        var existingPlayers = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);

        if (existingPlayers.Length > 0)
        {
            // Returning to the lobby with players already joined: skip the gate and the
            // intro swing — snap straight to the menu view.
            RevealMenu(existingPlayers[0]);
            if (lobbyCamera != null && menuView != null)
                lobbyCamera.transform.SetPositionAndRotation(menuView.position, menuView.rotation);

            System.Array.Sort(existingPlayers, (a, b) => a.playerIndex.CompareTo(b.playerIndex));
            foreach (var player in existingPlayers)
                HandlePlayerJoined(player);
        }
        else
        {
            ShowStartGate();
        }

        RefreshJoinHint();
    }

    private void OnDestroy()
    {
        if (manager != null)
            manager.onPlayerJoined -= HandlePlayerJoined;
    }

    private void Update() => RefreshJoinHint();

    private void ShowStartGate()
    {
        started = false;
        if (startPrompt != null) startPrompt.SetActive(true);
        SetMenuObjectsActive(false);

        if (inputModeManager != null) inputModeManager.enabled = false;
        Cursor.visible = false; // attract screen

        if (lobbyCamera != null && startView != null)
            lobbyCamera.transform.SetPositionAndRotation(startView.position, startView.rotation);
    }

    // True first join: reveal the menu, pick the input mode, swing the camera round.
    private void EnterMenuState(PlayerInput firstPlayer)
    {
        if (started) return;
        RevealMenu(firstPlayer);
        StartCoroutine(CameraIntro());
    }

    // Show the menu UI and choose the initial input mode from the first device.
    // Keyboard player -> Pointer (cursor + mouse), gamepad player -> Navigation (highlight).
    private void RevealMenu(PlayerInput firstPlayer)
    {
        started = true;

        if (startPrompt != null) startPrompt.SetActive(false);
        SetMenuObjectsActive(true);

        if (inputModeManager != null)
        {
            inputModeManager.enabled = true;
            bool keyboard = firstPlayer != null && firstPlayer.currentControlScheme != null
                && firstPlayer.currentControlScheme.StartsWith("Keyboard");
            inputModeManager.SetInitialMode(keyboard
                ? InputModeManager.Mode.Pointer
                : InputModeManager.Mode.Navigation);
        }
    }

    private IEnumerator CameraIntro()
    {
        if (lobbyCamera == null || startView == null || menuView == null)
            yield break;

        Transform cam = lobbyCamera.transform;
        Vector3 fromPos = startView.position;
        Quaternion fromRot = startView.rotation;
        float elapsed = 0f;

        while (elapsed < introDuration)
        {
            float t = elapsed / introDuration;
            t = t * t * (3f - 2f * t); // smoothstep
            cam.SetPositionAndRotation(
                Vector3.Lerp(fromPos, menuView.position, t),
                Quaternion.Slerp(fromRot, menuView.rotation, t));
            elapsed += Time.deltaTime;
            yield return null;
        }

        cam.SetPositionAndRotation(menuView.position, menuView.rotation);
    }

    private void HandlePlayerJoined(PlayerInput player)
    {
        if (joinedPlayers.Contains(player)) return;

        joinedPlayers.Add(player);
        DontDestroyOnLoad(player.gameObject);
        player.gameObject.SetActive(true);
        FreezeForLobby(player, true);

        if (!started)
            EnterMenuState(player);

        AddPlayerCard(player);
        UpdatePositions();
        RefreshJoinHint();
    }

    private void AddPlayerCard(PlayerInput player)
    {
        if (playerUIPrefab == null || playerPanel == null) return;

        GameObject ui = Instantiate(playerUIPrefab, playerPanel);

        foreach (var text in ui.GetComponentsInChildren<TMP_Text>())
        {
            if (text.name == "Title") text.text = $"Player {player.playerIndex + 1}";
            else if (text.name == "Subtitle") text.text = SchemeLabel(player.currentControlScheme);
        }

        Transform bar = ui.transform.Find("ColorBar");
        if (bar != null && bar.TryGetComponent(out Image barImage))
            barImage.color = playerColors[Mathf.Clamp(player.playerIndex, 0, playerColors.Length - 1)];

        // Keep the join-hint card at the end of the row.
        if (joinHintRoot != null && joinHintRoot.transform.parent == playerPanel)
            joinHintRoot.transform.SetAsLastSibling();
    }

    private static string SchemeLabel(string scheme) => scheme switch
    {
        PlayerSystem.SchemeKeyboardLeft  => "Keyboard · WASD",
        PlayerSystem.SchemeKeyboardRight => "Keyboard · Arrows",
        PlayerSystem.SchemeGamepad       => "Gamepad",
        _ => scheme,
    };

    // Show the join card only while something can still join, listing just the free options.
    private void RefreshJoinHint()
    {
        if (joinHintRoot == null) return;

        bool show = started && joinedPlayers.Count < 4;
        string options = "";

        if (show)
        {
            var parts = new List<string>(3);
            if (Keyboard.current != null)
            {
                if (PlayerSystem.IsSchemeFree(PlayerSystem.SchemeKeyboardLeft)) parts.Add("Space");
                if (PlayerSystem.IsSchemeFree(PlayerSystem.SchemeKeyboardRight)) parts.Add("Right Ctrl");
            }
            if (PlayerSystem.FreeGamepadCount() > 0) parts.Add("(A)");

            show = parts.Count > 0;
            options = string.Join("  ·  ", parts);
        }

        if (joinHintRoot.activeSelf != show)
            joinHintRoot.SetActive(show);
        if (show && joinHintDevices != null && joinHintDevices.text != options)
            joinHintDevices.text = options;
    }

    // Lay the joined characters out as a centred row facing the lobby camera.
    private void UpdatePositions()
    {
        if (spawnCenter == null) return;

        int count = joinedPlayers.Count;
        Vector3 axis = spawnCenter.right;
        float feetY = spawnCenter.position.y + feetPivotOffset * lobbyScale;

        // Spread out for few players, pack tighter so the row never exceeds bandWidth.
        float effSpacing = count > 1 ? Mathf.Min(spacing, bandWidth / (count - 1)) : 0f;

        for (int i = 0; i < count; i++)
        {
            float offset = (i - (count - 1) / 2f) * effSpacing;
            Vector3 pos = spawnCenter.position + axis * offset;
            pos.y = feetY;

            Transform t = joinedPlayers[i].transform;
            t.position = pos;
            t.localScale = Vector3.one * lobbyScale;

            if (lobbyCamera != null)
            {
                Vector3 look = lobbyCamera.transform.position - pos;
                look.y = 0;
                if (look.sqrMagnitude > 0.0001f)
                    t.rotation = Quaternion.LookRotation(look);
            }
        }
    }

    private void SetMenuObjectsActive(bool active)
    {
        if (menuObjects == null) return;
        foreach (var go in menuObjects)
            if (go != null) go.SetActive(active);
    }

    // Freeze the rigidbody so the character holds its exact lobby pose (no gravity/slide).
    // Restored before entering gameplay.
    private static void FreezeForLobby(PlayerInput player, bool frozen)
    {
        if (player == null) return;
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb == null) return;

        // Zero velocity while still dynamic — can't set it on a kinematic body.
        if (frozen && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = frozen;
    }

    public void OnClickStart()
    {
        // The press that just joined a player must not also activate the button.
        if (PlayerSystem.Instance != null && PlayerSystem.Instance.LastJoinFrame == Time.frameCount)
            return;

        // Need at least one joined player, otherwise the stage would have no characters.
        if (joinedPlayers.Count < 1)
        {
            AudioManager.Instance?.PlaySFX("wrong");
            return;
        }

        PlayButtonSelectSfx();

        // Un-freeze so gameplay physics resume.
        foreach (var player in joinedPlayers)
            FreezeForLobby(player, false);

        SceneManager.LoadScene("ChooseStage");
    }

    public void OnClickOptions()
    {
        PlayButtonSelectSfx();
    }

    public void OnClickExit()
    {
        PlayButtonSelectSfx();
        Application.Quit();
    }

    private static void PlayButtonSelectSfx()
    {
        AudioManager.Instance?.PlaySFX("Button Select");
    }
}
