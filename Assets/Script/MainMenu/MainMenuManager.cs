using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Lobby controller. The camera never moves: one fixed pose looks at the menu banner and
// the character lineup at the same time. The banner's world-space canvas is the whole
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

    [Header("Menu Banner")]
    [SerializeField] private GameObject joinPrompt;     // "Press ... to Join", shown before anyone joins
    [SerializeField] private GameObject menuRoot;       // columns + rows, shown after the first join
    [SerializeField] private InputModeManager inputModeManager;

    [Header("Panels")]
    [SerializeField] private GameObject savesPanel;     // save list, shared by New Game and Load Game
    [SerializeField] private TMP_Text savesTitle;       // reads NEW GAME or LOAD GAME
    [SerializeField] private BoardMenuRow[] saveSlotRows; // one per save slot, in slot order
    [SerializeField] private GameObject savesBackRow;   // fallback selection when every slot is locked
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private CameraFocus cameraFocus;   // leans the camera in while a panel is open

    [Header("Navigation Defaults")]
    [Tooltip("Row highlighted first in each view when a gamepad is driving.")]
    [SerializeField] private GameObject menuFirstRow;
    [SerializeField] private GameObject settingsFirstRow;

    // Which face of the banner is showing. The join gate is a separate state from the menu
    // because nothing is selectable yet and the camera must stay home.
    private enum View { JoinGate, Menu, Saves, Settings }

    private readonly List<PlayerInput> joinedPlayers = new();
    private PlayerInputManager manager;
    private bool started;

    // Both New Game and Load Game open the same list; this is the only difference between them.
    private bool pickingForNewGame;

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
    }

    private void OnDestroy()
    {
        if (manager != null)
            manager.onPlayerJoined -= HandlePlayerJoined;
    }

    private void ShowJoinGate()
    {
        started = false;
        ShowView(View.JoinGate);

        if (inputModeManager != null) inputModeManager.enabled = false;
        Cursor.visible = false; // attract screen
    }

    // Show the departures list and choose the initial input mode from the first device.
    // Keyboard player -> Pointer (cursor + mouse), gamepad player -> Navigation (highlight).
    private void RevealMenu(PlayerInput firstPlayer)
    {
        started = true;
        ShowView(View.Menu);

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

    // The single place that decides what the banner shows and where the camera sits. Every
    // panel lives on the same canvas, so switching view is only ever SetActive plus a lean.
    private void ShowView(View next)
    {
        if (joinPrompt != null) joinPrompt.SetActive(next == View.JoinGate);
        if (menuRoot != null) menuRoot.SetActive(next == View.Menu);
        if (savesPanel != null) savesPanel.SetActive(next == View.Saves);
        if (settingsPanel != null) settingsPanel.SetActive(next == View.Settings);

        // Zoom in for the panels, back out for the list the lineup is framed with.
        if (cameraFocus != null)
            cameraFocus.SetFocused(next == View.Saves || next == View.Settings);

        if (inputModeManager != null)
            inputModeManager.SetDefaultSelection(next switch
            {
                View.Menu => menuFirstRow,
                View.Saves => FirstSelectableSaveRow(),
                View.Settings => settingsFirstRow,
                _ => null,
            });
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

    public void OnClickNewGame() => OpenSaves(forNewGame: true);

    public void OnClickLoadGame() => OpenSaves(forNewGame: false);

    public void OnClickSettings()
    {
        if (ConsumedByJoin()) return;

        PlayButtonSelectSfx();
        ShowView(View.Settings);
    }

    // Wired on every panel's back row.
    public void OnClickBack()
    {
        if (ConsumedByJoin()) return;

        PlayButtonSelectSfx();
        ShowView(View.Menu);
    }

    public void OnClickExit()
    {
        if (ConsumedByJoin()) return;

        PlayButtonSelectSfx();
        Application.Quit();
    }

    private void OpenSaves(bool forNewGame)
    {
        if (ConsumedByJoin()) return;

        pickingForNewGame = forNewGame;
        PlayButtonSelectSfx();

        if (savesTitle != null)
            savesTitle.text = forNewGame ? "NEW GAME" : "LOAD GAME";

        // Fill before showing: ShowView picks the first selectable row off the interactable
        // states this sets.
        RefreshSaveSlots();
        ShowView(View.Saves);
    }

    // Fills the slot rows off disk. Called on every open so a save written last round shows up.
    //
    // New Game offers every slot — picking a filled one overwrites it. Load Game can only offer
    // slots that actually hold something, so empty rows are left in place but switched
    // non-interactable: still listed, not pressable, and BoardMenuRow draws them with its
    // locked wording.
    private void RefreshSaveSlots()
    {
        if (saveSlotRows == null) return;

        var summaries = ProgressionService.ReadSlotSummaries();

        for (int i = 0; i < saveSlotRows.Length; i++)
        {
            if (saveSlotRows[i] == null || i >= summaries.Length) continue;

            var summary = summaries[i];

            // Set interactable first: SetContent refreshes off it to pick idle vs locked wording.
            Button button = saveSlotRows[i].GetComponent<Button>();
            if (button != null)
                button.interactable = pickingForNewGame || summary.Exists;

            // "SLOT n" is 242 px and the FLIGHT column is 224 — the slot name goes in the wide
            // DESTINATION column, and FLIGHT carries a short code like the menu's C0110 rows.
            saveSlotRows[i].SetContent(
                $"SV{summary.Slot:00}",
                $"SLOT {summary.Slot}",
                summary.Exists ? Count(summary.TotalStars, "STAR") : "EMPTY");
        }
    }

    // Gamepad navigation must not start on a locked row. With no saves at all in Load Game,
    // every slot is locked and Back is the only way out.
    private GameObject FirstSelectableSaveRow()
    {
        if (saveSlotRows != null)
            foreach (BoardMenuRow row in saveSlotRows)
            {
                if (row == null) continue;
                Button button = row.GetComponent<Button>();
                if (button != null && button.interactable) return row.gameObject;
            }

        return savesBackRow;
    }

    private static string Count(int value, string noun) => $"{value} {noun}{(value == 1 ? "" : "S")}";

    // Wired on each slot row with the slot number as the button's int argument.
    public void OnClickSlot(int slot)
    {
        if (ConsumedByJoin()) return;

        // Loading a slot nobody has written yet has nothing to load.
        if (!pickingForNewGame && !ProgressionService.SlotHasData(slot))
        {
            AudioManager.Instance?.PlaySFX(Sfx.Wrong);
            return;
        }

        // Need at least one joined player, otherwise the stage would have no characters.
        if (joinedPlayers.Count < 1)
        {
            AudioManager.Instance?.PlaySFX(Sfx.Wrong);
            return;
        }

        if (ProgressionService.Instance != null)
        {
            if (pickingForNewGame) ProgressionService.Instance.StartNewGame(slot);
            else ProgressionService.Instance.UseSlot(slot);
        }

        PlayButtonSelectSfx();

        // Un-freeze so gameplay physics resume.
        foreach (var player in joinedPlayers)
            FreezeForLobby(player, false);

        SceneLoader.LoadStageSelect();
    }

    // The press that just joined a player must not also activate the selected row.
    private static bool ConsumedByJoin()
        => PlayerSystem.Instance != null && PlayerSystem.Instance.LastJoinFrame == Time.frameCount;

    private static void PlayButtonSelectSfx()
    {
        AudioManager.Instance?.PlaySFX(Sfx.ButtonSelect);
    }
}
