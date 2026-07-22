using UnityEngine;

[CreateAssetMenu(fileName = "LuggageTuning", menuName = "Carry On/Tuning/Luggage Tuning")]
public class LuggageTuning : ScriptableObject
{
    [Header("Fragile")]
    [SerializeField, Min(0f)] private float fragileBreakThreshold = 300f;
    [SerializeField, Min(0f)] private float fragileGrabImmunity = 2f;

    [Header("Collision Audio")]
    [SerializeField, Min(0f)] private float minimumCollisionAudioSpeed = 1.5f;
    [SerializeField, Min(0f)] private float mediumCollisionAudioSpeed = 4f;
    [SerializeField, Min(0f)] private float hardCollisionAudioSpeed = 8f;
    [SerializeField, Min(0f)] private float collisionAudioCooldown = 0.15f;

    public float FragileBreakThreshold => fragileBreakThreshold;
    public float FragileGrabImmunity => fragileGrabImmunity;
    public float MinimumCollisionAudioSpeed => minimumCollisionAudioSpeed;
    public float MediumCollisionAudioSpeed => Mathf.Max(minimumCollisionAudioSpeed, mediumCollisionAudioSpeed);
    public float HardCollisionAudioSpeed => Mathf.Max(MediumCollisionAudioSpeed, hardCollisionAudioSpeed);
    public float CollisionAudioCooldown => collisionAudioCooldown;
}
