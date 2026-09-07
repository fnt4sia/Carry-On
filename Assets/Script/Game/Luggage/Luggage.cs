using System;
using System.Collections.Generic;
using UnityEngine;

// Main luggage runtime. Owns flight colour, behavior type (Normal / Sticky / Fragile),
// fragile collision break, grabber tracking, and the IsWashed / IsWrapped flags the gate
// reads before accepting a bag onto a flight.
//
// Lifetime is opt-in: a level whose LevelConfig sets luggageLifetime to 0 runs with no
// countdown at all, which is how the flight-manifest levels play. The pressure there comes
// from the gate's departing flight, not from every individual bag rotting on the floor.
public class Luggage : MonoBehaviour
{
    private const float FallbackScenePlacedLifetime = 30f;

    [Header("Fragile")]
    [SerializeField, Min(0f)] private float fragileBreakThreshold = 300f;
    [SerializeField, Min(0f)] private float fragileGrabImmunityDuration = 2f;

    [Header("Collision Audio")]
    [SerializeField, Min(0f)] private float minimumCollisionAudioSpeed = 1.5f;
    [Tooltip("Minimum impact force before a clip plays. Speed alone counts a glancing scrape " +
             "as a hard hit; this is the force that actually landed. Luggage is 30 kg, so 60 " +
             "is about a 2 m/s head-on hit and a settling nudge is nearer 3.")]
    [SerializeField, Min(0f)] private float minimumCollisionImpulse = 60f;
    [SerializeField, Min(0f)] private float mediumCollisionAudioSpeed = 4f;
    [SerializeField, Min(0f)] private float hardCollisionAudioSpeed = 8f;
    [SerializeField, Min(0f)] private float collisionAudioCooldown = 0.15f;

    [SerializeField] private Outline outline;

    [Header("Wrapping")]
    [Tooltip("Shell shown once the bag has been wrapped. Authored on the prefab, hidden by " +
             "default, and switched on by MarkWrapped — the bag itself is never swapped, so its " +
             "flight colour survives the machine.")]
    [SerializeField] private GameObject wrapVisual;

    [Header("Tutorial / Menu")]
    [Tooltip("Menu and tutorial luggage: hides the timer UI and never expires or despawns on its own.")]
    public bool isTutorialLuggage;

    public LuggageBehaviorType behaviorType;

    [Tooltip("Which flight colour this bag counts as. Colour is the whole destination rule: " +
             "a gate's flight asks for N bags of a colour and any bag of that colour fills a slot.")]
    public LuggageColor color;
    [HideInInspector] public GameObject sourcePrefab;

    private float lifetimeRemaining;
    private float lifetimeDuration;
    private bool isInStation;
    private bool hasExpired;
    private bool isInitialized;

    private float fragileGrabImmunity;
    private float nextCollisionAudioTime;
    private LuggageBehaviorType initialBehaviorType;

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
        ApplyWrapVisual();
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
        ApplyWrapVisual();
        grabbers.Clear();
        lastGrabber = null;
        CachePhysicsComponents();
        RefreshTimerDisplay();
    }

    private void Update()
    {
        if (fragileGrabImmunity > 0f)
            fragileGrabImmunity -= Time.deltaTime;

        if (isTutorialLuggage || lifetimeDuration <= 0f || isInStation || hasExpired || IsDelivered) return;

        lifetimeRemaining -= Time.deltaTime;
        if (lifetimeRemaining <= 0f)
            OnLifetimeExpired();
    }

    private void OnCollisionEnter(Collision collision)
    {
        PlayCollisionAudio(collision);

        if (isTutorialLuggage) return;
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
        // A bag rattles against the deck for its whole trip down a belt, so every seam and
        // every bounce fired a clip and the track turned into a constant clatter. Checked
        // before the cooldown on purpose: a muted belt hit must not eat the cooldown that a
        // real impact needs. Only the belt itself is silent — bag-on-bag, bag-on-wall and
        // bag-on-player hits still play while riding it.
        if (collision.collider.GetComponentInParent<Conveyor>() != null)
            return;

        if (Time.time < nextCollisionAudioTime)
            return;

        float collisionSpeed = collision.relativeVelocity.magnitude;
        if (collisionSpeed < minimumCollisionAudioSpeed)
            return;

        // Speed alone reads a glancing scrape as a hard hit — a bag sliding along a wall or
        // shuffling against another bag keeps its full relative velocity while barely pushing
        // on anything, which is what made the clatter constant. Impulse is the force that
        // actually landed. Like the speed floor, this runs before the cooldown is stamped, so
        // a rejected tap never mutes the real impact a moment later.
        if (collision.impulse.magnitude < minimumCollisionImpulse)
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

        // Only a bag that came from a pool is expected back in one. Scene-placed luggage has no
        // pool key by design — Tutorial authors every bag in the scene and has no spawner at all —
        // so destroying it is the normal path, not a fault worth a warning.
        if (sourcePrefab != null)
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

    /// <summary>
    /// Wraps this bag in place: sets IsWrapped and reveals the wrap shell, keeping the same object
    /// so its flight colour is preserved. This is what the redesign's wrapper station uses —
    /// ConvertToWrapped swaps in a single colourless prefab and would throw the colour away.
    /// </summary>
    public void MarkWrapped()
    {
        IsWrapped = true;
        ApplyWrapVisual();
    }

    private void ApplyWrapVisual()
    {
        if (wrapVisual != null && wrapVisual.activeSelf != IsWrapped)
            wrapVisual.SetActive(IsWrapped);
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
        isTutorialLuggage = source.isTutorialLuggage;
        sourcePrefab = replacementPrefab;
        isInitialized = true;
        lifetimeRemaining = source.lifetimeRemaining;
        lifetimeDuration = source.lifetimeDuration;
        hasExpired = source.hasExpired;
        isInStation = source.isInStation;
        IsDelivered = source.IsDelivered;
        IsWashed = source.IsWashed || markWashed;
        IsWrapped = source.IsWrapped || markWrapped;
        ApplyWrapVisual();
        fragileGrabImmunity = source.fragileGrabImmunity;
        nextCollisionAudioTime = source.nextCollisionAudioTime;
        grabbers.Clear();
        lastGrabber = source.lastGrabber;
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
        else if (!isTutorialLuggage)
        {
            Debug.LogWarning(
                $"{nameof(Luggage)} '{name}' was placed in a scene without level tuning; "
                + $"using a {FallbackScenePlacedLifetime:0.#} second lifetime.",
                this);
        }

        Initialize(behaviorType, Mathf.Max(0f, lifetime), prefabKey: null);
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
