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
    [SerializeField] private LayerMask grabBlockLayer;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private float throwMinHoldTime = 0.25f;
    [SerializeField] private float throwMaxHoldTime = 2f;
    [SerializeField] private float throwMinForce = 8f;
    [SerializeField] private float throwMaxForce = 22f;
    [SerializeField] private float throwMinUpForce = 3f;
    [SerializeField] private float throwMaxUpForce = 8f;
    [SerializeField] private Animator animator;
    [SerializeField] private float grabAlignDuration = 2f;

    [Header("Arrow")]
    [SerializeField] private GameObject Arrow;
    [SerializeField] private float ArrowMinScale = 1.5f;
    [SerializeField] private float ArrowMaxScale = 3f;
    [SerializeField] private float ArrowMinZPos = 2f;
    [SerializeField] private float ArrowMaxZPos = 4f;
    [SerializeField] private float ArrowHeight = 2.0f;

    [Header("Bridge Collider")]
    [SerializeField] private float bridgeWidth = 0.5f;
    [SerializeField] private float bridgeHeight = 0.5f;
    [SerializeField] private float bridgeYOffset = 0f;
    [SerializeField] private float bridgeExtraZ = 0.5f;

    [SerializeField] private int playerIndex;

    private PlayerInput playerInput;
    private InputAction grabAction;
    private InputAction useStationAction;
    private DesignSceneInput designInput;

    private bool isGrabInputHeld;
    private float grabInputHoldTime;

    private ConfigurableJoint configurableJoint;
    private Luggage luggageHeld;
    private Rigidbody objectRigidbody;

    private GameObject bridgeObject;
    private BoxCollider bridgeBoxCollider;
    private Collider[] heldLuggageColliders;

    private Collider[] grabHits;
    private Collider[] outlineGrabHits;
    private Outline lastOutlined = null;

    private Collider[] playerColliders;
    private Vector3 originalCenterOfMass;
    private bool hasShiftedCoM;

    private void OnEnable()  => UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene _, UnityEngine.SceneManagement.LoadSceneMode __)
        => designInput = FindFirstObjectByType<DesignSceneInput>();

    private void Start()
    {
        designInput = FindFirstObjectByType<DesignSceneInput>();
    }

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        grabAction = playerInput.actions["Grab"];
        useStationAction = playerInput.actions["UseStation"];

        playerColliders = GetComponentsInChildren<Collider>();
        bridgeObject = new GameObject("BridgeCollider");
        bridgeObject.transform.SetParent(transform);
        bridgeObject.layer = gameObject.layer;
        bridgeBoxCollider = bridgeObject.AddComponent<BoxCollider>();
        bridgeObject.SetActive(false);
    }

    void Update()
    {
        if (designInput == null)
        {
            bool grabDown = grabAction.WasPressedThisFrame();
            bool grabUp   = grabAction.WasReleasedThisFrame();
            ProcessGrabInput(grabDown, grabUp);

            if (useStationAction.WasPressedThisFrame())
                TryUseStation();
        }

        // Arrow charge visual (runs in all scenes)
        if (isGrabInputHeld && objectRigidbody != null)
        {
            float t = Mathf.Clamp01(grabInputHoldTime / throwMaxHoldTime);
            float arrowScale = Mathf.Lerp(ArrowMinScale, ArrowMaxScale, t);
            Arrow.transform.localScale = new Vector3(arrowScale, arrowScale, arrowScale);

            float arrowZPos = Mathf.Lerp(ArrowMinZPos, ArrowMaxZPos, t);
            Arrow.transform.localPosition = new Vector3(0, ArrowHeight, arrowZPos);
        }

        if (isGrabInputHeld)
            grabInputHoldTime += Time.deltaTime;

        CheckOutline();

        if (luggageHeld != null)
            UpdateBridgeCollider();
    }

    private void ProcessGrabInput(bool grabDown, bool grabUp)
    {
        if (grabDown && objectRigidbody != null)
        {
            if (luggageHeld == null || luggageHeld.behaviorType != LuggageBehaviorType.Sticky)
            {
                isGrabInputHeld = true;
                grabInputHoldTime = 0f;
                if (Arrow) Arrow.SetActive(true);
            }
        }

        if (grabUp && objectRigidbody != null && isGrabInputHeld)
        {
            if (Arrow) Arrow.SetActive(false);

            if (grabInputHoldTime >= throwMinHoldTime)
            {
                float clampedHoldTime = Mathf.Clamp(grabInputHoldTime, throwMinHoldTime, throwMaxHoldTime);
                float t = (clampedHoldTime - throwMinHoldTime) / (throwMaxHoldTime - throwMinHoldTime);
                Throw(Mathf.Lerp(throwMinForce, throwMaxForce, t), Mathf.Lerp(throwMinUpForce, throwMaxUpForce, t));
            }
            else
            {
                Drop();
            }

            isGrabInputHeld = false;
        }

        if (grabDown && objectRigidbody == null)
            TryGrab();
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

        foreach (var hit in outlineGrabHits)
        {
            Outline current = hit.GetComponentInParent<Outline>();
            Luggage luggage = hit.GetComponentInParent<Luggage>();

            if (luggage != null)
            {
                if (lastOutlined != null && lastOutlined != current)
                    lastOutlined.enabled = false;

                if (current != null) current.enabled = true;
                lastOutlined = current;
                return;
            }
        }

        if (lastOutlined != null)
        {
            lastOutlined.enabled = false;
            lastOutlined = null;
        }
    }

    private void TryGrab()
    {
        grabHits = Physics.OverlapSphere(grabPoint.position, grabRadius, grabbableLayer);

        foreach (var hit in grabHits)
        {
            objectRigidbody = hit.attachedRigidbody;
            if (objectRigidbody != null)
            {
                Vector3 toTarget = objectRigidbody.worldCenterOfMass - grabPoint.position;
                if (Physics.Raycast(grabPoint.position, toTarget.normalized, toTarget.magnitude, grabBlockLayer))
                {
                    objectRigidbody = null;
                    continue;
                }

                luggageHeld = objectRigidbody.GetComponent<Luggage>();

                if (luggageHeld != null && luggageHeld.IsInStation)
                {
                    luggageHeld = null;
                    objectRigidbody = null;
                    continue;
                }

                if (luggageHeld != null)
                {
                    // Force-drop all other grabbers (steal the luggage)
                    luggageHeld.DropAllGrabbers();

                    objectRigidbody.isKinematic = false;

                    animator.SetBool("isGrabbing", true);

                    luggageHeld.AddGrabber(this);

                    playerMovement.isGrabbing = true;

                    LightLuggageGrab();
                    EnableBridgeCollider();

                    return;
                }

                luggageHeld = null;
                objectRigidbody = null;
            }
        }
    }

    private void Throw(float forwardForce, float upForce)
    {
        if (configurableJoint != null && objectRigidbody != null)
        {
            if (luggageHeld != null)
            {
                // Sticky luggage cannot be thrown
                if (luggageHeld.behaviorType == LuggageBehaviorType.Sticky)
                {
                    isGrabInputHeld = false;
                    grabInputHoldTime = 0f;
                    if (Arrow) Arrow.SetActive(false);
                    return;
                }

                DisableBridgeCollider();
                luggageHeld.RemoveGrabber(this);
                luggageHeld = null;
            }

            if (hasShiftedCoM)
            {
                objectRigidbody.centerOfMass = originalCenterOfMass;
                hasShiftedCoM = false;
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
        Quaternion startWorldRotation = objectRigidbody.transform.rotation;

        Bounds localBounds = ComputeLuggageLocalBounds(luggageHeld);
        float startYaw = startWorldRotation.eulerAngles.y;

        // Two valid carry poses: luggage aligned with player (endYaw1) or 180° flipped (endYaw2).
        // Pick whichever puts the luggage's back face toward the player at grab time, using a
        // horizontal-plane dot product to avoid eulerAngles gimbal-lock when pitch/roll are large.
        Quaternion playerRot = grabPoint.rotation;
        Vector3 luggageFwdH = Vector3.ProjectOnPlane(objectRigidbody.transform.forward, Vector3.up);
        Vector3 playerFwdH  = Vector3.ProjectOnPlane(grabPoint.forward, Vector3.up);
        bool useFlipped = Vector3.Dot(luggageFwdH, playerFwdH) < 0f;
        float endYaw1 = playerRot.eulerAngles.y;
        float endYaw2 = endYaw1 + 180f;
        float endYaw = useFlipped ? endYaw2 : endYaw1;

        // For the normal pose: connect at the back face (min.z) so luggage extends forward.
        // For the flipped pose: connect at the front face (max.z) — after 180° rotation that face
        // is the one closest to the player, so the luggage body still sits in front.
        float connectZ = useFlipped ? localBounds.max.z : localBounds.min.z;
        Vector3 localGrabPoint = new Vector3(localBounds.center.x, localBounds.center.y, connectZ);

        if (!hasShiftedCoM)
        {
            originalCenterOfMass = objectRigidbody.centerOfMass;
        }
        objectRigidbody.centerOfMass = localGrabPoint;
        hasShiftedCoM = true;

        CreateGrabJoint(localGrabPoint);

        Quaternion alignTarget = useFlipped
            ? Quaternion.Inverse(startWorldRotation) * (playerRot * Quaternion.Euler(0f, 180f, 0f))
            : Quaternion.Inverse(startWorldRotation) * playerRot;

        StartCoroutine(GrabRotationAnimation(startWorldRotation, startYaw, endYaw, alignTarget));
    }

    private IEnumerator GrabRotationAnimation(Quaternion startWorldRotation, float startYaw, float endYaw, Quaternion alignTarget)
    {
        // Pick the yaw arc that never swings the luggage to point back at the player.
        // "Dangerous" yaw = endYaw + 180° (luggage front aimed at player).
        float dangerousYaw      = endYaw + 180f;
        float diff              = Mathf.DeltaAngle(startYaw, endYaw);          // shortest signed arc
        float dangerRelToStart  = Mathf.DeltaAngle(startYaw, dangerousYaw);

        // Check whether the shortest arc passes through the dangerous direction.
        bool shortPathCrossesDanger = diff > 0f
            ? (dangerRelToStart > 0f && dangerRelToStart < diff)
            : (dangerRelToStart < 0f && dangerRelToStart > diff);

        // If it does, use the longer arc (opposite direction) to go around the outside.
        float totalYawDelta = shortPathCrossesDanger ? diff - Mathf.Sign(diff) * 360f : diff;

        float elapsed = 0f;

        while (elapsed < grabAlignDuration)
        {
            if (configurableJoint == null || luggageHeld == null) yield break;

            float t = elapsed / grabAlignDuration;
            t = t * t * (3f - 2f * t); // smoothstep

            // Animate only yaw; pitch/roll correction is left to the angular spring drive.
            float currentYaw = startYaw + totalYawDelta * t;
            Quaternion desiredWorldRot = Quaternion.Euler(0f, currentYaw, 0f);
            // targetRotation = Inverse(startWorldRot) * desiredWorldRot encodes the delta
            // the joint needs to apply on top of its captured initial relative rotation.
            configurableJoint.targetRotation = Quaternion.Inverse(startWorldRotation) * desiredWorldRot;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (configurableJoint != null)
            configurableJoint.targetRotation = alignTarget;
    }

    private static Bounds ComputeLuggageLocalBounds(Luggage luggage)
    {
        var meshFilters = luggage.GetComponentsInChildren<MeshFilter>();
        Bounds combined = new Bounds(Vector3.zero, Vector3.one);
        bool initialized = false;
        Matrix4x4 worldToLuggageLocal = luggage.transform.worldToLocalMatrix;

        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;
            Bounds meshBounds = mf.sharedMesh.bounds;
            Matrix4x4 meshToLuggage = worldToLuggageLocal * mf.transform.localToWorldMatrix;

            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                Vector3 inLuggageLocal = meshToLuggage.MultiplyPoint3x4(corner);

                if (!initialized) { combined = new Bounds(inLuggageLocal, Vector3.zero); initialized = true; }
                else combined.Encapsulate(inLuggageLocal);
            }
        }

        return combined;
    }

    private void CreateGrabJoint(Vector3 localGrabPoint)
    {
        configurableJoint = grabPoint.gameObject.AddComponent<ConfigurableJoint>();

        Rigidbody grabPointRb = grabPoint.GetComponent<Rigidbody>();
        if (grabPointRb != null) grabPointRb.isKinematic = true;

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
        SoftJointLimit linearLimit = new SoftJointLimit { limit = 0.05f };
        configurableJoint.linearLimit = linearLimit;

        SoftJointLimitSpring limitSpring = new SoftJointLimitSpring { spring = 500f, damper = 1000f };
        configurableJoint.linearLimitSpring = limitSpring;

        configurableJoint.autoConfigureConnectedAnchor = false;
        configurableJoint.connectedAnchor = localGrabPoint;
        configurableJoint.anchor = grabAnchor.localPosition;

        JointDrive fullDrive = new JointDrive
        {
            positionSpring = 3000f,
            positionDamper = 200f,
            maximumForce = 5000f
        };
        configurableJoint.xDrive = fullDrive;
        configurableJoint.yDrive = fullDrive;
        configurableJoint.zDrive = fullDrive;

        JointDrive angularDrive = new JointDrive
        {
            positionSpring = 1000f,
            positionDamper = 100f,
            maximumForce = 4000f
        };
        configurableJoint.angularXDrive = angularDrive;
        configurableJoint.angularYZDrive = angularDrive;

        configurableJoint.targetRotation = grabAnchor.localRotation;

        configurableJoint.angularXMotion = ConfigurableJointMotion.Free;
        configurableJoint.angularYMotion = ConfigurableJointMotion.Free;
        configurableJoint.angularZMotion = ConfigurableJointMotion.Free;

        configurableJoint.massScale = 1f;
        configurableJoint.connectedMassScale = 1f;

        configurableJoint.projectionMode = JointProjectionMode.PositionAndRotation;
        configurableJoint.projectionDistance = 0.05f;
        configurableJoint.projectionAngle = 5f;

        configurableJoint.breakForce = 10000;
        configurableJoint.breakTorque = 2500f;
    }

    private void EnableBridgeCollider()
    {
        if (bridgeObject == null || luggageHeld == null) return;

        bridgeObject.SetActive(true);

        heldLuggageColliders = luggageHeld.GetComponentsInChildren<Collider>();
        foreach (var col in heldLuggageColliders)
        {
            Physics.IgnoreCollision(bridgeBoxCollider, col, true);
            foreach (var pc in playerColliders)
                Physics.IgnoreCollision(pc, col, true);
        }

        UpdateBridgeCollider();
    }

    private void DisableBridgeCollider()
    {
        if (bridgeObject == null) return;

        if (heldLuggageColliders != null)
        {
            foreach (var col in heldLuggageColliders)
            {
                if (col != null)
                {
                    Physics.IgnoreCollision(bridgeBoxCollider, col, false);
                    foreach (var pc in playerColliders)
                        Physics.IgnoreCollision(pc, col, false);
                }
            }
            heldLuggageColliders = null;
        }

        bridgeObject.SetActive(false);
    }

    private void UpdateBridgeCollider()
    {
        if (luggageHeld == null || bridgeBoxCollider == null || !bridgeObject.activeSelf) return;

        Vector3 anchorPos = grabAnchor.position;
        Vector3 luggagePos = luggageHeld.transform.position;

        Vector3 dir = luggagePos - anchorPos;
        dir.y = 0;
        float dist = dir.magnitude;

        if (dist < 0.01f) return;

        float totalLength = dist + bridgeExtraZ;
        // Shift midpoint back toward player by half of bridgeExtraZ so the collider extends behind the anchor
        Vector3 dirNorm = dir.normalized;
        Vector3 midpoint = anchorPos + dirNorm * (dist * 0.5f - bridgeExtraZ * 0.5f);
        midpoint.y = anchorPos.y + bridgeYOffset;

        bridgeObject.transform.position = midpoint;
        bridgeObject.transform.rotation = Quaternion.LookRotation(dirNorm);
        bridgeBoxCollider.size = new Vector3(bridgeWidth, bridgeHeight, totalLength);
        bridgeBoxCollider.center = Vector3.zero;
    }

    // Called by DesignSceneInput to bypass PlayerInput in the design scene
    public void InjectGrabInput(bool grabDown, bool grabUp) => ProcessGrabInput(grabDown, grabUp);
    public void InjectStationUse(bool pressed)
    {
        if (pressed) TryUseStation();
    }

    private void TryUseStation()
    {
        if (luggageHeld == null) return;

        Collider[] hits = Physics.OverlapSphere(grabPoint.position, grabRadius);
        foreach (var hit in hits)
        {
            MachineStation station = hit.GetComponentInParent<MachineStation>();
            if (station == null || station.IsOccupied) continue;
            if (!station.CanAccept(luggageHeld)) continue;

            // TryPlace will DropAllGrabbers on the luggage, which nullifies luggageHeld via Drop()
            if (station.TryPlace(luggageHeld))
                return;
        }
    }

    public void Drop(bool forceRelease = false)
    {
        if (Arrow) Arrow.SetActive(false);

        // Sticky luggage cannot be dropped unless forced (e.g. by another player grabbing it, or station placement)
        if (!forceRelease && luggageHeld != null && luggageHeld.behaviorType == LuggageBehaviorType.Sticky)
        {
            grabInputHoldTime = 0f;
            isGrabInputHeld = false;
            return;
        }

        grabInputHoldTime = 0f;
        isGrabInputHeld = false;

        if (objectRigidbody != null && hasShiftedCoM)
        {
            objectRigidbody.centerOfMass = originalCenterOfMass;
            hasShiftedCoM = false;
        }

        if (configurableJoint != null)
        {
            DisableBridgeCollider();

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
        return playerIndex;
    }

    public Luggage GetHeldLuggage()
    {
        return luggageHeld;
    }

    public Vector3 GetGrabAnchorWorldPosition()
    {
        return grabAnchor.position;
    }

    private void OnDrawGizmosSelected()
    {
        if (grabPoint == null) return;

        // Draw grab detection sphere
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(grabPoint.position, grabRadius);

        // Draw block raycast to nearest luggage in range
        Collider[] nearby = Physics.OverlapSphere(grabPoint.position, grabRadius, grabbableLayer);
        foreach (var hit in nearby)
        {
            Rigidbody rb = hit.attachedRigidbody;
            if (rb == null) continue;

            Vector3 toTarget = rb.worldCenterOfMass - grabPoint.position;

            if (Physics.Raycast(grabPoint.position, toTarget.normalized, out RaycastHit blockHit, toTarget.magnitude, grabBlockLayer))
            {
                // Blocked - red line to wall, then dashed to luggage
                Gizmos.color = Color.red;
                Gizmos.DrawLine(grabPoint.position, blockHit.point);
                Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                Gizmos.DrawLine(blockHit.point, rb.worldCenterOfMass);
            }
            else
            {
                // Clear - green line to luggage
                Gizmos.color = Color.green;
                Gizmos.DrawLine(grabPoint.position, rb.worldCenterOfMass);
            }
        }
    }

}
