using System.Collections.Generic;
using UnityEngine;

// Trigger plate that controls its connected Gateways when stepped on (by players or
// luggage, individually toggleable). Toggle mode flips once per press; momentary mode
// opens while occupied and closes when the last object leaves.
public class GatewayPressurePlate : MonoBehaviour
{
    [Header("Connected Gateways")]
    [SerializeField] private List<Gateway> connectedGateways = new List<Gateway>();

    [Header("Settings")]
    [SerializeField] private bool isToggleMode = true;
    [SerializeField] private bool canPlayerTrigger = true;
    [SerializeField] private bool canLuggageTrigger = true;

    [Header("Visuals")]
    [SerializeField] private Renderer plateRenderer;
    [SerializeField] private Color defaultColor = Color.red;
    [SerializeField] private Color pressedColor = Color.green;

    private int objectsOnPlate = 0;
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
        if (IsValidTrigger(other))
        {
            objectsOnPlate++;

            if (objectsOnPlate == 1)
            {
                OnPlatePressed();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsValidTrigger(other))
        {
            objectsOnPlate--;
            
            if (objectsOnPlate < 0) objectsOnPlate = 0;

            if (objectsOnPlate == 0)
            {
                OnPlateReleased();
            }
        }
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
