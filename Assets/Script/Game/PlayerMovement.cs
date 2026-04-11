using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
    [SerializeField] private float dashForce;
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



    private static readonly HashSet<string> gameplayScenes = new()
        { "TestStage", "Stage_1", "Stage_2", "Stage_2_New", "Stage_3", "Stage Tutorial", "DesignScene" };

    private bool isPlaying;

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
        // Re-find camera each time a scene loads (it changes between scenes)
        var cam = FindAnyObjectByType<Camera>();
        if (cam != null) cameraTransform = cam.transform;
    }

    private bool IsGameplayScene()
        => gameplayScenes.Contains(SceneManager.GetActiveScene().name);

    private void Start()
    {
        var cam = FindAnyObjectByType<Camera>();
        if (cam != null) cameraTransform = cam.transform;
        StartCoroutine(SpawnBubblesCoroutine());
    }

    void Update()
    {
        if (!IsGameplayScene()) return;
        if (cameraTransform == null) return;
        if (SceneManager.GetActiveScene().name == "DesignScene") return; // driven by DesignSceneInput

        moveInput = moveAction.ReadValue<Vector2>();
        bool dashPressed = dashAction.WasPressedThisFrame();

        if (dashPressed && !isDashing && Time.time >= lastDashTime + dashCooldown)
        {
            StartCoroutine(DashCoroutine());
            return;
        }

        if (!isDashing)
        {
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;

            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            movementDirection = (moveInput.x * right + moveInput.y * forward).normalized;
            isMoving = movementDirection.sqrMagnitude > 0.1f;
        }
    }

    void FixedUpdate()
    {
        if (!IsGameplayScene()) return;
        if (isDashing) return;

        // Smooth movement
        Vector3 targetVelocity = movementDirection * movementSpeedNormal;
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, lerpSpeed);

        Vector3 moveDelta = new Vector3(currentVelocity.x, 0, currentVelocity.z) * Time.fixedDeltaTime;

        // When grabbing, player and luggage move as ONE UNIT
        // SweepTest the luggage first — if it would hit a wall, constrain BOTH
        if (isGrabbing && playerGrab != null)
        {
            Luggage held = playerGrab.GetHeldLuggage();
            if (held != null)
            {
                Rigidbody luggageRb = held.GetComponent<Rigidbody>();

                if (luggageRb != null && moveDelta.sqrMagnitude > 0.0001f)
                {
                    // Check if luggage would collide if moved in this direction
                    RaycastHit hit;
                    if (luggageRb.SweepTest(moveDelta.normalized, out hit, moveDelta.magnitude + 0.05f))
                    {
                        // Wall detected — slide along it instead of stopping completely
                        Vector3 wallNormal = hit.normal;
                        wallNormal.y = 0;
                        wallNormal.Normalize();

                        // Remove the into-wall component from movement
                        float intoWall = Vector3.Dot(moveDelta, -wallNormal);
                        if (intoWall > 0)
                        {
                            moveDelta += wallNormal * intoWall;
                        }

                        // Also fix velocity so we don't keep building speed into the wall
                        float velIntoWall = Vector3.Dot(currentVelocity, -wallNormal);
                        if (velIntoWall > 0)
                        {
                            currentVelocity += wallNormal * velIntoWall;
                        }
                    }
                }

                // Move BOTH by the same delta — they are one unit
                transform.position += moveDelta;
                if (luggageRb != null)
                {
                    luggageRb.MovePosition(luggageRb.position + moveDelta);
                }
            }
            else
            {
                transform.position += moveDelta;
            }
        }
        else
        {
            transform.position += moveDelta;
        }

        bool isTryingToMove = movementDirection.sqrMagnitude > 0.1f;
        animator.SetBool("isMoving", isTryingToMove);

        // Rotation: always face movement direction
        if (isTryingToMove)
        {
            Vector3 flatDir = new Vector3(movementDirection.x, 0, movementDirection.z).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(flatDir);

            float currentRotSpeed = isGrabbing ? rotationSpeed * grabRotationSpeedMultiplier : rotationSpeed;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, currentRotSpeed * Time.fixedDeltaTime);
        }
    }

    // Called by DesignSceneInput to bypass PlayerInput in the design scene
    public void InjectInput(Vector2 move, bool dashPressed)
    {
        if (dashPressed && !isDashing && Time.time >= lastDashTime + dashCooldown)
        {
            StartCoroutine(DashCoroutine());
            return;
        }

        if (!isDashing)
        {
            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 right   = cameraTransform != null ? cameraTransform.right   : Vector3.right;
            forward.y = 0; right.y = 0;
            forward.Normalize(); right.Normalize();
            movementDirection = (move.x * right + move.y * forward).normalized;
            isMoving = movementDirection.sqrMagnitude > 0.1f;
        }
    }

    private IEnumerator DashCoroutine()
    {
        isDashing = true;
        lastDashTime = Time.time;

        Vector3 dashDir = transform.forward.normalized;
        animator.SetBool("isDashing", true);

        float timer = 0f;
        while (timer < dashDuration)
        {
            transform.position += dashDir * dashForce * Time.deltaTime;
            timer += Time.deltaTime;
            yield return null;
        }

        currentVelocity = Vector3.zero;
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