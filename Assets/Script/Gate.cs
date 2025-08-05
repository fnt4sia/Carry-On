using System.Collections;
using System.Collections.Generic;
using TMPro;
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
        if (gateNumber != 0)
        {
            GameObject gateSignObject = Instantiate(gateSignPrefab, canvasWorld);
            gateSignObject.transform.position = gateSign.position;
            gateSignObject.transform.rotation = gateSign.rotation;
            gateSignObject.transform.localScale = new Vector3(0.025f, 0.025f, 0.025f);
            TextMeshProUGUI textMesh = gateSignObject.GetComponentInChildren<TextMeshProUGUI>();
            textMesh.text = gateNumber.ToString();
        }
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
