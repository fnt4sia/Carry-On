using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Per-player grab / throw / station-use controller. Reads the Grab and UseStation
// input, casts for grabbable luggage, builds a ConfigurableJoint to carry it, animates
// the carry alignment, handles throw-charge timing, and routes placement into stations.
public class PlayerGrab : MonoBehaviour
{
    [Header("Detection and Alignment")]
    [SerializeField, Min(0f)] private float grabRadius = 1.4f;
    [SerializeField, Min(0f)] private float grabAlignDuration = 1.25f;

    [Header("Throw")]
    [SerializeField, Min(0f)] private float throwMinHoldTime = 0.25f;
    [SerializeField, Min(0f)] private float throwMaxHoldTime = 1.5f;
    [SerializeField, Min(0f)] private float throwMinForce = 200f;
    [SerializeField, Min(0f)] private float throwMaxForce = 800f;
    [SerializeField, Min(0f)] private float throwMinUpForce = 100f;
    [SerializeField, Min(0f)] private float throwMaxUpForce = 400f;

    [Header("Bridge Collider")]
    [SerializeField, Min(0f)] private float bridgeWidth = 2f;
    [SerializeField, Min(0f)] private float bridgeHeight = 2f;
    [SerializeField] private float bridgeYOffset = 1f;
    [SerializeField, Min(0f)] private float bridgeExtraZ = 1f;

    // These all configure the carry ConfigurableJoint. The "joint" prefix keeps them from
    // colliding with the local JointDrive / SoftJointLimit builders in CreateGrabJoint.
    [Header("Joint Limits")]
    [SerializeField, Min(0f)] private float jointLinearLimit = 0.05f;
    [SerializeField, Min(0f)] private float jointLinearLimitSpring = 500f;
    [SerializeField, Min(0f)] private float jointLinearLimitDamper = 1000f;

    [Header("Joint Drives")]
    [SerializeField, Min(0f)] private float jointPositionSpring = 3000f;
    [SerializeField, Min(0f)] private float jointPositionDamper = 200f;
    [SerializeField, Min(0f)] private float jointPositionMaximumForce = 5000f;
    [SerializeField, Min(0f)] private float jointAngularSpring = 1000f;
    [SerializeField, Min(0f)] private float jointAngularDamper = 100f;
    [SerializeField, Min(0f)] private float jointAngularMaximumForce = 4000f;

    [Header("Joint Projection and Break")]
    [SerializeField, Min(0f)] private float jointProjectionDistance = 0.05f;
    [SerializeField, Min(0f)] private float jointProjectionAngle = 5f;
    [SerializeField, Min(0f)] private float jointBreakForce = 10000f;
    [SerializeField, Min(0f)] private float jointBreakTorque = 2500f;

    [SerializeField] private Transform grabPoint;
    [SerializeField] private Transform grabAnchor;
    [SerializeField] private LayerMask grabbableLayer;
    [SerializeField] private LayerMask grabBlockLayer;
    [SerializeField] private PlayerMovement playerMovement;
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
    private InputAction useStationAction;

    private bool isGrabInputHeld;
    private float grabInputHoldTime;
    private bool throwChargeStarted;

    // The machine this player is currently working with the grab button held. While this is set
    // the press drives the machine instead of grabbing, dropping or charging a throw.
    private MachineStation crankedStation;
    private AudioSource throwBuildUpAudioSource;

    private ConfigurableJoint configurableJoint;
    private Luggage luggageHeld;
    private Rigidbody objectRigidbody;

    private GameObject bridgeObject;
    private BoxCollider bridgeBoxCollider;
    private Collider[] heldLuggageColliders;

    private readonly Collider[] nearbyHits = new Collider[32];
    private Outline lastOutlined = null;

    private Collider[] playerColliders;
    private Vector3 originalCenterOfMass;
    private bool hasShiftedCoM;

