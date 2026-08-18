using UnityEngine;

// Keeps the hinged curtain strips simulating.
//
// A strip that is held wide open by a passing bag stops moving, and PhysX puts its
// rigidbody to sleep. A HingeJoint spring cannot wake a sleeping body — only a new
// collision can — so once the bag is gone the strip stays frozen at whatever angle it
// was pinned at, leaving a visible hole in the doorway. The same thing happens on a
// slow swing back: the return dips under the sleep threshold and freezes part-way.
//
// Strips are 0.5 kg boxes, so never sleeping costs nothing.
public class BaggageDoorCurtain : MonoBehaviour
{
    private Rigidbody[] strips;

    private void Awake()
    {
        strips = GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody strip in strips)
            strip.sleepThreshold = 0f;
    }

    private void OnEnable()
    {
        // Re-enabling a curtain (or reusing a pooled door) restores the sleeping state
        // the strips had when they were switched off, so wake them again here.
        foreach (Rigidbody strip in strips)
            strip.WakeUp();
    }
}
