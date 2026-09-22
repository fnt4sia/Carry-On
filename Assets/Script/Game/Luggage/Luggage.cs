using System;
using System.Collections.Generic;
using UnityEngine;

// Main luggage runtime. Owns flight colour, grabber tracking, collision audio, and the
// IsWrapped flag the gate reads before accepting a bag onto a flight.
//
// Bags never expire. The pressure comes from the gate's departing flight, not from every
// individual bag rotting on the floor.
public class Luggage : MonoBehaviour
{
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

    [Header("Menu")]
    [Tooltip("Menu luggage is scenery: LuggageVisualEffects silences its smoke and throw trail.")]
    public bool isTutorialLuggage;

    [Tooltip("Which flight colour this bag counts as. Colour is the whole destination rule: " +
             "a gate's flight asks for N bags of a colour and any bag of that colour fills a slot.")]
    public LuggageColor color;
    [HideInInspector] public GameObject sourcePrefab;

    private bool isInStation;
    private bool isInitialized;

    private float nextCollisionAudioTime;

    private List<PlayerGrab> grabbers = new List<PlayerGrab>();
    private PlayerGrab lastGrabber;
    private Rigidbody cachedRigidbody;
    private Collider cachedSurfaceCollider;

    public bool IsDelivered { get; set; }
    public bool IsWrapped { get; private set; }
    public bool IsInStation => isInStation;
    public Rigidbody Body => cachedRigidbody;
    public Collider SurfaceCollider => cachedSurfaceCollider;

    // Kept as properties so the three collision-audio thresholds stay ordered even if the
    // inspector values are typed out of order.
    private float MediumCollisionAudioSpeed => Mathf.Max(minimumCollisionAudioSpeed, mediumCollisionAudioSpeed);
    private float HardCollisionAudioSpeed => Mathf.Max(MediumCollisionAudioSpeed, hardCollisionAudioSpeed);

    private void Awake()
    {
        CachePhysicsComponents();
    }

    private void Start()
    {
        if (!isInitialized)
            Initialize(prefabKey: null);

        if (outline != null) outline.enabled = false;
        ApplyWrapVisual();
    }

    /// <summary>
    /// Resets a bag to fresh-from-the-belt state. The spawner calls this on every rent, since a
    /// pooled bag still carries whatever happened to it last time.
    /// </summary>
    public void Initialize(GameObject prefabKey)
    {
        sourcePrefab = prefabKey;
        isInitialized = true;
        isInStation = false;
        IsDelivered = false;
        IsWrapped = false;
        nextCollisionAudioTime = 0f;
        ApplyWrapVisual();
        grabbers.Clear();
        lastGrabber = null;
        CachePhysicsComponents();
    }

    private void OnCollisionEnter(Collision collision)
    {
        PlayCollisionAudio(collision);
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

    public void DestroyLuggage()
    {
        DropAllGrabbers();

        if (LuggageSpawner.Instance != null && LuggageSpawner.Instance.ReturnLuggage(this))
            return;

        // Only a bag that came from a pool is expected back in one. Scene-placed luggage has no
        // pool key by design, so destroying it is the normal path, not a fault worth a warning.
        if (sourcePrefab != null)
            Debug.LogWarning($"{nameof(Luggage)} '{name}' could not return to a pool and will be destroyed.", this);

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

    public void SetInStation(bool value)
    {
        isInStation = value;
    }

    /// <summary>
    /// Wraps this bag in place: sets IsWrapped and reveals the wrap shell, keeping the same object
    /// so its flight colour is preserved.
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

    public bool GetIsGrabbed()
    {
        return grabbers.Count > 0;
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
}
