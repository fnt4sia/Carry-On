using System.Collections.Generic;
using UnityEngine;

// TEMPORARY: Level1Test station-refill experiment (Assets/_TempStationTest).
//
// The box players take refills from, Overcooked-style. One refill always sits on its lid, held
// still (kinematic) so nobody bumps it off; grabbing it takes it, and the next one appears
// restockDelay later. maxOut stops the floor filling up with refills nobody used.
public class RefillCrate : MonoBehaviour
{
    [SerializeField] private RefillItem refillPrefab;
    [Tooltip("Where the waiting refill sits, on the box's lid.")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField, Min(0f)] private float restockDelay = 0.5f;
    [Tooltip("Refills from this box that may exist at once, the waiting one included. A refill " +
             "used in a machine is gone, so this only caps the ones lying around.")]
    [SerializeField, Min(1)] private int maxOut = 3;

    private readonly List<RefillItem> handedOut = new();
    private RefillItem waiting;
    private float restockAt;

    private void Update()
    {
        // Grabbing makes the body dynamic again (PlayerGrab.TryGrab), so it is off the lid from here.
        if (waiting != null && waiting.Luggage.GetIsGrabbed())
        {
            waiting = null;
            restockAt = Time.time + restockDelay;
        }

        if (waiting != null || Time.time < restockAt || refillPrefab == null || spawnPoint == null)
            return;

        handedOut.RemoveAll(item => item == null);
        if (handedOut.Count >= maxOut)
            return;

        // Not parented to the box: Level1's roots are scaled 0.6 and the refill must keep its size.
        waiting = Instantiate(refillPrefab, spawnPoint.position, spawnPoint.rotation);
        waiting.Luggage.Body.isKinematic = true;
        handedOut.Add(waiting);
    }
}
