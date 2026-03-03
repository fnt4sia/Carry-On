using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class BootInputListener : MonoBehaviour
{
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            DeviceManager.Instance.SetPrimaryDevice(Keyboard.current);
            SceneManager.LoadScene("MainMenu");
            return;
        }

        foreach (var gamepad in Gamepad.all)
        {
            if (gamepad.buttonSouth.wasPressedThisFrame)
            {
                DeviceManager.Instance.SetPrimaryDevice(gamepad);
                SceneManager.LoadScene("MainMenu");
                return;
            }
        }
    }
}