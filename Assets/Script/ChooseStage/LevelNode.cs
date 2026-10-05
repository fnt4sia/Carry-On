using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stage-select presentation for one LevelConfig. Progression data belongs to the
/// save service; this component only reflects it in the map scene.
///
/// The landing ring on the sea shows where the stage stands: yellow once completed, white and
/// pulsing while it's the one to play next, grey while it's still out of reach. The ring is also
/// the stage's area — a plane inside it is on this stage — so moving or scaling the ring in the
/// prefab moves that area with it.
///
/// The badge over the island reads like Overcooked's: best stars while nobody is parked
/// here, one slot per player once someone is, and a border that fills while every plane
/// waits on it. <see cref="MapController"/> decides all of that; this only draws it. A locked
/// stage has no badge, and planes fly straight over it. The prefab owns every element —
/// nothing here is built at runtime.
/// </summary>
public class LevelNode : MonoBehaviour
{
    [SerializeField] private LevelConfig level;

    [Header("Landing ring")]
    [Tooltip("Canvas on the sea around the island. Its centre and size are the area a plane " +
             "counts as on this stage — move and scale it to fit the island.")]
    [SerializeField] private RectTransform landingRing;
    [SerializeField] private Image ringImage;
    [Tooltip("Scaled up and down while this is the stage to play next. Holds the ring and its fill.")]
    [SerializeField] private Transform ringPulse;
    [Tooltip("Filled image on the landing ring; fills with the badge border while boarding.")]
    [SerializeField] private Image ringFill;
    [SerializeField] private Color completedRingColor = new(1f, 0.82f, 0.2f);
    [SerializeField] private Color currentRingColor = Color.white;
    [SerializeField] private Color lockedRingColor = new(0.55f, 0.57f, 0.6f);
    [Tooltip("How far the current stage's ring grows and shrinks, as a share of its size.")]
    [SerializeField, Range(0f, 0.5f)] private float pulseAmount = 0.06f;
    [Tooltip("Seconds for one full grow -> shrink cycle.")]
    [SerializeField, Min(0.1f)] private float pulsePeriod = 1.6f;

    [Header("Badge")]
    [Tooltip("World-space canvas over the island. Turned to face the camera every frame.")]
    [SerializeField] private Transform badge;
    [SerializeField] private GameObject starsGroup;
    [SerializeField] private Image[] starImages;
    [SerializeField] private Sprite starFilledSprite;
    [SerializeField] private Sprite starEmptySprite;
    [SerializeField] private GameObject playersGroup;
    [Tooltip("One slot per plane, in join order. Slots past the plane count are hidden.")]
    [SerializeField] private Image[] playerSlots;
    [SerializeField] private Color absentSlotColor = new(0.35f, 0.35f, 0.35f);
    [Tooltip("Filled image around the badge. Its fillAmount is the boarding progress.")]
    [SerializeField] private Image borderFill;

    public LevelConfig Level => level;
    public string DisplayName => level != null ? level.displayName : "Unconfigured Level";
    public string Description => level != null ? level.description : string.Empty;
    public bool IsUnlocked => level != null
        && (ProgressionService.Instance == null
            ? level.unlockedByDefault
            : ProgressionService.Instance.IsUnlocked(level));
    public bool IsCompleted => level != null
        && ProgressionService.Instance != null
        && ProgressionService.Instance.IsCompleted(level);
    public int BestStars => level != null && ProgressionService.Instance != null
        ? ProgressionService.Instance.GetBestStars(level)
        : 0;
    public int BestScore => level != null && ProgressionService.Instance != null
        ? ProgressionService.Instance.GetBestScore(level)
        : 0;
    public int LastScore => level != null && ProgressionService.Instance != null
        ? ProgressionService.Instance.GetLastScore(level)
        : 0;

    /// <summary>Centre of the landing ring, on the sea.</summary>
    public Vector3 RingCenter => landingRing != null ? landingRing.position : transform.position;

    /// <summary>Radius of the landing ring in metres, after any scaling.</summary>
    public float RingRadius => landingRing != null
        ? landingRing.rect.width * 0.5f * landingRing.lossyScale.x
        : 0f;

    /// <summary>0 = nobody boarding, 1 = full. Driven by <see cref="MapController"/>.</summary>
    public float BoardingProgress { get; private set; }

    private Transform cameraTransform;
    private bool pulsing;

    private void OnEnable()
    {
        if (ProgressionService.Instance != null)
            ProgressionService.Instance.ProgressChanged += Refresh;
        Refresh();
        SetBoardingProgress(0f);
    }

    private void OnDisable()
    {
        if (ProgressionService.Instance != null)
            ProgressionService.Instance.ProgressChanged -= Refresh;
    }

    public void Refresh()
    {
        bool unlocked = IsUnlocked;
        bool completed = unlocked && IsCompleted;

        if (ringImage != null)
            ringImage.color = !unlocked ? lockedRingColor
                : completed ? completedRingColor
                : currentRingColor;
        // The fill takes whichever of the two colours the ring isn't, so boarding shows on
        // a completed ring as well as on the current one.
        if (ringFill != null)
            ringFill.color = completed ? currentRingColor : completedRingColor;

        pulsing = unlocked && !completed;
        if (!pulsing && ringPulse != null)
            ringPulse.localScale = Vector3.one;

        if (badge != null)
            badge.gameObject.SetActive(unlocked);

        int stars = unlocked ? BestStars : 0;
        for (int i = 0; i < (starImages?.Length ?? 0); i++)
            if (starImages[i] != null)
                starImages[i].sprite = i < stars ? starFilledSprite : starEmptySprite;
    }

    /// <summary>
    /// Stars while no plane is parked here; otherwise one slot per plane, lit in that
    /// player's colour when their plane is on this node and grey when it isn't.
    /// </summary>
    public void ShowOccupancy(IReadOnlyList<MapPlane> planes)
    {
        bool anyHere = false;
        for (int i = 0; i < planes.Count; i++)
            if (planes[i].CurrentNode == this)
                anyHere = true;

        if (starsGroup != null && starsGroup.activeSelf == anyHere)
            starsGroup.SetActive(!anyHere);
        if (playersGroup != null && playersGroup.activeSelf != anyHere)
            playersGroup.SetActive(anyHere);
        if (!anyHere)
            return;

        for (int i = 0; i < (playerSlots?.Length ?? 0); i++)
        {
            Image slot = playerSlots[i];
            if (slot == null)
                continue;

            bool used = i < planes.Count;
            if (slot.gameObject.activeSelf != used)
                slot.gameObject.SetActive(used);
            if (used)
                slot.color = planes[i].CurrentNode == this ? planes[i].PlayerColor : absentSlotColor;
        }
    }

    public void SetBoardingProgress(float progress)
    {
        BoardingProgress = Mathf.Clamp01(progress);
        if (borderFill != null)
            borderFill.fillAmount = BoardingProgress;
        if (ringFill != null)
            ringFill.fillAmount = BoardingProgress;
    }

    private void LateUpdate()
    {
        if (pulsing && ringPulse != null)
        {
            float wave = Mathf.Sin(Time.time * (2f * Mathf.PI / pulsePeriod));
            ringPulse.localScale = Vector3.one * (1f + pulseAmount * wave);
        }

        if (badge == null || !badge.gameObject.activeSelf)
            return;

        // The map camera holds one fixed angle, so copying its rotation is the whole
        // billboard — same trick as LevelTicket.
        if (cameraTransform == null || !cameraTransform.gameObject.activeInHierarchy)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;
            cameraTransform = cam.transform;
        }

        badge.rotation = cameraTransform.rotation;
    }
}
