using System.Collections.Generic;
using UnityEngine;

// Rotates a platform body and carries players/luggage standing inside its rider zone.
// The rider zone is a child trigger collider under the same Rigidbody.
// Pressure plates can call ReverseDirection to flip clockwise/counter-clockwise.
[DisallowMultipleComponent]
public class RotatingPlatform : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] private Transform rotatingBody;
    [SerializeField, Min(0f)] private float rotationSpeed = 35f;
    [SerializeField] private bool clockwise = true;

    [Header("Pressure Plates")]
    [SerializeField] private List<RotatingPlatformPressurePlate> pressurePlates = new List<RotatingPlatformPressurePlate>();

    [Header("Riders")]
    [SerializeField] private bool carryRiders = true;
    [SerializeField] private bool rotateRiders = true;

    private readonly Dictionary<Transform, RiderState> riders = new Dictionary<Transform, RiderState>();
    private Rigidbody platformRigidbody;

    private sealed class RiderState
    {
        public RiderState(Transform transform, Rigidbody rigidbody)
        {
            Transform = transform;
            Rigidbody = rigidbody;
        }

        public Transform Transform { get; }
        public Rigidbody Rigidbody { get; }
        public int ContactCount { get; set; }
    }

    private void Reset()
    {
        rotatingBody = transform;
    }

    private void Awake()
    {
        if (rotatingBody == null)
            rotatingBody = transform;

        if (rotatingBody == transform)
            platformRigidbody = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        AssignPressurePlates();
    }

    private void Start()
    {
        AssignPressurePlates();
    }

    private void FixedUpdate()
    {
        if (rotatingBody == null || Mathf.Approximately(rotationSpeed, 0f))
            return;

        float direction = clockwise ? 1f : -1f;
        float angle = rotationSpeed * direction * Time.fixedDeltaTime;
        Quaternion rotationDelta = Quaternion.AngleAxis(angle, Vector3.up);
        Vector3 pivot = rotatingBody.position;

        Quaternion nextRotation = rotationDelta * rotatingBody.rotation;

        if (platformRigidbody != null)
            platformRigidbody.MoveRotation(nextRotation);
        else
            rotatingBody.rotation = nextRotation;

        if (carryRiders)
            MoveRiders(pivot, rotationDelta);
    }

    public void ReverseDirection()
    {
        clockwise = !clockwise;
    }

    public void SetClockwise(bool value)
    {
        clockwise = value;
    }

    public void AddPressurePlate(RotatingPlatformPressurePlate pressurePlate)
    {
        if (pressurePlate == null)
            return;

        if (!pressurePlates.Contains(pressurePlate))
            pressurePlates.Add(pressurePlate);

        pressurePlate.SetConnectedPlatform(this);
    }

    private void AssignPressurePlates()
    {
        for (int i = pressurePlates.Count - 1; i >= 0; i--)
        {
            RotatingPlatformPressurePlate pressurePlate = pressurePlates[i];
            if (pressurePlate == null)
                continue;

            pressurePlate.SetConnectedPlatform(this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterRider(other);
    }

    private void OnTriggerExit(Collider other)
    {
        UnregisterRider(other);
    }

    private void RegisterRider(Collider other)
    {
        if (!carryRiders || !IsCarryable(other))
            return;

        Transform riderTransform = ResolveRiderTransform(other, out Rigidbody riderRigidbody);
        if (riderTransform == null || rotatingBody == null)
            return;

        if (riderTransform == rotatingBody || riderTransform.IsChildOf(rotatingBody))
            return;

        if (!riders.TryGetValue(riderTransform, out RiderState state))
        {
            state = new RiderState(riderTransform, riderRigidbody);
            riders.Add(riderTransform, state);
        }

        state.ContactCount++;
    }

    private void UnregisterRider(Collider other)
    {
        Transform riderTransform = ResolveRiderTransform(other, out _);
        if (riderTransform == null)
            return;

        if (!riders.TryGetValue(riderTransform, out RiderState state))
            return;

        state.ContactCount = Mathf.Max(0, state.ContactCount - 1);
        if (state.ContactCount == 0)
            riders.Remove(riderTransform);
    }

    private void MoveRiders(Vector3 pivot, Quaternion rotationDelta)
    {
        if (riders.Count == 0)
            return;

        s_StaleRiders.Clear();

        foreach (KeyValuePair<Transform, RiderState> pair in riders)
        {
            RiderState state = pair.Value;
            Transform riderTransform = state.Transform;

            if (riderTransform == null || !riderTransform.gameObject.activeInHierarchy)
            {
                s_StaleRiders.Add(pair.Key);
                continue;
            }

            Rigidbody riderRigidbody = state.Rigidbody;
            Vector3 currentPosition = riderRigidbody != null ? riderRigidbody.position : riderTransform.position;
            Vector3 nextPosition = pivot + rotationDelta * (currentPosition - pivot);

            if (riderRigidbody != null)
            {
                riderRigidbody.MovePosition(nextPosition);

                if (rotateRiders)
                    riderRigidbody.MoveRotation(rotationDelta * riderRigidbody.rotation);
            }
            else
            {
                riderTransform.position = nextPosition;

                if (rotateRiders)
                    riderTransform.rotation = rotationDelta * riderTransform.rotation;
            }
        }

        for (int i = 0; i < s_StaleRiders.Count; i++)
            riders.Remove(s_StaleRiders[i]);
    }

    private static readonly List<Transform> s_StaleRiders = new List<Transform>();

    private static bool IsCarryable(Collider other)
    {
        return other.CompareTag("Player")
            || other.CompareTag("Luggage")
            || other.GetComponentInParent<PlayerMovement>() != null
            || other.GetComponentInParent<Luggage>() != null;
    }

    private static Transform ResolveRiderTransform(Collider other, out Rigidbody riderRigidbody)
    {
        riderRigidbody = other.attachedRigidbody;
        if (riderRigidbody == null)
            riderRigidbody = other.GetComponentInParent<Rigidbody>();

        if (riderRigidbody != null)
            return riderRigidbody.transform;

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player != null)
            return player.transform;

        Luggage luggage = other.GetComponentInParent<Luggage>();
        if (luggage != null)
            return luggage.transform;

        return null;
    }
}
