using System.Collections.Generic;
using UnityEngine;

// A trigger at the end of a belt that drops into a pen. Every bag coming off the belt is thrown out
// into the pen, off to one side, and tipped onto its face, so the pen fills as a heap of flat bags
// instead of a queue of standing ones.
//
// Without it the pen fills as a train, not a pile: a belt carries its bags standing on end, so they
// slide off still standing, in one line, and stop just past the belt's end. The next bag lands
// against the last, the row corks the belt's exit, and the belt backs up while the pen is still
// mostly empty.
//
// Timing is everything. The belt keeps steering a bag — pulling it upright — until a ray down from
// the bag's centre misses the deck, and that undoes any push given before. But the drop is short,
// so a push given late leaves too little fall to tip the bag over, and it lands still standing. So
// the bag is thrown the moment its centre crosses this zone's near face, placed where the belt lets
// go of it.
//
// Each bag gets one throw per trip off the belt, and the zone is only a strip just past the belt's
// end. A bag that comes to rest standing — on the heap, or against it at the belt's end — is left
// alone: throwing such bags again kept them hopping on the heap, which read as a bug.
//
// The zone's local Z runs along the belt and local X across it, so keep it square to the belt.
// Each push is picked between half and all of its maximum.
[RequireComponent(typeof(BoxCollider))]
public class LuggageDropScatter : MonoBehaviour
{
    // Tilt from upright beyond which a bag counts as down. It rides the belt at 0 and lies flat at 90.
    private const float StandingTilt = 30f;

    [Tooltip("Largest extra forward speed, so bags land out in the pen instead of at the belt's end.")]
    [SerializeField, Min(0f)] private float throwSpeed = 4f;
    [Tooltip("Largest sideways speed. Keep it high enough that every bag clears the belt's line.")]
    [SerializeField, Min(0f)] private float sideSpeed = 6f;
    [Tooltip("Largest forward spin (rad/s) that tips the bag onto its face. A bag rides the belt " +
             "standing on its narrow end, so it only has to lean ~17 degrees forward to fall flat — " +
             "sideways it would need ~28.")]
    [SerializeField, Min(0f)] private float tumble = 5f;
    [Tooltip("Largest random turn (rad/s) about the vertical, so flat bags don't all land lined up.")]
    [SerializeField, Min(0f)] private float spin = 2f;

    // Bags already thrown on their current trip off the belt.
    private readonly HashSet<Rigidbody> thrown = new();
    private BoxCollider zone;

    private void Awake()
    {
        zone = GetComponent<BoxCollider>();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!Luggage.TryGetFromCollider(other, out Luggage luggage)) return;

        // Never one a player is carrying through.
        Rigidbody body = luggage.Body;
        if (body == null || body.isKinematic || luggage.GetIsGrabbed()) return;

        // Its front reaches the zone first; wait for its centre, which is when the belt lets go. Until
        // then it is still riding in, so it is due a throw even if it had one before: a player may
        // have put it back on the belt.
        float nearFace = zone.center.z - zone.size.z * 0.5f;
        if (transform.InverseTransformPoint(body.worldCenterOfMass).z < nearFace)
        {
            thrown.Remove(body);
            return;
        }

        // A bag lying down is already the pile. Only a standing one trains up.
        if (Vector3.Angle(body.transform.up, Vector3.up) > StandingTilt) return;

        if (!thrown.Add(body)) return;

        // Spinning about the cross-belt axis tips the top forward, onto the bag's face.
        float side = Random.value < 0.5f ? -1f : 1f;
        body.linearVelocity += transform.forward * (Random.Range(0.5f, 1f) * throwSpeed)
                             + transform.right * (side * Random.Range(0.5f, 1f) * sideSpeed);
        body.angularVelocity += transform.right * (Random.Range(0.5f, 1f) * tumble)
                              + Vector3.up * Random.Range(-spin, spin);
    }
}
