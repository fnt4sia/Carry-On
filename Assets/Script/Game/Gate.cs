using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.UI;

public class Gate : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Luggage"))
        {
            Luggage luggage = other.GetComponentInParent<Luggage>();
            if (luggage == null || luggage.IsDelivered) return;

            luggage.IsDelivered = true;

            int playerIndex = luggage.GetLastGrabber() != null ? luggage.GetLastGrabber().GetPlayerIndex() : -1;

            GameManager.Instance.AddScore(10);

            if (playerIndex >= 0)
                GameManager.Instance.AddPlayerScore(playerIndex, 10);

            luggage.DestroyLuggage();
        }
    }
}
