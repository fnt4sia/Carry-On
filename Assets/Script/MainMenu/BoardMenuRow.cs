using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One departure-board row acting as a menu entry.
//
// The highlight itself is the row's own Image, driven by the Button's ColorBlock — no
// code needed for that. This component adds what a ColorBlock can't reach: the text has
// to flip dark once the yellow bar is behind it, the accent notch has to appear, and the
// STATUS column reads "BOARDING" while the row is picked. Locked rows (Button.interactable
// off) read "DELAYED" and never light up.
//
// Idle colours are whatever the prefab and its scene overrides authored — they are read
// once at Awake, never hard-coded here, so recolouring a row is still a scene edit.
[RequireComponent(typeof(Button))]
public class BoardMenuRow : MonoBehaviour,
    ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject selectionArrow; // accent notch at the left edge
    [SerializeField] private TMP_Text flightCodeText;
    [SerializeField] private TMP_Text destinationText;
    [SerializeField] private TMP_Text statusText;

    [Header("Picked Row")]
    [Tooltip("Text colour while the yellow highlight is behind this row.")]
    [SerializeField] private Color activeTextColor = new Color(0.07f, 0.08f, 0.11f);

    [Header("Status Wording")]
    [SerializeField] private string idleStatus = "ON TIME";
    [SerializeField] private string activeStatus = "BOARDING";
    [SerializeField] private string lockedStatus = "DELAYED";

    private Button button;
    private Color idleFlightColor, idleDestinationColor, idleStatusColor;
    private bool cached;
    private bool selected;
    private bool hovered;

    private void Awake() => EnsureCached();

    // Awake does NOT run while a GameObject is inactive, and these rows live inside panels that
    // start switched off. Anything that touches a row before its first activation — filling the
    // save list, say — would otherwise read the idle colours as default(Color), which is
    // transparent black, and paint the text invisible. So caching is lazy and happens once,
    // from whichever entry point gets there first.
    private void EnsureCached()
    {
        if (cached) return;
        cached = true;

        button = GetComponent<Button>();
        if (flightCodeText != null) idleFlightColor = flightCodeText.color;
        if (destinationText != null) idleDestinationColor = destinationText.color;
        if (statusText != null) idleStatusColor = statusText.color;
    }

    // Rows are revealed as a group after the first join, so re-sync on every enable —
    // pointer/selection state from a previous showing is stale by then.
    private void OnEnable()
    {
        EnsureCached();
        selected = false;
        hovered = false;
        Refresh();
    }

    // Save-slot rows are the one place a row's wording is not authored: the slot list has to
    // read off disk. Idle colours are untouched, so a filled row still looks like every other.
    public void SetContent(string flightCode, string destination, string status)
    {
        EnsureCached();
        if (flightCodeText != null) flightCodeText.text = flightCode;
        if (destinationText != null) destinationText.text = destination;
        idleStatus = status;
        Refresh();
    }

    public void OnSelect(BaseEventData _) { selected = true; Refresh(); }
    public void OnDeselect(BaseEventData _) { selected = false; Refresh(); }
    public void OnPointerEnter(PointerEventData _) { hovered = true; Refresh(); }
    public void OnPointerExit(PointerEventData _) { hovered = false; Refresh(); }

    private void Refresh()
    {
        bool locked = button != null && !button.interactable;
        bool active = !locked && (selected || hovered);

        if (selectionArrow != null && selectionArrow.activeSelf != active)
            selectionArrow.SetActive(active);

        if (flightCodeText != null)
            flightCodeText.color = active ? activeTextColor : idleFlightColor;
        if (destinationText != null)
            destinationText.color = active ? activeTextColor : idleDestinationColor;

        if (statusText != null)
        {
            statusText.color = active ? activeTextColor : idleStatusColor;
            statusText.text = locked ? lockedStatus : active ? activeStatus : idleStatus;
        }
    }
}
