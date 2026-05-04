using System.Collections;
using UnityEngine;

public abstract class MachineStation : MonoBehaviour
{
    [Header("Station")]
    [SerializeField] protected float processTime = 2f;
    [SerializeField] protected Transform slotTransform;

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

        if (slotTransform != null)
        {
            luggage.transform.position = slotTransform.position;
            luggage.transform.rotation = slotTransform.rotation;
        }

        luggage.ActiveConveyor = null;
        luggage.SetInStation(true);
        currentLuggage = luggage;
        StartCoroutine(ProcessRoutine(luggage));
        return true;
    }

    private IEnumerator ProcessRoutine(Luggage luggage)
    {
        isProcessing = true;
        yield return new WaitForSeconds(processTime);
        isProcessing = false;

        if (luggage != null)
        {
            OnProcessComplete(luggage);
            luggage.SetInStation(false);
        }
    }

    private void Update()
    {
        // Slot opens up once the processed luggage is grabbed back by a player
        if (currentLuggage == null) return;
        if (!isProcessing && currentLuggage.GetIsGrabbed())
            currentLuggage = null;
    }
}
