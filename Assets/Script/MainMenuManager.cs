using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{

    [Header("Buttons")]
    [SerializeField] private Button defaultButton;

    [Header("Player UI")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform playerPanel;  
    [SerializeField] private GameObject playerUIPrefab;

    private readonly List<PlayerInput> joinedPlayers = new();
    private bool uiModeLocked = false;

    private PlayerInputManager manager;

    private void Start()
    {
        manager = FindFirstObjectByType<PlayerInputManager>();

        if (manager != null)
            manager.onPlayerJoined += HandlePlayerJoined;

        var existingPlayers = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);

        foreach (var player in existingPlayers)
        {
            HandlePlayerJoined(player);
        }
    }

    private void HandlePlayerJoined(PlayerInput player)
    {
        if (joinedPlayers.Contains(player))
            return;

        joinedPlayers.Add(player);

        GameObject ui = Instantiate(playerUIPrefab, playerPanel);

        TMP_Text text = ui.GetComponentInChildren<TMP_Text>();
        text.text = $"Player {player.playerIndex + 1}";

        if (!uiModeLocked)
        {
            SetUIMode(player);
            uiModeLocked = true;
        }
    }

    private void SetUIMode(PlayerInput player)
    {
        if (player.devices[0] is Gamepad)
        {
            Cursor.visible = false;
        }
        else
        {
            Cursor.visible = true;
        }
    }

    public void OnClickStart()
    {
    }

    public void OnBackmenu()
    {
    }

    public void OnClickOptions()
    {
    }

    public void OnClickExit()
    {
        Application.Quit();
    }

    public void LoadStage(int level)
    {
        SceneManager.LoadScene(level);
    }
}