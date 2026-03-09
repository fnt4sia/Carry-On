using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Luggage : MonoBehaviour
{

    [SerializeField] private GameObject luggageSignPrefab;
    [SerializeField] private Outline outline;

    private PlayerGrab playerGrabber;
    private Transform canvasWorld;
    private bool isGrabbed;
    private GameObject luggageSign;
    private TextMeshProUGUI luggageText;
    private Transform cameraTransform;
    public int gateNumber;

    public bool IsDelivered { get; set; }

    private void Start()
    {
        outline.enabled = false;
        if (gateNumber != 0)
        {
            canvasWorld = GameObject.Find("WorldCanvas").transform;
            luggageSign = Instantiate(luggageSignPrefab, canvasWorld);
            luggageSign.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            luggageSign.transform.position = transform.position + new Vector3(0, 1.5f, 0);
            luggageText = luggageSign.GetComponentInChildren<TextMeshProUGUI>();
            luggageText.text = gateNumber.ToString();
            cameraTransform = GameObject.Find("Main Camera").transform;
            luggageSign.SetActive(false);
        }        
    }

    private void Update()
    {
        if (gateNumber != 0)
        {
            if (isGrabbed)
            {
                luggageSign.transform.position = transform.position + new Vector3(0, 1.5f, 0);
                luggageSign.transform.LookAt(cameraTransform);
                luggageSign.transform.Rotate(0, 180f, 0);
            }
        }
    }

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
    public void SetGrabbed(bool grabbed)
    {
        isGrabbed = grabbed;
        if (gateNumber != 0)
        {
            if (grabbed) luggageSign.SetActive(true);
            else luggageSign.SetActive(false);
        }
    }

    public bool GetIsGrabbed()
    {
        return isGrabbed;
    }
}
