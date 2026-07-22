using UnityEngine;

[CreateAssetMenu(fileName = "ConveyorTuning", menuName = "Carry On/Tuning/Conveyor Tuning")]
public class ConveyorTuning : ScriptableObject
{
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [Tooltip("How fast luggage velocity bends toward the belt direction (m/s²).")]
    [SerializeField, Min(0f)] private float acceleration = 18f;
    [Tooltip("How fast cargo is rotated upright and aligned with the belt direction (1/s).")]
    [SerializeField, Min(0f)] private float uprightGain = 4f;
    [Tooltip("How strongly turn cargo is pulled back to the centreline (1/s).")]
    [SerializeField, Min(0f)] private float centeringGain = 1.5f;
    [SerializeField, Min(0f)] private float centerRadius = 3f;
    [SerializeField, Min(0f)] private float surfaceCheckMargin = 0.25f;

    public float MoveSpeed => moveSpeed;
    public float Acceleration => acceleration;
    public float UprightGain => uprightGain;
    public float CenteringGain => centeringGain;
    public float CenterRadius => centerRadius;
    public float SurfaceCheckMargin => surfaceCheckMargin;
}
