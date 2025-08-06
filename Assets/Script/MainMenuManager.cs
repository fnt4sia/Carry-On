using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{

    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip mainMenuMusic;
    [SerializeField] private AudioSource firstAudioSource;
    [SerializeField] private AudioSource secondAudioSource;
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject stagePanel;
    [SerializeField] private GameObject backButton;

    [Header("Button")]
    [SerializeField] Button defaultButton;
    [SerializeField] Button stageButton;


    void OnEnable()
    {
        EventSystem.current.SetSelectedGameObject(null); 
        EventSystem.current.SetSelectedGameObject(defaultButton.gameObject);
    }

    private void Start()
    {
        firstAudioSource.PlayOneShot(mainMenuMusic);
        firstAudioSource.loop = true;
    }

    public void OnClickStart()
    {
        secondAudioSource.PlayOneShot(buttonClickSound);
        stagePanel.SetActive(true);
        menuPanel.SetActive(false);
        backButton.SetActive(true);

        EventSystem.current.SetSelectedGameObject(stageButton.gameObject);
    }

    public void OnBackmenu()
    {
        secondAudioSource.PlayOneShot(buttonClickSound);
        stagePanel.SetActive(false);
        menuPanel.SetActive(true);
        backButton?.SetActive(false);

        EventSystem.current.SetSelectedGameObject(defaultButton.gameObject);
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
