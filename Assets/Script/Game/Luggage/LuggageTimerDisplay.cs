using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Thin presenter for the prefab-authored world-space timer. The prefab owns all
/// art; this component only places it, fills the radial image, and updates text.
/// </summary>
[DisallowMultipleComponent]
public class LuggageTimerDisplay : MonoBehaviour
{
    [SerializeField] private Luggage luggage;
    [SerializeField] private Collider sourceCollider;
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text timeText;

    [Header("Presentation")]
    [SerializeField] private string worldUiLayerName = WorldUIOverlayCamera.LayerName;
    [SerializeField, Min(0f)] private float hoverHeight = 0.35f;
    [SerializeField] private Color normalColor = new(0.98f, 0.18f, 0.18f);
    [SerializeField] private Color warningColor = new(1f, 0.58f, 0.12f);
    [SerializeField] private Color dangerColor = new(0.88f, 0.08f, 0.08f);
    [SerializeField, Min(0f)] private float warningThreshold = 10f;
    [SerializeField, Min(0f)] private float dangerThreshold = 5f;

    private Camera targetCamera;
    private int lastDisplayedSecond = -1;

    private void Awake()
    {
        if (luggage == null)
            luggage = GetComponentInParent<Luggage>();
        if (sourceCollider == null && luggage != null)
            sourceCollider = luggage.SurfaceCollider;
        if (visualRoot == null)
            visualRoot = transform as RectTransform;

        if (luggage == null || visualRoot == null || fillImage == null)
        {
            Debug.LogError(
                $"{nameof(LuggageTimerDisplay)} '{name}' is missing its prefab references.",
                this);
            enabled = false;
            return;
        }

        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Radial360;
        fillImage.fillOrigin = (int)Image.Origin360.Top;
        fillImage.fillClockwise = false;
        ApplyWorldUiLayer();
        UpdatePlacementAndFacing();
        UpdateTimer();
    }

    private void LateUpdate()
    {
        if (luggage == null)
            return;

        UpdatePlacementAndFacing();
        UpdateTimer();
    }

    public void RefreshImmediate()
    {
        if (!enabled || luggage == null)
            return;

        lastDisplayedSecond = -1;
        UpdatePlacementAndFacing();
        UpdateTimer();
    }

    private void UpdateTimer()
    {
        int second = Mathf.CeilToInt(luggage.LifetimeRemaining);
        // The countdown is frozen while the bag is inside a station, so the readout would
        // sit there showing a number that never moves. Hide it until the bag is grabbable again.
        bool visible = !luggage.isTutorialLuggage
            && second > 0 && !luggage.IsDelivered && !luggage.IsInStation;
        if (visualRoot.gameObject.activeSelf != visible)
            visualRoot.gameObject.SetActive(visible);
        if (!visible)
            return;

        fillImage.fillAmount = luggage.LifetimeNormalized;

        if (second == lastDisplayedSecond)
            return;

        lastDisplayedSecond = second;
        fillImage.color = second <= dangerThreshold
            ? dangerColor
            : second <= warningThreshold
                ? warningColor
                : normalColor;
        if (timeText != null)
            timeText.text = second.ToString();
    }

    private void UpdatePlacementAndFacing()
    {
        Vector3 anchor = sourceCollider != null
            ? sourceCollider.bounds.center
            : luggage.transform.position;
        float top = sourceCollider != null
            ? sourceCollider.bounds.max.y
            : luggage.transform.position.y;
        transform.position = new Vector3(anchor.x, top + hoverHeight, anchor.z);

        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null)
            return;

        Vector3 direction = targetCamera.transform.position - transform.position;
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction, targetCamera.transform.up);
    }

    private void ApplyWorldUiLayer()
    {
        int layer = LayerMask.NameToLayer(worldUiLayerName);
        if (layer < 0)
        {
            Debug.LogWarning(
                $"{nameof(LuggageTimerDisplay)} could not find layer '{worldUiLayerName}'.",
                this);
            return;
        }

        SetLayerRecursively(transform, layer);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayerRecursively(child, layer);
    }
}
