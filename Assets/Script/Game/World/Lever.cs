using System.Collections.Generic;
using UnityEngine;

// A lever a player pulls by pressing the UseStation action while standing next to it.
// PlayerGrab does the range check with the same overlap sphere it uses to find a station,
// so a lever only needs a collider to be found.
//
// Wiring matches PressurePlate: the lever owns its target lists and the targets never point
// back, so a level's connections live in exactly one place. Every pull flips the state:
//   - Gateways : Toggle()
//   - Platforms: ReverseDirection()
public class Lever : MonoBehaviour
{
    [Header("Connected Targets")]
    [SerializeField] private List<Gateway> connectedGateways = new List<Gateway>();
    [SerializeField] private List<RotatingPlatform> connectedPlatforms = new List<RotatingPlatform>();

    [Header("Visuals")]
    [SerializeField] private Renderer leverRenderer;
    [SerializeField] private Color offColor = Color.red;
    [SerializeField] private Color onColor = Color.green;

    private bool isOn;
    private MaterialPropertyBlock propertyBlock;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Start()
    {
        if (leverRenderer == null) leverRenderer = GetComponentInChildren<Renderer>();
        SetLeverColor(offColor);
    }

    // Called by PlayerGrab when a player presses UseStation within range.
    public void Use()
    {
        isOn = !isOn;
        SetLeverColor(isOn ? onColor : offColor);

        foreach (Gateway gateway in connectedGateways)
        {
            if (gateway == null) continue;
            gateway.Toggle();
        }

        foreach (RotatingPlatform platform in connectedPlatforms)
        {
            if (platform == null) continue;
            platform.ReverseDirection();
        }
    }

    private void SetLeverColor(Color color)
    {
        if (leverRenderer == null)
            return;

        propertyBlock ??= new MaterialPropertyBlock();
        leverRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);
        leverRenderer.SetPropertyBlock(propertyBlock);
    }
}