    // True while a throw is actually charging: Grab held past the drop window with a case
    // in hand. PlayerHandIK reads this to let the wind-up clip show through the torso
    // instead of holding the carry pose.
    public bool IsChargingThrow => throwChargeStarted && luggageHeld != null;

    // Kept as properties so a max typed below its min still yields a usable range rather
    // than a negative one.
    private float ThrowMaxHoldTime => Mathf.Max(throwMinHoldTime, throwMaxHoldTime);
    private float ThrowMaxForce => Mathf.Max(throwMinForce, throwMaxForce);
    private float ThrowMaxUpForce => Mathf.Max(throwMinUpForce, throwMaxUpForce);

    private void OnDisable()
    {
        Drop(forceRelease: true);
    }

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        grabAction = playerInput.actions.FindAction("Player/Grab", throwIfNotFound: true);
        useStationAction = playerInput.actions.FindAction("Player/UseStation", throwIfNotFound: true);

        playerColliders = GetComponentsInChildren<Collider>();
        bridgeObject = new GameObject("BridgeCollider");
        bridgeObject.transform.SetParent(transform);
        bridgeObject.layer = gameObject.layer;
        bridgeBoxCollider = bridgeObject.AddComponent<BoxCollider>();
        bridgeObject.SetActive(false);
    }

    void Update()
    {
        bool grabDown = grabAction.WasPressedThisFrame();
        bool grabUp   = grabAction.WasReleasedThisFrame();
        Luggage candidate = luggageHeld == null ? FindBestGrabCandidate() : null;

        // Grab is also the machine button. A press near a machine loads the bag you are carrying,
        // and holding it afterwards works the machine — so one button covers carry, load and crank
        // the way Overcooked does. Handled before the normal grab logic so loading a bag never
        // starts a throw charge and cranking never grabs the bag back out.
        if (!ProcessStationInput(grabDown, grabUp))
            ProcessGrabInput(grabDown, grabUp, candidate);

        // Legacy secondary button: still loads a station and still pulls levers.
        if (useStationAction.WasPressedThisFrame() && !TryUseStation())
            TryUseLever();

        // Arrow charge visual (runs in all scenes). Arrow is optional: a body prefab
        // without one must not break grabbing, so guard instead of dereferencing.
        if (Arrow != null && throwChargeStarted && objectRigidbody != null)
            UpdateArrowChargeVisual();

        if (isGrabInputHeld)
        {
            grabInputHoldTime += Time.deltaTime;

            // Charge feedback (buildup SFX, throw pose, arrow) waits until the press has
            // outlived the drop window, so a quick tap reads as a plain drop with no wind-up.
            if (!throwChargeStarted && luggageHeld != null && grabInputHoldTime >= throwMinHoldTime)
            {
                throwChargeStarted = true;
                StartThrowBuildUpAudio();
                animator.SetBool(AnimId.IsThrowing, true);
                if (Arrow)
                {
                    // Scale before showing, or the arrow renders one frame at whatever
                    // stale scale it was left with (the prefab authors it big).
                    UpdateArrowChargeVisual();
                    Arrow.SetActive(true);
                }
            }
        }

        UpdateOutline(luggageHeld == null ? candidate : null);

        if (luggageHeld != null)
            UpdateBridgeCollider();
    }

    private void ProcessGrabInput(bool grabDown, bool grabUp, Luggage candidate)
    {
        if (grabDown && objectRigidbody != null)
        {
            if (luggageHeld == null || luggageHeld.behaviorType != LuggageBehaviorType.Sticky)
            {
                isGrabInputHeld = true;
                grabInputHoldTime = 0f;
            }
        }

        if (grabUp && objectRigidbody != null && isGrabInputHeld)
        {
            if (throwChargeStarted)
            {
                animator.SetBool(AnimId.IsThrowing, false);
                if (Arrow) Arrow.SetActive(false);

                float clampedHoldTime = Mathf.Clamp(grabInputHoldTime, throwMinHoldTime, ThrowMaxHoldTime);
                float holdRange = Mathf.Max(0.01f, ThrowMaxHoldTime - throwMinHoldTime);
                float t = (clampedHoldTime - throwMinHoldTime) / holdRange;
                Throw(Mathf.Lerp(throwMinForce, ThrowMaxForce, t), Mathf.Lerp(throwMinUpForce, ThrowMaxUpForce, t));
            }
            else
            {
                Drop();
            }

            isGrabInputHeld = false;
            throwChargeStarted = false;
        }

        if (grabDown && objectRigidbody == null)
            TryGrab(candidate);
    }

    // Returns true when the grab button was consumed by a machine this frame.
    private bool ProcessStationInput(bool grabDown, bool grabUp)
    {
        if (grabUp && crankedStation != null)
        {
            // Letting go pauses the machine. Progress stays banked.
            crankedStation = null;
            isGrabInputHeld = false;
            grabInputHoldTime = 0f;
            return true;
        }

        if (crankedStation != null)
        {
            // Walking away, or the machine finishing, ends the session on its own.
            if (!crankedStation.IsAwaitingCrank || !IsWithinReach(crankedStation.transform))
            {
                crankedStation = null;
                return true;
            }

            crankedStation.AddCrankProgress(Time.deltaTime);
            return true;
        }

        if (!grabDown)
            return false;

        if (luggageHeld != null)
        {
            MachineStation loaded = TryLoadStation();
            if (loaded == null)
                return false;

            // Keep the press alive into the crank, so loading and working the machine are one
            // continuous hold rather than two separate presses.
            if (loaded.IsAwaitingCrank)
                crankedStation = loaded;
            return true;
        }

        // Empty-handed at a loaded machine: start working it rather than grabbing.
        MachineStation waiting = FindStationAwaitingCrank();
        if (waiting == null)
            return false;

        crankedStation = waiting;
        crankedStation.AddCrankProgress(Time.deltaTime);
        return true;
    }

    private MachineStation FindStationAwaitingCrank()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(grabPoint.position, grabRadius, nearbyHits);
        MachineStation found = null;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = nearbyHits[i];
            nearbyHits[i] = null;
            if (found != null) continue;

            MachineStation station = hit != null ? hit.GetComponentInParent<MachineStation>() : null;
            if (station != null && station.IsAwaitingCrank)
                found = station;
        }

        return found;
    }

    private bool IsWithinReach(Transform target)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(grabPoint.position, grabRadius, nearbyHits);
        bool inReach = false;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = nearbyHits[i];
            nearbyHits[i] = null;
            if (inReach || hit == null) continue;

            MachineStation station = hit.GetComponentInParent<MachineStation>();
            if (station != null && station.transform == target)
                inReach = true;
        }

        return inReach;
    }

    private void UpdateOutline(Luggage candidate)
    {
        Outline current = candidate != null ? candidate.GetComponent<Outline>() : null;
        if (lastOutlined == current)
            return;

        if (lastOutlined != null)
            lastOutlined.enabled = false;

        lastOutlined = current;
        if (lastOutlined != null)
            lastOutlined.enabled = true;
    }

    private Luggage FindBestGrabCandidate()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            grabPoint.position,
            grabRadius,
            nearbyHits,
            grabbableLayer,
            QueryTriggerInteraction.Collide);

        Luggage best = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = nearbyHits[i];
            nearbyHits[i] = null;
            if (!Luggage.TryGetFromCollider(hit, out Luggage luggage)
                || luggage.Body == null
                || luggage.IsInStation)
                continue;

            Vector3 toTarget = luggage.Body.worldCenterOfMass - grabPoint.position;
            if (Physics.Raycast(
                    grabPoint.position,
                    toTarget.normalized,
                    toTarget.magnitude,
                    grabBlockLayer,
                    QueryTriggerInteraction.Ignore))
                continue;

            float distance = toTarget.sqrMagnitude;
            if (distance >= bestDistance)
                continue;

            best = luggage;
            bestDistance = distance;
        }

        return best;
    }

    private void TryGrab(Luggage candidateLuggage)
    {
        if (candidateLuggage == null || candidateLuggage.Body == null)
            return;

        objectRigidbody = candidateLuggage.Body;
        luggageHeld = candidateLuggage;

        // Force-drop all other grabbers (steal the luggage).
        luggageHeld.DropAllGrabbers();
        objectRigidbody.isKinematic = false;
        animator.SetBool(AnimId.IsGrabbing, true);
        luggageHeld.AddGrabber(this);
        playerMovement.isGrabbing = true;
        LightLuggageGrab();
        EnableBridgeCollider();
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
                    StopThrowBuildUpAudio();
                    animator.SetBool(AnimId.IsThrowing, false);
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
            StopThrowBuildUpAudio();
            AudioManager.Instance?.PlaySFX(Sfx.PlayerThrow);

            objectRigidbody = null;
            playerMovement.isGrabbing = false;
            animator.SetBool(AnimId.IsGrabbing, false);
        }
    }

    private void LightLuggageGrab()
    {
        Quaternion startWorldRotation = objectRigidbody.transform.rotation;

        Bounds localBounds = ComputeLuggageLocalBounds(luggageHeld);
        float startYaw = startWorldRotation.eulerAngles.y;

        // Always align luggage with the player (normal pose). Connect at the back face (min.z)
        // so the luggage body extends forward, away from the player.
        Quaternion playerRot = grabPoint.rotation;
        float endYaw = playerRot.eulerAngles.y;

        Vector3 localGrabPoint = new Vector3(localBounds.center.x, localBounds.center.y, localBounds.min.z);

        if (!hasShiftedCoM)
        {
            originalCenterOfMass = objectRigidbody.centerOfMass;
        }
        objectRigidbody.centerOfMass = localGrabPoint;
        hasShiftedCoM = true;

        CreateGrabJoint(localGrabPoint);

        Quaternion alignTarget = Quaternion.Inverse(startWorldRotation) * playerRot;

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

            float t = grabAlignDuration > 0f ? elapsed / grabAlignDuration : 1f;
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
        SoftJointLimit linearLimit = new SoftJointLimit
        {
            limit = jointLinearLimit
        };
        configurableJoint.linearLimit = linearLimit;

        SoftJointLimitSpring limitSpring = new SoftJointLimitSpring
        {
            spring = jointLinearLimitSpring,
            damper = jointLinearLimitDamper
        };
        configurableJoint.linearLimitSpring = limitSpring;

        configurableJoint.autoConfigureConnectedAnchor = false;
        configurableJoint.connectedAnchor = localGrabPoint;
        // anchor is in the joint's own local space (the joint lives on grabPoint), so
        // convert grabAnchor into that space. Using grabAnchor.localPosition directly
        // would be root-space and get multiplied by grabPoint's scale.
        configurableJoint.anchor = grabPoint.InverseTransformPoint(grabAnchor.position);

        JointDrive fullDrive = new JointDrive
        {
            positionSpring = jointPositionSpring,
            positionDamper = jointPositionDamper,
            maximumForce = jointPositionMaximumForce
        };
        configurableJoint.xDrive = fullDrive;
        configurableJoint.yDrive = fullDrive;
        configurableJoint.zDrive = fullDrive;

        JointDrive angularDrive = new JointDrive
        {
            positionSpring = jointAngularSpring,
            positionDamper = jointAngularDamper,
            maximumForce = jointAngularMaximumForce
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
        configurableJoint.projectionDistance = jointProjectionDistance;
        configurableJoint.projectionAngle = jointProjectionAngle;

        configurableJoint.breakForce = jointBreakForce;
        configurableJoint.breakTorque = jointBreakTorque;
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

    // Grows the arrow and pushes it out over the same min→max hold window the throw force
    // uses, so it appears at ArrowMinScale the moment the charge starts and peaks with it.
    private void UpdateArrowChargeVisual()
    {
        float holdRange = Mathf.Max(0.01f, ThrowMaxHoldTime - throwMinHoldTime);
        float t = Mathf.Clamp01((grabInputHoldTime - throwMinHoldTime) / holdRange);

        float arrowScale = Mathf.Lerp(ArrowMinScale, ArrowMaxScale, t);
        Arrow.transform.localScale = new Vector3(arrowScale, arrowScale, arrowScale);

        float arrowZPos = Mathf.Lerp(ArrowMinZPos, ArrowMaxZPos, t);
        Arrow.transform.localPosition = new Vector3(0, ArrowHeight, arrowZPos);
    }

    private bool TryUseStation() => TryLoadStation() != null;

    // Puts the carried bag into a nearby machine. Returns the machine it went into, so the caller
    // can carry the same button press straight on into cranking it.
    private MachineStation TryLoadStation()
    {
        if (luggageHeld == null) return null;

        int hitCount = Physics.OverlapSphereNonAlloc(grabPoint.position, grabRadius, nearbyHits);
        MachineStation loaded = null;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = nearbyHits[i];
            nearbyHits[i] = null;
            if (loaded != null || hit == null) continue;

            MachineStation station = hit.GetComponentInParent<MachineStation>();
            if (station == null || station.IsOccupied) continue;
            if (!station.CanAccept(luggageHeld)) continue;

            // TryPlace will DropAllGrabbers on the luggage, which nullifies luggageHeld via Drop()
            if (station.TryPlace(luggageHeld))
                loaded = station;
        }

        return loaded;
    }

    private bool TryUseLever()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(grabPoint.position, grabRadius, nearbyHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = nearbyHits[i];
            nearbyHits[i] = null;
            Lever lever = hit.GetComponentInParent<Lever>();
            if (lever == null) continue;

            lever.Use();
            return true;
        }

        return false;
    }

    public void Drop(bool forceRelease = false)
    {
        if (Arrow) Arrow.SetActive(false);
        StopThrowBuildUpAudio();

        // Sticky luggage cannot be dropped unless forced (e.g. by another player grabbing it, or station placement)
        if (!forceRelease && luggageHeld != null && luggageHeld.behaviorType == LuggageBehaviorType.Sticky)
        {
            grabInputHoldTime = 0f;
            isGrabInputHeld = false;
            return;
        }

        grabInputHoldTime = 0f;
        isGrabInputHeld = false;
        throwChargeStarted = false;

        if (objectRigidbody != null && hasShiftedCoM)
        {
            objectRigidbody.centerOfMass = originalCenterOfMass;
            hasShiftedCoM = false;
        }

        DisableBridgeCollider();

        if (luggageHeld != null)
        {
            luggageHeld.RemoveGrabber(this);
            luggageHeld = null;
        }

        if (configurableJoint != null)
            Destroy(configurableJoint);

        configurableJoint = null;
        objectRigidbody = null;
        if (playerMovement != null)
            playerMovement.isGrabbing = false;
        if (animator != null)
        {
            animator.SetBool(AnimId.IsGrabbing, false);
            animator.SetBool(AnimId.IsThrowing, false);
        }
    }

    public int GetPlayerIndex()
    {
        return playerInput != null ? playerInput.playerIndex : -1;
    }

    public Luggage GetHeldLuggage()
    {
        return luggageHeld;
    }

    public Vector3 GetGrabAnchorWorldPosition()
    {
        return grabAnchor.position;
    }

    private void StartThrowBuildUpAudio()
    {
        StopThrowBuildUpAudio();
        throwBuildUpAudioSource = AudioManager.Instance?.PlayLoopingSFX(Sfx.PlayerThrowBuildup);
    }

    private void StopThrowBuildUpAudio()
    {
        AudioManager.Instance?.StopSFX(throwBuildUpAudioSource);
        throwBuildUpAudioSource = null;
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
