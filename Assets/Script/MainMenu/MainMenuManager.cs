using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Lobby controller. The camera never moves: one fixed pose looks at the departure board
// and the character lineup at the same time. The board's world-space canvas is the whole
// menu — it opens showing only a "press to join" line, and the first player to press
// (keyboard half or gamepad) swaps that for the departures list. Further players join
// into a lineup that re-centres as they arrive.
//
// Every element referenced here is authored in the scene (Assets/Scenes/Menu/MainMenu)
// or in BoardMenuRow.prefab. This script only toggles and fills what already exists.
public class MainMenuManager : MonoBehaviour
{
    [Header("Player Lineup")]
    [SerializeField] private Transform spawnCenter;     // row centre, on the floor, +X = row axis
    [SerializeField] private Camera lobbyCamera;        // characters turn to face this
    [SerializeField] private float spacing = 2.6f;      // max world units between characters (few players)
    [SerializeField] private float bandWidth = 4.6f;    // row never spreads wider than this (many players pack in)
    [SerializeField] private float lobbyScale = 0.55f;  // character display scale in the lobby
    [SerializeField] private float feetPivotOffset = 1.005f; // model feet sit this far below pivot at scale 1

    [Header("Departure Board")]
    [SerializeField] private GameObject joinPrompt;     // "Press ... to Join", shown before anyone joins
    [SerializeField] private GameObject menuRoot;       // columns + rows, shown after the first join
    [SerializeField] private InputModeManager inputModeManager;

    [Header("Join Hint")]
    [SerializeField] private GameObject joinHintRoot;   // bottom strip, visible while a slot is free
    [SerializeField] private TMP_Text joinHintDevices;  // available options, e.g. "Right Ctrl  ·  (A)"

    [Header("Player List")]
    [SerializeField] private GameObject playerListRoot;    // hidden until the first player joins
    [SerializeField] private PlayerSlotView[] playerSlots; // four avatars, P1..P4 in order

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
            // Returning to the lobby with players already joined: skip the join gate.
            RevealMenu(existingPlayers[0]);

            System.Array.Sort(existingPlayers, (a, b) => a.playerIndex.CompareTo(b.playerIndex));
            foreach (var player in existingPlayers)
                HandlePlayerJoined(player);
        }
        else
        {
            ShowJoinGate();
        }

        RefreshJoinHint();
    }

    private void OnDestroy()
    {
        if (manager != null)
            manager.onPlayerJoined -= HandlePlayerJoined;
    }

    private void Update() => RefreshJoinHint();

    private void ShowJoinGate()
    {
        started = false;
        if (joinPrompt != null) joinPrompt.SetActive(true);
        if (menuRoot != null) menuRoot.SetActive(false);

        if (inputModeManager != null) inputModeManager.enabled = false;
        Cursor.visible = false; // attract screen

        RefreshPlayerList();
    }

    // The avatar strip only exists once somebody is in, then greys out the free slots.
    private void RefreshPlayerList()
    {
        if (playerListRoot != null && playerListRoot.activeSelf != started)
            playerListRoot.SetActive(started);

        if (playerSlots == null) return;
        for (int i = 0; i < playerSlots.Length; i++)
            if (playerSlots[i] != null)
                playerSlots[i].SetJoined(i < joinedPlayers.Count);
    }

    // Show the departures list and choose the initial input mode from the first device.
    // Keyboard player -> Pointer (cursor + mouse), gamepad player -> Navigation (highlight).
    private void RevealMenu(PlayerInput firstPlayer)
    {
        started = true;

        if (joinPrompt != null) joinPrompt.SetActive(false);
        if (menuRoot != null) menuRoot.SetActive(true);

        if (inputModeManager != null)
        {
            inputModeManager.enabled = true;
            bool keyboard = firstPlayer != null && firstPlayer.currentControlScheme != null
                && firstPlayer.currentControlScheme.StartsWith("Keyboard");
            inputModeManager.SetInitialMode(keyboard
                ? InputModeManager.Mode.Pointer
                : InputModeManager.Mode.Navigation);
        }

        RefreshPlayerList();
    }

    private void HandlePlayerJoined(PlayerInput player)
    {
        if (joinedPlayers.Contains(player)) return;

        joinedPlayers.Add(player);
        DontDestroyOnLoad(player.gameObject);
        player.gameObject.SetActive(true);
        FreezeForLobby(player, true);

        if (!started)
            RevealMenu(player);

        UpdatePositions();
        RefreshJoinHint();
        RefreshPlayerList();
    }

    // Show the join hint only while something can still join, listing just the free options.
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

    // ---- Board row handlers, wired on each row's Button in the scene ----

    public void OnClickNewGame()
    {
        // The press that just joined a player must not also activate the selected row.
        if (PlayerSystem.Instance != null && PlayerSystem.Instance.LastJoinFrame == Time.frameCount)
            return;

        // Need at least one joined player, otherwise the stage would have no characters.
        if (joinedPlayers.Count < 1)
        {
            AudioManager.Instance?.PlaySFX(Sfx.Wrong);
            return;
        }

        PlayButtonSelectSfx();

        // Un-freeze so gameplay physics resume.
        foreach (var player in joinedPlayers)
            FreezeForLobby(player, false);

        SceneLoader.LoadStageSelect();
    }

    // Load Game and Settings are selectable and highlight like any other row, but have
    // nowhere to go yet — deliberately silent until there is something to open. The
    // rows stay wired to these so hooking them up later is a one-line change.
    public void OnClickLoadGame() { }

    public void OnClickSettings() { }

    public void OnClickExit()
    {
        if (PlayerSystem.Instance != null && PlayerSystem.Instance.LastJoinFrame == Time.frameCount)
            return;

        PlayButtonSelectSfx();
        Application.Quit();
    }

    private static void PlayButtonSelectSfx()
    {
        AudioManager.Instance?.PlaySFX(Sfx.ButtonSelect);
    }
}
