using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Follow camera for the flight-manifest levels: the shot tracks the players' midpoint and pulls
// back along its own view axis when they spread out. This is the Moving Out-style rig, as opposed
// to ArenaCamera's held Overcooked-style frame — put one of the two on a level's Main Camera, not
// both (they each write transform.position in LateUpdate and would fight).
//
// It never writes rotation. The authored angle IS the shot, and two other systems read it as a
// fixed basis: PlayerMovement builds its movement axes from the camera's forward/right, and the
// world-space UI (gate manifest board, station progress bars) is authored to face that same angle.
// Rotating this camera at runtime would turn the controls under the player's thumb and skew the UI.
//
// This is deliberately a separate component from MultiplayerCamera rather than a retune of it:
// that one is still live in ChooseStage (and the archived scenes), and its (offset, 0, offset)
// pull-back only makes sense at the 45-degree yaw those scenes use.
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class ArenaFollowCamera : MonoBehaviour
{
    [Header("Framing")]
    [Tooltip("World-unit gap kept between the outermost player and the edge of the frame.")]
    [SerializeField, Min(0f)] private float framingMargin = 7f;
    [Tooltip("Closest the camera is allowed to sit, measured along its own view axis. The arena " +
             "is sparse and the billboard props are tall, so coming nearer than this fills the " +
             "frame with empty floor and lets props cut across the shot.")]
    [SerializeField, Min(1f)] private float minDistance = 22f;
    [Tooltip("Furthest the camera pulls back. Sized to hold four players across the whole " +
             "belt-to-gate run; past this a straggler is allowed off-screen rather than zooming " +
             "the level into a postage stamp.")]
    [SerializeField, Min(1f)] private float maxDistance = 34f;

    [Header("Damping")]
    [Tooltip("How lazily the shot slides sideways to follow the group.")]
    [SerializeField, Min(0f)] private float panSmoothTime = 0.35f;
    [Tooltip("Pulling back is urgent — someone is about to leave the frame — so it is quick. " +
             "Coming back in is slow on purpose: the level is a shuttle between the belt and the " +
             "gate, and a zoom that tracked that honestly would breathe in and out about every " +
             "four seconds, which is squarely in the band that makes people queasy.")]
    [SerializeField, Min(0f)] private float zoomOutSmoothTime = 0.25f;
    [SerializeField, Min(0f)] private float zoomInSmoothTime = 1.2f;

    [Header("Stand-ins")]
    [Tooltip("Objects the camera frames as if they were players. The Clone marker in the scene " +
             "goes here, so the follow shot can be judged at two- or three-player spread without " +
             "that many controllers plugged in. Deactivate the marker to get the solo shot back.")]
    [SerializeField] private Transform[] extraTargets;

    // Roster of everyone who joined, and the subset that is on screen right now. The pool hazard
    // deactivates a player for the length of its respawn, so the two are not always the same, and
    // a deactivated player must not drag the framing to the spot where they drowned.
    private readonly List<Transform> roster = new();
    private readonly List<Transform> framed = new();

    private Camera cam;
    private Vector3 pivot;
    private Vector3 pivotVelocity;
    private float distance;
    private float distanceVelocity;
    private bool hasFramedOnce;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        distance = minDistance;
    }

    private void LateUpdate()
    {
        roster.RemoveAll(player => player == null);
        if (roster.Count == 0)
            FindPlayers();

        framed.Clear();
        foreach (Transform player in roster)
            if (player.gameObject.activeInHierarchy)
                framed.Add(player);

        // Stand-ins count exactly like a joined player, so one Clone in the scene reproduces the
        // two-player framing. Same activeInHierarchy test, which is what makes toggling it a
        // one-click A/B between the solo and the spread shot.
        if (extraTargets != null)
            foreach (Transform extra in extraTargets)
                if (extra != null && extra.gameObject.activeInHierarchy)
                    framed.Add(extra);

        // Nobody to frame yet (or everyone is mid-respawn) — hold the shot where it is.
        if (framed.Count == 0)
            return;

        Vector3 targetPivot = GetPivot();
        float targetDistance = Mathf.Clamp(GetFitDistance(targetPivot), minDistance, maxDistance);

        if (!hasFramedOnce)
        {
            // Frame correctly on the very first tick. The round opens on a three-second countdown
            // at timeScale 0, so without this the camera would sit at its authored pose through
            // the countdown and then swoop into place the instant the round starts.
            hasFramedOnce = true;
            pivot = targetPivot;
            distance = targetDistance;
            pivotVelocity = Vector3.zero;
            distanceVelocity = 0f;
        }
        else
        {
            // Unscaled: the countdown freezes gameplay, and the shot should settle during it.
            float deltaTime = Time.unscaledDeltaTime;
            float zoomSmoothTime = targetDistance > distance ? zoomOutSmoothTime : zoomInSmoothTime;

            pivot = Vector3.SmoothDamp(pivot, targetPivot, ref pivotVelocity, panSmoothTime,
                Mathf.Infinity, deltaTime);
            distance = Mathf.SmoothDamp(distance, targetDistance, ref distanceVelocity,
                zoomSmoothTime, Mathf.Infinity, deltaTime);
        }

        transform.position = pivot - transform.forward * distance;
    }

    private void FindPlayers()
    {
        var joined = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        foreach (PlayerInput player in joined)
            roster.Add(player.transform);
    }

    /// <summary>
    /// Centre of the group, flattened onto one height. Players stand on the raised gate platform
    /// as often as on the floor, and letting that height difference into the framing would pull
    /// the camera back for a step nobody took.
    /// </summary>
    private Vector3 GetPivot()
    {
        Vector3 min = framed[0].position;
        Vector3 max = min;
        for (int i = 1; i < framed.Count; i++)
        {
            Vector3 position = framed[i].position;
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
        }

        return new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);
    }

    /// <summary>
    /// How far back the camera has to sit for every player to clear the frame edges by
    /// <see cref="framingMargin"/>. Solved against the real frustum so the shot holds up on any
    /// aspect ratio instead of being tuned to one screen.
    /// </summary>
    private float GetFitDistance(Vector3 target)
    {
        float tanVertical = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanHorizontal = tanVertical * cam.aspect;
        Quaternion toCameraSpace = Quaternion.Inverse(transform.rotation);
        float required = 0f;

        foreach (Transform player in framed)
        {
            // Flattened to the pivot height for the same reason GetPivot flattens.
            Vector3 flattened = new(player.position.x, target.y, player.position.z);
            Vector3 offset = toCameraSpace * (flattened - target);

            required = Mathf.Max(required, (Mathf.Abs(offset.x) + framingMargin) / tanHorizontal - offset.z);
            required = Mathf.Max(required, (Mathf.Abs(offset.y) + framingMargin) / tanVertical - offset.z);
        }

        return required;
    }
}
