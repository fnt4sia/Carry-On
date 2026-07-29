using System;
using System.Collections.Generic;
using UnityEngine;

// Main luggage runtime. Owns behavior type (Normal / Sticky / Fragile), lifetime
// countdown, fragile collision break, grabber tracking, and the IsWashed / IsWrapped
// flags the delivery gate reads to score it.
public class Luggage : MonoBehaviour
{
    private const float FallbackScenePlacedLifetime = 30f;

    [Header("Fragile")]
    [SerializeField, Min(0f)] private float fragileBreakThreshold = 300f;
    [SerializeField, Min(0f)] private float fragileGrabImmunityDuration = 2f;

    [Header("Collision Audio")]
    [SerializeField, Min(0f)] private float minimumCollisionAudioSpeed = 1.5f;
    [SerializeField, Min(0f)] private float mediumCollisionAudioSpeed = 4f;
    [SerializeField, Min(0f)] private float hardCollisionAudioSpeed = 8f;
    [SerializeField, Min(0f)] private float collisionAudioCooldown = 0.15f;

    [SerializeField] private Outline outline;

    public LuggageBehaviorType behaviorType;
    [HideInInspector] public GameObject sourcePrefab;

    private float lifetimeRemaining;
    private float lifetimeDuration;
    private bool isInStation;
    private bool hasExpired;
    private bool isInitialized;

    private float fragileGrabImmunity;
    private float nextCollisionAudioTime;
    private LuggageBehaviorType initialBehaviorType;
    private int destinationGateNumber;

    private List<PlayerGrab> grabbers = new List<PlayerGrab>();
    private PlayerGrab lastGrabber;
    private Rigidbody cachedRigidbody;
    private Collider cachedSurfaceCollider;

    public bool IsDelivered { get; set; }
    public bool IsWashed  { get; private set; }
    public bool IsWrapped { get; private set; }
    public bool RequiresWashing => initialBehaviorType == LuggageBehaviorType.Sticky;
    public bool RequiresWrapping => initialBehaviorType == LuggageBehaviorType.Fragile;
    public bool IsInStation => isInStation;
    public bool HasDestinationGate => destinationGateNumber > 0;
    public int DestinationGateNumber => destinationGateNumber;
    public float LifetimeRemaining => Mathf.Max(0f, lifetimeRemaining);
    public float LifetimeNormalized => lifetimeDuration > 0f
        ? Mathf.Clamp01(lifetimeRemaining / lifetimeDuration)
        : 0f;
    public Rigidbody Body => cachedRigidbody;
    public Collider SurfaceCollider => cachedSurfaceCollider;

    // Kept as properties so the three collision-audio thresholds stay ordered even if the
    // inspector values are typed out of order.
    private float MediumCollisionAudioSpeed => Mathf.Max(minimumCollisionAudioSpeed, mediumCollisionAudioSpeed);
    private float HardCollisionAudioSpeed => Mathf.Max(MediumCollisionAudioSpeed, hardCollisionAudioSpeed);

    private void Awake()
    {
        initialBehaviorType = behaviorType;
        CachePhysicsComponents();
    }

    private void Start()
    {
        if (!isInitialized)
            InitializeScenePlacedLuggage();

        if (outline != null) outline.enabled = false;
    }

    public void Initialize(LuggageBehaviorType behavior, float lifetime, GameObject prefabKey)
    {
        behaviorType = behavior;
        initialBehaviorType = behavior;
        sourcePrefab = prefabKey;
        isInitialized = true;
        lifetimeRemaining = lifetime;
        lifetimeDuration = lifetime;
        hasExpired = false;
        isInStation = false;
        IsDelivered = false;
        IsWashed = false;
        IsWrapped = false;
        fragileGrabImmunity = 0f;
        nextCollisionAudioTime = 0f;
        destinationGateNumber = 0;
        grabbers.Clear();
        lastGrabber = null;
        CachePhysicsComponents();
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

        int clipIndex = collisionSpeed >= HardCollisionAudioSpeed
            ? 3
            : collisionSpeed >= MediumCollisionAudioSpeed
                ? 2
                : 1;

        string clipName = Sfx.LuggageCollision(IsWindowLikeCollision(collision), clipIndex);
        float volume = Mathf.Lerp(
            0.35f,
            1f,
            Mathf.InverseLerp(minimumCollisionAudioSpeed, HardCollisionAudioSpeed * 1.5f, collisionSpeed));
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

        LevelConfig levelConfig = LevelContext.CurrentConfig;
        if (levelConfig != null)
            RoundScoreContext.TryApplyScore(levelConfig.scoreTimerExpired);

        DestroyLuggage();
    }

    public void DestroyLuggage()
    {
        DropAllGrabbers();

        if (LuggageSpawner.Instance != null && LuggageSpawner.Instance.ReturnLuggage(this))
            return;

        Debug.LogWarning($"{nameof(Luggage)} '{name}' could not return to a pool and will be destroyed.", this);
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
            fragileGrabImmunity = fragileGrabImmunityDuration;
    }

