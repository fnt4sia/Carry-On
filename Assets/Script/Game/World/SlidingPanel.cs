using UnityEngine;

// A panel that slides aside when actuated and slides back when closed. Open/Close/Toggle
// mirror Gateway, so a PressurePlate or Lever drives it with no special casing: toggle mode
// flips it once per press, momentary mode holds it aside while the plate is occupied.
//
// Two users: the Sliding Glass prop, and the door of Elevator.
//
// The pose the panel is authored in is always the CLOSED pose. `startOpen` snaps it to the
// open pose at Awake, so a door that starts open is still authored where it blocks.
[DisallowMultipleComponent]
public class SlidingPanel : MonoBehaviour
{
    // Axes are the sliding body's own, so "Right" means the panel's right no matter how the
    // prop is rotated in the level.
    public enum SlideAxis { Right, Left, Up, Down, Forward, Back }

    [Header("Slide")]
    [Tooltip("The transform that moves. Leave empty to slide this object.")]
    [SerializeField] private Transform slidingBody;
    [SerializeField] private SlideAxis slideDirection = SlideAxis.Right;
    [Tooltip("How far it slides, in the sliding body's local units.")]
    [SerializeField, Min(0f)] private float slideDistance = 2f;
    [Tooltip("Local units per second. 0 snaps instantly.")]
    [SerializeField, Min(0f)] private float slideSpeed = 3f;
    [Tooltip("On = already slid aside at level start (an elevator door starts open).")]
    [SerializeField] private bool startOpen;

    private Rigidbody slidingRigidbody;
    private Vector3 closedLocalPosition;
    private Vector3 openLocalPosition;
    private bool isOpen;

    public bool IsOpen => isOpen;

    // True while the body has not reached its target pose. MoveTowards lands exactly on the
    // target, so this goes false on the step the slide finishes. Elevator waits on it.
    public bool IsMoving => slidingBody != null &&
                            (slidingBody.localPosition - TargetLocalPosition).sqrMagnitude > 1e-8f;

    private Vector3 TargetLocalPosition => isOpen ? openLocalPosition : closedLocalPosition;

    private void Awake()
    {
        if (slidingBody == null)
            slidingBody = transform;

        slidingRigidbody = slidingBody.GetComponent<Rigidbody>();
        closedLocalPosition = slidingBody.localPosition;
        openLocalPosition = closedLocalPosition +
                            slidingBody.localRotation * AxisVector(slideDirection) * slideDistance;

        isOpen = startOpen;
        // Snap, not MovePosition: physics has not stepped yet at Awake.
        slidingBody.localPosition = TargetLocalPosition;
    }

    public void Open() => SetOpen(true);

    public void Close() => SetOpen(false);

    public void Toggle() => SetOpen(!isOpen);

    public void SetOpen(bool open) => isOpen = open;

    private void FixedUpdate()
    {
        if (!IsMoving) return;

        Vector3 target = TargetLocalPosition;
        Vector3 next = slideSpeed > 0f
            ? Vector3.MoveTowards(slidingBody.localPosition, target, slideSpeed * Time.fixedDeltaTime)
            : target;

        // MovePosition when the body is kinematic so it sweeps players and luggage out of the
        // way instead of letting PhysX resolve an interpenetration after the fact.
        if (slidingRigidbody != null && slidingRigidbody.isKinematic)
        {
            Transform parent = slidingBody.parent;
            slidingRigidbody.MovePosition(parent != null ? parent.TransformPoint(next) : next);
        }
        else
        {
            slidingBody.localPosition = next;
        }
    }

    private static Vector3 AxisVector(SlideAxis axis) => axis switch
    {
        SlideAxis.Right => Vector3.right,
        SlideAxis.Left => Vector3.left,
        SlideAxis.Up => Vector3.up,
        SlideAxis.Down => Vector3.down,
        SlideAxis.Forward => Vector3.forward,
        _ => Vector3.back,
    };
}
