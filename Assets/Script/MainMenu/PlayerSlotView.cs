using UnityEngine;
using UnityEngine.UI;

// One avatar in the bottom-right player list: a head and a body sprite that light up
// white once that slot's player has joined, and sit grey until then.
//
// Both Images are authored in PlayerSlot.prefab. This only tints them.
public class PlayerSlotView : MonoBehaviour
{
    // Note the file names read backwards from what they suggest: Multiplayer1 is the
    // 41x40 face (two eye holes), Multiplayer3 is the 49x30 shoulders.
    [SerializeField] private Image head;   // Multiplayer1.png
    [SerializeField] private Image body;   // Multiplayer3.png

    [SerializeField] private Color joinedColor = Color.white;
    [SerializeField] private Color emptyColor = new Color(0.42f, 0.46f, 0.56f);

    public void SetJoined(bool joined)
    {
        Color c = joined ? joinedColor : emptyColor;
        if (head != null) head.color = c;
        if (body != null) body.color = c;
    }
}