    public PlayerGrab GetLastGrabber()
    {
        return lastGrabber;
    }

    public void RemoveGrabber(PlayerGrab playerGrab)
    {
        if (grabbers.Contains(playerGrab)) grabbers.Remove(playerGrab);
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

    public bool GetIsGrabbed()
    {
        return grabbers.Count > 0;
    }

    private Luggage ReplaceWithPrefab(GameObject replacementPrefab, bool markWashed, bool markWrapped)
    {
        Transform originalParent = transform.parent;
        Vector3 originalWorldScale = transform.lossyScale;

        Luggage replacement;
        GameObject replacementObject;
        if (LuggageSpawner.Instance != null)
        {
            replacement = LuggageSpawner.Instance.RentLuggage(
                replacementPrefab,
                transform.position,
                transform.rotation);
            replacementObject = replacement != null ? replacement.gameObject : null;
        }
        else
        {
            replacementObject = Instantiate(replacementPrefab, transform.position, transform.rotation);
            replacement = replacementObject.GetComponent<Luggage>();
        }

        if (replacement == null)
        {
            Debug.LogError($"Replacement prefab {replacementPrefab.name} is missing {nameof(Luggage)}.");
            if (replacementObject != null)
                Destroy(replacementObject);
            return this;
        }

        replacementObject.transform.localScale = originalWorldScale;
        if (originalParent != null)
            replacementObject.transform.SetParent(originalParent, worldPositionStays: true);

        Rigidbody oldRb = cachedRigidbody;
        Rigidbody newRb = replacement.Body;

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

        DestroyLuggage();
        return replacement;
    }

    private void CopyRuntimeStateFrom(Luggage source, GameObject replacementPrefab, bool markWashed, bool markWrapped)
    {
        Luggage prefabDefinition = replacementPrefab.GetComponent<Luggage>();
        behaviorType = prefabDefinition != null
            ? prefabDefinition.behaviorType
            : LuggageBehaviorType.Normal;
        initialBehaviorType = source.initialBehaviorType;
        sourcePrefab = replacementPrefab;
        isInitialized = true;
        lifetimeRemaining = source.lifetimeRemaining;
        lifetimeDuration = source.lifetimeDuration;
        hasExpired = source.hasExpired;
        isInStation = source.isInStation;
        IsDelivered = source.IsDelivered;
        IsWashed = source.IsWashed || markWashed;
        IsWrapped = source.IsWrapped || markWrapped;
        fragileGrabImmunity = source.fragileGrabImmunity;
        nextCollisionAudioTime = source.nextCollisionAudioTime;
        grabbers.Clear();
        lastGrabber = source.lastGrabber;
        destinationGateNumber = source.destinationGateNumber;
        CachePhysicsComponents();
        RefreshTimerDisplay();
    }

    private void InitializeScenePlacedLuggage()
    {
        float lifetime = FallbackScenePlacedLifetime;

        if (LuggageSpawner.Instance != null
            && LuggageSpawner.Instance.TryGetConfiguredLifetime(out float configuredLifetime))
        {
            lifetime = configuredLifetime;
        }
        else if (LevelContext.TryGetConfig(out LevelConfig levelConfig))
        {
            lifetime = levelConfig.luggageLifetime;
        }
        else
        {
            Debug.LogWarning(
                $"{nameof(Luggage)} '{name}' was placed in a scene without level tuning; "
                + $"using a {FallbackScenePlacedLifetime:0.#} second lifetime.",
                this);
        }

        Initialize(behaviorType, Mathf.Max(0.1f, lifetime), prefabKey: null);
    }

    private void CachePhysicsComponents()
    {
        if (cachedRigidbody == null)
            cachedRigidbody = GetComponent<Rigidbody>();

        if (cachedSurfaceCollider == null)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (!colliders[i].isTrigger)
                {
                    cachedSurfaceCollider = colliders[i];
                    break;
                }
            }

            if (cachedSurfaceCollider == null && colliders.Length > 0)
                cachedSurfaceCollider = colliders[0];
        }
    }

    public static bool TryGetFromCollider(Collider collider, out Luggage luggage)
    {
        luggage = null;
        if (collider == null)
            return false;

        Rigidbody attachedBody = collider.attachedRigidbody;
        if (attachedBody != null)
            luggage = attachedBody.GetComponentInParent<Luggage>();

        if (luggage == null)
            luggage = collider.GetComponentInParent<Luggage>();

        return luggage != null;
    }

    private void RefreshTimerDisplay()
    {
        LuggageTimerDisplay timerDisplay = GetComponentInChildren<LuggageTimerDisplay>(true);
        if (timerDisplay != null)
            timerDisplay.RefreshImmediate();
    }

}
