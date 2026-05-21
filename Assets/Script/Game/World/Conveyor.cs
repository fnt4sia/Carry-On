using System.Collections.Generic;
using UnityEngine;

// Belt that takes ownership of luggage resting on its top surface and moves it kinematically
// along moveDirection. A trigger volume flags nearby luggage as candidates; the conveyor only
// drives a luggage when a downward raycast confirms it is resting on this conveyor's surface,
// so luggage merely bumping the side is left to physics. Ownership is released — and momentum
// handed back — when the luggage is grabbed, enters a station, or leaves the surface.
public class Conveyor : MonoBehaviour
{
    [Header("Conveyor Settings")]
    [SerializeField] private Vector3 moveDirection = Vector3.forward;

    [SerializeField] private float moveSpeed = 2f;
    [Tooltip("How smoothly the luggage steers. Higher = tighter corner, Lower = slides wide.")]
    [SerializeField] private float turnSmoothness = 6f;
    [SerializeField] private bool isFreezeRotation = false;

    [Header("Surface Check")]
    [Tooltip("Extra reach below a luggage's base for the on-surface raycast. Higher = more forgiving.")]
    [SerializeField] private float surfaceCheckMargin = 0.25f;

    private List<Rigidbody> rigidbodiesOnConveyor = new List<Rigidbody>();
    private Collider surfaceCollider;

    private void Awake()
    {
        // The solid (non-trigger) collider on the conveyor root counts as "the surface".
        foreach (var col in GetComponentsInParent<Collider>(true))
        {
            if (!col.isTrigger) { surfaceCollider = col; break; }
        }
    }

    private void FixedUpdate()
    {
        for (int i = rigidbodiesOnConveyor.Count - 1; i >= 0; i--)
        {
            Rigidbody rb = rigidbodiesOnConveyor[i];

            if (rb == null || !rb.gameObject.activeInHierarchy)
            {
                rigidbodiesOnConveyor.RemoveAt(i);
                continue;
            }

            Luggage luggage = rb.GetComponent<Luggage>();
            if (luggage == null)
            {
                rigidbodiesOnConveyor.RemoveAt(i);
                continue;
            }

            // Station ownership overrides conveyor movement
            if (luggage.IsInStation)
            {
                if (luggage.ActiveConveyor == this)
                    luggage.ActiveConveyor = null;
                rigidbodiesOnConveyor.RemoveAt(i);
                continue;
            }

            // A grabbed luggage belongs to the player — drop conveyor ownership but keep
            // tracking it so it is picked up again if dropped back onto the belt.
            if (luggage.GetIsGrabbed())
            {
                if (luggage.ActiveConveyor == this)
                {
                    luggage.ActiveConveyor = null;
                    rb.isKinematic = false;
                }
                continue;
            }

            // Only drive luggage actually resting on this conveyor's surface; luggage that
            // merely touches the side fails the raycast and is left to normal physics.
            if (!IsLuggageOnSurface(rb, out float surfaceY))
            {
                if (luggage.ActiveConveyor == this)
                    ReleaseLuggage(rb, luggage);
                rigidbodiesOnConveyor.RemoveAt(i);
                continue;
            }

            if (luggage.ActiveConveyor == null)
                TakeOwnership(rb, luggage);
            else if (luggage.ActiveConveyor != this)
                continue;

            Vector3 targetVelocity = moveDirection.normalized * moveSpeed;
            luggage.kinematicVelocity = Vector3.Lerp(luggage.kinematicVelocity, targetVelocity, Time.fixedDeltaTime * turnSmoothness);

            Vector3 nextPos = rb.position + luggage.kinematicVelocity * Time.fixedDeltaTime;
            // Seat the luggage on the belt instead of freezing it at whatever height it was grabbed.
            nextPos.y = float.IsNaN(surfaceY) ? rb.position.y : ResolveRestHeight(rb, surfaceY);
            rb.MovePosition(nextPos);
        }
    }

    // True when a downward ray from the luggage's center reaches this conveyor's surface.
    // Outputs the surface contact height so the belt can seat the luggage on top of it.
    private bool IsLuggageOnSurface(Rigidbody rb, out float surfaceY)
    {
        surfaceY = float.NaN;
        if (surfaceCollider == null) return true; // no surface assigned — fall back to trigger-only

        Collider luggageCollider = rb.GetComponentInChildren<Collider>();
        Vector3 origin = luggageCollider != null ? luggageCollider.bounds.center : rb.position;
        float reach = (luggageCollider != null ? luggageCollider.bounds.extents.y : 0.5f) + surfaceCheckMargin;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider == surfaceCollider)
        {
            surfaceY = hit.point.y;
            return true;
        }

        return false;
    }

    // Y position that seats the luggage's base exactly on the belt surface.
    private float ResolveRestHeight(Rigidbody rb, float surfaceY)
    {
        Collider luggageCollider = rb.GetComponentInChildren<Collider>();
        if (luggageCollider == null) return rb.position.y;
        float baseOffset = rb.position.y - luggageCollider.bounds.min.y;
        return surfaceY + baseOffset;
    }

    private void TakeOwnership(Rigidbody rb, Luggage luggage)
    {
        luggage.ActiveConveyor = this;

        // Only rip velocity if it wasn't already transferring kinematically from another conveyor
        if (!rb.isKinematic)
        {
            luggage.kinematicVelocity = rb.linearVelocity;
            rb.isKinematic = true;
        }

        if (isFreezeRotation)
            rb.constraints = RigidbodyConstraints.FreezeRotationX;
    }

    private void ReleaseLuggage(Rigidbody rb, Luggage luggage)
    {
        luggage.ActiveConveyor = null;
        rb.isKinematic = false;

        // Return the math momentum back to the physics engine
        rb.linearVelocity = luggage.kinematicVelocity;

        if (isFreezeRotation)
            rb.constraints = RigidbodyConstraints.None;
    }

    private void OnTriggerEnter(Collider other) => TrackLuggage(other);
    private void OnTriggerStay(Collider other) => TrackLuggage(other);

    // Registers a luggage as a candidate. Trigger events fire unreliably — toggling a
    // Rigidbody's isKinematic re-fires exit/enter spuriously — so OnTriggerStay re-adds any
    // luggage a spurious exit dropped. Ownership is decided only in FixedUpdate.
    private void TrackLuggage(Collider other)
    {
        if (!other.CompareTag("Luggage")) return;

        Rigidbody luggageRb = other.GetComponentInParent<Rigidbody>();
        if (luggageRb != null && !rigidbodiesOnConveyor.Contains(luggageRb))
            rigidbodiesOnConveyor.Add(luggageRb);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Luggage")) return;

        Rigidbody luggageRb = other.GetComponentInParent<Rigidbody>();
        if (luggageRb == null) return;

        // Luggage we own can report a spurious exit when its isKinematic toggles — keep owned
        // luggage in the list so FixedUpdate stays in charge of releasing it.
        Luggage luggage = luggageRb.GetComponent<Luggage>();
        if (luggage != null && luggage.ActiveConveyor == this) return;

        rigidbodiesOnConveyor.Remove(luggageRb);
    }
}
