using System;

[Serializable]
public class GameResult
{
    public GameResult(
        string levelId,
        int score,
        int deliveryCount,
        int[] playerScores,
        int[] playerDeliveries,
        int stars)
    {
        LevelId = levelId;
        Score = score;
        DeliveryCount = deliveryCount;
        PlayerScores = playerScores ?? Array.Empty<int>();
        PlayerDeliveries = playerDeliveries ?? Array.Empty<int>();
        Stars = stars;
    }

    public string LevelId { get; }
    public int Score { get; }
    public int DeliveryCount { get; }
    public int[] PlayerScores { get; }
    public int[] PlayerDeliveries { get; }
    public int Stars { get; }
}
