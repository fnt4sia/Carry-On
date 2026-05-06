using System.Collections.Generic;
using UnityEngine;

public class Luggage : MonoBehaviour
{
    [SerializeField] private Outline outline;

    public LuggageBehaviorType behaviorType;
    [HideInInspector] public GameObject sourcePrefab;

    [Header("Visuals")]
    [SerializeField] private MeshRenderer targetRenderer;
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material fragileMaterial;
    [SerializeField] private Material stickyMaterial;

    [Header("Fragile Settings")]
    [SerializeField] private float fragileBreakThreshold = 10f;

    [Header("Bomb Explosion")]
    [SerializeField] private float explosionRadius = 5f;
    [SerializeField] private float explosionForce = 500f;

    private float lifetimeRemaining;
    private bool hasExploded;
    private bool isInStation;
    private bool hasExpired;

    private float fragileGrabImmunity;

    private List<PlayerGrab> grabbers = new List<PlayerGrab>();
    private PlayerGrab lastGrabber;

    public bool IsDelivered { get; set; }
    public bool IsWashed  { get; private set; }
    public bool IsWrapped { get; private set; }
    public bool IsScanned { get; private set; }
    public bool IsBomb => behaviorType == LuggageBehaviorType.Bomb;
    public bool IsInStation => isInStation;
    public Conveyor ActiveConveyor { get; set; }
    [HideInInspector] public Vector3 kinematicVelocity;

    private void Start()
    {
        if (outline != null) outline.enabled = false;
    }

    public void Initialize(LuggageBehaviorType behavior, float lifetime, GameObject prefabKey)
    {
        behaviorType = behavior;
        sourcePrefab = prefabKey;
        lifetimeRemaining = lifetime;
        hasExploded = false;
        hasExpired = false;
        isInStation = false;
        IsDelivered = false;
        IsWashed = false;
        IsWrapped = false;
        IsScanned = false;
        fragileGrabImmunity = 0f;
        ApplyBehaviorVisual();
    }

    private void Update()
    {
        if (fragileGrabImmunity > 0f)
            fragileGrabImmunity -= Time.deltaTime;

        if (isInStation || hasExpired || IsDelivered) return;

        lifetimeRemaining -= Time.deltaTime;
        if (lifetimeRemaining <= 0f)
            OnLifetimeExpired();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (behaviorType != LuggageBehaviorType.Fragile) return;
        if (IsWrapped) return;
        if (fragileGrabImmunity > 0f) return;
        if (collision.impulse.magnitude > fragileBreakThreshold)
            BreakLuggage();
    }

    private void BreakLuggage()
    {
        DestroyLuggage();
    }

    private void OnLifetimeExpired()
    {
        hasExpired = true;

        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(GameManager.Instance.GetTimerExpiredPenalty());

        if (IsBomb)
            Explode();
        else
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
        if (targetRenderer == null) return;

        // Bomb has no indicator — looks normal. Fragile/Sticky swap to their material.
        if (behaviorType == LuggageBehaviorType.Fragile && fragileMaterial != null)
            targetRenderer.material = fragileMaterial;
        else if (behaviorType == LuggageBehaviorType.Sticky && stickyMaterial != null)
            targetRenderer.material = stickyMaterial;
        else if (normalMaterial != null)
            targetRenderer.material = normalMaterial;
    }

    public void SetInStation(bool value)
    {
        isInStation = value;
    }

    public void MarkWashed()
    {
        if (behaviorType == LuggageBehaviorType.Sticky)
            behaviorType = LuggageBehaviorType.Normal;
        IsWashed = true;
        ApplyBehaviorVisual();
    }

    public void MarkWrapped()
    {
        if (behaviorType == LuggageBehaviorType.Fragile)
            behaviorType = LuggageBehaviorType.Normal;
        IsWrapped = true;
        ApplyBehaviorVisual();
    }

    public void MarkScanned()
    {
        IsScanned = true;
        Debug.Log($"[Scanner] Luggage scanned — {(IsBomb ? "BOMB DETECTED" : "safe")}");
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
}
