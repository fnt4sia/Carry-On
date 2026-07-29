using System.Collections.Generic;
using UnityEngine;

// Belt that steers luggage by velocity. Luggage stays a normal dynamic rigidbody the
// whole time — the belt just steers its horizontal velocity toward the belt direction
// each physics step. No kinematic flips, no ownership handoff: collisions, stacking,
// grabbing and station placement all keep working naturally, and belt-to-belt transfer
// is just "whichever belt you're resting on steers you".
//
// Straight pieces move along their own transform.forward, so rotating the prefab
// instance is all the setup needed. Turn pieces steer along the arc tangent around the
// TurnPivot child, so luggage follows the curve of the model instead of cutting the
// corner on a diagonal.
public class Conveyor : MonoBehaviour
{
    private enum BeltShape { Straight, Turn }

    [Header("Tuning")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [Tooltip("How fast luggage velocity bends toward the belt direction (m/s²).")]
    [SerializeField, Min(0f)] private float acceleration = 18f;
    [Tooltip("How fast cargo is rotated upright and aligned with the belt direction (1/s).")]
    [SerializeField, Min(0f)] private float uprightGain = 4f;
    [Tooltip("How strongly turn cargo is pulled back to the centreline (1/s).")]
    [SerializeField, Min(0f)] private float centeringGain = 1.5f;
    [SerializeField, Min(0f)] private float centerRadius = 3f;
    [SerializeField, Min(0f)] private float surfaceCheckMargin = 0.25f;

    [Header("Belt")]
    [SerializeField] private BeltShape shape = BeltShape.Straight;

    [Header("Turn")]
    [Tooltip("Centre of the turn arc. Required for Turn shape — place at the corner's pivot.")]
    [SerializeField] private Transform turnPivot;
    [SerializeField] private bool clockwise = false;
    private readonly List<CandidateState> candidates = new();
    private readonly Dictionary<Luggage, CandidateState> candidateByLuggage = new();

    private class CandidateState
    {
        public Luggage Luggage;
        public Rigidbody Rigidbody;
        public Collider SurfaceCollider;
        public readonly HashSet<Collider> TriggerContacts = new();
    }

    private void FixedUpdate()
    {
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            CandidateState candidate = candidates[i];
            Luggage luggage = candidate.Luggage;
            Rigidbody rb = candidate.Rigidbody;

            if (luggage == null || rb == null || !luggage.gameObject.activeInHierarchy)
            {
                if (!ReferenceEquals(luggage, null))
                    candidateByLuggage.Remove(luggage);
                candidates.RemoveAt(i);
                continue;
            }

            // Player carrying or station processing — leave it alone, but keep tracking
            // so the belt picks it up again if it is dropped back on.
            if (luggage.GetIsGrabbed() || luggage.IsInStation) continue;

            // Stations dock luggage kinematically — don't fight them.
            if (rb.isKinematic) continue;

            if (!IsOnSurface(candidate)) continue;

            Steer(rb);
        }
    }

