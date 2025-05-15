using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{

    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip mainMenuMusic;
    [SerializeField] private AudioSource firstAudioSource;
    [SerializeField] private AudioSource secondAudioSource;


    private void Start()
    {
        firstAudioSource.PlayOneShot(mainMenuMusic);
        firstAudioSource.loop = true;
    }

    public void OnClickStart()
    {
        StartCoroutine(LoadSceneWithDelay());
    }

    private IEnumerator LoadSceneWithDelay()
    {
        secondAudioSource.PlayOneShot(buttonClickSound);
        yield return new WaitForSeconds(0.2f);
        SceneManager.LoadScene(1);
    }

    public void OnClickOptions()
    {
        secondAudioSource.PlayOneShot(buttonClickSound);
    }

    public void OnClickExit()
    {
        secondAudioSource.PlayOneShot(buttonClickSound);
    }
}
