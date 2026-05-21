using System.Collections.Generic;
using UnityEngine;

// ScriptableObject defining a level's tuning: round timer, 1/2/3-star score thresholds,
// luggage spawner settings (pool, wave size, intervals, bomb chance), and scoring
// values (correct delivery / missing-process / bomb / expired / wrong-gate penalties).
[CreateAssetMenu(fileName = "LevelConfig", menuName = "Carry On/Level Config")]
public class LevelConfig : ScriptableObject
{
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
    [Tooltip("0–1 chance that a wave contains a bomb (one of its luggage is replaced with a bomb-flagged one).")]
    [Range(0f, 1f)] public float bombWaveChance = 0.3f;

    [Header("Scoring")]
    [Tooltip("Points for a correctly processed non-bomb delivery.")]
    public int scoreCorrectDelivery = 10;
    [Tooltip("Penalty for delivering luggage with an unprocessed problem (unwashed/unwrapped).")]
    public int scoreMissingProcess = -5;
    [Tooltip("Penalty for delivering a bomb (any bomb, regardless of scan state).")]
    public int scoreBombDelivered = -15;
    [Tooltip("Penalty when a luggage's lifetime expires before delivery.")]
    public int scoreTimerExpired = -5;
    [Tooltip("Penalty for delivering luggage to the wrong numbered gate when a level has multiple delivery gates.")]
    public int scoreWrongGateDelivery = -5;
}
