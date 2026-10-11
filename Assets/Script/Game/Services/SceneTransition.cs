using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The scene-change transition: clouds sweep in from the top-left and the right, growing until
/// they cover the screen; a plane flies in from one corner and waits in the middle, bobbing,
/// while the next scene loads; once it has loaded, the plane carries on to the far corner and
/// the clouds part to show the new scene.
///
/// The SceneLoader prefab owns every piece. A cloud's authored pose is where it sits once the
/// screen is covered — rearrange the cover in the prefab — and each bank says where its clouds
/// come in from. One clock drives every piece, so nothing jumps between phases: the plane slows
/// to a stop in the middle, eases into its bob and pulls away again from wherever the bob has it.
/// The clock runs on unscaled time (a round ends at timeScale 0) with a capped step, so a scene's
/// activation hitch stalls the plane for a frame instead of teleporting it.
/// </summary>
[DisallowMultipleComponent]
public class SceneTransition : MonoBehaviour
{
    [Serializable]
    private struct CloudBank
    {
        [Tooltip("Parent of the clouds that come in from one side. Each child's authored pose is " +
                 "where it sits once the screen is covered.")]
        public RectTransform clouds;
        [Tooltip("Where this bank's clouds start, relative to their covering pose. They leave the same way.")]
        public Vector2 offset;
    }

    [Header("Clouds")]
    [SerializeField] private CloudBank[] banks;
    [Tooltip("Full-screen fill behind the clouds that closes any gap between them.")]
    [SerializeField] private Graphic sky;
    [Tooltip("Seconds one cloud takes to come in, and again to leave.")]
    [SerializeField, Min(0.05f)] private float cloudSeconds = 0.6f;
    [Tooltip("Delay between one cloud of a bank and the next, so a bank rolls in instead of " +
             "sliding as one block.")]
    [SerializeField, Min(0f)] private float cloudStagger = 0.05f;
    [Tooltip("A cloud's size where it starts, as a share of its covering size.")]
    [SerializeField, Range(0.1f, 1f)] private float enterScale = 0.5f;
    [Tooltip("A cloud's size as it leaves, as a multiple of its covering size: flying through them.")]
    [SerializeField, Min(1f)] private float exitScale = 1.6f;

    [Header("Plane")]
    [Tooltip("Moves along the flight path. Holds the body and its shadow.")]
    [SerializeField] private RectTransform plane;
    [SerializeField] private RectTransform planeBody;
    [SerializeField] private Graphic planeShadow;
    [Tooltip("Direction the nose points in the plane sprite, in degrees anticlockwise from screen-right.")]
    [SerializeField] private float spriteHeading = 133.5f;
    [Tooltip("Degrees below the horizontal of a forward flight, top-left to bottom-right. Going " +
             "back flies the same line the other way.")]
    [SerializeField] private float pathAngle = 35f;
    [Tooltip("Distance from the middle of the screen to where the plane starts and ends, off screen.")]
    [SerializeField, Min(0f)] private float pathDistance = 1500f;
    [SerializeField, Min(0.05f)] private float arriveSeconds = 0.9f;
    [SerializeField, Min(0.05f)] private float departSeconds = 0.7f;
    [Tooltip("The plane waits in the middle at least this long, however fast the scene loads.")]
    [SerializeField, Min(0f)] private float minHoverSeconds = 0.5f;
    [Tooltip("How long before the clouds finish covering the plane comes in.")]
    [SerializeField, Min(0f)] private float planeLead = 0.25f;
    [Tooltip("Seconds after the plane sets off again that the clouds start to part.")]
    [SerializeField, Min(0f)] private float revealDelay = 0.15f;

    [Header("Hover")]
    [SerializeField] private float bobHeight = 16f;
    [SerializeField] private float bankDegrees = 3f;
    [SerializeField, Min(0.1f)] private float bobSeconds = 1.6f;

    // A hitch longer than this (scene activation) slows the clock instead of skipping ahead.
    private const float MaxStep = 1f / 20f;

    private RectTransform[][] clouds;
    private Vector2[][] coverPositions;
    private Vector3[][] coverScales;
    private Color skyColor;
    private Color shadowColor;
    private Vector2 shadowOffset;
    private float coverSeconds;
    private bool cached;

    private bool forward;
    private bool released;
    private float clock;
    private float departAt;

    /// <summary>True from <see cref="Play"/> until the clouds have fully parted again.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>
    /// The clouds cover the screen and the plane has stopped in the middle, so the scene can be
    /// swapped underneath. Starting a load and activating a scene both stall the game for a
    /// moment, which only goes unnoticed while the plane bobs in place rather than flying in.
    /// </summary>
    public bool IsCovered => IsPlaying && clock >= Mathf.Max(coverSeconds, ArriveEnd);

    private float ArriveStart => coverSeconds - planeLead;
    private float ArriveEnd => ArriveStart + arriveSeconds;
    private float RevealAt => departAt + revealDelay;

