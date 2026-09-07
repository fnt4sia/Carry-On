using UnityEngine;
using UnityEngine.UI;

// Overcooked-style progress bar over a machine that a player has to work by hand.
//
// The whole bar is authored in the scene; this component only fills it and shows or hides it. It
// appears when a bag is loaded and waiting, tracks how far the crank has got, and disappears the
// moment the machine finishes — so an idle machine carries no UI at all.
[DisallowMultipleComponent]
public class StationProgressDisplay : MonoBehaviour
{
    [SerializeField] private MachineStation station;
    [Tooltip("Root toggled off whenever there is nothing to crank.")]
    [SerializeField] private GameObject visualRoot;
    [Tooltip("Image with Type = Filled. Its fillAmount is driven from the crank progress.")]
    [SerializeField] private Image fillImage;

    [Header("Colours")]
    [SerializeField] private Color barColor = new(0.98f, 0.78f, 0.16f);
    [Tooltip("Used for the last stretch, so a nearly-finished machine reads at a glance.")]
    [SerializeField] private Color nearlyDoneColor = new(0.35f, 0.85f, 0.40f);
    [SerializeField, Range(0f, 1f)] private float nearlyDoneThreshold = 0.8f;

    private void Awake()
    {
        if (station == null)
            station = GetComponentInParent<MachineStation>();

        if (station == null || visualRoot == null || fillImage == null)
        {
            Debug.LogError($"{nameof(StationProgressDisplay)} '{name}' is missing its references.", this);
            enabled = false;
            return;
        }

        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        Refresh();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void Refresh()
    {
        bool visible = station.IsAwaitingCrank;
        if (visualRoot.activeSelf != visible)
            visualRoot.SetActive(visible);
        if (!visible)
            return;

        float progress = station.CrankProgress01;
        fillImage.fillAmount = progress;
        fillImage.color = progress >= nearlyDoneThreshold ? nearlyDoneColor : barColor;
    }
}
