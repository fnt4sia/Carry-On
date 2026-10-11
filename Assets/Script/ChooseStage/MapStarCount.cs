using TMPro;
using UnityEngine;

/// <summary>
/// The star total in the stage-select HUD: every star earned across the map, summed from each
/// node's best result in the active save. Refreshes whenever progress changes. The HUD element
/// itself is authored in the scene; this only writes the number.
/// </summary>
public class MapStarCount : MonoBehaviour
{
    [SerializeField] private TMP_Text countText;

    private void OnEnable()
    {
        if (ProgressionService.Instance != null)
            ProgressionService.Instance.ProgressChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (ProgressionService.Instance != null)
            ProgressionService.Instance.ProgressChanged -= Refresh;
    }

    private void Refresh()
    {
        int total = 0;
        foreach (LevelNode node in FindObjectsByType<LevelNode>(FindObjectsSortMode.None))
            total += node.BestStars;

        if (countText != null)
            countText.text = total.ToString();
    }
}
