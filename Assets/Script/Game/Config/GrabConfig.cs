using UnityEngine;

[CreateAssetMenu(fileName = "GrabConfig", menuName = "Carry On/Tuning/Grab Config")]
public class GrabConfig : ScriptableObject
{
    [Header("Detection and Alignment")]
    [SerializeField, Min(0f)] private float grabRadius = 1.4f;
    [SerializeField, Min(0f)] private float grabAlignDuration = 1.25f;

    [Header("Throw")]
    [SerializeField, Min(0f)] private float throwMinHoldTime = 0.25f;
    [SerializeField, Min(0f)] private float throwMaxHoldTime = 1.5f;
    [SerializeField, Min(0f)] private float throwMinForce = 200f;
    [SerializeField, Min(0f)] private float throwMaxForce = 800f;
    [SerializeField, Min(0f)] private float throwMinUpForce = 100f;
    [SerializeField, Min(0f)] private float throwMaxUpForce = 400f;

    [Header("Bridge Collider")]
    [SerializeField, Min(0f)] private float bridgeWidth = 2f;
    [SerializeField, Min(0f)] private float bridgeHeight = 2f;
    [SerializeField] private float bridgeYOffset = 1f;
    [SerializeField, Min(0f)] private float bridgeExtraZ = 1f;

    [Header("Joint Limits")]
    [SerializeField, Min(0f)] private float linearLimit = 0.05f;
    [SerializeField, Min(0f)] private float linearLimitSpring = 500f;
    [SerializeField, Min(0f)] private float linearLimitDamper = 1000f;

    [Header("Joint Drives")]
    [SerializeField, Min(0f)] private float positionSpring = 3000f;
    [SerializeField, Min(0f)] private float positionDamper = 200f;
    [SerializeField, Min(0f)] private float positionMaximumForce = 5000f;
    [SerializeField, Min(0f)] private float angularSpring = 1000f;
    [SerializeField, Min(0f)] private float angularDamper = 100f;
    [SerializeField, Min(0f)] private float angularMaximumForce = 4000f;

    [Header("Joint Projection and Break")]
    [SerializeField, Min(0f)] private float projectionDistance = 0.05f;
    [SerializeField, Min(0f)] private float projectionAngle = 5f;
    [SerializeField, Min(0f)] private float breakForce = 10000f;
    [SerializeField, Min(0f)] private float breakTorque = 2500f;

    public float GrabRadius => grabRadius;
    public float GrabAlignDuration => grabAlignDuration;
    public float ThrowMinHoldTime => throwMinHoldTime;
    public float ThrowMaxHoldTime => Mathf.Max(throwMinHoldTime, throwMaxHoldTime);
    public float ThrowMinForce => throwMinForce;
    public float ThrowMaxForce => Mathf.Max(throwMinForce, throwMaxForce);
    public float ThrowMinUpForce => throwMinUpForce;
    public float ThrowMaxUpForce => Mathf.Max(throwMinUpForce, throwMaxUpForce);
    public float BridgeWidth => bridgeWidth;
    public float BridgeHeight => bridgeHeight;
    public float BridgeYOffset => bridgeYOffset;
    public float BridgeExtraZ => bridgeExtraZ;
    public float LinearLimit => linearLimit;
    public float LinearLimitSpring => linearLimitSpring;
    public float LinearLimitDamper => linearLimitDamper;
    public float PositionSpring => positionSpring;
    public float PositionDamper => positionDamper;
    public float PositionMaximumForce => positionMaximumForce;
    public float AngularSpring => angularSpring;
    public float AngularDamper => angularDamper;
    public float AngularMaximumForce => angularMaximumForce;
    public float ProjectionDistance => projectionDistance;
    public float ProjectionAngle => projectionAngle;
    public float BreakForce => breakForce;
    public float BreakTorque => breakTorque;
}
