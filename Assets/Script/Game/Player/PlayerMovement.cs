using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Per-player ground movement and dash. Reads camera-relative move input, applies
// smoothed velocity with a wall-slide sweep so the player (and any carried luggage)
// can't tunnel through walls, drives the movement / dash animator bools, and emits
// bubble particles while moving.
public class PlayerMovement : MonoBehaviour
{
    public bool isGrabbing;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float movementSpeedNormal = 10f;
    [SerializeField, Min(0f)] private float rotationSpeed = 4f;
    [SerializeField, Min(0f)] private float grabRotationSpeedMultiplier = 1f;
    [SerializeField, Range(0f, 1f)] private float movementLerpSpeed = 0.15f;

    [Header("Dash")]
    [SerializeField, Min(1f)] private float dashSpeedMultiplier = 2f;
    [SerializeField, Min(0f)] private float dashDuration = 0.15f;
    [SerializeField, Min(0f)] private float dashCooldown = 1f;

    [SerializeField] private Rigidbody playerRb;

    [Header("Bubble VFX")]
    [SerializeField] private GameObject bubblePrefab;
    [SerializeField, Min(0.01f)] private float bubbleSpawnInterval = 0.04f;
    [SerializeField] private Vector3 bubbleOffsetRange;
    // Where the bubbles leave the ground, relative to the body's pivot. Bodies put their
    // pivot in different places (Ramp Agent at the waist, Annie at the feet), so this is
    // per-prefab rather than shared tuning.
    [SerializeField] private Vector3 bubbleSpawnOffset = new(0f, -0.6f, 0f);
    [SerializeField] private Animator animator;

    private PlayerGrab playerGrab;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction dashAction;

    private bool isDashing = false;
    private float lastDashTime = -10f;

    private Transform cameraTransform;
    private Vector2 moveInput;
    private Vector3 movementDirection;
    private Vector3 currentVelocity;
    private bool isMoving;

    private readonly List<BubbleVisual> activeBubbles = new();
    private readonly Stack<BubbleVisual> pooledBubbles = new();
    private MaterialPropertyBlock bubblePropertyBlock;
    private float bubbleSpawnTimer;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private class BubbleVisual
    {
        public GameObject GameObject;
        public Transform Transform;
        public Renderer Renderer;
        public Vector3 PrefabScale;
        public Vector3 StartPosition;
        public Vector3 EndPosition;
        public Color BaseColor;
        public float Elapsed;
    }

    private void Awake()
    {
        bubblePropertyBlock = new MaterialPropertyBlock();
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions.FindAction("Player/Move", throwIfNotFound: true);
        dashAction = playerInput.actions.FindAction("Player/Dash", throwIfNotFound: true);
        playerGrab = GetComponent<PlayerGrab>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ReleaseAllBubbles();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetRuntimeState();
        var cam = FindAnyObjectByType<Camera>();
        if (cam != null) cameraTransform = cam.transform;
        RemoveDestroyedBubbles();
    }

    public void ResetRuntimeState()
    {
        StopAllCoroutines();
        playerGrab?.Drop();

        moveInput = Vector2.zero;
        movementDirection = Vector3.zero;
        currentVelocity = Vector3.zero;
        isMoving = false;
        isDashing = false;
        isGrabbing = false;
        lastDashTime = -10f;
        bubbleSpawnTimer = 0f;

        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }

        if (animator != null)
        {
            animator.SetBool(AnimId.IsMoving, false);
            animator.SetBool(AnimId.IsDashing, false);
            animator.SetBool(AnimId.IsGrabbing, false);
            animator.SetBool(AnimId.IsThrowing, false);
        }

