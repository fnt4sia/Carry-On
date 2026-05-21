using UnityEngine;

// Marker component placed on each level-select node in the ChooseStage map.
// Holds the scene name (or fallback index) that MapMover loads when the player
// presses Enter while standing on this node.
public class LevelNode : MonoBehaviour
{
    public string sceneName;
    public int levelIndex;
}
