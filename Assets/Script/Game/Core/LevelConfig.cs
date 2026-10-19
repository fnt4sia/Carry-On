using System.Collections.Generic;
using UnityEngine;

// ScriptableObject defining a level's tuning: round timer, 1/2/3-star score thresholds,
// luggage spawner settings (pool, spawn interval, active cap), and points per delivery.
[CreateAssetMenu(fileName = "LevelConfig", menuName = "Carry On/Level Config")]
public class LevelConfig : ScriptableObject
{
    [Header("Identity & Progression")]
    [Tooltip("Stable save-data key. Do not change after shipping a level.")]
    public string levelId = "stage-1";
    [Tooltip("Scene name as listed in Build Settings.")]
    public string sceneName = "Stage_1";
    public string displayName = "Stage 1";
    [TextArea] public string description;
    public bool unlockedByDefault;
    public LevelConfig nextLevel;

    [Header("Stage Select Ticket")]
    [Tooltip("Flight number printed on the ticket stub, e.g. AB01.")]
    public string flightCode = "AB01";
    [Tooltip("Departure airport code, e.g. CGK.")]
    public string originCode = "CGK";
    [Tooltip("Departure city printed under the code, e.g. JAKARTA.")]
    public string originName = "JAKARTA";
    [Tooltip("Arrival airport code, e.g. DPS.")]
    public string destinationCode = "DPS";
    [Tooltip("Arrival city printed under the code, e.g. BALI.")]
    public string destinationName = "BALI";
    [Tooltip("Screenshot shown in the ticket's preview window. Optional.")]
    public Sprite previewImage;

    [Header("Timer")]
    public float gameTime = 120f;

    [Header("Star Thresholds (score needed for 1★ / 2★ / 3★)")]
    public int star1Score = 30;
    public int star2Score = 60;
    public int star3Score = 90;

    [Header("Spawner — Luggage Pool")]
    [Tooltip("Prefabs the belt cycles through, in order. Each prefab defines its own flight colour.")]
    public List<GameObject> luggagePrefabs;

    [Header("Spawner — Pacing")]
    [Tooltip("Seconds between spawns. The belt runs flat; there are no waves.")]
    public float spawnInterval = 1f;
    [Tooltip("Bags one spawner will keep in play at once. Bags never expire, so this is the only " +
             "thing stopping an ignored belt from burying the arena.")]
    public int maxActiveLuggage = 14;

    [Header("Scoring")]
    [Tooltip("Points for each bag a flight accepts.")]
    public int scoreCorrectDelivery = 10;

    [Header("Orders (dispatch gates)")]
    [Tooltip("Seconds a dispatch gate gives each order before the flight leaves without its bags.")]
    public float orderTime = 20f;
    [Tooltip("Coins for every whole second still on the order's timer when the flight is dispatched.")]
    public int coinsPerSecondLeft = 1;
    [Tooltip("Coin multiplier for the 1st, 2nd, 3rd... flight in a row. The last entry holds for " +
             "every flight after it. A failed flight starts the count over.")]
    public float[] comboMultipliers = { 1f, 1.5f, 2f };

    // streak = flights in a row including this one, so the first good flight is 1.
    public float ComboMultiplier(int streak)
    {
        if (comboMultipliers == null || comboMultipliers.Length == 0) return 1f;
        return comboMultipliers[Mathf.Clamp(streak - 1, 0, comboMultipliers.Length - 1)];
    }

    public int CalculateStars(int score)
    {
        if (score >= star3Score) return 3;
        if (score >= star2Score) return 2;
        if (score >= star1Score) return 1;
        return 0;
    }

    private void OnValidate()
    {
        gameTime = Mathf.Max(1f, gameTime);
        orderTime = Mathf.Max(1f, orderTime);
        spawnInterval = Mathf.Max(0.05f, spawnInterval);
        maxActiveLuggage = Mathf.Max(1, maxActiveLuggage);

        star1Score = Mathf.Max(0, star1Score);
        star2Score = Mathf.Max(star1Score, star2Score);
        star3Score = Mathf.Max(star2Score, star3Score);

        if (string.IsNullOrWhiteSpace(levelId))
            levelId = name.ToLowerInvariant().Replace(' ', '-');
    }
}
