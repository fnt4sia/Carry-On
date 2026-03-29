using UnityEngine;

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
