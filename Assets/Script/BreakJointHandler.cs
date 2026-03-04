using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BreakJointHandler : MonoBehaviour
{

    [SerializeField] private PlayerGrab playerGrab;
    

    void OnJointBreak(float breakForce)
    {
        playerGrab.Drop();
    }

}
