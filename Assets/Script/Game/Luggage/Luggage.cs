using System;
using System.Collections.Generic;
using UnityEngine;

// Main luggage runtime. Owns behavior type (Normal / Sticky / Fragile / Bomb),
// lifetime countdown, fragile collision break, bomb explosion, grabber tracking,
// and the IsWashed / IsWrapped / IsScanned flags the delivery gate reads to score it.
public class Luggage : MonoBehaviour
{
    [SerializeField] private Outline outline;

    public LuggageBehaviorType behaviorType;
    [HideInInspector] public GameObject sourcePrefab;

    [Header("Fragile Settings")]
    [SerializeField] private float fragileBreakThreshold = 10f;

    [Header("Bomb Explosion")]
    [SerializeField] private float explosionRadius = 5f;
    [SerializeField] private float explosionForce = 500f;

    [Header("Collision Audio")]
    [SerializeField, Min(0f)] private float minimumCollisionAudioSpeed = 1.5f;
    [SerializeField, Min(0f)] private float mediumCollisionAudioSpeed = 4f;
    [SerializeField, Min(0f)] private float hardCollisionAudioSpeed = 8f;
    [SerializeField, Min(0f)] private float collisionAudioCooldown = 0.15f;

    private float lifetimeRemaining;
    private float lifetimeDuration;
    private bool hasExploded;
    private bool isInStation;
    private bool hasExpired;

    private float fragileGrabImmunity;
    private float nextCollisionAudioTime;
    private LuggageBehaviorType initialBehaviorType;
    private int destinationGateNumber;

    private List<PlayerGrab> grabbers = new List<PlayerGrab>();
    private PlayerGrab lastGrabber;

    public bool IsDelivered { get; set; }
    public bool IsWashed  { get; private set; }
    public bool IsWrapped { get; private set; }
    public bool IsScanned { get; private set; }
    public bool IsBomb => behaviorType == LuggageBehaviorType.Bomb;
    public bool RequiresWashing => initialBehaviorType == LuggageBehaviorType.Sticky;
    public bool RequiresWrapping => initialBehaviorType == LuggageBehaviorType.Fragile;
    public bool IsInStation => isInStation;
    public bool HasDestinationGate => destinationGateNumber > 0;
    public int DestinationGateNumber => destinationGateNumber;
    public float LifetimeRemaining => Mathf.Max(0f, lifetimeRemaining);
    public float LifetimeNormalized => lifetimeDuration > 0f
        ? Mathf.Clamp01(lifetimeRemaining / lifetimeDuration)
        : 0f;
    private void Awake()
    {
        initialBehaviorType = behaviorType;
    }

    private void Start()
    {
        if (outline != null) outline.enabled = false;
    }

    public void Initialize(LuggageBehaviorType behavior, float lifetime, GameObject prefabKey)
    {
        behaviorType = behavior;
        initialBehaviorType = behavior;
        sourcePrefab = prefabKey;
        lifetimeRemaining = lifetime;
        lifetimeDuration = lifetime;
        hasExploded = false;
        hasExpired = false;
        isInStation = false;
        IsDelivered = false;
        IsWashed = false;
        IsWrapped = false;
        IsScanned = false;
        fragileGrabImmunity = 0f;
        destinationGateNumber = 0;
        RefreshTimerDisplay();
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
        PlayCollisionAudio(collision);

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

    private void PlayCollisionAudio(Collision collision)
    {
        if (Time.time < nextCollisionAudioTime)
            return;

        float collisionSpeed = collision.relativeVelocity.magnitude;
        if (collisionSpeed < minimumCollisionAudioSpeed)
            return;

        nextCollisionAudioTime = Time.time + collisionAudioCooldown;

        int clipIndex = collisionSpeed >= hardCollisionAudioSpeed
            ? 3
            : collisionSpeed >= mediumCollisionAudioSpeed
                ? 2
                : 1;

        string surfacePrefix = IsWindowLikeCollision(collision) ? "window" : "ground";
        string clipName = $"{surfacePrefix}Luggage Collision{clipIndex}";
        float volume = Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(minimumCollisionAudioSpeed, hardCollisionAudioSpeed * 1.5f, collisionSpeed));
        AudioManager.Instance?.PlaySFX(clipName, volume);
    }

