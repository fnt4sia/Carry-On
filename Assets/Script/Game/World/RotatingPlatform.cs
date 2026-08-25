using System.Collections.Generic;
using UnityEngine;

// Rotates a platform body and carries players/luggage standing inside its rider zone.
// The rider zone is a child trigger collider under the same Rigidbody.
// A PressurePlate calls ReverseDirection to flip clockwise/counter-clockwise; the plate
// owns that connection, so the platform keeps no reference back to it.
//
// Riders are re-discovered every FixedUpdate by overlapping the rider zone rather than by
// counting OnTriggerEnter/Exit pairs. A missed exit — a collider disabled while standing on
// the platform, a teleport, a grab that swaps colliders — used to leave a rider registered
// forever, and because the carry maths is relative to the pivot, the further that ghost rider
// walked away the faster the platform flung them. Re-querying each step cannot drift.
[DisallowMultipleComponent]
public class RotatingPlatform : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] private Transform rotatingBody;
    // Degrees per second. Tip speed is this times the platform's radius, so a long beam gets
    // fast at the ends — keep it well under the player's 10 u/s walk speed or riders can't stand.
    [SerializeField, Min(0f)] private float rotationSpeed = 10f;
    [SerializeField] private bool clockwise = true;

    [Header("Riders")]
    [SerializeField] private bool carryRiders = true;
    [SerializeField] private bool rotateRiders = true;
    // Trigger volume that defines "standing on the platform". Auto-found if left empty.
    [SerializeField] private Collider riderZone;

    private readonly Dictionary<Transform, RiderState> riders = new Dictionary<Transform, RiderState>();
    private Rigidbody platformRigidbody;
    private int refreshStamp;

    private class RiderState
    {
        public RiderState(Transform transform, Rigidbody rigidbody, bool transformDriven)
        {
            Transform = transform;
            Rigidbody = rigidbody;
            TransformDriven = transformDriven;
        }

        public Transform Transform { get; }
        public Rigidbody Rigidbody { get; }
        // The player walks by writing transform.position, not through the rigidbody. Carrying
        // one with MovePosition fights that write instead of adding to it — see MoveRiders.
        public bool TransformDriven { get; }
        public int Stamp { get; set; }
        public Vector3 CarryVelocity { get; set; }
    }

    private void Reset()
    {
        rotatingBody = transform;
        riderZone = FindRiderZone();
    }

    private void Awake()
    {
        if (rotatingBody == null)
            rotatingBody = transform;

        if (rotatingBody == transform)
            platformRigidbody = GetComponent<Rigidbody>();

        if (riderZone == null)
            riderZone = FindRiderZone();
    }

    private Collider FindRiderZone()
    {
        foreach (Collider candidate in GetComponentsInChildren<Collider>(true))
        {
            if (candidate.isTrigger)
                return candidate;
        }

        return null;
    }

    private void FixedUpdate()
    {
        if (rotatingBody == null || Mathf.Approximately(rotationSpeed, 0f))
            return;

        float direction = clockwise ? 1f : -1f;
        float angle = rotationSpeed * direction * Time.fixedDeltaTime;
        Quaternion rotationDelta = Quaternion.AngleAxis(angle, Vector3.up);
        Vector3 pivot = rotatingBody.position;

        Quaternion nextRotation = rotationDelta * rotatingBody.rotation;

        if (platformRigidbody != null)
            platformRigidbody.MoveRotation(nextRotation);
        else
            rotatingBody.rotation = nextRotation;

        if (carryRiders)
        {
            RefreshRiders();
            MoveRiders(pivot, rotationDelta);
        }
        else if (riders.Count > 0)
        {
            ReleaseAllRiders();
        }
    }

    public void ReverseDirection()
    {
        clockwise = !clockwise;
    }

    public void SetClockwise(bool value)
    {
        clockwise = value;
    }

    // Rebuilds the rider set from what is actually overlapping the zone right now. Anything
    // that was a rider last step but is not in the overlap has left, so it gets released.
    private void RefreshRiders()
    {
        if (riderZone == null)
            return;

        refreshStamp++;

        Vector3 centre = riderZone.bounds.center;
        Quaternion rotation = riderZone.transform.rotation;
        Vector3 halfExtents = GetZoneHalfExtents();

        int count = OverlapRiderZone(centre, halfExtents, rotation);
        for (int i = 0; i < count; i++)
        {
            Collider other = s_Overlaps[i];
            if (other == null || !IsCarryable(other))
                continue;

            Transform riderTransform = ResolveRiderTransform(other, out Rigidbody riderRigidbody);
            if (riderTransform == null)
                continue;

            if (riderTransform == rotatingBody || riderTransform.IsChildOf(rotatingBody))
                continue;

            if (!riders.TryGetValue(riderTransform, out RiderState state))
            {
                bool transformDriven = riderTransform.GetComponent<PlayerMovement>() != null;
                state = new RiderState(riderTransform, riderRigidbody, transformDriven);
                riders.Add(riderTransform, state);
            }

            state.Stamp = refreshStamp;
        }

        s_StaleRiders.Clear();

        foreach (KeyValuePair<Transform, RiderState> pair in riders)
        {
            RiderState state = pair.Value;
            if (state.Stamp != refreshStamp || state.Transform == null || !state.Transform.gameObject.activeInHierarchy)
                s_StaleRiders.Add(pair.Key);
        }

        for (int i = 0; i < s_StaleRiders.Count; i++)
        {
            if (riders.TryGetValue(s_StaleRiders[i], out RiderState state))
                ReleaseRider(state);

            riders.Remove(s_StaleRiders[i]);
        }
    }

    private Vector3 GetZoneHalfExtents()
    {
        if (riderZone is BoxCollider box)
            return Vector3.Scale(box.size, box.transform.lossyScale) * 0.5f;

        // Non-box zones fall back to their world bounds, which is generous but still bounded.
        return riderZone.bounds.extents;
    }

    private static int OverlapRiderZone(Vector3 centre, Vector3 halfExtents, Quaternion rotation)
    {
        while (true)
        {
            int count = Physics.OverlapBoxNonAlloc(centre, halfExtents, s_Overlaps, rotation, ~0, QueryTriggerInteraction.Ignore);
            if (count < s_Overlaps.Length)
                return count;

            // The buffer filled up, so the result may be truncated — grow it and ask again.
            s_Overlaps = new Collider[s_Overlaps.Length * 2];
        }
    }

    private void MoveRiders(Vector3 pivot, Quaternion rotationDelta)
    {
        if (riders.Count == 0)
            return;

        foreach (KeyValuePair<Transform, RiderState> pair in riders)
        {
            RiderState state = pair.Value;
            Transform riderTransform = state.Transform;

            Rigidbody riderRigidbody = state.Rigidbody;

            // PlayerMovement walks by writing transform.position directly, and the project runs
            // with Auto Sync Transforms off, so rigidbody.position is stale the moment it does.
            // Carrying the player through MovePosition therefore reads a stale origin and then
            // loses the race with that transform write — the platform rotates out from under the
            // rider and they slide off. Move whoever is transform-driven the same way they move
            // themselves, so the two writes add up instead of cancelling.
            bool useTransform = state.TransformDriven || riderRigidbody == null;

            Vector3 currentPosition = useTransform ? riderTransform.position : riderRigidbody.position;
            Vector3 nextPosition = pivot + rotationDelta * (currentPosition - pivot);

            // Remember what this step's carry is worth as a velocity, so stepping off can
            // hand it back instead of leaving the rider with the platform's speed.
            state.CarryVelocity = (nextPosition - currentPosition) / Time.fixedDeltaTime;

            if (useTransform)
            {
                riderTransform.position = nextPosition;

                if (rotateRiders)
                    riderTransform.rotation = rotationDelta * riderTransform.rotation;
            }
            else
            {
                riderRigidbody.MovePosition(nextPosition);

                if (rotateRiders)
                    riderRigidbody.MoveRotation(rotationDelta * riderRigidbody.rotation);
            }
        }
    }

    // MovePosition leaves the carry motion baked into a dynamic rider's velocity. Take back
    // exactly what the last step added, so walking off the platform does not shove the player.
    private void ReleaseRider(RiderState state)
    {
        // A transform-driven rider was never pushed through its rigidbody, so there is no
        // baked carry velocity to hand back — subtracting one would be a shove of its own.
        if (state.TransformDriven)
            return;

        Rigidbody body = state.Rigidbody;
        if (body == null || body.isKinematic)
            return;

        Vector3 carry = state.CarryVelocity;
        carry.y = 0f;
        if (carry.sqrMagnitude <= 0f)
            return;

        Vector3 velocity = body.linearVelocity;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        Vector3 corrected = horizontal - carry;

        // Only ever slow the rider down; never let the correction become a push of its own.
        if (corrected.sqrMagnitude > horizontal.sqrMagnitude)
            return;

        body.linearVelocity = new Vector3(corrected.x, velocity.y, corrected.z);
    }

    private void ReleaseAllRiders()
    {
        foreach (KeyValuePair<Transform, RiderState> pair in riders)
            ReleaseRider(pair.Value);

        riders.Clear();
    }

    private static Collider[] s_Overlaps = new Collider[64];
    private static readonly List<Transform> s_StaleRiders = new List<Transform>();

    private static bool IsCarryable(Collider other)
    {
        return other.CompareTag("Player")
            || other.GetComponentInParent<PlayerMovement>() != null
            || Luggage.TryGetFromCollider(other, out _);
    }

    private static Transform ResolveRiderTransform(Collider other, out Rigidbody riderRigidbody)
    {
        riderRigidbody = other.attachedRigidbody;
        if (riderRigidbody == null)
            riderRigidbody = other.GetComponentInParent<Rigidbody>();

        if (riderRigidbody != null)
            return riderRigidbody.transform;

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player != null)
            return player.transform;

        Luggage luggage = other.GetComponentInParent<Luggage>();
        if (luggage != null)
            return luggage.transform;

        return null;
    }
}
