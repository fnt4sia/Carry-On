using UnityEngine;

// Tiny helper that sits on the grab anchor. When the ConfigurableJoint built by
// PlayerGrab exceeds its break force/torque, Unity fires OnJointBreak here and we
// call PlayerGrab.Drop so the held luggage releases cleanly instead of being stuck.
public class JointBreakHandler : MonoBehaviour
{
    public PlayerGrab playerGrab;

    private void OnJointBreak(float breakForce)
    {
        if (playerGrab != null)
        {
            playerGrab.Drop();
        }
    }
}
