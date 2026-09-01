using UnityEngine;

// A panel that slides aside and back automatically, alternating forever on a timer. No
// PressurePlate or Lever needed — it just runs. Same slide mechanics as SlidingPanel, so pick
// whichever one a given prop needs.
//
// Use: background scenery doors, conveyor gates, anything that should breathe open/closed
// without being actuated.
//
// The pose the panel is authored in is always the CLOSED pose. `startOpen` snaps it to the
// open pose at Awake, matching SlidingPanel.
[DisallowMultipleComponent]
public class AutoSlidingPanel : MonoBehaviour
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
    [Tooltip("On = already slid aside at level start.")]
    [SerializeField] private bool startOpen;

    [Header("Auto Cycle")]
    [Tooltip("Seconds to sit closed before opening again.")]
    [SerializeField, Min(0f)] private float closedDwell = 2f;
    [Tooltip("Seconds to sit open before closing again.")]
    [SerializeField, Min(0f)] private float openDwell = 2f;
    [Tooltip("Off = timer runs from Awake using startOpen's dwell. On = timer starts counting from a fresh dwell, ignoring however far into it startOpen would imply.")]
    [SerializeField] private bool resetTimerOnAwake = true;

    private Rigidbody slidingRigidbody;
    private Vector3 closedLocalPosition;
    private Vector3 openLocalPosition;
    private bool isOpen;
    private float dwellTimer;

    public bool IsOpen => isOpen;

    // True while the body has not reached its target pose. MoveTowards lands exactly on the
    // target, so this goes false on the step the slide finishes.
    public bool IsMoving => slidingBody != null &&
                            (slidingBody.localPosition - TargetLocalPosition).sqrMagnitude > 1e-8f;

    private Vector3 TargetLocalPosition => isOpen ? openLocalPosition : closedLocalPosition;
    private float CurrentDwell => isOpen ? openDwell : closedDwell;

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

        dwellTimer = resetTimerOnAwake ? 0f : dwellTimer;
    }

    private void FixedUpdate()
    {
        // Count the dwell down even while mid-slide, so slideSpeed and dwell overlap the same
        // way they would if a Lever were firing Toggle() on a timer.
        dwellTimer += Time.fixedDeltaTime;
        if (dwellTimer >= CurrentDwell)
        {
            dwellTimer -= CurrentDwell;
            isOpen = !isOpen;
        }

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