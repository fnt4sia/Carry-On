using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StageManager : MonoBehaviour
{

    [SerializeField] private AudioSource firstAudioSource;
    [SerializeField] private AudioSource secondAudioSource;
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip backgroundMusic;
    private void Start()
    {
        Time.timeScale = 1;
        firstAudioSource.PlayOneShot(backgroundMusic);
        firstAudioSource.loop = true;
    }

    public void LoadStage(int level)
    {
        StartCoroutine(DelayLoadScene(level));
    }

    private IEnumerator DelayLoadScene(int level)
    {
        Debug.Log("abc");
        secondAudioSource.PlayOneShot(buttonClickSound);
        yield return new WaitForSeconds(0.2f);
        Debug.Log("def");
        SceneManager.LoadScene(level + 2);
    }
}