    private static bool IsWindowLikeCollision(Collision collision)
    {
        return ContainsWindowLikeName(collision.collider.name)
            || ContainsWindowLikeName(collision.gameObject.name);
    }

    private static bool ContainsWindowLikeName(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && (value.Contains("window", StringComparison.OrdinalIgnoreCase)
                || value.Contains("glass", StringComparison.OrdinalIgnoreCase));
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

    public void SetInStation(bool value)
    {
        isInStation = value;
    }

    public void AssignDestinationGate(int gateNumber)
    {
        destinationGateNumber = gateNumber;
        RefreshTimerDisplay();
    }

    public Luggage ConvertToWashed(GameObject washedPrefab)
    {
        if (washedPrefab == null)
        {
            Debug.LogWarning($"{nameof(Luggage)} on {name} has no washed replacement prefab.");
            behaviorType = LuggageBehaviorType.Normal;
            IsWashed = true;
            return this;
        }

        return ReplaceWithPrefab(washedPrefab, markWashed: true, markWrapped: false);
    }

    public Luggage ConvertToWrapped(GameObject wrappedPrefab)
    {
        if (wrappedPrefab == null)
        {
            Debug.LogWarning($"{nameof(Luggage)} on {name} has no wrapped replacement prefab.");
            behaviorType = LuggageBehaviorType.Normal;
            IsWrapped = true;
            return this;
        }

        return ReplaceWithPrefab(wrappedPrefab, markWashed: false, markWrapped: true);
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

    private Luggage ReplaceWithPrefab(GameObject replacementPrefab, bool markWashed, bool markWrapped)
    {
        Transform originalParent = transform.parent;
        Vector3 originalWorldScale = transform.lossyScale;

        GameObject replacementObject = Instantiate(
            replacementPrefab,
            transform.position,
            transform.rotation);

        replacementObject.transform.localScale = originalWorldScale;
        if (originalParent != null)
            replacementObject.transform.SetParent(originalParent, worldPositionStays: true);

        Luggage replacement = replacementObject.GetComponent<Luggage>();
        if (replacement == null)
        {
            Debug.LogError($"Replacement prefab {replacementPrefab.name} is missing {nameof(Luggage)}.");
            Destroy(replacementObject);
            return this;
        }

        Rigidbody oldRb = GetComponent<Rigidbody>();
        Rigidbody newRb = replacement.GetComponent<Rigidbody>();

        replacement.CopyRuntimeStateFrom(this, replacementPrefab, markWashed, markWrapped);

        if (oldRb != null && newRb != null)
        {
            newRb.isKinematic = oldRb.isKinematic;
            if (!newRb.isKinematic)
            {
                newRb.linearVelocity = oldRb.linearVelocity;
                newRb.angularVelocity = oldRb.angularVelocity;
            }
        }

        gameObject.SetActive(false);
        Destroy(gameObject);
        return replacement;
    }

    private void CopyRuntimeStateFrom(Luggage source, GameObject replacementPrefab, bool markWashed, bool markWrapped)
    {
        initialBehaviorType = source.initialBehaviorType;
        sourcePrefab = replacementPrefab;
        lifetimeRemaining = source.lifetimeRemaining;
        lifetimeDuration = source.lifetimeDuration;
        hasExploded = source.hasExploded;
        hasExpired = source.hasExpired;
        isInStation = source.isInStation;
        IsDelivered = source.IsDelivered;
        IsWashed = source.IsWashed || markWashed;
        IsWrapped = source.IsWrapped || markWrapped;
        IsScanned = source.IsScanned;
        lastGrabber = source.lastGrabber;
        destinationGateNumber = source.destinationGateNumber;
        RefreshTimerDisplay();
    }

    private void RefreshTimerDisplay()
    {
        LuggageTimerDisplay timerDisplay = GetComponentInChildren<LuggageTimerDisplay>(true);
        if (timerDisplay != null)
            timerDisplay.RefreshImmediate();
    }

}
