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

        // START HOLD
        if (grabDown && objectRigidbody != null)
        {
            isGrabInputHeld = true;
            grabInputHoldTime = 0f;
            if (Arrow) Arrow.SetActive(true);
        }

        // UPDATE HOLD VISUAL
        if (isGrabInputHeld && objectRigidbody != null)
        {
            float t = Mathf.Clamp01(grabInputHoldTime / throwMaxHoldTime);
            float arrowScale = Mathf.Lerp(ArrowMinScale, ArrowMaxScale, t);
            Arrow.transform.localScale = new Vector3(arrowScale, arrowScale, arrowScale);

            float arrowZPos = Mathf.Lerp(ArrowMinZPos, ArrowMaxZPos, t);
            Arrow.transform.localPosition = new Vector3(0, ArrowHeight, arrowZPos);
        }

        // RELEASE
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

        // TRY GRAB
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

            if (luggage.GetIsGrabbed()) return;

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

            Vector3 throwDir = grabPoint.forward.normalized * forwardForce + Vector3.up * upForce;
            objectRigidbody.AddForce(throwDir, ForceMode.Impulse);

            objectRigidbody = null;
            playerMovement.isGrabbing = false;
            animator.SetBool("isGrabbing", false);
        }
    }

    private void LightLuggageGrab()
    {
        configurableJoint = grabPoint.gameObject.AddComponent<ConfigurableJoint>();
        configurableJoint.connectedBody = objectRigidbody;

        configurableJoint.autoConfigureConnectedAnchor = false;

        Vector3 worldGrabPoint = grabHits[0].ClosestPoint(grabAnchor.position);
        Vector3 localGrabPoint = objectRigidbody.transform.InverseTransformPoint(worldGrabPoint);

        configurableJoint.connectedAnchor = localGrabPoint;
        configurableJoint.anchor = grabAnchor.localPosition;

        JointDrive drive = new JointDrive
        {
            positionSpring = 1800f,
            positionDamper = 220f,
            maximumForce = 3200f
        };

        configurableJoint.xDrive = drive;
        configurableJoint.yDrive = drive;
        configurableJoint.zDrive = drive;

        configurableJoint.targetPosition = Vector3.zero;
        configurableJoint.angularXMotion = ConfigurableJointMotion.Free;
        configurableJoint.angularYMotion = ConfigurableJointMotion.Free;
        configurableJoint.angularZMotion = ConfigurableJointMotion.Free;

        configurableJoint.massScale = 1f;
        configurableJoint.connectedMassScale = 1.2f;

        configurableJoint.projectionMode = JointProjectionMode.PositionAndRotation;
        configurableJoint.projectionDistance = 0.08f;
        configurableJoint.projectionAngle = 4f;

        StartCoroutine(DelayBreakForce());
    }

    private IEnumerator DelayBreakForce()
    {
        configurableJoint.breakForce = 8000f;
        yield return new WaitForSeconds(0.5f);

        if (configurableJoint != null)
            configurableJoint.breakForce = 2500f;
    }

    public void Drop()
    {
        if (Arrow) Arrow.SetActive(false);

        grabInputHoldTime = 0f;
        isGrabInputHeld = false;

        if (configurableJoint != null)
        {
            objectRigidbody.mass = 125f;
            luggageHeld.SetGrabbed(false);
            luggageHeld = null;

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
}
