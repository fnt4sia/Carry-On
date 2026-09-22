using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Lobby controller. The camera never moves: one fixed pose looks at the menu banner and
// the character lineup at the same time. The banner's world-space canvas is the whole
// menu — it opens showing only a "press to join" line, and the first player to press
// (keyboard half or gamepad) swaps that for the departures list. Each player takes the next
// authored lobby slot, in join order.
//
// Every element referenced here is authored in the scene (Assets/Scenes/Menu/MainMenu)
// or in BoardMenuRow.prefab. This script only toggles and fills what already exists.
public class MainMenuManager : MonoBehaviour
{
    [Header("Player Lineup")]
    [Tooltip("One authored stand per seat, in join order. Position and rotation are both used.")]
    [SerializeField] private Transform[] lobbySlots = new Transform[4];

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

    [Header("Lobby HUD")]
    [SerializeField] private TeamPanel teamPanel;       // bottom-of-screen seat strip, hidden until someone joins

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

        RefreshTeamPanel();
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
        RefreshTeamPanel();
    }

    // The seat strip only exists once someone has joined; before that the banner's join prompt
    // is the whole screen.
    private void RefreshTeamPanel()
    {
        if (teamPanel == null) return;

        teamPanel.gameObject.SetActive(joinedPlayers.Count > 0);
        teamPanel.Refresh(joinedPlayers);
    }

    // Stand each joined character on its own authored slot, in join order. Where a character
    // stands and which way it faces are both the slot's — re-pose the lobby by moving the slot
    // objects in the scene, never here. Slots sit on the floor: the character models' pivots
    // are their soles.
    private void UpdatePositions()
    {
        if (lobbySlots == null) return;

        for (int i = 0; i < joinedPlayers.Count && i < lobbySlots.Length; i++)
        {
            Transform slot = lobbySlots[i];
            if (slot == null) continue;

            joinedPlayers[i].transform.SetPositionAndRotation(slot.position, slot.rotation);
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
