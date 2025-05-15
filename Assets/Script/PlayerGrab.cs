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

    private ConfigurableJoint configurableJoint;
    private Luggage luggageHeld;
    private Rigidbody objectRigidbody;
    private int playerId;

    private Collider[] grabHits;

    private void Start()
    {
        playerId = playerMovement.playerId;
    }

    void Update()
    {

        if(playerMovement.playerId == 1)
        {
            if (Input.GetKeyDown(KeyCode.Joystick1Button2) || Input.GetKeyDown(KeyCode.E))
            {
                if (objectRigidbody == null) TryGrab();
                else Drop();
            }
        } else if (playerMovement.playerId == 2)
        {
            if (Input.GetKeyDown(KeyCode.Joystick2Button2) || Input.GetKeyDown(KeyCode.RightControl) )
            {
                if (objectRigidbody == null) TryGrab();
                else Drop();
            }
        }
     }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(grabPoint.position, grabRadius);

        if (grabPoint != null && objectRigidbody != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(grabPoint.position, objectRigidbody.position);
            Gizmos.DrawSphere(grabPoint.position, 0.05f);
            Gizmos.DrawSphere(objectRigidbody.position, 0.05f);
        }
    }

    void TryGrab()
    {

        grabHits = Physics.OverlapSphere(grabPoint.position, grabRadius, grabbableLayer);

        if (grabHits.Length > 0)
        {
            objectRigidbody = grabHits[0].attachedRigidbody;
            if (objectRigidbody != null)
            {
                luggageHeld = objectRigidbody.GetComponent<Luggage>();
                luggageHeld.SetPlayerGrabber(this);
                objectRigidbody.mass = 7.5f;
                playerMovement.isGrabbing = true;
                LightLuggageGrab();
            }
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

        configurableJoint.breakForce = 1750f;
        configurableJoint.breakTorque = 1750f;

        configurableJoint.massScale = 1f;
        configurableJoint.connectedMassScale = 1f;

        configurableJoint.projectionMode = JointProjectionMode.PositionAndRotation;
        configurableJoint.projectionDistance = 0.1f;
        configurableJoint.projectionAngle = 5f;
    }
    public void Drop()
    {
        if (configurableJoint != null)
        {
            objectRigidbody.mass = 125f;
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
