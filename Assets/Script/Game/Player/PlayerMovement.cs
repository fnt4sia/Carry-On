using System.Collections;
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

    [SerializeField] private Rigidbody playerRb;
    [SerializeField] private float movementSpeedNormal;
    [SerializeField] private float rotationSpeed;
    [SerializeField] private float grabRotationSpeedMultiplier = 0.6f;
    [SerializeField] private float lerpSpeed;
    [SerializeField] private GameObject bubblePrefab;
    [SerializeField] private float bubbleSpawnInterval;
    [SerializeField] private Vector3 bubbleOffsetRange;
    [SerializeField] private Animator animator;

    [Header("Dash")]
    [SerializeField] private float dashSpeedMultiplier = 2.5f;
    [SerializeField] private float dashDuration;
    [SerializeField] private float dashCooldown;

    [SerializeField] private int playerIndex;

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
    private DesignSceneInput designInput;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
        dashAction = playerInput.actions["Dash"];
        playerGrab = GetComponent<PlayerGrab>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var cam = FindAnyObjectByType<Camera>();
        if (cam != null) cameraTransform = cam.transform;
        designInput = FindFirstObjectByType<DesignSceneInput>();
    }

    private static bool IsGameplayScene() => GameManager.Instance != null;

    private void Start()
    {
        var cam = FindAnyObjectByType<Camera>();
        if (cam != null) cameraTransform = cam.transform;
        designInput = FindFirstObjectByType<DesignSceneInput>();
        StartCoroutine(SpawnBubblesCoroutine());
    }

    void Update()
    {
        if (!IsGameplayScene()) return;
        if (cameraTransform == null) return;
        if (designInput != null) return;

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
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, lerpSpeed);
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
        animator.SetBool("isMoving", isTryingToMove);
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
    // Trigger colliders (pressure plates, scanner/sink zones) are ignored — they must
    // never block movement even though the project has queriesHitTriggers enabled.
    private void ClampMoveDeltaAgainstWalls(Rigidbody body, ref Vector3 moveDelta)
    {
        if (body == null || moveDelta.sqrMagnitude <= 0.0001f) return;
        if (!body.SweepTest(moveDelta.normalized, out RaycastHit hit, moveDelta.magnitude + 0.05f,
                            QueryTriggerInteraction.Ignore)) return;

        Rigidbody hitRb = hit.rigidbody;
        bool isWall = hitRb == null || hitRb.isKinematic || hitRb.mass >= body.mass;
        if (!isWall) return;

        Vector3 wallNormal = hit.normal; wallNormal.y = 0f;
        if (wallNormal.sqrMagnitude < 0.0001f) return; // grazing a floor/ceiling — ignore
        wallNormal.Normalize();

        float intoWall = Vector3.Dot(moveDelta, -wallNormal);
        if (intoWall > 0f) moveDelta += wallNormal * intoWall;

        float velIntoWall = Vector3.Dot(currentVelocity, -wallNormal);
        if (velIntoWall > 0f) currentVelocity += wallNormal * velIntoWall;
    }

    // Called by DesignSceneInput to bypass PlayerInput in the design scene
    public void InjectInput(Vector2 move, bool dashPressed)
    {
        if (dashPressed && !isDashing && Time.time >= lastDashTime + dashCooldown)
            StartCoroutine(DashCoroutine());

        Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 right   = cameraTransform != null ? cameraTransform.right   : Vector3.right;
        forward.y = 0; right.y = 0;
        forward.Normalize(); right.Normalize();
        movementDirection = (move.x * right + move.y * forward).normalized;
        isMoving = movementDirection.sqrMagnitude > 0.1f;
    }

    private IEnumerator DashCoroutine()
    {
        isDashing = true;
        lastDashTime = Time.time;
        animator.SetBool("isDashing", true);
        AudioManager.Instance?.PlaySFX("Player Dash");

        yield return new WaitForSeconds(dashDuration);

        animator.SetBool("isDashing", false);
        isDashing = false;
    }

    IEnumerator SpawnBubblesCoroutine()
    {
        while (true)
        {
            if (isMoving)
            {
                Vector3 offset = new Vector3(
                    Random.Range(-bubbleOffsetRange.x, bubbleOffsetRange.x),
                    Random.Range(-bubbleOffsetRange.y, bubbleOffsetRange.y),
                    Random.Range(-bubbleOffsetRange.z, bubbleOffsetRange.z));

                Vector3 spawnPos = (transform.position + new Vector3(0, -0.6f, 0)) + offset;
                GameObject bubble = Instantiate(bubblePrefab, spawnPos, Quaternion.identity);
                StartCoroutine(AnimateBubble(bubble));
            }

            yield return new WaitForSeconds(bubbleSpawnInterval);
        }
    }

    IEnumerator AnimateBubble(GameObject bubble)
    {
        float duration = 0.5f;
        float elapsed = 0f;

        Vector3 startPos = bubble.transform.position;
        Vector3 endPos = startPos + new Vector3(0, 0.4f, 0);

        Vector3 startScale = bubble.transform.localScale;
        Vector3 endScale = startScale * 0.75f;

        Renderer renderer = bubble.GetComponent<Renderer>();
        Material mat = renderer.material;
        Color startColor = mat.color;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            bubble.transform.position = Vector3.Lerp(startPos, endPos, t);
            bubble.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            mat.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t));

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(bubble);
    }

}
