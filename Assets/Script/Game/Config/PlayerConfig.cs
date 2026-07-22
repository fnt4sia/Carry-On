using UnityEngine;

[CreateAssetMenu(fileName = "PlayerConfig", menuName = "Carry On/Tuning/Player Config")]
public class PlayerConfig : ScriptableObject
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float movementSpeedNormal = 10f;
    [SerializeField, Min(0f)] private float rotationSpeed = 4f;
    [SerializeField, Min(0f)] private float grabRotationSpeedMultiplier = 1f;
    [SerializeField, Range(0f, 1f)] private float lerpSpeed = 0.15f;

    [Header("Dash")]
    [SerializeField, Min(1f)] private float dashSpeedMultiplier = 2f;
    [SerializeField, Min(0f)] private float dashDuration = 0.15f;
    [SerializeField, Min(0f)] private float dashCooldown = 1f;

    public float MovementSpeedNormal => movementSpeedNormal;
    public float RotationSpeed => rotationSpeed;
    public float GrabRotationSpeedMultiplier => grabRotationSpeedMultiplier;
    public float LerpSpeed => lerpSpeed;
    public float DashSpeedMultiplier => dashSpeedMultiplier;
    public float DashDuration => dashDuration;
    public float DashCooldown => dashCooldown;
}