    /// <summary>Starts covering the screen. Forward flies the plane top-left to bottom-right.</summary>
    public void Play(bool forward)
    {
        CacheAuthoredPoses();
        this.forward = forward;
        released = false;
        clock = 0f;
        departAt = float.PositiveInfinity;
        IsPlaying = true;
        Pose(clock);
    }

    /// <summary>The next scene is in: the plane leaves (after its minimum wait) and the clouds part.</summary>
    public void Release() => released = true;

    private void Update()
    {
        if (!IsPlaying)
            return;

        clock += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
        if (released && float.IsPositiveInfinity(departAt))
            departAt = Mathf.Max(clock, ArriveEnd + minHoverSeconds);

        Pose(clock);

        if (clock >= RevealAt + coverSeconds && clock >= departAt + departSeconds)
            IsPlaying = false;
    }

    // The prefab is authored covered, so the poses read here are the covering poses. Read once:
    // after a run every piece is left off screen.
    private void CacheAuthoredPoses()
    {
        if (cached)
            return;
        cached = true;

        clouds = new RectTransform[banks.Length][];
        coverPositions = new Vector2[banks.Length][];
        coverScales = new Vector3[banks.Length][];
        int mostClouds = 0;
        for (int b = 0; b < banks.Length; b++)
        {
            Transform bank = banks[b].clouds;
            int count = bank.childCount;
            clouds[b] = new RectTransform[count];
            coverPositions[b] = new Vector2[count];
            coverScales[b] = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var cloud = (RectTransform)bank.GetChild(i);
                clouds[b][i] = cloud;
                coverPositions[b][i] = cloud.anchoredPosition;
                coverScales[b][i] = cloud.localScale;
            }
            mostClouds = Mathf.Max(mostClouds, count);
        }
        coverSeconds = Mathf.Max(0, mostClouds - 1) * cloudStagger + cloudSeconds;

        skyColor = sky.color;
        shadowColor = planeShadow.color;
        shadowOffset = planeShadow.rectTransform.anchoredPosition;
    }

    private void Pose(float t)
    {
        // Clouds: in growing and slowing, out growing and speeding up, each bank the way it came.
        // A leaving cloud travels as much further as it grows, or the biggest would still be
        // poking onto the screen when the view switches off.
        for (int b = 0; b < clouds.Length; b++)
            for (int i = 0; i < clouds[b].Length; i++)
            {
                float delay = i * cloudStagger;
                float enter = EaseOut(Mathf.Clamp01((t - delay) / cloudSeconds));
                float leave = EaseIn(Mathf.Clamp01((t - RevealAt - delay) / cloudSeconds));
                clouds[b][i].anchoredPosition = coverPositions[b][i] + banks[b].offset * (1f - enter + leave * exitScale);
                clouds[b][i].localScale = coverScales[b][i]
                    * (Mathf.Lerp(enterScale, 1f, enter) * Mathf.Lerp(1f, exitScale, leave));
            }

        // The sky fills in under the last clouds as they settle, and clears the moment they part,
        // so the new scene shows through the gaps first.
        float half = cloudSeconds * 0.5f;
        float skyAlpha = Smooth01((t - (coverSeconds - half)) / half) * (1f - Smooth01((t - RevealAt) / half));
        sky.color = WithAlpha(skyColor, skyColor.a * skyAlpha);

        // Plane: decelerates into the middle, accelerates out. Zero speed at both ends of the
        // wait, so arriving, waiting and leaving join without a kink.
        float heading = forward ? -pathAngle : 180f - pathAngle;
        Vector2 dir = new(Mathf.Cos(heading * Mathf.Deg2Rad), Mathf.Sin(heading * Mathf.Deg2Rad));
        float arrive = EaseOut(Mathf.Clamp01((t - ArriveStart) / arriveSeconds));
        float depart = EaseIn(Mathf.Clamp01((t - departAt) / departSeconds));

        // The bob grows in over the approach rather than starting at full swing.
        float hover = Smooth01((t - ArriveStart) / arriveSeconds);
        float phase = (t - ArriveStart) * (2f * Mathf.PI / bobSeconds);
        float lift = hover * Mathf.Sin(phase);
        plane.anchoredPosition = dir * ((arrive - 1f + depart) * pathDistance) + Vector2.up * (bobHeight * lift);

        Quaternion rotation = Quaternion.Euler(0f, 0f, heading - spriteHeading + bankDegrees * hover * Mathf.Cos(phase));
        planeBody.localRotation = rotation;
        planeShadow.rectTransform.localRotation = rotation;
        // The shadow drops away as the plane rises, which is what makes the bob read as height.
        planeShadow.rectTransform.anchoredPosition = shadowOffset * (1f + 0.25f * lift);
        planeShadow.color = WithAlpha(shadowColor, shadowColor.a * skyAlpha);
    }

    private static float EaseOut(float k) => 1f - (1f - k) * (1f - k) * (1f - k);
    private static float EaseIn(float k) => k * k * k;

    private static float Smooth01(float k)
    {
        k = Mathf.Clamp01(k);
        return k * k * (3f - 2f * k);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
