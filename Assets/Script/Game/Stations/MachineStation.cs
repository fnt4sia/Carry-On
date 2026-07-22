using System.Collections;
using UnityEngine;

public enum StationPhase
{
    Idle,
    Processing,
    Ready
}

/// <summary>
/// Shared place → process → ready → eject lifecycle for the Washer and Wrapper.
/// Finished animation clips can drive the lifecycle with events; prototype machines
/// can use the timed fallback without changing gameplay code.
/// </summary>
public abstract class MachineStation : MonoBehaviour
{
    [Header("Tuning")]
    [SerializeField] private StationTuning stationTuning;

    [Header("Station")]
    [Tooltip("Where luggage snaps when placed.")]
    [SerializeField] protected Transform snapTransform;
    [Tooltip("Moving anchor used by the machine's output animation.")]
    [SerializeField] protected Transform sliderTransform;
    [SerializeField] protected Animator machineAnimator;

    [Header("Lifecycle")]
    [Tooltip("Disable for prototype machines that do not have animation events.")]
    [SerializeField] private bool animationDriven = true;
    [SerializeField, Min(0.05f)] private float processDuration = 1.5f;
    [SerializeField, Min(0f)] private float readyDuration = 0.75f;

    [Header("Output Clearance")]
    [SerializeField] private bool shoveBlockingLuggageOnPlace = true;
    [SerializeField] private Transform obstacleCheckTransform;

    protected Luggage currentLuggage;
    protected bool isProcessing;

    private Vector3 luggageSliderLocalPosition;
    private Quaternion luggageSliderLocalRotation;
    private StationPhase phase;

    private static readonly Collider[] s_ObstacleHits = new Collider[24];

    public bool IsOccupied => phase != StationPhase.Idle;
    public bool IsProcessing => phase == StationPhase.Processing;

    private Vector3 ObstacleCheckHalfExtents => stationTuning.ObstacleCheckHalfExtents;
    private float ObstacleShoveImpulse => stationTuning.ObstacleShoveImpulse;
    private float ObstacleShoveUpImpulse => stationTuning.ObstacleShoveUpImpulse;
    private float ObstacleShoveRandomness => stationTuning.ObstacleShoveRandomness;

    public abstract bool CanAccept(Luggage luggage);
    protected abstract Luggage OnProcessComplete(Luggage luggage);
    protected virtual void OnLuggagePlaced(Luggage luggage) { }
    protected virtual void OnStationReset() { }

    private void Awake()
    {
        if (stationTuning != null)
            return;

        Debug.LogError($"{GetType().Name} '{name}' has no {nameof(StationTuning)}.", this);
        enabled = false;
    }

    public bool TryPlace(Luggage luggage)
    {
        if (IsOccupied || luggage == null || !CanAccept(luggage))
            return false;

        ShoveBlockingLuggage(luggage);
        luggage.DropAllGrabbers();

        Rigidbody rb = luggage.Body;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        Transform anchor = snapTransform != null
            ? snapTransform
            : sliderTransform != null
                ? sliderTransform
                : transform;
        luggage.transform.SetPositionAndRotation(anchor.position, anchor.rotation);

        if (sliderTransform != null)
        {
            luggageSliderLocalPosition = sliderTransform.InverseTransformPoint(luggage.transform.position);
            luggageSliderLocalRotation = Quaternion.Inverse(sliderTransform.rotation) * luggage.transform.rotation;
        }

        luggage.SetInStation(true);
        currentLuggage = luggage;
        isProcessing = true;
        phase = StationPhase.Processing;
        OnLuggagePlaced(luggage);

        if (machineAnimator != null)
            machineAnimator.SetBool(AnimId.IsTriggered, true);

        if (!animationDriven || machineAnimator == null)
            StartCoroutine(AutomaticProcess());

        return true;
    }

    // Animation Event: the gameplay operation occurs when the luggage is sealed.
    public void AnimEvent_OnDoorClosed()
    {
        if (currentLuggage == null || phase != StationPhase.Processing)
            return;

        currentLuggage = OnProcessComplete(currentLuggage);
        phase = StationPhase.Ready;
    }

    // Animation Event: the output is reachable and can be picked up again.
    public void AnimEvent_OnOutputComplete()
    {
        if (currentLuggage == null)
            return;

        Luggage finishedLuggage = currentLuggage;
        Rigidbody rb = finishedLuggage.Body;
        if (rb != null)
            rb.isKinematic = false;

        finishedLuggage.SetInStation(false);
        currentLuggage = null;
        isProcessing = false;
        if (machineAnimator != null)
            machineAnimator.SetBool(AnimId.IsTriggered, false);

        phase = StationPhase.Idle;
        OnStationReset();
    }

    private void LateUpdate()
    {
        if (!isProcessing || currentLuggage == null || sliderTransform == null)
            return;

        currentLuggage.transform.position = sliderTransform.TransformPoint(luggageSliderLocalPosition);
        currentLuggage.transform.rotation = sliderTransform.rotation * luggageSliderLocalRotation;
    }

    private IEnumerator AutomaticProcess()
    {
        yield return new WaitForSeconds(processDuration);
        AnimEvent_OnDoorClosed();

        if (readyDuration > 0f)
            yield return new WaitForSeconds(readyDuration);

        AnimEvent_OnOutputComplete();
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
            ObstacleCheckHalfExtents,
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

            if (!Luggage.TryGetFromCollider(hit, out Luggage blockingLuggage)
                || blockingLuggage == incomingLuggage
                || blockingLuggage.IsInStation
                || blockingLuggage.IsDelivered
                || blockingLuggage.GetIsGrabbed())
            {
                continue;
            }

            Rigidbody blockingRb = blockingLuggage.Body;
            if (blockingRb == null)
                continue;

            blockingRb.isKinematic = false;

            Vector3 shoveDirection = blockingRb.position - pushOrigin;
            shoveDirection.y = 0f;

            if (shoveDirection.sqrMagnitude < 0.0001f)
            {
                Vector2 randomFlatDirection = UnityEngine.Random.insideUnitCircle.normalized;
                shoveDirection = new Vector3(randomFlatDirection.x, 0f, randomFlatDirection.y);
            }

            Vector2 randomFlatOffset = UnityEngine.Random.insideUnitCircle * ObstacleShoveRandomness;
            shoveDirection = (
                shoveDirection.normalized
                + new Vector3(randomFlatOffset.x, 0f, randomFlatOffset.y)).normalized;
            if (shoveDirection.sqrMagnitude < 0.0001f)
                shoveDirection = transform.forward;

            blockingRb.AddForce(
                shoveDirection * ObstacleShoveImpulse + Vector3.up * ObstacleShoveUpImpulse,
                ForceMode.Impulse);
            blockingRb.AddTorque(
                UnityEngine.Random.onUnitSphere * ObstacleShoveImpulse,
                ForceMode.Impulse);
        }

        for (int i = 0; i < hitCount; i++)
            s_ObstacleHits[i] = null;
    }
}
