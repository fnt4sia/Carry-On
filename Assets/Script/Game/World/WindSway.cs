using UnityEngine;

// Ambient wind for hanging decoration. Swings the object about its own pivot — for the
// hanging-plant mesh that pivot is the ceiling attachment, so the pot arcs like a pendulum.
//
// Pure set dressing: no physics, no colliders, no dependencies. The authored localRotation
// is captured at Awake and composed onto, never replaced, so the FBX's imported orientation
// survives. Swing axes live in parent space, so the arc reads as vertical no matter how the
// mesh itself is rotated.
public class WindSway : MonoBehaviour
{
    [Header("Strength")]
    [SerializeField, Tooltip("Swing angle along the wind, in degrees. Gusting scales this by 1 +/- gustStrength, so the true peak is swayAngle * (1 + gustStrength).")]
    private float swayAngle = 5f;
    [SerializeField, Tooltip("Peak swing angle across the wind. Keep it smaller than swayAngle or the arc stops reading as wind.")]
    private float crossSwayAngle = 2f;

    [Header("Speed")]
    [SerializeField, Min(0.01f), Tooltip("Swings per second.")]
    private float swaySpeed = 0.45f;
    [SerializeField, Tooltip("Direction the wind pushes, in degrees around Y. 0 = world +Z.")]
    private float windDirection;

    [Header("Randomness")]
    [SerializeField, Range(0f, 1f), Tooltip("0 = every instance swings in lockstep. 1 = each starts anywhere in its cycle and runs at its own rate.")]
    private float randomness = 1f;
    [SerializeField, Range(0f, 1f), Tooltip("How much slow gusting swells and stills the swing. 0 = a metronome.")]
    private float gustStrength = 0.6f;
    [SerializeField, Min(0.01f), Tooltip("Gusts per second. Should be much slower than swaySpeed.")]
    private float gustSpeed = 0.12f;

    private Quaternion restRotation;
    private float phase;
    private float crossPhase;
    private float rateScale = 1f;
    private float gustSeed;

    private void Awake()
    {
        restRotation = transform.localRotation;

        // Seeded off the instance so a row of identical prefabs never swings as one block.
        // Borrow and restore the global state instead of leaving it reseeded.
        Random.State state = Random.state;
        Random.InitState(GetInstanceID());
        phase = Random.value * Mathf.PI * 2f * randomness;
        crossPhase = Random.value * Mathf.PI * 2f * randomness;
        rateScale = 1f + Random.Range(-0.35f, 0.35f) * randomness;
        gustSeed = Random.value * 100f;
        Random.state = state;
    }

    private void Update()
    {
        float t = Time.time * swaySpeed * rateScale * Mathf.PI * 2f;

        // Perlin gust swells and stills the swing so it never ticks forever at one size.
        // The noise averages 0.5, so doubling it keeps the mean amplitude at 1.
        float gust = Mathf.Lerp(1f, Mathf.PerlinNoise(gustSeed, Time.time * gustSpeed) * 2f, gustStrength);

        float along = Mathf.Sin(t + phase) * swayAngle * gust;

        // Deliberately not a whole multiple of the main rate: the two never resync, so the
        // pot traces a wandering ellipse instead of a flat back-and-forth.
        float across = Mathf.Sin(t * 0.63f + crossPhase) * crossSwayAngle * gust;

        Quaternion wind = Quaternion.Euler(0f, windDirection, 0f);
        transform.localRotation =
            Quaternion.AngleAxis(along, wind * Vector3.right)
            * Quaternion.AngleAxis(across, wind * Vector3.forward)
            * restRotation;
    }
}
