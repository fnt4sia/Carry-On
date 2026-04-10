using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button defaultButton;

    [Header("Player UI")]
    [SerializeField] private Transform playerPanel;
    [SerializeField] private GameObject playerUIPrefab;
    [SerializeField] private Transform spawnCenter;
    [SerializeField] private float spacing = 2f;
    [SerializeField] private Camera lobbyCamera;

    [Header("Join Prompt")]
    [SerializeField] private GameObject joinPrompt; // "Press Space/A to join"

    private readonly List<PlayerInput> joinedPlayers = new();
    private PlayerInputManager manager;

    private void Start()
    {
        manager = FindFirstObjectByType<PlayerInputManager>();

        if (manager != null)
            manager.onPlayerJoined += HandlePlayerJoined;

        // Register players that already joined in Boot
        var existingPlayers = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        foreach (var player in existingPlayers)
            HandlePlayerJoined(player);

        UpdateJoinPrompt();
    }

    private void HandlePlayerJoined(PlayerInput player)
    {
        if (joinedPlayers.Contains(player)) return;

        joinedPlayers.Add(player);
        DontDestroyOnLoad(player.gameObject);

        // Show player avatar in lobby
        player.gameObject.SetActive(true);

        // Create UI card
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

    private void UpdatePositions()
    {
        int count = joinedPlayers.Count;
        for (int i = 0; i < count; i++)
        {
            float offset = (i - (count - 1) / 2f) * spacing;
            Vector3 pos = spawnCenter.position + new Vector3(offset, 0, 0);
            joinedPlayers[i].transform.position = pos;

            if (lobbyCamera != null)
            {
                Vector3 lookDir = lobbyCamera.transform.position - joinedPlayers[i].transform.position;
                lookDir.y = 0;
                joinedPlayers[i].transform.rotation = Quaternion.LookRotation(lookDir);
            }

            joinedPlayers[i].transform.localScale = Vector3.one * 0.325f;
        }
    }

    private void UpdateJoinPrompt()
    {
        if (joinPrompt == null) return;
        // Show join prompt only when fewer than 4 players have joined
        joinPrompt.SetActive(joinedPlayers.Count < 4);
    }

    public void OnClickStart()
    {
        SceneManager.LoadScene("ChooseStage");
    }

    public void OnClickExit()
    {
        Application.Quit();
    }

    private void OnDestroy()
    {
        if (manager != null)
            manager.onPlayerJoined -= HandlePlayerJoined;
    }
}
