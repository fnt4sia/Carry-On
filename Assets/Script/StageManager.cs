using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StageManager : MonoBehaviour
{
    public void LoadStage(int level)
    {
        SceneManager.LoadScene(level + 1);
    }
}
