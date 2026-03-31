using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public bool isGrabbing;

    [SerializeField] private Rigidbody playerRb;
    [SerializeField] private float movementSpeedNormal;
    [SerializeField] private float rotationSpeed;
    [SerializeField] private float lerpSpeed;
    [SerializeField] private GameObject bubblePrefab;
    [SerializeField] private float bubbleSpawnInterval;
    [SerializeField] private Vector3 bubbleOffsetRange;
    [SerializeField] private Animator animator;

    [Header("Dash")]
    [SerializeField] private float dashForce;
    [SerializeField] private float dashDuration;
    [SerializeField] private float dashCooldown;

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction dashAction;
    private PlayerGrab playerGrab;

    private bool isDashing = false;
    private float lastDashTime = -10f;

    private Transform cameraTransform;
    private Vector2 moveInput;
    private Vector3 movementDirection;
    private bool isMoving;

    private bool isPlaying;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
        dashAction = playerInput.actions["Dash"];
        playerGrab = GetComponent<PlayerGrab>();
    }

    private void Start()
    {
        cameraTransform = FindAnyObjectByType<Camera>().transform;
        StartCoroutine(SpawnBubblesCoroutine());
    }

    void Update()
    {
        // Removed `if(!isPlaying) return;` because it is preventing movement.

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
        float currentSpeed = movementSpeedNormal;

        if (isGrabbing && playerGrab != null && playerGrab.GetHeldLuggage() != null)
        {
            Luggage heldLuggage = playerGrab.GetHeldLuggage();
            int grabbers = Mathf.Max(1, heldLuggage.GetGrabberCount());
            
            if (heldLuggage.weightClass == WeightClass.Heavy)
            {
                currentSpeed *= (grabbers > 1) ? 0.6f : 0.3f;
            }
            else if (heldLuggage.weightClass == WeightClass.Medium)
            {
                currentSpeed *= (grabbers > 1) ? 1.0f : 0.5f;
            }
        }

        Vector3 currentVelocity = playerRb.linearVelocity;
        Vector3 flatMovement = movementDirection * currentSpeed;
        Vector3 finalVelocity = new(flatMovement.x, currentVelocity.y, flatMovement.z);

        playerRb.linearVelocity = Vector3.Lerp(playerRb.linearVelocity, finalVelocity, lerpSpeed);

        if (movementDirection.sqrMagnitude > 0.1f)
        {
            animator.SetBool("isMoving", true);
            Vector3 flatDir = new Vector3(movementDirection.x, 0, movementDirection.z).normalized;

            Quaternion targetRotation = Quaternion.LookRotation(flatDir);
            
            float rSpeed = rotationSpeed;

            if (isGrabbing && playerGrab != null && playerGrab.GetHeldLuggage() != null)
            {
                Luggage heldLuggage = playerGrab.GetHeldLuggage();
                int grabbers = Mathf.Max(1, heldLuggage.GetGrabberCount());

                if (heldLuggage.weightClass == WeightClass.Heavy)
                {
                    rSpeed = (grabbers > 1) ? (rotationSpeed * 0.6f) : 0f;
                }
                else if (heldLuggage.weightClass == WeightClass.Medium)
                {
                    rSpeed = (grabbers > 1) ? rotationSpeed : (rotationSpeed * 0.5f);
                }
            }

            Quaternion smoothedRotation = Quaternion.Slerp(playerRb.rotation, targetRotation, rSpeed * Time.fixedDeltaTime);

            if (!isGrabbing || rSpeed > 0f)
            {
                playerRb.MoveRotation(smoothedRotation);
            }
        }
        else animator.SetBool("isMoving", false);
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
            playerRb.linearVelocity = dashDir * dashForce;
            timer += Time.deltaTime;
            yield return null;
        }

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
