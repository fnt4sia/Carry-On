using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One player's plane token on the stage-select map.
///
/// Movement is deliberately unsmoothed: full speed on the frame the stick moves, zero on
/// the frame it stops. There is no acceleration and no SmoothDamp, which is what makes it
/// read as snappy next to the gameplay controller.
/// </summary>
[DisallowMultipleComponent]
public class MapPlane : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 22f;
    [Tooltip("Degrees per second the model yaws toward travel. High values read as a snap.")]
    [SerializeField, Min(0f)] private float turnSpeed = 1080f;
    [Tooltip("Model child that yaws. Leave empty to turn the whole token.")]
    [SerializeField] private Transform model;

    /// <summary>playerIndex of the player driving this plane.</summary>
    public int PlayerIndex { get; private set; }

    /// <summary>Node this plane is currently parked on. Assigned by <see cref="MapController"/>.</summary>
    public LevelNode CurrentNode { get; set; }

    private InputAction moveAction;
    private Transform cameraTransform;
    private Vector3 mapCenter;
    private Vector2 mapHalfExtents = new(60f, 42f);
    private float flightHeight;

    public void Initialize(int playerIndex, InputAction move, Vector3 center, Vector2 halfExtents)
    {
        PlayerIndex = playerIndex;
        moveAction = move;
        mapCenter = center;
        mapHalfExtents = halfExtents;
        flightHeight = transform.position.y;
        if (model == null)
            model = transform;
    }

    private void Update()
    {
        if (moveAction == null)
            return;

        // The map is read at the same isometric angle as a gameplay scene, so "up" has to
        // mean "away from the camera" here exactly like it does for PlayerMovement.
        if (cameraTransform == null || !cameraTransform.gameObject.activeInHierarchy)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;
            cameraTransform = cam.transform;
        }

        Vector2 input = moveAction.ReadValue<Vector2>();
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        // A straight-down map camera has no horizontal forward left after flattening, which
        // would normalise to zero. Its up vector is the screen-up direction in that case.
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = cameraTransform.up;
            forward.y = 0f;
        }

        Vector3 right = cameraTransform.right;
        right.y = 0f;
        forward.Normalize(); right.Normalize();

        Vector3 direction = input.x * right + input.y * forward;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector3 next = transform.position + direction * (moveSpeed * Time.deltaTime);
        next.x = Mathf.Clamp(next.x, mapCenter.x - mapHalfExtents.x, mapCenter.x + mapHalfExtents.x);
        next.z = Mathf.Clamp(next.z, mapCenter.z - mapHalfExtents.y, mapCenter.z + mapHalfExtents.y);
        next.y = flightHeight;
        transform.position = next;

        Quaternion facing = Quaternion.LookRotation(direction, Vector3.up);
        model.rotation = Quaternion.RotateTowards(model.rotation, facing, turnSpeed * Time.deltaTime);
    }
}
