using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class BootManager : MonoBehaviour
{
    private PlayerInputManager manager;
    private bool firstPlayerJoined = false;

    private void Awake()
    {
        manager = GetComponent<PlayerInputManager>();
        manager.onPlayerJoined += OnPlayerJoined;
    }

    private void OnPlayerJoined(PlayerInput player)
    {
        DontDestroyOnLoad(manager.gameObject);
        DontDestroyOnLoad(player.gameObject);

        if (!firstPlayerJoined)
        {
            firstPlayerJoined = true;
            SceneManager.LoadScene("MainMenu");
        }
    }
}