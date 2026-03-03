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
    [Header("Audio")]
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip mainMenuMusic;
    [SerializeField] private AudioSource firstAudioSource;
    [SerializeField] private AudioSource secondAudioSource;

    [Header("Buttons")]
    [SerializeField] private Button defaultButton;

    [Header("Player UI")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform playerPanel;  
    [SerializeField] private GameObject playerUIPrefab;

    private readonly List<PlayerInput> joinedPlayers = new();
    private bool uiModeLocked = false;

    private InputDevice firstDevice;

    private void Start()
    {
        firstAudioSource.clip = mainMenuMusic;
        firstAudioSource.loop = true;
        firstAudioSource.Play();

        if (DeviceManager.Instance != null &&
            DeviceManager.Instance.assignedDevices.Count > 0)
        {
            firstDevice = DeviceManager.Instance.assignedDevices[0];
            PlayerInput player = PlayerInput.Instantiate(playerPrefab, pairWithDevice: firstDevice);
            OnPlayerJoined(player);
        }
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            Debug.Log("SPACE DETECTED");

        foreach (var g in Gamepad.all)
        {
            if (g.buttonSouth.wasPressedThisFrame)
                Debug.Log("GAMEPAD SOUTH DETECTED");
        }
    }

    public void OnPlayerJoined(PlayerInput player)
    {
        Debug.Log("kepanggil disini bnang");
        foreach (var p in joinedPlayers)
        {
            if (p.devices.Count > 0 && player.devices.Count > 0 &&
                p.devices[0] == player.devices[0])
            {
                Destroy(player.gameObject);
                return;
            }
        }

        joinedPlayers.Add(player);

        GameObject ui = Instantiate(playerUIPrefab, playerPanel);

        TMP_Text text = ui.GetComponentInChildren<TMP_Text>();
        text.text = $"Player {joinedPlayers.Count}";

        if (!uiModeLocked)
        {
            SetUIMode(player);
            uiModeLocked = true;
        }
    }

    private void SetUIMode(PlayerInput player)
    {
        var device = player.devices[0];

        if (device is Gamepad)
        {
            EnableGamepadUI();
        }
        else if (device is Keyboard)
        {
            EnableKeyboardUI();
        }
    }

    private void EnableKeyboardUI()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        EventSystem.current.sendNavigationEvents = false;
    }

    private void EnableGamepadUI()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        EventSystem.current.sendNavigationEvents = true;
        EventSystem.current.SetSelectedGameObject(defaultButton.gameObject);
    }

    public void OnClickStart()
    {
        secondAudioSource.PlayOneShot(buttonClickSound);
    }

    public void OnBackmenu()
    {
        secondAudioSource.PlayOneShot(buttonClickSound);

        if (firstDevice is Gamepad) EventSystem.current.SetSelectedGameObject(defaultButton.gameObject);
    }

    public void OnClickOptions()
    {
        secondAudioSource.PlayOneShot(buttonClickSound);
    }

    public void OnClickExit()
    {
        secondAudioSource.PlayOneShot(buttonClickSound);
        Application.Quit();
    }

    public void LoadStage(int level)
    {
        SceneManager.LoadScene(level);
    }
}