    private void Steer(Rigidbody rb)
    {
        Vector3 beltDir = BeltDirectionAt(rb.position);
        Vector3 target = beltDir * moveSpeed;

        if (shape == BeltShape.Turn && turnPivot != null)
        {
            // Pull cargo onto the belt centreline so long boxes don't spiral out
            // and clip their corners on the rails.
            Vector3 radial = rb.position - turnPivot.position;
            radial.y = 0f;
            float r = radial.magnitude;
            if (r > 0.1f)
            {
                float centerError = centerRadius - r;
                target += (radial / r) * Mathf.Clamp(centerError * centeringGain, -0.8f, 0.8f);
            }
        }
        else
        {
            // Keep cargo on the straight belt centreline so small collision impulses
            // cannot leave it offset enough to catch a rail at the next turn.
            Vector3 right = transform.right;
            right.y = 0f;
            right.Normalize();
            float lateralOffset = Vector3.Dot(rb.position - transform.position, right);
            target -= right * Mathf.Clamp(lateralOffset * centeringGain, -0.8f, 0.8f);
        }

        Vector3 v = rb.linearVelocity;
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        flat = Vector3.MoveTowards(flat, target, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(flat.x, v.y, flat.z);

        // The deck is frictionless (steering replaces friction), so the belt owns
        // rotation too: PD-rotate cargo upright and facing the belt direction. This
        // rights tipped luggage, stops tumbling, and on turns the rotating tangent
        // yaws the cargo through the arc automatically.
        if (beltDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(beltDir, Vector3.up);
            Quaternion delta = targetRot * Quaternion.Inverse(rb.rotation);
            delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
            if (angleDeg > 180f) angleDeg -= 360f;

            if (Mathf.Abs(angleDeg) > 0.5f && !float.IsNaN(axis.x))
            {
                Vector3 angVel = axis.normalized * (angleDeg * Mathf.Deg2Rad * uprightGain);
                rb.angularVelocity = Vector3.ClampMagnitude(angVel, 6f);
            }
            else
            {
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    private Vector3 BeltDirectionAt(Vector3 position)
    {
        if (shape == BeltShape.Straight || turnPivot == null)
        {
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            return fwd.normalized;
        }

        // Tangent of the arc around the pivot: perpendicular to the radial direction.
        Vector3 radial = position - turnPivot.position;
        radial.y = 0f;
        if (radial.sqrMagnitude < 0.0001f) return Vector3.zero;

        Vector3 tangent = clockwise
            ? Vector3.Cross(Vector3.up, radial)
            : Vector3.Cross(radial, Vector3.up);
        return tangent.normalized;
    }

    // True when a downward ray from the luggage's centre lands on this conveyor.
    private bool IsOnSurface(CandidateState candidate)
    {
        Collider luggageCollider = candidate.SurfaceCollider;
        Vector3 origin = luggageCollider != null ? luggageCollider.bounds.center : candidate.Rigidbody.position;
        float reach = (luggageCollider != null ? luggageCollider.bounds.extents.y : 0.5f) + surfaceCheckMargin;

        return Physics.Raycast(origin, Vector3.down, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.transform.IsChildOf(transform);
    }

    private void OnTriggerEnter(Collider other) => Track(other);
    private void OnTriggerStay(Collider other) => Track(other);

    private void Track(Collider other)
    {
        if (!Luggage.TryGetFromCollider(other, out Luggage luggage))
            return;

        if (!candidateByLuggage.TryGetValue(luggage, out CandidateState candidate))
        {
            Rigidbody rb = luggage.Body;
            if (rb == null)
                return;

            candidate = new CandidateState
            {
                Luggage = luggage,
                Rigidbody = rb,
                SurfaceCollider = luggage.SurfaceCollider
            };
            candidates.Add(candidate);
            candidateByLuggage.Add(luggage, candidate);
        }

        candidate.TriggerContacts.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!Luggage.TryGetFromCollider(other, out Luggage luggage)
            || !candidateByLuggage.TryGetValue(luggage, out CandidateState candidate))
            return;

        candidate.TriggerContacts.Remove(other);
        if (candidate.TriggerContacts.Count > 0)
            return;

        candidateByLuggage.Remove(luggage);
        candidates.Remove(candidate);
    }

    private void OnDisable()
    {
        candidates.Clear();
        candidateByLuggage.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        if (shape == BeltShape.Turn && turnPivot != null)
        {
            // Arc direction samples around the pivot
            Vector3 toSelf = transform.position - turnPivot.position; toSelf.y = 0f;
            float radius = Mathf.Max(toSelf.magnitude, 0.5f);
            for (int i = 0; i < 12; i++)
            {
                Vector3 p = turnPivot.position + Quaternion.Euler(0, i * 30f, 0) * (Vector3.forward * radius);
                p.y = transform.position.y + 0.6f;
                Gizmos.DrawRay(p, BeltDirectionAt(p) * 0.5f);
            }
        }
        else
        {
            Vector3 c = transform.position + Vector3.up * 0.6f;
            Gizmos.DrawRay(c, BeltDirectionAt(c) * 1.5f);
        }
    }
}