        ReleaseAllBubbles();
    }

    private static bool IsGameplayScene() => GameManager.Instance != null;

    private void Start()
    {
        var cam = FindAnyObjectByType<Camera>();
        if (cam != null) cameraTransform = cam.transform;
    }

    void Update()
    {
        UpdateBubbleEffects();

        if (!IsGameplayScene()) return;
        if (cameraTransform == null) return;
        moveInput = moveAction.ReadValue<Vector2>();

        if (dashAction.WasPressedThisFrame() && !isDashing && Time.time >= lastDashTime + dashCooldown)
            StartCoroutine(DashCoroutine());

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0; right.y = 0;
        forward.Normalize(); right.Normalize();

        movementDirection = (moveInput.x * right + moveInput.y * forward).normalized;
        isMoving = movementDirection.sqrMagnitude > 0.1f;
    }

    void FixedUpdate()
    {
        if (!IsGameplayScene()) return;

        float speed = movementSpeedNormal * (isDashing ? dashSpeedMultiplier : 1f);
        Vector3 targetVelocity = movementDirection * speed;
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, movementLerpSpeed);
        Vector3 moveDelta = new Vector3(currentVelocity.x, 0, currentVelocity.z) * Time.fixedDeltaTime;

        bool isHolding = isGrabbing && playerGrab != null;
        Luggage held = isHolding ? playerGrab.GetHeldLuggage() : null;
        Rigidbody luggageRb = held?.GetComponent<Rigidbody>();

        // Wall-slide sweep: the player is moved by a transform teleport, which gets NO
        // swept collision — so fast movement (dash) would tunnel through thin walls.
        // Sweep the carried luggage AND the player body, clamping moveDelta against each.
        ClampMoveDeltaAgainstWalls(luggageRb, ref moveDelta);
        ClampMoveDeltaAgainstWalls(playerRb, ref moveDelta);

        // Snapshot grab anchor BEFORE movement + rotation so we can compute full delta
        Vector3 anchorBefore = (luggageRb != null) ? playerGrab.GetGrabAnchorWorldPosition() : Vector3.zero;

        // Move player
        transform.position += moveDelta;

        // Rotate to face movement direction
        bool isTryingToMove = movementDirection.sqrMagnitude > 0.1f;
        animator.SetBool(AnimId.IsMoving, isTryingToMove);
        if (isTryingToMove)
        {
            Vector3 flatDir = new Vector3(movementDirection.x, 0, movementDirection.z).normalized;
            float rotSpeed = isGrabbing ? rotationSpeed * grabRotationSpeedMultiplier : rotationSpeed;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flatDir), rotSpeed * Time.fixedDeltaTime);
        }

        // Move luggage by full anchor delta (linear movement + rotational arc combined)
        if (luggageRb != null)
        {
            Vector3 anchorDelta = playerGrab.GetGrabAnchorWorldPosition() - anchorBefore;
            luggageRb.MovePosition(luggageRb.position + anchorDelta);
        }
    }

    // Sweeps `body` along moveDelta and strips out the into-wall component if it would
    // hit a static or heavier collider, so the teleport-based move can't push through it.
    // Lightweight dynamic objects (cones, decorations) are ignored — let physics push them.
    // Trigger colliders (pressure plates, sink zones) are ignored — they must
    // never block movement even though the project has queriesHitTriggers enabled.
    private void ClampMoveDeltaAgainstWalls(Rigidbody body, ref Vector3 moveDelta)
    {
        if (body == null || moveDelta.sqrMagnitude <= 0.0001f) return;
        if (!body.SweepTest(moveDelta.normalized, out RaycastHit hit, moveDelta.magnitude + 0.05f,
                            QueryTriggerInteraction.Ignore)) return;

        // Anything tagged "Ground" (ramps, low platforms, floor seams) is walkable —
        // skip the wall-clamp entirely and let the player move onto/over it normally.
        if (hit.collider.CompareTag("Ground")) return;

        Rigidbody hitRb = hit.rigidbody;
        bool isWall = hitRb == null || hitRb.isKinematic || hitRb.mass >= body.mass;
        if (!isWall) return;

        // A surface facing mostly upward is a floor, a ramp, or the lip of a low platform:
        // walkable, not a wall. This used to flatten the normal and test it against ~0, which
        // let a barely-tilted floor hit through — flattening (-0.09, 0.99, -0.09) leaves a small
        // horizontal component that normalises to a full-strength wall and cancels the whole
        // move. That is what stopped players dead at rotating-platform seams, and only
        // sometimes, because it depended on how deep the capsule happened to be resting in the
        // deck that step.
        if (hit.normal.y > 0.5f) return;

        Vector3 wallNormal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;

        float intoWall = Vector3.Dot(moveDelta, -wallNormal);
        if (intoWall > 0f) moveDelta += wallNormal * intoWall;

        float velIntoWall = Vector3.Dot(currentVelocity, -wallNormal);
        if (velIntoWall > 0f) currentVelocity += wallNormal * velIntoWall;
    }

    private IEnumerator DashCoroutine()
    {
        isDashing = true;
        lastDashTime = Time.time;
        animator.SetBool(AnimId.IsDashing, true);
        AudioManager.Instance?.PlaySFX(Sfx.PlayerDash);

        yield return new WaitForSeconds(dashDuration);

        animator.SetBool(AnimId.IsDashing, false);
        isDashing = false;
    }

    private void UpdateBubbleEffects()
    {
        const float duration = 0.5f;

        for (int i = activeBubbles.Count - 1; i >= 0; i--)
        {
            BubbleVisual bubble = activeBubbles[i];
            if (bubble.GameObject == null)
            {
                activeBubbles.RemoveAt(i);
                continue;
            }

            bubble.Elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(bubble.Elapsed / duration);
            bubble.Transform.position = Vector3.Lerp(bubble.StartPosition, bubble.EndPosition, t);
            bubble.Transform.localScale = Vector3.Lerp(bubble.PrefabScale, bubble.PrefabScale * 0.75f, t);
            SetBubbleColor(bubble.Renderer, new Color(
                bubble.BaseColor.r,
                bubble.BaseColor.g,
                bubble.BaseColor.b,
                bubble.BaseColor.a * (1f - t)));

            if (t >= 1f)
                ReleaseBubbleAt(i);
        }

        if (!isMoving || bubblePrefab == null)
        {
            bubbleSpawnTimer = 0f;
            return;
        }

        bubbleSpawnTimer -= Time.deltaTime;
        if (bubbleSpawnTimer > 0f)
            return;

        bubbleSpawnTimer = Mathf.Max(0.01f, bubbleSpawnInterval);
        SpawnBubble();
    }

    private void SpawnBubble()
    {
        BubbleVisual bubble = AcquireBubble();
        if (bubble == null)
            return;

        Vector3 offset = new(
            Random.Range(-bubbleOffsetRange.x, bubbleOffsetRange.x),
            Random.Range(-bubbleOffsetRange.y, bubbleOffsetRange.y),
            Random.Range(-bubbleOffsetRange.z, bubbleOffsetRange.z));

        bubble.StartPosition = transform.position + bubbleSpawnOffset + offset;
        bubble.EndPosition = bubble.StartPosition + new Vector3(0f, 0.4f, 0f);
        bubble.Elapsed = 0f;
        bubble.Transform.SetPositionAndRotation(bubble.StartPosition, Quaternion.identity);
        bubble.Transform.localScale = bubble.PrefabScale;
        bubble.GameObject.SetActive(true);
        SetBubbleColor(bubble.Renderer, bubble.BaseColor);
        activeBubbles.Add(bubble);
    }

    private BubbleVisual AcquireBubble()
    {
        while (pooledBubbles.Count > 0)
        {
            BubbleVisual pooled = pooledBubbles.Pop();
            if (pooled.GameObject != null)
                return pooled;
        }

        GameObject instance = Instantiate(bubblePrefab);
        Renderer bubbleRenderer = instance.GetComponentInChildren<Renderer>();
        return new BubbleVisual
        {
            GameObject = instance,
            Transform = instance.transform,
            Renderer = bubbleRenderer,
            PrefabScale = instance.transform.localScale,
            BaseColor = ReadSharedColor(bubbleRenderer)
        };
    }

    private void ReleaseBubbleAt(int index)
    {
        BubbleVisual bubble = activeBubbles[index];
        activeBubbles.RemoveAt(index);

        if (bubble.GameObject == null)
            return;

        if (bubble.Renderer != null)
            bubble.Renderer.SetPropertyBlock(null);
        bubble.GameObject.SetActive(false);
        pooledBubbles.Push(bubble);
    }

    private void ReleaseAllBubbles()
    {
        for (int i = activeBubbles.Count - 1; i >= 0; i--)
            ReleaseBubbleAt(i);
    }

    private void RemoveDestroyedBubbles()
    {
        for (int i = activeBubbles.Count - 1; i >= 0; i--)
        {
            if (activeBubbles[i].GameObject == null)
                activeBubbles.RemoveAt(i);
        }
    }

    private void SetBubbleColor(Renderer bubbleRenderer, Color color)
    {
        if (bubbleRenderer == null)
            return;

        bubblePropertyBlock.Clear();
        bubblePropertyBlock.SetColor(BaseColorId, color);
        bubblePropertyBlock.SetColor(ColorId, color);
        bubbleRenderer.SetPropertyBlock(bubblePropertyBlock);
    }

    private static Color ReadSharedColor(Renderer bubbleRenderer)
    {
        Material material = bubbleRenderer != null ? bubbleRenderer.sharedMaterial : null;
        if (material == null)
            return Color.white;
        if (material.HasProperty(BaseColorId))
            return material.GetColor(BaseColorId);
        if (material.HasProperty(ColorId))
            return material.GetColor(ColorId);
        return Color.white;
    }

    private void OnDestroy()
    {
        ReleaseAllBubbles();
        while (pooledBubbles.Count > 0)
        {
            BubbleVisual bubble = pooledBubbles.Pop();
            if (bubble.GameObject != null)
                Destroy(bubble.GameObject);
        }
    }

}
