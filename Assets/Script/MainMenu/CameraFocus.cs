using UnityEngine;

// Swings the lobby camera between its authored pose and a close-up on the menu banner, so
// opening a panel reads as leaning in to the sign rather than a hard cut.
//
// The home pose is whatever the camera was authored at — captured at Awake, never typed in
// twice. The close-up is an empty Transform in the scene (MenuFocusPose): move that object to
// re-frame the zoom, no code change.
//
// State is one 0..1 blend rather than a tween to a destination. Interrupting a move part-way
// just reverses it from where it is, and the camera can never drift off its two poses.
public class CameraFocus : MonoBehaviour
{
    [SerializeField, Tooltip("Empty Transform holding the zoomed-in pose. Position and rotation are both used.")]
    private Transform focusPose;

    [SerializeField, Min(0.01f), Tooltip("Seconds for a full trip between the two poses.")]
    private float travelTime = 0.55f;

    private Vector3 homePosition;
    private Quaternion homeRotation;
    private float blend;
    private bool focused;

    public bool IsFocused => focused;

    private void Awake()
    {
        homePosition = transform.position;
        homeRotation = transform.rotation;
    }

    public void SetFocused(bool value) => focused = value;

    private void LateUpdate()
    {
        if (focusPose == null) return;

        float target = focused ? 1f : 0f;
        if (Mathf.Approximately(blend, target)) return;

        // Unscaled: the lobby is allowed to sit at timeScale 0 and the move must still play.
        blend = Mathf.MoveTowards(blend, target, Time.unscaledDeltaTime / travelTime);

        float eased = Mathf.SmoothStep(0f, 1f, blend);
        transform.SetPositionAndRotation(
            Vector3.Lerp(homePosition, focusPose.position, eased),
            Quaternion.Slerp(homeRotation, focusPose.rotation, eased));
    }
}
