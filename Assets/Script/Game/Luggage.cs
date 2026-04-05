using System.Collections.Generic;
using UnityEngine;

public class Luggage : MonoBehaviour
{
    [SerializeField] private Outline outline;

    [Header("Grab Settings")]
    public Transform[] grabPoints;
    public Vector3 grabCalculationOffset;

    public LuggageData data;
    public LuggageBehaviorType behaviorType;

    [Header("Fragile Settings")]
    [SerializeField] private float fragileBreakThreshold = 10f;

    [Header("Bomb Settings")]
    [SerializeField] private float bombTimer = 10f;
    [SerializeField] private float explosionRadius = 5f;
    [SerializeField] private float explosionForce = 500f;

    private float bombCountdown;
    private bool hasExploded;

    private float fragileGrabImmunity;

    private List<PlayerGrab> grabbers = new List<PlayerGrab>();
    private PlayerGrab lastGrabber;

    public bool IsDelivered { get; set; }
    public Conveyor ActiveConveyor { get; set; }
    [HideInInspector] public Vector3 kinematicVelocity;

    private void Start()
    {
        outline.enabled = false;
    }

    private void OnEnable()
    {
        bombCountdown = bombTimer;
        hasExploded = false;
    }

    private void Update()
    {
        if (fragileGrabImmunity > 0f)
            fragileGrabImmunity -= Time.deltaTime;

        if (behaviorType != LuggageBehaviorType.Bomb) return;
        bombCountdown -= Time.deltaTime;
        if (bombCountdown <= 0f)
            Explode();
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log(collision.impulse.magnitude);
        if (behaviorType != LuggageBehaviorType.Fragile) return;
        if (fragileGrabImmunity > 0f) return;
        if (collision.impulse.magnitude > fragileBreakThreshold) {
            Debug.Log(collision.gameObject.name);
            BreakLuggage();
        }
    }

    private void BreakLuggage()
    {
        Debug.Log("Hancur bang");
        DestroyLuggage();
    }

    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        Collider[] cols = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var col in cols)
        {
            Rigidbody rb = col.attachedRigidbody;
            if (rb != null && rb.gameObject != gameObject)
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius, 1f, ForceMode.Impulse);
        }

        DestroyLuggage();
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
            if (p != null) p.Drop(true);
        }
    }

    public void AddGrabber(PlayerGrab playerGrab)
    {
        if (!grabbers.Contains(playerGrab)) grabbers.Add(playerGrab);
        lastGrabber = playerGrab;
        if (behaviorType == LuggageBehaviorType.Fragile)
            fragileGrabImmunity = 2f;
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

    public List<PlayerGrab> GetGrabbers()
    {
        return new List<PlayerGrab>(grabbers);
    }

    public void ApplyBehaviorVisual()
    {
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend == null) return;

        switch (behaviorType)
        {
            case LuggageBehaviorType.Fragile:
                rend.material.color = Color.yellow;
                break;
            case LuggageBehaviorType.Sticky:
                rend.material.color = Color.green;
                break;
            default:
                rend.material.color = Color.white;
                break;
        }
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
