using System.Collections;
using UnityEngine;

// A cab that closes its door, travels to another height, then opens the door again.
// Actuated by a PressurePlate or a Lever through the same Open/Close/Toggle API Gateway
// uses, so the actuator needs no special casing:
//   Open()  -> travel to the raised floor
//   Close() -> travel back to the start floor
//   Toggle() -> swap floors
//
// The door is a Gateway — the same animated two-leaf sliding door the level gates use — so the
// slide itself is an animation transition, not something this script drives. All this needs to
// know is how long to wait for it.
[DisallowMultipleComponent]
public class Elevator : MonoBehaviour
{
    [Header("Travel")]
    [Tooltip("The cab that moves. Leave empty to move this object.")]
    [SerializeField] private Transform liftBody;
    [Tooltip("How far above the authored position the raised floor sits, in local units.")]
    [SerializeField] private float travelHeight = 6f;
    [Tooltip("Local units per second. 0 snaps instantly.")]
    [SerializeField, Min(0f)] private float travelSpeed = 3f;
    [Tooltip("Beat held after the door shuts and again on arrival, before the door reopens.")]
    [SerializeField, Min(0f)] private float doorPause = 0.25f;
    [Tooltip("On = the cab starts at the raised floor.")]
    [SerializeField] private bool startRaised;

    [Header("Door")]
    [Tooltip("Optional. Without one the cab just travels.")]
    [SerializeField] private Gateway door;
    [Tooltip("How long the door's open/close animation takes. The cab waits this out before moving.")]
    [SerializeField, Min(0f)] private float doorTravelTime = 0.35f;

    private Rigidbody liftRigidbody;
    private Vector3 startLocalPosition;
    private bool isRaised;
    private Coroutine travelRoutine;

    public bool IsRaised => isRaised;

    private void Awake()
    {
        if (liftBody == null)
            liftBody = transform;

        liftRigidbody = liftBody.GetComponent<Rigidbody>();
        startLocalPosition = liftBody.localPosition;

        isRaised = startRaised;
        // Snap, not MovePosition: physics has not stepped yet at Awake.
        liftBody.localPosition = TargetLocalPosition;
    }

    public void Open() => SetRaised(true);

    public void Close() => SetRaised(false);

    public void Toggle() => SetRaised(!isRaised);

    public void SetRaised(bool raised)
    {
        // Already there and idle: nothing to replay. Mid-travel the request always restarts the
        // sequence, which is how a reversal partway up works.
        if (raised == isRaised && travelRoutine == null) return;

        isRaised = raised;

        if (travelRoutine != null) StopCoroutine(travelRoutine);
        travelRoutine = StartCoroutine(TravelRoutine());
    }

    private Vector3 TargetLocalPosition =>
        startLocalPosition + Vector3.up * (isRaised ? travelHeight : 0f);

    private IEnumerator TravelRoutine()
    {
        if (door != null && door.IsOpen)
        {
            door.Close();
            yield return new WaitForSeconds(doorTravelTime);

            if (doorPause > 0f) yield return new WaitForSeconds(doorPause);
        }

        Vector3 target = TargetLocalPosition;
        while ((liftBody.localPosition - target).sqrMagnitude > 1e-8f)
        {
            Vector3 next = travelSpeed > 0f
                ? Vector3.MoveTowards(liftBody.localPosition, target, travelSpeed * Time.fixedDeltaTime)
                : target;

            // MovePosition when the cab is kinematic so riders are carried by the sweep instead
            // of being depenetrated out of the floor after the fact.
            if (liftRigidbody != null && liftRigidbody.isKinematic)
            {
                Transform parent = liftBody.parent;
                liftRigidbody.MovePosition(parent != null ? parent.TransformPoint(next) : next);
            }
            else
            {
                liftBody.localPosition = next;
            }

            yield return new WaitForFixedUpdate();
        }

        if (door != null)
        {
            if (doorPause > 0f) yield return new WaitForSeconds(doorPause);
            door.Open();
        }

        travelRoutine = null;
    }
}
