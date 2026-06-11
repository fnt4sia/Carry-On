using UnityEngine;

// A level-select node on the ChooseStage map. Holds the scene it loads plus the
// display info shown in the popup card when the airplane token parks on it.
public class LevelNode : MonoBehaviour
{
    [Header("Load")]
    public string sceneName;        // scene loaded on confirm

    [Header("Display")]
    public string levelName = "Level";   // e.g. "Level 1"
    [TextArea] public string description; // short blurb shown in the popup
}
