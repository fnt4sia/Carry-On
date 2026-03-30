using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PressurePlate : MonoBehaviour
{
    [Header("Connected Gateways")]
    [Tooltip("Drag the gateways you want this plate to control into this list.")]
    public List<Gateway> connectedGateways = new List<Gateway>();

    [Header("Settings")]
    [Tooltip("True: Stepping on the plate toggles the gate state (stays open after you leave). False: Gate opens while you stand on it, closes when you leave.")]
    public bool isToggleMode = true;

    [Tooltip("Can players trigger this plate?")]
    public bool canPlayerTrigger = true;

    [Tooltip("Can luggage trigger this plate?")]
    public bool canLuggageTrigger = true;

    [Header("Visuals")]
    [Tooltip("Animator to show plate being pushed down. Requires a boolean 'IsPressed'.")]
    public Animator plateAnimator;

    private int objectsOnPlate = 0;

    private void Start()
    {
        if (plateAnimator == null) plateAnimator = GetComponent<Animator>();

        // Make sure the collider is set to Trigger so things don't bounce off it 
        // entirely, they just pass freely over it.
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[PressurePlate] {gameObject.name}'s Collider was not a Trigger. Setting it to IsTrigger=true.");
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsValidTrigger(other))
        {
            objectsOnPlate++;

            // If it goes from 0 to 1, someone just stepped on it
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
            
            // Safeguard against physics glitches letting it dip below zero somehow
            if (objectsOnPlate < 0) objectsOnPlate = 0;

            // If it returns exactly to 0, everyone got off
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
        if (plateAnimator != null)
        {
            plateAnimator.SetBool("IsPressed", true);
        }

        foreach (Gateway gateway in connectedGateways)
        {
            if (gateway == null) continue;

            if (isToggleMode)
            {
                gateway.Toggle(); 
            }
            else
            {
                gateway.Open();  
            }
        }
    }

    private void OnPlateReleased()
    {
        if (plateAnimator != null)
        {
            plateAnimator.SetBool("IsPressed", false);
        }

        // If it's a hold-plate, leaving it should close it.
        // If it's a toggle mode, leaving does absolutely nothing.
        if (!isToggleMode)
        {
            foreach (Gateway gateway in connectedGateways)
            {
                if (gateway == null) continue;
                gateway.Close();
            }
        }
    }
}
