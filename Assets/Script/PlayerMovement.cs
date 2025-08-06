using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class PlayerMovement : MonoBehaviour
{

    public int playerId;
    public bool isGrabbing;

    [SerializeField] private Rigidbody playerRb;
    [SerializeField] private float movementSpeedNormal;
    [SerializeField] private float movementSpeedShift;
    [SerializeField] private float rotationSpeedNormal;
    [SerializeField] private float rotationSpeedGrab;
    [SerializeField] private float lerpSpeed;
    [SerializeField] GameObject bubblePrefab; 
    [SerializeField] float bubbleSpawnInterval;
    [SerializeField] Vector3 bubbleOffsetRange;
    [SerializeField] Animator animator;
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip dashClip;

    [Header("Dash")]
    [SerializeField] private float dashForce;
    [SerializeField] private float dashDuration;
    [SerializeField] private float dashCooldown;
    private bool isDashing = false;
    private float dashTimer = 0f;
    private float lastDashTime = -10f;

    private Transform cameraTransform;
    private float horizontalInput;
    private float verticalInput;
    private bool shiftInput;
    private bool isMoving;
    private Vector3 movementDirection;

    private void Start()
    {
        cameraTransform = FindObjectOfType<Camera>().transform;
        StartCoroutine(SpawnBubblesCoroutine());
    }

    void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal_p" + playerId.ToString());
        verticalInput = Input.GetAxisRaw("Vertical_p" + playerId.ToString());

        bool dashInput = false;
        if (playerId == 1)
            dashInput = Input.GetKeyDown(KeyCode.Joystick1Button5) || Input.GetKeyDown(KeyCode.LeftShift);
        else if (playerId == 2)
            dashInput = Input.GetKeyDown(KeyCode.Joystick2Button5) || Input.GetKeyDown(KeyCode.RightShift);

        // Dash logic
        if (dashInput && !isDashing && Time.time >= lastDashTime + dashCooldown)
        {
            StartCoroutine(DashCoroutine());
            return; // Don't process movement this frame
        }

        // Only process normal movement if not dashing
        if (!isDashing)
        {
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;

            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            movementDirection = (horizontalInput * right + verticalInput * forward).normalized;
            isMoving = movementDirection.sqrMagnitude > 0.1f;
        }
    }

    void FixedUpdate()
    {

        Vector3 currentVelocity = playerRb.velocity;

        Vector3 flatMovement = shiftInput ? movementDirection * movementSpeedShift : movementDirection * movementSpeedNormal;
        Vector3 finalVelocity = new(flatMovement.x, currentVelocity.y, flatMovement.z);
        playerRb.velocity = Vector3.Lerp(playerRb.velocity, finalVelocity, lerpSpeed);

        if (movementDirection.sqrMagnitude > 0.1f && !shiftInput)
        {
            animator.SetBool("isMoving", true);
            Vector3 flatDir = new Vector3(movementDirection.x, 0, movementDirection.z).normalized;
            if (flatDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(flatDir);
                Quaternion smoothedRotation = isGrabbing
                    ? Quaternion.Slerp(playerRb.rotation, targetRotation, rotationSpeedGrab * Time.fixedDeltaTime)
                    : Quaternion.Slerp(playerRb.rotation, targetRotation, rotationSpeedNormal * Time.fixedDeltaTime);
                playerRb.MoveRotation(smoothedRotation);
            }
        }else animator.SetBool("isMoving", false);
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
                    Random.Range(-bubbleOffsetRange.z, bubbleOffsetRange.z)
                );

                Vector3 spawnPos = (transform.position + new Vector3(0, -0.6f, 0)) + offset;
                GameObject bubble = Instantiate(bubblePrefab, spawnPos, Quaternion.identity);
                StartCoroutine(AnimateBubble(bubble));
            }

            yield return new WaitForSeconds(bubbleSpawnInterval);
        }
    }

    private IEnumerator DashCoroutine()
    {
        isDashing = true;
        lastDashTime = Time.time;

        Vector3 dashDir = transform.forward;
        dashDir.Normalize();

        animator.SetBool("isDashing", true);
        audioSource.PlayOneShot(dashClip);

        dashTimer = 0f;
        while (dashTimer < dashDuration)
        {
            playerRb.velocity = dashDir * dashForce;
            dashTimer += Time.deltaTime;
            yield return null;
        }

        animator.SetBool("isDashing", false);
        isDashing = false;
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
            Color newColor = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t));
            mat.color = newColor;

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(bubble);
    }
}
