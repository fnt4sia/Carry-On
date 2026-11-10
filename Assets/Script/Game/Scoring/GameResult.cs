using System;

[Serializable]
public class GameResult
{
    public GameResult(string levelId, int score, int deliveryCount, int stars)
    {
        LevelId = levelId;
        Score = score;
        DeliveryCount = deliveryCount;
        Stars = stars;
    }

    public string LevelId { get; }
    public int Score { get; }
    public int DeliveryCount { get; }
    public int Stars { get; }
}
