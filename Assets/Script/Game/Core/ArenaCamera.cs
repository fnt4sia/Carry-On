using UnityEngine;

// Fixed arena camera, Overcooked-style: the whole playfield is in frame at all times and the
// shot never follows anyone and never zooms. The pose is authored in the scene — this component
// reads it on Awake and only adds a slow drift on top, so a static frame reads as a held shot
// rather than a frozen image.
//
// Drift is deliberately tiny. If it is noticeable as camera movement it is too strong; it should
// only stop the picture from looking like a paused game. Set either amplitude to 0 to switch that
// half off, or disable the component for a dead-still shot.
[DisallowMultipleComponent]
public class ArenaCamera : MonoBehaviour
{
    [Header("Position drift")]
    [Tooltip("How far the camera wanders from its authored position, in world units.")]
    [SerializeField, Min(0f)] private float driftAmplitude = 0.35f;
    [Tooltip("Seconds for one pass of the drift. The vertical axis runs slightly slower, so the " +
             "path is a slow figure-of-eight instead of a straight bob.")]
    [SerializeField, Min(0.1f)] private float driftPeriod = 14f;

    [Header("Roll sway")]
    [Tooltip("Camera roll either side of the authored angle, in degrees.")]
    [SerializeField, Min(0f)] private float swayAmplitude = 0.18f;
    [SerializeField, Min(0.1f)] private float swayPeriod = 19f;

    private Vector3 basePosition;
    private Quaternion baseRotation;
    private Vector3 driftRight;
    private Vector3 driftUp;

    private void Awake()
    {
        basePosition = transform.position;
        baseRotation = transform.rotation;

        // Cached from the authored pose, not read per frame: the transform is being written every
        // LateUpdate, so live axes would feed the drift back into itself.
        driftRight = baseRotation * Vector3.right;
        driftUp = baseRotation * Vector3.up;
    }

    private void LateUpdate()
    {
        float time = Time.time;

        float horizontal = Mathf.Sin(time * Mathf.PI * 2f / driftPeriod);
        float vertical = Mathf.Sin(time * Mathf.PI * 2f / (driftPeriod * 1.37f));
        Vector3 drift = (driftRight * horizontal + driftUp * vertical) * driftAmplitude;

        float sway = Mathf.Sin(time * Mathf.PI * 2f / swayPeriod) * swayAmplitude;

        transform.SetPositionAndRotation(
            basePosition + drift,
            baseRotation * Quaternion.Euler(0f, 0f, sway));
    }
}
