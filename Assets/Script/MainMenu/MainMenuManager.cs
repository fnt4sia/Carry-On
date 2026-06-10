using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Lobby controller. Opens on a "press to start" gate with the camera looking away; the
// first player to press (keyboard or gamepad) sets the input mode, swings the camera
// round to the menu, reveals the UI and spawns as the first character. Further players
// join into a camera-facing lineup that re-centres as players join/leave. Start routes
// to ChooseStage.
public class MainMenuManager : MonoBehaviour
{
    [Header("Player Lineup")]
    [SerializeField] private Transform spawnCenter;     // row centre, on the floor, +X = row axis
    [SerializeField] private Camera lobbyCamera;        // characters face this
    [SerializeField] private float spacing = 1.6f;      // world units between characters
    [SerializeField] private float lobbyScale = 0.55f;  // character display scale in the lobby
    [SerializeField] private float feetPivotOffset = 1.005f; // model feet sit this far below pivot at scale 1

    [Header("Player Cards")]
    [SerializeField] private Transform playerPanel;
    [SerializeField] private GameObject playerUIPrefab;

    [Header("Start Gate / Intro")]
    [SerializeField] private GameObject startPrompt;       // "Press Space / (A) to Start"
    [SerializeField] private GameObject[] menuObjects;     // MenuPanel, PlayerPanel — shown after start
    [SerializeField] private GameObject joinPrompt;        // "Press to Join" — shown after start when < 4
    [SerializeField] private InputModeManager inputModeManager;
    [SerializeField] private Transform startView;          // camera pose before start (looking away)
    [SerializeField] private Transform menuView;           // camera pose framing the lineup
    [SerializeField] private float introDuration = 1f;

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
    }

    private void OnDestroy()
    {
        if (manager != null)
            manager.onPlayerJoined -= HandlePlayerJoined;
    }

    private void ShowStartGate()
    {
        started = false;
        if (startPrompt != null) startPrompt.SetActive(true);
        SetMenuObjectsActive(false);
        if (joinPrompt != null) joinPrompt.SetActive(false);

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
    // Keyboard/mouse player -> Pointer (cursor), gamepad player -> Navigation (highlight).
    private void RevealMenu(PlayerInput firstPlayer)
    {
        started = true;

        if (startPrompt != null) startPrompt.SetActive(false);
        SetMenuObjectsActive(true);

        if (inputModeManager != null)
        {
            inputModeManager.enabled = true;
            bool keyboard = firstPlayer != null && firstPlayer.currentControlScheme == "Keyboard";
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

        if (playerUIPrefab != null && playerPanel != null)
        {
            GameObject ui = Instantiate(playerUIPrefab, playerPanel);
            TMP_Text text = ui.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = $"Player {player.playerIndex + 1}";
        }

        UpdatePositions();
        UpdateJoinPrompt();
    }

    // Lay the joined characters out as a centred row facing the lobby camera.
    private void UpdatePositions()
    {
        if (spawnCenter == null) return;

        int count = joinedPlayers.Count;
        Vector3 axis = spawnCenter.right;
        float feetY = spawnCenter.position.y + feetPivotOffset * lobbyScale;

        for (int i = 0; i < count; i++)
        {
            float offset = (i - (count - 1) / 2f) * spacing;
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

    private void UpdateJoinPrompt()
    {
        if (joinPrompt == null) return;
        joinPrompt.SetActive(started && joinedPlayers.Count < 4);
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
        // Need at least one joined player, otherwise the stage would have no characters.
        if (joinedPlayers.Count < 1)
        {
            AudioManager.Instance?.PlaySFX("wrong");
            return;
        }

        PlayButtonSelectSfx();

        // Un-freeze so gameplay physics resume, and stop accepting new joiners.
        foreach (var player in joinedPlayers)
            FreezeForLobby(player, false);

        if (manager != null)
            manager.DisableJoining();

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
