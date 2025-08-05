using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerGrab : MonoBehaviour 
{
    [SerializeField] private Transform grabPoint;
    [SerializeField] private Transform grabAnchor;
    [SerializeField] private float grabRadius;
    [SerializeField] private LayerMask grabbableLayer;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private float throwMinHoldTime = 0.25f;
    [SerializeField] private float throwMaxHoldTime = 2f;
    [SerializeField] private float throwMinForce = 8f;
    [SerializeField] private float throwMaxForce = 22f;
    [SerializeField] private float throwMinUpForce = 3f;
    [SerializeField] private float throwMaxUpForce = 8f;

    private bool isGrabInputHeld;
    private float grabInputHoldTime;

    private ConfigurableJoint configurableJoint;
    private Luggage luggageHeld;
    private Rigidbody objectRigidbody;
    private int playerId;

    private Collider[] grabHits;
    private Collider[] outlineGrabHits;
    private Outline lastOutlined = null; 

    private void Start()
    {
        playerId = playerMovement.playerId;
    }

    void Update()
    {
        bool grabDown = false;
        bool grabUp = false;

        if (playerMovement.playerId == 1)
        {
            grabDown = Input.GetKeyDown(KeyCode.Joystick1Button2) || Input.GetKeyDown(KeyCode.E);
            grabUp = Input.GetKeyUp(KeyCode.Joystick1Button2) || Input.GetKeyUp(KeyCode.E);
        }
        else if (playerMovement.playerId == 2)
        {
            grabDown = Input.GetKeyDown(KeyCode.Joystick2Button2) || Input.GetKeyDown(KeyCode.RightControl);
            grabUp = Input.GetKeyUp(KeyCode.Joystick2Button2) || Input.GetKeyUp(KeyCode.RightControl);
        }

        // Button Down: start timer if holding luggage
        if (grabDown && objectRigidbody != null)
        {
            isGrabInputHeld = true;
            grabInputHoldTime = 0f;
        }

        if (grabUp && objectRigidbody != null && isGrabInputHeld)
        {
            if (grabInputHoldTime >= throwMinHoldTime)
            {
                float clampedHoldTime = Mathf.Clamp(grabInputHoldTime, throwMinHoldTime, throwMaxHoldTime);
                float t = (clampedHoldTime - throwMinHoldTime) / (throwMaxHoldTime - throwMinHoldTime); // 0..1

                float forwardForce = Mathf.Lerp(throwMinForce, throwMaxForce, t);
                float upForce = Mathf.Lerp(throwMinUpForce, throwMaxUpForce, t);

                Throw(forwardForce, upForce);
            }
            else
            {
                Drop();
            }
            isGrabInputHeld = false;
        }

        // If not grabbing luggage, old grab/drop logic
        if (grabDown && objectRigidbody == null)
        {
            TryGrab();
        }

        // Update timer if holding
        if (isGrabInputHeld)
        {
            grabInputHoldTime += Time.deltaTime;
        }

        CheckOutline();
    }

    private void CheckOutline()
    {
        if (luggageHeld != null)
        {
            if (lastOutlined != null)
            {
                lastOutlined.enabled = false;
                lastOutlined = null;
            }

            return;
            
        }

        outlineGrabHits = Physics.OverlapSphere(grabPoint.position, grabRadius, grabbableLayer);

        if (outlineGrabHits.Length > 0)
        {
            Outline current = outlineGrabHits[0].GetComponentInParent<Outline>();
            Luggage luggage = outlineGrabHits[0].GetComponentInParent<Luggage>();

            if (luggage.GetIsGrabbed())
            {
                return;
            }

            if (lastOutlined != null && lastOutlined != current)
            {
                lastOutlined.enabled = false;
            }

            current.enabled = true;
            lastOutlined = current;
        }
        else
        {
            if (lastOutlined != null)
            {
                lastOutlined.enabled = false;
                lastOutlined = null;
            }
        }
    }

    private void TryGrab()
    {

        grabHits = Physics.OverlapSphere(grabPoint.position, grabRadius, grabbableLayer);

        if (grabHits.Length > 0)
        {
            objectRigidbody = grabHits[0].attachedRigidbody;
            if (objectRigidbody != null)
            {
                luggageHeld = objectRigidbody.GetComponent<Luggage>();
                luggageHeld.SetPlayerGrabber(this);

                luggageHeld.SetGrabbed(true);

                objectRigidbody.mass = 7.5f;
                playerMovement.isGrabbing = true;
                LightLuggageGrab();
            }
        }
    }

    private void Throw(float forwardForce, float upForce)
    {
        if (configurableJoint != null && objectRigidbody != null)
        {
            objectRigidbody.mass = 125f;
            luggageHeld.SetGrabbed(false);
            luggageHeld = null;

            Destroy(configurableJoint);
            configurableJoint = null;

            // Direction = forward + up
            Vector3 throwDir = grabPoint.forward.normalized * forwardForce + Vector3.up * upForce;
            objectRigidbody.AddForce(throwDir, ForceMode.Impulse);

            objectRigidbody = null;
            playerMovement.isGrabbing = false;
        }
    }

    private void LightLuggageGrab()
    {
        configurableJoint = grabPoint.gameObject.AddComponent<ConfigurableJoint>();
        configurableJoint.connectedBody = objectRigidbody;

        JointDrive drive = new()
        {
            positionSpring = 1750f,
            positionDamper = 75f,
            maximumForce = 5000f,
        };

        configurableJoint.xDrive = drive;
        configurableJoint.yDrive = drive;
        configurableJoint.zDrive = drive;

        configurableJoint.angularXMotion = ConfigurableJointMotion.Limited;
        configurableJoint.angularYMotion = ConfigurableJointMotion.Limited;
        configurableJoint.angularZMotion = ConfigurableJointMotion.Limited;

        configurableJoint.lowAngularXLimit = new SoftJointLimit { limit = -1f };
        configurableJoint.highAngularXLimit = new SoftJointLimit { limit = 15f };
        configurableJoint.angularYLimit = new SoftJointLimit { limit = 15f };
        configurableJoint.angularZLimit = new SoftJointLimit { limit = 15f };    

        Vector3 worldGrabPoint = grabHits[0].ClosestPoint(grabAnchor.position);
        Vector3 localGrabPoint = objectRigidbody.transform.InverseTransformPoint(worldGrabPoint);

        configurableJoint.autoConfigureConnectedAnchor = false;
        configurableJoint.connectedAnchor = localGrabPoint;
        configurableJoint.anchor = grabAnchor.localPosition;

        configurableJoint.breakForce = 2800f;
        configurableJoint.breakTorque = 2800f;

        configurableJoint.massScale = 1f;
        configurableJoint.connectedMassScale = 1.5f;

        configurableJoint.projectionMode = JointProjectionMode.PositionAndRotation;
        configurableJoint.projectionDistance = 0.1f;
        configurableJoint.projectionAngle = 5f;
    }
    public void Drop()
    {
        if (configurableJoint != null)
        {
            objectRigidbody.mass = 125f;
            luggageHeld.SetGrabbed(false);
            luggageHeld = null;
            Destroy(configurableJoint);
            configurableJoint = null;
            objectRigidbody = null;
            playerMovement.isGrabbing = false;
        }
    }

    public int GetPlayerId()
    {
        return playerId;
    }
}
