using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.UI;

public class Gate : MonoBehaviour
{
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
            if (luggage == null || luggage.IsDelivered) return;

            luggage.IsDelivered = true;

            int playerIndex = luggage.GetPlayerGrabber().GetPlayerIndex();

            if (luggage.gateNumber == gateNumber)
            {
                GameManager.Instance.AddScore(10);

                if (playerIndex == 0) GameManager.Instance.AddPlayer1Score(10);
                else GameManager.Instance.AddPlayer2Score(10);
            }
            else
            {
                GameManager.Instance.AddScore(-10);

                if (playerIndex == 0) GameManager.Instance.AddPlayer1Score(-10);
                else GameManager.Instance.AddPlayer2Score(-10);
            }

            luggage.DestroyLuggage();
        }
    }
}
