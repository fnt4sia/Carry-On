using UnityEngine;

// Abstract base for all luggage-processing stations (Washer / Wrapper / Scanner).
// Handles the place → animator-trigger → process-complete → eject lifecycle and is
// driven by animation events (AnimEvent_OnDoorClosed, AnimEvent_OnOutputComplete).
public abstract class MachineStation : MonoBehaviour
{
    [Header("Station")]
    [Tooltip("Where the luggage snaps to when first placed (should sit on the slider's receive end).")]
    [SerializeField] protected Transform snapTransform;
    [Tooltip("The slider GameObject — luggage is parented here so it rides the animation.")]
    [SerializeField] protected Transform sliderTransform;
    [SerializeField] protected Animator machineAnimator;

    [Header("Output Clearance")]
    [SerializeField] private bool shoveBlockingLuggageOnPlace = true;
    [SerializeField] private Transform obstacleCheckTransform;
    [SerializeField] private Vector3 obstacleCheckHalfExtents = new Vector3(1.5f, 1f, 1.5f);
    [SerializeField, Min(0f)] private float obstacleShoveImpulse = 5f;
    [SerializeField, Min(0f)] private float obstacleShoveUpImpulse = 1.5f;
    [SerializeField, Min(0f)] private float obstacleShoveRandomness = 0.65f;

    protected Luggage currentLuggage;
    protected bool isProcessing;
    private Vector3 luggageSliderLocalPosition;
    private Quaternion luggageSliderLocalRotation;

    private static readonly Collider[] s_ObstacleHits = new Collider[24];

    public bool IsOccupied => isProcessing;
    public bool IsProcessing => isProcessing;

    public abstract bool CanAccept(Luggage luggage);
    protected abstract Luggage OnProcessComplete(Luggage luggage);

    public bool TryPlace(Luggage luggage)
    {
        if (IsOccupied || luggage == null || !CanAccept(luggage)) return false;

        ShoveBlockingLuggage(luggage);
        luggage.DropAllGrabbers();

        Rigidbody rb = luggage.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        Transform anchor = snapTransform != null ? snapTransform : sliderTransform != null ? sliderTransform : transform;
        luggage.transform.position = anchor.position;
        luggage.transform.rotation = anchor.rotation;

        if (sliderTransform != null)
        {
            luggageSliderLocalPosition = sliderTransform.InverseTransformPoint(luggage.transform.position);
            luggageSliderLocalRotation = Quaternion.Inverse(sliderTransform.rotation) * luggage.transform.rotation;
        }

        luggage.SetInStation(true);
        currentLuggage = luggage;
        isProcessing = true;

        machineAnimator.SetBool("isTriggered", true);
        return true;
    }

    // Animation Event — place on the last frame of "door closing" (luggage is sealed inside)
    public void AnimEvent_OnDoorClosed()
    {
        if (currentLuggage == null) return;
        currentLuggage = OnProcessComplete(currentLuggage);
    }

    // Animation Event — place on the last frame of "washing slider pushing" (luggage fully pushed out)
    public void AnimEvent_OnOutputComplete()
    {
        if (currentLuggage == null) return;

        Luggage finishedLuggage = currentLuggage;
        Rigidbody rb = finishedLuggage.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = false;

        finishedLuggage.SetInStation(false);
        currentLuggage = null;
        isProcessing = false;

        machineAnimator.SetBool("isTriggered", false);
    }

    private void LateUpdate()
    {
        if (!isProcessing || currentLuggage == null || sliderTransform == null)
            return;

        currentLuggage.transform.position = sliderTransform.TransformPoint(luggageSliderLocalPosition);
        currentLuggage.transform.rotation = sliderTransform.rotation * luggageSliderLocalRotation;
    }

    private void ShoveBlockingLuggage(Luggage incomingLuggage)
    {
        if (!shoveBlockingLuggageOnPlace)
            return;

        Transform checkTransform = obstacleCheckTransform != null
            ? obstacleCheckTransform
            : sliderTransform != null
                ? sliderTransform
                : snapTransform != null
                    ? snapTransform
                    : transform;

        int hitCount = Physics.OverlapBoxNonAlloc(
            checkTransform.position,
            obstacleCheckHalfExtents,
            s_ObstacleHits,
            checkTransform.rotation,
            ~0,
            QueryTriggerInteraction.Ignore);

        Vector3 pushOrigin = snapTransform != null ? snapTransform.position : checkTransform.position;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = s_ObstacleHits[i];
            if (hit == null)
                continue;

            Luggage blockingLuggage = hit.GetComponentInParent<Luggage>();
            if (blockingLuggage == null
                || blockingLuggage == incomingLuggage
                || blockingLuggage.IsInStation
                || blockingLuggage.IsDelivered
                || blockingLuggage.GetIsGrabbed())
                continue;

            Rigidbody blockingRb = blockingLuggage.GetComponent<Rigidbody>();
            if (blockingRb == null)
                continue;

            blockingRb.isKinematic = false;

            Vector3 shoveDirection = blockingRb.position - pushOrigin;
            shoveDirection.y = 0f;

            if (shoveDirection.sqrMagnitude < 0.0001f)
            {
                Vector2 randomFlatDirection = Random.insideUnitCircle.normalized;
                shoveDirection = new Vector3(randomFlatDirection.x, 0f, randomFlatDirection.y);
            }

            Vector2 randomFlatOffset = Random.insideUnitCircle * obstacleShoveRandomness;
            shoveDirection = (shoveDirection.normalized + new Vector3(randomFlatOffset.x, 0f, randomFlatOffset.y)).normalized;
            if (shoveDirection.sqrMagnitude < 0.0001f)
                shoveDirection = transform.forward;

            blockingRb.AddForce(
                shoveDirection * obstacleShoveImpulse + Vector3.up * obstacleShoveUpImpulse,
                ForceMode.Impulse);
            blockingRb.AddTorque(Random.onUnitSphere * obstacleShoveImpulse, ForceMode.Impulse);
        }

        for (int i = 0; i < hitCount; i++)
            s_ObstacleHits[i] = null;
    }
}
