using System.Collections.Generic;
using UnityEngine;

// One room of the Tutorial scene. A room owns the luggage authored inside it and the doors
// out of it: the luggage stays switched off until a player walks in, and the doors open once
// every bag in the room is gone — delivered, expired, or otherwise removed. The final room
// ends the round instead of opening anything.
//
// Rooms are chained by their doors, not by each other: room 1 opens the doors to room 2, so
// the order of the tutorial is scene wiring and this component never needs to know where it
// sits in the sequence.
//
// Tutorial has no LuggageSpawner and no LuggageSink, so every bag is authored in the scene and
// nothing recycles one that leaves the floor. A bag knocked into the void would be gone for
// good and the room could never complete, so anything that falls past resetBelowY is put back
// where it started rather than counted as finished.
[RequireComponent(typeof(BoxCollider))]
public class TutorialRoom : MonoBehaviour
{
    [Header("Contents")]
    [Tooltip("Parent holding this room's luggage. Leave the object switched off in the scene — " +
             "it is enabled when a player arrives. Set each bag's Is Tutorial Luggage on the bag " +
             "itself: on for a room with no timer, off for a room that counts down.")]
    [SerializeField] private Transform luggageRoot;

    [Header("On Completion")]
    [Tooltip("Doors opened once every bag in this room is gone. Wire both ends of a corridor " +
             "so players are never sealed inside it.")]
    [SerializeField] private List<Gateway> doorsToOpen = new();
    [Tooltip("Last room: end the round instead of opening a door.")]
    [SerializeField] private bool endsRound;

    [Header("Detection")]
    [Tooltip("Layers a player body is on. The room's BoxCollider is the volume that has to be entered.")]
    [SerializeField] private LayerMask playerLayers = 1 << 7;
    [Tooltip("A bag that falls below this world Y is returned to where it started, because " +
             "nothing else in this scene recovers it.")]
    [SerializeField] private float resetBelowY = -20f;

    private readonly List<Luggage> bags = new();
    private readonly List<Vector3> bagStartPositions = new();
    private readonly List<Quaternion> bagStartRotations = new();

    private BoxCollider volume;
    private bool isActivated;
    private bool isComplete;

    public bool IsActivated => isActivated;
    public bool IsComplete => isComplete;

    private void Awake()
    {
        volume = GetComponent<BoxCollider>();
        volume.isTrigger = true;
    }

    private void Update()
    {
        // Nothing counts during the 3-2-1 countdown or after the timer has run out.
        GameManager game = GameManager.Instance;
        if (game == null || !game.IsRoundStarted || game.IsRoundEnded) return;
        if (isComplete) return;

        if (!isActivated)
        {
            if (ContainsPlayer()) Activate();
            return;
        }

        RecoverFallenBags();
        if (CountRemainingBags() == 0) Complete();
    }

    // Polled rather than driven by OnTriggerEnter: the first room already contains the players
    // when the round starts, and an enter callback for a body that was never outside the volume
    // is not something to rely on.
    private bool ContainsPlayer()
    {
        Vector3 center = transform.TransformPoint(volume.center);
        Vector3 halfExtents = Vector3.Scale(volume.size, transform.lossyScale) * 0.5f;

        Collider[] hits = Physics.OverlapBox(
            center, halfExtents, transform.rotation, playerLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
            if (hits[i].GetComponentInParent<PlayerMovement>() != null)
                return true;

        return false;
    }

    private void Activate()
    {
        isActivated = true;

        if (luggageRoot == null)
        {
            // Completing is the safe failure: an unwired room must not seal the level shut.
            Debug.LogError($"{nameof(TutorialRoom)} '{name}' has no luggage root assigned.", this);
            Complete();
            return;
        }

        luggageRoot.gameObject.SetActive(true);
        luggageRoot.GetComponentsInChildren(true, bags);

        for (int i = 0; i < bags.Count; i++)
        {
            bags[i].gameObject.SetActive(true);
            bagStartPositions.Add(bags[i].transform.position);
            bagStartRotations.Add(bags[i].transform.rotation);
        }

        if (bags.Count == 0)
        {
            Debug.LogWarning(
                $"{nameof(TutorialRoom)} '{name}' has no {nameof(Luggage)} under '{luggageRoot.name}'.", this);
            Complete();
        }
    }

    private int CountRemainingBags()
    {
        int remaining = 0;
        for (int i = 0; i < bags.Count; i++)
            if (!IsBagFinished(bags[i]))
                remaining++;

        return remaining;
    }

    // A delivered bag is destroyed by the gate and an expired one destroys itself, so "gone"
    // covers both. Expiry counting as finished is deliberate — the player already took the
    // penalty, and leaving the bag on the tally would deadlock the room.
    private static bool IsBagFinished(Luggage bag)
    {
        return bag == null || !bag.gameObject.activeInHierarchy || bag.IsDelivered;
    }

    private void RecoverFallenBags()
    {
        for (int i = 0; i < bags.Count; i++)
        {
            Luggage bag = bags[i];
            if (IsBagFinished(bag)) continue;
            if (bag.transform.position.y > resetBelowY) continue;

            bag.DropAllGrabbers();

            Rigidbody body = bag.Body;
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            bag.transform.SetPositionAndRotation(bagStartPositions[i], bagStartRotations[i]);
        }
    }

    private void Complete()
    {
        if (isComplete) return;
        isComplete = true;

        for (int i = 0; i < doorsToOpen.Count; i++)
            if (doorsToOpen[i] != null)
                doorsToOpen[i].Open();

        if (endsRound)
            GameManager.Instance?.EndRoundEarly();
    }
}
