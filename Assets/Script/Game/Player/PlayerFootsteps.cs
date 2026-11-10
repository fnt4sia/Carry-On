using UnityEngine;

// This character's footsteps, landed on the jog animation itself. The jog loop is watched and a
// step plays each time it passes a frame where a foot touches down, so steps keep pace with the
// legs — Scannor's 24 fps jog steps slower, like his feet do — and stop the moment the legs do:
// idle, dash, or falling.
public class PlayerFootsteps : MonoBehaviour
{
    [Tooltip("Row name on the AudioManager prefab. Each character has its own.")]
    [SerializeField] private string stepSound;
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody body;
    [Tooltip("Where in the jog loop (0-1) each foot lands. Measured on AN_2-jog, the same poses on " +
             "all four characters: left foot down at frame 0, right at frame 8 of 17.")]
    [SerializeField] private float[] footfalls = { 0f, 0.47f };
    [Tooltip("Falling faster than this (m/s) counts as airborne: the legs land nothing.")]
    [SerializeField, Min(0f)] private float airborneFallSpeed = 2f;

    // The jog's normalizedTime last frame; below zero while not jogging.
    private float lastJogTime = -1f;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (body == null) body = GetComponent<Rigidbody>();
    }

    private void OnEnable() => lastJogTime = -1f;

    private void Update()
    {
        if (animator == null)
            return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool airborne = body != null && body.linearVelocity.y < -airborneFallSpeed;
        if (state.shortNameHash != AnimId.MoveState || airborne)
        {
            lastJogTime = -1f;
            return;
        }

        // The first jogging frame only takes a reading, so tapping the stick doesn't stomp.
        float now = state.normalizedTime;
        if (lastJogTime >= 0f)
        {
            foreach (float footfall in footfalls)
            {
                if (Crossed(lastJogTime, now, footfall))
                {
                    AudioManager.Instance?.PlaySFX(stepSound);
                    break;
                }
            }
        }

        lastJogTime = now;
    }

    // normalizedTime keeps counting up through the loops (2.3 = third loop, 30% in), so a
    // footfall at phase f is crossed when its next occurrence after `last` is at or before `now`.
    private static bool Crossed(float last, float now, float footfall)
    {
        float next = Mathf.Floor(last) + footfall;
        if (next <= last)
            next += 1f;
        return next <= now;
    }
}
