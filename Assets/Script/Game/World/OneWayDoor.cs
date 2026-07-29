using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Automatic one-way door. The trigger spans both sides of the doorway, but only
// players standing on the allowed side can open it — anyone approaching from the
// blocked side is ignored and the leaves stay shut in their face.
[DisallowMultipleComponent]
public class OneWayDoor : MonoBehaviour
{
    [Header("Door State")]
    [SerializeField] private bool startOpen;
    [SerializeField, Min(0f)] private float closeDelay = 0.45f;

    [Header("Components")]
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private string openParameter = "IsOpen";

    [Header("One-Way Check")]
    // Local-space direction of the side players are allowed to open from.
    [SerializeField] private Vector3 allowedEntryLocalDirection = Vector3.forward;
    // How far past the door plane a player must stand before they count as
    // being on the allowed side. Keep this bigger than half the leaf thickness.
    [SerializeField, Min(0f)] private float allowedSideCenterOffset = 0.1f;

    private int playersInSensor;
    private int openParameterHash;
    private bool isOpen;
    private Coroutine closeRoutine;
    private readonly HashSet<Collider> acceptedPlayerColliders = new HashSet<Collider>();

    public bool IsOpen => isOpen;

    private void Reset()
    {
        doorAnimator = GetComponent<Animator>();

        BoxCollider trigger = GetComponent<BoxCollider>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void Awake()
    {
        if (doorAnimator == null)
            doorAnimator = GetComponent<Animator>();

        openParameterHash = Animator.StringToHash(openParameter);
        SetOpen(startOpen);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryAccept(other);
    }

    // A fast player can already be past the door plane on the frame they enter the
    // trigger, so keep re-testing anyone we have not accepted yet. Someone parked on
    // the blocked side simply never passes the test.
    private void OnTriggerStay(Collider other)
    {
        if (acceptedPlayerColliders.Contains(other))
            return;

        TryAccept(other);
    }

    private void TryAccept(Collider other)
    {
        if (!IsPlayer(other))
            return;

        if (!IsOnAllowedSide(other))
            return;

        if (!acceptedPlayerColliders.Add(other))
            return;

        playersInSensor = acceptedPlayerColliders.Count;
        Open();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
            return;

        if (!acceptedPlayerColliders.Remove(other))
            return;

        playersInSensor = acceptedPlayerColliders.Count;
        if (playersInSensor == 0)
            ScheduleClose();
    }

    public void Open()
    {
        if (closeRoutine != null)
        {
            StopCoroutine(closeRoutine);
            closeRoutine = null;
        }

        SetOpen(true);
    }

    public void Close()
    {
        acceptedPlayerColliders.Clear();
        playersInSensor = 0;
        SetOpen(false);
    }

    private void ScheduleClose()
    {
        if (closeRoutine != null)
            StopCoroutine(closeRoutine);

        closeRoutine = StartCoroutine(CloseAfterDelay());
    }

    private IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSeconds(closeDelay);
        closeRoutine = null;

        if (playersInSensor == 0)
            SetOpen(false);
    }

    private void SetOpen(bool value)
    {
        isOpen = value;
        if (doorAnimator != null)
            doorAnimator.SetBool(openParameterHash, isOpen);
    }

    private static bool IsPlayer(Collider other)
    {
        return other.CompareTag("Player")
            || other.GetComponentInParent<PlayerGrab>() != null;
    }

    private bool IsOnAllowedSide(Collider other)
    {
        Vector3 localDirection = allowedEntryLocalDirection.sqrMagnitude > 0f
            ? allowedEntryLocalDirection.normalized
            : Vector3.forward;

        Vector3 playerPosition = GetPlayerReferencePosition(other);
        Vector3 localOffset = transform.InverseTransformPoint(playerPosition);
        return Vector3.Dot(localOffset, localDirection) >= allowedSideCenterOffset;
    }

    private static Vector3 GetPlayerReferencePosition(Collider other)
    {
        Rigidbody attachedRigidbody = other.attachedRigidbody;
        if (attachedRigidbody != null)
            return attachedRigidbody.position;

        PlayerGrab playerGrab = other.GetComponentInParent<PlayerGrab>();
        if (playerGrab != null)
            return playerGrab.transform.position;

        return other.transform.position;
    }

#if UNITY_EDITOR
    // Green arrow marks the side players may open from; red marks the blocked side.
    private void OnDrawGizmosSelected()
    {
        Vector3 localDirection = allowedEntryLocalDirection.sqrMagnitude > 0f
            ? allowedEntryLocalDirection.normalized
            : Vector3.forward;

        BoxCollider trigger = GetComponent<BoxCollider>();
        float reach = trigger != null
            ? Vector3.Scale(trigger.size, localDirection).magnitude * 0.5f
            : 2f;

        Vector3 origin = transform.TransformPoint(trigger != null ? trigger.center : Vector3.zero);
        Vector3 worldDirection = transform.TransformDirection(localDirection).normalized;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin, origin + worldDirection * reach);
        Gizmos.DrawSphere(origin + worldDirection * reach, 0.25f);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, origin - worldDirection * reach);
    }
#endif
}
