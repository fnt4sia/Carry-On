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

    [Header("Code-Driven Travel")]
    [Tooltip("Point under the machine roof that placed luggage slides to. Empty keeps it at the snap point.")]
    [SerializeField] private Transform intakeTransform;
    [Tooltip("Where the finished luggage starts its trip out. Empty reuses the intake point.")]
    [SerializeField] private Transform releaseTransform;
    [Tooltip("Where finished luggage is handed back. Empty leaves it where it was processed.")]
    [SerializeField] private Transform outputTransform;
    [SerializeField, Min(0f)] private float intakeDuration = 0.5f;

    [Header("Output Clearance")]
    [SerializeField] private bool shoveBlockingLuggageOnPlace = true;
    [SerializeField] private Transform obstacleCheckTransform;
    [SerializeField] private Vector3 obstacleCheckHalfExtents = new(1.5f, 1f, 1.5f);
    [SerializeField, Min(0f)] private float obstacleShoveImpulse = 5f;
    [SerializeField, Min(0f)] private float obstacleShoveUpImpulse = 1.5f;
    [SerializeField, Min(0f)] private float obstacleShoveRandomness = 0.65f;

    protected Luggage currentLuggage;
    protected bool isProcessing;

    private Vector3 luggageSliderLocalPosition;
    private Quaternion luggageSliderLocalRotation;
    private StationPhase phase;

    private static readonly Collider[] s_ObstacleHits = new Collider[24];

    public bool IsOccupied => phase != StationPhase.Idle;
    public bool IsProcessing => phase == StationPhase.Processing;
    public StationPhase Phase => phase;

    // Kept as a property because Vector3 has no Min attribute and a negative half-extent
    // silently makes the overlap box empty.
    private Vector3 ObstacleCheckHalfExtents => new(
        Mathf.Max(0f, obstacleCheckHalfExtents.x),
        Mathf.Max(0f, obstacleCheckHalfExtents.y),
        Mathf.Max(0f, obstacleCheckHalfExtents.z));

    public abstract bool CanAccept(Luggage luggage);
    protected abstract Luggage OnProcessComplete(Luggage luggage);
    protected virtual void OnLuggagePlaced(Luggage luggage) { }
    protected virtual void OnStationReset() { }

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

        // For animation-driven machines the flag is the whole lifecycle, so it goes up the moment
        // the bag is placed. Timed machines raise it later, once the bag is actually inside.
        if (animationDriven)
            SetAnimatorTriggered(true);

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
        SetAnimatorTriggered(false);

        phase = StationPhase.Idle;
        OnStationReset();
    }

    private void SetAnimatorTriggered(bool value)
    {
        if (machineAnimator != null)
            machineAnimator.SetBool(AnimId.IsTriggered, value);
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
        if (intakeTransform != null)
            yield return TravelTo(intakeTransform, intakeDuration);

        // The machine only runs while the bag is behind the curtain: it starts once the bag is
        // all the way in and stops before it is handed back out.
        SetAnimatorTriggered(true);
        yield return new WaitForSeconds(processDuration);
        SetAnimatorTriggered(false);

        AnimEvent_OnDoorClosed();

        // The bag goes in on the entry lane and comes back out on the exit lane. Both points sit
        // under the machine roof behind the curtains, so this hard cut is never on screen.
        if (releaseTransform != null && currentLuggage != null)
        {
            currentLuggage.transform.SetPositionAndRotation(
                releaseTransform.position, releaseTransform.rotation);
        }

        if (outputTransform != null)
            yield return TravelTo(outputTransform, readyDuration);
        else if (readyDuration > 0f)
            yield return new WaitForSeconds(readyDuration);

        AnimEvent_OnOutputComplete();
    }

    // Slides the held luggage to an anchor. Machines whose art has no tray to ride drive the
    // trip through the machine from here instead of from an animation clip.
    // AnimEvent_OnDoorClosed swaps currentLuggage for a fresh object at the same pose, so this
    // re-reads currentLuggage every frame rather than caching the transform.
    private IEnumerator TravelTo(Transform destination, float duration)
    {
        if (currentLuggage == null)
            yield break;

        Vector3 startPosition = currentLuggage.transform.position;
        Quaternion startRotation = currentLuggage.transform.rotation;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (currentLuggage == null)
                yield break;

            float t = Mathf.Clamp01(elapsed / duration);
            currentLuggage.transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, destination.position, t),
                Quaternion.Slerp(startRotation, destination.rotation, t));
            yield return null;
        }

        if (currentLuggage != null)
            currentLuggage.transform.SetPositionAndRotation(destination.position, destination.rotation);
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

            Vector2 randomFlatOffset = UnityEngine.Random.insideUnitCircle * obstacleShoveRandomness;
            shoveDirection = (
                shoveDirection.normalized
                + new Vector3(randomFlatOffset.x, 0f, randomFlatOffset.y)).normalized;
            if (shoveDirection.sqrMagnitude < 0.0001f)
                shoveDirection = transform.forward;

            blockingRb.AddForce(
                shoveDirection * obstacleShoveImpulse + Vector3.up * obstacleShoveUpImpulse,
                ForceMode.Impulse);
            blockingRb.AddTorque(
                UnityEngine.Random.onUnitSphere * obstacleShoveImpulse,
                ForceMode.Impulse);
        }

        for (int i = 0; i < hitCount; i++)
            s_ObstacleHits[i] = null;
    }
}
