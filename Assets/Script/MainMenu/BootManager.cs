using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class BootManager : MonoBehaviour
{
    private PlayerInputManager manager;
    private bool firstPlayerJoined = false;

    private void Awake()
    {
        manager = GetComponent<PlayerInputManager>();
        manager.onPlayerJoined += OnPlayerJoined;
    }

    void Update()
    {
        if (Gamepad.current != null)
        {
            Debug.Log(Gamepad.current.leftStick.ReadValue());
        }else
        {
            Debug.Log("gada bang");
        }
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