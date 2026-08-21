using System.Collections.Generic;
using UnityEngine;

// ScriptableObject defining a level's tuning: round timer, 1/2/3-star score thresholds,
// luggage spawner settings (pool, wave size, intervals), and scoring values
// (correct delivery / missing-process / expired / wrong-gate penalties).
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
    [Tooltip("Prefabs that can be spawned. Each prefab already defines its own luggage behavior.")]
    public List<GameObject> luggagePrefabs;
    [Tooltip("Seconds before a spawned luggage expires if not delivered.")]
    public float luggageLifetime = 20f;

    [Header("Spawner — Waves")]
    [Tooltip("Seconds between the end of one wave and the start of the next.")]
    public float waveDelay = 10f;
    [Tooltip("How many luggage spawn per wave.")]
    public int luggagePerWave = 5;
    [Tooltip("Seconds between spawns within a single wave.")]
    public float intraWaveInterval = 1.5f;

    [Header("Scoring")]
    [Tooltip("Points for a correctly processed delivery at the right gate.")]
    public int scoreCorrectDelivery = 10;
    [Tooltip("Penalty for delivering luggage with an unprocessed problem (unwashed/unwrapped).")]
    public int scoreMissingProcess = -5;
    [Tooltip("Penalty when a luggage's lifetime expires before delivery.")]
    public int scoreTimerExpired = -5;
    [Tooltip("Penalty for delivering luggage to the wrong numbered gate when a level has multiple delivery gates.")]
    public int scoreWrongGateDelivery = -5;

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
        luggageLifetime = Mathf.Max(1f, luggageLifetime);
        waveDelay = Mathf.Max(0f, waveDelay);
        luggagePerWave = Mathf.Max(1, luggagePerWave);
        intraWaveInterval = Mathf.Max(0f, intraWaveInterval);

        star1Score = Mathf.Max(0, star1Score);
        star2Score = Mathf.Max(star1Score, star2Score);
        star3Score = Mathf.Max(star2Score, star3Score);

        if (string.IsNullOrWhiteSpace(levelId))
            levelId = name.ToLowerInvariant().Replace(' ', '-');
    }
}
