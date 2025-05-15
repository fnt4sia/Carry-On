using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Luggage : MonoBehaviour
{

    private PlayerGrab playerGrabber;
    public int gateNumber;

    public void DestroyLuggage()
    {
        if (playerGrabber != null)
        {
            playerGrabber.Drop(); 
        }

        Destroy(gameObject);
    }

    public void SetPlayerGrabber(PlayerGrab playerGrab)
    {
        playerGrabber = playerGrab;
    }

    public PlayerGrab GetPlayerGrabber()
    {
        return playerGrabber;
    }
}
