using System.Collections.Generic;
using UnityEngine;

public class PressurePlate : MonoBehaviour
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

    private void Start()
    {
        if (plateRenderer == null) plateRenderer = GetComponentInChildren<Renderer>();
        if (plateRenderer != null)
        {
            plateRenderer.material.color = defaultColor;
        }
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
        bool isPlayer = canPlayerTrigger && other.CompareTag("Player");
        bool isLuggage = canLuggageTrigger && other.CompareTag("Luggage");
        return isPlayer || isLuggage;
    }

    private void OnPlatePressed()
    {
        if (plateRenderer != null)
        {
            plateRenderer.material.color = pressedColor;
        }

        foreach (Gateway gateway in connectedGateways)
        {
            if (gateway == null) continue;

            gateway.Toggle();
        }
    }

    private void OnPlateReleased()
    {
        if (plateRenderer != null)
        {
            plateRenderer.material.color = defaultColor;
        }

        if (!isToggleMode)
        {
            foreach (Gateway gateway in connectedGateways)
            {
                if (gateway == null) continue;
                gateway.Toggle();
            }
        }
    }
}
