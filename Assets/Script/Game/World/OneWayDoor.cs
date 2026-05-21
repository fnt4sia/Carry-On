using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Automatic one-way door. The trigger collider should sit only on the allowed
// approach side, so players coming from the blocked side cannot open it.
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
    [SerializeField] private Vector3 allowedEntryLocalDirection = Vector3.forward;
    [SerializeField, Min(0f)] private float allowedSideCenterOffset = 0.1f;

    private int playersInSensor;
    private int openParameterHash;
    private bool isOpen;
    private Coroutine closeRoutine;
    private readonly HashSet<Collider> acceptedPlayerColliders = new HashSet<Collider>();

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
}
