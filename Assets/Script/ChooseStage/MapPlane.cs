using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// One player's plane token on the stage-select map.
///
/// Moves with the same feel as PlayerMovement: velocity eases toward the stick direction and
/// the model slerps to face travel, using the player's two tuning numbers. The player runs
/// them once per 50 Hz physics step; this converts them to per-frame fractions so the plane
/// glides smoothly at any frame rate but accelerates, stops and turns exactly as fast. The
/// bubble trail is the player's WalkSmoke particle system, copied onto the tail.
///
/// Whose plane is whose: the body is painted in the player's colour and the characters' own
/// "1P" pin floats over it. The pin is the Player Indicator prefab nested without its
/// PlayerIndicator component — that one follows a PlayerInput, and the players are hidden while
/// the map is open — so this fills in the label and disc instead.
///
/// The propeller spins about its hub, a marker at the propeller's centre: the propeller's own
/// pivot isn't on its axis.
/// </summary>
[DisallowMultipleComponent]
public class MapPlane : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 22f;
    [Tooltip("Same meaning and default as PlayerMovement.movementLerpSpeed: the share of the gap " +
             "to the target velocity closed per 50 Hz physics step.")]
    [SerializeField, Range(0f, 1f)] private float movementLerpSpeed = 0.15f;
    [Tooltip("Same meaning and default as PlayerMovement.rotationSpeed.")]
    [SerializeField, Min(0f)] private float rotationSpeed = 4f;
    [Tooltip("Model child that yaws. Leave empty to turn the whole token.")]
    [SerializeField] private Transform model;

    [Header("Landing")]
    [Tooltip("Height above a node's pivot the plane settles at while parked on an unlocked node. " +
             "Node pivots sit at the island's ground level and the plane's pivot is its middle, " +
             "so this is about half the model's height.")]
    [SerializeField] private float landingClearance = 0.9f;
    [Tooltip("Seconds to settle onto the runway or climb back to cruise.")]
    [SerializeField, Min(0.01f)] private float landingSmoothTime = 0.35f;

    [Header("Player identity")]
    [Tooltip("Renderers painted in the player's colour: the body, wings and tail fin.")]
    [SerializeField] private Renderer[] paintedParts;
    [Tooltip("World-space pin over the plane. Turned to face the camera every frame.")]
    [SerializeField] private Transform pin;
    [SerializeField] private Image pinDisc;
    [SerializeField] private TMP_Text pinLabel;

    [Header("Propeller")]
    [Tooltip("The propeller part of the model: blades and spinner.")]
    [SerializeField] private Transform propeller;
    [Tooltip("Marker at the centre of the propeller, its forward along the nose. The propeller " +
             "spins about this, not about its own pivot.")]
    [SerializeField] private Transform propellerHub;
    [Tooltip("Degrees per second. Much faster and the three blades strobe at 60 fps.")]
    [SerializeField] private float propellerSpeed = 900f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    /// <summary>playerIndex of the player driving this plane.</summary>
    public int PlayerIndex { get; private set; }

    /// <summary>The driving player's identity colour, read from their PlayerIndicator.</summary>
    public Color PlayerColor { get; private set; } = Color.white;

    /// <summary>Node this plane is currently parked on. Assigned by <see cref="MapController"/>.</summary>
    public LevelNode CurrentNode { get; set; }

    /// <summary>Set while the group boards: the stick is ignored, so the plane stays parked.</summary>
    public bool Locked { get; set; }

    private InputAction moveAction;
    private Transform cameraTransform;
    private Vector3 mapCenter;
    private Vector2 mapHalfExtents = new(60f, 42f);
    private float flightHeight;
    private Vector3 velocity;
    private float climbVelocity;

    public void Initialize(int playerIndex, Color playerColor, InputAction move, Vector3 center, Vector2 halfExtents)
    {
        PlayerIndex = playerIndex;
        PlayerColor = playerColor;
        moveAction = move;
        mapCenter = center;
        mapHalfExtents = halfExtents;
        flightHeight = transform.position.y;
        if (model == null)
            model = transform;

        Paint(playerColor);
        if (pinDisc != null)
            pinDisc.color = playerColor;
        if (pinLabel != null)
            pinLabel.text = $"{playerIndex + 1}P";
    }

    private void Paint(Color color)
    {
        // A property block, not a material copy: the FBX's materials stay shared and nothing
        // needs cleaning up when the plane is destroyed.
        var block = new MaterialPropertyBlock();
        foreach (Renderer part in paintedParts)
        {
            if (part == null)
                continue;
            part.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            part.SetPropertyBlock(block);
        }
    }

    private void Update()
    {
        if (moveAction == null || !ResolveCamera())
            return;

        // Stick-up means screen-up, so steer along the camera's flattened up vector. Its
        // flattened forward only agrees while the camera pitches less than 90°; past vertical
        // (the old top-down map camera sat at 96°) forward points down the screen.
        Vector2 input = Locked ? Vector2.zero : moveAction.ReadValue<Vector2>();

        Vector3 forward = cameraTransform.up;
        forward.y = 0f;
        // A camera aimed at the horizon has no horizontal up left after flattening; its
        // forward is screen-up in that case.
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = cameraTransform.forward;
            forward.y = 0f;
        }

        Vector3 right = cameraTransform.right;
        right.y = 0f;
        forward.Normalize(); right.Normalize();

        // Normalised like PlayerMovement: any stick tilt past the dead zone is full speed.
        Vector3 direction = (input.x * right + input.y * forward).normalized;
        bool moving = direction.sqrMagnitude > 0.1f;

        // (1 - k)^(frames per physics step) turns "k per 50 Hz step" into this frame's share.
        float steps = Time.deltaTime / Time.fixedDeltaTime;
        velocity = Vector3.Lerp(velocity, direction * moveSpeed, 1f - Mathf.Pow(1f - movementLerpSpeed, steps));

        Vector3 next = transform.position + velocity * Time.deltaTime;
        float minX = mapCenter.x - mapHalfExtents.x, maxX = mapCenter.x + mapHalfExtents.x;
        float minZ = mapCenter.z - mapHalfExtents.y, maxZ = mapCenter.z + mapHalfExtents.y;
        // Kill the velocity into the edge so the plane slides along it instead of pressing
        // against it and then lurching away when the stick turns.
        if (next.x < minX || next.x > maxX) velocity.x = 0f;
        if (next.z < minZ || next.z > maxZ) velocity.z = 0f;
        next.x = Mathf.Clamp(next.x, minX, maxX);
        next.z = Mathf.Clamp(next.z, minZ, maxZ);

        // Touching down on an island is the plane's half of "you're on this level" — the
        // badge and the landing ring are the other half. Only unlocked stages are ever a
        // plane's CurrentNode, so it never lands on one it can't play.
        bool landed = CurrentNode != null;
        float cruise = landed ? CurrentNode.transform.position.y + landingClearance : flightHeight;
        next.y = Mathf.SmoothDamp(transform.position.y, cruise, ref climbVelocity, landingSmoothTime);
        transform.position = next;

        if (moving)
        {
            float turn = 1f - Mathf.Pow(1f - Mathf.Clamp01(rotationSpeed * Time.fixedDeltaTime), steps);
            model.rotation = Quaternion.Slerp(model.rotation, Quaternion.LookRotation(direction), turn);
        }
    }

    private void LateUpdate()
    {
        if (propeller != null && propellerHub != null)
            propeller.RotateAround(propellerHub.position, propellerHub.forward, propellerSpeed * Time.deltaTime);

        // The map camera holds one angle, so copying its rotation is the whole billboard —
        // same as the badges and the characters' own pins.
        if (pin != null && ResolveCamera())
            pin.rotation = cameraTransform.rotation;
    }

    private bool ResolveCamera()
    {
        if (cameraTransform != null && cameraTransform.gameObject.activeInHierarchy)
            return true;

        Camera cam = Camera.main;
        cameraTransform = cam != null ? cam.transform : null;
        return cameraTransform != null;
    }
}
