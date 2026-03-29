using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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
    [SerializeField] private Animator animator;

    [Header("Arrow")]
    [SerializeField] private GameObject Arrow;
    [SerializeField] private float ArrowMinScale = 1.5f;
    [SerializeField] private float ArrowMaxScale = 3f;
    [SerializeField] private float ArrowMinZPos = 2f;
    [SerializeField] private float ArrowMaxZPos = 4f;
    [SerializeField] private float ArrowHeight = 2.0f;

    private PlayerInput playerInput;
    private InputAction grabAction;

    private bool isGrabInputHeld;
    private float grabInputHoldTime;

    private ConfigurableJoint configurableJoint;
    private Luggage luggageHeld;
    private Rigidbody objectRigidbody;

    private Collider[] grabHits;
    private Collider[] outlineGrabHits;
    private Outline lastOutlined = null;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        grabAction = playerInput.actions["Grab"];
    }

    void Update()
    {
        bool grabDown = grabAction.WasPressedThisFrame();
        bool grabUp = grabAction.WasReleasedThisFrame();

        if (grabDown && objectRigidbody != null)
        {
            isGrabInputHeld = true;
            grabInputHoldTime = 0f;
            if (Arrow) Arrow.SetActive(true);
        }

        if (isGrabInputHeld && objectRigidbody != null)
        {
            float t = Mathf.Clamp01(grabInputHoldTime / throwMaxHoldTime);
            float arrowScale = Mathf.Lerp(ArrowMinScale, ArrowMaxScale, t);
            Arrow.transform.localScale = new Vector3(arrowScale, arrowScale, arrowScale);

            float arrowZPos = Mathf.Lerp(ArrowMinZPos, ArrowMaxZPos, t);
            Arrow.transform.localPosition = new Vector3(0, ArrowHeight, arrowZPos);
        }

        if (grabUp && objectRigidbody != null && isGrabInputHeld)
        {
            if (Arrow) Arrow.SetActive(false);

            if (grabInputHoldTime >= throwMinHoldTime)
            {
                float clampedHoldTime = Mathf.Clamp(grabInputHoldTime, throwMinHoldTime, throwMaxHoldTime);
                float t = (clampedHoldTime - throwMinHoldTime) / (throwMaxHoldTime - throwMinHoldTime);

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

        if (grabDown && objectRigidbody == null)
        {
            TryGrab();
        }

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

            if (luggage.GetGrabberCount() >= Mathf.Max(1, luggage.grabPoints.Length)) return;

            if (lastOutlined != null && lastOutlined != current)
                lastOutlined.enabled = false;

            current.enabled = true;
            lastOutlined = current;
        }
        else if (lastOutlined != null)
        {
            lastOutlined.enabled = false;
            lastOutlined = null;
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
                animator.SetBool("isGrabbing", true);

                luggageHeld = objectRigidbody.GetComponent<Luggage>();
                luggageHeld.AddGrabber(this);

                playerMovement.isGrabbing = true;

                LightLuggageGrab();
            }
        }
    }

    private void Throw(float forwardForce, float upForce)
    {
        if (configurableJoint != null && objectRigidbody != null)
        {
            if (luggageHeld != null)
            {
                luggageHeld.RemoveGrabber(this);
                luggageHeld = null;
            }

            Destroy(configurableJoint);
            configurableJoint = null;

            Vector3 throwDir = grabPoint.forward.normalized * forwardForce + Vector3.up * upForce;
            objectRigidbody.AddForce(throwDir, ForceMode.Impulse);

            objectRigidbody = null;
            playerMovement.isGrabbing = false;
            animator.SetBool("isGrabbing", false);
        }
    }

    private void LightLuggageGrab()
    {
        Transform closestPoint = luggageHeld.GetClosestGrabPoint(grabAnchor.position);
        Vector3 localGrabPoint = objectRigidbody.transform.InverseTransformPoint(closestPoint.position);

        configurableJoint = grabPoint.gameObject.AddComponent<ConfigurableJoint>();

        JointBreakHandler breakHandler = grabPoint.gameObject.GetComponent<JointBreakHandler>();
        if (breakHandler == null)
        {
            breakHandler = grabPoint.gameObject.AddComponent<JointBreakHandler>();
        }
        breakHandler.playerGrab = this;

        configurableJoint.connectedBody = objectRigidbody;

        configurableJoint.xMotion = ConfigurableJointMotion.Limited;
        configurableJoint.yMotion = ConfigurableJointMotion.Limited;
        configurableJoint.zMotion = ConfigurableJointMotion.Limited;
        SoftJointLimit linearLimit = new SoftJointLimit { limit = 0.75f };
        configurableJoint.linearLimit = linearLimit;

        configurableJoint.autoConfigureConnectedAnchor = false;
        configurableJoint.connectedAnchor = localGrabPoint; 
        
        configurableJoint.anchor = grabAnchor.localPosition;

        JointDrive drive = new JointDrive
        {
            positionSpring = 7500f, 
            positionDamper = 250f,  
            maximumForce = 10000f    
        };

        configurableJoint.xDrive = drive;
        configurableJoint.yDrive = drive;
        configurableJoint.zDrive = drive;

        JointDrive angularDrive = new JointDrive
        {
            positionSpring = 1000f,       
            positionDamper = 100f,     
            maximumForce = 2000f
        };

        configurableJoint.angularXDrive = angularDrive;
        configurableJoint.angularYZDrive = angularDrive;

        configurableJoint.targetRotation = grabAnchor.localRotation;

        configurableJoint.angularXMotion = ConfigurableJointMotion.Free;
        configurableJoint.angularYMotion = ConfigurableJointMotion.Free;
        configurableJoint.angularZMotion = ConfigurableJointMotion.Free;

        configurableJoint.massScale = 1f;
        configurableJoint.connectedMassScale = 1.5f;

        configurableJoint.projectionMode = JointProjectionMode.PositionAndRotation;
        configurableJoint.projectionDistance = 0.05f; 
        configurableJoint.projectionAngle = 5f; 

        StartCoroutine(DelayBreakForce());
    }

    private IEnumerator DelayBreakForce()
    {
        configurableJoint.breakForce = Mathf.Infinity; 
        configurableJoint.breakTorque = Mathf.Infinity;
        yield return new WaitForSeconds(0.5f);

        if (configurableJoint != null)
        {
            configurableJoint.breakForce = 6500f; 
            configurableJoint.breakTorque = 3500f;
        }
    }

    public void Drop()
    {
        if (Arrow) Arrow.SetActive(false);

        grabInputHoldTime = 0f;
        isGrabInputHeld = false;

        if (configurableJoint != null)
        {
            if (luggageHeld != null)
            {
                luggageHeld.RemoveGrabber(this);
                luggageHeld = null;
            }

            Destroy(configurableJoint);
            configurableJoint = null;
            objectRigidbody = null;
            playerMovement.isGrabbing = false;

            animator.SetBool("isGrabbing", false);
        }
    }

    public int GetPlayerIndex()
    {
        return GetComponent<PlayerInput>().playerIndex;
    }

    public Luggage GetHeldLuggage()
    {
        return luggageHeld;
    }
}
