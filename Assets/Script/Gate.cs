using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.UI;

public class Gate : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private int gateNumber;
    [SerializeField] private Transform gateSign;
    [SerializeField] private Transform canvasWorld;
    [SerializeField] private GameObject gateSignPrefab;

    private void Start()
    {
        GameObject gateSignObject = Instantiate(gateSignPrefab, canvasWorld);
        gateSignObject.transform.position = gateSign.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Luggage"))
        {
            Luggage luggage = other.GetComponentInParent<Luggage>();
            Debug.Log(luggage.GetPlayerGrabber().GetPlayerId());
            if (luggage.gateNumber == gateNumber)
            {
                gameManager.AddScore(10);
                if (luggage.GetPlayerGrabber().GetPlayerId() == 1) gameManager.AddPlayer1Score(10);
                else gameManager.AddPlayer2Score(10);
            }
            else
            {
                gameManager.AddScore(-10);
                if (luggage.GetPlayerGrabber().GetPlayerId() == 1) gameManager.AddPlayer1Score(-10);
                else gameManager.AddPlayer2Score(-10);
            }

            luggage.DestroyLuggage();
        }
    }
}
