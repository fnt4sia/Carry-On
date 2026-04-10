using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Boot scene. The first player to press Space (keyboard) or A (gamepad) joins
/// and the game immediately loads MainMenu. Additional players join in MainMenu.
/// </summary>
public class BootManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text promptText; // "Press Space / A to Start"

    private PlayerInputManager manager;
    private bool loading = false;

    private void Awake()
    {
        manager = GetComponent<PlayerInputManager>();
        manager.onPlayerJoined += OnPlayerJoined;

        if (promptText != null)
            promptText.text = "Press  <b>Space</b>  (Keyboard)  or  <b>A</b>  (Gamepad)  to Start";
    }

    private void OnPlayerJoined(PlayerInput player)
    {
        if (loading) return;
        loading = true;

        DontDestroyOnLoad(manager.gameObject);
        DontDestroyOnLoad(player.gameObject);

        SceneManager.LoadScene("MainMenu");
    }

    private void OnDestroy()
    {
        if (manager != null)
            manager.onPlayerJoined -= OnPlayerJoined;
    }
}
