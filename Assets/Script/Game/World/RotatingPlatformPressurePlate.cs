using UnityEngine;

// Trigger plate for RotatingPlatform. The first valid object entering the plate
// reverses the platform direction; the visual resets when the plate is empty.
[DisallowMultipleComponent]
public class RotatingPlatformPressurePlate : MonoBehaviour
{
    [Header("Connected Platform")]
    [SerializeField] private RotatingPlatform connectedPlatform;

    [Header("Settings")]
    [SerializeField] private bool canPlayerTrigger = true;
    [SerializeField] private bool canLuggageTrigger = true;

    [Header("Visuals")]
    [SerializeField] private Renderer plateRenderer;
    [SerializeField] private Color defaultColor = new Color(1f, 0.45f, 0.05f);
    [SerializeField] private Color pressedColor = Color.green;

    private int objectsOnPlate;

    private void Reset()
    {
        connectedPlatform = GetComponentInParent<RotatingPlatform>();
        plateRenderer = GetComponentInChildren<Renderer>();

        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void Awake()
    {
        if (connectedPlatform == null)
            connectedPlatform = GetComponentInParent<RotatingPlatform>();

        if (plateRenderer == null)
            plateRenderer = GetComponentInChildren<Renderer>();
    }

    private void Start()
    {
        SetPlateColor(defaultColor);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsValidTrigger(other))
            return;

        objectsOnPlate++;

        if (objectsOnPlate == 1)
            OnPlatePressed();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsValidTrigger(other))
            return;

        objectsOnPlate = Mathf.Max(0, objectsOnPlate - 1);

        if (objectsOnPlate == 0)
            SetPlateColor(defaultColor);
    }

    private void OnPlatePressed()
    {
        SetPlateColor(pressedColor);
        connectedPlatform?.ReverseDirection();
    }

    public void SetConnectedPlatform(RotatingPlatform platform)
    {
        connectedPlatform = platform;
    }

    private void SetPlateColor(Color color)
    {
        if (plateRenderer != null)
            plateRenderer.material.color = color;
    }

    private bool IsValidTrigger(Collider other)
    {
        bool isPlayer = canPlayerTrigger
            && (other.CompareTag("Player") || other.GetComponentInParent<PlayerMovement>() != null);

        bool isLuggage = canLuggageTrigger
            && (other.CompareTag("Luggage") || other.GetComponentInParent<Luggage>() != null);

        return isPlayer || isLuggage;
    }
}
