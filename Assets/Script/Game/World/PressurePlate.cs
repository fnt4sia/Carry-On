using System.Collections.Generic;
using UnityEngine;

// One pressure plate for every "step here to actuate something" prop. The plate owns its
// connections: drag Gateways and/or RotatingPlatforms into the lists below. The targets
// never point back at the plate, so wiring lives in one place. Valid contacts are counted
// so multi-collider objects don't release the plate early.
//
// On press (first valid contact enters):
//   - Gateways : Toggle() in toggle mode, otherwise Open().
//   - Platforms: ReverseDirection().
// On release (last contact leaves):
//   - Gateways : Close() in momentary mode (toggle mode keeps the new state).
//   - Platforms: nothing (direction is kept); only the plate visual resets.
public class PressurePlate : MonoBehaviour
{
    [Header("Connected Targets")]
    [SerializeField] private List<Gateway> connectedGateways = new List<Gateway>();
    [SerializeField] private List<RotatingPlatform> connectedPlatforms = new List<RotatingPlatform>();

    [Header("Settings")]
    [Tooltip("Gateways only. On = flip once per press. Off = open while occupied, close on release.")]
    [SerializeField] private bool isToggleMode = true;
    [SerializeField] private bool canPlayerTrigger = true;
    [SerializeField] private bool canLuggageTrigger = true;

    [Header("Visuals")]
    [SerializeField] private Renderer plateRenderer;
    [SerializeField] private Color defaultColor = Color.red;
    [SerializeField] private Color pressedColor = Color.green;

    private int objectsOnPlate;
    private MaterialPropertyBlock propertyBlock;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Start()
    {
        if (plateRenderer == null) plateRenderer = GetComponentInChildren<Renderer>();
        SetPlateColor(defaultColor);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsValidTrigger(other)) return;

        objectsOnPlate++;
        if (objectsOnPlate == 1) OnPlatePressed();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsValidTrigger(other)) return;

        objectsOnPlate = Mathf.Max(0, objectsOnPlate - 1);
        if (objectsOnPlate == 0) OnPlateReleased();
    }

    private bool IsValidTrigger(Collider other)
    {
        bool isPlayer = canPlayerTrigger && other.GetComponentInParent<PlayerMovement>() != null;
        bool isLuggage = canLuggageTrigger && Luggage.TryGetFromCollider(other, out _);
        return isPlayer || isLuggage;
    }

    private void OnPlatePressed()
    {
        SetPlateColor(pressedColor);

        foreach (Gateway gateway in connectedGateways)
        {
            if (gateway == null) continue;

            if (isToggleMode)
                gateway.Toggle();
            else
                gateway.Open();
        }

        foreach (RotatingPlatform platform in connectedPlatforms)
        {
            if (platform == null) continue;
            platform.ReverseDirection();
        }
    }

    private void OnPlateReleased()
    {
        SetPlateColor(defaultColor);

        if (!isToggleMode)
        {
            foreach (Gateway gateway in connectedGateways)
            {
                if (gateway == null) continue;
                gateway.Close();
            }
        }
    }

    private void SetPlateColor(Color color)
    {
        if (plateRenderer == null)
            return;

        propertyBlock ??= new MaterialPropertyBlock();
        plateRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);
        plateRenderer.SetPropertyBlock(propertyBlock);
    }
}
