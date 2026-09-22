using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One seat in the lobby's team panel: a figure that is lit while a player holds the seat and dim
// while it is free, plus the line under it that names the seat's device or asks you to press
// something.
//
// The art and the layout are authored in TeamSlot.prefab. This only swaps colours and text, so
// recolouring stays an Inspector edit. The pulse is the same UIPulse the banner's join prompt
// uses, and it runs only while this seat is the one asking to be filled.
public class TeamSlot : MonoBehaviour
{
    [SerializeField] private Graphic[] figure;   // head + body
    [SerializeField] private TMP_Text tagText;   // the line under the figure
    [SerializeField] private CanvasGroup tagGroup;
    [SerializeField] private UIPulse tagPulse;

    [Header("Colours")]
    [SerializeField] private Color joinedColor = Color.white;
    [SerializeField] private Color emptyColor = new Color(0.41f, 0.41f, 0.41f);
    [SerializeField] private Color tagColor = new Color(0.85f, 0.85f, 0.85f);
    [SerializeField] private Color promptColor = new Color(1f, 0.894f, 0.431f);

    /// <summary>Seat taken: light the figure and name the device driving it.</summary>
    public void ShowPlayer(string label)
    {
        Tint(joinedColor);
        SetTag(label, tagColor, pulsing: false);
    }

    /// <summary>Seat free and next in line: dim figure, pulsing prompt for how to take it.</summary>
    public void ShowPrompt(string prompt)
    {
        Tint(emptyColor);
        SetTag(prompt, promptColor, pulsing: !string.IsNullOrEmpty(prompt));
    }

    /// <summary>Seat free but not next: dim figure, nothing written under it.</summary>
    public void ShowEmpty()
    {
        Tint(emptyColor);
        SetTag(string.Empty, tagColor, pulsing: false);
    }

    private void Tint(Color color)
    {
        foreach (Graphic graphic in figure)
            if (graphic != null) graphic.color = color;
    }

    private void SetTag(string text, Color color, bool pulsing)
    {
        if (tagText != null)
        {
            tagText.text = text;
            tagText.color = color;
        }

        if (tagPulse != null) tagPulse.enabled = pulsing;

        // UIPulse leaves the group wherever its last frame put it, so put the alpha back itself.
        if (tagGroup != null && !pulsing) tagGroup.alpha = 1f;
    }
}
