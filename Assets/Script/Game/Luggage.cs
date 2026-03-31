using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Luggage : MonoBehaviour
{

    [SerializeField] private Outline outline;
    
    [Header("Grab Settings")]
    public Transform[] grabPoints;
    public Vector3 grabCalculationOffset;
    
    public LuggageType luggageType;
    public WeightClass weightClass;

    private List<PlayerGrab> grabbers = new List<PlayerGrab>();
    private PlayerGrab lastGrabber;

    public bool IsDelivered { get; set; }
    public Conveyor ActiveConveyor { get; set; }
    [HideInInspector] public Vector3 kinematicVelocity;

    private void Start()
    {
        outline.enabled = false;
    }

    public void DestroyLuggage()    
    {
        DropAllGrabbers();
        Destroy(gameObject);
    }

    public void DropAllGrabbers()
    {
        List<PlayerGrab> currentGrabbers = new List<PlayerGrab>(grabbers);
        foreach (var p in currentGrabbers)
        {
            if (p != null) p.Drop(); 
        }
    }

    public void AddGrabber(PlayerGrab playerGrab)
    {
        if (!grabbers.Contains(playerGrab)) grabbers.Add(playerGrab);
        lastGrabber = playerGrab; 
    }

    public PlayerGrab GetLastGrabber()
    {
        return lastGrabber;
    }

    public void RemoveGrabber(PlayerGrab playerGrab)
    {
        if (grabbers.Contains(playerGrab)) grabbers.Remove(playerGrab);
    }

    public int GetGrabberCount()
    {
        return grabbers.Count;
    }

    public float GetMass()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        return rb != null ? rb.mass : 0f;
    }

    public bool GetIsGrabbed()
    {
        return grabbers.Count > 0;
    }

    public Transform GetClosestGrabPoint(Vector3 playerPosition)
    {
        if (grabPoints == null || grabPoints.Length == 0) return transform;

        Vector3 checkPosition = playerPosition + grabCalculationOffset;

        Transform closestPoint = grabPoints[0];
        float minDistance = float.MaxValue;

        foreach (Transform point in grabPoints)
        {
            if (point == null) continue;
            
            float distance = Vector3.Distance(checkPosition, point.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestPoint = point;
            }
        }

        foreach (Transform point in grabPoints)
        {
            if (point == null) continue;
            MeshRenderer rnd = point.GetComponent<MeshRenderer>();
            if (rnd != null) rnd.material.color = Color.white;
        }

        MeshRenderer closestRnd = closestPoint.GetComponent<MeshRenderer>();
        if (closestRnd != null) closestRnd.material.color = Color.red;

        return closestPoint;
    }
}
