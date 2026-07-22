using UnityEngine;

[CreateAssetMenu(fileName = "StationTuning", menuName = "Carry On/Tuning/Station Tuning")]
public class StationTuning : ScriptableObject
{
    [SerializeField] private Vector3 obstacleCheckHalfExtents = new(1.5f, 1f, 1.5f);
    [SerializeField, Min(0f)] private float obstacleShoveImpulse = 5f;
    [SerializeField, Min(0f)] private float obstacleShoveUpImpulse = 1.5f;
    [SerializeField, Min(0f)] private float obstacleShoveRandomness = 0.65f;

    public Vector3 ObstacleCheckHalfExtents => new(
        Mathf.Max(0f, obstacleCheckHalfExtents.x),
        Mathf.Max(0f, obstacleCheckHalfExtents.y),
        Mathf.Max(0f, obstacleCheckHalfExtents.z));
    public float ObstacleShoveImpulse => obstacleShoveImpulse;
    public float ObstacleShoveUpImpulse => obstacleShoveUpImpulse;
    public float ObstacleShoveRandomness => obstacleShoveRandomness;
}
