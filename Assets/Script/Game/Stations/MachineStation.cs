using UnityEngine;

public abstract class MachineStation : MonoBehaviour
{
    [Header("Station")]
    [Tooltip("Where the luggage snaps to when first placed (should sit on the slider's receive end).")]
    [SerializeField] protected Transform snapTransform;
    [Tooltip("The slider GameObject — luggage is parented here so it rides the animation.")]
    [SerializeField] protected Transform sliderTransform;
    [SerializeField] protected Animator machineAnimator;

    protected Luggage currentLuggage;
    protected bool isProcessing;

    public bool IsOccupied => currentLuggage != null;
    public bool IsProcessing => isProcessing;

    public abstract bool CanAccept(Luggage luggage);
    protected abstract void OnProcessComplete(Luggage luggage);

    public bool TryPlace(Luggage luggage)
    {
        if (IsOccupied || luggage == null || !CanAccept(luggage)) return false;

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

        Transform parent = sliderTransform != null ? sliderTransform : transform;
        luggage.transform.SetParent(parent, worldPositionStays: true);

        luggage.ActiveConveyor = null;
        luggage.SetInStation(true);
        currentLuggage = luggage;
        isProcessing = true;

        machineAnimator.SetBool("isTriggered", true);
        return true;
    }

    // Animation Event — place on the last frame of "door closing" (luggage is sealed inside)
    // This is also the right time to update the luggage's behavior and material
    public void AnimEvent_OnDoorClosed()
    {
        if (currentLuggage == null) return;
        OnProcessComplete(currentLuggage);  // calls MarkWashed/MarkWrapped → ApplyBehaviorVisual
    }

    // Animation Event — place on the last frame of "washing slider pushing" (luggage fully pushed out)
    public void AnimEvent_OnOutputComplete()
    {
        if (currentLuggage == null) return;

        currentLuggage.transform.SetParent(null);

        Rigidbody rb = currentLuggage.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = false;

        currentLuggage.SetInStation(false);
        isProcessing = false;

        machineAnimator.SetBool("isTriggered", false);
    }

    private void Update()
    {
        if (currentLuggage == null) return;
        if (!isProcessing && currentLuggage.GetIsGrabbed())
            currentLuggage = null;
    }
}
