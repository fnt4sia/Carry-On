using System.Collections.Generic;
using UnityEngine;

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
    public List<LuggageData> luggageDataList;

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
}
